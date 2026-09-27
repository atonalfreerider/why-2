using System;
using TMPro;
using UnityEngine;

namespace Why.Humans.Bands
{
    /// <summary>
    /// Anchors and labels of the civilization streams (worker thread): "civ:&lt;id&gt;" for every stream,
    /// "civ:humanity" for prehistoric Homo sapiens, "civ:_lineage" for our path along the inner edge, and
    /// "war:&lt;id&gt;" for every war. Label priorities are the level of detail: the most powerful streams
    /// are named first, small streams and wars appear as the view makes room.
    /// </summary>
    public static class BandAnnotations
    {
        // streams: priority = base + scale * largest power share; text size grows with importance
        const float StreamPriorityBase = 8f;
        const float StreamPriorityScale = 60f;
        const float HumanityPriority = 45f;
        const float StreamSizeMin = 12f;
        const float StreamSizeRange = 4f;
        const double Tier1Share = 0.12;

        // wars: priority around 6, a little higher for deadlier wars
        const float WarPriorityBase = 4f;
        const float WarPriorityPerDecade = 0.4f;
        const float WarLabelSize = 11f;

        public static void Register(GraphContext ctx, HumanWorld world, BandGeometry geometry)
        {
            foreach (StreamInfo s in geometry.Streams) RegisterStream(ctx, world, s);
            RegisterLineage(world);
            foreach (WarInfo w in geometry.Wars) RegisterWar(ctx, world, w);
        }

        static void RegisterStream(GraphContext ctx, HumanWorld world, StreamInfo s)
        {
            Civ c = s.Civ;
            bool humanity = c == world.Humanity;
            string blurb = c.Blurb ?? c.Region;
            if (!string.IsNullOrEmpty(c.Source)) blurb = string.IsNullOrEmpty(blurb) ? c.Source : blurb + " (" + c.Source + ")";

            Anchor anchor = new Anchor
            {
                Key = "civ:" + c.Id,
                Label = c.Name,
                Blurb = blurb,
                Level = GraphLevel.Humans,
                YearsAgo = world.NowYear - s.Start,
                EndYearsAgo = c.Extant ? 0 : Math.Max(0, world.NowYear - s.End),
                Y = GraphStyle.HumansY,
                Rho = s.AnchorRho,
                Ids = new IdRange(GraphIds.Civ(c.Index), GraphIds.CivEnd(c.Index)),
                Tier = humanity || s.MaxShare > Tier1Share ? 1 : 2
            };
            Anchors.Register(anchor);

            float importance = Mathf.Clamp01((float)s.MaxShare / 0.5f);
            ctx.Labels.Add(new LabelSpec
            {
                Text = c.Name,
                Data = new Vector3(s.LabelU, GraphStyle.HumansY, s.LabelRho),
                Priority = humanity ? HumanityPriority : StreamPriorityBase + StreamPriorityScale * (float)s.MaxShare,
                SizePx = humanity ? StreamSizeMin + StreamSizeRange : StreamSizeMin + StreamSizeRange * importance,
                Color = GraphStyle.Text,
                Align = TextAlignmentOptions.Center,
                AnchorKey = anchor.Key,
                Ids = anchor.Ids
            });
        }

        static void RegisterLineage(HumanWorld world)
        {
            Anchors.Register(new Anchor
            {
                Key = CivBandsLayer.LineageAnchor,
                Label = "Our path",
                Blurb = "The inner edge of the human layer: Homo sapiens rising out of the tree of life, then the " +
                        "westernmost stream of each age in the Histomap, down to the present. Inside is most " +
                        "relevant to us; everything further out split off along the way.",
                Level = GraphLevel.Humans,
                YearsAgo = HumanWorld.SapiensYearsAgo,
                EndYearsAgo = 0,
                Y = GraphStyle.HumansY,
                Rho = 0,
                Ids = IdRange.Single(CivBandsLayer.LineageId),
                Tier = 1
            });
        }

        static void RegisterWar(GraphContext ctx, HumanWorld world, WarInfo w)
        {
            War war = w.War;
            Anchor anchor = new Anchor
            {
                Key = "war:" + war.id,
                Label = war.name ?? war.id,
                Blurb = war.blurb,
                Level = GraphLevel.Humans,
                YearsAgo = world.NowYear - war.startYear,
                EndYearsAgo = Math.Max(0, world.NowYear - Math.Max(war.endYear, war.startYear)),
                Y = GraphStyle.HumansY + BandGeometry.WarY,
                Rho = w.Rho,
                Ids = IdRange.Single(w.Id),
                Tier = 2
            };
            Anchors.Register(anchor);

            float decades = (float)Math.Log10(Math.Max(war.deaths, 1e4));
            ctx.Labels.Add(new LabelSpec
            {
                Text = anchor.Label,
                Data = anchor.Data,
                Priority = WarPriorityBase + WarPriorityPerDecade * decades,
                SizePx = WarLabelSize,
                Color = GraphStyle.TextDim,
                Align = TextAlignmentOptions.Center,
                PixelOffset = new Vector2(0, 10),
                AnchorKey = anchor.Key,
                Ids = anchor.Ids
            });
        }
    }
}
