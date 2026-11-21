using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

public enum AnimationPath
{
    Translation,
    Rotation,
    Scale
}

public enum AnimationInterpolation
{
    Linear,
    Step,
    CubicSpline
}

/// <summary>
/// Keyframe track targeting a single node's translation, rotation, or scale.
/// </summary>
public sealed class AnimationChannel
{
    public int TargetNodeIndex { get; }
    public AnimationPath Path { get; }
    public AnimationInterpolation Interpolation { get; }
    public float[] Times { get; }
    public Vector3[]? Vec3Values { get; }
    public Quaternion[]? QuatValues { get; }

    public AnimationChannel(
        int targetNodeIndex,
        AnimationPath path,
        AnimationInterpolation interpolation,
        float[] times,
        Vector3[]? vec3Values = null,
        Quaternion[]? quatValues = null)
    {
        TargetNodeIndex = targetNodeIndex;
        Path = path;
        Interpolation = interpolation;
        Times = times;
        Vec3Values = vec3Values;
        QuatValues = quatValues;
    }

    public Vector3 SampleVec3(float time)
    {
        if (Vec3Values is null || Vec3Values.Length == 0 || Times.Length == 0)
            return Path == AnimationPath.Scale ? Vector3.One : Vector3.Zero;

        int stride = Interpolation == AnimationInterpolation.CubicSpline ? 3 : 1;
        int offset = Interpolation == AnimationInterpolation.CubicSpline ? 1 : 0;

        if (Times.Length == 1 || time <= Times[0])
            return Vec3Values[offset];
        if (time >= Times[^1])
            return Vec3Values[(Times.Length - 1) * stride + offset];

        int idx = FindKeyframeIndex(time);
        float t0 = Times[idx];
        float t1 = Times[idx + 1];
        float alpha = (t1 - t0) > 1e-6f ? (time - t0) / (t1 - t0) : 0f;

        var v0 = Vec3Values[idx * stride + offset];
        if (Interpolation == AnimationInterpolation.Step)
            return v0;

        var v1 = Vec3Values[(idx + 1) * stride + offset];
        return Vector3.Lerp(v0, v1, alpha);
    }

    public Quaternion SampleQuat(float time)
    {
        if (QuatValues is null || QuatValues.Length == 0 || Times.Length == 0)
            return Quaternion.Identity;

        int stride = Interpolation == AnimationInterpolation.CubicSpline ? 3 : 1;
        int offset = Interpolation == AnimationInterpolation.CubicSpline ? 1 : 0;

        if (Times.Length == 1 || time <= Times[0])
            return QuatValues[offset].Normalized();
        if (time >= Times[^1])
            return QuatValues[(Times.Length - 1) * stride + offset].Normalized();

        int idx = FindKeyframeIndex(time);
        float t0 = Times[idx];
        float t1 = Times[idx + 1];
        float alpha = (t1 - t0) > 1e-6f ? (time - t0) / (t1 - t0) : 0f;

        var q0 = QuatValues[idx * stride + offset];
        if (Interpolation == AnimationInterpolation.Step)
            return q0.Normalized();

        var q1 = QuatValues[(idx + 1) * stride + offset];
        if (Vector4.Dot(new Vector4(q0.X, q0.Y, q0.Z, q0.W), new Vector4(q1.X, q1.Y, q1.Z, q1.W)) < 0f)
            q1 = new Quaternion(-q1.X, -q1.Y, -q1.Z, -q1.W);

        return Quaternion.Slerp(q0, q1, alpha).Normalized();
    }

    private int FindKeyframeIndex(float time)
    {
        int lo = 0, hi = Times.Length - 2;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (time < Times[mid]) hi = mid - 1;
            else if (time >= Times[mid + 1]) lo = mid + 1;
            else return mid;
        }
        return Math.Clamp(lo, 0, Times.Length - 2);
    }
}

/// <summary>
/// Named glTF animation clip containing one or more node transform channels.
/// </summary>
public sealed class AnimationClip(string name, float duration, List<AnimationChannel> channels)
{
    public string Name { get; } = name;
    public float Duration { get; } = duration;
    public IReadOnlyList<AnimationChannel> Channels { get; } = channels;
}

/// <summary>
/// Skeletal skin definition mapping joint node indices to inverse bind matrices.
/// </summary>
public sealed class SkinData(string name, int[] jointNodeIndices, Matrix4[] inverseBindMatrices)
{
    public string Name { get; } = name;
    public int[] JointNodeIndices { get; } = jointNodeIndices;
    public Matrix4[] InverseBindMatrices { get; } = inverseBindMatrices;
}
