using System;
using System.Collections.Generic;
using Why.Economy.Emphasis;
using Why.Economy.Land;

namespace Why.Economy
{
    /// <summary>
    /// What a view preset asks of the land, beside its camera pose (<see cref="EconomyPresets"/>): the transition's
    /// target (0 = folded into the cut, 2.4 = the bowl), a temporary year, the season's held round and whether it
    /// shows the betrayal season, whether the road's labels step aside, and the emphasis of every land group (the view's
    /// land.* values in the emphasis table, <see cref="EmphasisTable"/>) with the label groups it shows. Applied by the
    /// land's view layer on focus.
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
    /// with the emphasis of each land group per preset, read from the one emphasis table every economy layer reads (its
    /// land.* keys, <see cref="EconomyEmphasis"/>: the land's view layer eases each group toward it at
    /// <see cref="LandStyle.EmphasisRate"/> per second; land groups are also multiplied by <see cref="LandView.Reveal"/>)
    /// and the label groups each preset shows.
    /// </summary>
    public static class EconomyViews
    {
        /// <summary>The preset ids, keyed (1-8) then keyless.</summary>
        public static readonly string[] Ids =
            { "overview", "section", "landscape", "capture", "people", "rivers", "mind", "society", "roots", "betrayal", "y1972", "foundation" };

        /// <summary>The year the keyless y1972 preset opens temporarily.</summary>
        public const int Year1972 = 1972;

        /// <summary>
        /// Views that tell one story on a structure of their own: what moves people up or down the hill (the decade's
        /// ladder), what companies spend and which way they push, the viewer's own place, the state's levers, and the same
        /// hills on other governments' foundations, and the present taken apart. The wave stays as faint context and every other layer steps back, so
        /// the story reads alone.
        /// </summary>
        public static bool Focused(string id) => id == "mobility" || id == "strategy" || id == "you" || id == "money" || id == "regimes" || id == "present" || id == "advantage";

        /// <summary>The presets with a spec of their own (the others copy the overview's, <see cref="Get"/>).</summary>
        static readonly string[] OwnSpecs =
            { "overview", "section", "landscape", "roots", "capture", "people", "rivers", "mind", "society", "betrayal", "y1972", "foundation" };

        /// <summary>
        /// The emphasis key of each land group, in LandGroup order ("land.road" ... "land.coalitions"): the land's rows of the
        /// emphasis table, which took over this class's table of groups by presets (WP07) with the values its <see cref="Get"/>
        /// gave the presets without a column of their own.
        /// </summary>
        static readonly int[] LandKeys = Keyed();

        static int[] Keyed()
        {
            var keys = new int[(int)LandGroup.Count];
            for (int g = 0; g < keys.Length; g++) keys[g] = EconomyEmphasis.Key("land." + ((LandGroup)g).ToString().ToLowerInvariant());
            return keys;
        }

        /// <summary>A spec's land groups at its view's values in the emphasis table.</summary>
        static void Alphas(ViewSpec s)
        {
            for (int g = 0; g < (int)LandGroup.Count; g++) s.Alpha[g] = EconomyEmphasis.Row(s.Id, LandKeys[g]);
        }

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

        /// <summary>
        /// The spec of a preset; an id without a spec of its own (the wave's later views: companies, atom, dilemma, ladder)
        /// gets a copy of the overview's under its own id, its land groups at its own values in the emphasis table, so
        /// <see cref="Land.LandView.PresetId"/> names the view the hill layers key their emphasis on (a null id gets the
        /// overview's). The person views and the focused views hide the road's labels.
        /// </summary>
        public static ViewSpec Get(string presetId)
        {
            Build();
            if (presetId == null) return specs["overview"];
            if (specs.TryGetValue(presetId, out ViewSpec s)) return s;
            ViewSpec o = specs["overview"];
            s = new ViewSpec { Id = presetId, MorphTarget = o.MorphTarget, HideRoadLabels = Layers.PersonFocus.IsView(presetId) || o.HideRoadLabels, Labels = o.Labels };
            Alphas(s);
            // a focused view's structure stands in front of the road, whose labels step aside
            if (Focused(presetId)) s.HideRoadLabels = true;
            specs[presetId] = s;
            return s;
        }

        static void Build()
        {
            if (specs != null) return;
            Dictionary<string, ViewSpec> d = new Dictionary<string, ViewSpec>(StringComparer.Ordinal);
            foreach (string id in OwnSpecs)
            {
                ViewSpec s = new ViewSpec { Id = id };
                Alphas(s);
                d[s.Id] = s;
            }

            d["overview"].HideRoadLabels = false;
            d["overview"].Labels = new[] { LandGroup.Terraces, LandGroup.Cut };
            d["section"].MorphTarget = 0;
            d["section"].HideRoadLabels = true;   // the wall's industry labels would fall on the card's bars
            d["section"].Labels = new[] { LandGroup.Cut };
            d["landscape"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors };
            d["y1972"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors };
            d["y1972"].Year = Year1972;
            d["foundation"].Labels = new[] { LandGroup.Terraces };
            d["roots"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors, LandGroup.Roots };
            d["capture"].Labels = new[] { LandGroup.Terraces, LandGroup.Sectors, LandGroup.Towers, LandGroup.Crown, LandGroup.Overlays, LandGroup.CapitalFlows };
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
