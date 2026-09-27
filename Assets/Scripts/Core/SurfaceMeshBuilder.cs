using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Why
{
    /// <summary>
    /// Accumulates filled bands (data space) into a single mesh rendered by the Why/Surface shader.
    /// Safe to fill on a worker thread; <see cref="ToMesh"/> must run on the main thread.
    /// </summary>
    public sealed class SurfaceMeshBuilder
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 Pos;
            public Color32 Color;
            public Vector4 P; // id, intensity, across, noise
        }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
        };

        readonly List<Vertex> vertices = new List<Vertex>(4096);
        readonly List<int> indices = new List<int>(8192);

        public int VertexCount => vertices.Count;

        /// <summary>
        /// Adds a band between two edges sampled at matching indices (e.g. inner and outer boundary of a
        /// region over time). Colors may vary per sample to fade the band in and out.
        /// </summary>
        public void AddBand(IReadOnlyList<Vector3> inner, IReadOnlyList<Vector3> outer, IReadOnlyList<Color32> colors,
            float id, float intensity = 1, float noise = 0)
        {
            int n = Mathf.Min(inner.Count, outer.Count);
            if (n < 2) return;
            int b = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Color32 c = colors[Mathf.Min(i, colors.Count - 1)];
                vertices.Add(new Vertex { Pos = inner[i], Color = c, P = new Vector4(id, intensity, 0, noise) });
                vertices.Add(new Vertex { Pos = outer[i], Color = c, P = new Vector4(id, intensity, 1, noise) });
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = b + i * 2;
                indices.Add(a);
                indices.Add(a + 2);
                indices.Add(a + 1);
                indices.Add(a + 1);
                indices.Add(a + 2);
                indices.Add(a + 3);
            }
        }

        /// <summary>
        /// Adds a band with separate colors on its inner and outer edge (e.g. fading to transparent on the
        /// outside so a layer has no hard border).
        /// </summary>
        public void AddBand(IReadOnlyList<Vector3> inner, IReadOnlyList<Vector3> outer, IReadOnlyList<Color32> innerColors,
            IReadOnlyList<Color32> outerColors, float id, float intensity = 1, float noise = 0)
        {
            int n = Mathf.Min(inner.Count, outer.Count);
            if (n < 2) return;
            int b = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Color32 ci = innerColors[Mathf.Min(i, innerColors.Count - 1)];
                Color32 co = outerColors[Mathf.Min(i, outerColors.Count - 1)];
                vertices.Add(new Vertex { Pos = inner[i], Color = ci, P = new Vector4(id, intensity, 0, noise) });
                vertices.Add(new Vertex { Pos = outer[i], Color = co, P = new Vector4(id, intensity, 1, noise) });
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = b + i * 2;
                indices.Add(a);
                indices.Add(a + 2);
                indices.Add(a + 1);
                indices.Add(a + 1);
                indices.Add(a + 2);
                indices.Add(a + 3);
            }
        }

        /// <summary>Adds a single triangle.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 color, float id, float intensity = 1,
            float noise = 0)
        {
            int i = vertices.Count;
            vertices.Add(new Vertex { Pos = a, Color = color, P = new Vector4(id, intensity, 0.5f, noise) });
            vertices.Add(new Vertex { Pos = b, Color = color, P = new Vector4(id, intensity, 0.5f, noise) });
            vertices.Add(new Vertex { Pos = c, Color = color, P = new Vector4(id, intensity, 0.5f, noise) });
            indices.Add(i);
            indices.Add(i + 1);
            indices.Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                    MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertexBufferParams(vertices.Count, Layout);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, 0, flags);
            mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Count, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count), flags);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
