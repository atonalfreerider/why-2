using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;

namespace Why.Economy
{
    /// <summary>Camera poses for the wave and the separate 3D decision program. Targets are land-local (the frame stands
    /// on the road's present moment: z is time, the hillside rises toward +x); yaw is relative to looking along the
    /// road into the future, so 180 looks back from the future at the crest's face and 90 looks across the wake.</summary>
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
            Add("overview", "Economy / a wave moving into the future", "The road of lives fans out into the economy · industries ride the wave as ridges · companies are born, rise and die on them · the crest is now", KeyCode.Alpha1, new Vector3(95, 18, axis - 70), 140, 26, 270);
            Add("section", "Population / families through time", "Partners meet at the mid-plane; their child's life starts at that junction", KeyCode.Alpha2, Vector3.zero, 0, 6, 6.2f);
            Add("landscape", "The wave of value", "Each rib is a year's economy · tiers stand on the ones beneath them · the crest is where value is created now", KeyCode.Alpha3, new Vector3(105, 26, crest - 20), 152, 18, 210);
            Add("capture", "Who keeps the money", "Every household dollar splits at a market: wages flow back down, owners' surplus climbs to the gold halos and rises to its owners · rose rings pay more than they collect", KeyCode.Alpha4, new Vector3(120, 40, crest - 14), 150, 32, 150);
            Add("people", "Class is altitude", "Valley side: out of work · foothills: frontline · slopes: office · upper slopes: managers · under the summit: owners · clouds: the 1% · arrows: where each cohort's members stood ten years ago (gold climbed · rose fell)", KeyCode.Alpha5, new Vector3(80, 20, crest - 14), 152, 34, 135);
            Add("rivers", "The money circuit", "Desire / fear rivers to each market · gold climbs to the halos · wages back to the valleys · investment falls from the clouds", KeyCode.Alpha6, new Vector3(100, 24, crest - 18), 150, 42, 175);
            Add("mind", "Inside the decision program", "Select a player · memory → desire / fear → deliberation → spending → next balance sheet", KeyCode.Alpha7, HillLandscapeLayer.MindOrigin + new Vector3(0, 1, 0), 0, 20, 15);
            Add("society", "Repeated encounters", "Tit for tat · blue ties cooperate · red ties defect · use the season controls", KeyCode.Alpha8, new Vector3(90, 20, crest - 14), 150, 46, 150);
            Add("foundation", "Social infrastructure", "Taxes rain into the public bedrock · pillars carry pensions, health, schools and roads up to the people · interest rises to bondholders", KeyCode.Alpha9, new Vector3(100, -6, crest - 16), 168, 9, 205);
            Add("companies", "Companies live and die", "Each line is a company: born on its industry's ridge, height = market value, color = how Jev judged it sells (gold need · rose want · ice fear) · ✕ failed · ○ bought", KeyCode.Alpha0, new Vector3(95, 16, axis - 78), 104, 14, 235);
            Add("social", "Attention above capital", "White: the most followed Americans, laid out by who follows whom; rings: followed by the other most-followed · the attention loop: companies pay platforms, platforms pay creators, people give attention and take in wants, and spend · gold rings: capital holders' companies · grey: seats in government", KeyCode.None, new Vector3(95, 70, crest - 10), 150, 18, 175);
            Add("roots", "Industries depend on each other", "Input-output purchases connect suppliers to buyers; widths follow the stored use table", KeyCode.None, new Vector3(100, 10, crest - 16), 150, 34, 190);
            Add("betrayal", "One betrayal", "Follow retaliation and forgiveness over repeated encounters", KeyCode.None, new Vector3(90, 20, crest - 14), 150, 46, 150);
            Add("y1972", "The wave in 1972", "Historical accounts on the same axis: the crest stands back in time · modern companies are not born yet", KeyCode.None, new Vector3(60, 12, y1972 - 30), 140, 24, 190);
            Add("strategy", "Companies spend too", "Each firm runs the people's program · amber: running the business down to suppliers · blue: its workers · forward over the crest: investment and research (ice: fear of losing position · rose: desire for new demand) · rose up: advertising · gold: owners · steel: taxes and lobbying · white arrows: which way the money moves it",
                KeyCode.None, new Vector3(150, 34, crest - 4), 150, 20, 150);
            // The price of money and the edge of trade stand beyond the far side of the wave (HillMoneyLayer), seen from
            // beyond them so time runs left to right with the wave behind.
            float first = HillLandscape.Z(1950);
            Add("money", "The price of money and the edge of trade", "The state's levers on the market layer, on the wave's own time axis · gold: the federal funds rate · rose: the 30-year mortgage · ice: the 10-year Treasury · steel: the effective tariff rate · faint band: exports + imports over GDP",
                KeyCode.None, new Vector3(HillMoneyLayer.ChartX, 26, (first + axis) * .5f), 270, 6, 128);
            // Other governments (HillRegimeLayer): the crest's cross-section from the future, its bedrock redrawn by the regime;
            // aimed toward the valley so the whole section stands left of the panel (from the future, +x runs to the left).
            Add(HillRegimeLayer.View, "Other foundations", "The same US hills on another state: bedrock as deep as its spending, in strata of what it lives on · steel summits it owns, gold private · grey: its rules, gold: firms shaping them · steel lanes: the share of every river that passes through it · white: votes · the border: trade · gold ring: the top 1%",
                KeyCode.None, new Vector3(88, 24, crest), 172, 6, 300);
            // The decade's ladder stands beside the wave (HillMobilityLayer): seen from the hills' side so time runs left
            // to right, its labels and the crest's year on the right.
            float decade = HillLandscape.Z(EconomyState.Year - Model.Mobility.DefaultWindow);
            Add("mobility", "What moves people up or down the hill", "The hill's altitude pulled out beside the wave: five rungs of household income ten years ago and now · streams of people who climbed (gold), fell (rose) or stayed · what came with the moves · the forces at each rung",
                KeyCode.None, new Vector3(HillMobilityLayer.LadderX, HillMobilityLayer.LadderY + HillMobilityLayer.RungStep * 2 - 1, (decade + crest) * .5f - 4), 270, 4, 76);
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

