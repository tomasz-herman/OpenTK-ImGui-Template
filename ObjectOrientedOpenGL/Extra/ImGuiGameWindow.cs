using ImSharp;
using ImSharp.Backends;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ObjectOrientedOpenGL.Extra;

/// <summary>
/// OpenTK <see cref="GameWindow"/> integrated with <c>ImSharp</c> (Dear ImGui 1.93 docking,
/// ImPlot, and ImGuizmo) using native GLFW and OpenGL3 backends.
/// </summary>
public class ImGuiGameWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    protected ImGuiContextPtr ImGuiContext { get; private set; }
    protected ImPlotContextPtr ImPlotContext { get; private set; }
    public bool EnableDocking { get; set; } = true;
    public bool EnableViewports { get; set; } = false;
    public bool EnablePassthroughDockspace { get; set; } = true;

    protected override unsafe void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;

        ImGuiContext = ImGui.CreateContext();
        ImPlotContext = ImPlot.CreateContext();
        ImGuizmo.SetImGuiContext(ImGuiContext);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        if (EnableDocking)
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        if (EnableViewports)
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

        ImGui.StyleColorsDark();
        if (EnableViewports)
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 0.0f;
            style.AsRef().Colors[(int)ImGuiCol.WindowBg].w = 1.0f;
        }

        ImGuiImplGlfw.InitForOpenGL(WindowPtr, true);
        ImGuiImplOpenGL3.Init("#version 330 core");
    }

    protected override void OnUnload()
    {
        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplGlfw.Shutdown();
        if (!ImPlotContext.IsNull)
            ImPlot.DestroyContext(ImPlotContext);
        if (!ImGuiContext.IsNull)
            ImGui.DestroyContext(ImGuiContext);

        base.OnUnload();
    }

    protected virtual void BuildGuiLayout()
    {
    }

    protected unsafe void RenderGui()
    {
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplGlfw.NewFrame();
        ImGui.NewFrame();
        ImGuizmo.BeginFrame();

        var io = ImGui.GetIO();
        if (EnablePassthroughDockspace && (io.ConfigFlags & ImGuiConfigFlags.DockingEnable) != 0)
        {
            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
        }

        BuildGuiLayout();

        ImGui.Render();
        ImGuiImplOpenGL3.RenderDrawData();

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            Window* backup = GLFW.GetCurrentContext();
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
            GLFW.MakeContextCurrent(backup);
        }
    }
}
