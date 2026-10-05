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
            Add("overview", "Economy / a wave moving into the future", "The road of lives fans out into the economy · industries ride the wave as ridges · companies are born, rise and die on them · the crest is now", KeyCode.Alpha1, new Vector3(95, 18, crest - 70), 140, 26, 270);
            Add("section", "Population / families through time", "Partners meet at the mid-plane; their child's life starts at that junction", KeyCode.Alpha2, Vector3.zero, 0, 6, 6.2f);
            Add("landscape", "The wave of value", "Each rib is a year's economy · tiers stand on the ones beneath them · the crest is where value is created now", KeyCode.Alpha3, new Vector3(105, 26, crest - 20), 152, 18, 210);
            Add("capture", "Who keeps the money", "Every household dollar splits at a market: wages flow back down, owners' surplus climbs to the gold halos and rises to its owners · rose rings pay more than they collect", KeyCode.Alpha4, new Vector3(120, 40, crest - 14), 150, 32, 150);
            Add("people", "Class is altitude", "Valley side: out of work · foothills: frontline · slopes: office · upper slopes: managers · under the summit: owners · clouds: the 1% · threads: the road of lives", KeyCode.Alpha5, new Vector3(80, 20, crest - 14), 152, 34, 135);
            Add("rivers", "The money circuit", "Desire / fear rivers to each market · gold climbs to the halos · wages back to the valleys · investment falls from the clouds", KeyCode.Alpha6, new Vector3(100, 24, crest - 18), 150, 42, 175);
            Add("mind", "Inside the decision program", "Select a player · memory → desire / fear → deliberation → spending → next balance sheet", KeyCode.Alpha7, HillLandscapeLayer.MindOrigin + new Vector3(0, 1, 0), 0, 20, 15);
            Add("society", "Repeated encounters", "Tit for tat · blue ties cooperate · red ties defect · use the season controls", KeyCode.Alpha8, new Vector3(90, 20, crest - 14), 150, 46, 150);
            Add("foundation", "Social infrastructure", "Taxes rain into the public bedrock · pillars carry pensions, health, schools and roads up to the people · interest rises to bondholders", KeyCode.Alpha9, new Vector3(100, -6, crest - 16), 168, 9, 205);
            Add("companies", "Companies live and die", "Each line is a company: born on its industry's ridge, height = market value, color = how Jev judged it sells (gold need · rose want · ice fear) · ✕ failed · ○ bought", KeyCode.Alpha0, new Vector3(95, 16, crest - 78), 104, 14, 235);
            Add("roots", "Industries depend on each other", "Input-output purchases connect suppliers to buyers; widths follow the stored use table", KeyCode.None, new Vector3(100, 10, crest - 16), 150, 34, 190);
            Add("betrayal", "One betrayal", "Follow retaliation and forgiveness over repeated encounters", KeyCode.None, new Vector3(90, 20, crest - 14), 150, 46, 150);
            Add("y1972", "The wave in 1972", "Historical accounts on the same axis: the crest stands back in time · modern companies are not born yet", KeyCode.None, new Vector3(60, 12, y1972 - 30), 140, 24, 190);
            // The families: a side view of the road's last decades, from the valley side, with the wave rising behind.
            var section = list[1]; section.FixedTarget = EconomyStage.OnRoad(EconomyState.Year - 9, .55f, EconomyStyle.FramingRho);
            section.FixedYaw = f.Yaw + 70; section.Pitch = 9; section.Distance = 34; section.PortraitWidth = 30;
            return list;

            void Add(string id, string title, string subtitle, KeyCode key, Vector3 target, float yaw, float pitch, float distance)
            {
                list.Add(new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key, FixedTarget = f.World(target), FixedYaw = f.Yaw + yaw,
                    Pitch = pitch, Distance = distance, PortraitWidth = id == "mind" ? 12 : 165, YaOld = DeepTime.NowYear - EconomyStyle.FirstYear,
                    YaNew = 0, LogOffset = EconomyStyle.WindowLogOffset, Length = EconomyStyle.WindowLength,
                    RhoScale = EconomyStyle.RhoScale, YScale = EconomyStyle.YScale });
            }
        }
    }
}
