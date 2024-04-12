using OpenTK.Graphics.OpenGL4;

namespace TemplateProject;

public class State : IDisposable
{
    private List<IAttribute> Attributes { get; } = new();

    public State(params IAttribute[] attribute)
    {
        Attributes.AddRange(attribute);
    }

    public State Save()
    {
        foreach (var attribute in Attributes)
        {
            attribute.SaveState();
        }
        return this;
    }

    public void Dispose()
    {
        foreach (var attribute in Attributes)
        {
            attribute.RestoreState();
            OpenGLUtils.CheckError();
        }
        GC.SuppressFinalize(this);
    }

    public interface IAttribute
    {
        public void SaveState();
        public void RestoreState();
    }

    public class Capability : IAttribute
    {
        public Capability(EnableCap cap)
        {
            Cap = cap;
        }

        private EnableCap Cap { get; }
        private bool Value { get; set; }

        public void SaveState()
        {
            Value = GL.IsEnabled(Cap);
        }

        public void RestoreState()
        {
            if (Value) GL.Enable(Cap);
            else GL.Disable(Cap);
        }
    }

    public class Texture2D : IAttribute
    {
        private int _activeTexture;
        private int _texture;

        public void SaveState()
        {
            GL.GetInteger(GetPName.ActiveTexture, out _activeTexture);
            GL.GetInteger(GetPName.TextureBinding2D, out _texture);
        }

        public void RestoreState()
        {
            GL.ActiveTexture((TextureUnit)_activeTexture);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
        }
    }

    public class Program : IAttribute
    {
        private int _program;

        public void SaveState()
        {
            GL.GetInteger(GetPName.CurrentProgram, out _program);
        }

        public void RestoreState()
        {
            if (_program == 0 || GL.IsProgram(_program)) GL.UseProgram(_program);
        }
    }

    public class ElementArrayBuffer : IAttribute
    {
        private int _buffer;

        public void SaveState()
        {
            GL.GetInteger(GetPName.ElementArrayBufferBinding, out _buffer);
        }

        public void RestoreState()
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _buffer);
        }
    }

    public class ArrayBuffer : IAttribute
    {
        private int _buffer;

        public void SaveState()
        {
            GL.GetInteger(GetPName.ArrayBufferBinding, out _buffer);
        }

        public void RestoreState()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, _buffer);
        }
    }

    public class VertexArrayObject : IAttribute
    {
        private int _array;

        public void SaveState()
        {
            GL.GetInteger(GetPName.VertexArrayBinding, out _array);
        }

        public void RestoreState()
        {
            GL.BindVertexArray(_array);
        }
    }

    public class Sampler : IAttribute
    {
        private int _sampler;
        private readonly int _unit;

        public Sampler(int unit)
        {
            _unit = unit;
        }

        public void SaveState()
        {
            GL.GetInteger(GetPName.SamplerBinding, out _sampler);
        }

        public void RestoreState()
        {
            GL.BindSampler(_unit, _sampler);
        }
    }

    public class Scissor : IAttribute
    {
        private readonly int[] _scissorBox = new int[4];
        private Capability ScissorCap { get; } = new(EnableCap.ScissorTest);

        public void SaveState()
        {
            GL.GetInteger(GetPName.ScissorBox, _scissorBox);
            ScissorCap.SaveState();
        }

        public void RestoreState()
        {
            ScissorCap.RestoreState();
            GL.Scissor(_scissorBox[0], _scissorBox[1], _scissorBox[2], _scissorBox[3]);
        }
    }

    public class PolygonMode : IAttribute
    {
        private readonly int[] _polygonMode = new int[2];

        public void SaveState()
        {
            GL.GetInteger(GetPName.PolygonMode, _polygonMode);
        }

        public void RestoreState()
        {
            GL.PolygonMode(MaterialFace.FrontAndBack, (OpenTK.Graphics.OpenGL4.PolygonMode)_polygonMode[0]);
        }
    }

    public class Viewport : IAttribute
    {
        private readonly int[] _viewport = new int[4];

        public void SaveState()
        {
            GL.GetInteger(GetPName.Viewport, _viewport);
        }

        public void RestoreState()
        {
            GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        }
    }

    public class Blend : IAttribute
    {
        private int _blendSrcRgb;
        private int _blendSrcAlpha;
        private int _blendDstRgb;
        private int _blendDstAlpha;
        private int _blendEquationRgb;
        private int _blendEquationAlpha;
        private Capability BlendCap { get; } = new(EnableCap.Blend);

        public void SaveState()
        {
            GL.GetInteger(GetPName.BlendSrcRgb, out _blendSrcRgb);
            GL.GetInteger(GetPName.BlendSrcAlpha, out _blendSrcAlpha);
            GL.GetInteger(GetPName.BlendDstRgb, out _blendDstRgb);
            GL.GetInteger(GetPName.BlendDstAlpha, out _blendDstAlpha);
            GL.GetInteger(GetPName.BlendEquationRgb, out _blendEquationRgb);
            GL.GetInteger(GetPName.BlendEquationAlpha, out _blendEquationAlpha);
            BlendCap.SaveState();
        }

        public void RestoreState()
        {
            BlendCap.RestoreState();
            GL.BlendEquationSeparate((BlendEquationMode)_blendEquationRgb, (BlendEquationMode)_blendEquationAlpha);
            GL.BlendFuncSeparate((BlendingFactorSrc)_blendSrcRgb, (BlendingFactorDest)_blendDstRgb,
                (BlendingFactorSrc)_blendSrcAlpha, (BlendingFactorDest)_blendDstAlpha);
        }
    }
}
