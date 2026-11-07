using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;

namespace Why.Economy
{
    /// <summary>Camera poses for the wave and the separate 3D decision program. Targets are land-local (the frame stands
    /// on the road's present moment: z is time, the hillside rises toward +x); yaw is relative to looking along the
    /// road into the future, so 180 looks back from the future at the crest's face and 90 looks across the wake.
    /// Each view's subtitle says in about fifteen plain words what the picture shows; how to read its colors and marks is
    /// the view's key (<see cref="Key"/>), which the HUD's legend shows, during the tour too.</summary>
    public static class HillPresets
    {
        public static List<ViewPreset> Build()
        {
            var f = EconomyStage.Land();
            var list = new List<ViewPreset>();
            float crest = HillLandscape.Z(EconomyState.Year), y1972 = HillLandscape.Z(EconomyViews.Year1972);
            // The views of the whole time axis (the overview, the companies' lives, the money chart) frame the wake up to the
            // present whatever year the crest stands at: an earlier year, or the replay, grows the wave across them instead of
            // carrying them into the empty past before the first rib. The other views stand on the crest.
            float axis = HillLandscape.Z(System.Math.Max(EconomyState.Year, EconomyState.DefaultYear(EconomyState.MaxYear)));
            Add("overview", "The US economy, a wave through time", "Each rib across the wave is one year's economy; the bright crest in front is the year shown, and the past stretches behind it.", KeyCode.Alpha1, new Vector3(95, 18, axis - 70), 140, 26, 270);
            Add("section", "Families through time", "Each line is one simulated life. Partners' lines meet, and a child's life begins where they join.", KeyCode.Alpha2, Vector3.zero, 0, 6, 6.2f);
            Add("landscape", "Layers of the economy", "Each industry is a ridge as wide as its share of what the economy produces, layered from raw materials up to tech.", KeyCode.Alpha3, new Vector3(105, 26, crest - 20), 152, 18, 210);
            // A lower camera than the other crest views: the owners' climbs end in the clouds above the hills, in frame. The
            // year's split of the dollar is printed once, in the scene over a bar of its parts (HillCaptureLayer): there it
            // follows a what-if program as the drawing does, and it shows during the tour too, when this subtitle is hidden.
            Add("capture", "Who keeps the money", "Where each household dollar goes at the market: to wages, to owners, to upkeep, to imports and to taxes.", KeyCode.Alpha4, new Vector3(120, 40, crest - 14), 150, 28, 150);
            Add("people", "Class is height on the hill that pays you", "Groups of households stand on the hill of the industry that pays them, higher with more income; the top 1% float above.", KeyCode.Alpha5, new Vector3(80, 20, crest - 14), 152, 34, 135);
            Add("rivers", "Rivers of desire and fear", "Household spending flows to the industries that sell what it buys, in two colors: rose for wants, ice blue for fears.", KeyCode.Alpha6, new Vector3(100, 24, crest - 18), 150, 42, 175);
            Add("mind", "Inside one decision", "One group of households deciding: what it wants (left), what it fears (right), and what it chooses to spend.", KeyCode.Alpha7, HillLandscapeLayer.MindOrigin + new Vector3(0, 1, 0), 0, 20, 15);
            Add("society", "Repeated encounters", "Groups of households meet again and again and choose to cooperate or cheat; most copy what the other did last time.", KeyCode.Alpha8, new Vector3(90, 20, crest - 14), 150, 46, 150);
            Add("foundation", "Government, the bedrock", "Federal spending as the bedrock under every hill, in layers by what it pays for; taxes flow in, and programs flow out to people.", KeyCode.Alpha9, new Vector3(100, -6, crest - 16), 168, 9, 205);
            Add("companies", "Companies live and die", "Each line is a company's life on its industry's ridge, rising with its market value; × marks a failure, ○ a takeover.", KeyCode.Alpha0, new Vector3(95, 16, axis - 78), 104, 14, 235);
            Add("social", "Attention above capital", "The most-followed Americans (followers across platforms, 2025) float above the economy, placed by who follows whom on Instagram.", KeyCode.None, new Vector3(95, 70, crest - 10), 150, 18, 175);
            Add("roots", "Industries depend on each other", "Lines under the hills join each industry to the ones it buys from; the wider the line, the more money a year.", KeyCode.None, new Vector3(100, 10, crest - 16), 150, 34, 190);
            Add("betrayal", "One betrayal", "One group cheats a partner that trusted it. Play the rounds: lines turn rose where others strike back and blue again where they forgive.", KeyCode.None, new Vector3(90, 20, crest - 14), 150, 46, 150);
            // Aimed high enough that today's crest, outlined over 1972's (HillWaveLayer), stays below the title: its tech end,
            // highest and furthest up the hillside, stands about 150 px down at 1920x1080, right of the What if? chip (aimed 10
            // units lower, it stood behind the title). From 190 units the comparison's labels keep the size they were made for.
            // HillWaveLayer sizes the tiers' shares and places the bedrock's name for this pose (Then, Face): measure them
            // again if it changes.
            Add("y1972", "The economy in 1972", Subtitle1972(), KeyCode.None, new Vector3(60, 22, y1972 - 30), 140, 24, 190);
            Add("strategy", "Companies spend too", "Where one large company's money went in its latest year: to suppliers, workers, the future, its owners and the state.",
                KeyCode.None, new Vector3(150, 34, crest - 4), 150, 20, 150);
            // The price of money and the edge of trade stand beyond the far side of the wave (HillMoneyLayer), seen from
            // beyond them so time runs left to right with the wave behind. The chart ends at the year on screen.
            float first = HillLandscape.Z(1950);
            int shown = LandService.ShownYear > 0 ? LandService.ShownYear : EconomyState.Year;
            Add("money", "The price of money, and trade", "Yearly rates up to " + shown + ": the central bank's rate, what homebuyers and the government pay to borrow, and the tax on imports.",
                KeyCode.None, new Vector3(HillMoneyLayer.ChartX, 26, (first + axis) * .5f), 270, 6, 128);
            // Other governments (HillRegimeLayer): the crest's cross-section from the future, its bedrock redrawn by the regime;
            // aimed toward the valley so the whole section stands left of the panel (from the future, +x runs to the left).
            Add(HillRegimeLayer.View, "What if: the US economy on another government", "The same US hills standing on the government of Sweden, China, Saudi Arabia or the Soviet Union: a thought experiment.",
                KeyCode.None, new Vector3(88, 24, crest), 172, 6, 300);
            // The decade's ladder stands beside the wave (HillMobilityLayer): seen from the hills' side so time runs left
            // to right, its labels and the crest's year on the right.
            float decade = HillLandscape.Z(EconomyState.Year - Model.Mobility.DefaultWindow);
            Add("mobility", "Who moved up or down, and what came with it", "Five income levels ten years ago (left) and now (right): who climbed, stayed or fell, and what happened in their lives.",
                KeyCode.None, new Vector3(HillMobilityLayer.LadderX, HillMobilityLayer.LadderY + HillMobilityLayer.RungStep * 2 - 1, (decade + crest) * .5f - 4), 270, 4, 76);
            // The present, taken apart (HillPresentLayer): the crest's cut face exploded into the empty future beyond it, seen
            // from further on, looking back at the crest 20 degrees off the wave's axis (the design's pose, its distance grown
            // with the present's width). Entering plays the choreography (PresentFace, then PresentRise, then this pose).
            Add(HillPresentLayer.View, "The present, taken apart", "This year's economy taken out of the wave: the state beneath, tiers as terraces, each industry a field of its firms, every peak as wide as its market share and as tall as its margin, gold where it rises above normal profit; veins for what the industries buy from one another, threads for who owns the profits.",
                KeyCode.None, new Vector3(PresentX, 70, crest + PresentLayout.Gap0 + 100), 160, 24, 590);
            // The present as a contest (HillAdvantageLayer): the crest of 2026 opened into the arena past it, seen from the
            // future side and above, looking back toward the wave 20 degrees off its axis. Key I opens it (the crest's cut
            // face, then the floor spreading out of it as the peaks rise).
            Add(HillAdvantageLayer.View, "The present as a contest", "2026's economy as a living contest: each company its balance sheet inside a shell of market value, higher the more it dominates; rivers of money between them; their owners in gold above.",
                KeyCode.I, HillAdvantageLayer.FrameTarget(crest), HillAdvantageLayer.FrameYaw, HillAdvantageLayer.FramePitch, HillAdvantageLayer.FrameDistance);
            // The followed person (HillPersonLayer, HillDilemmaLayer): poses follow where the person stands.
            foreach (string id in PersonViews) list.Add(Person(id));
            // The viewer (HillYouLayer): the pose follows where the "You" panel places them.
            list.Add(You());
            // The families: a side view of the road's last decades, from the valley side, with the wave rising behind.
            var section = list[1]; section.FixedTarget = EconomyStage.OnRoad(EconomyState.Year - 9, .55f, EconomyStyle.FramingRho);
            section.FixedYaw = f.Yaw + 70; section.Pitch = 9; section.Distance = 34; section.PortraitWidth = 30;
            return list;

            void Add(string id, string title, string subtitle, KeyCode key, Vector3 target, float yaw, float pitch, float distance) =>
                list.Add(Make(id, title, subtitle, key, target, yaw, pitch, distance));
        }

