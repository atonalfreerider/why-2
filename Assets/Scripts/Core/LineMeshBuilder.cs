using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Why
{
    /// <summary>One point of a polyline in data space, with per-point styling.</summary>
    public struct LinePoint
    {
        public Vector3 Data;       // (u, y, rho)
        public Color32 Color;      // tint rgb + alpha
        public float WidthPx;      // constant screen width
        public float WidthWorld;   // world width (scales with zoom)
        public float Intensity;    // HDR multiplier (>1 blooms)

        public LinePoint(Vector3 data, Color32 color, float widthPx, float widthWorld = 0, float intensity = 1)
        {
            Data = data;
            Color = color;
            WidthPx = widthPx;
            WidthWorld = widthWorld;
            Intensity = intensity;
        }
    }

    /// <summary>
    /// Accumulates polylines (data space) into a single mesh rendered by the Why/Line shader.
    /// Safe to fill on a worker thread; <see cref="ToMesh"/> must run on the main thread.
    /// </summary>
    public sealed class LineMeshBuilder
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
        readonly List<int> indices;

        public LineMeshBuilder(int capacityPoints = 1024)
        {
            vertices = new List<Vertex>(capacityPoints * 2);
            indices = new List<int>(capacityPoints * 6);
        }

        public int VertexCount => vertices.Count;
        public int PolylineCount { get; private set; }

        /// <summary>Adds a polyline with uniform styling.</summary>
        public void AddPolyline(IReadOnlyList<Vector3> points, Color32 color, float widthPx, float widthWorld, float id,
            float intensity = 1, float flow = 0)
        {
            int n = points.Count;
            if (n < 2) return;
            int baseIndex = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[i > 0 ? i - 1 : i];
                Vector3 next = points[i < n - 1 ? i + 1 : i];
                AddPair(points[i], prev, next, color, widthPx, widthWorld, id, intensity, flow);
            }

            AddIndices(baseIndex, n);
        }

        /// <summary>Adds a polyline with per-point styling.</summary>
        public void AddPolyline(IReadOnlyList<LinePoint> points, float id, float flow = 0)
        {
            int n = points.Count;
            if (n < 2) return;
            int baseIndex = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                LinePoint lp = points[i];
                Vector3 prev = points[i > 0 ? i - 1 : i].Data;
                Vector3 next = points[i < n - 1 ? i + 1 : i].Data;
                AddPair(lp.Data, prev, next, lp.Color, lp.WidthPx, lp.WidthWorld, id, lp.Intensity, flow);
            }

            AddIndices(baseIndex, n);
        }

        /// <summary>Adds a polyline with per-point styling and per-point highlight ids.</summary>
        public void AddPolyline(IReadOnlyList<LinePoint> points, IReadOnlyList<float> ids, float flow)
        {
            int n = Mathf.Min(points.Count, ids.Count);
            if (n < 2) return;
            int baseIndex = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                LinePoint lp = points[i];
                Vector3 prev = points[i > 0 ? i - 1 : i].Data;
                Vector3 next = points[i < n - 1 ? i + 1 : i].Data;
                AddPair(lp.Data, prev, next, lp.Color, lp.WidthPx, lp.WidthWorld, ids[i], lp.Intensity, flow);
            }

            AddIndices(baseIndex, n);
        }

        /// <summary>
        /// Adds a polyline whose flow pulses travel along the path itself, from its first point to its last
        /// (instead of along time toward the present): each point's flow is 2 + its distance from the start
        /// times <paramref name="phasePerUnit"/>, which the line shader reads as a path phase (pulses per phase
        /// unit = the material's _FlowFreq). For money flowing between things that are not apart in time.
        /// </summary>
        public void AddFlowPath(IReadOnlyList<LinePoint> points, float id, float phasePerUnit = 1)
        {
            int n = points.Count;
            if (n < 2) return;
            int baseIndex = vertices.Count;
            float distance = 0;
            for (int i = 0; i < n; i++)
            {
                LinePoint lp = points[i];
                if (i > 0) distance += Vector3.Distance(points[i - 1].Data, lp.Data);
                Vector3 prev = points[i > 0 ? i - 1 : i].Data;
                Vector3 next = points[i < n - 1 ? i + 1 : i].Data;
                AddPair(lp.Data, prev, next, lp.Color, lp.WidthPx, lp.WidthWorld, id, lp.Intensity,
                    2f + distance * phasePerUnit);
            }

            AddIndices(baseIndex, n);
        }

        /// <summary>Adds a straight segment (two points).</summary>
        public void AddSegment(Vector3 a, Vector3 b, Color32 color, float widthPx, float widthWorld, float id,
            float intensity = 1)
        {
            int baseIndex = vertices.Count;
            AddPair(a, a, b, color, widthPx, widthWorld, id, intensity, 0);
            AddPair(b, a, b, color, widthPx, widthWorld, id, intensity, 0);
            AddIndices(baseIndex, 2);
        }

        void AddPair(Vector3 pos, Vector3 prev, Vector3 next, Color32 color, float widthPx, float widthWorld,
            float id, float intensity, float flow)
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

        void AddIndices(int baseIndex, int n)
        {
            for (int i = 0; i < n - 1; i++)
            {
                int a = baseIndex + i * 2;
                indices.Add(a);
                indices.Add(a + 1);
                indices.Add(a + 2);
                indices.Add(a + 2);
                indices.Add(a + 1);
                indices.Add(a + 3);
            }

            PolylineCount++;
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
            // the vertex shader moves everything; never cull on CPU
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
