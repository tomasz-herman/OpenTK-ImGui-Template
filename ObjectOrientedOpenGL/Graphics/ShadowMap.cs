using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

/// <summary>
/// Directional sunlight shadow map (depth-only FBO + PCF shader support + skeletal skinning + alpha-mask cutout).
/// </summary>
public sealed class ShadowMap : IDisposable
{
    public int Resolution { get; }
    public int FboHandle { get; }
    public int DepthTextureHandle { get; }
    public Shader ShadowShader { get; }
    public Matrix4 LightSpaceMatrix { get; private set; } = Matrix4.Identity;

    private readonly int[] _prevViewport = new int[4];

    public ShadowMap(int resolution = 2048)
    {
        Resolution = resolution;

        GL.CreateTextures(TextureTarget.Texture2D, 1, out int tex);
        DepthTextureHandle = tex;
        GL.TextureStorage2D(tex, 1, SizedInternalFormat.DepthComponent24, resolution, resolution);
        GL.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        float[] border = [1f, 1f, 1f, 1f];
        GL.TextureParameter(tex, TextureParameterName.TextureBorderColor, border);

        GL.CreateFramebuffers(1, out int fbo);
        FboHandle = fbo;
        GL.NamedFramebufferTexture(fbo, FramebufferAttachment.DepthAttachment, tex, 0);
        GL.NamedFramebufferDrawBuffer(fbo, DrawBufferMode.None);
        GL.NamedFramebufferReadBuffer(fbo, ReadBufferMode.None);

        ShadowShader = new Shader(
            ("ObjectOrientedOpenGL.Resources.Shaders.shadow.vert", ShaderType.VertexShader),
            ("ObjectOrientedOpenGL.Resources.Shaders.shadow.frag", ShaderType.FragmentShader));
    }

    public void BeginPass(Vector3 sunDirection, Vector3 focusCenter, float orthoHalfExtent = 22f, float nearPlane = 0.5f, float farPlane = 80f)
    {
        GL.GetInteger(GetPName.Viewport, _prevViewport);

        var dir = sunDirection.LengthSquared > 1e-6f ? sunDirection.Normalized() : new Vector3(0.4f, 0.8f, 0.3f).Normalized();
        var lightPos = focusCenter + dir * (farPlane * 0.45f);
        var up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.98f ? Vector3.UnitZ : Vector3.UnitY;

        var lightView = Matrix4.LookAt(lightPos, focusCenter, up);
        var lightProj = Matrix4.CreateOrthographicOffCenter(
            -orthoHalfExtent, orthoHalfExtent,
            -orthoHalfExtent, orthoHalfExtent,
            nearPlane, farPlane);

        LightSpaceMatrix = lightView * lightProj;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, FboHandle);
        GL.Viewport(0, 0, Resolution, Resolution);
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.PolygonOffset(2.0f, 4.0f);

        ShadowShader.Use();
        ShadowShader.LoadMatrix4("uLightSpaceMatrix", LightSpaceMatrix);
        ShadowShader.LoadInteger("uBaseColorMap", 0);
    }

    public void DrawMesh(Mesh mesh, PbrMaterial material, Matrix4 worldTransform, ReadOnlySpan<Matrix4> bonePalette = default)
    {
        if (material.AlphaMode == AlphaMode.Blend)
            return;

        bool hasMaskTex = material.AlphaMode == AlphaMode.Mask && material.BaseColorTexture is not null;
        ShadowShader.LoadMatrix4("uModel", worldTransform);
        ShadowShader.LoadInteger("uAlphaMode", hasMaskTex ? 1 : 0);
        ShadowShader.LoadFloat("uAlphaCutoff", material.AlphaCutoff);
        ShadowShader.LoadFloat("uBaseColorAlpha", material.BaseColorFactor.W);

        if (hasMaskTex)
            material.BaseColorTexture!.ActivateUnit(0);

        if (bonePalette.Length > 0)
        {
            ShadowShader.LoadInteger("uHasSkinning", 1);
            int count = Math.Min(bonePalette.Length, 128);
            for (int i = 0; i < count; i++)
                ShadowShader.LoadMatrix4($"uJointMatrices[{i}]", bonePalette[i]);
        }
        else
        {
            ShadowShader.LoadInteger("uHasSkinning", 0);
        }

        if (material.DoubleSided) GL.Disable(EnableCap.CullFace);
        else GL.Enable(EnableCap.CullFace);

        mesh.Bind();
        mesh.RenderIndexed();
    }

    public void EndPass()
    {
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(_prevViewport[0], _prevViewport[1], _prevViewport[2], _prevViewport[3]);
    }

    public void BindDepthTexture(int unit = 5)
    {
        GL.BindTextureUnit(unit, DepthTextureHandle);
    }

    public void Dispose()
    {
        ShadowShader.Dispose();
        GL.DeleteFramebuffer(FboHandle);
        GL.DeleteTexture(DepthTextureHandle);
        GC.SuppressFinalize(this);
    }
}