        /// <summary>The middle of the exploded present across the land (land-local x, at the latest year's width): the
        /// terraces run from about 50 to 580 at <see cref="PresentLayout.S"/> times the crest's width plus their gaps.</summary>
        const float PresentX = 290;

        /// <summary>The present's first pose (not in the catalog): the crest's cut face from the future, as the regimes view
        /// sees it, where the choreography starts.</summary>
        public static ViewPreset PresentFace() => Make(HillPresentLayer.View, "The present, taken apart", "", KeyCode.None,
            new Vector3(110, 24, HillLandscape.Z(EconomyState.Year)), 172, 6, 300);

        /// <summary>The present's second pose (not in the catalog): pulled back and up as the cut face grows to full scale.</summary>
        public static ViewPreset PresentRise() => Make(HillPresentLayer.View, "The present, taken apart", "", KeyCode.None,
            new Vector3(PresentX - 20, 40, HillLandscape.Z(EconomyState.Year) + PresentLayout.Gap0 + 100), 160, 18, 700);

        /// <summary>A pose of the contest at a world target (the strata's focus, HillAdvantageLayer.FocusOn): the view's yaw,
        /// the given pitch and distance (not in the catalog).</summary>
        public static ViewPreset AdvantagePose(Vector3 worldTarget, float pitch, float distance)
        {
            var p = Make(HillAdvantageLayer.View, "The present as a contest", "", KeyCode.None, Vector3.zero, HillAdvantageLayer.FrameYaw, pitch, distance);
            p.FixedTarget = worldTarget;
            return p;
        }

