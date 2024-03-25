using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TemplateProject;

public class Camera
{
    public IControl Control { get; set; }
    public IProjection Projection { get; set; }
    public Vector3 Position => Control.Position;
    public Vector3 Front => Control.Front;
    public Vector3 Right => Control.Right;
    public Vector3 Up => Control.Up;

    public float Aspect
    {
        set => Projection.Aspect = value;
        get => Projection.Aspect;
    }

    public Matrix4 ProjectionMatrix => Projection.ProjectionMatrix;
    public Matrix4 ViewMatrix => Control.ViewMatrix;
    public Matrix4 ProjectionViewMatrix => ViewMatrix * ProjectionMatrix;

    public Camera(IControl control, IProjection projection)
    {
        Control = control;
        Projection = projection;
    }

    public interface IControl
    {
        void Update(Camera camera, float dt);
        void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse);
        Vector3 Position { get; }
        Vector3 Front { get; }
        Vector3 Right { get; }
        Vector3 Up { get; }
        Matrix4 ViewMatrix { get; }
    }

    public interface IProjection
    {
        void Update(Camera camera, float dt);
        float Aspect { get; set; }
        Matrix4 ProjectionMatrix { get; }
    }

    public void Update(float dt)
    {
        Control.Update(this, dt);
        Projection.Update(this, dt);
    }

    public void HandleInput(float dt, KeyboardState keyboard, MouseState mouse)
    {
        Control.HandleInput(this, dt, keyboard, mouse);
    }
}

public class NoControl : Camera.IControl
{
    public NoControl(Camera.IControl control)
    {
        Position = control.Position;
        Front = control.Front;
        Right = control.Right;
        Up = control.Up;
        ViewMatrix = control.ViewMatrix;
    }

    public NoControl(Vector3 position, Vector3 target, Vector3? up = null)
    {
        up ??= Vector3.UnitY;
        Position = position;
        Front = (position - target).Normalized();
        Right = Vector3.Cross(up.Value, Front).Normalized();
        Up = Vector3.Cross(Front, Right).Normalized();
        ViewMatrix = Matrix4.LookAt(position, target, up.Value);
    }

    public void Update(Camera camera, float dt) { }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse) { }

    public Vector3 Position { get; }
    public Vector3 Front { get; }
    public Vector3 Right { get; }
    public Vector3 Up { get; }
    public Matrix4 ViewMatrix { get; }
}

public class OrbitingControl : Camera.IControl
{
    public OrbitingControl(Camera.IControl control, float distance = 1.0f)
    {
        Position = control.Position;
        Front = control.Front;
        Right = control.Right;
        Up = control.Up;
        Target = Position - Front * distance;
        ViewMatrix = control.ViewMatrix;
    }

    public OrbitingControl(Vector3 position, Vector3 target, Vector3? up)
    {
        up ??= Vector3.UnitY;
        Position = position;
        Target = target;
        Front = (position - target).Normalized();
        Right = Vector3.Cross(up.Value, Front).Normalized();
        Up = Vector3.Cross(Front, Right).Normalized();
        ViewMatrix = Matrix4.LookAt(position, target, up.Value);
    }

    public void Update(Camera camera, float dt) { }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector3 toCameraVector = Position - Target;
        if (mouse.IsButtonDown(MouseButton.Button1) && mouse.WasButtonDown(MouseButton.Button1))
        {
            Vector2 delta = mouse.Delta;
            float dYaw = delta.X * Sensitivity;
            float dPitch = delta.Y * Sensitivity;
            Matrix4 rotateX = Matrix4.CreateFromAxisAngle(Up, -dYaw);
            Matrix4 rotateY = Matrix4.CreateFromAxisAngle(Right, -dPitch);
            toCameraVector = Vector3.TransformVector(toCameraVector, rotateY * rotateX);
        }

        if (mouse.IsButtonDown(MouseButton.Button2) && mouse.WasButtonDown(MouseButton.Button2))
        {
            Vector2 delta = mouse.Delta;
            float dRoll = delta.Y * Sensitivity;
            Matrix4 rotateZ = Matrix4.CreateFromAxisAngle(Front, dRoll);
            Up = Vector3.TransformVector(Up, rotateZ);
        }

