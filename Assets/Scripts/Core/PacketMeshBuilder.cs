using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Why
{
    /// <summary>
    /// What a packet carries, which sets its shape in Why/Packets; its colour is the caller's palette role. One shape a kind,
    /// told apart at a few pixels, and none a form the mark code already gives a meaning (a diamond on a lane is an
    /// export-control valve, a still dash is "no data").
    /// </summary>
    public enum PacketKind
    {
        /// <summary>Money paid (spending, wages, taxes, revenue): a round dot.</summary>
        Money = 0,

        /// <summary>Goods shipped (trade between countries, purchases of things): a square of the dot's area, a crate.</summary>
        Goods = 1,

        /// <summary>
        /// Capital invested or lent (rounds, financing, the payouts that rise to owners): an ingot, a dot drawn out along its lane.
        /// </summary>
        Capital = 2,

        /// <summary>People moving (workers, migrants, customers): a soft dot with no edge.</summary>
        People = 3,

        /// <summary>
        /// A promise: a contract's commitment, not yet paid. A thin dash, so a contract's lane marches as a dashed line
        /// (ADVANTAGE.md section 5: "Contracted commitments are dashed: promises"); it moves, which tells it from the mark code's
        /// still dashes of "no data".
        /// </summary>
        Promise = 4,
    }

    /// <summary>
    /// Accumulates lanes of packets into a single mesh rendered by the Why/Packets shader: the things that move along a path
    /// (money, goods, capital, people, promises: <see cref="PacketKind"/>) drawn as small marks marching from the path's source
    /// to its sink on the flow clock (<see cref="FlowClock"/>), so they move while the year is frozen, the tour paused or the
    /// replay held, and a capture repeats them frame for frame. The shader places each packet from its lane's distance along the
    /// path and the clock alone: a mesh is built once (again only when its paths or flows change), costs no CPU a frame, and
    /// draws all its lanes in one call. Safe to fill on a worker thread; <see cref="ToMesh"/> must run on the main thread.
    /// <list type="bullet">
    /// <item>A lane's size is its rate, packets a second past any point. Every lane of a view travels at the same speed, so the
    /// gaps between packets shrink as the flow grows (the view's key states the unit: one packet a second for $25B a year, say),
    /// and a flow too large for its packets closes them into a solid line.</item>
    /// <item>Lanes are expanded on screen as Why/Line expands its polylines, in graph data space through the warp or in the
    /// renderer's object space with <see cref="GraphMaterials.Raw"/>; speeds and gaps are in the path's own units. A packet is
    /// sized in pixels (plus world units, as a line's width) and anti-aliased.</item>
    /// <item>A lane can keep to the right of its direction of travel by some pixels, so the two directions of a two-way lane
    /// (exports and imports; goods one way and the money paid for them the other) pass each other on one path.</item>
    /// <item>A packet fades in once it is clear of its source and out before its sink, so none pops into being or is cut by
    /// the lane's ends.</item>
    /// <item>A packet's place follows its lane's rate times the clock, so a lane rebuilt at another rate starts a new pattern:
    /// cross-fade the old mesh out over a year swap, as the land layers do, rather than swapping it.</item>
    /// </list>
    /// A trade arch between two countries, the money going back, and a contract between two companies:
    /// <code>
    /// var packets = new PacketMeshBuilder();
    /// float perSecond = (float)(goodsUsdBn / 100);   // the view's key: one packet a second = $100B a year
    /// packets.AddLane(arch, PacketKind.Goods, Roles.Commerce, perSecond, speed: 6, sizePx: 4, id, offsetPx: 3);
    /// packets.AddLane(arch, PacketKind.Money, Roles.Owners, perSecond, speed: 6, sizePx: 3, id, offsetPx: 3, backward: true);
    /// packets.AddLane(contract, PacketKind.Promise, Roles.Commerce, 0.4f, speed: 6, sizePx: 4, id, seed: 0.37f);
    /// Material m = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, 3186));
    /// AddMesh("Packets", packets.ToMesh("Packets"), m);
    /// </code>
    /// </summary>
    public sealed class PacketMeshBuilder
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 Pos;
            public Color32 Color;
            public Vector3 Prev;
            public Vector3 Next;
            public Vector4 P;      // side, size px, size world, id
            public Vector3 Q;      // intensity, kind, offset px to the right of travel
            public Vector4 Lane;   // cycles from the source, cycles to the sink, packets a second, seed
        }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
        };

        readonly List<Vertex> vertices;
        readonly List<int> indices;

        /// <summary>
        /// A builder starting with room for <paramref name="capacityPoints"/> points; it grows as lanes are added. Small by
        /// default, as <see cref="LineMeshBuilder"/>'s is: a view may hold a builder for each of its parts.
        /// </summary>
        public PacketMeshBuilder(int capacityPoints = 32)
        {
            vertices = new List<Vertex>(capacityPoints * 2);
            indices = new List<int>(capacityPoints * 6);
        }

        public int VertexCount => vertices.Count;
        public int LaneCount { get; private set; }

        /// <summary>Empties the builder (keeping its capacity) to build another mesh with it.</summary>
        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            LaneCount = 0;
        }

        /// <summary>
        /// Adds a lane: packets of one <paramref name="kind"/> and <paramref name="color"/> (its alpha too) that leave the path's
        /// first point, or its last when <paramref name="backward"/>, and travel along it to the other end.
        /// </summary>
        /// <param name="path">The lane's points (at least two), in the space the material places them in.</param>
        /// <param name="kind">What the packets carry: their shape.</param>
        /// <param name="color">The packets' colour, alpha included: the palette role of what they carry.</param>
        /// <param name="perSecond">
        /// Packets a second past any point: the flow's size in the view's unit. Taken to the flow clock's nearest seamless rate
        /// (<see cref="FlowClock.SeamlessRate"/>); a lane slower than a packet every 2.3 hours rounds to none and is not drawn.
        /// </param>
        /// <param name="speed">Path units a second each packet travels: one speed for every lane of a view.</param>
        /// <param name="sizePx">The packet's size on screen in pixels (a dot's diameter).</param>
        /// <param name="id">Highlight id (<see cref="Highlighter"/>): the packets glow and dim with the part they belong to.</param>
        /// <param name="intensity">HDR multiplier of the colour (above 1 blooms).</param>
        /// <param name="offsetPx">Pixels the lane keeps to the right of its direction of travel on screen (negative: the left).</param>
        /// <param name="seed">
        /// Where the packets stand at the source, in gaps (0..1): lanes that leave one body together take different seeds, so
        /// their packets do not leave in step.
        /// </param>
        /// <param name="backward">The packets travel from the path's last point to its first: the return lane of a two-way pair.</param>
        /// <param name="sizeWorld">World units added to the size, so the packets grow as the camera comes close.</param>
        public void AddLane(IReadOnlyList<Vector3> path, PacketKind kind, Color32 color, float perSecond, float speed, float sizePx,
            float id, float intensity = 1, float offsetPx = 0, float seed = 0, bool backward = false, float sizeWorld = 0)
        {
            int n = path.Count;
            float rate = FlowClock.SeamlessRate(perSecond);
            // a NaN or infinite rate or speed (an unmeasured flow) draws no lane rather than NaN vertices
            if (n < 2 || !(rate > 0) || float.IsInfinity(rate) || !(speed > 0) || float.IsInfinity(speed)) return;

            // the points in the order of travel, so the screen's tangent points where the packets go (their offset keeps to
            // their right), measured from the source in cycles of the pattern: one cycle per gap between packets
            Vector3 At(int k) => path[backward ? n - 1 - k : k];
            float length = 0;
            for (int k = 1; k < n; k++) length += Vector3.Distance(At(k - 1), At(k));
            if (length <= 0) return;

            float perUnit = rate / speed;
            float total = length * perUnit;
            int baseIndex = vertices.Count;
            float distance = 0;
            for (int k = 0; k < n; k++)
            {
                // summed in the same order as the length, so the sink's last point is exactly zero cycles from it
                if (k > 0) distance += Vector3.Distance(At(k - 1), At(k));
                float cycles = distance * perUnit;
                AddPair(At(k), At(Mathf.Max(k - 1, 0)), At(Mathf.Min(k + 1, n - 1)), color, sizePx, sizeWorld, id, intensity,
                    kind, offsetPx, new Vector4(cycles, total - cycles, rate, seed));
            }

            AddIndices(baseIndex, n);
        }

        void AddPair(Vector3 pos, Vector3 prev, Vector3 next, Color32 color, float sizePx, float sizeWorld, float id,
            float intensity, PacketKind kind, float offsetPx, Vector4 lane)
        {
            Vertex v = new Vertex
            {
                Pos = pos,
                Color = color,
                Prev = prev,
                Next = next,
                P = new Vector4(-1, sizePx, sizeWorld, id),
                Q = new Vector3(intensity, (float)kind, offsetPx),
                Lane = lane
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

            LaneCount++;
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
