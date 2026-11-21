using System.Collections.Concurrent;
using ObjectOrientedOpenGL.Graphics;
using StbImageSharp;

namespace ObjectOrientedOpenGL.Core;

public readonly record struct AssetLoadProgress(string Label, string Stage, float Fraction);

/// <summary>
/// Handle tracking an asynchronous asset load job (background CPU decode + main-thread OpenGL upload).
/// </summary>
public sealed class AssetHandle<T> where T : class
{
    private volatile AssetLoadProgressBox _progressBox;

    public string Name { get; }
    public bool IsCompleted { get; internal set; }
    public bool IsFaulted { get; internal set; }
    public bool IsCanceled { get; internal set; }
    public Exception? Error { get; internal set; }
    public T? Asset { get; internal set; }
    public AssetLoadProgress Progress => _progressBox.Value;

    public event Action<T>? OnLoaded;
    public event Action<Exception>? OnFailed;

    internal AssetHandle(string name)
    {
        Name = name;
        _progressBox = new AssetLoadProgressBox(new AssetLoadProgress(name, "Queued...", 0f));
    }

    internal void ReportProgress(AssetLoadProgress progress) =>
        _progressBox = new AssetLoadProgressBox(progress);

    internal void Complete(T asset)
    {
        Asset = asset;
        IsCompleted = true;
        ReportProgress(new AssetLoadProgress(Name, "Loaded", 1f));
        OnLoaded?.Invoke(asset);
    }

    internal void Fail(Exception ex)
    {
        Error = ex;
        IsFaulted = true;
        IsCompleted = true;
        ReportProgress(new AssetLoadProgress(Name, $"Failed: {ex.Message}", 1f));
        OnFailed?.Invoke(ex);
    }

    internal void Cancel()
    {
        IsCanceled = true;
        IsCompleted = true;
    }

    private sealed class AssetLoadProgressBox(AssetLoadProgress value)
    {
        public readonly AssetLoadProgress Value = value;
    }
}

/// <summary>
/// Thread-safe asynchronous asset loader: parses &amp; decodes glTF models and textures on worker threads,
/// then uploads GPU resources on the OpenGL main thread via <see cref="ProcessMainThreadUploads"/>.
/// </summary>
public sealed class AssetLoader : IDisposable
{
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private readonly List<Func<AssetLoadProgress?>> _activeJobs = [];
    private CancellationTokenSource _cts = new();

    public bool IsLoading
    {
        get
        {
            lock (_activeJobs) return _activeJobs.Count > 0;
        }
    }

    public int ActiveJobCount
    {
        get
        {
            lock (_activeJobs) return _activeJobs.Count;
        }
    }

    /// <summary>
    /// Returns progress snapshots for all currently active asynchronous asset loads.
    /// </summary>
    public List<AssetLoadProgress> GetActiveProgress()
    {
        var result = new List<AssetLoadProgress>();
        lock (_activeJobs)
        {
            for (int i = _activeJobs.Count - 1; i >= 0; i--)
            {
                var p = _activeJobs[i]();
                if (p.HasValue) result.Add(p.Value);
                else _activeJobs.RemoveAt(i);
            }
        }
        return result;
    }

    /// <summary>
    /// Asynchronously loads a <c>.gltf</c> or <c>.glb</c> model (CPU parse + parallel texture decode on worker threads,
    /// OpenGL upload on main thread when <see cref="ProcessMainThreadUploads"/> is called).
    /// </summary>
    public AssetHandle<Model> LoadModelAsync(string path, Action<Model>? onLoaded = null, Action<Exception>? onFailed = null)
    {
        string name = Path.GetFileName(path);
        var handle = new AssetHandle<Model>(name);
        if (onLoaded is not null) handle.OnLoaded += onLoaded;
        if (onFailed is not null) handle.OnFailed += onFailed;

        var token = _cts.Token;
        TrackJob(handle);

        var progress = new DirectProgress<AssetLoadProgress>(p => handle.ReportProgress(p));

        _ = Task.Run(async () =>
        {
            try
            {
                var prepared = await GltfModelLoader.PrepareAsync(path, progress, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    handle.Cancel();
                    return;
                }

                _mainThreadQueue.Enqueue(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        handle.Cancel();
                        return;
                    }
                    try
                    {
                        handle.ReportProgress(new AssetLoadProgress(name, "Uploading to GPU...", 0.99f));
                        var model = GltfModelLoader.UploadPrepared(prepared);
                        handle.Complete(model);
                    }
                    catch (Exception ex)
                    {
                        handle.Fail(ex);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                handle.Cancel();
            }
            catch (Exception ex)
            {
                _mainThreadQueue.Enqueue(() => handle.Fail(ex));
            }
        }, token);

        return handle;
    }

    /// <summary>
    /// Asynchronously loads a 2D image texture from disk (CPU decode on worker thread, GL upload on main thread).
    /// </summary>
    public AssetHandle<Texture> LoadTextureAsync(
        string path,
        Texture.Options? options = null,
        bool generateMipmaps = true,
        Action<Texture>? onLoaded = null,
        Action<Exception>? onFailed = null)
    {
        string name = Path.GetFileName(path);
        var handle = new AssetHandle<Texture>(name);
        if (onLoaded is not null) handle.OnLoaded += onLoaded;
        if (onFailed is not null) handle.OnFailed += onFailed;

        var token = _cts.Token;
        TrackJob(handle);

        _ = Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                handle.ReportProgress(new AssetLoadProgress(name, "Decoding texture...", 0.25f));
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var img = ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha);
                token.ThrowIfCancellationRequested();

                _mainThreadQueue.Enqueue(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        handle.Cancel();
                        return;
                    }
                    try
                    {
                        var tex = Texture.FromRgba(img.Data, img.Width, img.Height, options, generateMipmaps);
                        handle.Complete(tex);
                    }
                    catch (Exception ex)
                    {
                        handle.Fail(ex);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                handle.Cancel();
            }
            catch (Exception ex)
            {
                _mainThreadQueue.Enqueue(() => handle.Fail(ex));
            }
        }, token);

        return handle;
    }

    /// <summary>
    /// Executes pending OpenGL uploads on the main thread. Call once per frame from <c>OnUpdateFrame</c>.
    /// </summary>
    public int ProcessMainThreadUploads(int maxUploadsPerFrame = 2)
    {
        int processed = 0;
        while (processed < maxUploadsPerFrame && _mainThreadQueue.TryDequeue(out var work))
        {
            work();
            processed++;
        }

        // Prune finished jobs
        lock (_activeJobs)
        {
            for (int i = _activeJobs.Count - 1; i >= 0; i--)
            {
                if (_activeJobs[i]() is null)
                    _activeJobs.RemoveAt(i);
            }
        }

        return processed;
    }

    /// <summary>
    /// Cancels all in-flight asynchronous asset loads (e.g. before switching scenes).
    /// </summary>
    public void CancelAll()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        while (_mainThreadQueue.TryDequeue(out _)) { }
        lock (_activeJobs) _activeJobs.Clear();
    }

    private void TrackJob<T>(AssetHandle<T> handle) where T : class
    {
        lock (_activeJobs)
        {
            _activeJobs.Add(() => handle.IsCompleted ? null : handle.Progress);
        }
    }

    public void Dispose()
    {
        CancelAll();
        _cts.Dispose();
    }

    private sealed class DirectProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
