using System.Runtime.InteropServices;
using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Extra;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SharpGLTF.Schema2;
using StbImageSharp;
using AlphaMode = ObjectOrientedOpenGL.Graphics.AlphaMode;
using Mesh = ObjectOrientedOpenGL.Core.Mesh;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Texture = ObjectOrientedOpenGL.Core.Texture;

namespace ObjectOrientedOpenGL.Graphics;

/// <summary>
/// Loads glTF 2.0 (<c>.gltf</c> / <c>.glb</c>) assets using <c>SharpGLTF</c>.
/// Only <c>.gltf</c> and <c>.glb</c> formats are supported.
/// </summary>
public static class GltfModelLoader
{
    public static void ValidateExtension(string path)
    {
        string ext = Path.GetExtension(path);
        if (!ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".glb", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Unsupported model format '{ext}'. Only glTF 2.0 (.gltf, .glb) files are supported.");
        }
    }

    /// <summary>
    /// Loads a <c>.gltf</c> or <c>.glb</c> model from disk or an embedded resource.
    /// </summary>
    public static Model Load(string path, bool fromResource = false)
    {
        ValidateExtension(path);

        ModelRoot gltf;
        if (fromResource)
        {
            using var stream = ResourcesUtils.GetResourceStream(path)
                ?? throw new FileNotFoundException($"Embedded glTF resource not found: {path}");
            gltf = ModelRoot.ReadGLB(stream);
        }
        else
        {
            gltf = ModelRoot.Load(path);
        }

        return BuildModel(path, gltf);
    }

    /// <summary>
    /// Loads a <c>.glb</c> binary glTF model from a stream.
    /// </summary>
    public static Model LoadGlb(Stream stream, string name = "MemoryModel.glb")
    {
        var gltf = ModelRoot.ReadGLB(stream);
        return BuildModel(name, gltf);
    }

    /// <summary>
    /// Parses glTF/GLB geometry, materials, and node hierarchy from disk into CPU data without creating OpenGL resources.
    /// Useful for headless validation and unit tests.
    /// </summary>
    public static ParsedGltfData ParseCpu(string path)
    {
        ValidateExtension(path);
        var gltf = ModelRoot.Load(path);
        return ParseCpu(gltf);
    }

    /// <summary>
    /// Parses glTF/GLB geometry, materials, and node hierarchy into CPU data without creating OpenGL resources.
    /// Useful for headless validation and unit tests.
    /// </summary>
    public static ParsedGltfData ParseGlbCpu(Stream stream)
    {
        var gltf = ModelRoot.ReadGLB(stream);
        return ParseCpu(gltf);
    }

