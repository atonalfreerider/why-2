using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy
{
    /// <summary>
    /// The economy scene's views (number keys 1 - 8, the HUD's preset bar, the tour). Every view shares one lens
    /// (<see cref="EconomyStage.TimelineWarp"/>), so the road from 1946 to now never moves: the timeline views
    /// look at it from different places, the station views (<see cref="ViewPreset.FixedTarget"/>) at the
    /// diagrams that stand beyond its present end.
    /// </summary>
    public static class EconomyPresets
    {
        static double Ya(double calendarYear) => DeepTime.NowYear - calendarYear;

        /// <summary>
        /// The people view: close to the population from the mid 1990s to now (the camera on mid-2011, a little above the
        /// lifelines' base, looking down 36 degrees and turned slightly toward the past), near enough that the gold lines of
        /// the people in control read as lines at 1920x1080 while the bundle fills the middle of the screen and the wall's
        /// top floors, where the money threads rise from, the bottom quarter. The present end of the road stays in the
        /// frame with the wall's tier labels beside it (at distance 3.9 and yaw -6 the bundle ran off the right edge
        /// around 2024 and the tier labels were cut). On a portrait screen it steps back until
        /// <see cref="PeoplePortraitWidth"/> world units (about 25 years, 1999 - 2024) fit the width; the lens stays the
        /// shared one (a narrowed lens would move the road from under the fixed target).
        /// </summary>
        const double PeopleYear = 2011.5;

        const float PeopleHeight = 0.04f, PeoplePitch = 36f, PeopleDistance = 4.8f, PeopleYaw = -3f, PeoplePortraitWidth = 5f;

        public static List<ViewPreset> Build()
        {
            Station circuit = EconomyStage.Get(EconomyStage.Circuit);
            Station mind = EconomyStage.Get(EconomyStage.Mind);
            Station games = EconomyStage.Get(EconomyStage.Games);
            float h = EconomyStyle.StationHeight, w = EconomyStyle.StationWidth;

            return new List<ViewPreset>
            {
                Timeline(new ViewPreset
                {
                    Id = "overview", Title = "The economy, 1946 - 2026",
                    Subtitle = "People above, the industries that pay them below, money flowing between",
                    Key = KeyCode.Alpha1, TargetY = 0.5f, Pitch = 26, Distance = 13.5f, YawOffset = -6,
                    PortraitWidth = 12f
                }),
                OnRoad(new ViewPreset
                {
                    Id = "industries", Title = "Where value is created",
                    Subtitle = "Value added by industry (2025 dollars): the bright part of each band is what owners keep",
                    Key = KeyCode.Alpha2, Pitch = 8, Distance = 9.5f, PortraitWidth = 9f
                }, 2001, 0.36f, -6),
                Station(circuit, new ViewPreset
                {
                    Id = "circuit", Title = "The money circuit",
                    Subtitle = "Industries pay workers, owners and the state; households spend it back into industries",
                    Key = KeyCode.Alpha3, Pitch = 8, Distance = 11.5f, PortraitWidth = w * 1.05f
                }, new Vector3(0, h * 0.5f, 0)),
                Station(circuit, new ViewPreset
                {
                    Id = "capture", Title = "Where value is captured",
                    Subtitle = "Profits, payouts and who receives them; the companies that keep the most",
                    Key = KeyCode.Alpha4, Pitch = 14, Distance = 7f, PortraitWidth = w * 0.6f
                }, new Vector3(-w * 0.22f, h * 0.5f, 0)),
                OnRoad(new ViewPreset
                {
                    Id = "people", Title = "Who owns their path",
                    Subtitle = "Each line many people; gold lines own capital and steer their own lives",
                    Key = KeyCode.Alpha5, Pitch = PeoplePitch, Distance = PeopleDistance, PopulationDetail = true,
                    PortraitWidth = PeoplePortraitWidth
                }, PeopleYear, GraphStyle.HumansY + PeopleHeight, PeopleYaw),
                Station(mind, new ViewPreset
                {
                    Id = "mind", Title = "Desire and fear",
                    Subtitle = "Toward what we want, away from what we fear: where the money of each mind goes",
                    Key = KeyCode.Alpha6, Pitch = 18, Distance = 12f, PortraitWidth = w * 1.05f
                }, new Vector3(0, h * 0.5f, w * 0.15f)),
                Station(games, new ViewPreset
                {
                    Id = "games", Title = "Cooperation",
                    Subtitle = "The prisoner's dilemma: one round defects, the shadow of the future climbs",
                    Key = KeyCode.Alpha7, Pitch = 10, Distance = 10f, PortraitWidth = w * 0.7f
                }, new Vector3(-w * 0.22f, h * 0.5f, 0)),
                Station(games, new ViewPreset
                {
                    Id = "tribes", Title = "Tribes",
                    Subtitle = "Groups repay in kind: tit for tat between tribes, and what forgiveness does",
                    Key = KeyCode.Alpha8, Pitch = 10, Distance = 9f, PortraitWidth = w * 0.6f
                }, new Vector3(w * 0.25f, h * 0.5f, 0)),
            };
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

        /// <summary>
        /// A view of one stretch of the road: the shared lens, the camera looking outward at a calendar year and height
        /// (data y) of the population's center line, turned by <paramref name="yawOffset"/> degrees.
        /// </summary>
        static ViewPreset OnRoad(ViewPreset p, double year, float y, float yawOffset)
        {
            Timeline(p);
            WarpState w = EconomyStage.TimelineWarp();
            float u = EconomyStage.U(year);
            Vector3 n = GraphWarp.NormalAt(u, w);
            p.FixedTarget = GraphWarp.ToWorld(u, y, EconomyStyle.FramingRho, w);
            p.FixedYaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + yawOffset;
            return p;
        }

        /// <summary>A view of a station: the shared lens (the road stays put), the camera on the station.</summary>
        static ViewPreset Station(Station s, ViewPreset p, Vector3 localTarget)
        {
            Timeline(p);
            p.YaOld = Ya(EconomyStyle.FirstYear);
            p.PopulationDetail = false;
            p.FixedTarget = s.World(localTarget);
            p.FixedYaw = s.Yaw;
            return p;
        }
    }
}
