using ImGuiNET;

namespace ObjectOrientedOpenGL.Extra;

public class FpsCounter
{
    private Overlay Overlay { get; }
    public double Fps { get; private set; }
    public double UpdateInterval { get; set; }

    public FpsCounter(double updateInterval = 1.0)
    {
        UpdateInterval = updateInterval;
        Overlay = new Overlay(new Vector2tk(-10, 10), () => { ImGui.TextUnformatted($"{Fps:##.0}"); }, Anchor.TopRight);
    }

    private double _accumulator;
    private int _frames;
    public void Update(double dt)
    {
        _accumulator += dt;
        _frames++;

        if (_accumulator >= UpdateInterval)
        {
            Fps = _frames / _accumulator;
            _accumulator = 0.0;
            _frames = 0;
        }
    }

    public void Render()
    {
        Overlay.Render();
    }
}