    private static Model BuildModel(string path, ModelRoot gltf)
    {
        var parsed = ParseCpu(gltf);

        // 1. Decode images in parallel on CPU, then upload textures on the OpenGL thread
        var decoded = new ImageResult?[parsed.Images.Count];
        Parallel.For(0, parsed.Images.Count, i =>
        {
            var imgBytes = parsed.Images[i];
            if (imgBytes.Length > 0)
            {
                using var ms = new MemoryStream(imgBytes, writable: false);
                decoded[i] = ImageResult.FromStream(ms, ColorComponents.RedGreenBlueAlpha);
            }
        });

        var textures = new List<Texture>(parsed.Images.Count);
        for (int i = 0; i < decoded.Length; i++)
        {
            var img = decoded[i];
            textures.Add(img is not null
                ? Texture.FromRgba(img.Data, img.Width, img.Height)
                : Texture.CreateSolidColor(255, 255, 255, 255));
            decoded[i] = null;
        }

        // 2. Build PBR materials
        var materials = new List<PbrMaterial>(parsed.Materials.Count);
        foreach (var pm in parsed.Materials)
        {
            var mat = new PbrMaterial(pm.Name)
            {
                BaseColorFactor = pm.BaseColorFactor,
                MetallicFactor = pm.MetallicFactor,
                RoughnessFactor = pm.RoughnessFactor,
                EmissiveFactor = pm.EmissiveFactor,
                AlphaMode = pm.AlphaMode,
                AlphaCutoff = pm.AlphaCutoff,
                DoubleSided = pm.DoubleSided,
                BaseColorTexture = pm.BaseColorImageIndex >= 0 ? textures[pm.BaseColorImageIndex] : null,
                MetallicRoughnessTexture = pm.MetallicRoughnessImageIndex >= 0 ? textures[pm.MetallicRoughnessImageIndex] : null,
                NormalTexture = pm.NormalImageIndex >= 0 ? textures[pm.NormalImageIndex] : null,
                OcclusionTexture = pm.OcclusionImageIndex >= 0 ? textures[pm.OcclusionImageIndex] : null,
                EmissiveTexture = pm.EmissiveImageIndex >= 0 ? textures[pm.EmissiveImageIndex] : null,
            };
            materials.Add(mat);
        }

        var defaultMaterial = new PbrMaterial("Default");
        if (materials.Count == 0)
            materials.Add(defaultMaterial);

        // 3. Upload GPU meshes
        var allPrimitives = new List<MeshPrimitive>();
        var primitivesByMesh = new List<List<MeshPrimitive>>(parsed.Meshes.Count);

        foreach (var meshGroup in parsed.Meshes)
        {
            var groupList = new List<MeshPrimitive>(meshGroup.Count);
            foreach (var pp in meshGroup)
            {
                var ibo = new IndexBuffer(
                    pp.Indices,
                    pp.Indices.Length * sizeof(uint),
                    DrawElementsType.UnsignedInt,
                    pp.Indices.Length);
                var vbo = new VertexBuffer(
                    pp.Vertices,
                    pp.Vertices.Length * Marshal.SizeOf<StandardVertex>(),
                    pp.Vertices.Length,
                    BufferUsageHint.StaticDraw,
                    StandardVertex.Attributes);
                var gpuMesh = new Mesh(pp.Name, PrimitiveType.Triangles, ibo, vbo);
                var mat = pp.MaterialIndex >= 0 && pp.MaterialIndex < materials.Count
                    ? materials[pp.MaterialIndex]
                    : defaultMaterial;

                var prim = new MeshPrimitive(pp.Name, gpuMesh, mat, pp.Bounds);
                groupList.Add(prim);
                allPrimitives.Add(prim);
            }
            primitivesByMesh.Add(groupList);
        }

        // 4. Build ModelNode tree
        ModelNode BuildNode(ParsedNode pn)
        {
            var prims = pn.MeshIndex >= 0 && pn.MeshIndex < primitivesByMesh.Count
                ? primitivesByMesh[pn.MeshIndex]
                : [];
            var children = pn.Children.Select(BuildNode).ToList();
            return new ModelNode(pn.Name, pn.LocalTransform, prims, children, pn.NodeIndex, pn.SkinIndex);
        }

        var root = BuildNode(parsed.Root);
        return new Model(path, root, allPrimitives, materials, textures, parsed.Animations, parsed.Skins);
    }

