using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>Where the followed person's structures stand (land-local), published by <see cref="HillPersonLayer"/> for
    /// the view presets and the dilemma arena.</summary>
    public static class PersonFocus
    {
        public static PersonLife Life { get; internal set; }
        /// <summary>The person's place on the hillside, the mind above it, the arena beside it, the ladder's center and span.</summary>
        public static Vector3 Place { get; internal set; } = new Vector3(80, 20, 0);
        public static Vector3 Mind { get; internal set; } = new Vector3(80, 28, 0);
        public static Vector3 Arena { get; internal set; } = new Vector3(70, 26, 0);
        public static Vector3 LadderCenter { get; internal set; } = new Vector3(60, 12, 0);
        public static float LadderSpan { get; internal set; } = 90;
        /// <summary>False until a person was placed (the presets then use a default near the crest).</summary>
        public static bool Ready { get; internal set; }
        /// <summary>Incremented whenever the person or the land changes.</summary>
        public static int Version { get; internal set; }
        /// <summary>The views of the followed person.</summary>
        public static bool IsView(string id) => id == "atom" || id == "dilemma" || id == "ladder";
    }

    /// <summary>
    /// One person, atomistically (view "atom"), and their economic ladder (view "ladder"). The person followed is the
    /// inspected person when one is selected, else a documented notable (the striver first; <see cref="PersonLife"/>).
    /// <para><b>Atom.</b> A figure at the person's place on the hillside (their household's hill at the rung of their
    /// household income rank: class is altitude), with the mind floating above it (<see cref="MindDiagram"/>: the lives
    /// model's five desire and five fear drives that year, the deliberation gate, the choice and the memory). In the
    /// landscape the mind's pulls: rose threads reach from the person toward the markets that serve each desire
    /// (assumed from psyche.json's "served by"), ice threads come from the threats each fear moves away from (losing the
    /// job, isolation, violence, illness, ending without a family; debt arriving from the future; a thin buffer), each
    /// ending in a push away from the threat; blue / gold / steel threads bring income from its source (the employer,
    /// owners' claims in the clouds, the government bedrock, family). The figure sways along the net pull.</para>
    /// <para><b>Ladder.</b> The person's adult life across the wave: each year at that year's z (the road's time), on
    /// the household's hill at the rung of the year's income rank, as a line turning from labor blue to capital gold as
    /// agency rises (gold when in control); jumps between hills arc over the ground; markers name the life's events;
    /// a faint ghost runs at the parent's earnings rank; a walker repeats the life in a loop.</para>
    /// Synthetic records (one line = 100,000 people); motives and placements are model assumptions.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillPersonLayer : GraphLayer
    {
        public override int Order => 59;
        enum Part { Atom, Pulls, Ladder, Count }
        static readonly Color Gold = new Color(1, .70f, .20f), Blue = new Color(.36f, .70f, 1), Steel = new Color(.55f, .66f, .80f),
            White = new Color(1, .96f, .9f), Rose = new Color(1, .25f, .50f), Ice = new Color(.45f, .82f, 1);
        /// <summary>Desires and the industries assumed to serve them (psyche.json "served by"), in DesireIds order.</summary>
        static readonly string[] DesireMarkets = { "hospitality", "entertainment", "legal", "real_estate", "trade" };
        static readonly string[] DesireMarketNote = { "restaurants, groceries", "bars, sports, congregations", "courts, insurance, the state", "rent, mortgages, utilities", "fashion, beauty, dating" };
        /// <summary>Fears and where their threat stands (starvation: the employer, i.e. losing the job), in FearIds order.</summary>
        static readonly string[] FearMarkets = { "", "internet", "state_local", "health", "other_services" };
        static readonly string[] FearThreat = { "losing the job", "loneliness · sold by platforms", "violence · police, guns, courts", "illness, cold · insurance, care", "no partner or children" };
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, Part part)> labels = new List<(TextMeshPro, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content; LandFrame frame;
        MeshRenderer figure, walker; TextMeshPro walkerLabel;
        Vector3 sway; Vector3[] path; float[] pathLength; int[] pathRecord;
        int person = -2, selection = -2, shownRecord = -1;
        float clock;

        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx) { frame = EconomyStage.Land(); frame.Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null || LandService.Model?.Lives == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;   // the landscape catches up first
            // The landscape object is rebuilt for every selection or scenario step with the same geometry: only a new year's
            // land or another person rebuilds (and restarts the arena).
            if (snapshot != LandService.Current || land == null || land.Source != LandService.Current || selection != EconomyState.Person) Build();
            if (content == null) return;
            string view = LandView.PresetId;
            float atom = view == "atom" ? 1 : view == "dilemma" ? .22f : view == "ladder" ? .12f : 0;
            float pulls = view == "atom" ? 1 : view == "dilemma" ? .06f : 0;
            float ladder = view == "ladder" ? 1 : view == "atom" ? .18f : 0;
            bool show = atom + pulls + ladder > 0;
            content.SetActive(show); if (!show) return;
            clock += Time.deltaTime;
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, e.part == Part.Atom ? atom : e.part == Part.Pulls ? pulls : ladder);
            foreach (var e in labels)
            {
                bool on = e.part == Part.Atom ? atom >= .99f : e.part == Part.Pulls ? pulls >= .99f : ladder >= .99f;
                e.text.gameObject.SetActive(on); if (on) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
            // The figure leans along its net pull and back, as if weighing it.
            if (figure) figure.transform.localPosition = PersonFocus.Place + sway * (.5f + .5f * Mathf.Sin(clock * 1.1f));
            Walk(rig);
        }

        void Walk(CameraRig rig)
        {
            if (walker == null || path == null || path.Length < 2) return;
            float total = pathLength[pathLength.Length - 1], period = Mathf.Clamp(total / 9f, 10, 22);
            float d = Mathf.Repeat(clock / period, 1.08f) / 1.0f * total;   // a short rest at the end before it repeats
            d = Mathf.Min(d, total);
            int k = Array.BinarySearch(pathLength, d); if (k < 0) k = Mathf.Clamp(~k - 1, 0, path.Length - 2); else k = Mathf.Min(k, path.Length - 2);
            float seg = Mathf.Max(1e-5f, pathLength[k + 1] - pathLength[k]);
            Vector3 at = Vector3.Lerp(path[k], path[k + 1], (d - pathLength[k]) / seg);
            walker.transform.localPosition = at;
            if (walkerLabel)
            {
                walkerLabel.transform.localPosition = at + Vector3.up * 2.6f;
                walkerLabel.transform.rotation = rig.Cam.transform.rotation;
                int record = pathRecord[k];
                var life = PersonFocus.Life;
                if (record != shownRecord && life != null && record >= 0 && record < life.Records.Length)
                {
                    shownRecord = record; var r = life.Records[record];
                    walkerLabel.text = "<b>" + life.Years[record] + "</b> · age " + (int)r.Age + " · income rank " + PersonLife.Pct(r.IncomeRank) +
                        "\nnet worth " + PersonLife.Money(r.Wealth) + (r.InControl ? " · in control" : "");
                }
            }
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; selection = EconomyState.Person;
            var lives = LandService.Model.Lives; var data = LandService.Model.Data;
            var life = PersonLife.Build(lives, data, snapshot.Year, selection);
            PersonFocus.Life = life; PersonFocus.Version++;
            if (life == null) { PersonFocus.Ready = false; HillPresets.RefreshPersonViews(); return; }
            person = life.Person;
            content = new GameObject("The followed person"); content.transform.SetParent(transform, false);
            var now = life.Now; int last = life.Records.Length - 1, industry = life.IndustryAt(last);
            Vector3 place = land.Rung(life.Year + .5, industry, now.IncomeRank, Owner(now), land.CrestZ - 3, 0);
            PersonFocus.Place = place; PersonFocus.Mind = place + Vector3.up * 8.5f;
            // The arena floats over the valley side of the person, a little back in time, clear of the slope.
            Vector3 arena = place + new Vector3(-12, 0, -5); arena.y = Mathf.Max(place.y, land.Ground(arena.x, arena.z)) + 5.5f;
            PersonFocus.Arena = arena;
            Atom(life, place, industry, data);
            Ladder(life, data);
            PersonFocus.Ready = true;
            Register(life, data);
            HillPresets.RefreshPersonViews();
            Debug.Log("[Why] Person " + life.Person + " (" + life.Why + ") " + life.Year + ": " + life.Records.Length + " adult years, " + life.Events.Count +
                " events, industry " + (industry >= 0 ? data.Industries[industry].Id : "none") + ", rank " + PersonLife.Pct(now.IncomeRank) +
                ", strategy " + PrisonersDilemma.Name(life.Traits.Strategy) + "; drives and placements are model assumptions.");
        }

        static bool Owner(PersonYear r) => r.SelfEmployed && r.IncomeRank > .6f;

        void Atom(PersonLife life, Vector3 place, int industry, Data.EconomyData data)
        {
            var lives = LandService.Model.Lives; var now = life.Now;
            var mind = MindState.Of(lives, life.Person, life.Year);
            if (mind == null) return;
            int player = snapshot.Players.PlayerOfPerson != null && life.Person < snapshot.Players.PlayerOfPerson.Length ? snapshot.Players.PlayerOfPerson[life.Person] : -1;
            string cohort = player >= 0 ? HillLandscapeLayer.PlayerTitle(snapshot.Players.Players[player]) : "";
            mind.Title = "SYNTHETIC RECORD #" + life.Person + " · " + life.Why + " · " + life.Year;
            mind.Note = "age " + (int)now.Age + (cohort.Length > 0 ? " · " + cohort : "") + " · one line = 100,000 people · drives are model assumptions";
            var b = new LineMeshBuilder();
            MindDiagram.Build(b, PersonFocus.Mind, mind, (t, at, size, c) => Label(t, at, size, c, Part.Atom));
            // The spine: from the head up into the memory at the mind's base.
            MindDiagram.Thread(b, place + Vector3.up * 1.9f, PersonFocus.Mind + MindDiagram.Memory(1) + Vector3.down * .3f, WithAlpha(White, .6f), 1.6f, 1.8f, 0);
            Lines(b, Part.Atom, true, "Atom mind");
            // The figure (moves): body, head, the gold ring of one in control, its balance-sheet rings.
            var f = new LineMeshBuilder(); Color body = now.InControl ? Gold : White;
            f.AddSegment(Vector3.zero, Vector3.up * 1.25f, body, 3, .05f, 0, 2.6f);
            f.AddSegment(Vector3.up * .95f + Vector3.left * .45f, Vector3.up * .95f + Vector3.right * .45f, body, 2.2f, .04f, 0, 2.2f);
            MindDiagram.Sphere(f, Vector3.up * 1.55f, .25f, body, 2.2f, 3);
            MindDiagram.Ring(f, Vector3.up * .02f, .55f, body, 2, 2.4f, false);
            MindDiagram.Ring(f, Vector3.up * .06f, .45f + Mathf.Log10(1 + Math.Max(0, now.Wealth)) * .09f, Gold, 1.6f, 2.2f, false);
            if (now.Debt > 0) MindDiagram.Ring(f, Vector3.up * .12f, .4f + Mathf.Log10(1 + now.Debt) * .08f, Rose, 1.6f, 2.2f, false);
            figure = Lines(f, Part.Atom, false, "Followed person"); figure.transform.localPosition = place;
            Pulls(life, place, industry, mind, data);
        }

        void Pulls(PersonLife life, Vector3 place, int industry, MindState mind, Data.EconomyData data)
        {
            var b = new LineMeshBuilder(); var now = life.Now; Vector3 net = Vector3.zero;
            Vector3 Market(string id)
            {
                var ind = data.IndustryById(id);
                if (ind == null || land.Hills[ind.Index].Tier == 0) return land.ValleyFloor(.3f, place.z - 8);
                return land.Market(ind.Index);
            }
            string IndustryName(string id) => data.IndustryById(id)?.Name ?? id;
            // Desire: pulled toward the markets that serve each drive.
            for (int k = 0; k < 5; k++)
            {
                float w = MindDiagram.Share(mind, false, k); if (w < .015f) continue;
                Vector3 to = Market(DesireMarkets[k]); Color c = MindDiagram.DesireColor(k);
                Arc(b, place + Vector3.up * 1.0f, to, c, 1 + 22 * w, 2.4f + 4 * w, true);
                net += Flat(to - place) * w;
                Label("DESIRE · " + MindDiagram.Name(false, k).ToUpperInvariant() + " " + PersonLife.Pct(w) + "\n→ " + IndustryName(DesireMarkets[k]) + " (" + DesireMarketNote[k] + ")",
                    to + Vector3.up * 1.6f, .11f, Color.Lerp(c, Color.white, .3f), Part.Pulls);
            }
            // Fear: pushed away from each threat.
            bool employed = now.Employed && now.Industry >= 0;
            for (int k = 0; k < 5; k++)
            {
                float w = MindDiagram.Share(mind, true, k); if (w < .015f) continue;
                Vector3 from = k == 0 ? employed ? MarketOf(now.Industry, place) + Vector3.up * .4f : land.ValleyFloor(0, place.z - 4) : Market(FearMarkets[k]);
                string where = k == 0 ? employed ? "losing the job · " + data.Industries[now.Industry].Name : "destitution · no paying hill" : FearThreat[k];
                net -= Threat(b, from, place, MindDiagram.FearColor(k), w, "FEAR · " + MindDiagram.Name(true, k).ToUpperInvariant() + " " + PersonLife.Pct(w) + "\n" + where);
            }
            // Debt arrives from the future (spending drawn on income not yet earned); a thin buffer is the valley below.
            if (now.Debt > 500)
            {
                float w = Mathf.Clamp(now.DebtService * 1.6f, .05f, .5f);
                Vector3 future = new Vector3(place.x, place.y + 6, land.CrestZ + 9);
                net -= Threat(b, future, place, Ice, w, "DEBT FROM THE FUTURE\nowes " + PersonLife.Money(now.Debt) + " · payments " + PersonLife.Pct(now.DebtService) + " of income");
            }
            if (now.Runway < .25f)
            {
                float w = Mathf.Clamp01(1 - 4 * now.Runway) * .4f;
                net -= Threat(b, land.ValleyFloor(0, place.z - 1), place, Ice, w, "THIN BUFFER\n" + Mathf.RoundToInt(now.Runway * 12) + " months of spending saved");
            }
            // Income from its source: the employer (wages blue, own business gold), the owners' claims (gold, from the
            // clouds), the government bedrock (steel), family (blue) and inheritance (gold).
            double income = Math.Max(1, now.Earned + now.Transfers + now.CapitalIncome);
            if (now.Earned > 100)
            {
                int ind = industry >= 0 ? industry : now.Industry;
                Vector3 from = ind >= 0 ? Employer(ind) : land.ValleyFloor(.5f, place.z - 6);
                Income(b, from, place, now.SelfEmployed ? Gold : Blue, now.Earned / income,
                    (now.SelfEmployed ? "OWN BUSINESS" : "EMPLOYER") + (ind >= 0 ? " · " + data.Industries[ind].Name : "") + "\n" + PersonLife.Money(now.Earned) + " a year");
            }
            if (now.Transfers > 100)
            {
                Vector3 from = Pillar(now.SocialSecurity > 0 ? "SOCIAL SECURITY" : "OTHER FEDERAL", place);
                Income(b, from, place, Steel, now.Transfers / income, "GOVERNMENT · " + (now.SocialSecurity > 0 ? "Social Security" : "benefits") + "\n" + PersonLife.Money(now.Transfers) + " a year");
            }
            if (now.CapitalIncome > 100)
                Income(b, land.CloudCenter, place, Gold, now.CapitalIncome / income, "CAPITAL · claims on every hill\n" + PersonLife.Money(now.CapitalIncome) + " a year");
            if (now.FamilyDependent && now.SupportParent >= 0 && snapshot.Players.PlayerOfPerson != null && now.SupportParent < snapshot.Players.PlayerOfPerson.Length)
            {
                int parent = snapshot.Players.PlayerOfPerson[now.SupportParent];
                if (parent >= 0) Income(b, land.People[parent], place, Blue, .3f, "FAMILY · a parent's support");
            }
            if (now.Inherited > 1000) Income(b, place + Vector3.up * 14 + Vector3.back * 4, place, Gold, .5f, "INHERITANCE\n" + PersonLife.Money(now.Inherited));
            Lines(b, Part.Pulls, true, "Atom pulls");
            net.y = 0; sway = net.sqrMagnitude > 1e-6f ? net.normalized * Mathf.Min(1.4f, net.magnitude * 3) : Vector3.zero;
            Label("pulled by desire · pushed by fear · fed by its sources", place + Vector3.down * .8f + Vector3.back * 1.5f, .09f, Steel, Part.Pulls);
        }

        /// <summary>An industry's market; for government, the valley floor (its hills lie in the bedrock).</summary>
        Vector3 MarketOf(int industry, Vector3 place) =>
            industry >= 0 && industry < land.Hills.Length && land.Hills[industry].Tier > 0 ? land.Market(industry) : land.ValleyFloor(.4f, place.z - 6);

        /// <summary>The employer: the nearest named firm seat on the industry's hill, else its market.</summary>
        Vector3 Employer(int industry)
        {
            var towers = snapshot.Land.Towers; Vector3 best = MarketOf(industry, PersonFocus.Place); float d = float.MaxValue;
            for (int t = 0; t < towers.Length; t++)
            {
                if (towers[t].Industry != industry) continue;
                Vector3 at = HillActivityLayer.CompanyPosition(t);
                if (at == Vector3.zero) continue;
                float e = Vector3.Distance(at, PersonFocus.Place);
                if (e < d) { d = e; best = at; }
            }
            return best;
        }

        Vector3 Pillar(string name, Vector3 place)
        {
            foreach (var p in HillFoundationLayer.Pillars) if (p.Name == name) return p.Base;
            return new Vector3(place.x, HillLandscape.Bedrock, place.z);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }

        /// <summary>An arc from a to z over the ground, with an arrowhead at z (a pull toward z).</summary>
        void Arc(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, bool head)
        {
            float lift = 1.5f + Vector3.Distance(a, z) * .14f; var pts = new LinePoint[33];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 32f; Vector3 p = Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift;
                p.y = Mathf.Max(p.y, land.Ground(p.x, p.z) + .4f);
                pts[s] = new LinePoint(p, color, width, .01f * width, intensity);
            }
            b.AddFlowPath(pts, 0, .6f);
            if (head) Head(b, pts[29].Data, pts[32].Data, color, width);
        }

        /// <summary>A fear's threat: a warning mark at its source, an ice thread from it to the person and a push away
        /// from it at the person. Returns the push direction times its weight.</summary>
        Vector3 Threat(LineMeshBuilder b, Vector3 from, Vector3 place, Color color, float w, string text)
        {
            MindDiagram.Ring(b, from, .45f + w * 1.5f, color, 2, 2.6f, true); MindDiagram.Ring(b, from, .45f + w * 1.5f, color, 1.4f, 2, false);
            float x = .3f + w; b.AddSegment(from + new Vector3(-x, -x, 0), from + new Vector3(x, x, 0), color, 2, .03f, 0, 2.4f); b.AddSegment(from + new Vector3(-x, x, 0), from + new Vector3(x, -x, 0), color, 2, .03f, 0, 2.4f);
            Arc(b, from, place + Vector3.up * .8f, WithAlpha(color, .75f), 1 + 16 * w, 2 + 3 * w, false);
            Vector3 away = Flat(place - from); if (away == Vector3.zero) away = Vector3.left;
            Vector3 a = place + Vector3.up * .5f, z = a + away * (.8f + 3.5f * w);
            b.AddSegment(a, z, color, 2 + 8 * w, .02f + .05f * w, 0, 3);
            Head(b, a, z, color, 2 + 8 * w);
            Label(text, from + Vector3.up * (1.4f + w * 1.5f), .11f, Color.Lerp(color, Color.white, .3f), Part.Pulls);
            return away * w;
        }

        void Income(LineMeshBuilder b, Vector3 from, Vector3 place, Color color, double share, string text)
        {
            float w = Mathf.Sqrt(Mathf.Clamp01((float)share));
            Arc(b, from, place + Vector3.up * 1.2f, color, 1.2f + 7 * w, 2.4f + 2 * w, true);
            Label(text, from + Vector3.up * 2.2f, .11f, Color.Lerp(color, Color.white, .3f), Part.Pulls);
        }

        static void Head(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width)
        {
            Vector3 d = (z - a).normalized, side = Vector3.Cross(d, Vector3.up).normalized; if (side.sqrMagnitude < .01f) side = Vector3.right;
            float l = .35f + width * .02f;
            b.AddPolyline(new[] { z - d * l + side * l * .5f, z, z - d * l - side * l * .5f }, color, Mathf.Max(1.5f, width * .5f), .03f, 0, 3);
            b.AddPolyline(new[] { z - d * l + Vector3.up * l * .5f, z, z - d * l - Vector3.up * l * .5f }, color, Mathf.Max(1.5f, width * .5f), .03f, 0, 3);
        }

        // ------------------------------------------------------------------ the ladder

        void Ladder(PersonLife life, Data.EconomyData data)
        {
            int n = life.Records.Length; if (n == 0) return;
            var points = new List<LinePoint>(); var walk = new List<Vector3>(); var records = new List<int>();
            var ghost = new List<LinePoint>(); var b = new LineMeshBuilder();
            Vector3 Point(int k, float rank)
            {
                var r = life.Records[k]; int ind = life.IndustryAt(k);
                float z = Mathf.Min(HillLandscape.Z(life.Years[k] + .5), land.CrestZ - .6f);
                if (k == n - 1) z = land.CrestZ - 3;
                return land.Rung(life.Years[k] + .5, ind, rank, Owner(r) && rank == r.IncomeRank, z, .35f);
            }
            float parent = life.Traits?.ParentRank ?? .5f;
            Vector3[] at = new Vector3[n];
            for (int k = 0; k < n; k++) at[k] = Point(k, life.Records[k].IncomeRank);
            float cut = Mathf.Max(.05f, LandService.Model.Lives.ControlThreshold);
            for (int k = 0; k < n; k++)
            {
                var r = life.Records[k];
                float gold = r.InControl ? 1 : Mathf.Clamp01((r.Agency - .3f) / Mathf.Max(.05f, cut - .3f)) * .45f;
                Color c = Color.Lerp(Blue, Gold, gold); float width = 2.6f + Mathf.Log10(1 + Math.Max(0, r.Wealth)) * .35f;
                if (k > 0)
                {
                    // Between years the line follows the ground; a move to another hill arcs over it.
                    bool jump = life.IndustryAt(k) != life.IndustryAt(k - 1);
                    for (int s = 1; s < 6; s++)
                    {
                        float t = s / 6f; Vector3 p = Vector3.Lerp(at[k - 1], at[k], t);
                        p.y = Mathf.Max(p.y, land.Ground(p.x, p.z) + .35f) + (jump ? Mathf.Sin(t * Mathf.PI) * (2 + Vector3.Distance(at[k - 1], at[k]) * .12f) : 0);
                        points.Add(new LinePoint(p, c, width, .05f, 2.6f)); walk.Add(p); records.Add(k - 1);
                    }
                }
                points.Add(new LinePoint(at[k], c, width, .05f, 2.8f)); walk.Add(at[k]); records.Add(k);
                Vector3 g = Point(k, parent); ghost.Add(new LinePoint(g, WithAlpha(White, .28f), 1.3f, .02f, 1.2f));
            }
            b.AddFlowPath(points, 0, .5f);
            if (ghost.Count > 1) b.AddPolyline(ghost, 0);
            // Events: a mark and a short label (alternating heights so neighbors do not collide).
            int e = 0;
            foreach (var ev in life.Events)
            {
                Vector3 p = at[ev.Index]; float h = 2.2f + (e++ % 3) * 1.3f; Color c = EventColor(ev.Kind);
                b.AddSegment(p, p + Vector3.up * h, WithAlpha(c, .7f), 1.4f, .02f, 0, 2.2f);
                MindDiagram.Sphere(b, p, .32f, c, 1.6f, 3);
                Label(ev.Year + " · " + ev.Text, p + Vector3.up * (h + .45f), .13f, Color.Lerp(c, Color.white, .3f), Part.Ladder);
            }
            var now = life.Now;
            Label("<b>" + life.Year + "</b> · income rank " + PersonLife.Pct(now.IncomeRank) + " · net worth " + PersonLife.Money(now.Wealth) +
                  "\nparents' earnings rank " + PersonLife.Pct(parent) + " (white ghost)", at[n - 1] + new Vector3(0, 6.5f, 2), .15f, White, Part.Ladder);
            Label("THE LADDER · record #" + life.Person + " (" + life.Why + ")\nheight = rung of household income on the household's hill · blue labor → gold in control",
                at[0] + new Vector3(0, 8, -2), .16f, White, Part.Ladder);
            Lines(b, Part.Ladder, true, "Ladder path");
            path = walk.ToArray(); pathRecord = records.ToArray(); pathLength = new float[path.Length];
            for (int k = 1; k < path.Length; k++) pathLength[k] = pathLength[k - 1] + Vector3.Distance(path[k - 1], path[k]);
            var w = new LineMeshBuilder();
            w.AddSegment(Vector3.zero, Vector3.up * 1.4f, White, 3, .06f, 0, 4); MindDiagram.Sphere(w, Vector3.up * 1.75f, .3f, White, 2.4f, 4);
            MindDiagram.Ring(w, Vector3.up * .03f, .7f, Gold, 2.4f, 3.5f, false);
            walker = Lines(w, Part.Ladder, false, "Ladder walker"); walker.transform.localPosition = path[0];
            walkerLabel = Label("", path[0], .14f, White, Part.Ladder); shownRecord = -1;
            Vector3 lo = path[0], hi = path[0]; foreach (var p in path) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            PersonFocus.LadderCenter = (lo + hi) * .5f; PersonFocus.LadderSpan = Mathf.Max(30, (hi - lo).magnitude);
        }

        static Color EventColor(PersonLife.Kind k)
        {
            switch (k)
            {
                case PersonLife.Kind.Control: case PersonLife.Kind.Inherited: case PersonLife.Kind.Business: case PersonLife.Kind.Home: return Gold;
                case PersonLife.Kind.DebtPeak: case PersonLife.Kind.Discharged: case PersonLife.Kind.JobLost: return Ice;
                case PersonLife.Kind.Married: case PersonLife.Kind.Child: case PersonLife.Kind.MarriageEnded: return Rose;
                default: return Blue;
            }
        }

        // ------------------------------------------------------------------ anchors

        void Register(PersonLife life, Data.EconomyData data)
        {
            var now = life.Now; double ya = DeepTime.NowYear - life.Year;
            void Put(string key, string label, string blurb, Vector3 local) => Anchors.Register(new Anchor
            {
                Key = key, Label = label, Blurb = blurb, Level = GraphLevel.Humans, YearsAgo = ya, EndYearsAgo = ya, Tier = 1, Fixed = frame.World(local)
            });
            Put("atom:person", "Record #" + life.Person, "A synthetic person (" + life.Why + ") at age " + (int)now.Age + " on the hillside: household income rank " + PersonLife.Pct(now.IncomeRank) + ".", PersonFocus.Place + Vector3.up * 1.2f);
            Put("atom:mind", "The mind", "Five desire and five fear drives feed a deliberation gate (reason " + PersonLife.Pct(now.Reason) + ") and a choice; fear is " + PersonLife.Pct(now.FearShare) + " of the motive.", PersonFocus.Mind);
            Put("ladder:path", "The ladder", life.Records.Length + " adult years across the wave, " + life.Events.Count + " events.", PersonFocus.LadderCenter);
            Put("dilemma:arena", "Tit for tat", "Repeated encounters with five counterparts.", PersonFocus.Arena);
        }

        // ------------------------------------------------------------------ plumbing

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, string name)
        {
            Mesh mesh = b.ToMesh(name); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3220, true, 0, flow ? 1 : 0)); owned.Add(material);
            if (flow) { material.SetFloat("_FlowFreq", 1.6f); material.SetFloat("_FlowSpeed", .5f); }
            var r = AddMesh(name, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color, Part part)
        {
            var go = new GameObject("Person label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(40, 8);
            go.transform.localScale = Vector3.one * size; labels.Add((t, part)); return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null; figure = walker = null; walkerLabel = null; path = null;
        }

        void OnDestroy() { Clear(); PersonFocus.Life = null; PersonFocus.Ready = false; }
    }
}
