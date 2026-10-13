using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Social
{
    /// <summary>
    /// The social graph at the present moment of the causality graph: one cloud per region (the United States, East
    /// Asia, South Asia, Europe, the rest of the world) floating above its streams where the human branch ends. Each
    /// person is a white star (bright enough to bloom apart from the gold of capital) placed by the social graph's own
    /// force-directed layout, sized by followers; the social graph's category clouds (K-pop, Bollywood, football,
    /// tech ...) are camera-facing rings in their colors; mutual follows are faint white ties; a tether runs from every
    /// living person's lifeline end into their star. Capital holders carry a gold ring, officeholders a grey one.
    /// <para>Level of detail by distance from each cloud: far, a glow and the most followed stars; nearer, every star
    /// and the category rings; close, the ties and names. The clouds follow the warp (lens, unrolling) by
    /// re-placing their roots only when <see cref="GraphWarp.Version"/> changes.</para>
    /// </summary>
    public sealed class PopularCloudsLayer : GraphLayer
    {
        public override int Order => 36;
        const float Lift = 1.35f;   // cloud height above the lifelines' top, in SMV heights
        const int FarStars = 14;

        sealed class RegionCloud
        {
            public int Region;
            public GameObject Root;
            public Transform Rings;
            public MeshRenderer Glow, Stars, Bright, Ties, RingLines;
            public Vector3 Data;            // cloud center, data space
            public float Radius = 1;        // world radius of the unit layout
            public readonly List<(SocialPeople.Person p, LabelSpec label)> Names = new List<(SocialPeople.Person, LabelSpec)>();
            public readonly List<(Vector3 local, int cloud)> Categories = new List<(Vector3, int)>();
        }

        SocialPeople people;
        PopularLines lines;
        readonly List<RegionCloud> clouds = new List<RegionCloud>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        MeshRenderer tethers;
        int warpVersion = -1;
        LabelSystem labelSystem;

        public override void Prepare(GraphContext ctx) { }

        public override void Upload(GraphContext ctx)
        {
            people = ctx.Shared<SocialPeople>(SocialPeople.SharedKey);
            lines = ctx.Shared<PopularLines>(PopularLines.PathsKey);
            labelSystem = ctx.Labels;
            if (people == null || lines == null) return;
            double now = DeepTime.NowYear;
            for (int r = 0; r < people.regions.Length; r++)
            {
                Vector3 band = lines.RegionNow[r];
                if (band == Vector3.zero) continue;
                var cloud = new RegionCloud { Region = r, Data = new Vector3(band.x, GraphStyle.HumansY + GraphStyle.SmvHeight * Lift, band.y) };
                Build(cloud, now, ctx);
                clouds.Add(cloud);
            }
            var t = new GameObject("Popular tethers"); t.transform.SetParent(transform, false);
            tethers = MakeRenderer(t.transform, "Tethers", new LineMeshBuilder(), false);
            Debug.Log("[Why] PopularClouds: " + clouds.Count + " regional clouds of " + people.people.Length + " popular people");
        }

        void Build(RegionCloud cloud, double now, GraphContext ctx)
        {
            var region = people.regions[cloud.Region];
            cloud.Root = new GameObject("Social cloud / " + region.name); cloud.Root.transform.SetParent(transform, false);
            var stars = new LineMeshBuilder(); var bright = new LineMeshBuilder(); var ties = new LineMeshBuilder(); var glow = new SurfaceMeshBuilder();
            var members = people.ByRegion[cloud.Region];
            var ranked = new List<SocialPeople.Person>(members); ranked.Sort((a, b) => b.fol.CompareTo(a.fol));
            var sums = new Dictionary<int, (Vector3 sum, int n)>();
            foreach (var p in members)
            {
                if (!p.Alive(now)) continue;
                Vector3 at = Spread(p.Layout); bool top = ranked.IndexOf(p) < FarStars;
                Star(top ? bright : stars, at, .014f + .036f * p.Fame, 1.6f + 3.2f * p.Fame, p.firms.Length > 0, p.InOffice(now, out _));
                if (p.cloud >= 0) { sums.TryGetValue(p.cloud, out var sum); sums[p.cloud] = (sum.sum + at, sum.n + 1); }
                if (p.Fame > .45f || ranked.IndexOf(p) < 40)
                {
                    var label = new LabelSpec { Text = p.name, Fixed = true, Priority = 6 + 12 * p.Fame, Rank = 10 * p.Fame, SizePx = 10 + 3 * p.Fame,
                        Color = new Color(.88f, .95f, 1f), PixelOffset = new Vector2(6, 4), Hidden = true, Ids = IdRange.Single(PopularLines.Id(p)) };
                    ctx.Labels?.Add(label); cloud.Names.Add((p, label));
                }
            }
            foreach (var kv in sums) if (kv.Value.n >= 3) cloud.Categories.Add((kv.Value.sum / kv.Value.n, kv.Key));
            foreach (var e in people.edges)
            {
                var a = people.people[e[0]]; var b = people.people[e[1]];
                if (a.RegionIndex != cloud.Region || b.RegionIndex != cloud.Region || !a.Alive(now) || !b.Alive(now)) continue;
                Color c = SocialPeople.White; c.a = e.Length > 2 && e[2] > 0 ? .22f : .10f;
                ties.AddSegment(Spread(a.Layout), Spread(b.Layout), c, .7f, 0, 0, 1.4f);
            }
            // A soft glow under the whole cloud: what remains from far away.
            Disc(glow, Vector3.zero, 1.1f, SocialPeople.White, .018f);
            cloud.Glow = MakeSurface(cloud.Root.transform, "Glow", glow);
            cloud.Stars = MakeRenderer(cloud.Root.transform, "Stars", stars, true);
            cloud.Bright = MakeRenderer(cloud.Root.transform, "Brightest", bright, true);
            cloud.Ties = MakeRenderer(cloud.Root.transform, "Ties", ties, true);
            // Category rings face the camera: a child per ring, rotated each frame.
            var rings = new GameObject("Category rings"); rings.transform.SetParent(cloud.Root.transform, false); cloud.Rings = rings.transform;
            var ringLines = new LineMeshBuilder();
            foreach (var cat in cloud.Categories)
            {
                float spread = 0; int n = 0;
                foreach (var p in members) if (p.cloud == cat.cloud && p.Alive(now)) { spread += (Spread(p.Layout) - cat.local).sqrMagnitude; n++; }
                float r = Mathf.Clamp(Mathf.Sqrt(spread / Math.Max(1, n)) * 1.5f, .12f, .9f);
                Color col = people.CloudColor(cat.cloud); col.a = .55f;
                var pts = new Vector3[49];
                for (int k = 0; k < pts.Length; k++) { float a = k * Mathf.PI * 2 / 48; pts[k] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
                var holder = new GameObject(people.CloudLabel(cat.cloud)); holder.transform.SetParent(rings.transform, false); holder.transform.localPosition = cat.local;
                var lb = new LineMeshBuilder(); lb.AddPolyline(pts, col, 1.3f, 0, 0, 2.2f);
                MakeRenderer(holder.transform, "Ring", lb, true);
                var label = new LabelSpec { Text = people.CloudLabel(cat.cloud), Fixed = true, Priority = 20, SizePx = 11, Color = col, Hidden = true, Align = TMPro.TextAlignmentOptions.Center };
                ctx.Labels?.Add(label); cloud.Names.Add((null, label));
            }
            var title = new LabelSpec { Text = region.name.ToUpperInvariant() + " · popular people", Fixed = true, Priority = 30, SizePx = 13,
                Color = new Color(.92f, .97f, 1f), Hidden = true, Align = TMPro.TextAlignmentOptions.Center };
            ctx.Labels?.Add(title); cloud.Names.Add((null, title));
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (clouds.Count == 0 || rig == null) return;
            if (warpVersion != GraphWarp.Version) { warpVersion = GraphWarp.Version; Place(); }
            Vector3 cam = rig.Cam.transform.position;
            bool any = false;
            foreach (var c in clouds)
            {
                Vector3 center = c.Root.transform.position;
                float d = Vector3.Distance(cam, center) / Mathf.Max(1e-4f, c.Radius);
                float far = Smooth(40, 14, d), mid = Smooth(14, 5, d), near = Smooth(5, 2.2f, d);
                GraphMaterials.SetAlpha(c.Glow.sharedMaterial, .5f * far + .15f);
                GraphMaterials.SetAlpha(c.Bright.sharedMaterial, far);
                GraphMaterials.SetAlpha(c.Stars.sharedMaterial, mid);
                GraphMaterials.SetAlpha(c.Ties.sharedMaterial, near);
                c.Rings.gameObject.SetActive(mid > .05f);
                if (mid > .05f) foreach (Transform ring in c.Rings) ring.rotation = rig.Cam.transform.rotation;
                bool names = near > .3f, cats = mid > .5f;
                foreach (var n in c.Names)
                {
                    bool show = n.p == null ? cats : names;
                    if (n.label.Hidden == show) { n.label.Hidden = !show; any = true; }
                }
            }
            if (tethers) GraphMaterials.SetAlpha(tethers.sharedMaterial, .55f);
            if (any) labelSystem?.MarkDirty();
        }

        /// <summary>Re-places every cloud (and its labels and tethers) for the current warp.</summary>
        void Place()
        {
            var tb = new LineMeshBuilder(); double now = DeepTime.NowYear;
            foreach (var c in clouds)
            {
                Vector3 band = lines.RegionNow[c.Region];
                Vector3 w0 = GraphWarp.ToWorld(c.Data), w1 = GraphWarp.ToWorld(c.Data + new Vector3(0, 0, Mathf.Max(.01f, band.z)));
                c.Radius = Mathf.Clamp(Vector3.Distance(w0, w1) * .9f, .02f, Vector3.Distance(w0, GraphWarp.ToWorld(c.Data + new Vector3(0, GraphStyle.SmvHeight, 0))) * .8f);
                c.Root.transform.position = w0; c.Root.transform.localScale = Vector3.one * c.Radius;
                foreach (var n in c.Names)
                {
                    Vector3 local = n.p != null ? Spread(n.p.Layout) : Vector3.zero;
                    if (n.p == null) { foreach (var cat in c.Categories) if (people.CloudLabel(cat.cloud) == n.label.Text) local = cat.local + Vector3.up * .15f; }
                    if (n.p == null && n.label.Text.Contains("popular")) local = Vector3.up * 1.15f;
                    n.label.Data = w0 + local * c.Radius;
                }
                foreach (var p in people.ByRegion[c.Region])
                {
                    if (!p.Alive(now) || !lines.Ends.TryGetValue(p.Index, out Vector3 end)) continue;
                    Color col = SocialPeople.White; col.a = .08f + .25f * p.Fame;
                    tb.AddSegment(GraphWarp.ToWorld(end), w0 + Spread(p.Layout) * c.Radius, col, .6f, 0, 0, 1.2f);
                }
            }
            Replace(tethers, tb);
            labelSystem?.MarkDirty();
        }

        /// <summary>The force-directed layout crowds its hubs at the center; a radial power spreads the core so stars stay apart.</summary>
        public static Vector3 Spread(Vector3 v)
        {
            float r = v.magnitude; return r < 1e-5f ? v : v * (Mathf.Pow(r, .55f) / r);
        }

        static void Star(LineMeshBuilder b, Vector3 at, float r, float intensity, bool capital, bool office)
        {
            Color w = SocialPeople.White;
            b.AddSegment(at - Vector3.right * r, at + Vector3.right * r, w, 1.6f, 0, 0, intensity);
            b.AddSegment(at - Vector3.up * r, at + Vector3.up * r, w, 1.6f, 0, 0, intensity);
            b.AddSegment(at - Vector3.forward * r, at + Vector3.forward * r, w, 1.6f, 0, 0, intensity);
            if (capital) Ring(b, at, r * 1.6f, new Color(1, .68f, .2f), 1.6f, 4);
            if (office) Ring(b, at, r * 1.9f, new Color(.62f, .66f, .72f), 1.4f, 2.2f);
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[25];
            for (int k = 0; k < pts.Length; k++) { float a = k * Mathf.PI / 12; pts[k] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Disc(SurfaceMeshBuilder b, Vector3 c, float r, Color color, float alpha)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                var inner = new Vector3[49]; var outer = new Vector3[49];
                for (int k = 0; k < 49; k++)
                {
                    float a = k * Mathf.PI * 2 / 48; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector3 v = axis == 0 ? new Vector3(d.x, 0, d.y) : axis == 1 ? new Vector3(d.x, d.y, 0) : new Vector3(0, d.x, d.y);
                    inner[k] = c; outer[k] = c + v * r;
                }
                Color a0 = color; a0.a = alpha; Color a1 = color; a1.a = 0;
                b.AddBand(inner, outer, new[] { (Color32)a0 }, new[] { (Color32)a1 }, 0, 2f);
            }
        }

        MeshRenderer MakeRenderer(Transform parent, string name, LineMeshBuilder b, bool additive)
        {
            Mesh mesh = b.ToMesh(name); owned.Add(mesh);
            Material mat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, GraphMaterials.QueueHumans + 5, additive)); owned.Add(mat);
            var r = AddMesh(name, mesh, mat); r.transform.SetParent(parent, false); return r;
        }

        MeshRenderer MakeSurface(Transform parent, string name, SurfaceMeshBuilder b)
        {
            Mesh mesh = b.ToMesh(name); owned.Add(mesh);
            Material mat = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, GraphMaterials.QueueHumans + 4, true)); owned.Add(mat);
            var r = AddMesh(name, mesh, mat); r.transform.SetParent(parent, false); return r;
        }

        void Replace(MeshRenderer r, LineMeshBuilder b)
        {
            if (!r) return;
            var f = r.GetComponent<MeshFilter>(); if (f.sharedMesh) { owned.Remove(f.sharedMesh); Destroy(f.sharedMesh); }
            f.sharedMesh = b.ToMesh(r.name); owned.Add(f.sharedMesh);
        }

        static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, x));

        void OnDestroy() { foreach (var o in owned) if (o) Destroy(o); }
    }
}