        /// <summary>The contest's first pose (not in the catalog): the crest's cut face from the future, where the arena opens.</summary>
        public static ViewPreset AdvantageFace() => Make(HillAdvantageLayer.View, "The present as a contest", "", KeyCode.None,
            new Vector3(110, 24, HillLandscape.Z(EconomyState.Year)), 172, 8, 320);

        /// <summary>The contest's history (not in the catalog): the wave from 1947 to the crest seen from the hillside, so time
        /// runs left to right into the arena on the right (as the money view's yaw 270); the lineage's lane and the contests on
        /// the companies' lifelines in front of the wave.</summary>
        public static ViewPreset AdvantageHistory()
        {
            float first = HillLandscape.Z(1947), crest = HillLandscape.Z(EconomyState.Year);
            return Make(HillAdvantageLayer.View, "The present as a contest: its history", "", KeyCode.None,
                new Vector3(190, 22, (first + crest) * .5f + 30), 262, 12, 285);
        }

        /// <summary>The views of the followed person (keyless; the tour points at them by id).</summary>
        public static readonly string[] PersonViews = { "atom", "dilemma", "ladder" };

        static ViewPreset Make(string id, string title, string subtitle, KeyCode key, Vector3 target, float yaw, float pitch, float distance)
        {
            var f = EconomyStage.Land();
            return new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key, FixedTarget = f.World(target), FixedYaw = f.Yaw + yaw,
                Pitch = pitch, Distance = distance, PortraitWidth = id == "mind" ? 12 : id == "atom" || id == "dilemma" ? 24 : id == "you" ? 44 : id == "mobility" ? 70 : id == HillRegimeLayer.View ? 330 : id == HillPresentLayer.View || id == HillAdvantageLayer.View ? 600 : 165,
                YaOld = DeepTime.NowYear - EconomyStyle.FirstYear, YaNew = 0, LogOffset = EconomyStyle.WindowLogOffset, Length = EconomyStyle.WindowLength,
                RhoScale = EconomyStyle.RhoScale, YScale = EconomyStyle.YScale };
        }

