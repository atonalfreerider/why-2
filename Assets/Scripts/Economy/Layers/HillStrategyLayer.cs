using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Companies spend too (view "strategy"; the data is <see cref="CorporatePrograms"/>): every stored firm runs the
    /// people's program at its own scale. From each firm's place on its hill its year's money leaves in the directions it
    /// goes: running the business runs down to its suppliers' hills (amber) and to its own workers (blue); investment and
    /// research leave forward over the crest into the future, split into the fear lane (ice: spending so as not to lose
    /// position) and the desire lane (rose: spending for new demand) by Jev's judgment; advertising climbs to the attention
    /// platforms (rose); acquisitions and stakes reach across (white); buybacks and dividends rise to the owners (gold);
    /// cash taxes drop into the bedrock and lobbying runs to the officeholders (steel). White arrows show which way Jev
    /// judged the money moves the firm in the hierarchy (climb, reach down, spread across, lean on the state, harvest).
    /// <para>The AI cascade: the five hyperscalers' AI investment pours down the hill into computers and chips and into
    /// data-center buildings, in proportion to the year's US totals for each (BEA computers and peripherals, Census data
    /// centers). Above the wave a readout sets the firms against the US totals (business investment, advertising, payouts,
    /// lobbying, M&amp;A, the hyperscalers' capex and guidance).</para>
    /// Widths are the square root of dollars. One firm is drawn in full at a time (the selected or hovered one, else the
    /// largest investor); the others' streams are faint. Fiscal years differ by firm (each label says which). Splits of a
    /// firm's costs come from its industry's accounts; the judgments are model evidence, not measurements.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillStrategyLayer : GraphLayer
    {
        public override int Order => 63;
        public override IEnumerable<string> RequiredTexts => new[] { CorporatePrograms.Path };
        enum Part { Streams, Focus, Arrows, Cascade, Count }
        static readonly Color Gold = new Color(1, .70f, .22f), Rose = new Color(1, .26f, .50f), Ice = new Color(.30f, .72f, 1),
            Blue = new Color(.36f, .70f, 1), White = new Color(1, .97f, .92f), Steel = new Color(.62f, .70f, .82f), Amber = new Color(1, .80f, .52f);
        /// <summary>The tickers whose capital spending is the AI build-out (calendar or nearest fiscal year).</summary>
        static readonly string[] Hyperscalers = { "MSFT", "GOOGL", "AMZN", "META", "ORCL" };
        /// <summary>The firm pinned by the strategy panel (tower index; -1: follow hover, else the largest investor).</summary>
        public static int Pinned = -1;
        /// <summary>The focused firm (tower index), its readout and the US totals' readout, for the strategy panel.</summary>
        public static int Focused { get; private set; } = -1;
        public static string FocusText { get; private set; } = "";
        public static string ReadoutText { get; private set; } = "";
        /// <summary>The towers with a program, largest spender first (the panel's order for previous / next).</summary>
        public static readonly List<int> Ranked = new List<int>();
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int firm)> labels = new List<(TextMeshPro, int)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json;
        LandSnapshot snapshot; HillLandscape land; CorporatePrograms programs; GameObject content;
        MeshRenderer focusRenderer; TextMeshPro focusLabel;
        int focused = -2;
        Vector3 attention, seats;

        public override void Prepare(GraphContext ctx) { json = ctx.Text(CorporatePrograms.Path); }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current) Build();
            if (content == null) return;
            bool on = LandView.PresetId == "strategy";
            content.SetActive(on); if (!on) return;
            int f = Focus();
            if (f != focused) { focused = f; Focused = f; DrawFocus(); }
            foreach (var e in labels) { e.text.gameObject.SetActive(true); e.text.transform.rotation = rig.Cam.transform.rotation; }
            if (focusLabel) { focusLabel.gameObject.SetActive(focused >= 0); focusLabel.transform.rotation = rig.Cam.transform.rotation; }
        }

        /// <summary>The firm drawn in full (tower index): the explorer's firm, else the hovered company line's tower, else the
        /// largest investor.</summary>
        int Focus()
        {
            if (programs == null) return -1;
            if (Pinned >= 0 && Pinned < snapshot.Land.Towers.Length && programs.OfTower(Tower(Pinned)) != null) return Pinned;
            if (HillExplorer.Company >= 0 && programs.OfTower(Tower(HillExplorer.Company)) != null) return HillExplorer.Company;
            int life = HillWaveLayer.HoverCompany >= 0 ? HillWaveLayer.HoverCompany : HillWaveLayer.SelectedCompany;
            var lives = CompanyLives.Get();
            if (life >= 0 && lives != null && life < lives.Lives.Length)
            {
                var towers = snapshot.Land.Towers;
                for (int t = 0; t < towers.Length; t++) if (CompanyLives.Matches(towers[t].Ticker, towers[t].Name, lives.Lives[life])) return t;
            }
            int best = -1; double most = 0;
            for (int t = 0; t < snapshot.Land.Towers.Length; t++) { var p = programs.OfTower(Tower(t)); if (p != null && p.Invest > most) { most = p.Invest; best = t; } }
            return best;
        }

        /// <summary>The capture index of a tower.</summary>
        int Tower(int t) => t >= 0 && t < snapshot.Land.Towers.Length ? snapshot.Land.Towers[t].Company : -1;

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current;
            CorporatePrograms.Provide(LandService.Model.Data, json); programs = CorporatePrograms.Get();
            if (programs == null || snapshot.Land.Towers.Length == 0) return;
            foreach (string problem in programs.Problems(snapshot.Land.Towers.Length)) Debug.LogError("[Why] Strategy check failed: " + problem);
            content = new GameObject("Corporate programs"); content.transform.SetParent(transform, false);
            var data = LandService.Model.Data;
            // Where attention is sold (the internet platforms' firms, else the internet hill) and where officeholders sit.
            attention = Vector3.zero; int n = 0; var towers = snapshot.Land.Towers;
            var internet = data.IndustryById("internet");
            for (int t = 0; t < towers.Length; t++) if (internet != null && towers[t].Industry == internet.Index) { attention += HillActivityLayer.CompanyHalo(t); n++; }
            attention = n > 0 ? attention / n + Vector3.up * 3 : internet != null ? land.Halo(internet.Index) + Vector3.up * 3 : land.CloudCenter;
            seats = Vector3.zero; n = 0;
            foreach (var s in HillFoundationLayer.Seats) { seats += s.At; n++; }
            seats = n > 0 ? seats / n : new Vector3(HillLandscape.FootX, HillLandscape.Bedrock, land.CrestZ);
            var faint = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder(); var arrows = new LineMeshBuilder();
            double invest = 0, owners = 0, ads = 0, lobbying = 0, research = 0, deals = 0, taxes = 0, operations = 0;
            Ranked.Clear();
            for (int t = 0; t < towers.Length; t++) if (programs.OfTower(Tower(t)) != null) Ranked.Add(t);
            Ranked.Sort((a, c) => programs.OfTower(Tower(c)).Total.CompareTo(programs.OfTower(Tower(a)).Total));
            for (int t = 0; t < towers.Length; t++)
            {
                var p = programs.OfTower(Tower(t)); if (p == null) continue;
                Streams(faint, fill, t, p, .3f, false);
                Arrows(arrows, t, p);
                invest += p.Invest; owners += p.Owners; ads += p.Attention; lobbying += double.IsNaN(p.Lobbying) ? 0 : p.Lobbying; research += p.Research;
                deals += p.Deals; taxes += p.State; operations += p.Operations;
                // the largest spenders are named with the way their money moves them; every firm is named in the panel
                if (Ranked.IndexOf(t) < 12)
                    Label("<b>" + p.Name + "</b>\n" + CorporatePrograms.DirectionShort[p.Main] + " " + LandFacts.Percent(p.Direction[p.Main]), HillActivityLayer.CompanyHalo(t) + Vector3.up * 3.5f, .4f, Color.Lerp(White, Steel, .25f), t);
            }
            Surface(fill, Part.Streams); Lines(faint, Part.Streams, true); Lines(arrows, Part.Arrows, false);
            Cascade();
            focusRenderer = Lines(new LineMeshBuilder(), Part.Focus, true); focused = -2;
            focusLabel = Label("", Vector3.zero, .6f, White, -1);
            Readout(invest, research, owners, ads, deals, taxes, lobbying, operations);
            Debug.Log("[Why] Strategy " + snapshot.Year + ": " + programs.Firms.Length + " firms (judgments " + (programs.Model ?? "absent") + "); they spent " +
                LandFacts.Money(operations) + " running the business, invested " + LandFacts.Money(invest) + " + research " + LandFacts.Money(research) +
                ", paid owners " + LandFacts.Money(owners) + ", advertising " + LandFacts.Money(ads) + ", deals " + LandFacts.Money(deals) + ", cash taxes " +
                LandFacts.Money(taxes) + ", lobbying $" + lobbying.ToString("0", LandFacts.Ci) + "M; hyperscalers " + LandFacts.Money(programs.Hyperscalers().y2025) +
                " in 2025, guiding " + LandFacts.Money(programs.Hyperscalers().y2026) + " for 2026.");
        }

        // ------------------------------------------------------------------ one firm's streams

        void Streams(LineMeshBuilder b, SurfaceMeshBuilder fill, int t, CorporatePrograms.Firm p, float alpha, bool full)
        {
            Vector3 foot = HillActivityLayer.CompanyPosition(t), halo = HillActivityLayer.CompanyHalo(t);
            float W(double billions) => (full ? .6f : .4f) + Mathf.Sqrt((float)Math.Max(0, billions)) * (full ? .55f : .32f);
            // Running the business: suppliers down the hill (top three supplying industries) and workers on its own slope.
            var bySupplier = new double[land.Hills.Length];
            CorporatePrograms.SplitOperations(p, ValueCapture.Get(), out double suppliers, out double workers, bySupplier);
            var order = new List<int>(); for (int i = 0; i < bySupplier.Length; i++) if (bySupplier[i] > 0) order.Add(i);
            order.Sort((a, c) => bySupplier[c].CompareTo(bySupplier[a]));
            for (int k = 0; k < order.Count && k < (full ? 4 : 2); k++)
                Arc(b, foot + Vector3.up * .5f, land.Market(order[k]) + Vector3.up * .3f, WithAlpha(Amber, alpha), W(bySupplier[order[k]]), full ? 2.2f : 1.2f, 1.5f + .08f * Vector3.Distance(foot, land.Market(order[k])));
            if (p.Industry >= 0 && workers > 0)
                Arc(b, foot + Vector3.up * .4f, land.Market(p.Industry) + Vector3.up * .5f, WithAlpha(Blue, alpha), W(workers), full ? 2.2f : 1.2f, 1.2f);
            // The future: investment and research leave forward over the crest, in the fear and desire lanes.
            double future = p.Invest + p.Research;
            if (future > 0)
            {
                Vector3 lip = new Vector3(halo.x, halo.y + 2.5f, land.CrestZ + 2), ahead = new Vector3(halo.x + 3, Mathf.Max(4, halo.y * .55f), land.CrestZ + (full ? 16 : 12));
                var path = new Vector3[25];
                for (int s = 0; s < path.Length; s++) { float u = s / 24f; path[s] = Vector3.Lerp(Vector3.Lerp(halo, lip, u), Vector3.Lerp(lip, ahead, u), u); }
                Lanes(fill, b, path, (full ? .3f : .15f) + Mathf.Sqrt((float)future) * (full ? .16f : .09f), p.Defensive, alpha, t);
            }
            if (p.Owners > 0) Arc(b, halo + Vector3.up * .4f, land.CloudCenter, WithAlpha(Gold, alpha), W(p.Owners), full ? 3f : 1.6f, 4);
            if (p.Attention > 0) Arc(b, halo + Vector3.up * .3f, attention, WithAlpha(Rose, alpha), W(p.Attention), full ? 2.6f : 1.4f, 3);
            if (p.Deals > 0 && p.Industry >= 0) Arc(b, halo, land.Halo(p.Industry) + new Vector3(0, 1.5f, -2), WithAlpha(White, alpha * .9f), W(p.Deals), full ? 2.2f : 1.1f, 2.5f);
            if (p.State > 0)
            {
                Vector3 down = new Vector3(foot.x, HillLandscape.Bedrock + .3f, Mathf.Min(foot.z, land.CrestZ - 1));
                var drop = new LinePoint[12];
                for (int s = 0; s < drop.Length; s++) drop[s] = new LinePoint(Vector3.Lerp(foot, down, s / 11f), WithAlpha(Steel, alpha), W(p.State), 0, full ? 2 : 1.1f);
                b.AddFlowPath(drop, 0, .5f, t * .3f);
            }
            if (!double.IsNaN(p.Lobbying) && p.Lobbying > 0)
                Arc(b, foot, seats + Vector3.up * 2, WithAlpha(Steel, alpha * (full ? 1 : .6f)), full ? 1.6f : .8f, full ? 2.6f : 1, 2);
        }

        /// <summary>Which way the money moves the firm: an arrow for each direction Jev gave at least a fifth of its weight,
        /// as long as its probability (spreading across: a ring around the firm).</summary>
        void Arrows(LineMeshBuilder b, int t, CorporatePrograms.Firm p)
        {
            if (!p.Judged) return;
            Vector3 at = HillActivityLayer.CompanyHalo(t) + Vector3.up * 1.2f;
            var tech = land.TierBand(snapshot.Year, 4); var raw = land.TierBand(snapshot.Year, 1);
            for (int d = 0; d < CorporatePrograms.Directions; d++)
            {
                float w = p.Direction[d]; if (w < .2f) continue;
                float length = 5 + 16 * w; Color c = WithAlpha(White, .5f + .5f * w); float width = 2.4f + 5 * w;
                Vector3 dir;
                switch ((CorporatePrograms.Direction)d)
                {
                    case CorporatePrograms.Direction.Climb: dir = new Vector3((tech.x + tech.y) * .5f - at.x, 18, 0); break;
                    case CorporatePrograms.Direction.IntegrateDown: dir = new Vector3((raw.x + raw.y) * .5f - at.x, -18, 0); break;
                    case CorporatePrograms.Direction.LeanOnState: dir = Vector3.down; break;
                    case CorporatePrograms.Direction.Harvest: dir = land.CloudCenter - at; break;
                    default:
                        Ring(b, at, 1.2f + 3 * w, c, width, 1.6f + 2 * w); continue;
                }
                dir = dir.normalized; Vector3 tip = at + dir * length;
                b.AddSegment(at, tip, c, width, 0, 0, 1.8f + 2.5f * w);
                Vector3 side = Vector3.Cross(dir, Vector3.forward); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
                b.AddSegment(tip, tip - dir * 2f + side * 1.2f, c, width, 0, 0, 1.8f + 2.5f * w);
                b.AddSegment(tip, tip - dir * 2f - side * 1.2f, c, width, 0, 0, 1.8f + 2.5f * w);
            }
        }

        void DrawFocus()
        {
            var b = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder();
            var p = focused >= 0 ? programs.OfTower(Tower(focused)) : null;
            if (p != null)
            {
                Streams(b, fill, focused, p, .95f, true);
                var bySupplier = new double[land.Hills.Length];
                CorporatePrograms.SplitOperations(p, ValueCapture.Get(), out double suppliers, out double workers, bySupplier);
                string D(int d) => CorporatePrograms.DirectionShort[d] + " " + LandFacts.Percent(p.Direction[d]);
                var dirs = new List<int>(); for (int d = 0; d < CorporatePrograms.Directions; d++) dirs.Add(d);
                dirs.Sort((a, c) => p.Direction[c].CompareTo(p.Direction[a]));
                string text = "<b>" + p.Name + "</b> · " + p.Period + (double.IsNaN(p.Revenue) ? "" : " · revenue " + LandFacts.Money(p.Revenue)) +
                    "\n<color=#FFCC85>runs the business " + LandFacts.Money(p.Operations) + "</color> (suppliers ≈ " + LandFacts.Money(suppliers) + " · <color=#6FB6FF>workers ≈ " + LandFacts.Money(workers) + "</color>)" +
                    "\ninvests " + LandFacts.Money(p.Invest) + (p.Research > 0 ? " + research " + LandFacts.Money(p.Research) : "") + ": <color=#4FB8FF>fear " + LandFacts.Percent(p.Defensive) +
                    "</color> · <color=#FF5C8A>desire " + LandFacts.Percent(1 - p.Defensive) + "</color> · AI " + LandFacts.Percent(p.Ai) +
                    "\n" + (p.Attention > 0 ? "<color=#FF5C8A>advertising " + LandFacts.Money(p.Attention) + "</color> · " : "") + (p.Deals > 0 ? "deals " + LandFacts.Money(p.Deals) + " · " : "") +
                    "<color=#FFB347>owners " + LandFacts.Money(p.Owners) + "</color> · taxes " + LandFacts.Money(p.State) +
                    (double.IsNaN(p.Lobbying) ? "" : " · lobbying $" + p.Lobbying.ToString("0.0", LandFacts.Ci) + "M") +
                    "\n<b>which way its money moves it</b> (Jev): " + D(dirs[0]) + " · " + D(dirs[1]) + " · " + D(dirs[2]) +
                    (string.IsNullOrEmpty(p.Strategy) ? "" : "\n\n<size=90%><i>What it says: " + p.Strategy + "</i></size>");
                FocusText = text;
                focusLabel.text = "<b>" + p.Name + "</b>"; focusLabel.transform.localPosition = HillActivityLayer.CompanyHalo(focused) + Vector3.up * 7;
            }
            else FocusText = "";
            Replace(focusRenderer, b);
            if (focusFill != null) { var filter = focusFill.GetComponent<MeshFilter>(); owned.Remove(filter.sharedMesh); Destroy(filter.sharedMesh); filter.sharedMesh = fill.ToMesh("Strategy focus lanes"); owned.Add(filter.sharedMesh); }
            else { focusFill = Surface(fill, Part.Focus); }
        }
        MeshRenderer focusFill;

        /// <summary>The AI build-out pouring down the hill: each hyperscaler's AI investment (its investment times Jev's AI
        /// share) to computers and chips and to data-center construction, split by the year's US totals for the two.</summary>
        void Cascade()
        {
            var data = LandService.Model.Data; var hardware = data.IndustryById("hardware"); var construction = data.IndustryById("construction");
            if (hardware == null || construction == null) return;
            double computers = programs.Total("privateFixedInvestment2025.computersAndPeripherals"), centers = programs.Total("dataCenterConstruction2025.annual2025");
            if (double.IsNaN(computers) || double.IsNaN(centers) || computers + centers <= 0) return;
            double toChips = computers / (computers + centers), total = 0;
            var b = new LineMeshBuilder(); var towers = snapshot.Land.Towers;
            for (int t = 0; t < towers.Length; t++)
            {
                var p = programs.OfTower(Tower(t)); if (p == null || Array.IndexOf(Hyperscalers, p.Ticker) < 0) continue;
                double ai = p.Invest * p.Ai; if (ai <= 0) continue; total += ai;
                Vector3 halo = HillActivityLayer.CompanyHalo(t);
                Arc(b, halo + Vector3.up * .6f, land.Market(hardware.Index) + Vector3.up * 1.2f, WithAlpha(Gold, .6f), .8f + Mathf.Sqrt((float)(ai * toChips)) * .45f, 2.2f, 6);
                Arc(b, halo + Vector3.up * .6f, land.Market(construction.Index) + Vector3.up * 1.2f, WithAlpha(Gold, .5f), .8f + Mathf.Sqrt((float)(ai * (1 - toChips))) * .45f, 1.8f, 6);
            }
            Lines(b, Part.Cascade, true).sharedMaterial.SetFloat("_FlowSpeed", .8f);
            if (total > 0)
                Label("<b>THE AI BUILD-OUT POURS DOWN THE HILL</b>\nthe hyperscalers' AI investment ≈ " + LandFacts.Money(total) + " (their investment × the AI share Jev judged)" +
                      "\n→ computers & chips " + LandFacts.Percent(toChips) + " · data-center buildings " + LandFacts.Percent(1 - toChips) + " (US 2025: computers $" + computers.ToString("0", LandFacts.Ci) +
                      "B, data centers $" + centers.ToString("0", LandFacts.Ci) + "B)", land.Market(hardware.Index) + new Vector3(0, 14, 2), .2f, Color.Lerp(Gold, White, .4f), -1);
        }

        /// <summary>The firms set against the US totals, floating above the summit.</summary>
        void Readout(double invest, double research, double owners, double ads, double deals, double taxes, double lobbying, double operations)
        {
            string M(double b) => double.IsNaN(b) ? "n/a" : LandFacts.Money(b);
            var h = programs.Hyperscalers();
            double nonres = programs.Total("privateFixedInvestment2025.nonresidential"), adsAll = programs.Total("totalAdSpend"), digital = programs.Total("digitalAdRevenue.total"),
                social = programs.Total("digitalAdRevenue.socialMedia"), buybacks = programs.Total("sp500Buybacks2025.ttmSep2025"), dividends = programs.Total("sp500Dividends2025.ttmSep2025"),
                lobbyAll = programs.Total("federalLobbying2025.total"), ma = programs.Total("usMergersAcquisitions2025");
            string text = "<b>COMPANIES SPEND TOO</b> · the " + programs.Firms.Length + " largest firms in their latest fiscal year" +
                "\nran their businesses on " + M(operations) + " · invested " + M(invest) + " + research " + M(research) + " · paid owners " + M(owners) +
                "\nadvertised " + M(ads) + " (disclosed) · bought " + M(deals) + " of companies and stakes · paid " + M(taxes) + " in cash taxes · lobbied $" + lobbying.ToString("0", LandFacts.Ci) + "M" +
                "\n<color=#9AA8B8>All US business in 2025: invested " + M(nonres) + " (BEA) · advertising " + M(adsAll) + ", digital " + M(digital) + ", social " + M(social) +
                " · S&P 500 buybacks " + M(buybacks) + " + dividends " + M(dividends) + " (12 months to Sep 2025) · lobbying " + M(lobbyAll) + " · M&A $" +
                (double.IsNaN(ma) ? "n/a" : ma.ToString("0.0", LandFacts.Ci) + "T") + "</color>" +
                "\n<color=#FFC04A>AI: the hyperscalers spent " + M(h.y2025) + " on capital in 2025 and guide " + M(h.y2026) + " for 2026</color>";
            ReadoutText = text;
        }

        // ------------------------------------------------------------------ plumbing

        static string Wrap(string s, int width, int lines)
        {
            var words = s.Split(' '); var sb = new System.Text.StringBuilder(); int col = 0, row = 1;
            foreach (var w in words)
            {
                if (col + w.Length > width) { if (row >= lines) { sb.Append('…'); break; } sb.Append('\n'); col = 0; row++; }
                sb.Append(w).Append(' '); col += w.Length + 1;
            }
            return sb.ToString().TrimEnd();
        }

        static void Arc(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[25];
            for (int s = 0; s < pts.Length; s++) { float t = s / 24f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .5f);
        }

        void Lanes(SurfaceMeshBuilder fill, LineMeshBuilder b, Vector3[] path, float width, float fear, float alpha, int id)
        {
            float split = -width * .5f + width * fear;
            for (int lane = 0; lane < 2; lane++)
            {
                float e0 = lane == 0 ? -width * .5f : split + .02f, e1 = lane == 0 ? split - .02f : width * .5f;
                if (e1 <= e0) continue;
                Color hue = lane == 0 ? Ice : Rose; hue.a = alpha;
                var left = new Vector3[path.Length]; var right = new Vector3[path.Length]; var mid = new LinePoint[path.Length];
                for (int k = 0; k < path.Length; k++)
                {
                    Vector3 d = path[Math.Min(k + 1, path.Length - 1)] - path[Math.Max(0, k - 1)];
                    Vector3 normal = Vector3.Cross(d, Vector3.up); normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.right;
                    left[k] = path[k] + normal * e0; right[k] = path[k] + normal * e1;
                    mid[k] = new LinePoint((left[k] + right[k]) * .5f, hue, 1.2f, 0, 2);
                }
                fill.AddBand(left, right, new[] { (Color32)hue }, 0, 1.1f); b.AddFlowPath(mid, 0, 1, id * .41f + lane * .2f);
            }
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[33];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 32; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("Strategy " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3236, true, 0, flow ? 1 : 0)); owned.Add(material);
            if (flow) { material.SetFloat("_FlowFreq", 1.6f); material.SetFloat("_FlowSpeed", .5f); }
            var r = AddMesh("Strategy " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        MeshRenderer Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Strategy " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3234)); owned.Add(material);
            var r = AddMesh("Strategy " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Replace(MeshRenderer r, LineMeshBuilder b)
        {
            var filter = r.GetComponent<MeshFilter>(); Mesh old = filter.sharedMesh; owned.Remove(old); Destroy(old);
            filter.sharedMesh = b.ToMesh(r.name); owned.Add(filter.sharedMesh);
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color, int firm)
        {
            var go = new GameObject("Strategy label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(80, 8);
            go.transform.localScale = Vector3.one * size;
            if (!string.IsNullOrEmpty(text)) labels.Add((t, firm));
            return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null; focusRenderer = null; focusLabel = null; focusFill = null;
        }

        void OnDestroy() { Clear(); Focused = -1; FocusText = ReadoutText = ""; Ranked.Clear(); }
    }
}
