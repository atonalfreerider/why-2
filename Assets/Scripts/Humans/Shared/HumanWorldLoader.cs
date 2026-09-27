using System.Collections.Generic;

namespace Why.Humans
{
    /// <summary>
    /// Builds the shared <see cref="HumanWorld"/> before the human layers (Order 30+) prepare, and
    /// publishes it as <see cref="HumanWorld.SharedKey"/>.
    /// </summary>
    public sealed class HumanWorldLoader : GraphLayer
    {
        public override int Order => 20;

        public override IEnumerable<string> RequiredTexts => new[]
        {
            HumanWorld.PowerPath, HumanWorld.CivPath, HumanWorld.DemographyPath
        };

        public override void Prepare(GraphContext ctx)
        {
            HumanWorld world = HumanWorld.Load(ctx.Text(HumanWorld.PowerPath), ctx.Text(HumanWorld.CivPath),
                ctx.Text(HumanWorld.DemographyPath), ctx.NowYear);
            ctx.Share(HumanWorld.SharedKey, world);
        }

        public override void Upload(GraphContext ctx) { }
    }
}
