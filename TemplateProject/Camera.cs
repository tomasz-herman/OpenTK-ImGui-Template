using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TemplateProject;

public class Camera
{
    public IControl Control { get; set; }
    public IView View { get; set; }
    
    public Vector3 Front { get; private set; } = Vector3.UnitZ;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public float Aspect { get; set; } = 16f / 9f;
    public Vector3 Position { get; set; } = Vector3.Zero;
    
    private float _pitch;
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = value;
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 * 0.99f, MathHelper.PiOver2 * 0.99f);
            UpdateVectors();
        }
    }
        
    private float _yaw = MathHelper.PiOver2;
    public float Yaw
    {
        get => _yaw;
        set
        {
            _yaw = value;
            UpdateVectors();
        }
    }

    public Camera(IControl control, IView view)
    {
        Control = control;
        View = view;
        UpdateVectors();
    }
    
    public interface IControl
    {
        void HandleInput(Camera camera, KeyboardState keyboard, MouseState mouse, float dt);
    }

    public interface IView
    {
        Matrix4 GetProjectionMatrix(float aspect);
    }

    public void Rotate(float dPitch, float dYaw)
    {
        _pitch += dPitch;
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 * 0.99f, MathHelper.PiOver2 * 0.99f);
        _yaw += dYaw;
        UpdateVectors();
    }

    public void Move(float dx, float dy, float dz)
    {
        Position += Front * dz + Up * dy + Right * dx;
    }

    public Matrix4 GetProjectionViewMatrix()
    {
        return GetViewMatrix() * GetProjectionMatrix();
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + Front, Up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return View.GetProjectionMatrix(Aspect);
    }

    public void UpdateVectors()
    {
        Front = new Vector3
        {
            X = (float) Math.Cos(Pitch) * (float) Math.Cos(Yaw),
            Y = (float) Math.Sin(Pitch),
            Z = (float) Math.Cos(Pitch) * (float) Math.Sin(Yaw)
        };

        Front = Vector3.Normalize(Front);
        Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Front));
    }

    public void HandleInput(KeyboardState keyboard, MouseState mouse, float dt)
    {
        Control.HandleInput(this, keyboard, mouse, dt);
    }
}

public class FirstPersonControl : Camera.IControl
{
    public float Sensitivity { get; set; } = 0.001f;
    public float Speed { get; set; } = 1.0f;

    public void HandleInput(Camera camera, KeyboardState keyboard, MouseState mouse, float dt)
    {
        if (mouse.IsButtonDown(MouseButton.Button1) && mouse.WasButtonDown(MouseButton.Button1))
        {
            Vector2 delta = mouse.Delta;
            camera.Rotate(-Sensitivity * delta.Y, Sensitivity * delta.X);
        }

        if (keyboard.IsKeyDown(Keys.W))
        {
            camera.Move(0, 0, dt * Speed);
        }
        if (keyboard.IsKeyDown(Keys.S))
        {
            camera.Move(0, 0, -dt * Speed);
        }
        if (keyboard.IsKeyDown(Keys.D))
        {
            camera.Move(dt * Speed, 0, 0);
        }
        if (keyboard.IsKeyDown(Keys.A))
        {
            camera.Move(-dt * Speed, 0, 0);
        }
        if (keyboard.IsKeyDown(Keys.E))
        {
            camera.Move(0, dt * Speed, 0);
        }
        if (keyboard.IsKeyDown(Keys.Q))
        {
            camera.Move(0, -dt * Speed, 0);
        }
    }
}

public class PerspectiveView : Camera.IView
{
    private float _fov = MathHelper.PiOver3;

    public float Fov
    {
        get => MathHelper.RadiansToDegrees(_fov);
        set => _fov = MathHelper.DegreesToRadians(value);
    }

    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 100f;

    public Matrix4 GetProjectionMatrix(float aspect)
    {
        return Matrix4.CreatePerspectiveFieldOfView(_fov, aspect, NearPlane, FarPlane);
    }
}