using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ObjectOrientedOpenGL.Core;

public class Camera
{
    public IControl Control { get; set; }
    public IProjection Projection { get; set; }
    public Vector3 Position => Control.Position;
    public Vector3 Front => Control.Forward;
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
        Vector3 Forward { get; }
        Vector3 Right { get; }
        Vector3 Up { get; }
        Matrix4 ViewMatrix { get; }
    }

    public interface IProjection
    {
        void Update(Camera camera, float dt);
        void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse);
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
        Projection.HandleInput(this, dt, keyboard, mouse);
    }
}

public class NoControl : Camera.IControl
{
    public NoControl(Camera.IControl control)
    {
        Position = control.Position;
        Forward = control.Forward;
        Right = control.Right;
        Up = control.Up;
        ViewMatrix = control.ViewMatrix;
    }

    public NoControl(Vector3 position, Vector3 target, Vector3? up = null)
    {
        up ??= Vector3.UnitY;
        Position = position;
        Forward = (target - position).Normalized();
        Right = Vector3.Cross(Forward, up.Value).Normalized();
        Up = Vector3.Cross(Right, Forward).Normalized();
        ViewMatrix = Matrix4.LookAt(position, target, up.Value);
    }

    public void Update(Camera camera, float dt) { }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse) { }

    public Vector3 Position { get; }
    public Vector3 Forward { get; }
    public Vector3 Right { get; }
    public Vector3 Up { get; }
    public Matrix4 ViewMatrix { get; }
}

public class OrbitalControl : Camera.IControl
{
    public OrbitalControl()
    {
        Pitch = Yaw = 0;
        Distance = 10;
        FocalPoint = Vector3.Zero;
    }
    
    public OrbitalControl(Camera.IControl control)
    {
        Distance = 10;
        FocalPoint = control.Position + Distance * control.Forward;
        Pitch = MathF.Asin(control.Forward.Y);
        Yaw = MathF.Atan2(-control.Forward.X, -control.Forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }
    
    public OrbitalControl(Vector3 position, Vector3 focal)
    {
        Distance = (position - focal).Length;
        FocalPoint = focal;
        Vector3 forward = (focal - position).Normalized();
        Pitch = MathF.Asin(forward.Y);
        Yaw = MathF.Atan2(-forward.X, -forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }

    public void Update(Camera camera, float dt)
    {
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector2 delta = mouse.Delta * dt;
        if(delta == Vector2.Zero) return;
        if (mouse.IsButtonDown(MouseButton.Button1))
        {
            Rotate(delta);
        }
        else if (mouse.IsButtonDown(MouseButton.Button2))
        {
            Zoom(delta);
        }
    }

    public void Rotate(Vector2 delta)
    {
        var sign = Up.Y < 0 ? -1 : 1;
        Yaw += sign * delta.X * RotationSpeed;
        Pitch -= delta.Y * RotationSpeed;
    }

    public void Zoom(Vector2 delta)
    {
        Distance = MathF.Max(1.0f, Distance * MathF.Pow(1 + ZoomSpeed, delta.Y));
    }

    public Vector3 Position => FocalPoint - Distance * Forward;
    public Vector3 FocalPoint { get; set; }
    public Vector3 Forward => Orientation * -Vector3.UnitZ;
    public Vector3 Right => Orientation * Vector3.UnitX;
    public Vector3 Up => Orientation * Vector3.UnitY;
    public Matrix4 ViewMatrix { get; set; }

    public float ZoomSpeed { get; set; } = 1;
    public float RotationSpeed { get; set; } = 1;

    private Quaternion Orientation => 
        Quaternion.FromAxisAngle(Vector3.UnitY, Yaw) * 
        Quaternion.FromAxisAngle(Vector3.UnitX, Pitch);
    private float Pitch { get; set; }
    private float Yaw { get; set; }
    private float Distance { get; set; }
}

public class FlyByControl : Camera.IControl
{
    public FlyByControl()
    {
        Pitch = Yaw = 0;
        Position = Vector3.Zero;
    }
    
    public FlyByControl(Camera.IControl control)
    {
        Position = control.Position;
        Pitch = MathF.Asin(control.Forward.Y);
        Yaw = MathF.Atan2(-control.Forward.X, -control.Forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, Position + Forward, Up);
    }
    
    public FlyByControl(Vector3 position, Vector3 focal)
    {
        Position = position;
        Vector3 forward = (focal - position).Normalized();
        Pitch = MathF.Asin(forward.Y);
        Yaw = MathF.Atan2(-forward.X, -forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, Position + Forward, Up);
    }

    public void Update(Camera camera, float dt)
    {
        ViewMatrix = Matrix4.LookAt(Position, Position + Forward, Up);
    }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W))
        {
            move += Forward * dt * Speed;
        }
        if (keyboard.IsKeyDown(Keys.S))
        {
            move -= Forward * dt * Speed;
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

        Position += move;
        
        Vector2 delta = mouse.Delta * dt;
        if(delta == Vector2.Zero) return;
        if (mouse.IsButtonDown(MouseButton.Button1))
        {
            Rotate(delta);
        }
    }

