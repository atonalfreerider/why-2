using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Tit for tat (view "dilemma"): a 3D arena floating beside the followed person (<see cref="PersonFocus.Arena"/>)
    /// where they play a real iterated prisoner's dilemma (<see cref="DilemmaArena"/>) with five counterparts drawn from
    /// the same year's population (spouse, coworker, neighbor, boss or firm, someone of the other tribe), each with the
    /// strategy the lives model drew for them, one round every 0.6 s. Each round sends two pulses along each pair's link,
    /// blue for cooperate and red for defect; above each counterpart a strip of the last rounds shows the person's moves
    /// (upper row) and the partner's (lower row), with a gold diagonal wherever the partner repeated the person's previous
    /// move: tit for tat's signature. Gold bars are the running payoffs. In round 9 the person defects once against the
    /// coworker; what follows (retaliation, an echo of feuding, forgiveness, revenge without end) is the partner's
    /// strategy. A ledger turns the cooperation dividend over mutual defection into an illustrative sum on screen
    /// (historical records unchanged). Thin threads tie each counterpart to where their cohort stands on the hills.
    /// Above the arena the group level: the season of plain tit for tat between all cohorts (<see cref="SocialSeason"/>,
    /// mean field over shares), as a ring for the person's own tribe and one for the other, the bridge between them
    /// colored by cooperation across tribes, stepping through the season's rounds with the arena.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillDilemmaLayer : GraphLayer
    {
        public override int Order => 60;
        const float RoundSeconds = .6f, Radius = 4.4f, Strip = 12;
        static readonly Color Coop = new Color(.30f, .70f, 1), Defect = new Color(1, .22f, .30f), Gold = new Color(1, .70f, .20f),
            White = new Color(1, .96f, .9f), Steel = new Color(.45f, .55f, .68f), Rose = new Color(1, .30f, .52f), Ice = new Color(.5f, .84f, 1);
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<TextMeshPro> seatLabels = new List<TextMeshPro>();
        LandSnapshot snapshot; GameObject content; DilemmaArena arena; PersonLife life;
        MeshRenderer dynamicLines, pulses;
        TextMeshPro roundLabel, ledgerLabel, slipLabel, groupLabel;
        Vector3 center; Vector3[] seats;
        int version = -1; float clock; bool wasShown;

        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (LandService.Current == null) return;
            bool shown = LandView.PresetId == "dilemma";
            if (version != PersonFocus.Version || snapshot != LandService.Current) Build();
            if (content == null) return;
            content.SetActive(shown);
            if (!shown) { wasShown = false; return; }
            // Entering the view starts a fresh match, so the slip always comes a few seconds in.
            if (!wasShown) { wasShown = true; Build(); if (content == null) return; content.SetActive(true); }
            clock += Time.deltaTime;
            if (clock >= RoundSeconds) { clock = 0; arena.Step(); Dynamic(); }
            Pulses(clock / RoundSeconds);
            foreach (var l in labels) if (l) l.transform.rotation = rig.Cam.transform.rotation;
        }

        void Build()
        {
            Clear();
            version = PersonFocus.Version; snapshot = LandService.Current; life = PersonFocus.Life;
            var land = HillLandscapeLayer.Current;
            if (life == null || land == null || LandService.Model?.Lives == null) return;
            arena = DilemmaArena.Build(LandService.Model.Lives, snapshot.Players, LandService.Model.Data, life);
            if (arena == null) return;
            content = new GameObject("Tit for tat arena"); content.transform.SetParent(transform, false);
            center = PersonFocus.Arena; seats = new Vector3[arena.Seats.Length];
            for (int k = 0; k < seats.Length; k++)
            {
                float a = Mathf.PI * (.5f + 2f * k / seats.Length);
                seats[k] = center + new Vector3(Mathf.Cos(a) * Radius, 0, Mathf.Sin(a) * Radius);
            }
            var b = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder();
            // The floor: a faint disc with rings, so the arena reads as a place from any side.
            for (int r = 1; r <= 3; r++) MindDiagram.Ring(b, center, Radius * (.45f + .3f * r), WithAlpha(Steel, .45f), 1, 1.2f, false);
            var inner = new Vector3[49]; var outer = new Vector3[49];
            for (int j = 0; j <= 48; j++) { float a = j * Mathf.PI / 24; inner[j] = center; outer[j] = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Radius * 1.4f; }
            fill.AddBand(inner, outer, new[] { (Color32)WithAlpha(Steel, .16f) }, new[] { (Color32)WithAlpha(Steel, .02f) }, 0, 1);
            Figure(b, center, life.Now.InControl ? Gold : White, 1.25f);
            var playerOf = snapshot.Players.PlayerOfPerson;
            for (int k = 0; k < seats.Length; k++)
            {
                var s = arena.Seats[k];
                Figure(b, seats[k], White, 1);
                b.AddSegment(center + Vector3.up, seats[k] + Vector3.up, WithAlpha(Steel, .6f), 1.2f, .02f, 0, 1.2f);
                // Where this counterpart's cohort stands on the hills: the dealings come from somewhere real.
                int player = playerOf != null && s.Person >= 0 && s.Person < playerOf.Length ? playerOf[s.Person] : -1;
                if (player >= 0 && player < land.People.Length) MindDiagram.Thread(b, seats[k], land.People[player] + Vector3.up * .5f, WithAlpha(Steel, .35f), .8f, 1, 2);
                seatLabels.Add(Label("<b>" + s.Role.ToUpperInvariant() + "</b> · " + PrisonersDilemma.Name(s.Strategy) + "\n" + s.Note + " · record #" + s.Person, seats[k] + Vector3.up * 2.35f, .1f, White));
                Label(Rule(s.Strategy), seats[k] + Vector3.up * 4.25f, .075f, Steel * 1.5f);
            }
            Label("<b>YOU · record #" + life.Person + "</b> · " + PrisonersDilemma.Name(arena.Strategy) + "\n" + PrisonersDilemma.Rule(arena.Strategy), center + Vector3.up * 2.9f, .1f, Gold);
            Label("cooperate (blue) pays both R = " + arena.Pay.R.ToString("0", LandFacts.Ci) + " · defect (red) against cooperation pays T = " + arena.Pay.T.ToString("0", LandFacts.Ci) +
                  " and the sucker S = " + arena.Pay.S.ToString("0", LandFacts.Ci) + " · both defect P = " + arena.Pay.P.ToString("0", LandFacts.Ci) +
                  "\nupper row: your moves · lower row: theirs · gold diagonal: they copied your last move (tit for tat)", center + new Vector3(0, -.6f, 0) + Vector3.back * (Radius + 1.6f), .085f, Steel * 1.6f);
            roundLabel = Label("", center + Vector3.up * 5.4f, .12f, White);
            ledgerLabel = Label("", center + Vector3.down * 1.4f, .095f, Gold);
            slipLabel = Label("", center + Vector3.up * 6.1f, .11f, Defect);
            groupLabel = Label("", center + Vector3.up * 11.6f, .11f, White);
            Lines(b, "Arena", false);
            Surface(fill, "Arena floor");
            dynamicLines = Lines(new LineMeshBuilder(), "Arena moves", true);
            pulses = Lines(new LineMeshBuilder(), "Arena pulses", false);
            clock = 0; Dynamic();
            var groups = Groups(0);
            Anchors.Register(new Anchor { Key = "dilemma:groups", Label = "Groups", Blurb = "Cooperation within and across tribes, the season's mean field.", Level = GraphLevel.Humans,
                Tier = 1, YearsAgo = DeepTime.NowYear - snapshot.Year, EndYearsAgo = DeepTime.NowYear - snapshot.Year, Fixed = EconomyStage.Land().World(groups) });
        }

        static string Rule(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "copies your last move";
                case PdStrategy.GenerousTitForTat: return "copies, but forgives one defection in three";
                case PdStrategy.WinStayLoseShift: return "repeats what paid, switches after a loss";
                case PdStrategy.AlwaysCooperate: return "always cooperates";
                case PdStrategy.AlwaysDefect: return "always defects";
                case PdStrategy.Grim: return "cooperates until betrayed, then never again";
                default: return "flips a coin";
            }
        }

        /// <summary>Everything that changes each round: the move strips with tit for tat's copies, the payoff bars, the
        /// readouts, the group level.</summary>
        void Dynamic()
        {
            var b = new LineMeshBuilder(); int round = arena.Round;
            float maxPair = arena.Pay.R * DilemmaArena.MatchRounds;
            for (int k = 0; k < seats.Length; k++)
            {
                var s = arena.Seats[k]; Vector3 at = seats[k];
                Vector3 tangent = Vector3.Cross(Vector3.up, (at - center).normalized).normalized;
                int n = s.MyMoves.Count, from = Mathf.Max(0, n - (int)Strip);
                Vector3 origin = at + Vector3.up * 3.15f - tangent * (Strip * .11f);
                for (int t = from; t < n; t++)
                {
                    Vector3 p = origin + tangent * ((t - from) * .22f);
                    bool slip = t == DilemmaArena.SlipRound && k == arena.SlipSeat;
                    b.AddSegment(p + Vector3.up * .32f, p + Vector3.up * .5f, Move(s.MyMoves[t]), slip ? 5 : 3.2f, .05f, 0, slip ? 5 : 2.6f);
                    b.AddSegment(p - Vector3.up * .09f, p + Vector3.up * .09f, Move(s.TheirMoves[t]), 3.2f, .05f, 0, 2.6f);
                    if (DilemmaArena.Copied(s, t) && t > from)
                        b.AddSegment(p - tangent * .22f + Vector3.up * .32f, p + Vector3.up * .09f, WithAlpha(Gold, .8f), 1.2f, .01f, 0, 2.4f);
                }
                // Payoff bars: the partner's beside them, scaled to a whole match of mutual cooperation.
                Bar(b, at + tangent * .9f, s.TheirScore / maxPair * 3, Gold);
            }
            float mine = arena.Score;
            Bar(b, center + Vector3.right * .9f, mine / (maxPair * seats.Length) * 4, Gold);
            Bar(b, center + Vector3.left * .9f, arena.Pay.P * round / maxPair * 4, WithAlpha(Steel, .7f));
            Groups(arena.Total, b);
            Replace(dynamicLines, b);
            var data = life.Now; double perPoint = DilemmaArena.PointValue * Math.Max(1000, data.Disposable);
            roundLabel.text = "<b>ROUND " + round + " / " + DilemmaArena.MatchRounds + "</b> · match " + (arena.Matches + 1) + " · one round = one stretch of dealings";
            ledgerLabel.text = "your payoff this match " + mine.ToString("0", LandFacts.Ci) + " (gold) vs mutual defection (grey)\ncooperation dividend so far +" +
                arena.Dividend.ToString("0", LandFacts.Ci) + " points ≈ " + PersonLife.Money(arena.Dividend * perPoint) + " added to the buffer (illustrative: 1 point = 0.02% of income)";
            var slipSeat = arena.Seats[arena.SlipSeat];
            if (round > DilemmaArena.SlipRound && round <= DilemmaArena.SlipRound + 12)
            {
                bool retaliated = slipSeat.TheirMoves.Count > DilemmaArena.SlipRound + 1 && slipSeat.TheirMoves[DilemmaArena.SlipRound + 1] == PdMove.Defect;
                int feud = 0; for (int t = DilemmaArena.SlipRound + 1; t < slipSeat.TheirMoves.Count; t++) if (slipSeat.TheirMoves[t] == PdMove.Defect || slipSeat.MyMoves[t] == PdMove.Defect) feud++;
                slipLabel.text = "ROUND " + (DilemmaArena.SlipRound + 1) + ": YOU DEFECTED ONCE AGAINST THE " + slipSeat.Role.ToUpperInvariant() + "\n" +
                    (retaliated ? PrisonersDilemma.Name(slipSeat.Strategy) + " retaliated next round" : PrisonersDilemma.Name(slipSeat.Strategy) + " did not retaliate") +
                    (feud > 2 ? " · the feud echoes (" + feud + " rounds with a defection)" : retaliated ? " · then cooperation returns" : "");
                slipLabel.color = retaliated ? Defect : Coop;
            }
            else slipLabel.text = round <= DilemmaArena.SlipRound ? "round " + (DilemmaArena.SlipRound + 1) + ": watch the " + slipSeat.Role.ToLowerInvariant() + " · you will defect once" : "";
            for (int k = 0; k < seatLabels.Count; k++)
            {
                var s = arena.Seats[k];
                seatLabels[k].text = "<b>" + s.Role.ToUpperInvariant() + "</b> · " + PrisonersDilemma.Name(s.Strategy) + " · " + s.TheirScore.ToString("0", LandFacts.Ci) + " pts\n" + s.Note + " · record #" + s.Person;
            }
        }

        /// <summary>The group level: two rings (the person's tribe, the other) with each tribe's cooperation among its own as
        /// a gold arc, and a bridge colored by cooperation across tribes, at the season's round for this arena round.
        /// Returns the rings' center.</summary>
        Vector3 Groups(int total, LineMeshBuilder b = null)
        {
            Vector3 g = center + Vector3.up * 9.2f;
            var season = snapshot.Society;
            if (b == null || season?.CoPartisan == null || season.CrossPartisan == null) return g;
            int round = Mathf.Clamp(total % (season.Rounds + 1), 0, Math.Min(season.CoPartisan.Length, season.CrossPartisan.Length) - 1);
            float co = season.CoPartisan[round], cross = season.CrossPartisan[round];
            float same = season.SameGroup != null && round < season.SameGroup.Length ? season.SameGroup[round] : co;
            float other = season.OtherGroup != null && round < season.OtherGroup.Length ? season.OtherGroup[round] : cross;
            byte tribe = life.Now.Tribe; Color mine = tribe == 1 ? Rose : Ice, theirs = tribe == 1 ? Ice : Rose;
            Vector3 left = g + Vector3.left * 3.2f, right = g + Vector3.right * 3.2f;
            for (int side = 0; side < 2; side++)
            {
                Vector3 c = side == 0 ? left : right; Color col = side == 0 ? mine : theirs;
                MindDiagram.Ring(b, c, 1.8f, WithAlpha(col, .8f), 2, 2.4f, false); MindDiagram.Ring(b, c, 1.8f, WithAlpha(col, .5f), 1.2f, 1.6f, true);
                Arc(b, c + Vector3.up * .1f, 2.1f, co, Gold);
                for (int m = 0; m < 9; m++) { float a = m * Mathf.PI * 2 / 9; Vector3 p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.3f; b.AddSegment(p, p + Vector3.up * .45f, col, 1.6f, .02f, 0, 2); }
            }
            Color bridge = Color.Lerp(Defect, Coop, cross);
            for (int lane = -1; lane <= 1; lane++) b.AddSegment(left + Vector3.right * 1.8f + Vector3.forward * lane * .25f, right + Vector3.left * 1.8f + Vector3.forward * lane * .25f, WithAlpha(bridge, .4f + .5f * cross), 1 + 5 * cross, .03f, 0, 2.6f);
            b.AddSegment(center + Vector3.up * 3.6f, g - Vector3.up * .3f, WithAlpha(Steel, .4f), 1, .01f, 0, 1);
            groupLabel.text = "<b>THE GROUPS</b> · " + snapshot.Players.Players.Length + " cohorts play tit for tat as shares (season round " + round + " / " + season.Rounds + ")" +
                "\nwithin a tribe " + PersonLife.Pct(co) + " cooperate (gold arcs) · across tribes " + PersonLife.Pct(cross) + " (bridge) · same class " + PersonLife.Pct(same) + " · other class " + PersonLife.Pct(other) +
                "\nleft: your tribe (" + (tribe == 0 ? "Democrat" : tribe == 1 ? "Republican" : "independent") + ")";
            return g;
        }

        void Pulses(float t)
        {
            if (arena == null || pulses == null) return;
            var b = new LineMeshBuilder(); t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.2f));
            for (int k = 0; k < seats.Length; k++)
            {
                var s = arena.Seats[k]; if (s.MyMoves.Count == 0) continue;
                PdMove mine = s.MyMoves[s.MyMoves.Count - 1], theirs = s.TheirMoves[s.TheirMoves.Count - 1];
                Vector3 a = center + Vector3.up * 1.15f, z = seats[k] + Vector3.up * 1.15f, a2 = center + Vector3.up * .85f, z2 = seats[k] + Vector3.up * .85f;
                Pulse(b, Vector3.Lerp(a, z, t), (z - a).normalized, Move(mine), mine == PdMove.Defect && s.MyMoves.Count - 1 == DilemmaArena.SlipRound && k == arena.SlipSeat);
                Pulse(b, Vector3.Lerp(z2, a2, t), (a2 - z2).normalized, Move(theirs), false);
            }
            Replace(pulses, b);
        }

        static void Pulse(LineMeshBuilder b, Vector3 at, Vector3 dir, Color c, bool big)
        {
            float l = big ? .9f : .6f;
            b.AddSegment(at - dir * l, at, c, big ? 7 : 5, big ? .12f : .08f, 0, big ? 7 : 5);
            MindDiagram.Ring(b, at, big ? .2f : .13f, c, 3, 5, true); MindDiagram.Ring(b, at, big ? .2f : .13f, c, 3, 5, false);
        }

        static Color Move(PdMove m) => m == PdMove.Cooperate ? Coop : Defect;

        static void Figure(LineMeshBuilder b, Vector3 at, Color c, float scale)
        {
            b.AddSegment(at, at + Vector3.up * 1.2f * scale, c, 2.6f, .04f, 0, 2.6f);
            MindDiagram.Sphere(b, at + Vector3.up * 1.5f * scale, .24f * scale, c, 2, 2.8f);
            MindDiagram.Ring(b, at, .5f * scale, c, 1.8f, 2.2f, false);
        }

        static void Bar(LineMeshBuilder b, Vector3 at, float height, Color c)
        {
            height = Mathf.Max(.02f, height);
            b.AddSegment(at, at + Vector3.up * height, c, 6, .1f, 0, 3);
            for (float y = .5f; y < height; y += .5f) b.AddSegment(at + new Vector3(-.12f, y, 0), at + new Vector3(.12f, y, 0), WithAlpha(Color.black, .6f), 1, 0, 0, 1);
        }

        static void Arc(LineMeshBuilder b, Vector3 c, float r, float share, Color color)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(48 * Mathf.Clamp01(share))); var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) { float a = Mathf.PI * .5f + Mathf.PI * 2 * Mathf.Clamp01(share) * i / n; pts[i] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, 4, .06f, 0, 3.5f);
        }

        // ------------------------------------------------------------------ plumbing

        MeshRenderer Lines(LineMeshBuilder b, string name, bool flow)
        {
            Mesh mesh = b.ToMesh(name); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3222, true, 0, flow ? 1 : 0)); owned.Add(material);
            var r = AddMesh(name, mesh, material); r.transform.SetParent(content.transform, false); return r;
        }

        void Surface(SurfaceMeshBuilder b, string name)
        {
            Mesh mesh = b.ToMesh(name); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3218)); owned.Add(material);
            var r = AddMesh(name, mesh, material); r.transform.SetParent(content.transform, false);
        }

        void Replace(MeshRenderer r, LineMeshBuilder b)
        {
            var filter = r.GetComponent<MeshFilter>(); Mesh old = filter.sharedMesh; owned.Remove(old); if (old) Destroy(old);
            filter.sharedMesh = b.ToMesh(r.name); owned.Add(filter.sharedMesh);
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject("Arena label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(40, 8);
            go.transform.localScale = Vector3.one * size; labels.Add(t); return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); labels.Clear(); seatLabels.Clear(); content = null; arena = null; dynamicLines = pulses = null;
        }

        void OnDestroy() => Clear();
    }
}