    private static ParsedGltfData ParseCpu(ModelRoot gltf)
    {
        // Images
        var images = new List<byte[]>(gltf.LogicalImages.Count);
        foreach (var img in gltf.LogicalImages)
        {
            images.Add(img.Content.Content.ToArray());
        }

        // Materials
        var materials = new List<ParsedMaterial>(gltf.LogicalMaterials.Count);
        foreach (var m in gltf.LogicalMaterials)
        {
            var baseCol = Vector4.One;
            float metallic = 0.0f;
            float roughness = 0.5f;
            int baseColorImg = -1;
            int mrImg = -1;

            var baseChan = m.FindChannel("BaseColor");
            if (baseChan.HasValue)
            {
                var c = baseChan.Value.Color;
                baseCol = new Vector4(c.X, c.Y, c.Z, c.W);
                baseColorImg = baseChan.Value.Texture?.PrimaryImage?.LogicalIndex ?? -1;
            }

            var mrChan = m.FindChannel("MetallicRoughness");
            if (mrChan.HasValue)
            {
                foreach (var param in mrChan.Value.Parameters)
                {
                    if (param.Name == "MetallicFactor") metallic = (float)param.Value;
                    else if (param.Name == "RoughnessFactor") roughness = (float)param.Value;
                }
                mrImg = mrChan.Value.Texture?.PrimaryImage?.LogicalIndex ?? -1;
            }

            var normalImg = m.FindChannel("Normal")?.Texture?.PrimaryImage?.LogicalIndex ?? -1;
            var occImg = m.FindChannel("Occlusion")?.Texture?.PrimaryImage?.LogicalIndex ?? -1;
            var emissiveChan = m.FindChannel("Emissive");
            var emissiveFactor = emissiveChan.HasValue
                ? new Vector3(emissiveChan.Value.Color.X, emissiveChan.Value.Color.Y, emissiveChan.Value.Color.Z)
                : Vector3.Zero;
            var emissiveImg = emissiveChan?.Texture?.PrimaryImage?.LogicalIndex ?? -1;

            var alphaMode = m.Alpha switch
            {
                SharpGLTF.Schema2.AlphaMode.MASK => AlphaMode.Mask,
                SharpGLTF.Schema2.AlphaMode.BLEND => AlphaMode.Blend,
                _ => AlphaMode.Opaque
            };

            materials.Add(new ParsedMaterial(
                m.Name ?? $"Material_{m.LogicalIndex}",
                baseCol, metallic, roughness, emissiveFactor,
                alphaMode, m.AlphaCutoff, m.DoubleSided,
                baseColorImg, mrImg, normalImg, occImg, emissiveImg));
        }

        // Meshes & primitives
        var meshes = new List<List<ParsedPrimitive>>(gltf.LogicalMeshes.Count);
        foreach (var gltfMesh in gltf.LogicalMeshes)
        {
            var primList = new List<ParsedPrimitive>(gltfMesh.Primitives.Count);
            int pIdx = 0;
            foreach (var prim in gltfMesh.Primitives)
            {
                var posAcc = prim.GetVertexAccessor("POSITION");
                if (posAcc is null) continue;

                var positions = posAcc.AsVector3Array();
                var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var tangents = prim.GetVertexAccessor("TANGENT")?.AsVector4Array();
                var texCoords = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var joints = prim.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
                var weights = prim.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

                uint[] indices = prim.IndexAccessor != null
                    ? prim.GetIndices().ToArray()
                    : Enumerable.Range(0, positions.Count).Select(i => (uint)i).ToArray();

                var vertices = new StandardVertex[positions.Count];
                var bounds = BoundingBox.Empty;

                for (int i = 0; i < positions.Count; i++)
                {
                    var p = positions[i].ToOpenTk();
                    var n = normals != null ? normals[i].ToOpenTk() : Vector3.UnitY;
                    var t = tangents != null ? tangents[i].ToOpenTk() : new Vector4(1, 0, 0, 1);
                    var uv = texCoords != null ? texCoords[i].ToOpenTk() : Vector2.Zero;
                    var j = joints != null ? joints[i].ToOpenTk() : Vector4.Zero;
                    var w = weights != null ? weights[i].ToOpenTk() : Vector4.Zero;
                    float wSum = w.X + w.Y + w.Z + w.W;
                    if (wSum > 1e-6f) w /= wSum;

                    vertices[i] = new StandardVertex(p, n, t, uv, j, w);
                    bounds.Expand(p);
                }

                if (normals is null)
                    ComputeSmoothNormals(vertices, indices);

                if (tangents is null)
                    ComputeTangents(vertices, indices);

                string primName = string.IsNullOrWhiteSpace(gltfMesh.Name)
                    ? $"Mesh_{gltfMesh.LogicalIndex}_{pIdx}"
                    : (gltfMesh.Primitives.Count == 1 ? gltfMesh.Name : $"{gltfMesh.Name}_{pIdx}");

                primList.Add(new ParsedPrimitive(
                    primName,
                    vertices,
                    indices,
                    prim.Material?.LogicalIndex ?? -1,
                    bounds));
                pIdx++;
            }
            meshes.Add(primList);
        }

        // Skins
        var skins = new List<SkinData>(gltf.LogicalSkins.Count);
        foreach (var skin in gltf.LogicalSkins)
        {
            int count = skin.JointsCount;
            var jointIndices = new int[count];
            var ibms = new Matrix4[count];
            for (int i = 0; i < count; i++)
            {
                var (jointNode, ibm) = skin.GetJoint(i);
                jointIndices[i] = jointNode.LogicalIndex;
                ibms[i] = ibm.ToOpenTk();
            }
            skins.Add(new SkinData(skin.Name ?? $"Skin_{skin.LogicalIndex}", jointIndices, ibms));
        }

        // Animations
        var animations = new List<AnimationClip>(gltf.LogicalAnimations.Count);
        foreach (var anim in gltf.LogicalAnimations)
        {
            var channels = new List<AnimationChannel>(anim.Channels.Count);
            float maxTime = 0f;

            foreach (var ch in anim.Channels)
            {
                if (ch.TargetNode is null) continue;
                int nodeIdx = ch.TargetNode.LogicalIndex;

                switch (ch.TargetNodePath)
                {
                    case PropertyPath.translation:
                    {
                        var sampler = ch.GetTranslationSampler();
                        if (sampler is null) continue;
                        var interp = ConvertInterpolation(sampler.InterpolationMode);
                        var (times, values) = ExtractVec3Keys(sampler);
                        if (times.Length == 0) continue;
                        maxTime = MathF.Max(maxTime, times[^1]);
                        channels.Add(new AnimationChannel(nodeIdx, AnimationPath.Translation, interp, times, vec3Values: values));
                        break;
                    }
                    case PropertyPath.scale:
                    {
                        var sampler = ch.GetScaleSampler();
                        if (sampler is null) continue;
                        var interp = ConvertInterpolation(sampler.InterpolationMode);
                        var (times, values) = ExtractVec3Keys(sampler);
                        if (times.Length == 0) continue;
                        maxTime = MathF.Max(maxTime, times[^1]);
                        channels.Add(new AnimationChannel(nodeIdx, AnimationPath.Scale, interp, times, vec3Values: values));
                        break;
                    }
                    case PropertyPath.rotation:
                    {
                        var sampler = ch.GetRotationSampler();
                        if (sampler is null) continue;
                        var interp = ConvertInterpolation(sampler.InterpolationMode);
                        var (times, values) = ExtractQuatKeys(sampler);
                        if (times.Length == 0) continue;
                        maxTime = MathF.Max(maxTime, times[^1]);
                        channels.Add(new AnimationChannel(nodeIdx, AnimationPath.Rotation, interp, times, quatValues: values));
                        break;
                    }
                }
            }

            if (channels.Count > 0)
                animations.Add(new AnimationClip(anim.Name ?? $"Anim_{anim.LogicalIndex}", maxTime, channels));
        }

        // Scene graph root
        var scene = gltf.DefaultScene ?? gltf.LogicalScenes.FirstOrDefault();
        ParsedNode ConvertNode(Node n) => new(
            n.Name ?? $"Node_{n.LogicalIndex}",
            n.LogicalIndex,
            n.LocalMatrix.ToOpenTk(),
            n.Mesh?.LogicalIndex ?? -1,
            n.Skin?.LogicalIndex ?? -1,
            n.VisualChildren.Select(ConvertNode).ToList());

        var rootChildren = scene?.VisualChildren.Select(ConvertNode).ToList() ?? [];
        var root = new ParsedNode(scene?.Name ?? "Root", -1, Matrix4.Identity, -1, -1, rootChildren);

        return new ParsedGltfData(root, meshes, materials, images, animations, skins);
    }

