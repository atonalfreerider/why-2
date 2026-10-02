using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Why.Economy.Land;

namespace Why.Economy
{
    /// <summary>
    /// The economy scene's views (number keys 1 - 8, the HUD's preset bar, the tour; SPEC 7.2): the road (overview), the
    /// cut through the selected year (section), and nine views of the land, the year opened into a stepped bowl on the
    /// plaza past the road's end. Every view shares one lens (<see cref="EconomyStage.TimelineWarp"/>), so the road from
    /// 1946 to now never moves: the land views (<see cref="ViewPreset.FixedTarget"/>) are poses in the land's frame
    /// (<see cref="EconomyStage.Land"/>: a land-local target, a yaw offset from the frame's, a pitch and a distance).
    /// What each view asks of the land beside its pose (the transition, a temporary year, the season's round, the
    /// emphasis) is its <see cref="ViewSpec"/> in <see cref="EconomyViews"/>. These are the starting poses; the land's
    /// final views are tuned against the harness.
    /// </summary>
    public static class EconomyPresets
    {
        static double Ya(double calendarYear) => DeepTime.NowYear - calendarYear;

        /// <summary>
        /// Data height and rho of the section view's target: the middle of the cut's frame (the ground to above the
        /// lifelines, the wall's plane to the outer lifelines).
        /// </summary>
        const float SectionY = 0.55f, SectionRho = EconomyStyle.FramingRho;

        public static List<ViewPreset> Build()
        {
            LandFrame land = EconomyStage.Land();
            int year = EconomyState.Year;
            WarpState w = EconomyStage.TimelineWarp();
            float u1990 = EconomyStage.U(1990);
            Vector3 n = GraphWarp.NormalAt(u1990, w);
            Vector3 overviewTarget = 0.5f * (EconomyStage.OnRoad(1990, 0.3f, EconomyStyle.FramingRho) + land.World(0, 1, 0));

            return new List<ViewPreset>
            {
                Fixed(new ViewPreset
                {
                    Id = "overview", Title = "The economy, 1946 - 2026",
                    Subtitle = "The road of time; one year cut out and opened into a land",
                    Key = KeyCode.Alpha1, Pitch = 28, Distance = 30, PortraitWidth = 26
                }, overviewTarget, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + 20),
                Fixed(new ViewPreset
                {
                    Id = "section", Title = "The cut through " + year.ToString(CultureInfo.InvariantCulture),
                    Subtitle = "Every industry's band and every life that pierces " + year.ToString(CultureInfo.InvariantCulture),
                    Key = KeyCode.Alpha2, Pitch = 6, Distance = 5.5f, PortraitWidth = 4.5f
                }, EconomyStage.OnRoad(year + 0.5, SectionY, SectionRho), land.Yaw),
                OnLand(land, new ViewPreset
                {
                    Id = "landscape", Title = "Where value is created",
                    Subtitle = "Each industry a sector of its stratum, as large as the value it adds; gold is what owners keep",
                    Key = KeyCode.Alpha3, Pitch = 40, Distance = 16.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 0.7f, 0.5f), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "capture", Title = "Where value is captured",
                    Subtitle = "Profit rises into the crown; the companies that capture the most",
                    Key = KeyCode.Alpha4, Pitch = 20, Distance = 12, PortraitWidth = 10
                }, new Vector3(0, 1.6f, 2.0f), -12),
                OnLand(land, new ViewPreset
                {
                    Id = "people", Title = "Who stands where",
                    Subtitle = "119 players, about 2 million people each, standing where their money comes from",
                    Key = KeyCode.Alpha5, Pitch = 46, Distance = 12, PortraitWidth = 13
                }, new Vector3(0, 1.6f, -1.5f), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "rivers", Title = "Where the money goes",
                    Subtitle = "Spending runs down into the industries that are paid; ice is fear, rose is desire, glitter is fantasy",
                    Key = KeyCode.Alpha6, Pitch = 58, Distance = 15, PortraitWidth = 13.5f
                }, new Vector3(0, 1.0f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "mind", Title = "Desire, fear and fantasy",
                    Subtitle = "Many buy fantasy; few control their path",
                    Key = KeyCode.Alpha7, Pitch = 18, Distance = 13, PortraitWidth = 13
                }, new Vector3(0, 2.0f, -0.6f), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "society", Title = "How people organize",
                    Subtitle = "Tit for tat between groups: who cooperates, how forgiveness helps, how coalitions form",
                    Key = KeyCode.Alpha8, Pitch = 64, Distance = 15.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 1.8f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "roots", Title = "What each industry stands on",
                    Subtitle = "Purchases between industries rise into each buyer from below",
                    Pitch = 10, Distance = 14, PortraitWidth = 14
                }, new Vector3(0, 0.5f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "betrayal", Title = "One betrayal",
                    Subtitle = "Tit for tat echoes it; forgiveness ends it",
                    Pitch = 64, Distance = 15.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 1.8f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "y1972", Title = "The land in 1972",
                    Subtitle = "The same cut, fifty-three years earlier",
                    Pitch = 40, Distance = 16.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 0.7f, 0.5f), 0),
            };
        }

        /// <summary>A view of the land: the shared lens, the camera on a land-local target, turned from the land's yaw.</summary>
        static ViewPreset OnLand(LandFrame land, ViewPreset p, Vector3 localTarget, float yawOffset) =>
            Fixed(p, land.World(localTarget), land.Yaw + yawOffset);

        /// <summary>A view of a world point: the shared lens (the road stays put), the camera at a fixed yaw.</summary>
        static ViewPreset Fixed(ViewPreset p, Vector3 target, float yaw)
        {
            Timeline(p);
            p.PopulationDetail = false;
            p.FixedTarget = target;
            p.FixedYaw = yaw;
            return p;
        }

        /// <summary>A view of the road: the shared lens, the camera looking outward at the population.</summary>
        static ViewPreset Timeline(ViewPreset p)
        {
            if (p.YaOld <= 0) p.YaOld = Ya(EconomyStyle.FirstYear);
            p.YaNew = 0;
            p.LogOffset = EconomyStyle.WindowLogOffset;
            p.Length = EconomyStyle.WindowLength;
            p.RhoScale = EconomyStyle.RhoScale;
            p.YScale = EconomyStyle.YScale;
            p.TargetRho = EconomyStyle.FramingRho;
            p.StrataEmphasis = 0;
            return p;
        }
    }
}