        /// <summary>A view of the followed person, posed on where <see cref="PersonFocus"/> last placed them (a default
        /// near the crest before a person is placed).</summary>
        public static ViewPreset Person(string id)
        {
            float crest = HillLandscape.Z(EconomyState.Year);
            bool ready = PersonFocus.Ready;
            Vector3 place = ready ? PersonFocus.Place : new Vector3(80, 20, crest - 3);
            // the person is one of the simulation's, never a real record: say so wherever they are named
            string name = ready && PersonFocus.Life != null ? "Synthetic person #" + PersonFocus.Life.Person : "One simulated person";
            string who = ready && PersonFocus.Life != null && !string.IsNullOrEmpty(PersonFocus.Life.Why) ? name + ", " + PersonFocus.Life.Why : name;
            switch (id)
            {
                case "atom":
                    return Make(id, "One person, up close", who + ": the wants that pull their spending, the fears that push it, and where their income comes from.",
                        KeyCode.None, (ready ? PersonFocus.Mind : place + Vector3.up * 8.5f) + Vector3.down * 3.2f, 150, 12, 30);
                case "dilemma":
                    return Make(id, "Cooperate or cheat?", name + " keeps meeting a spouse, a coworker, a neighbor, a boss and a political rival; each time both choose: blue to cooperate, red to cheat.",
                        KeyCode.None, (ready ? PersonFocus.Arena : place + new Vector3(-12, 5.5f, -5)) + Vector3.up * 2.4f, 155, 24, 27);
                default:
                    float span = ready ? PersonFocus.LadderSpan : 90;
                    return Make("ladder", "The economic ladder", who + ", year by year: the higher their household's income, the higher they stand on the hill that pays them.",
                        KeyCode.None, ready ? PersonFocus.LadderCenter + Vector3.up * 3 : new Vector3(60, 12, crest - 30), 118, 16, Mathf.Clamp(span * 0.8f, 36, 150));
            }
        }

        /// <summary>The viewer's own place (keyless "you" view), posed on where <see cref="YouFocus"/> last placed them:
        /// from the future, in front of the crest and a little to the valley side, so the ruler of possible futures stands
        /// beside the viewer on screen and the road of lives (which ends at the crest) stays out of the way.</summary>
        public static ViewPreset You()
        {
            Vector3 place = YouFocus.Ready ? YouFocus.Place : new Vector3(60, 14, HillLandscape.Z(EconomyState.Year) - 6);
            return Make("you", "Where do you stand?", "Enter your household's age, income, savings and work in the panel to see your place among simulated Americans and where your spending goes.",
                KeyCode.None, place + new Vector3(3, 5, 4), 166, 15, 44);
        }

        /// <summary>The 1972 view's caption: real growth since then and the services tier's share then and now, read off the
        /// industries' value added (industries.json, in 2025 dollars), so then-versus-now is on screen, not left to memory.</summary>
        static string Subtitle1972()
        {
            var data = LandService.Model?.Data;
            if (data != null && ReferenceEquals(data, subtitleData)) return subtitle1972;
            subtitleData = data;
            return subtitle1972 = Growth1972(data);
        }

        static Data.EconomyData subtitleData;
        static string subtitle1972;

        static string Growth1972(Data.EconomyData data)
        {
            int then = EconomyViews.Year1972, now = EconomyState.DefaultYear(EconomyState.MaxYear);
            double past = data != null ? data.Real(data.ValueAddedTotal(then), then) : 0, present = data != null ? data.Real(data.ValueAddedTotal(now), now) : 0;
            if (past <= 0 || present <= 0) return "The wave's bright crest set back to 1972: a smaller economy, before most of today's companies were born.";
            double Services(int year)
            {
                double s = 0;
                foreach (var industry in data.Industries) if (industry.TierId == "services") s += industry.ValueAdded.GrowthAt(year);
                return s / Math.Max(1e-9, data.ValueAddedTotal(year));
            }
            return "The wave's bright crest set back to 1972. Since then the economy has become " + LandFacts.Num(present / past, 1) +
                   " times as large after inflation, and services went from " + LandFacts.Percent(Services(then)) + " to " + LandFacts.Percent(Services(now)) + " of it.";
        }

