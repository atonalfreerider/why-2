using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Why.Axis
{
    /// <summary>
    /// Straight data-space segments for the Why/Line shader whose opacity can change every frame, for
    /// level of detail that depends on the view (LineMeshBuilder meshes are immutable once uploaded).
    /// Writes the line vertex layout documented in Docs/ARCHITECTURE.md ("Line vertex layout").
    /// Fill on any thread; <see cref="CreateMesh"/> and <see cref="Apply"/> run on the main thread.
    /// </summary>
    public sealed class DynamicLineMesh
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 Pos;
            public Color32 Color;
            public Vector3 Prev;
            public Vector3 Next;
            public Vector4 P;   // side, width px, width world, id
            public Vector2 Q;   // intensity, flow
        }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2),
        };

        readonly List<Vertex> vertices;
        readonly List<byte> baseAlpha; // per segment end (2 per segment)
        readonly List<int> indices;
        Vertex[] buffer;
        Mesh mesh;
        bool dirty;

        public DynamicLineMesh(int capacitySegments = 256)
        {
            vertices = new List<Vertex>(capacitySegments * 4);
            baseAlpha = new List<byte>(capacitySegments * 2);
            indices = new List<int>(capacitySegments * 6);
        }

        public int SegmentCount => baseAlpha.Count / 2;

        /// <summary>Adds a segment from a to b; the alpha of each end is its full (level of detail = 1) opacity.</summary>
        /// <returns>The segment index for <see cref="SetFade"/>.</returns>
        public int AddSegment(Vector3 a, Vector3 b, Color32 colorA, Color32 colorB, float widthPx, float widthWorld,
            float id = 0, float intensity = 1, float flow = 0)
        {
            int segment = SegmentCount;
            int v = vertices.Count;
            AddPair(a, a, b, colorA, widthPx, widthWorld, id, intensity, flow);
            AddPair(b, a, b, colorB, widthPx, widthWorld, id, intensity, flow);
            baseAlpha.Add(colorA.a);
            baseAlpha.Add(colorB.a);
            indices.Add(v);
            indices.Add(v + 1);
            indices.Add(v + 2);
            indices.Add(v + 2);
            indices.Add(v + 1);
            indices.Add(v + 3);
            return segment;
        }

        void AddPair(Vector3 pos, Vector3 prev, Vector3 next, Color32 color, float widthPx, float widthWorld, float id,
            float intensity, float flow)
        {
            Vertex v = new Vertex
            {
                Pos = pos,
                Color = color,
                Prev = prev,
                Next = next,
                P = new Vector4(-1, widthPx, widthWorld, id),
                Q = new Vector2(intensity, flow)
            };
            vertices.Add(v);
            v.P.x = 1;
            vertices.Add(v);
        }

        /// <summary>Main thread: creates the mesh (32-bit indices, infinite bounds, dynamic vertex buffer).</summary>
        public Mesh CreateMesh(string name)
        {
            buffer = vertices.ToArray();
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                    MeshUpdateFlags.DontNotifyMeshUsers;
            mesh.SetVertexBufferParams(buffer.Length, Layout);
            mesh.SetVertexBufferData(buffer, 0, 0, buffer.Length, 0, flags);
            mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Count, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count), flags);
            // the vertex shader moves everything; never cull on CPU
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            dirty = false;
            return mesh;
        }

        /// <summary>Scales the opacity of a segment (0 = invisible, 1 = as built). Takes effect on <see cref="Apply"/>.</summary>
        public void SetFade(int segment, float fade)
        {
            if (buffer == null) return;
            fade = Mathf.Clamp01(fade);
            for (int end = 0; end < 2; end++)
            {
                byte a = (byte)(baseAlpha[segment * 2 + end] * fade + 0.5f);
                int v = segment * 4 + end * 2;
                if (buffer[v].Color.a == a) continue;
                buffer[v].Color.a = a;
                buffer[v + 1].Color.a = a;
                dirty = true;
            }
        }

        /// <summary>Main thread: uploads changed opacities.</summary>
        public void Apply()
        {
            if (!dirty || mesh == null) return;
            mesh.SetVertexBufferData(buffer, 0, 0, buffer.Length, 0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            dirty = false;
        }
    }
}
