using ImSharp;
using ObjectOrientedOpenGL.Canvas2D;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace Template2D;

public enum DemoMode
{
    Paint,
    GameOfLife,
    Plasma
}

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : CanvasGameWindow(gameWindowSettings, nativeWindowSettings, canvasWidth: 256, canvasHeight: 192)
{
    private DemoMode _mode = DemoMode.Paint;
    private Color4 _brushColor = new(1.0f, 0.45f, 0.15f, 1.0f);
    private int _brushRadius = 4;
    private bool _lifeRunning = true;
    private float _lifeTimer;
    private float _plasmaTime;
    private bool[,] _lifeScratch = new bool[256, 192];

    public static void Main(string[] args)
    {
        var gw = GameWindowSettings.Default;
        var nw = NativeWindowSettings.Default;
        using var program = new Program(gw, nw);
        program.Title = "OpenTK-ImGui-Template — 2D Pixel Canvas";
        program.ClientSize = new Vector2i(1260, 800);
        program.Run();
    }

    protected override void OnInitCanvas(IPixelCanvas canvas)
    {
        canvas.Fill(new Color4(0.08f, 0.09f, 0.12f, 1.0f));
        DrawWelcomeArt(canvas);
    }

    private void DrawWelcomeArt(IPixelCanvas canvas)
    {
        canvas.DrawRect(8, 8, canvas.Width - 16, canvas.Height - 16, new Color4(0.25f, 0.55f, 0.9f, 1f));
        canvas.FillCircle(canvas.Width / 2, canvas.Height / 2, 34, new Color4(0.95f, 0.4f, 0.2f, 1f));
        canvas.DrawCircle(canvas.Width / 2, canvas.Height / 2, 46, new Color4(0.2f, 0.9f, 0.7f, 1f));
        canvas.DrawLine(16, 16, canvas.Width - 17, canvas.Height - 17, new Color4(1f, 0.9f, 0.3f, 1f));
        canvas.DrawLine(canvas.Width - 17, 16, 16, canvas.Height - 17, new Color4(1f, 0.9f, 0.3f, 1f));
    }

    protected override void OnUpdateCanvas(IPixelCanvas canvas, float dt)
    {
        switch (_mode)
        {
            case DemoMode.Paint:
                UpdatePaint(canvas);
                break;
            case DemoMode.GameOfLife:
                UpdatePaint(canvas);
                if (_lifeRunning)
                {
                    _lifeTimer += dt;
                    if (_lifeTimer >= 0.08f)
                    {
                        _lifeTimer = 0f;
                        StepGameOfLife(canvas);
                    }
                }
                break;
            case DemoMode.Plasma:
                _plasmaTime += dt;
                RenderPlasma(canvas, _plasmaTime);
                break;
        }
    }

    private void UpdatePaint(IPixelCanvas canvas)
    {
        if (!IsCanvasHovered || HoveredPixel is not { } p) return;

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var col = _mode == DemoMode.GameOfLife ? Color4.White : _brushColor;
            int r = _mode == DemoMode.GameOfLife ? 1 : _brushRadius;
            if (r <= 1) canvas.SetPixel(p.X, p.Y, col);
            else canvas.FillCircle(p.X, p.Y, r, col);
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            var bg = new Color4(0.08f, 0.09f, 0.12f, 1.0f);
            if (_brushRadius <= 1) canvas.SetPixel(p.X, p.Y, bg);
            else canvas.FillCircle(p.X, p.Y, _brushRadius, bg);
        }
    }

    private void SeedGameOfLife(IPixelCanvas canvas)
    {
        canvas.Fill(new Color4(0.05f, 0.06f, 0.09f, 1.0f));
        for (int y = 0; y < canvas.Height; y++)
        for (int x = 0; x < canvas.Width; x++)
        {
            if (Random.Shared.NextSingle() < 0.25f)
                canvas.SetPixel(x, y, Color4.White);
        }
    }

    private void StepGameOfLife(IPixelCanvas canvas)
    {
        if (_lifeScratch.GetLength(0) != canvas.Width || _lifeScratch.GetLength(1) != canvas.Height)
            _lifeScratch = new bool[canvas.Width, canvas.Height];

        int w = canvas.Width;
        int h = canvas.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool alive = canvas.GetPixel(x, y).R > 0.5f;
            int neighbors = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = (x + dx + w) % w;
                int ny = (y + dy + h) % h;
                if (canvas.GetPixel(nx, ny).R > 0.5f)
                    neighbors++;
            }
            _lifeScratch[x, y] = alive ? (neighbors is 2 or 3) : (neighbors == 3);
        }

        var deadCol = new Color4(0.05f, 0.06f, 0.09f, 1.0f);
        var aliveCol = new Color4(0.35f, 0.95f, 0.65f, 1.0f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            canvas.SetPixel(x, y, _lifeScratch[x, y] ? aliveCol : deadCol);
    }

    private static void RenderPlasma(IPixelCanvas canvas, float t)
    {
        int w = canvas.Width;
        int h = canvas.Height;
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

    protected override void BuildCanvasGui()
    {
        ImGui.SetNextWindowPos(new ImVec2(16, 16), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(268, 380), ImGuiCond.FirstUseEver);

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
        if (ImGui.RadioButton("Pixel Paint", _mode == DemoMode.Paint))
        {
            _mode = DemoMode.Paint;
            Canvas.Fill(new Color4(0.08f, 0.09f, 0.12f, 1.0f));
            DrawWelcomeArt(Canvas);
        }
        if (ImGui.RadioButton("Game of Life", _mode == DemoMode.GameOfLife))
        {
            _mode = DemoMode.GameOfLife;
            SeedGameOfLife(Canvas);
        }
        if (ImGui.RadioButton("CPU Plasma", _mode == DemoMode.Plasma))
        {
            _mode = DemoMode.Plasma;
        }

        ImGui.Separator();

        if (_mode == DemoMode.Paint)
        {
            ImGui.TextDisabled("LMB: Draw  |  RMB: Erase");
            Span<float> col = stackalloc float[] { _brushColor.R, _brushColor.G, _brushColor.B, _brushColor.A };
            if (ImGui.ColorEdit4("Brush", col))
                _brushColor = new Color4(col[0], col[1], col[2], col[3]);
            ImGui.SliderInt("Radius", ref _brushRadius, 1, 20);
            if (ImGui.Button("Clear"))
                Canvas.Fill(new Color4(0.08f, 0.09f, 0.12f, 1.0f));
            ImGui.SameLine();
            if (ImGui.Button("Sample Art"))
                DrawWelcomeArt(Canvas);
        }
        else if (_mode == DemoMode.GameOfLife)
        {
            ImGui.Checkbox("Running", ref _lifeRunning);
            ImGui.SameLine();
            if (ImGui.Button("Step"))
                StepGameOfLife(Canvas);
            if (ImGui.Button("Randomize"))
                SeedGameOfLife(Canvas);
        }

        ImGui.Separator();
        bool nearest = Canvas.NearestFilter;
        if (ImGui.Checkbox("Nearest Filter (Crisp Pixels)", ref nearest))
            Canvas.NearestFilter = nearest;

        ImGui.End();
    }
}