        // ------------------------------------------------------------------ the bar, the help sheet and the legend

        /// <summary>Captions of the HUD's view bar: one to three words, so every view reads at full size (the tooltip has the
        /// full title).</summary>
        static readonly Dictionary<string, string> ShortTitles = new Dictionary<string, string>
        {
            { "overview", "Wave" }, { "section", "Families" }, { "landscape", "Layers" }, { "capture", "Who keeps it" }, { "people", "Class" },
            { "rivers", "Rivers" }, { "mind", "Mind" }, { "society", "Encounters" }, { "foundation", "Bedrock" }, { "companies", "Companies" },
            { "social", "Attention" }, { "roots", "Supply chains" }, { "betrayal", "Betrayal" }, { "y1972", "1972" }, { "strategy", "Firm spending" },
            { "money", "Money & trade" }, { HillRegimeLayer.View, "Other states" }, { "mobility", "Mobility" }, { "atom", "One person" },
            { "dilemma", "Dilemma" }, { "ladder", "Ladder" }, { "you", "You" }, { HillPresentLayer.View, "Industries" },
            { HillAdvantageLayer.View, "Advantage" },
        };

        /// <summary>A view's caption in the bar (its title when it has no short one).</summary>
        public static string ShortTitle(ViewPreset preset) =>
            preset == null ? "" : ShortTitles.TryGetValue(preset.Id, out string s) ? s : preset.Title;

        /// <summary>The views grouped for the help sheet, each chapter in the order a first visit would take them.</summary>
        public static readonly (string chapter, string[] ids)[] Chapters =
        {
            ("The wave", new[] { "overview", "landscape", "foundation", "roots", "y1972", "section", HillRegimeLayer.View, HillAdvantageLayer.View, HillPresentLayer.View }),
            ("Money", new[] { "capture", "rivers", "companies", "strategy", "money", "social" }),
            ("People", new[] { "people", "mind", "mobility", "society", "betrayal" }),
            ("One life", new[] { "atom", "ladder", "dilemma", "you" }),
        };

        /// <summary>
        /// A view's own key for the HUD's legend: its swatches (a color and what it marks there) and one short reading of
        /// its marks. The views of the whole wave share the legend's own key (the hills' hues and grammar); a view that
        /// tells its story with other marks (a flat chart, a mind, a ladder, a flow split by motive) brings its own. Colors
        /// are the palette's (<see cref="EconomyStyle"/>), so a key follows the palette when the layers do.
        /// </summary>
        public sealed class ViewKey
        {
            public readonly (string label, Color color)[] Swatches;
            public readonly string Reading;
            public ViewKey((string, Color)[] swatches, string reading) { Swatches = swatches; Reading = reading; }
        }

        /// <summary>White of the attention cloud's stars and of the regimes' votes; amber of a firm's spending on its suppliers
        /// (the layers' own hues, which the palette does not name yet).</summary>
        public static readonly Color White = new Color(1f, .97f, .92f), Amber = new Color(1f, .80f, .52f);

        /// <summary>The key of a view, or null for the views of the whole wave (the legend's own key); the present's carries its
        /// year's area scale (HillPresentLayer).</summary>
        public static ViewKey Key(string id) => id == HillPresentLayer.View ? HillPresentLayer.Key : id == HillAdvantageLayer.View ? HillAdvantageLayer.Key : id != null && Keys.TryGetValue(id, out ViewKey k) ? k : null;

        /// <summary>The first year the attention cloud is drawn on the wave: Instagram's, whose follows place its stars (the
        /// cloud's own gate, HillSocialLayer's PlatformYear, is set from it).</summary>
        public const int AttentionFirstYear = 2010;

        /// <summary>Whether the land on screen draws the attention cloud in the views of the whole wave: not in 1972's view, nor
        /// in an earlier year of the slider or the replay, where the legend's key leaves the cloud's swatch out.</summary>
        public static bool AttentionDrawn => (HillLandscapeLayer.Current?.Year ?? EconomyState.Year) >= AttentionFirstYear;

