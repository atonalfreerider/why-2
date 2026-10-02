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
            Add("landscape", "Where value is created", "Hill footprint = value added · lowlands = labor · gold summit = capital claims", KeyCode.Alpha3, new Vector3(0, 6, 5), 30, 72);
            Add("capture", "Owners above the industries", "Named voting blocs sit in crowns; diversified wealth floats between hills", KeyCode.Alpha4, new Vector3(2, 15, 13), 16, 58);
            Add("people", "Households in the landscape", "Workers near their employers · business owners on slopes · dependents beneath parents", KeyCode.Alpha5, new Vector3(4, 2, -8), 18, 36);
            Add("rivers", "The money circuit", "Gold investment climbs · wages flow to households · spending returns to industries", KeyCode.Alpha6, new Vector3(0, 5, 5), 48, 73);
            Add("mind", "Inside the decision program", "Select a player · memory → desire / fear → deliberation → spending → next balance sheet", KeyCode.Alpha7, new Vector3(145, 3, 0), 20, 15);
            Add("society", "Repeated encounters", "Tit for tat · blue ties cooperate · red ties defect · use the season controls", KeyCode.Alpha8, new Vector3(0, 5, 5), 46, 74);
            Add("roots", "Industries depend on each other", "Input-output purchases connect suppliers to buyers; widths follow the stored use table", KeyCode.None, new Vector3(0, 5, 5), 48, 74);
            Add("betrayal", "One betrayal", "Follow retaliation and forgiveness over repeated encounters", KeyCode.None, new Vector3(0, 5, 5), 46, 74);
            Add("y1972", "The landscape in 1972", "Historical accounts on the same terrain; modern company crowns are omitted", KeyCode.None, new Vector3(0, 6, 5), 30, 72);
            var section=list[1];section.FixedTarget=EconomyStage.OnRoad(EconomyState.Year,.55f,EconomyStyle.FramingRho);
            section.Pitch=6;section.Distance=6.2f;section.PortraitWidth=4.9f;
            return list;
            void Add(string id, string title, string subtitle, KeyCode key, Vector3 target, float pitch, float distance)
            {
                list.Add(new ViewPreset { Id = id, Title = title, Subtitle = subtitle, Key = key,
                    FixedTarget = f.World(id == "mind" || id == "section" || id == "people" ? target : new Vector3(0,32,6)), FixedYaw = f.Yaw + (id == "mind" || id == "section" ? 0 : 24), Pitch = id == "mind" || id == "section" || id == "people" ? pitch : 28, Distance = id == "mind" || id == "section" || id == "people" ? distance : 235,
                    PortraitWidth = id == "mind" ? 12 : 165, YaOld = DeepTime.NowYear - EconomyStyle.FirstYear,
                    YaNew = 0, LogOffset = EconomyStyle.WindowLogOffset, Length = EconomyStyle.WindowLength,
                    RhoScale = EconomyStyle.RhoScale, YScale = EconomyStyle.YScale });
            }
        }
    }
}
