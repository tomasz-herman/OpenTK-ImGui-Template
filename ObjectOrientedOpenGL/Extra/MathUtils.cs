global using Vector4tk = OpenTK.Mathematics.Vector4;
global using Vector3tk = OpenTK.Mathematics.Vector3;
global using Vector2tk = OpenTK.Mathematics.Vector2;

global using Matrix4tk = OpenTK.Mathematics.Matrix4;
global using Matrix3tk = OpenTK.Mathematics.Matrix3;
global using Matrix2tk = OpenTK.Mathematics.Matrix2;

global using Quaterniontk = OpenTK.Mathematics.Quaternion;

namespace ObjectOrientedOpenGL.Extra;

using System.Numerics;

public static class MathUtils
{
    public static Vector2 ToSystem(this Vector2tk v)
    {
        return new Vector2(v.X, v.Y);
    }
    
    public static Vector3 ToSystem(this Vector3tk v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }
    
    public static Vector4 ToSystem(this Vector4tk v)
    {
        return new Vector4(v.X, v.Y, v.Z, v.W);
    }
    
    public static Vector2tk ToOpenTk(this Vector2 v)
    {
        return new Vector2tk(v.X, v.Y);
    }
    
    public static Vector3tk ToOpenTk(this Vector3 v)
    {
        return new Vector3tk(v.X, v.Y, v.Z);
    }
    
    public static Vector4tk ToOpenTk(this Vector4 v)
    {
        return new Vector4tk(v.X, v.Y, v.Z, v.W);
    }
    
    public static Matrix4x4 ToSystem(this Matrix4tk m)
    {
        return new Matrix4x4(m.M11, m.M12, m.M13, m.M14,
                             m.M21, m.M22, m.M23, m.M24,
                             m.M31, m.M32, m.M33, m.M34,
                             m.M41, m.M42, m.M43, m.M44);
    }
    
    public static Matrix4tk ToOpenTk(this Matrix4x4 m)
    {
        return new Matrix4tk(m.M11, m.M12, m.M13, m.M14,
                             m.M21, m.M22, m.M23, m.M24,
                             m.M31, m.M32, m.M33, m.M34,
                             m.M41, m.M42, m.M43, m.M44);
    }

    public static Quaternion ToSystem(this Quaterniontk q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }
    
    public static Quaterniontk ToOpenTk(this Quaternion q)
    {
        return new Quaterniontk(q.X, q.Y, q.Z, q.W);
    }
}