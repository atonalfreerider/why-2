using System.Collections.Generic;
using UnityEngine;
namespace Why.Economy
{
    /// <summary>Camera poses for the landscape and the separate 3D decision program.</summary>
    public static class HillPresets
    {
        public static List<ViewPreset> Build()
        {
            var f = EconomyStage.Land();
            var list = new List<ViewPreset>();
            Add("overview", "Economy / a landscape of lives", "Industry hills, households, ownership and the money between them", KeyCode.Alpha1, new Vector3(0, 7, 5), 24, 76);
            Add("section", "Population / families through time", "Partners meet at the mid-plane; their child's life starts at that junction", KeyCode.Alpha2, new Vector3(0, 0, -12), 28, 17);
            Add("landscape", "Where value is created", "Hill footprint = value added · the work happens on the slopes · gold summit = capital claims", KeyCode.Alpha3, new Vector3(0, 6, 5), 30, 72);
            Add("capture", "Who keeps the money", "Every household dollar splits at a market: wages flow back down, owners' surplus climbs to the gold halos and rises to its owners · rose rings pay more than they collect", KeyCode.Alpha4, new Vector3(2, 15, 13), 16, 58);
            Add("people", "Class is altitude", "Valley floor: out of work · foothills: frontline · slopes: office · upper slopes: managers · under the summit: owners · clouds: the 1%", KeyCode.Alpha5, new Vector3(4, 2, -8), 18, 36);
            Add("rivers", "The money circuit", "Desire / fear rivers to each market · gold climbs to the halos · wages back to the valleys · investment falls from the clouds", KeyCode.Alpha6, new Vector3(0, 5, 5), 48, 73);
            Add("mind", "Inside the decision program", "Select a player · memory → desire / fear → deliberation → spending → next balance sheet", KeyCode.Alpha7, new Vector3(145, 3, 0), 20, 15);
            Add("society", "Repeated encounters", "Tit for tat · blue ties cooperate · red ties defect · use the season controls", KeyCode.Alpha8, new Vector3(0, 5, 5), 46, 74);
            Add("foundation", "Social infrastructure", "Taxes rain into the public bedrock · pillars carry pensions, health, schools and roads up to the people · interest rises to bondholders", KeyCode.Alpha9, new Vector3(0, -2, -12), 9, 205);
            Add("roots", "Industries depend on each other", "Input-output purchases connect suppliers to buyers; widths follow the stored use table", KeyCode.None, new Vector3(0, 5, 5), 48, 74);
            Add("betrayal", "One betrayal", "Follow retaliation and forgiveness over repeated encounters", KeyCode.None, new Vector3(0, 5, 5), 46, 74);
            Add("y1972", "The landscape in 1972", "Historical accounts on the same terrain; modern company crowns are omitted", KeyCode.None, new Vector3(0, 6, 5), 30, 72);
            var section=list[1];section.FixedTarget=EconomyStage.OnRoad(EconomyState.Year,.55f,EconomyStyle.FramingRho);
            section.Pitch=6;section.Distance=6.2f;section.PortraitWidth=4.9f;
            return list;
            void Add(string id, string title, string subtitle, KeyCode key, Vector3 target, float pitch, float distance)
            {
                if (id == "foundation")
                {
                    list.Add(new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key, FixedTarget = f.World(target), FixedYaw = f.Yaw + 18,
                        Pitch = pitch, Distance = distance, PortraitWidth = 165, YaOld = DeepTime.NowYear - EconomyStyle.FirstYear, YaNew = 0,
                        LogOffset = EconomyStyle.WindowLogOffset, Length = EconomyStyle.WindowLength, RhoScale = EconomyStyle.RhoScale, YScale = EconomyStyle.YScale });
                    return;
                }
                list.Add(new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key,
                    FixedTarget = f.World(id == "mind" || id == "section" || id == "people" ? target : new Vector3(0,32,6)), FixedYaw = f.Yaw + (id == "mind" || id == "section" ? 0 : 24), Pitch = id == "mind" || id == "section" || id == "people" ? pitch : 28, Distance = id == "mind" || id == "section" || id == "people" ? distance : 235,
                    PortraitWidth = id == "mind" ? 12 : 165, YaOld = DeepTime.NowYear - EconomyStyle.FirstYear,
                    YaNew = 0, LogOffset = EconomyStyle.WindowLogOffset, Length = EconomyStyle.WindowLength,
                    RhoScale = EconomyStyle.RhoScale, YScale = EconomyStyle.YScale });
            }
        }
    }
}
