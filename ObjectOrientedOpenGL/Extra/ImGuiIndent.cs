using ImGuiNET;

namespace ObjectOrientedOpenGL.Extra;

public readonly struct ImGuiIndent : IDisposable
{
    private float Value { get; }

    public ImGuiIndent(float value)
    {
        Value = value;
        ImGui.Indent(Value);
    }

    public void Dispose()
    {
        ImGui.Unindent(Value);
    }
}