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
    /// markets it reaches, split ice / rose by what is sold by fear; the share of all wages and owners' income that goes
    /// to people ranked above them (gold, up) and below (blue, down), the population's distribution standing in for their
    /// own money's; a cushion ring around the feet (gold for the years of spending the net worth covers, the home counted;
    /// a full turn is ten); and where people like them were a decade later, on a ruler of the fifths beside the figure: a
    /// labelled step for each fifth they reached, each joined to the figure by a white ribbon as wide as the share who
    /// ended there, at one brightness (white whichever way it went, so gold and rose keep the meanings the view's key
    /// gives them). The labels stack above the figure and beside the ruler. Every mark is drawn at intensity 1 and blended
    /// (the bloom starts above 1 and is kept for what the pointer holds), so the rivers' rose and ice stay true. A reading
    /// of the model, not advice.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillYouLayer : GraphLayer
    {
        public override int Order => 62;
        enum Part { Figure, Rivers, Flow, Futures, Count }
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom.</summary>
        const float Pulse = .2f;
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
            // the figure breathes under the bloom: brighter, it would wash its gold cushion and the labels around it to white
            if (figure) figure.sharedMaterial.SetColor("_Color", Color.white * (.85f + .15f * Mathf.Sin(clock * 2.2f)));
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
            // the view looks back from the future: its screen right, so the ruler stands beside the figure, not toward the eye
            Vector3 right = Quaternion.Euler(0, HillPresets.You().FixedYaw - EconomyStage.Land().Yaw, 0) * Vector3.right;
            Figure(at, r, hill >= 0 && land.Hills[hill].Tier > 0, you.Owner, right); Rivers(at, r, capture, data); Flow(at, r); Futures(at, r, right);
        }

        /// <summary>The cushion ring's full turn, in years of spending.</summary>
        const int CushionYears = 10;

        /// <summary>The viewer: a bright figure with the cushion ring at the feet and their place in words, stacked above
        /// and to the left of the figure (the ruler of futures stands to its right).</summary>
        void Figure(Vector3 at, YouModel.Result r, bool onHill, bool owner, Vector3 right)
        {
            var b = new LineMeshBuilder();
            b.AddSegment(at, at + Vector3.up * 2.2f, White, 3.5f, 0, 0);
            Ring(b, at + Vector3.up * 2.6f, .38f, White, 3, true);
            b.AddSegment(at + Vector3.up * 1.5f + Vector3.forward * .7f, at + Vector3.up * 1.5f - Vector3.forward * .7f, White, 3, 0, 0);
            Ring(b, at, 1.2f, WithAlpha(White, .8f), 2.2f, false);
            // The cushion: the gold arc fills a tenth of the ring per year of spending (net worth with the home, so it is
            // not held to the model's three years of financial assets), a tick at each year.
            float share = Mathf.Clamp01((float)(r.Cushion / CushionYears));
            Arc(b, at + Vector3.up * .05f, 1.7f, 0, Mathf.PI * 2, WithAlpha(Steel, .35f), 1.4f);
            for (int k = 0; k < CushionYears; k++)
            {
                float a = -Mathf.PI * .5f + k * Mathf.PI * 2 / CushionYears; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.AddSegment(at + Vector3.up * .05f + d * 1.55f, at + Vector3.up * .05f + d * 1.85f, WithAlpha(Steel, .6f), 1.2f, 0, 0);
            }
            if (share > 0) Arc(b, at + Vector3.up * .06f, 1.7f, -Mathf.PI * .5f, -Mathf.PI * .5f + share * Mathf.PI * 2, Gold, 4);
            figure = Lines(b, Part.Figure, false);
            string years = r.YearsToIndependence == 0 ? "already there" : double.IsNaN(r.YearsToIndependence) ? "not within a century" : "about " + r.YearsToIndependence.ToString("0", LandFacts.Ci) + " years away";
            Label("<b>YOU</b> · " + Ordinal(r.IncomeRank) + " percentile of household income · " + Ordinal(r.WealthRank) + " of net worth" +
                  "\n<size=85%>" + Rung(r.IncomeRank, onHill, owner) + " · cushion: " + r.Cushion.ToString("0.0", LandFacts.Ci) + " years of spending, home included" +
                  "\nthe gold ring at your feet: one turn is ten years" +
                  "\nindependence: " + PersonLife.Money(r.Independence) + ", " + years + "</size>",
                  at - right * 1.4f + Vector3.up * 4.8f, .22f, White, TextAlignmentOptions.Right);
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
                    Label(PersonLife.Money(spend[j]) + " a year → " + data.Industries[j].Name + " · " + LandFacts.Percent(f) + " sold by fear" + (capture.Judged ? " (Jev)" : ""),
                        path[20] + Vector3.up * 1.1f, .16f, Color.Lerp(Color.Lerp(Rose, Ice, f), White, .4f));
            }
            Surface(fill, Part.Rivers); var lr = Lines(b, Part.Rivers, true); lr.sharedMaterial.SetFloat("_FlowFreq", 2.4f); lr.sharedMaterial.SetFloat("_FlowSpeed", .6f);
        }

        /// <summary>Of all wages and owners' income in the simulated population, the share that goes to people ranked above
        /// the viewer (gold, uphill toward the summit and the clouds) and below (blue, downhill), as long as their shares;
        /// the viewer's spending is assumed to pay people the way everyone's does.</summary>
        void Flow(Vector3 at, YouModel.Result r)
        {
            var b = new LineMeshBuilder();
            float up = (float)r.Uphill, down = 1 - up;
            Vector3 top = at + new Vector3(3 + 6 * up, 3 + 9 * up, -1), low = at + new Vector3(-3 - 6 * down, 0, -1); low.y = land.Ground(low.x, low.z) + .4f;
            Stream(b, at + Vector3.up * 1.2f, top, Gold, 1.2f + 6 * up);
            Stream(b, at + Vector3.up * .8f, low, Blue, 1.2f + 6 * down);
            var lr = Lines(b, Part.Flow, true); lr.sharedMaterial.SetFloat("_FlowFreq", 1.8f); lr.sharedMaterial.SetFloat("_FlowSpeed", .7f);
            Label("<color=#FFC04A>↑ " + LandFacts.Percent(up) + "</color> of all wages and owners' income in the simulated population goes to households ranked above you" +
                  "\nyour spending is assumed to pay people the way everyone's does · your dollar: " + Cents(r.Wages) + " wages · " + Cents(r.Owners) + " owners' surplus (" +
                  LandFacts.Percent(r.Persuaded) + " persuaded) · " + Cents(r.Taxes) + " taxes · " + Cents(r.Upkeep) + " upkeep · " + Cents(r.Imports) + " imports",
                  top + Vector3.up * 1.6f, .17f, Color.Lerp(Gold, White, .4f));
            Label("<color=#6FB6FF>↓ " + LandFacts.Percent(down) + " downhill</color>", low + Vector3.up * 1.2f, .17f, Color.Lerp(Blue, White, .4f));
        }

        /// <summary>Where adults like the viewer were a decade later, on a local ruler beside the figure (to its right on
        /// screen, at its depth, so the steps read up and down rather than toward the eye): a fifth per step, the viewer's
        /// own level with the chest, and a labelled step for each fifth they reached, joined to the figure by a ribbon as
        /// wide as the share who ended there at one brightness, each share with its 95% range (a climb of two fifths is not
        /// drawn as one; a share under half a percent reads "&lt;1%", without one); the heading above names whom the odds
        /// rest on.</summary>
        void Futures(Vector3 at, YouModel.Result r, Vector3 right)
        {
            if (r.OddsLines == 0) return;
            var b = new LineMeshBuilder();
            const float Away = 7.5f;
            Vector3 chest = at + Vector3.up * 1.4f, ruler = chest + right * Away;
            // the fifths reached, counted from the viewer's own; the steps close up when they span all five fifths
            int low = 0, high = 0;
            for (int q = 0; q < Mobility.Quintiles; q++) if (r.Reached[q] > 0) { low = Mathf.Min(low, q - r.Quintile); high = Mathf.Max(high, q - r.Quintile); }
            float step = Mathf.Min(3.4f, 10.5f / Mathf.Max(1, high - low));
            for (int q = 0; q < Mobility.Quintiles; q++)
            {
                int dq = q - r.Quintile; if (dq < low || dq > high) continue;
                double share = r.Reached[q]; Vector3 to = ruler + Vector3.up * (step * dq);
                // a fifth between those reached that nobody ended in keeps a faint tick, unlabelled
                if (share <= 0) { b.AddSegment(to - right * .25f, to + right * .25f, WithAlpha(Steel, .6f), 1.4f, 0, 0); continue; }
                // every step white, the figure's own hue, whichever way it went (its label says): gold (the income uphill,
                // the cushion) and rose (the spending on wants) keep the one meaning the view's key gives them
                var pts = new LinePoint[17];
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 16f, e = t * t * (3 - 2 * t);
                    Vector3 p = Vector3.Lerp(chest, to, t); p.y = Mathf.Lerp(chest.y, to.y, e);
                    pts[s] = new LinePoint(p, WithAlpha(White, .8f), 0, .12f + 1.3f * (float)share);
                }
                b.AddPolyline(pts, 0);
                b.AddSegment(to - right * .45f, to + right * .45f, WithAlpha(White, .9f), 2.2f, 0, 0);
                string where = (dq > 0 ? "climbed to the " : dq < 0 ? "fell to the " : "stayed in the ") + Mobility.QuintileNames[q];
                // under half a percent (a line or two of hundreds) would print "0% ±0": a few did end there, so "<1%", and
                // no range, which whole percents cannot give for so few lines
                string odds = share < .005 ? "<1%" : LandFacts.Percent(share) + " ±" + (100 * r.Margin(share)).ToString("0", LandFacts.Ci);
                Label("<b>" + odds + "</b>\n<size=80%>" + where + "</size>", to + right * .8f, .2f, White, TextAlignmentOptions.Left);
            }
            // the ruler: a fifth per step, the viewer's own fifth level with the chest
            b.AddSegment(ruler + Vector3.up * (step * low - .8f), ruler + Vector3.up * (step * high + .8f), WithAlpha(Steel, .7f), 1.6f, 0, 0);
            // in short lines, so the heading ends before the panel on the right
            string who = r.OddsAllAges ? "adults of every age in your fifth ten years ago\ntoo few were " + Mobility.AgeBandNames[r.AgeBand] + " · "
                : "adults in your fifth aged " + Mobility.AgeBandNames[r.AgeBand] + " ten years ago\n";
            Label("<b>TEN YEARS LATER</b>\n<size=80%>" + who + r.OddsLines + " simulated · ± a 95% range</size>",
                  ruler + Vector3.up * (step * high + 2.1f) - right * .4f, .19f, White, TextAlignmentOptions.Left);
            Lines(b, Part.Futures, false);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The rung the figure stands on, in the hill's own names (its strata, valley floor to under the summit):
        /// the rank times four, owners one up, rounded, as <see cref="HillLandscape.Rung"/> places it; without a paying hill
        /// the valley floor.</summary>
        static string Rung(double rank, bool onHill, bool owner)
        {
            if (!onHill) return "on the valley floor";
            int k = Mathf.Clamp(Mathf.RoundToInt((float)rank * 4 + (owner ? 1 : 0)), 0, 5);
            return new[] { "on the valley floor", "in the lower valley", "on the foothills", "on the slopes", "on the upper slopes", "under the summit" }[k];
        }
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
                    mid[k] = new LinePoint((left[k] + right[k]) * .5f + Vector3.up * .03f, hue, 1f);
                }
                fill.AddBand(left, right, new[] { (Color32)hue }, 0); b.AddFlowPath(mid, 0, 1.2f, id * .37f + lane * .2f);
            }
        }

        static void Stream(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 20f; Vector3 p = Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                pts[s] = new LinePoint(p, WithAlpha(color, Mathf.Lerp(.9f, .5f, t)), width);
            }
            b.AddFlowPath(pts, 0, .8f);
            Vector3 d = (z - pts[17].Data).normalized, side = Vector3.Cross(d, Vector3.up).normalized;
            if (side.sqrMagnitude < 1e-4f) side = Vector3.forward;
            b.AddSegment(z, z - d * 1.2f + side * .6f, color, width, 0, 0);
            b.AddSegment(z, z - d * 1.2f - side * .6f, color, width, 0, 0);
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, bool vertical)
        {
            var pts = new Vector3[33];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 32; pts[s] = c + (vertical ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) : new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            b.AddPolyline(pts, color, width, 0, 0);
        }

        static void Arc(LineMeshBuilder b, Vector3 c, float r, float a0, float a1, Color color, float width)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / (Mathf.PI * 2) * 64)); var pts = new Vector3[n + 1];
            for (int s = 0; s <= n; s++) { float a = Mathf.Lerp(a0, a1, s / (float)n); pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0);
        }

        /// <summary>A mesh of lines, blended: where the lanes and streams meet at the figure, the top one's hue shows instead
        /// of a sum turning white.</summary>
        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("You " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3232, false, 0, flow ? Pulse : 0)); owned.Add(material);
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

        void Label(string text, Vector3 at, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("You label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = align;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(60, 6);
            // a left- or right-aligned label starts or ends at its point, so stacked labels keep their side of the figure
            if (align == TextAlignmentOptions.Left) t.rectTransform.pivot = new Vector2(0, .5f);
            else if (align == TextAlignmentOptions.Right) t.rectTransform.pivot = new Vector2(1, .5f);
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
