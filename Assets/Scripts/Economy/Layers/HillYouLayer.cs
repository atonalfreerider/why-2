using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>Where the viewer stands (land-local), published for the "you" view's camera.</summary>
    public static class YouFocus
    {
        public static Vector3 Place { get; internal set; } = new Vector3(60, 14, -6);
        public static bool Ready { get; internal set; }
        /// <summary>Incremented whenever the viewer's place moves.</summary>
        public static int Version { get; internal set; }
    }

    /// <summary>
    /// The viewer on the hill (view "you"; the reading is <see cref="YouModel"/>, the household is told to the "You"
    /// panel through <see cref="YouState"/>): a bright figure at the viewer's rung on the hill of the industry they work
    /// in (the valley floor without one), the same placement the simulated people get; their spending's rivers to the
    /// markets it reaches, split ice / rose by what is sold by fear; which way the income it pays flows, uphill (to people
    /// ranked above them) in gold and downhill in blue; a runway gauge around the feet (gold toward the three years of
    /// spending that make material autonomy); and three possible futures standing in front of the crest, a decade ahead:
    /// a rung up, the same rung, a rung down, as bright as the share of people who stood where the viewer stands ten years
    /// ago and went there. A reading of the model, not advice.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillYouLayer : GraphLayer
    {
        public override int Order => 62;
        enum Part { Figure, Rivers, Flow, Futures, Count }
        static readonly Color Gold = new Color(1, .72f, .24f), Rose = new Color(1, .26f, .50f), Ice = new Color(.30f, .72f, 1),
            Blue = new Color(.36f, .70f, 1), White = new Color(1, .97f, .92f), Steel = new Color(.58f, .68f, .80f);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; YouModel model; GameObject content;
        MeshRenderer figure;
        int version = -1, mobilityYear = -1;
        bool checkedModel;
        float clock;

        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null || LandService.Model?.Lives == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current)
            {
                if (!HillRebuilds.Take()) return;
                snapshot = LandService.Current; land = HillLandscapeLayer.Current;
                model = YouModel.Build(LandService.Model.Lives, snapshot.Year); version = -1; checkedModel = false;
            }
            int mobility = HillMobilityLayer.Shown?.Year ?? -1;
            if (version != YouState.Version || mobility != mobilityYear)
            {
                if (!HillRebuilds.Take()) return;
                version = YouState.Version; mobilityYear = mobility; Build();
            }
            if (content == null) return;
            bool on = LandView.PresetId == "you";
            content.SetActive(on); if (!on) return;
            clock += Time.deltaTime;
            if (figure) figure.sharedMaterial.SetColor("_Color", Color.white * (1.6f + .5f * Mathf.Sin(clock * 2.2f)));
            foreach (var t in labels) t.transform.rotation = rig.Cam.transform.rotation;
        }

        void Build()
        {
            Clear();
            if (model == null || land == null) return;
            var data = LandService.Model.Data; var you = YouState.Inputs; var capture = ValueCapture.Get(); var mobility = HillMobilityLayer.Shown;
            if (mobility != null && mobility.Year != snapshot.Year) mobility = null;
            if (!checkedModel && mobility != null)
            {
                checkedModel = true;
                foreach (string problem in model.Problems(data, capture, mobility)) Debug.LogError("[Why] You check failed: " + problem);
            }
            var r = model.Read(you, data, capture, mobility);
            YouState.ReadingInputs = you; YouState.Reading = r;
            content = new GameObject("You"); content.transform.SetParent(transform, false);
            int hill = you.Industry >= 0 && you.Industry < land.Hills.Length ? you.Industry : -1;
            float z = land.CrestZ - 2.5f;
            Vector3 at = land.Rung(snapshot.Year, hill, (float)r.IncomeRank, you.Owner, z, .7f);
            YouFocus.Place = at; YouFocus.Ready = true; YouFocus.Version++;
            HillPresets.RefreshYouView();
            Figure(at, r); Rivers(at, r, capture, data); Flow(at, r); Futures(at, r, hill, you.Owner);
        }

        /// <summary>The viewer: a bright figure with a runway gauge at the feet and their place in words.</summary>
        void Figure(Vector3 at, YouModel.Result r)
        {
            var b = new LineMeshBuilder();
            b.AddSegment(at, at + Vector3.up * 2.2f, White, 3.5f, 0, 0, 3);
            Ring(b, at + Vector3.up * 2.6f, .38f, White, 3, true, 3.4f);
            b.AddSegment(at + Vector3.up * 1.5f + Vector3.forward * .7f, at + Vector3.up * 1.5f - Vector3.forward * .7f, White, 3, 0, 0, 3);
            Ring(b, at, 1.2f, WithAlpha(White, .8f), 2.2f, false, 2.6f);
            // Runway toward material autonomy (three years of spending): the gold arc fills as the years do.
            float share = Mathf.Clamp01((float)(r.Runway / 3));
            Arc(b, at + Vector3.up * .05f, 1.7f, 0, Mathf.PI * 2, WithAlpha(Steel, .35f), 1.4f, 1.2f);
            if (share > 0) Arc(b, at + Vector3.up * .06f, 1.7f, -Mathf.PI * .5f, -Mathf.PI * .5f + share * Mathf.PI * 2, Gold, 4, 3.6f);
            figure = Lines(b, Part.Figure, false);
            string rung = Rung(r.IncomeRank);
            Label("<b>YOU</b> · " + Ordinal(r.IncomeRank) + " percentile of household income · " + Ordinal(r.WealthRank) + " of net worth\n" + rung +
                  " · runway " + r.Runway.ToString("0.0", LandFacts.Ci) + " years of spending" + (r.Runway >= 3 ? " (past the three that make autonomy)" : " (autonomy needs three)"),
                at + new Vector3(-6, 7.5f, 0), .22f, White);
        }

        /// <summary>The spending's rivers: to the markets of the industries the neighbors' mix first pays, split by fear.</summary>
        void Rivers(Vector3 at, YouModel.Result r, ValueCapture capture, Data.EconomyData data)
        {
            if (capture == null || r.Spending <= 0) return;
            var b = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder(); int n = land.Hills.Length;
            var spend = new double[n]; var fear = new double[n];
            for (int c = 0; c < 6; c++)
                for (int j = 0; j < n; j++)
                {
                    double d = r.Spending * r.Mix[c] * capture.CategorySeller[c][j];
                    spend[j] += d; fear[j] += d * capture.SoldFear[c][j];
                }
            var order = new List<int>(); for (int j = 0; j < n; j++) if (spend[j] > r.Spending * .03) order.Add(j);
            order.Sort((a, c) => spend[c].CompareTo(spend[a]));
            for (int k = 0; k < order.Count && k < 5; k++)
            {
                int j = order[k]; float f = (float)(fear[j] / Math.Max(1e-9, spend[j]));
                // an arc through the air to the market (hugging the ground, rivers zigzag over every ridge between)
                Vector3 start = at + Vector3.up * 1.6f, end = land.Market(j) + Vector3.up * .4f; var path = new Vector3[29];
                float lift = 2 + Vector3.Distance(start, end) * .18f;
                for (int s = 0; s < path.Length; s++) { float t = s / 28f; path[s] = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift; }
                float width = .25f + Mathf.Sqrt((float)(spend[j] / Math.Max(1, r.Spending))) * 1.1f;
                Lanes(fill, b, path, width, f, k);
                if (k < 3)
                    Label(PersonLife.Money(spend[j]) + " a year → " + data.Industries[j].Name + " · " + LandFacts.Percent(f) + " sold by fear",
                        path[20] + Vector3.up * 1.1f, .16f, Color.Lerp(Color.Lerp(Rose, Ice, f), White, .4f));
            }
            Surface(fill, Part.Rivers); var lr = Lines(b, Part.Rivers, true); lr.sharedMaterial.SetFloat("_FlowFreq", 2.4f); lr.sharedMaterial.SetFloat("_FlowSpeed", .6f);
        }

        /// <summary>Which way the income the spending pays flows: uphill (gold, toward the summit and the clouds) and
        /// downhill (blue, toward the valley), as long as their shares.</summary>
        void Flow(Vector3 at, YouModel.Result r)
        {
            var b = new LineMeshBuilder();
            float up = (float)r.Uphill, down = 1 - up;
            Vector3 top = at + new Vector3(3 + 6 * up, 3 + 9 * up, -1), low = at + new Vector3(-3 - 6 * down, 0, -1); low.y = land.Ground(low.x, low.z) + .4f;
            Stream(b, at + Vector3.up * 1.2f, top, Gold, 1.2f + 6 * up, 3.4f);
            Stream(b, at + Vector3.up * .8f, low, Blue, 1.2f + 6 * down, 2.6f);
            var lr = Lines(b, Part.Flow, true); lr.sharedMaterial.SetFloat("_FlowFreq", 1.8f); lr.sharedMaterial.SetFloat("_FlowSpeed", .7f);
            Label("<color=#FFC04A>↑ " + LandFacts.Percent(up) + " uphill</color>: of the wages and owners' income your spending pays, the share reaching people ranked above you" +
                  "\nyour dollar: " + Cents(r.Wages) + " wages · " + Cents(r.Owners) + " owners' surplus (" + LandFacts.Percent(r.Persuaded) + " persuaded) · " +
                  Cents(r.Taxes) + " taxes · " + Cents(r.Upkeep) + " upkeep · " + Cents(r.Imports) + " imports",
                top + Vector3.up * 1.6f, .17f, Color.Lerp(Gold, White, .4f));
            Label("<color=#6FB6FF>↓ " + LandFacts.Percent(down) + " downhill</color>", low + Vector3.up * 1.2f, .17f, Color.Lerp(Blue, White, .4f));
        }

        /// <summary>Three possible futures a decade ahead, in front of the crest: a rung up, the same, a rung down, as bright
        /// as the share of people like the viewer who went there over the last decade.</summary>
        void Futures(Vector3 at, YouModel.Result r, int hill, bool owner)
        {
            if (r.OddsLines == 0) return;
            var b = new LineMeshBuilder();
            float ahead = land.CrestZ + 9;
            (double share, int dq, string word, Color color)[] futures = { (r.Up, 1, "climbed", Gold), (r.Stay, 0, "stayed", Steel), (r.Down, -1, "fell", Rose) };
            foreach (var f in futures)
            {
                if (r.Quintile + f.dq < 0 || r.Quintile + f.dq > 4) continue;
                float rank = Mathf.Clamp01((r.Quintile + f.dq + .5f) / 5f);
                Vector3 to = land.Rung(snapshot.Year, hill, rank, owner, land.CrestZ - .5f, .7f); to.z = ahead; to.x += f.dq * 5;
                float a = .25f + .75f * (float)f.share;
                var pts = new LinePoint[17];
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 16f; Vector3 p = Vector3.Lerp(at + Vector3.up * 1.4f, to + Vector3.up * 1.4f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.5f;
                    pts[s] = new LinePoint(p, WithAlpha(f.color, a * (s % 2 == 0 ? 1 : .25f)), 1 + 4 * (float)f.share, 0, 1 + 3 * (float)f.share);
                }
                b.AddPolyline(pts, 0);
                b.AddSegment(to, to + Vector3.up * 1.8f, WithAlpha(f.color, a), 2.5f, 0, 0, 2 + 3 * (float)f.share);
                Ring(b, to + Vector3.up * 2.2f, .32f, WithAlpha(f.color, a), 2.2f, true, 2 + 3 * (float)f.share);
                Label(LandFacts.Percent(f.share) + " " + f.word, to + Vector3.up * 3.3f, .2f, Color.Lerp(f.color, White, .35f));
            }
            Label("<b>A DECADE AHEAD</b> · of adults in your fifth (" + Mobility.QuintileNames[r.Quintile] + ") aged " + Mobility.AgeBandNames[r.AgeBand] + " ten years ago",
                new Vector3(at.x, at.y + 9, ahead), .19f, White);
            Lines(b, Part.Futures, false);
        }

        // ------------------------------------------------------------------ helpers

        static string Rung(double rank) => rank < .2 ? "valley floor and lower valley" : rank < .4 ? "the foothills" : rank < .6 ? "the slopes" : rank < .8 ? "the upper slopes" : rank < .99 ? "high on the slopes" : "near the summit";
        static string Ordinal(double share)
        {
            int p = Mathf.Clamp(Mathf.RoundToInt((float)share * 100), 1, 99);
            string suffix = p % 100 >= 11 && p % 100 <= 13 ? "th" : p % 10 == 1 ? "st" : p % 10 == 2 ? "nd" : p % 10 == 3 ? "rd" : "th";
            return p.ToString(LandFacts.Ci) + suffix;
        }
        static string Cents(double share) => Mathf.RoundToInt((float)share * 100).ToString(LandFacts.Ci) + "¢";

        void Lanes(SurfaceMeshBuilder fill, LineMeshBuilder b, Vector3[] path, float width, float fear, int id)
        {
            float split = -width * .5f + width * fear;
            for (int lane = 0; lane < 2; lane++)
            {
                float e0 = lane == 0 ? -width * .5f : split + .03f, e1 = lane == 0 ? split - .03f : width * .5f;
                if (e1 <= e0) continue;
                Color hue = lane == 0 ? Ice : Rose; hue.a = .45f;
                var left = new Vector3[path.Length]; var right = new Vector3[path.Length]; var mid = new LinePoint[path.Length];
                for (int k = 0; k < path.Length; k++)
                {
                    Vector3 d = path[Math.Min(k + 1, path.Length - 1)] - path[Math.Max(0, k - 1)];
                    Vector3 normal = new Vector3(-d.z, 0, d.x); normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.right;
                    left[k] = path[k] + normal * e0; right[k] = path[k] + normal * e1;
                    mid[k] = new LinePoint((left[k] + right[k]) * .5f + Vector3.up * .03f, hue, 1f, 0, 1.6f);
                }
                fill.AddBand(left, right, new[] { (Color32)hue }, 0, 1.2f); b.AddFlowPath(mid, 0, 1.2f, id * .37f + lane * .2f);
            }
        }

        static void Stream(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 20f; Vector3 p = Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                pts[s] = new LinePoint(p, WithAlpha(color, Mathf.Lerp(.9f, .5f, t)), width, 0, intensity);
            }
            b.AddFlowPath(pts, 0, .8f);
            Vector3 d = (z - pts[17].Data).normalized, side = Vector3.Cross(d, Vector3.up).normalized;
            if (side.sqrMagnitude < 1e-4f) side = Vector3.forward;
            b.AddSegment(z, z - d * 1.2f + side * .6f, color, width, 0, 0, intensity);
            b.AddSegment(z, z - d * 1.2f - side * .6f, color, width, 0, 0, intensity);
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, bool vertical, float intensity)
        {
            var pts = new Vector3[33];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 32; pts[s] = c + (vertical ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) : new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Arc(LineMeshBuilder b, Vector3 c, float r, float a0, float a1, Color color, float width, float intensity)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / (Mathf.PI * 2) * 64)); var pts = new Vector3[n + 1];
            for (int s = 0; s <= n; s++) { float a = Mathf.Lerp(a0, a1, s / (float)n); pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("You " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3232, true, 0, flow ? 1 : 0)); owned.Add(material);
            var r = AddMesh("You " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("You " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3230)); owned.Add(material);
            var r = AddMesh("You " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        void Label(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject("You label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(60, 6);
            go.transform.localScale = Vector3.one * size; labels.Add(t);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null; figure = null;
        }

        void OnDestroy() { Clear(); YouFocus.Ready = false; }
    }
}
