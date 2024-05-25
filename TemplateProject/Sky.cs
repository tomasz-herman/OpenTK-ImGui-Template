using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TemplateProject;

public class Sky : IDisposable
{
    private Mesh Quad { get; }
    private Shader Shader { get; }

    private Vector3 _a, _b, _c, _d, _e, _z;
    private const float NormalizedSunY = 1.15f;
    private float _turbidity = 4f;
    public Vector3 SunDir { get; set; } = Vector3.UnitY;

    public Sky()
    {
        var ibo = new IndexBuffer(new [] { 0, 1, 2, 3 }, 16, DrawElementsType.UnsignedInt, 4);
        Quad = new Mesh(PrimitiveType.TriangleStrip, ibo);
        Shader = new Shader(("sky.frag", ShaderType.FragmentShader),
            ("sky.vert", ShaderType.VertexShader));
    }

    public void Update()
    {
        var dir = SunDir.Normalized();
        float sunTheta = float.Acos(float.Clamp(dir.Y, 0.0f, 1.0f));

        // A.2 Skylight Distribution Coefficients and Zenith Values: compute Perez distribution coefficients
        _a = new Vector3(-0.0193f, -0.0167f,  0.1787f) * _turbidity + new Vector3(-0.2592f, -0.2608f, -1.4630f);
        _b = new Vector3(-0.0665f, -0.0950f, -0.3554f) * _turbidity + new Vector3( 0.0008f,  0.0092f,  0.4275f);
        _c = new Vector3(-0.0004f, -0.0079f, -0.0227f) * _turbidity + new Vector3( 0.2125f,  0.2102f,  5.3251f);
        _d = new Vector3(-0.0641f, -0.0441f,  0.1206f) * _turbidity + new Vector3(-0.8989f, -1.6537f, -2.5771f);
        _e = new Vector3(-0.0033f, -0.0109f, -0.0670f) * _turbidity + new Vector3( 0.0452f,  0.0529f,  0.3703f);

        // A.2 Skylight Distribution Coefficients and Zenith Values: compute zenith color
        _z.X = ZenithChromacity(new Vector4(0.00166f, -0.00375f, 0.00209f, 0), new Vector4(-0.02903f, 0.06377f, -0.03202f, 0.00394f), new Vector4(0.11693f, -0.21196f, 0.06052f, 0.25886f), sunTheta, _turbidity);
        _z.Y = ZenithChromacity(new Vector4(0.00275f, -0.00610f, 0.00317f, 0), new Vector4(-0.04214f, 0.08970f, -0.04153f, 0.00516f), new Vector4(0.15346f, -0.26756f, 0.06670f, 0.26688f), sunTheta, _turbidity);
        _z.Z = ZenithLuminance(sunTheta, _turbidity);
        _z.Z *= 1000; // conversion from kcd/m^2 to cd/m^2

        // 3.2 Skylight Model: pre-divide zenith color by distribution denominator
        _z.X /= Perez(0, sunTheta, _a.X, _b.X, _c.X, _d.X, _e.X);
        _z.Y /= Perez(0, sunTheta, _a.Y, _b.Y, _c.Y, _d.Y, _e.Y);
        _z.Z /= Perez(0, sunTheta, _a.Z, _b.Z, _c.Z, _d.Z, _e.Z);

        // For low dynamic range simulation, normalize luminance to have a fixed value for sun
        if (NormalizedSunY != 0) _z.Z = NormalizedSunY / Perez(sunTheta, 0, _a.Z, _b.Z, _c.Z, _d.Z, _e.Z);
    }

    public void Render(Camera camera)
    {
        Shader.Use();
        
        Shader.LoadFloat3("SunDir", SunDir.Normalized());
        Shader.LoadMatrix4("InvViewProj", camera.ProjectionViewMatrix.Inverted());
        Shader.LoadFloat3("p_A", _a);
        Shader.LoadFloat3("p_B", _b);
        Shader.LoadFloat3("p_C", _c);
        Shader.LoadFloat3("p_D", _d);
        Shader.LoadFloat3("p_E", _e);
        Shader.LoadFloat3("p_Z", _z);
        
        Quad.Bind();
        Quad.RenderIndexed();
    }
    
    private float ZenithChromacity(Vector4 c0, Vector4 c1, Vector4 c2, float sunTheta, float turbidity)
    {
        Vector4 thetav = new Vector4(sunTheta * sunTheta * sunTheta, sunTheta * sunTheta, sunTheta, 1);
        return Vector3.Dot(new Vector3(turbidity * turbidity, turbidity, 1), new Vector3(Vector4.Dot(thetav, c0), Vector4.Dot(thetav, c1), Vector4.Dot(thetav, c2)));
    }

    private float ZenithLuminance(float sunTheta, float turbidity)
    {
        float chi = (4.0f / 9.0f - turbidity / 120) * (float.Pi - 2 * sunTheta);
        return (4.0453f * turbidity - 4.9710f) * float.Tan(chi) - 0.2155f * turbidity + 2.4192f;
    }

    private float Perez(float theta, float gamma, float a, float b, float c, float d, float e)
    {
        return (1.0f + a * float.Exp(b / (float.Cos(theta) + 0.01f))) * (1.0f + c * float.Exp(d * gamma) + e * float.Cos(gamma) * float.Cos(gamma));
    }

    public void ShowGui()
    {
        ImGui.Begin("Sky");
        ImGui.DragFloat("Turbidity", ref _turbidity, 0.01f);
        var sunDir = new System.Numerics.Vector3(SunDir.X, SunDir.Y, SunDir.Z);
        ImGui.DragFloat3("Sun Dir", ref sunDir, 0.01f, -1, 1);
        sunDir /= sunDir.Length();
        SunDir = new Vector3(sunDir.X, sunDir.Y, sunDir.Z);
        SunDir.Normalize();
        ImGui.End();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Shader.Dispose();
        Quad.Dispose();
    }
}