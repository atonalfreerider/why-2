using System.Collections.Generic;
using System.Diagnostics;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Builds the economy scene's model before the population is simulated: parses the economy data, creates the
    /// money circuit and the economic lives, and publishes them (<see cref="EconomyModel.SharedKey"/>). The lives are
    /// also published as the population's line style (<see cref="SmvPopulation.StyleKey"/>), so <see cref="SmvLayer"/>
    /// runs them on the finished population and draws the lifelines with them. Draws nothing itself.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class EconomyLoaderLayer : GraphLayer
    {
        /// <summary>Seed of the economic lives (the population's own seed is SmvLayer's).</summary>
        const int Seed = 2026;

        /// <summary>Before the population (Order 30), after nothing: tier 0.</summary>
        public override int Order => 25;

        public override IEnumerable<string> RequiredTexts => EconomyData.Paths;

        EconomyModel model;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            EconomyData data = EconomyData.Parse(ctx.Text);
            MoneyCircuit circuit = new MoneyCircuit(data);
            EconomicLives lives = new EconomicLives(data, Seed);
            model = new EconomyModel(data, circuit, lives);
            ctx.Share(EconomyModel.SharedKey, model);
            ctx.Share(SmvPopulation.StyleKey, lives);

            Debug.Log($"[Why] EconomyLoaderLayer.Prepare {sw.ElapsedMilliseconds} ms: {data.Industries.Count} industries in " +
                      $"{data.Tiers.Count} tiers, {data.FirstYear}-{data.LastYear}, {data.Categories.Count} spending categories");
            foreach (string w in data.Warnings) Debug.LogWarning("[Why] economy data: " + w);
        }

        public override void Upload(GraphContext ctx)
        {
            EconomyState.Reset();
            if (model == null) return;
            EconomyState.SetYearRange(model.Data.LastYear);
            if (model.Lives.Ready && model.Lives.Log.Length > 0) Debug.Log("[Why] economic lives: " + model.Lives.Log);
            else if (model.Lives.Log.Length > 0) Debug.LogWarning("[Why] economic lives not simulated: " + model.Lives.Log);
            if (model.Log.Length > 0) Debug.Log("[Why] economy model: " + model.Log);
        }
    }
}