    private static AnimationInterpolation ConvertInterpolation(SharpGLTF.Schema2.AnimationInterpolationMode mode) => mode switch
    {
        SharpGLTF.Schema2.AnimationInterpolationMode.STEP => AnimationInterpolation.Step,
        SharpGLTF.Schema2.AnimationInterpolationMode.CUBICSPLINE => AnimationInterpolation.CubicSpline,
        _ => AnimationInterpolation.Linear
    };

    private static (float[] Times, Vector3[] Values) ExtractVec3Keys(IAnimationSampler<System.Numerics.Vector3> sampler)
    {
        if (sampler.InterpolationMode == SharpGLTF.Schema2.AnimationInterpolationMode.CUBICSPLINE)
        {
            var keys = sampler.GetCubicKeys().ToList();
            var times = new float[keys.Count];
            var vals = new Vector3[keys.Count * 3];
            for (int i = 0; i < keys.Count; i++)
            {
                times[i] = keys[i].Key;
                vals[i * 3 + 0] = keys[i].Value.TangentIn.ToOpenTk();
                vals[i * 3 + 1] = keys[i].Value.Value.ToOpenTk();
                vals[i * 3 + 2] = keys[i].Value.TangentOut.ToOpenTk();
            }
            return (times, vals);
        }
        else
        {
            var keys = sampler.GetLinearKeys().ToList();
            var times = new float[keys.Count];
            var vals = new Vector3[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                times[i] = keys[i].Key;
                vals[i] = keys[i].Value.ToOpenTk();
            }
            return (times, vals);
        }
    }

