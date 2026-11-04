using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Draw
{
    /// <summary>
    /// The area forms of the mark code (spec 3.3, S09): one form, one meaning, so a viewer can learn them. The two forms
    /// that are not areas are built apart: the not-money strand (<see cref="MarkCode.Strand"/>) and the "assumed:" chip
    /// (<see cref="MarkCode.Chip"/>).
    /// </summary>
    public enum Mark
    {
        /// <summary>A solid fill: measured. Regular type; the source and year on hover and in the panel.</summary>
        Measured,

        /// <summary>
        /// An outline with its fill at alpha 0.5 × confidence: judged by Jev, an AI model (italic type and a "J 0.74" tag).
        /// Below a confidence of 0.5 it is not drawn, and the panel lists it as "not drawn: too uncertain".
        /// </summary>
        Judged,

        /// <summary>A dotted edge: modelled or simulated ("≈" before the number, n printed for simulations).</summary>
        Modelled,

        /// <summary>A hatched fill (screen-space stripes at 45 degrees): owed (debts, the state's borrowing, net liabilities, deficits).</summary>
        Owed,

        /// <summary>A dashed empty outline: no data (a total not split, a year not covered).</summary>
        NoData,

        /// <summary>A thin grey hairline outline, labelled with its year: an earlier value.</summary>
        Ghost
    }

    /// <summary>
    /// A mark's area: a parallelogram in the marks' plane, from a corner along two edges (whole lengths), in the layer's
    /// object space. Most marks are rectangles in a plane facing the camera (<see cref="Rect"/>).
    /// </summary>
    public readonly struct MarkQuad
    {
        public readonly Vector3 Origin, Right, Up;

        public MarkQuad(Vector3 origin, Vector3 right, Vector3 up)
        {
            Origin = origin;
            Right = right;
            Up = up;
        }

        /// <summary>A rectangle <paramref name="width"/> by <paramref name="height"/> around a centre, along two unit axes of the plane.</summary>
        public static MarkQuad Rect(Vector3 centre, Vector3 rightAxis, Vector3 upAxis, float width, float height) =>
            new MarkQuad(centre - 0.5f * width * rightAxis - 0.5f * height * upAxis, width * rightAxis, height * upAxis);

        public Vector3 Centre => Origin + 0.5f * (Right + Up);
    }

    /// <summary>
    /// The meshes a layer draws marks and ribbons into: one builder per material of the mark code (fills, hatched fills,
    /// solid lines, dashed and dotted lines), filled on any thread (<see cref="MarkCode"/>, <see cref="FlowRibbon"/>) and
    /// uploaded together on the main thread (<see cref="Upload"/>). Marks lie in a plane facing the camera, in the object
    /// space of the transform they are uploaded under (raw materials, through no warp), LDR and alpha-blended: emphasis
    /// fades them with <see cref="GraphMaterials.SetAlpha"/> on the renderers' materials, and HDR is left to hover and
    /// selection.
    /// </summary>
    public sealed class MarkMeshes
    {
        public readonly SurfaceMeshBuilder Fills = new SurfaceMeshBuilder(), Hatched = new SurfaceMeshBuilder();
        public readonly LineMeshBuilder Edges = new LineMeshBuilder(), Dashed = new LineMeshBuilder(), Dotted = new LineMeshBuilder();

        /// <summary>
        /// Screen pixels per world unit at the marks' plane in the view they are drawn for (<see cref="CameraRig.PixelsPerUnit"/>
        /// once the camera stands at the view's pose; about 1303.7 / distance at 1080p for a frontal view): it sizes the dashes
        /// and dots in pixels and decides when a ribbon is too narrow to draw at its width (<see cref="FlowRibbon.HairlinePx"/>).
        /// </summary>
        public readonly float PxPerUnit;

        /// <summary>Builders for marks drawn in a view of <paramref name="pxPerUnit"/> screen pixels per world unit (a finite positive number).</summary>
        public MarkMeshes(float pxPerUnit)
        {
            if (!(pxPerUnit > 0) || float.IsInfinity(pxPerUnit))
                throw new System.ArgumentOutOfRangeException(nameof(pxPerUnit), pxPerUnit, "marks need the view's pixels per world unit");
            PxPerUnit = pxPerUnit;
        }

        /// <summary>
        /// Main thread: a mesh and a renderer under <paramref name="parent"/> for each builder that holds anything, fills and
        /// hatches at <paramref name="queue"/> and lines one above; the meshes and materials go to <paramref name="owned"/> (to
        /// destroy on a rebuild), the renderers are returned (to fade with the emphasis).
        /// </summary>
        public List<MeshRenderer> Upload(Transform parent, string name, int queue, List<Object> owned)
        {
            var renderers = new List<MeshRenderer>(5);
            Add(renderers, parent, name + " fills", Fills.VertexCount > 0 ? Fills.ToMesh(name + " fills") : null, () => MarkCode.FillMaterial(queue), owned);
            Add(renderers, parent, name + " owed", Hatched.VertexCount > 0 ? Hatched.ToMesh(name + " owed") : null, () => MarkCode.HatchMaterial(queue), owned);
            Add(renderers, parent, name + " edges", Edges.VertexCount > 0 ? Edges.ToMesh(name + " edges") : null, () => MarkCode.LineMaterial(queue + 1), owned);
            Add(renderers, parent, name + " no data", Dashed.VertexCount > 0 ? Dashed.ToMesh(name + " no data") : null, () => MarkCode.DashMaterial(queue + 1), owned);
            Add(renderers, parent, name + " dotted", Dotted.VertexCount > 0 ? Dotted.ToMesh(name + " dotted") : null, () => MarkCode.DotMaterial(queue + 1), owned);
            return renderers;
        }

        static void Add(List<MeshRenderer> renderers, Transform parent, string name, Mesh mesh, System.Func<Material> material, List<Object> owned)
        {
            if (mesh == null) return;
            Material m = material();
            owned?.Add(mesh);
            owned?.Add(m);
            // as GraphLayer.AddMesh makes them: no shadows, probes or occlusion
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            renderers.Add(r);
        }
    }

    /// <summary>
    /// The mark code (spec 3.3, S09): one builder per form, so measured, judged, modelled, owed, missing and earlier values
    /// look the same in every view, with the words each form carries. Builders add geometry to a <see cref="MarkMeshes"/>
    /// and allocate only the mark's own few points, so a layer can build its marks on a worker thread; the materials are
    /// made on the main thread by <see cref="MarkMeshes.Upload"/>.
    /// </summary>
    public static class MarkCode
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>A judgment below this confidence is not drawn (the panel lists it as <see cref="NotDrawn"/>).</summary>
        public const float JudgedFloor = 0.5f;

        /// <summary>A judged mark's fill alpha per unit of confidence (0.5 × confidence).</summary>
        public const float JudgedFillPerConfidence = 0.5f;

        /// <summary>Screen widths (px): the judged outline and the dotted and dashed edges; the ghost's hairline; the seam of a judged split; the not-money strand.</summary>
        public const float EdgePx = 1.5f, GhostPx = 1f, SeamPx = 1f, StrandPx = 1.5f;

        /// <summary>Owed: a stripe every this many screen pixels, this share of it drawn, the gaps empty.</summary>
        public const float HatchPeriodPx = 6f, HatchDuty = 0.5f, HatchStrength = 1f;

        /// <summary>No data: a dash every this many pixels along the outline, this share of it drawn.</summary>
        public const float DashPeriodPx = 8f, DashDuty = 0.6f;

        /// <summary>Modelled edges and the not-money strand: a dot every this many pixels, this share of it drawn.</summary>
        public const float DotPeriodPx = 4f, DotDuty = 0.4f;

        /// <summary>A fill lighter than this (relative luminance, linear) gets a dark dotted edge, a darker one a light edge.</summary>
        const float LightFill = 0.35f;

        /// <summary>The ghost's grey: the dim ink, a little faded.</summary>
        static readonly Color GhostInk = new Color(Roles.InkDim.r, Roles.InkDim.g, Roles.InkDim.b, 0.8f);

        // ------------------------------------------------------------------ rules and words

        /// <summary>Whether a judgment of this confidence is drawn (at least <see cref="JudgedFloor"/>; not a number is not).</summary>
        public static bool Drawn(float confidence) => confidence >= JudgedFloor;

        /// <summary>A judged mark's fill alpha: 0.5 × confidence, or 0 below <see cref="JudgedFloor"/> (not drawn).</summary>
        public static float JudgedFill(float confidence) => Drawn(confidence) ? JudgedFillPerConfidence * Mathf.Min(confidence, 1f) : 0f;

        /// <summary>The judged tag: "J 0.74".</summary>
        public static string JudgedTag(float confidence) => "J " + confidence.ToString("0.00", Ci);

        /// <summary>A judged mark's label: its text in italics, then its tag ("<i>toward a want</i> J 0.74").</summary>
        public static string Judged(string text, float confidence) => "<i>" + text + "</i> " + JudgedTag(confidence);

        /// <summary>What the panel says of a judgment that is not drawn.</summary>
        public const string NotDrawn = "not drawn: too uncertain";

        /// <summary>A modelled number: "≈" before it, and how many runs when it is simulated ("≈$2.1T (n = 400)").</summary>
        public static string Modelled(string number, int runs = 0) =>
            "≈" + number + (runs > 0 ? " (n = " + runs.ToString("N0", Ci) + ")" : "");

        /// <summary>Words of the owed, no-data and not-money forms.</summary>
        public const string Owes = "owes", Borrowed = "borrowed", NoData = "no data", NotSplit = "not split by industry in the data",
            NotMoney = "not money";

        /// <summary>A drawing choice: "assumed: order of dependence".</summary>
        public static string Assumed(string what) => "assumed: " + what;

        /// <summary>An earlier value's label: its year.</summary>
        public static string Ghost(int year) => year.ToString(Ci);

        // ------------------------------------------------------------------ materials (main thread)

        /// <summary>Solid fills: raw (object space), alpha-blended, with hard edges.</summary>
        public static Material FillMaterial(int queue)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, queue));
            m.SetFloat("_EdgeSoft", 0);
            return m;
        }

        /// <summary>Owed: hatched fills.</summary>
        public static Material HatchMaterial(int queue) => GraphMaterials.SetHatch(FillMaterial(queue), HatchPeriodPx, HatchDuty, HatchStrength);

        /// <summary>Solid lines: raw, alpha-blended (never additive: a line's hue is its meaning).</summary>
        public static Material LineMaterial(int queue) => GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, false));

        /// <summary>No data: dashed lines (paths built with the marks' pixels per unit as their phase).</summary>
        public static Material DashMaterial(int queue) => GraphMaterials.SetDash(LineMaterial(queue), DashPeriodPx, DashDuty);

        /// <summary>Modelled edges and not-money strands: dotted lines.</summary>
        public static Material DotMaterial(int queue) => GraphMaterials.SetDash(LineMaterial(queue), DotPeriodPx, DotDuty);

        // ------------------------------------------------------------------ builders (any thread)

        /// <summary>
        /// Adds one mark in its form (<paramref name="confidence"/> is read by <see cref="Mark.Judged"/> only). Returns false
        /// when nothing is drawn: a judgment below <see cref="JudgedFloor"/>. The colour is the role's (a no-data outline is
        /// usually <see cref="EconomyStyle.Roles.Missing"/>; a ghost is always the dim ink).
        /// </summary>
        public static bool Add(MarkMeshes m, Mark form, MarkQuad q, Color color, float id, float confidence = 1f)
        {
            switch (form)
            {
                case Mark.Measured:
                    Fill(m.Fills, q, color, id);
                    return true;
                case Mark.Judged:
                    if (!Drawn(confidence)) return false;
                    Fill(m.Fills, q, WithAlpha(color, color.a * JudgedFill(confidence)), id);
                    Outline(m.Edges, q, color, EdgePx, id, 0);
                    return true;
                case Mark.Modelled:
                    // the fill as measured, its edge dotted in an ink that stands out on it (dark on light fills)
                    Fill(m.Fills, q, color, id);
                    Outline(m.Dotted, q, EdgeInk(color), EdgePx, id, m.PxPerUnit);
                    return true;
                case Mark.Owed:
                    Fill(m.Hatched, q, color, id);
                    return true;
                case Mark.NoData:
                    Outline(m.Dashed, q, color, EdgePx, id, m.PxPerUnit);
                    return true;
                case Mark.Ghost:
                    Outline(m.Edges, q, GhostInk, GhostPx, id, 0);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A measured line (a rule, a tether drawn as money): solid, <paramref name="widthPx"/> wide on screen.</summary>
        public static void Line(MarkMeshes m, IReadOnlyList<Vector3> path, Color color, float widthPx, float id)
        {
            if (path == null || path.Count < 2) return;
            m.Edges.AddPolyline(path, color, widthPx, 0, id);
        }

        /// <summary>
        /// Something that is not money (attention given, wants taken in): a dotted strand <see cref="StrandPx"/> wide with no
        /// width of its own, labelled <see cref="NotMoney"/>.
        /// </summary>
        public static void Strand(MarkMeshes m, IReadOnlyList<Vector3> path, Color color, float id)
        {
            if (path == null || path.Count < 2) return;
            var points = new LinePoint[path.Count];
            for (int i = 0; i < points.Length; i++) points[i] = new LinePoint(path[i], color, StrandPx);
            m.Dotted.AddFlowPath(points, id, m.PxPerUnit);
        }

        /// <summary>
        /// A small mark's motive (spec 3.2): a ring in two parts, <see cref="MotiveLanes.DesireShare"/> of its turn in desire's
        /// colour from twelve o'clock clockwise (seen from the camera), the rest in fear's, never a blend. The plane is given
        /// by its unit axes as the camera sees them.
        /// </summary>
        public static void MotiveRing(MarkMeshes m, Vector3 centre, Vector3 rightAxis, Vector3 upAxis, float radius, float fear,
            float widthPx, float id)
        {
            MotiveLanes split = EconomyStyle.MotiveSplit(1, fear);
            float turn = split.DesireShare;
            Arc(m.Edges, centre, rightAxis, upAxis, radius, 0f, turn, split.DesireColor, widthPx, id);
            Arc(m.Edges, centre, rightAxis, upAxis, radius, turn, 1f, split.FearColor, widthPx, id);
        }

        /// <summary>
        /// The "assumed:" chip (a drawing choice), as a label at a world position (a layer passes its object-space point through
        /// its transform): dim ink on the ink's backing, at the note size, both sRGB-encoded as labels take their colours
        /// (<see cref="EconomyStyle.Roles.Encoded"/>).
        /// </summary>
        public static LabelSpec Chip(string what, Vector3 world, float sizePx = 12f) => new LabelSpec
        {
            Text = Assumed(what),
            Data = world,
            Fixed = true,
            SizePx = sizePx,
            Color = Roles.Encoded(Roles.InkDim),
            Plate = Roles.Encoded(Roles.InkBacking),
            Align = TextAlignmentOptions.Center,
            Priority = 4
        };

        // ------------------------------------------------------------------ geometry

        static void Fill(SurfaceMeshBuilder b, MarkQuad q, Color color, float id)
        {
            Color32 c = color;
            b.AddBand(new[] { q.Origin, q.Origin + q.Right }, new[] { q.Origin + q.Up, q.Origin + q.Right + q.Up }, new[] { c }, id);
        }

        /// <summary>
        /// A closed outline, starting and ending halfway along the bottom edge so the line's two ends meet straight (a loop
        /// closed at a corner shows a notch there); as a path with this phase per unit when it is dashed or dotted (> 0).
        /// </summary>
        static void Outline(LineMeshBuilder b, MarkQuad q, Color color, float widthPx, float id, float phasePerUnit)
        {
            Vector3 o = q.Origin, r = q.Right, u = q.Up;
            Vector3[] corners = { o + 0.5f * r, o + r, o + r + u, o + u, o, o + 0.5f * r };
            if (phasePerUnit <= 0)
            {
                b.AddPolyline(corners, color, widthPx, 0, id);
                return;
            }

            var points = new LinePoint[corners.Length];
            for (int i = 0; i < corners.Length; i++) points[i] = new LinePoint(corners[i], color, widthPx);
            b.AddFlowPath(points, id, phasePerUnit);
        }

        /// <summary>An arc from share <paramref name="from"/> to <paramref name="to"/> of a turn, clockwise from twelve o'clock.</summary>
        static void Arc(LineMeshBuilder b, Vector3 centre, Vector3 right, Vector3 up, float radius, float from, float to, Color color,
            float widthPx, float id)
        {
            if (to - from <= 1e-4f) return;
            int n = Mathf.Max(2, Mathf.CeilToInt((to - from) * 48));
            var points = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = 2 * Mathf.PI * Mathf.Lerp(from, to, i / (float)n);
                points[i] = centre + radius * (Mathf.Sin(a) * right + Mathf.Cos(a) * up);
            }

            b.AddPolyline(points, color, widthPx, 0, id);
        }

        /// <summary>The dotted edge's ink: dark on a light fill, light on a dark one.</summary>
        static Color EdgeInk(Color fill)
        {
            float luminance = 0.2126f * fill.r + 0.7152f * fill.g + 0.0722f * fill.b;
            Color ink = luminance > LightFill ? Roles.InkBacking : Roles.InkText;
            ink.a = 1;
            return ink;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
