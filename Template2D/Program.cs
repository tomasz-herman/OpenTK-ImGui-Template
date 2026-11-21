using ImSharp;
using ObjectOrientedOpenGL.Canvas2D;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace Template2D;

public enum DemoMode
{
    GameOfLife,
    FallingSand,
    Paint,
    Plasma
}

public enum SandElement : byte
{
    Empty = 0,
    Sand = 1,
    Water = 2,
    Stone = 3
}

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : CanvasGameWindow(gameWindowSettings, nativeWindowSettings, canvasWidth: 160, canvasHeight: 120)
{
    private static readonly Color4 BgColor = new(0.06f, 0.07f, 0.10f, 1.0f);

    private DemoMode _mode = DemoMode.GameOfLife;
    private Vector2i? _lastDrawPixel;

    // Paint state
    private Color4 _brushColor = new(1.0f, 0.45f, 0.15f, 1.0f);
    private int _brushRadius = 3;
    private bool _fillBucketMode;

    // Game of Life state
    private bool[,] _lifeA = new bool[160, 120];
    private bool[,] _lifeB = new bool[160, 120];
    private byte[,] _lifeAge = new byte[160, 120];
    private bool _lifeRunning = true;
    private bool _lifeWrap = true;
    private float _lifeStepsPerSec = 18f;
    private float _lifeTimer;
    private int _lifeGeneration;
    private int _lifeAliveCount;

    // Falling Sand state
    private SandElement[,] _sand = new SandElement[160, 120];
    private SandElement _selectedElement = SandElement.Sand;
    private int _sandBrushRadius = 4;
    private float _sandStepsPerSec = 45f;
    private float _sandTimer;

    // Plasma state
    private float _plasmaTime;
    private float _plasmaSpeed = 1.5f;

    public static void Main(string[] args)
    {
        var gw = GameWindowSettings.Default;
        var nw = NativeWindowSettings.Default;
        using var program = new Program(gw, nw);
        program.Title = "OpenTK-ImGui-Template — 2D Pixel Canvas";
        program.ClientSize = new Vector2i(1260, 820);
        program.Run();
    }

    protected override void OnInitCanvas(IPixelCanvas canvas)
    {
        EnsureBuffers(canvas.Width, canvas.Height);
        SeedGosperGliderGun(canvas);
    }

    private void EnsureBuffers(int w, int h)
    {
        if (_lifeA.GetLength(0) != w || _lifeA.GetLength(1) != h)
        {
            _lifeA = new bool[w, h];
            _lifeB = new bool[w, h];
            _lifeAge = new byte[w, h];
            _sand = new SandElement[w, h];
        }
    }

    protected override void OnUpdateCanvas(IPixelCanvas canvas, float dt)
    {
        EnsureBuffers(canvas.Width, canvas.Height);

        bool mouseDown = IsCanvasHovered && HoveredPixel.HasValue &&
                         (ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseDown(ImGuiMouseButton.Right));
        if (!mouseDown)
            _lastDrawPixel = null;

        switch (_mode)
        {
            case DemoMode.GameOfLife:
                UpdateLifeInput(canvas);
                if (_lifeRunning)
                {
                    _lifeTimer += dt;
                    float stepInterval = 1.0f / MathF.Max(_lifeStepsPerSec, 0.5f);
                    int maxSteps = 4;
                    while (_lifeTimer >= stepInterval && maxSteps-- > 0)
                    {
                        _lifeTimer -= stepInterval;
                        StepGameOfLife(canvas);
                    }
                }
                break;

            case DemoMode.FallingSand:
                UpdateSandInput();
                _sandTimer += dt;
                float sandInterval = 1.0f / MathF.Max(_sandStepsPerSec, 1f);
                int sandSteps = 0;
                while (_sandTimer >= sandInterval && sandSteps < 3)
                {
                    _sandTimer -= sandInterval;
                    StepFallingSand();
                    sandSteps++;
                }
                RenderFallingSand(canvas);
                break;

            case DemoMode.Paint:
                UpdatePaintInput(canvas);
                break;

            case DemoMode.Plasma:
                _plasmaTime += dt * _plasmaSpeed;
                RenderPlasma(canvas, _plasmaTime);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Game of Life
    // -------------------------------------------------------------------------

    private void UpdateLifeInput(IPixelCanvas canvas)
    {
        if (!IsCanvasHovered || HoveredPixel is not { } p) return;

        bool lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        if (!lmb && !rmb) return;

        bool setAlive = lmb;
        var from = _lastDrawPixel ?? p;
        PlotLine(from.X, from.Y, p.X, p.Y, (x, y) =>
        {
            if (!canvas.InBounds(x, y)) return;
            _lifeA[x, y] = setAlive;
            _lifeAge[x, y] = setAlive ? (byte)1 : (byte)0;
        });
        _lastDrawPixel = p;
        RenderGameOfLife(canvas);
    }

    private void ClearGameOfLife(IPixelCanvas canvas)
    {
        Array.Clear(_lifeA);
        Array.Clear(_lifeAge);
        _lifeGeneration = 0;
        _lifeAliveCount = 0;
        RenderGameOfLife(canvas);
    }

    private void SeedRandomLife(IPixelCanvas canvas, float density = 0.24f)
    {
        int w = canvas.Width, h = canvas.Height;
        _lifeGeneration = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool alive = Random.Shared.NextSingle() < density;
            _lifeA[x, y] = alive;
            _lifeAge[x, y] = alive ? (byte)1 : (byte)0;
        }
        RenderGameOfLife(canvas);
    }

    private void SeedGosperGliderGun(IPixelCanvas canvas)
    {
        ClearGameOfLife(canvas);
        int ox = Math.Max(4, canvas.Width / 2 - 22);
        int oy = Math.Max(4, canvas.Height / 2 - 14);
        (int x, int y)[] cells =
        [
            (0,4),(0,5),(1,4),(1,5),
            (10,4),(10,5),(10,6),(11,3),(11,7),(12,2),(12,8),(13,2),(13,8),
            (14,5),(15,3),(15,7),(16,4),(16,5),(16,6),(17,5),
            (20,2),(20,3),(20,4),(21,2),(21,3),(21,4),(22,1),(22,5),
            (24,0),(24,1),(24,5),(24,6),
            (34,2),(34,3),(35,2),(35,3)
        ];
        foreach (var (cx, cy) in cells)
        {
            int px = ox + cx;
            int py = oy + cy;
            if (canvas.InBounds(px, py))
            {
                _lifeA[px, py] = true;
                _lifeAge[px, py] = 1;
            }
        }
        RenderGameOfLife(canvas);
    }

    private void SeedAcorn(IPixelCanvas canvas)
    {
        ClearGameOfLife(canvas);
        int ox = canvas.Width / 2 - 3;
        int oy = canvas.Height / 2 - 1;
        (int x, int y)[] cells = [(1,0),(3,1),(0,2),(1,2),(4,2),(5,2),(6,2)];
        foreach (var (cx, cy) in cells)
        {
            int px = ox + cx;
            int py = oy + cy;
            if (canvas.InBounds(px, py))
            {
                _lifeA[px, py] = true;
                _lifeAge[px, py] = 1;
            }
        }
        RenderGameOfLife(canvas);
    }

    private void StepGameOfLife(IPixelCanvas canvas)
    {
        int w = canvas.Width, h = canvas.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int neighbors = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (_lifeWrap)
                {
                    nx = (nx + w) % w;
                    ny = (ny + h) % h;
                }
                else if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                {
                    continue;
                }
                if (_lifeA[nx, ny]) neighbors++;
            }

            bool alive = _lifeA[x, y];
            bool next = alive ? (neighbors is 2 or 3) : (neighbors == 3);
            _lifeB[x, y] = next;
            if (next)
                _lifeAge[x, y] = (byte)Math.Min(_lifeAge[x, y] + 1, 255);
            else
                _lifeAge[x, y] = 0;
        }

        (_lifeA, _lifeB) = (_lifeB, _lifeA);
        _lifeGeneration++;
        RenderGameOfLife(canvas);
    }

    private void RenderGameOfLife(IPixelCanvas canvas)
    {
        int w = canvas.Width, h = canvas.Height;
        int aliveCount = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (_lifeA[x, y])
            {
                aliveCount++;
                float ageT = Math.Clamp((_lifeAge[x, y] - 1) / 30f, 0f, 1f);
                // Fresh cells: bright lime-green -> mature cells: cyan-teal
                var col = new Color4(
                    0.38f * (1f - ageT) + 0.12f * ageT,
                    0.98f * (1f - ageT) + 0.72f * ageT,
                    0.45f * (1f - ageT) + 0.95f * ageT,
                    1.0f);
                canvas.SetPixel(x, y, col);
            }
            else
            {
                canvas.SetPixel(x, y, BgColor);
            }
        }
        _lifeAliveCount = aliveCount;
    }

    // -------------------------------------------------------------------------
    // Falling Sand
    // -------------------------------------------------------------------------

    private void UpdateSandInput()
    {
        if (!IsCanvasHovered || HoveredPixel is not { } p) return;
        bool lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        if (!lmb && !rmb) return;

        var elem = rmb ? SandElement.Empty : _selectedElement;
        int w = _sand.GetLength(0), h = _sand.GetLength(1);
        int r2 = _sandBrushRadius * _sandBrushRadius;
        for (int dy = -_sandBrushRadius; dy <= _sandBrushRadius; dy++)
        for (int dx = -_sandBrushRadius; dx <= _sandBrushRadius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            int nx = p.X + dx, ny = p.Y + dy;
            if ((uint)nx < (uint)w && (uint)ny < (uint)h)
                _sand[nx, ny] = elem;
        }
    }

    private void StepFallingSand()
    {
        int w = _sand.GetLength(0), h = _sand.GetLength(1);

        for (int y = h - 2; y >= 0; y--)
        {
            bool leftToRight = (y & 1) == 0;
            for (int i = 0; i < w; i++)
            {
                int x = leftToRight ? i : (w - 1 - i);
                var e = _sand[x, y];
                if (e == SandElement.Sand)
                {
                    if (_sand[x, y + 1] is SandElement.Empty or SandElement.Water)
                    {
                        (_sand[x, y + 1], _sand[x, y]) = (_sand[x, y], _sand[x, y + 1]);
                    }
                    else
                    {
                        int dir = Random.Shared.Next(2) == 0 ? -1 : 1;
                        if (x + dir >= 0 && x + dir < w && _sand[x + dir, y + 1] is SandElement.Empty or SandElement.Water)
                            (_sand[x + dir, y + 1], _sand[x, y]) = (_sand[x, y], _sand[x + dir, y + 1]);
                        else if (x - dir >= 0 && x - dir < w && _sand[x - dir, y + 1] is SandElement.Empty or SandElement.Water)
                            (_sand[x - dir, y + 1], _sand[x, y]) = (_sand[x, y], _sand[x - dir, y + 1]);
                    }
                }
                else if (e == SandElement.Water)
                {
                    if (_sand[x, y + 1] == SandElement.Empty)
                    {
                        _sand[x, y + 1] = SandElement.Water;
                        _sand[x, y] = SandElement.Empty;
                    }
                    else
                    {
                        int dir = Random.Shared.Next(2) == 0 ? -1 : 1;
                        if (x + dir >= 0 && x + dir < w && _sand[x + dir, y + 1] == SandElement.Empty)
                        {
                            _sand[x + dir, y + 1] = SandElement.Water;
                            _sand[x, y] = SandElement.Empty;
                        }
                        else if (x + dir >= 0 && x + dir < w && _sand[x + dir, y] == SandElement.Empty)
                        {
                            _sand[x + dir, y] = SandElement.Water;
                            _sand[x, y] = SandElement.Empty;
                        }
                        else if (x - dir >= 0 && x - dir < w && _sand[x - dir, y] == SandElement.Empty)
                        {
                            _sand[x - dir, y] = SandElement.Water;
                            _sand[x, y] = SandElement.Empty;
                        }
                    }
                }
            }
        }
    }

    private void RenderFallingSand(IPixelCanvas canvas)
    {
        int w = canvas.Width, h = canvas.Height;
        var sandCol = new Color4(0.92f, 0.78f, 0.42f, 1f);
        var waterCol = new Color4(0.22f, 0.58f, 0.96f, 1f);
        var stoneCol = new Color4(0.45f, 0.47f, 0.52f, 1f);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var col = _sand[x, y] switch
            {
                SandElement.Sand => sandCol,
                SandElement.Water => waterCol,
                SandElement.Stone => stoneCol,
                _ => BgColor
            };
            canvas.SetPixel(x, y, col);
        }
    }

    // -------------------------------------------------------------------------
    // Pixel Paint
    // -------------------------------------------------------------------------

    private void UpdatePaintInput(IPixelCanvas canvas)
    {
        if (!IsCanvasHovered || HoveredPixel is not { } p) return;

        if (_fillBucketMode && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            FloodFill(canvas, p.X, p.Y, _brushColor);
            return;
        }

        bool lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        if (!lmb && !rmb) return;

        var col = rmb ? BgColor : _brushColor;
        var from = _lastDrawPixel ?? p;
        PlotLine(from.X, from.Y, p.X, p.Y, (x, y) =>
        {
            if (_brushRadius <= 1) canvas.SetPixel(x, y, col);
            else canvas.FillCircle(x, y, _brushRadius, col);
        });
        _lastDrawPixel = p;
    }

    private static void FloodFill(IPixelCanvas canvas, int startX, int startY, Color4 fillCol)
    {
        var target = Pixel.FromColor4(canvas.GetPixel(startX, startY));
        var replacement = Pixel.FromColor4(fillCol);
        if (target == replacement) return;

        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        canvas.SetPixel(startX, startY, fillCol);

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            TryEnqueue(x + 1, y);
            TryEnqueue(x - 1, y);
            TryEnqueue(x, y + 1);
            TryEnqueue(x, y - 1);
        }

        void TryEnqueue(int nx, int ny)
        {
            if (!canvas.InBounds(nx, ny)) return;
            if (Pixel.FromColor4(canvas.GetPixel(nx, ny)) != target) return;
            canvas.SetPixel(nx, ny, fillCol);
            queue.Enqueue((nx, ny));
        }
    }

    private static void PlotLine(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            plot(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // -------------------------------------------------------------------------
    // CPU Plasma
    // -------------------------------------------------------------------------

    private static void RenderPlasma(IPixelCanvas canvas, float t)
    {
        int w = canvas.Width, h = canvas.Height;
        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;
                float v = MathF.Sin(nx * 10f + t)
                        + MathF.Sin((ny * 10f + t) * 0.5f)
                        + MathF.Sin((nx + ny) * 10f + t)
                        + MathF.Sin(MathF.Sqrt((nx - 0.5f) * (nx - 0.5f) + (ny - 0.5f) * (ny - 0.5f)) * 16f - t * 2f);
                float r = 0.5f + 0.5f * MathF.Sin(v * MathF.PI);
                float g = 0.5f + 0.5f * MathF.Sin(v * MathF.PI + 2.094f);
                float b = 0.5f + 0.5f * MathF.Sin(v * MathF.PI + 4.188f);
                canvas.SetPixel(x, y, new Color4(r, g, b, 1.0f));
            }
        }
    }

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------

    protected override void BuildCanvasGui()
    {
        ImGui.SetNextWindowPos(new ImVec2(16, 16), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(272, 460), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("2D Canvas Controls"))
        {
            ImGui.End();
            return;
        }

        ImGui.Text($"Canvas: {Canvas.Width} x {Canvas.Height}");
        if (HoveredPixel is { } p)
        {
            var c = Canvas.GetPixel(p.X, p.Y);
            ImGui.Text($"Pixel ({p.X}, {p.Y}): ({c.R:F2}, {c.G:F2}, {c.B:F2})");
        }
        else
        {
            ImGui.TextDisabled("Hover canvas to inspect pixels");
        }

        ImGui.Separator();
        if (ImGui.RadioButton("Game of Life", _mode == DemoMode.GameOfLife))
        {
            _mode = DemoMode.GameOfLife;
            RenderGameOfLife(Canvas);
        }
        if (ImGui.RadioButton("Falling Sand", _mode == DemoMode.FallingSand))
        {
            _mode = DemoMode.FallingSand;
        }
        if (ImGui.RadioButton("Pixel Paint", _mode == DemoMode.Paint))
        {
            _mode = DemoMode.Paint;
            Canvas.Fill(BgColor);
        }
        if (ImGui.RadioButton("CPU Plasma", _mode == DemoMode.Plasma))
        {
            _mode = DemoMode.Plasma;
        }

        ImGui.Separator();

        switch (_mode)
        {
            case DemoMode.GameOfLife:
                ImGui.TextDisabled("LMB: Draw cells  |  RMB: Erase");
                ImGui.Checkbox("Running", ref _lifeRunning);
                ImGui.SameLine();
                ImGui.Checkbox("Wrap", ref _lifeWrap);
                ImGui.SliderFloat("Speed", ref _lifeStepsPerSec, 1f, 60f, "%.0f gen/s");
                if (ImGui.Button("Step")) StepGameOfLife(Canvas);
                ImGui.SameLine();
                if (ImGui.Button("Clear")) ClearGameOfLife(Canvas);
                if (ImGui.Button("Glider Gun")) SeedGosperGliderGun(Canvas);
                ImGui.SameLine();
                if (ImGui.Button("Acorn")) SeedAcorn(Canvas);
                ImGui.SameLine();
                if (ImGui.Button("Random")) SeedRandomLife(Canvas);
                ImGui.TextDisabled($"Gen: {_lifeGeneration}  |  Alive: {_lifeAliveCount}");
                break;

            case DemoMode.FallingSand:
                ImGui.TextDisabled("LMB: Spawn  |  RMB: Erase");
                if (ImGui.RadioButton("Sand", _selectedElement == SandElement.Sand)) _selectedElement = SandElement.Sand;
                ImGui.SameLine();
                if (ImGui.RadioButton("Water", _selectedElement == SandElement.Water)) _selectedElement = SandElement.Water;
                ImGui.SameLine();
                if (ImGui.RadioButton("Stone", _selectedElement == SandElement.Stone)) _selectedElement = SandElement.Stone;
                ImGui.SliderInt("Brush", ref _sandBrushRadius, 1, 14);
                ImGui.SliderFloat("Sim Speed", ref _sandStepsPerSec, 5f, 120f, "%.0f step/s");
                if (ImGui.Button("Clear Sand")) Array.Clear(_sand);
                break;

            case DemoMode.Paint:
                ImGui.TextDisabled("LMB: Draw  |  RMB: Erase");
                Span<float> col = stackalloc float[] { _brushColor.R, _brushColor.G, _brushColor.B, _brushColor.A };
                if (ImGui.ColorEdit4("Color", col))
                    _brushColor = new Color4(col[0], col[1], col[2], col[3]);
                ImGui.SliderInt("Radius", ref _brushRadius, 1, 20);
                ImGui.Checkbox("Fill Bucket", ref _fillBucketMode);
                if (ImGui.Button("Clear Canvas")) Canvas.Fill(BgColor);
                break;

            case DemoMode.Plasma:
                ImGui.SliderFloat("Speed", ref _plasmaSpeed, 0f, 5f, "%.2fx");
                break;
        }

        ImGui.Separator();
        bool nearest = Canvas.NearestFilter;
        if (ImGui.Checkbox("Nearest Filter (Crisp Pixels)", ref nearest))
            Canvas.NearestFilter = nearest;

        ImGui.Text("Resolution:");
        if (ImGui.SmallButton("128x96")) ResizeCanvas(128, 96);
        ImGui.SameLine();
        if (ImGui.SmallButton("160x120")) ResizeCanvas(160, 120);
        ImGui.SameLine();
        if (ImGui.SmallButton("256x192")) ResizeCanvas(256, 192);

        ImGui.End();
    }

    private void ResizeCanvas(int w, int h)
    {
        Canvas.Resize(w, h);
        EnsureBuffers(w, h);
        if (_mode == DemoMode.GameOfLife) SeedGosperGliderGun(Canvas);
        else Canvas.Fill(BgColor);
    }
}
