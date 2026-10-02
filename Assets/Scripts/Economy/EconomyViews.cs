using System;
using System.Collections.Generic;
using Why.Economy.Land;

namespace Why.Economy
{
    /// <summary>
    /// What a view preset asks of the land, beside its camera pose (<see cref="EconomyPresets"/>): the transition's
    /// target (0 = folded into the cut, 2.4 = the bowl), a temporary year, the season's held round and whether it
    /// shows the betrayal season, whether the road's labels step aside, and the emphasis of every land group (one row
    /// of the table in <see cref="EconomyViews"/>) with the label groups it shows. Applied by the land's view layer on
    /// focus.
    /// </summary>
    public sealed class ViewSpec                          // EconomyViews.Get(presetId)
    {
        /// <summary>The preset id, the morph target (s) and the temporary year.</summary>
        public string Id; public float MorphTarget = 2.4f; public int Year;   // Year 0: no override

        /// <summary>The season's round shown on focus (-1: leave playback alone), the betrayal season, the road's labels.</summary>
        public int HoldRound = -1; public bool Betrayal, HideRoadLabels = true;

        /// <summary>Target emphasis (0..1) of every land group.</summary>
        public readonly float[] Alpha = new float[(int)LandGroup.Count];

        /// <summary>The label groups the preset shows.</summary>
        public LandGroup[] Labels = Array.Empty<LandGroup>();                  // label groups shown (7.2)

        /// <summary>Whether the preset lists a label group.</summary>
        public bool ShowsLabels(LandGroup g) => Array.IndexOf(Labels, g) >= 0;
    }

    /// <summary>
    /// The economy views' side table (SPEC 7.2): one <see cref="ViewSpec"/> per preset of <see cref="EconomyPresets"/>,
    /// with the emphasis of each land group per preset (the land's view layer eases each group toward it at
    /// <see cref="LandStyle.EmphasisRate"/> per second; land groups are also multiplied by <see cref="LandView.Reveal"/>)
    /// and the label groups each preset shows.
    /// </summary>
    public static class EconomyViews
    {
        /// <summary>The preset ids, keyed (1-8) then keyless.</summary>
        public static readonly string[] Ids =
            { "overview", "section", "landscape", "capture", "people", "rivers", "mind", "society", "roots", "betrayal", "y1972" };

        /// <summary>The year the keyless y1972 preset opens temporarily.</summary>
        public const int Year1972 = 1972;

        /// <summary>Columns of <see cref="Table"/>.</summary>
        static readonly string[] Columns =
            { "overview", "section", "landscape", "roots", "capture", "people", "rivers", "mind", "society", "betrayal", "y1972" };

        /// <summary>The emphasis table: one row per LandGroup (in enum order), one column per preset of <see cref="Columns"/>.</summary>
        static readonly float[][] Table =
        {
            //            overview section landscape roots capture people rivers mind society betrayal y1972
            new[] { 1f, 1f, .35f, .35f, .35f, .35f, .35f, .35f, .35f, .35f, .35f },   // Road
            new[] { 1f, 1f, .6f, .6f, .6f, .6f, .6f, .6f, .6f, .6f, .6f },            // Cut
            new[] { .8f, 0f, 1f, .6f, .6f, .6f, .6f, .35f, .35f, .35f, 1f },          // Terraces
            new[] { .8f, 0f, 1f, .6f, .8f, .5f, .6f, .3f, .3f, .3f, 1f },             // Sectors
            new[] { .6f, 0f, .6f, .2f, .6f, .3f, 1f, .6f, .2f, .2f, .6f },            // Pools
            new[] { .3f, 0f, .6f, 1f, .2f, .1f, .1f, .1f, .05f, .05f, .6f },          // Roots
            new[] { .8f, 0f, .6f, .3f, 1f, .4f, .3f, .3f, .3f, .3f, .6f },            // Towers
            new[] { .8f, 0f, .5f, .2f, 1f, .6f, .4f, .4f, .3f, .3f, .5f },            // Crown
            new[] { .5f, 0f, .6f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, .6f },                  // Overlays
            new[] { .7f, 0f, .5f, .2f, .6f, 1f, .6f, 1f, 1f, 1f, .5f },               // Glyphs
            new[] { .5f, 0f, .3f, .1f, .4f, 1f, .4f, 1f, .5f, .5f, .3f },             // Dots
            new[] { .4f, 0f, .2f, 0f, .3f, .6f, .4f, 1f, .2f, .2f, .2f },             // Mirages
            new[] { .4f, 0f, .2f, .1f, .6f, 1f, .2f, .3f, .1f, .1f, .2f },            // Income
            new[] { .4f, 0f, .3f, .1f, 1f, .5f, .3f, .3f, .1f, .1f, .3f },            // CapitalFlows
            new[] { .6f, 0f, .4f, .1f, .3f, .4f, 1f, .7f, .15f, .15f, .4f },          // Rivers
            new[] { .4f, 0f, .2f, 0f, .2f, .3f, 1f, 1f, .1f, .1f, .2f },              // Glitter
            new[] { .4f, 0f, .3f, .1f, .3f, .5f, 1f, .3f, .1f, .1f, .3f },            // Taxes
            new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 0f },                     // Ties
            new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, .5f, 0f },                    // Coalitions
        };

        static Dictionary<string, ViewSpec> specs;

        /// <summary>Every spec, in <see cref="Ids"/> order.</summary>
        public static IReadOnlyList<ViewSpec> All
        {
            get
            {
                Build();
                List<ViewSpec> list = new List<ViewSpec>(Ids.Length);
                foreach (string id in Ids) list.Add(specs[id]);
                return list;
            }
        }

        /// <summary>The spec of a preset; an unknown id gets the overview's.</summary>
        public static ViewSpec Get(string presetId)
        {
            Build();
            return presetId != null && specs.TryGetValue(presetId, out ViewSpec s) ? s : specs["overview"];
        }

        static void Build()
        {
            if (specs != null) return;
            Dictionary<string, ViewSpec> d = new Dictionary<string, ViewSpec>(StringComparer.Ordinal);
            for (int c = 0; c < Columns.Length; c++)
            {
                ViewSpec s = new ViewSpec { Id = Columns[c] };
                for (int g = 0; g < (int)LandGroup.Count; g++) s.Alpha[g] = Table[g][c];
                d[s.Id] = s;
            }

            d["overview"].HideRoadLabels = false;
            d["overview"].Labels = new[] { LandGroup.Terraces, LandGroup.Cut };
            d["section"].MorphTarget = 0;
            d["section"].HideRoadLabels = false;
            d["section"].Labels = new[] { LandGroup.Cut };
            d["landscape"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors };
            d["y1972"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors };
            d["y1972"].Year = Year1972;
            d["roots"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors, LandGroup.Roots };
            d["capture"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors, LandGroup.Towers, LandGroup.Crown, LandGroup.Overlays };
            d["people"].Labels = new[] { LandGroup.Terraces, LandGroup.Glyphs };
            d["rivers"].Labels = new[] { LandGroup.Pools, LandGroup.Rivers, LandGroup.Taxes };
            d["mind"].Labels = new[] { LandGroup.Glyphs, LandGroup.Mirages, LandGroup.Rivers };
            d["society"].Labels = new[] { LandGroup.Coalitions };
            d["society"].HoldRound = LandStyle.SeasonRounds;
            d["betrayal"].Labels = new[] { LandGroup.Coalitions };
            d["betrayal"].HoldRound = LandStyle.BetrayalHoldRound;
            d["betrayal"].Betrayal = true;
            specs = d;
        }
    }
}