    public void Rotate(Vector2 delta)
    {
        var sign = Up.Y < 0 ? -1 : 1;
        Yaw -= sign * delta.X * RotationSpeed;
        Pitch -= delta.Y * RotationSpeed;
    }

    public Vector3 Position { get; set; }
    public Vector3 Forward => Orientation * -Vector3.UnitZ;
    public Vector3 Right => Orientation * Vector3.UnitX;
    public Vector3 Up => Orientation * Vector3.UnitY;
    public Matrix4 ViewMatrix { get; set; }

    public float RotationSpeed { get; set; } = 1;
    public float Speed { get; set; } = 1;

    private Quaternion Orientation => 
        Quaternion.FromAxisAngle(Vector3.UnitY, Yaw) * 
        Quaternion.FromAxisAngle(Vector3.UnitX, Pitch);
    private float Pitch { get; set; }
    private float Yaw { get; set; }
}

public class EditorControl : Camera.IControl
{
    public EditorControl()
    {
        Pitch = Yaw = 0;
        Distance = 10;
        FocalPoint = Vector3.Zero;
    }
    
    public EditorControl(Camera.IControl control)
    {
        Distance = 10;
        FocalPoint = control.Position + Distance * control.Forward;
        Pitch = MathF.Asin(control.Forward.Y);
        Yaw = MathF.Atan2(-control.Forward.X, -control.Forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }
    
    public EditorControl(Vector3 position, Vector3 focal)
    {
        Distance = (position - focal).Length;
        FocalPoint = focal;
        Vector3 forward = (focal - position).Normalized();
        Pitch = MathF.Asin(forward.Y);
        Yaw = MathF.Atan2(-forward.X, -forward.Z);
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }

    public void Update(Camera camera, float dt)
    {
        ViewMatrix = Matrix4.LookAt(Position, FocalPoint, Up);
    }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector2 delta = mouse.Delta * dt;
        if(delta == Vector2.Zero) return;
        if (mouse.IsButtonDown(MouseButton.Button1))
        {
            Rotate(delta);
        }
        else if (mouse.IsButtonDown(MouseButton.Button2))
        {
            Zoom(delta);
        }
        else if (mouse.IsButtonDown(MouseButton.Button3))
        {
            Pan(delta);
        }
    }

    public void Rotate(Vector2 delta)
    {
        var sign = Up.Y < 0 ? -1 : 1;
        Yaw += sign * delta.X * RotationSpeed;
        Pitch -= delta.Y * RotationSpeed;
    }

    public void Pan(Vector2 delta)
    {
        FocalPoint += (-Right * delta.X + Up * delta.Y) * Distance * PanSpeed;
    }

    public void Zoom(Vector2 delta)
    {
        Distance *= MathF.Pow(1 + ZoomSpeed, delta.Y);
        Console.WriteLine(Distance);
        if (Distance < 1)
        {
            FocalPoint += Forward * (1 - Distance);
            Distance = 1;
        }
    }

    public Vector3 Position => FocalPoint - Distance * Forward;
    public Vector3 FocalPoint { get; set; }
    public Vector3 Forward => Orientation * -Vector3.UnitZ;
    public Vector3 Right => Orientation * Vector3.UnitX;
    public Vector3 Up => Orientation * Vector3.UnitY;
    public Matrix4 ViewMatrix { get; set; }

    public float ZoomSpeed { get; set; } = 1;
    public float RotationSpeed { get; set; } = 1;
    public float PanSpeed { get; set; } = 1;

    private Quaternion Orientation => 
        Quaternion.FromAxisAngle(Vector3.UnitY, Yaw) * 
        Quaternion.FromAxisAngle(Vector3.UnitX, Pitch);
    private float Pitch { get; set; }
    private float Yaw { get; set; }
    private float Distance { get; set; }
}

public class PerspectiveProjection : Camera.IProjection
{
    public void Update(Camera camera, float dt)
    {
        ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(FovY, Aspect, Near, Far);
    }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        float scroll = mouse.ScrollDelta.Y;
        FovY *= MathF.Pow(1 + Sensitivity, scroll);
        FovY = float.Clamp(FovY, float.MinValue, float.Pi);
    }

    public float FovY { get; set; } = MathF.PI / 4;
    public float Aspect { get; set; }
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 100.0f;
    public Matrix4 ProjectionMatrix { get; private set; }
    public float Sensitivity { get; set; } = 0.002f;
}

public class OrthographicProjection : Camera.IProjection
{
    public void Update(Camera camera, float dt)
    {
        ProjectionMatrix = Matrix4.CreateOrthographic(Height * Aspect, Height, Near, Far);
    }

    public void HandleInput(Camera camera, float dt, KeyboardState keyboard, MouseState mouse)
    {
        if (mouse.ScrollDelta != Vector2.Zero)
        {
            float scroll = mouse.ScrollDelta.Y;
            Height *= MathF.Pow(1 + Sensitivity, scroll);
            Height = float.Clamp(Height, 0, float.PositiveInfinity);
        }
    }

    public float Near { get; set; } = 0f;
    public float Far { get; set; } = 100.0f;
    public float Height { get; set; } = 1.0f;
    public float Aspect { get; set; }
    public Matrix4 ProjectionMatrix { get; private set; }
    public float Sensitivity { get; set; } = 0.002f;
}