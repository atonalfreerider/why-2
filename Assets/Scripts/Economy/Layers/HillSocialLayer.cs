using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Social;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The United States' popular people above the economy (Data/social/people.json): a cloud floating over the wave,
    /// apart from and higher than the gold pool of diversified wealth, laid out by the social graph's own force-directed
    /// layout and lit white, brighter than gold, so attention and capital never read as one. Category clouds (Kardashian
    /// Jenner, leading men, American sports, MAGA, liberal, tech ...) are rings in their colors around their members.
    /// <para>Where a popular person is also a capital holder (Jev judged which listed company they found, run or
    /// control), they are not in the cloud: their star stands on the gold inner circle of owners around that company's
    /// tower (or on its lifeline at the crest when it has no tower), joined to their other companies by gold, with only a
    /// faint thread back to the cloud. Officeholders of the shown year sit at the government's crest edge
    /// (<see cref="HillFoundationLayer.Seats"/>). People appear once they are adults alive in the shown year; their size
    /// is their 2025 following, so the past shows who would become famous, not who was.</para>
    /// <para>Level of detail by camera distance: far, the glow and the most followed stars; nearer, every star and the
    /// category rings and names of the most followed; close, the mutual follows and every name.</para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillSocialLayer : GraphLayer
    {
        public override int Order => 59;
        public override IEnumerable<string> RequiredTexts => new[] { SocialPeople.Path };
        /// <summary>The parsed social graph (null without the file); read by the government's seats.</summary>
        public static SocialPeople People { get; private set; }
        /// <summary>Where each drawn person stands this year (land-local), and how: 0 cloud, 1 company, 2 office.</summary>
        public static readonly Dictionary<int, (Vector3 at, int place)> Placed = new Dictionary<int, (Vector3, int)>();
        public static int HoverPerson { get; private set; } = -1;
        enum Part { Glow, Stars, Bright, Ties, Rings, Anchors, Threads, Count }
        const float CloudRadius = 32, Squash = .5f;
        static readonly Color Gold = new Color(1, .68f, .20f), Grey = new Color(.70f, .74f, .80f);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int person, float fame)> labels = new List<(TextMeshPro, int, float)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content; LandFrame frame;
        Vector3 center;
        int seats = -1, lod = -1;

        public override void Prepare(GraphContext ctx) { People = SocialPeople.Parse(ctx.Text(SocialPeople.Path)); }
        public override void Upload(GraphContext ctx) { frame = EconomyStage.Land(); frame.Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (People == null || HillLandscapeLayer.Current == null || LandService.Current == null) return;
            // Rebuild for a new year, and once the firms and seats it anchors to have been rebuilt.
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || seats != HillFoundationLayer.SeatsVersion) Build();
            if (content == null) return;
            string view = LandView.PresetId;
            bool hidden = view == "mind" || view == "section" || PersonFocus.IsView(view);
            content.SetActive(!hidden); if (hidden) { HoverPerson = -1; return; }
            float d = Vector3.Distance(rig.Cam.transform.position, frame.World(center));
            lod = d < 45 ? 2 : d < 140 ? 1 : 0;
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            foreach (var l in labels)
            {
                bool social = view == "social";
                bool show = l.person < 0 ? lod >= 1 || social
                    : lod >= 2 || (lod >= 1 || social) && l.fame > .62f || Placed.TryGetValue(l.person, out var p) && p.place > 0 && (lod >= 1 || social);
                l.text.gameObject.SetActive(show); if (show) l.text.transform.rotation = rig.Cam.transform.rotation;
            }
            Pick(rig.Cam);
        }

        float Emphasis(Part part, string view)
        {
            if (view == "society" || view == "betrayal") return part == Part.Glow ? .2f : .05f;
            bool social = view == "social";
            switch (part)
            {
                case Part.Glow: return social ? 1 : .7f;
                case Part.Bright: return 1;
                case Part.Stars: return social || lod >= 1 ? 1 : .25f;
                case Part.Ties: return social ? .9f : lod >= 2 ? .6f : 0;
                case Part.Rings: return social ? 1 : lod >= 1 ? .6f : .15f;
                case Part.Anchors: return 1;
                case Part.Threads: return social ? .5f : lod >= 1 ? .25f : .08f;
            }
            return 1;
        }

        void Build()
        {
            Clear(); Placed.Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; seats = HillFoundationLayer.SeatsVersion;
            if (land == null) return;
            content = new GameObject("Popular people"); content.transform.SetParent(transform, false);
            int year = land.Year;
            // Above the wave, over the road side of the crest and higher than the gold pool of wealth.
            center = new Vector3(HillLandscape.FootX + land.CrestWidth * .30f, land.CloudY + 20, land.CrestZ - 10);
            var lives = CompanyLives.Get();
            var stars = new LineMeshBuilder(); var bright = new LineMeshBuilder(); var ties = new LineMeshBuilder(); var rings = new LineMeshBuilder();
            var anchors = new LineMeshBuilder(); var threads = new LineMeshBuilder(); var glow = new SurfaceMeshBuilder();
            var us = People.ByRegion[Math.Max(0, People.RegionIndex("us"))];
            var shown = new List<SocialPeople.Person>();
            foreach (var p in us) if (p.Alive(year) && year - p.born.Value >= 18) shown.Add(p);
            shown.Sort((a, b) => b.fol.CompareTo(a.fol));
            var seatOf = new Dictionary<int, Vector3>();
            foreach (var s in HillFoundationLayer.Seats) if (s.Person >= 0) seatOf[s.Person] = s.At + Vector3.up * 2.2f;
            var cats = new Dictionary<int, List<Vector3>>();
            for (int rank = 0; rank < shown.Count; rank++)
            {
                var p = shown[rank];
                float career = Mathf.Clamp01((year - p.born.Value - 18) / 8f), fame = p.Fame * (.35f + .65f * career);
                Vector3 inCloud = center + Vector3.Scale(Spread(p.Layout), new Vector3(CloudRadius, CloudRadius * Squash, CloudRadius));
                // Capital first: a person who controls a listed company stands in its circle of owners.
                if (Anchor(p, lives, out Vector3 firm, out var others))
                {
                    Star(anchors, firm, .35f + .55f * fame, 5 + 6 * fame);
                    foreach (var o in others) Rope(anchors, firm, o, WithAlpha(Gold, .7f), 1.2f, 2.6f, 1.5f);
                    Rope(threads, firm, inCloud, WithAlpha(SocialPeople.White, .35f), .6f, 1.4f, 4);
                    if (seatOf.TryGetValue(p.Index, out Vector3 office)) Rope(threads, firm, office, WithAlpha(Grey, .5f), .7f, 1.6f, -3);
                    Placed[p.Index] = (firm, 1); Name(p, firm, fame, true);
                    continue;
                }
                if (seatOf.TryGetValue(p.Index, out Vector3 seat))
                {
                    Star(anchors, seat, .3f + .5f * fame, 4.5f + 6 * fame);
                    Rope(threads, seat, inCloud, WithAlpha(SocialPeople.White, .25f), .5f, 1.2f, 6);
                    Placed[p.Index] = (seat, 2); Name(p, seat, fame, true);
                    continue;
                }
                Star(rank < 16 ? bright : stars, inCloud, .22f + .7f * fame, 2.2f + 4.2f * fame);
                Placed[p.Index] = (inCloud, 0);
                if (p.cloud >= 0) { if (!cats.TryGetValue(p.cloud, out var list)) cats[p.cloud] = list = new List<Vector3>(); list.Add(inCloud); }
                if (rank < 60 || fame > .6f) Name(p, inCloud, fame, false);
            }
            foreach (var e in People.edges)
            {
                if (!Placed.TryGetValue(e[0], out var a) || !Placed.TryGetValue(e[1], out var b)) continue;
                Color c = SocialPeople.White; c.a = e.Length > 2 && e[2] > 0 ? .28f : .12f;
                ties.AddSegment(a.at, b.at, c, .7f, 0, 0, 1.4f);
            }
            foreach (var kv in cats)
            {
                if (kv.Value.Count < 3) continue;
                Vector3 c = Vector3.zero; foreach (var v in kv.Value) c += v; c /= kv.Value.Count;
                float spread = 0; foreach (var v in kv.Value) spread += (v - c).sqrMagnitude;
                float r = Mathf.Clamp(Mathf.Sqrt(spread / kv.Value.Count) * 1.5f, 2, 12);
                Color col = People.CloudColor(kv.Key); col.a = .6f;
                Circle(rings, c, r, col, 1.4f, 2.4f); Circle(rings, c + Vector3.up * .3f, r * 1.04f, WithAlpha(col, .25f), 1, 1.6f);
                TextLabel(People.CloudLabel(kv.Key), c + Vector3.up * (r * .5f + 1.5f), .3f, col, -1, 1);
            }
            Glow(glow, center, CloudRadius * 1.15f, .035f);
            TextLabel("<b>POPULAR PEOPLE</b> · United States " + year + "\nwhite: attention (2025 followers) · gold rings: their companies · grey: in office",
                center + Vector3.up * (CloudRadius * Squash + 6), .42f, new Color(.92f, .97f, 1f), -1, 1);
            Surface(glow, Part.Glow);
            Lines(stars, Part.Stars); Lines(bright, Part.Bright); Lines(ties, Part.Ties); Lines(rings, Part.Rings);
            Lines(anchors, Part.Anchors); Lines(threads, Part.Threads, true);
        }

        /// <summary>The company circle a capital holder stands in: their first listed company with a tower (its gold
        /// inner circle of owners), else its lifeline at the crest; the others they are tied to.</summary>
        bool Anchor(SocialPeople.Person p, CompanyLives lives, out Vector3 at, out List<Vector3> others)
        {
            at = default; others = new List<Vector3>(); bool found = false;
            if (lives == null || p.firms.Length == 0) return false;
            foreach (string id in p.firms)
            {
                int life = Array.FindIndex(lives.Lives, l => l.Id == id);
                if (life < 0 || lives.Lives[life].Founded > land.Year) continue;
                Vector3 spot;
                if (HillActivityLayer.OwnerCircle(lives.Lives[life], out Vector3 c, out float r))
                {
                    float a = (p.Index * 2.399963f) % (Mathf.PI * 2);
                    spot = c + new Vector3(Mathf.Cos(a) * r, .55f, Mathf.Sin(a) * r);
                }
                else if (HillWaveLayer.CompanyAtCrest(life, out Vector3 crest)) spot = crest + Vector3.up * 1.2f;
                else continue;
                if (!found) { at = spot; found = true; } else others.Add(spot);
            }
            return found;
        }

        void Name(SocialPeople.Person p, Vector3 at, float fame, bool placed)
        {
            var lives = CompanyLives.Get(); var names = new List<string>();
            foreach (string id in p.firms) { var l = lives == null ? null : Array.Find(lives.Lives, x => x.Id == id); names.Add(l != null ? l.Name : id); }
            string extra = placed && names.Count > 0 ? "\n" + string.Join(" · ", names) : "";
            TextLabel(p.name + extra, at + Vector3.up * (1 + fame), .16f + .12f * fame, placed ? new Color(1, .93f, .8f) : new Color(.88f, .95f, 1f), p.Index, fame);
        }

        void Pick(Camera cam)
        {
            HoverPerson = -1;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || lod < 1) return;
            Vector2 pointer = mouse.position.ReadValue(); float best = 14 * Screen.height / 1080f;
            foreach (var kv in Placed)
            {
                Vector3 s = cam.WorldToScreenPoint(frame.World(kv.Value.at));
                float d = Vector2.Distance(pointer, s);
                if (s.z > 0 && d < best) { best = d; HoverPerson = kv.Key; }
            }
            if (HoverPerson >= 0)
                foreach (var l in labels) if (l.person == HoverPerson) { l.text.gameObject.SetActive(true); }
        }

        /// <summary>The force-directed layout crowds its hubs at the center; a radial power spreads the core so stars stay apart.</summary>
        static Vector3 Spread(Vector3 v)
        {
            float r = v.magnitude; return r < 1e-5f ? v : v * (Mathf.Pow(r, .55f) / r);
        }

        static void Star(LineMeshBuilder b, Vector3 at, float r, float intensity)
        {
            Color w = SocialPeople.White;
            b.AddSegment(at - Vector3.right * r, at + Vector3.right * r, w, 1.8f, 0, 0, intensity);
            b.AddSegment(at - Vector3.up * r, at + Vector3.up * r, w, 1.8f, 0, 0, intensity);
            b.AddSegment(at - Vector3.forward * r, at + Vector3.forward * r, w, 1.8f, 0, 0, intensity);
            Circle(b, at, r * .45f, w, 1.4f, intensity * .8f);
        }

        static void Rope(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++) { float t = s / 20f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .3f);
        }

        static void Circle(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[49];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 48; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Glow(SurfaceMeshBuilder b, Vector3 c, float r, float alpha)
        {
            for (int axis = 0; axis < 2; axis++)
            {
                var inner = new Vector3[49]; var outer = new Vector3[49];
                for (int k = 0; k < 49; k++)
                {
                    float a = k * Mathf.PI * 2 / 48; Vector3 d = axis == 0 ? new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) : new Vector3(Mathf.Cos(a), Mathf.Sin(a) * Squash, 0);
                    inner[k] = c; outer[k] = c + d * r;
                }
                Color a0 = SocialPeople.White; a0.a = alpha; Color a1 = a0; a1.a = 0;
                b.AddBand(inner, outer, new[] { (Color32)a0 }, new[] { (Color32)a1 }, 0, 2);
            }
        }

        void Lines(LineMeshBuilder b, Part part, bool flow = false)
        {
            Mesh mesh = b.ToMesh("Popular " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3220, true, 0, flow ? 1 : 0)); owned.Add(m);
            var r = AddMesh("Popular " + part, mesh, m); r.transform.SetParent(content.transform, false); renderers.Add((r, part));
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Popular " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3218, true)); owned.Add(m);
            var r = AddMesh("Popular " + part, mesh, m); r.transform.SetParent(content.transform, false); renderers.Add((r, part));
        }

        void TextLabel(string text, Vector3 at, float size, Color color, int person, float fame)
        {
            var go = new GameObject("Popular label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(30, 6);
            go.transform.localScale = Vector3.one * size; go.SetActive(false); labels.Add((t, person, fame));
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() { Clear(); Placed.Clear(); People = null; HoverPerson = -1; }
    }
}
