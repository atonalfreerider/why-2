using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Why.Humans.Social;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Model
{
    /// <summary>
    /// The lives view's model at load (M3; WP12): builds <see cref="LivesModel"/> for the economy's opening year on the
    /// finished lives with the approved real figures (<see cref="RealLives"/>, visibility.json checked against people.json),
    /// shares it (<see cref="LivesModel.SharedKey"/>, <see cref="LivesModel.Current"/>) for the lives layers, and reads the
    /// futures of the default viewer (the You panel's household as it stands at load, placed by <see cref="YouModel"/>).
    /// Upload prints its lines ("[Why] Lives 2005-2025: ..."), logs every problem as an error and publishes the facts
    /// (lives.*): on the main thread, where the smoke test counts errors (Unity passes a worker's log messages only to
    /// threaded listeners). Order 46: tier 4, after the population (30) that runs the lives, beside the land's model (45);
    /// nothing here waits on the land. Draws nothing.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class LivesModelLayer : GraphLayer
    {
        public override int Order => 46;

        public override IEnumerable<string> RequiredTexts => new[] { RealLives.Path, SocialPeople.Path };

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        LivesModel model;
        string viewerLine;
        readonly List<string> viewerProblems = new List<string>();
        long prepareMs;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            LivesModel.Show(null);
            EconomyModel economy = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            if (economy == null)
            {
                Debug.LogWarning("[Why] LivesModelLayer: no economy model, the lives are skipped");
                return;
            }

            // the land's opening year (LandModelLayer reads it the same way, on a worker before EconomyState.Reset)
            int year = EconomyState.DefaultYear(Math.Min(EconomyState.MaxYear, Math.Max(economy.Data.LastYear, EconomyState.MinYear)));
            // visibility.json and people.json do not depend on the lives: parsed on a second worker beside the records (each
            // takes about as long), so the tier waits on the longer of the two rather than their sum
            string visibility = ctx.Text(RealLives.Path), people = ctx.Text(SocialPeople.Path);
            Task<RealLives> real = Task.Run(() => RealLives.Parse(visibility, SocialPeople.Parse(people)));
            model = LivesModel.BuildAtLoad(economy.Lives, economy.Data, year, real);
            Viewer(economy);
            ctx.Share(LivesModel.SharedKey, model);
            LivesModel.Show(model);
            prepareMs = sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// The default viewer's futures (7.4): the You panel's household as it stands at load (YouState's defaults until the
        /// viewer changes them), placed among the year's adults by YouModel; its fifth and age band's odds from the lives
        /// model, which must be the You panel's own (the same lines), with n and 90% ranges.
        /// </summary>
        void Viewer(EconomyModel economy)
        {
            if (model.Decade == null) return;
            YouModel.Inputs you = YouState.Inputs;
            YouModel.Result reading = YouModel.Build(economy.Lives, model.Year)?.Read(you, economy.Data, null, model.Decade);
            LivesModel.Odds odds = model.ForViewer(reading);
            if (odds == null || odds.N == 0)
            {
                viewerProblems.Add("the default viewer has no futures: no simulated adult like them over the decade");
                return;
            }

            int climb = Pct(odds.Climb.Value), stay = Pct(odds.Stay.Value), fall = Pct(odds.Fall.Value);
            viewerLine = "[Why] Lives you " + model.Year.ToString(Ci) + ": default viewer aged " + you.Age.ToString(Ci) +
                         ", household income $" + you.Income.ToString("#,0", Ci) + " (YouState" + (YouState.Entered ? ", as entered" : "") +
                         "): income rank " + Pct(reading.IncomeRank).ToString(Ci) + ", " + Mobility.QuintileNames[odds.Fifth] + ", aged " +
                         Mobility.AgeBandNames[odds.Band] + (odds.AllAges ? " (too few at that age: the fifth at every age)" : "") +
                         "; " + odds.From.ToString(Ci) + "-" + odds.Year.ToString(Ci) + ": climb " + odds.Climb + ", stay " + odds.Stay +
                         ", fall " + odds.Fall + "; sum " + (climb + stay + fall).ToString(Ci) + "%, n = " + odds.N.ToString(Ci) + " records";

            // the futures and the You panel read the same lines
            if (Math.Abs(odds.Climb.Value - reading.Up) > 1e-12 || Math.Abs(odds.Stay.Value - reading.Stay) > 1e-12 ||
                Math.Abs(odds.Fall.Value - reading.Down) > 1e-12 || odds.N != reading.OddsLines)
            {
                viewerProblems.Add("the default viewer's futures differ from the You panel's odds (" + reading.Up.ToString("0.###", Ci) + "/" +
                                   reading.Stay.ToString("0.###", Ci) + "/" + reading.Down.ToString("0.###", Ci) + " of " +
                                   reading.OddsLines.ToString(Ci) + ")");
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (model == null) return;
            foreach (string line in model.LogLines()) Debug.Log(line);
            if (viewerLine != null) Debug.Log(viewerLine);
            List<string> problems = model.Problems();
            problems.AddRange(viewerProblems);
            foreach (string problem in problems) Debug.LogError("[Why] Lives check failed: " + problem);
            model.PublishFacts();
            Debug.Log("[Why] LivesModelLayer.Prepare " + prepareMs.ToString(Ci) + " ms: " + model.Years.ToString(Ci) + " years, " +
                      model.StartRecords.ToString(Ci) + " records in the start group, the decade's futures, " +
                      (model.Real?.Picks.Count ?? 0).ToString(Ci) + " real figures; checks " +
                      (problems.Count == 0 ? "PASS" : problems.Count.ToString(Ci) + " FAILED"));
        }

        static int Pct(double share) => (int)Math.Round(100 * share, MidpointRounding.AwayFromZero);
    }
}