        /// <summary>The views of the followed person (keyless; the tour points at them by id).</summary>
        public static readonly string[] PersonViews = { "atom", "dilemma", "ladder" };

        static ViewPreset Make(string id, string title, string subtitle, KeyCode key, Vector3 target, float yaw, float pitch, float distance)
        {
            var f = EconomyStage.Land();
            return new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key, FixedTarget = f.World(target), FixedYaw = f.Yaw + yaw,
                Pitch = pitch, Distance = distance, PortraitWidth = id == "mind" ? 12 : id == "atom" || id == "dilemma" ? 24 : id == "you" ? 44 : id == "mobility" ? 70 : id == HillRegimeLayer.View ? 330 : 165,
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
            string who = ready && PersonFocus.Life != null ? "Record #" + PersonFocus.Life.Person + " (" + PersonFocus.Life.Why + ")" : "One simulated person";
            switch (id)
            {
                case "atom":
                    return Make(id, "One person, atomistically", who + " · five desires (rose) and five fears (ice) feed reason and a choice · rose threads pull toward markets, ice threads push away from threats, income arrives from its sources",
                        KeyCode.None, (ready ? PersonFocus.Mind : place + Vector3.up * 8.5f) + Vector3.down * 3.2f, 150, 12, 30);
                case "dilemma":
                    return Make(id, "Tit for tat", who + " plays repeated prisoner's dilemmas with spouse, coworker, neighbor, boss and the other tribe · blue cooperates, red defects · one slip in round 9, then retaliation or forgiveness · above: the groups",
                        KeyCode.None, (ready ? PersonFocus.Arena : place + new Vector3(-12, 5.5f, -5)) + Vector3.up * 2.4f, 155, 24, 27);
                default:
                    float span = ready ? PersonFocus.LadderSpan : 90;
                    return Make("ladder", "The economic ladder", who + " across the wave: each year on the household's hill at the rung of its income · blue labor, gold in control · white ghost: the parents' rank",
                        KeyCode.None, ready ? PersonFocus.LadderCenter + Vector3.up * 3 : new Vector3(60, 12, crest - 30), 118, 16, Mathf.Clamp(span * 0.8f, 36, 150));
            }
        }

        /// <summary>The viewer's own place (keyless "you" view), posed on where <see cref="YouFocus"/> last placed them:
        /// from the future, in front of the crest and a little to the valley side, so the possible futures stand between
        /// the camera and the viewer and the road of lives (which ends at the crest) stays out of the way.</summary>
        public static ViewPreset You()
        {
            Vector3 place = YouFocus.Ready ? YouFocus.Place : new Vector3(60, 14, HillLandscape.Z(EconomyState.Year) - 6);
            return Make("you", "Where you stand", "Use the panel to tell the model about your household · you stand at your rung on the hill you work in · your spending's rivers (ice: sold by fear, rose: desire) · gold: the income it pays that flows uphill · ahead: where people like you were a decade later",
                KeyCode.None, place + new Vector3(3, 5, 4), 166, 15, 44);
        }

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