        static readonly ViewKey Encounters = new ViewKey(new (string, Color)[] { ("Cooperate", EconomyStyle.Wages), ("Cheat", EconomyStyle.Desire) },
            "Each line joins two groups of households that keep meeting; its color runs from rose (they cheat each other) to blue (they cooperate). The panel replays the rounds.");

        static readonly Dictionary<string, ViewKey> Keys = new Dictionary<string, ViewKey>
        {
            ["section"] = new ViewKey(new (string, Color)[] { ("One simulated American life", EconomyStyle.People) },
                "Each line is a life through time. Where two lines meet, partners join; where a new line starts at a junction, their child is born."),
            ["capture"] = new ViewKey(new (string, Color)[] { ("To owners", EconomyStyle.Capital), ("To wages", EconomyStyle.Wages), ("To taxes", EconomyStyle.Government) },
                "From each market, gold climbs to the owners' rings above the hills, blue runs back down to the workers and grey falls into the bedrock as taxes. Rose rings: groups that pay owners more than they collect."),
            ["people"] = new ViewKey(new (string, Color)[] { ("Climbed", EconomyStyle.Capital), ("Fell", EconomyStyle.Desire) },
                "Height: each group's income rank on the hill that pays it; the top 1% float in the clouds. Arrows: where the group's members stood ten years ago. Each moving dot is a fifth of a group."),
            ["rivers"] = new ViewKey(new (string, Color)[] { ("Spent toward a want", EconomyStyle.Desire), ("Spent away from a fear", EconomyStyle.Fear), ("Wages", EconomyStyle.Wages), ("Owners", EconomyStyle.Capital) },
                "Each river carries household money to the industries that sell what it buys; the wider, the more money a year. Which purchases are driven by fear was judged item by item by an AI model."),
            ["mind"] = new ViewKey(new (string, Color)[] { ("Wants", EconomyStyle.Desire), ("Fears", EconomyStyle.Fear) },
                "The rose spheres (wants) and blue spheres (fears) on either side are the drives: the bigger the sphere, the larger the drive's share of the motive. The white sphere at the center (deliberation) weighs them with reason into the choice below it: spend, save or borrow. Memory (what is owned and owed) carries the result into next year."),
            ["society"] = Encounters,
            ["betrayal"] = Encounters,
            ["foundation"] = new ViewKey(new (string, Color)[] { ("Taxes", EconomyStyle.Government), ("Pensions and health", EconomyStyle.Fear), ("Schools, police, roads", EconomyStyle.Wages), ("Interest on the debt", EconomyStyle.Capital) },
                "The deeper the block, the more the federal government spends. Grey lines up: rules; gold lines down: industries shaping those rules (judged by an AI model)."),
            ["companies"] = new ViewKey(new (string, Color)[] { ("Sells a need", EconomyStyle.Capital), ("Sells a want", EconomyStyle.Desire), ("Sells safety from a fear", EconomyStyle.Fear) },
                "Each line is one company over time, rising with its market value (compressed scale); its color is what it mostly sells, as judged by an AI model (Jev). × failed · ○ bought."),
            ["social"] = new ViewKey(new (string, Color)[] { ("The most-followed Americans", White), ("Their companies", EconomyStyle.Capital), ("In office", EconomyStyle.Government) },
                "Size: followers across Instagram, TikTok, YouTube and X (2025). Placed by who follows whom on Instagram. Rings: how many of the 1,139 most-followed accounts follow them. The loop: companies pay platforms, platforms pay creators, people give their attention."),
            ["roots"] = new ViewKey(Array.Empty<(string, Color)>(),
                "Lines under the hills: what each industry buys from the others (US accounts, 2024); the wider the line, the more money a year."),
            ["strategy"] = new ViewKey(new (string, Color)[] { ("Suppliers", Amber), ("Workers", EconomyStyle.Wages), ("Investing so as not to fall behind", EconomyStyle.Fear),
                    ("Investing and advertising for new demand", EconomyStyle.Desire), ("Owners", EconomyStyle.Capital), ("Taxes and lobbying", EconomyStyle.Government) },
                "White arrows: which way the money moves the company, as judged by an AI model (Jev)."),
            ["money"] = new ViewKey(new (string, Color)[] { ("Central bank rate", EconomyStyle.Capital), ("Mortgage rate", EconomyStyle.Desire), ("Government borrowing rate", EconomyStyle.Fear), ("Tariff on imports", EconomyStyle.Government) },
                "Each line shows percent a year, left to right up to the year shown, starting with the first year of its data (some start later). The strip below shows trade (exports plus imports) as a share of GDP, on its own scale."),
            [HillRegimeLayer.View] = new ViewKey(new (string, Color)[] { ("Owned by the state", EconomyStyle.Government), ("Privately owned", EconomyStyle.Capital), ("Votes", White) },
                "The US hills on another country's government, as a what-if. The bedrock is as deep as all its levels of government spend. Dotted rings: summits no firm is ranked for; the steel gauge: the state's share of the value of its listed firms."),
            ["mobility"] = new ViewKey(new (string, Color)[] { ("Climbed", EconomyStyle.Capital), ("Fell", EconomyStyle.Desire), ("Stayed", EconomyStyle.Government) },
                "Left: a household's income level ten years ago; right: now. Stream width: number of people. Life events go with the moves in the simulation; they are not shown to cause them. The ladder counts simulated adults 25 and older, ranked as the tax records rank people; dashed outlines: the US tax records (the panel switches to every adult)."),
            ["atom"] = new ViewKey(new (string, Color)[] { ("Wants", EconomyStyle.Desire), ("Fears", EconomyStyle.Fear), ("Pay from work", EconomyStyle.Wages), ("Income from owning", EconomyStyle.Capital), ("Help from the state", EconomyStyle.Government) },
                "Rose lines pull toward what the person buys; ice lines push away from threats."),
            ["dilemma"] = new ViewKey(new (string, Color)[] { ("Cooperates", EconomyStyle.Wages), ("Cheats", EconomyStyle.Defect) },
                "In each round both sides choose. After one mistake in round 9, see who strikes back and who forgives. Above: the groups play the same game."),
            ["ladder"] = new ViewKey(new (string, Color)[] { ("Living on wages", EconomyStyle.Wages), ("Living on savings and investments", EconomyStyle.Capital), ("Their parents' level", White) },
                "The line follows one life, year by year; the higher it is, the more the household earns compared with others on its hill."),
            ["you"] = new ViewKey(new (string, Color)[] { ("Your spending on wants", EconomyStyle.Desire), ("Your spending on fears", EconomyStyle.Fear), ("Income that goes to people ranked above you", EconomyStyle.Capital) },
                "You stand at your income rank on the hill you work in. Gold, up: the share of all wages and owners' income that goes to people ranked above you (the population's, not traced from your purchases). The gold ring at your feet is your cushion; one turn is ten years. Beside you: where people like you were ten years later, one step per fifth."),
        };