        if (mouse.ScrollDelta != Vector2.Zero)
        {
            float scroll = mouse.ScrollDelta.Y;
            toCameraVector *= MathF.Pow(1 + Sensitivity, scroll);
        }

        Position = Target + toCameraVector;
        Front = (Position - Target).Normalized();
        Right = Vector3.Cross(Up, Front).Normalized();
        Up = Vector3.Cross(Front, Right).Normalized();
        ViewMatrix = Matrix4.LookAt(Position, Target, Up);
    }

    public Vector3 Position { get; private set; }
    public Vector3 Front { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }
    public Vector3 Target { get; set; }
    public float Sensitivity { get; set; } = 0.002f;
    public Matrix4 ViewMatrix { get; private set; }
}

public class FlyByControl : Camera.IControl
{
    public FlyByControl(Camera.IControl control)
    {
        Position = control.Position;
        Front = control.Front;
        Right = control.Right;
        Up = control.Up;
        ViewMatrix = Matrix4.LookAt(Position, Position - Front, Up);
    }

    public FlyByControl(Vector3 position, Vector3 target, Vector3? up = null)
    {
        up ??= Vector3.UnitY;
        Position = position;
        Front = (position - target).Normalized();
        Right = Vector3.Cross(up.Value, Front).Normalized();
        Up = Vector3.Cross(Front, Right).Normalized();
        ViewMatrix = Matrix4.LookAt(position, target, up.Value);
    }

    public void Update(Camera camera, float dt) { }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector3 move = Vector3.Zero;
        Vector3 rotation = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W))
        {
            move -= Front * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.S))
        {
            move += Front * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.A))
        {
            move -= Right * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.D))
        {
            move += Right * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.Q))
        {
            move -= Up * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.E))
        {
            move += Up * dt * Speed;
        }

        if (mouse.IsButtonDown(MouseButton.Button1) && mouse.WasButtonDown(MouseButton.Button1))
        {
            Vector2 delta = mouse.Delta;
            rotation.X = delta.X * Sensitivity;
            rotation.Y = delta.Y * Sensitivity;
            Matrix4 rotateX = Matrix4.CreateFromAxisAngle(Up, -rotation.X);
            Matrix4 rotateY = Matrix4.CreateFromAxisAngle(Right, -rotation.Y);
            Front = Vector3.TransformVector(Front, rotateY * rotateX);
        }

        if (mouse.IsButtonDown(MouseButton.Button2) && mouse.WasButtonDown(MouseButton.Button2))
        {
            Vector2 delta = mouse.Delta;
            rotation.Z = delta.Y * Sensitivity;
            Matrix4 rotateZ = Matrix4.CreateFromAxisAngle(Front, rotation.Z);
            Up = Vector3.TransformVector(Up, rotateZ);
        }

        if (move != Vector3.Zero || rotation != Vector3.Zero)
        {
            Position += move;
            Right = Vector3.Cross(Up, Front).Normalized();
            Up = Vector3.Cross(Front, Right).Normalized();
            ViewMatrix = Matrix4.LookAt(Position, Position - Front, Up);
        }
    }

    public Vector3 Position { get; private set; }
    public Vector3 Front { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }
    public Matrix4 ViewMatrix { get; private set; }
    public float Speed { get; set; } = 1f;
    public float Sensitivity { get; set; } = 0.002f;
}

public class PerspectiveProjection : Camera.IProjection
{
    public void Update(Camera camera, float dt)
    {
        ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(FovY, Aspect, Near, Far);
    }

    public float FovY { get; set; } = MathF.PI / 4;
    public float Aspect { get; set; }
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 100.0f;
    public Matrix4 ProjectionMatrix { get; private set; }
}

public class OrthographicProjection : Camera.IProjection
{
    public void Update(Camera camera, float dt)
    {
        ProjectionMatrix = Matrix4.CreateOrthographic(Height * Aspect, Height, Near, Far);
    }

    public float Near { get; set; } = 0f;
    public float Far { get; set; } = 10.0f;
    public float Height { get; set; } = 1.0f;
    public float Aspect { get; set; }
    public Matrix4 ProjectionMatrix { get; private set; }
}