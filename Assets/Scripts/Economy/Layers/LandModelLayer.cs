using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The land's model (SPEC 8.3): builds the default year's snapshot on the finished population (layout, players, then
    /// the money and the season in parallel, the circuit), puts it on screen (<see cref="LandService.Init"/>) and shares it
    /// under <see cref="LandService.SharedKey"/> for the land layers' Prepare (tier 5). Prints the land's log lines (8.8)
    /// for the default year and for every other year the first time it is shown (each tagged "(demo)" while its model is a
    /// stand-in, <see cref="LandDemo"/>); the Society controls line once the four light control seasons, run after the
    /// load, are done; a year's Betrayal line once its (lazy) betrayal season exists. Order 45: tier 4, after the
    /// population (30), beside the wall (40). Draws nothing (the land layers draw the snapshot).
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class LandModelLayer : GraphLayer
    {
        public override int Order => 45;

        EconomyModel model;
        LandSnapshot first;
        readonly HashSet<int> logged = new HashSet<int>();

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            SmvPopulation pop = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            if (model == null || model.Data.Industries.Count == 0)
            {
                Debug.LogWarning("[Why] LandModelLayer: no economy model, the land is skipped");
                return;
            }

            int year = EconomyState.DefaultYear(Math.Min(EconomyState.MaxYear, Math.Max(model.Data.LastYear, EconomyState.MinYear)));
            SocialSettings settings = EconomyState.DefaultSocial();

            // what does not depend on the census, warmed beside it: the seller prior (year-independent) and the year's accounts
            EconomyModel economy = model;
            Task warm = Task.Run(() =>
            {
                SellerMatrix.PriorOf(economy.Data);
                economy.Circuit.Build(year);
            });
            Stopwatch arrange = Stopwatch.StartNew();
            LandLayout.EnsureArranged(model.Data);   // once per load: timed apart from the per-year layout (budget 2 ms)
            double arrangeMs = arrange.Elapsed.TotalMilliseconds;
            double[] ms = new double[5];
            LandService.Init(model, pop, null);   // the season reads the members' records through LandService.Model (WP4)
            first = Build(model, pop, year, settings, ms, warm);
            LandService.Init(model, pop, first);
            ctx.Share(LandService.SharedKey, first);
            foreach (string line in LandLog.Lines(model, first, null)) Debug.Log(line);
            logged.Add(year);

            // the four control seasons of 5.3 (log only) run after the load, off the critical path; their line prints when
            // they are done (Upload or Tick). The betrayal season is lazy (5.6): its line prints once something asks for it.
            LandSnapshot snapshot = first;
            controlsWatch = Stopwatch.StartNew();
            controls = Task.Run(() => Controls(economy, snapshot));
            Debug.Log("[Why] LandModelLayer.Prepare " + sw.ElapsedMilliseconds.ToString(LandFacts.Ci) + " ms: " +
                      year.ToString(LandFacts.Ci) + " (arrangement " + Ms(arrangeMs) + " once, layout " + Ms(ms[0]) + ", census " +
                      Ms(ms[1]) + ", money and season in parallel " + Ms(ms[2]) + " (money " + Ms(ms[3]) + ", season " + Ms(ms[4]) +
                      "); controls after the load, betrayal lazy)");
        }

        /// <summary>The control seasons running after the load (null once their line printed) and their clock.</summary>
        Task<SocialSeasonResult[]> controls;

        Stopwatch controlsWatch;

        static string Ms(double ms) => ms.ToString("0", LandFacts.Ci) + " ms";

        /// <summary>
        /// LandService.Build with each stage timed (ms: layout, census, money and season together, money, season): the money
        /// and the season both read only the layout and the players, so they run in parallel (each pure, the result the same
        /// as in sequence); the circuit comes from the warm-up's cache.
        /// </summary>
        static LandSnapshot Build(EconomyModel model, SmvPopulation pop, int year, SocialSettings settings, double[] ms, Task warm)
        {
            Stopwatch sw = Stopwatch.StartNew();
            LandGeometry land = LandLayout.Build(model.Data, year);
            ms[0] = Lap(sw);
            PlayerSet players = PlayerCensus.Build(model, pop, land, year);
            ms[1] = Lap(sw);
            double moneyMs = 0;
            Task<MoneyFlows> money = Task.Run(() =>
            {
                Stopwatch m = Stopwatch.StartNew();
                warm.Wait();
                MoneyFlows flows = MoneyRouting.Build(model, land, players, year);
                moneyMs = m.Elapsed.TotalMilliseconds;
                return flows;
            });
            SocialSeasonResult society = SocialSeason.Run(model.Data, land, players, settings, year);
            ms[4] = sw.Elapsed.TotalMilliseconds;
            MoneyFlows flowsBuilt = money.GetAwaiter().GetResult();
            ms[3] = moneyMs;
            ms[2] = Lap(sw);
            LandSnapshot s = new LandSnapshot
            {
                Year = year, Land = land, Players = players, Money = flowsBuilt, Society = society, Circuit = model.Circuit.Build(year)
            };
            s.ChecksLine = LandService.ChecksLine(s, model.Data);
            return s;
        }

        static double Lap(Stopwatch sw)
        {
            double t = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            return t;
        }

        /// <summary>
        /// The four control seasons of 5.3 (log only): nobody forgives, everyone forgives, no partner choice, polarization × 2;
        /// light seasons (<see cref="SocialSeason.Light"/>) on the default season's base, one after another on one worker.
        /// </summary>
        static SocialSeasonResult[] Controls(EconomyModel model, LandSnapshot s)
        {
            SocialSettings b = s.Society?.Settings ?? SocialSettings.Default;
            SocialSettings nobody = b, everyone = b, still = b, polar = b;
            nobody.Forgive = 2;
            everyone.Forgive = 1;
            still.Rewire = false;
            polar.Polarization = 2f * b.Polarization;
            SocialSettings[] all = { nobody, everyone, still, polar };
            SocialSeasonResult[] r = new SocialSeasonResult[all.Length];
            for (int k = 0; k < all.Length; k++) r[k] = SocialSeason.Light(model.Data, s.Players, all[k], s.Year);
            return r;
        }

        /// <summary>Prints the controls' line once their seasons are done (wait: block until they are; the load's end).</summary>
        void LogControls(bool wait)
        {
            if (controls == null || !wait && !controls.IsCompleted) return;
            Task<SocialSeasonResult[]> t = controls;
            controls = null;
            try
            {
                SocialSeasonResult[] r = t.GetAwaiter().GetResult();
                string line = LandLog.ControlsLine(first, r);
                if (line != null) Debug.Log(line + " (" + Ms(controlsWatch.Elapsed.TotalMilliseconds) + " after the load began)");
            }
            catch (Exception e)
            {
                Debug.LogError("[Why] LandModelLayer: the control seasons failed: " + e);
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (first == null) return;
            LandService.Changed += OnChanged;
            LogControls(false);
        }

        /// <summary>
        /// A year shown for the first time prints its lines (without the controls). A preset's year (built blocking:
        /// y1972, the harness) also computes its betrayal season here, so its Betrayal line prints with the rest; a year
        /// from the keys, the chip or a click does not (5.6: the betrayal season is lazy, and a year change keeps the main
        /// thread within 8.6's budget): its Betrayal line prints from <see cref="Tick"/> once something has asked for the
        /// season (the betrayal view, the tour, the panel).
        /// </summary>
        void OnChanged(LandSnapshot s)
        {
            if (s == null || model == null) return;
            if (logged.Add(s.Year))
            {
                if (s.Blocking && s.Betrayal == null) LandService.Betrayal(true);
                foreach (string line in LandLog.Lines(model, s, null)) Debug.Log(line);
                if (s.Betrayal != null) betrayalLogged.Add(s.Year);
            }
        }

        /// <summary>Years whose Betrayal line has been printed.</summary>
        readonly HashSet<int> betrayalLogged = new HashSet<int>();

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            LogControls(false);
            LandSnapshot shown = LandService.Current;
            if (shown?.Betrayal != null && model != null && logged.Contains(shown.Year) && betrayalLogged.Add(shown.Year))
            {
                string line = LandLog.BetrayalLine(shown);
                if (line != null) Debug.Log(line);
            }
        }

        void OnDestroy()
        {
            LandService.Changed -= OnChanged;
            LogControls(true);
        }
    }

    /// <summary>
    /// The land's log lines (SPEC 8.8) for a snapshot: Land, Roots, Players, Money, Pools, Society, Society controls (the
    /// default year only), Betrayal, Facts and Land checks. Lines whose model is a stand-in carry "(demo)" after the
    /// year. Numbers in invariant culture, at the precisions the tour quotes.
    /// </summary>
    public static class LandLog
    {
        static readonly string[] GroupShort =
            { "1%", "owners", "gig", "PMC", "public", "office", "frontline", "poor", "comf.ret", "ss.ret", "students", "out" };

        /// <summary>Every line for a snapshot (controls: the four control seasons, or null).</summary>
        public static List<string> Lines(EconomyModel model, LandSnapshot s, SocialSeasonResult[] controls)
        {
            List<string> lines = new List<string>();
            if (s?.Land == null) return lines;
            string y = s.Year.ToString(LandFacts.Ci);
            bool playersDemo = LandDemo.IsDemo(s.Players?.Log), moneyDemo = LandDemo.IsDemo(s.Money?.Log);
            bool societyDemo = LandDemo.IsDemo(s.Society?.Log);
            lines.Add("[Why] Land " + y + ": " + s.Land.Log);
            lines.Add("[Why] Roots " + y + ": " + LandLayout.RootsLine(model.Data, s.Land));
            lines.Add("[Why] Players " + y + Tag(playersDemo) + ": " + LandDemo.Body(s.Players?.Log));
            lines.Add("[Why] Money " + y + Tag(moneyDemo) + ": " + LandDemo.Body(s.Money?.Log));
            lines.Add("[Why] Pools " + y + Tag(moneyDemo) + ": " + Pools(model, s));
            lines.Add("[Why] Society " + y + Tag(societyDemo) + ": " + LandDemo.Body(s.Society?.Log));
            string control = ControlsLine(s, controls);
            if (control != null) lines.Add(control);

            string betrayal = BetrayalLine(s);
            if (betrayal != null) lines.Add(betrayal);

            lines.Add("[Why] Facts " + y + Tag(playersDemo) + ": " + Facts(model, s));
            lines.Add("[Why] Land checks " + y + Tag(playersDemo || moneyDemo || societyDemo) + ": " + s.ChecksLine);
            return lines;
        }

        /// <summary>
        /// "[Why] Society controls 2025: nobody forgives r96 0.425 (co/cross 0.521/0.370); everyone forgives 0.868; no partner
        /// choice 0.717; polarization x2 co/cross 0.770/0.685", or null without the four control seasons.
        /// </summary>
        public static string ControlsLine(LandSnapshot s, SocialSeasonResult[] controls)
        {
            if (s == null || controls == null || controls.Length < 4) return null;
            return "[Why] Society controls " + s.Year.ToString(LandFacts.Ci) + Tag(LandDemo.IsDemo(s.Society?.Log)) + ": nobody forgives r96 " +
                   R96(controls[0].Cooperation) + " (co/cross " + R96(controls[0].CoPartisan) + "/" + R96(controls[0].CrossPartisan) +
                   "); everyone forgives " + R96(controls[1].Cooperation) + "; no partner choice " + R96(controls[2].Cooperation) +
                   "; polarization x2 co/cross " + R96(controls[3].CoPartisan) + "/" + R96(controls[3].CrossPartisan);
        }

        /// <summary>"[Why] Betrayal 2025 r48: p -> q: 2 players hit, calm after 7 rounds", or null before the season is computed.</summary>
        public static string BetrayalLine(LandSnapshot s)
        {
            SocialSeasonResult b = s?.Betrayal;
            if (b?.Incident == null || s.Players?.Players == null) return null;
            Incident inc = b.Incident.Value;
            return "[Why] Betrayal " + s.Year.ToString(LandFacts.Ci) + " r" + inc.Round.ToString(LandFacts.Ci) + Tag(LandDemo.IsDemo(b.Log)) +
                   ": " + Key(s.Players, inc.From) + " -> " + Key(s.Players, inc.To) + ": " + b.Hit.ToString(LandFacts.Ci) +
                   " players hit, calm after " + b.CalmAfter.ToString(LandFacts.Ci) + " rounds";
        }

        static string Tag(bool demo) => demo ? " (demo)" : "";

        static string R96(float[] series) => series == null || series.Length == 0 ? "?" : LandFacts.Num(series[series.Length - 1], 3);

        static string Key(PlayerSet set, int p) => p >= 0 && p < set.Players.Length ? set.Players[p].Key : "?";

        /// <summary>
        /// "health $3.62T (153%, overflows), ...; dry: professional 7%, ...; construction 0": pools of at least
        /// <see cref="LandStyle.PoolLabelMinB"/> by inflow (at least the five largest), then the private sectors of at
        /// least 1% of GDP that households pay less than 40% of their value added directly (ascending), then those paid
        /// nothing.
        /// </summary>
        static string Pools(EconomyModel model, LandSnapshot s)
        {
            MoneyFlows m = s.Money;
            if (m == null) return "no money";
            int n = s.Land.Sectors.Length;
            List<int> order = new List<int>(n), dry = new List<int>(), none = new List<int>();
            for (int k = 0; k < n; k++)
            {
                SectorGeom g = s.Land.Sectors[k];
                double share = g.ValueAdded > 0 ? m.PoolInflow[k] / g.ValueAdded : 0;
                if (m.PoolInflow[k] >= LandStyle.PoolLabelMinB) order.Add(k);
                if (m.PoolInflow[k] < 0.5) none.Add(k);
                else if (g.Tier != Tier.Gov && g.ValueAdded >= 0.01 * s.Land.Gdp && share < 0.4) dry.Add(k);
            }

            order.Sort((a, b) => m.PoolInflow[a] != m.PoolInflow[b] ? m.PoolInflow[b].CompareTo(m.PoolInflow[a]) : a.CompareTo(b));
            if (order.Count < 5)
            {
                // a small economy (an early year): the five largest pools whatever their size
                order.Clear();
                for (int k = 0; k < n; k++) order.Add(k);
                order.Sort((a, b) => m.PoolInflow[a] != m.PoolInflow[b] ? m.PoolInflow[b].CompareTo(m.PoolInflow[a]) : a.CompareTo(b));
                order.RemoveRange(5, order.Count - 5);
            }

            dry.Sort((a, b) =>
            {
                double sa = m.PoolInflow[a] / s.Land.Sectors[a].ValueAdded, sb = m.PoolInflow[b] / s.Land.Sectors[b].ValueAdded;
                return sa != sb ? sa.CompareTo(sb) : a.CompareTo(b);
            });
            StringBuilder b = new StringBuilder();
            foreach (int k in order)
            {
                double share = m.PoolInflow[k] / Math.Max(1e-9, s.Land.Sectors[k].ValueAdded);
                if (b.Length > 0) b.Append(", ");
                b.Append(model.Data.Industries[k].Id).Append(' ').Append(LandFacts.Money(m.PoolInflow[k])).Append(" (")
                    .Append(LandFacts.Percent(share)).Append(share > 1 ? ", overflows)" : ")");
            }

            b.Append("; dry:");
            for (int i = 0; i < dry.Count; i++)
            {
                int k = dry[i];
                b.Append(i > 0 ? ", " : " ").Append(model.Data.Industries[k].Id).Append(' ')
                    .Append(LandFacts.Percent(m.PoolInflow[k] / s.Land.Sectors[k].ValueAdded));
            }

            if (dry.Count == 0) b.Append(" none");
            if (none.Count > 0) b.Append(';');
            for (int i = 0; i < none.Count; i++) b.Append(i > 0 ? "," : "").Append(' ').Append(model.Data.Industries[none[i]].Id).Append(" 0");
            return b.ToString();
        }

        /// <summary>
        /// The thesis numbers (6), from the lives directly: the in-control share of adults, fantasy's share of spending and
        /// per adult (in control vs the rest), reason (in control vs the rest), fear's share of spending; then the
        /// in-control share of three groups from the players.
        /// </summary>
        static string Facts(EconomyModel model, LandSnapshot s)
        {
            EconomicLives lives = model.Lives;
            if (lives == null || !lives.Ready || lives.Sim == null) return "the lives were not simulated";
            int n = lives.Sim.People.Count, adults = 0, inControl = 0;
            double spend = 0, fantasy = 0, fear = 0, fantC = 0, fantR = 0, reasonC = 0, reasonR = 0;
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, s.Year, out PersonYear r) || !r.Adult) continue;
                adults++;
                spend += r.Spending;
                fantasy += r.Spending * r.Fantasy;
                fear += r.Spending * r.FearShare;
                if (r.InControl)
                {
                    inControl++;
                    fantC += r.Fantasy;
                    reasonC += r.Reason;
                }
                else
                {
                    fantR += r.Fantasy;
                    reasonR += r.Reason;
                }
            }

            int rest = adults - inControl;
            StringBuilder b = new StringBuilder();
            b.Append("in control ").Append(LandFacts.Percent(adults > 0 ? inControl / (double)adults : 0, 1)).Append("; fantasy ")
                .Append(LandFacts.Percent(spend > 0 ? fantasy / spend : 0)).Append(" of spending (per adult: in control ")
                .Append(LandFacts.Num(inControl > 0 ? fantC / inControl : 0, 2)).Append(", the rest ")
                .Append(LandFacts.Num(rest > 0 ? fantR / rest : 0, 2)).Append("); reason in control ")
                .Append(LandFacts.Num(inControl > 0 ? reasonC / inControl : 0, 2)).Append(", the rest ")
                .Append(LandFacts.Num(rest > 0 ? reasonR / rest : 0, 2)).Append("; fear ").Append(LandFacts.Percent(spend > 0 ? fear / spend : 0));

            double[] ctrl = new double[12], count = new double[12];
            if (s.Players?.Players != null)
            {
                foreach (Player p in s.Players.Players)
                {
                    ctrl[(int)p.Group] += p.InControl * p.Adults.Length;
                    count[(int)p.Group] += p.Adults.Length;
                }
            }

            string Share(Land.Group g) => LandFacts.Percent(count[(int)g] > 0 ? ctrl[(int)g] / count[(int)g] : 0);
            b.Append("; the 1% ").Append(Share(Land.Group.Top1)).Append(" in control, business owners ").Append(Share(Land.Group.Owners))
                .Append(", frontline ").Append(Share(Land.Group.Frontline));
            return b.ToString();
        }
    }
}