        /// <summary>The viewer moved: the catalog's "you" view takes the new pose, and the camera follows when it is on screen.</summary>
        public static void RefreshYouView()
        {
            var root = GraphRoot.Instance; var catalog = ViewPresets.All;
            if (catalog == null) return;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].Id != "you") continue;
                bool moved = EconomyPresets.CopyPose(You(), catalog[i]);
                var now = root != null ? root.CurrentPreset : null;
                if (moved && now != null && now.Id == "you" && root.Rig != null)
                {
                    if (!ReferenceEquals(now, catalog[i])) EconomyPresets.CopyPose(catalog[i], now);
                    root.Rig.FlyTo(now.Pose(), 1.2f);
                }
            }
        }

        /// <summary>The person moved (a new person or land): the catalog's person views take the new poses in place, and
        /// the camera follows when one of them is on screen.</summary>
        public static void RefreshPersonViews()
        {
            var root = GraphRoot.Instance; var catalog = ViewPresets.All;
            if (catalog == null) return;
            foreach (string id in PersonViews)
                for (int i = 0; i < catalog.Count; i++)
                {
                    if (catalog[i].Id != id) continue;
                    bool moved = EconomyPresets.CopyPose(Person(id), catalog[i]);
                    var now = root != null ? root.CurrentPreset : null;
                    if (moved && now != null && now.Id == id && root.Rig != null)
                    {
                        if (!ReferenceEquals(now, catalog[i])) EconomyPresets.CopyPose(catalog[i], now);
                        root.Rig.FlyTo(now.Pose(), 1.6f);
                    }
                }
        }
    }
}