    private static (float[] Times, Quaternion[] Values) ExtractQuatKeys(IAnimationSampler<System.Numerics.Quaternion> sampler)
    {
        if (sampler.InterpolationMode == SharpGLTF.Schema2.AnimationInterpolationMode.CUBICSPLINE)
        {
            var keys = sampler.GetCubicKeys().ToList();
            var times = new float[keys.Count];
            var vals = new Quaternion[keys.Count * 3];
            for (int i = 0; i < keys.Count; i++)
            {
                times[i] = keys[i].Key;
                vals[i * 3 + 0] = keys[i].Value.TangentIn.ToOpenTk();
                vals[i * 3 + 1] = keys[i].Value.Value.ToOpenTk();
                vals[i * 3 + 2] = keys[i].Value.TangentOut.ToOpenTk();
            }
            return (times, vals);
        }
        else
        {
            var keys = sampler.GetLinearKeys().ToList();
            var times = new float[keys.Count];
            var vals = new Quaternion[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                times[i] = keys[i].Key;
                vals[i] = keys[i].Value.ToOpenTk();
            }
            return (times, vals);
        }
    }

    private static void ComputeSmoothNormals(StandardVertex[] vertices, uint[] indices)
    {
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].Normal = Vector3.Zero;

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            uint i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
            var e1 = vertices[i1].Position - vertices[i0].Position;
            var e2 = vertices[i2].Position - vertices[i0].Position;
            var n = Vector3.Cross(e1, e2);
            vertices[i0].Normal += n;
            vertices[i1].Normal += n;
            vertices[i2].Normal += n;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].Normal = vertices[i].Normal.LengthSquared > 1e-8f
                ? vertices[i].Normal.Normalized()
                : Vector3.UnitY;
        }
    }

    private static void ComputeTangents(StandardVertex[] vertices, uint[] indices)
    {
        var tan1 = new Vector3[vertices.Length];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            uint i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
            var e1 = vertices[i1].Position - vertices[i0].Position;
            var e2 = vertices[i2].Position - vertices[i0].Position;
            var duv1 = vertices[i1].TexCoord - vertices[i0].TexCoord;
            var duv2 = vertices[i2].TexCoord - vertices[i0].TexCoord;
            float det = duv1.X * duv2.Y - duv2.X * duv1.Y;
            if (MathF.Abs(det) < 1e-8f) continue;
            float r = 1.0f / det;
            var sdir = (e1 * duv2.Y - e2 * duv1.Y) * r;
            tan1[i0] += sdir;
            tan1[i1] += sdir;
            tan1[i2] += sdir;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            var n = vertices[i].Normal;
            var t = tan1[i] - n * Vector3.Dot(n, tan1[i]);
            vertices[i].Tangent = t.LengthSquared > 1e-8f
                ? new Vector4(t.Normalized(), 1.0f)
                : new Vector4(1, 0, 0, 1);
        }
    }

    public sealed record ParsedGltfData(
        ParsedNode Root,
        List<List<ParsedPrimitive>> Meshes,
        List<ParsedMaterial> Materials,
        List<byte[]> Images,
        List<AnimationClip> Animations,
        List<SkinData> Skins);

    public sealed record ParsedNode(string Name, int NodeIndex, Matrix4 LocalTransform, int MeshIndex, int SkinIndex, List<ParsedNode> Children);

    public sealed record ParsedPrimitive(string Name, StandardVertex[] Vertices, uint[] Indices, int MaterialIndex, BoundingBox Bounds);

    public sealed record ParsedMaterial(
        string Name,
        Vector4 BaseColorFactor,
        float MetallicFactor,
        float RoughnessFactor,
        Vector3 EmissiveFactor,
        AlphaMode AlphaMode,
        float AlphaCutoff,
        bool DoubleSided,
        int BaseColorImageIndex,
        int MetallicRoughnessImageIndex,
        int NormalImageIndex,
        int OcclusionImageIndex,
        int EmissiveImageIndex);
}
