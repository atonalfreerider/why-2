using System;
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
    /// emphasis) is its <see cref="ViewSpec"/> in <see cref="EconomyViews"/>.
    ///
    /// The poses are tuned against the harness at 16:9 and 9:16: every land view frames the whole bowl or its subject
    /// with the labels it shows inside the frame. On a portrait screen a land view steps back until its
    /// <see cref="ViewPreset.PortraitWidth"/> fits across and aims <see cref="PortraitAim"/> of the visible height lower,
    /// so the bowl sits in the upper part of the tall frame, clear of the bottom sheet (7.2). The catalog reads the
    /// screen's orientation, the selected year (the section's title and pose) and the land on screen (generated
    /// subtitles), so the land's view layer invalidates it when any of these changes.
    /// </summary>
    public static class EconomyPresets
    {
        static double Ya(double calendarYear) => DeepTime.NowYear - calendarYear;

        /// <summary>
        /// Data height and rho of the section view's target: the middle of the cut's frame (the ground to above the
        /// lifelines, the wall's plane to the outer lifelines).
        /// </summary>
        const float SectionY = 0.55f, SectionRho = EconomyStyle.FramingRho;

        /// <summary>On a portrait screen a land view aims this share of the visible height lower (the bowl rises into the upper 60%).</summary>
        const float PortraitAim = 0.2f;

        /// <summary>The overview's camera: turned from the road's normal toward the future (degrees), pitch, distance.</summary>
        const float OverviewYaw = -15f, OverviewPitch = 26f, OverviewDistance = 28f, OverviewPortraitPitch = 58f;

        /// <summary>The overview's target: this far from the 1950 road point toward the far rim, raised this much (world).</summary>
        const float OverviewToBowl = 0.6f, OverviewLift = 1.2f;

        /// <summary>The portrait overview: its width across (world) and its target's place from 1950 toward the far rim.</summary>
        const float OverviewPortraitWidth = 21f, OverviewPortraitToBowl = 0.42f;

        /// <summary>
        /// The roots view looks from the side of the bowl away from the road (the cut and the road would stand in front of
        /// a low camera on the road's side), low enough to see under the terraces; on a portrait screen higher, so the
        /// sectors' labels have rows to stand in.
        /// </summary>
        const float RootsYaw = -78f, RootsPortraitPitch = 30f;

        /// <summary>
        /// The low views (capture, mind) on a portrait screen: stepped back to fit the narrow width, a camera this low would
        /// see the cut on the road in front of the bowl; from this pitch the cut stays under it.
        /// </summary>
        const float RaisedPortraitPitch = 30f;

        /// <summary>
        /// The capture view on a portrait screen looks straight along the road from higher up (the road then stands under
        /// the bowl, not beside the towers) and wider, so the sectors' labels around the towers have room.
        /// </summary>
        const float CapturePortraitPitch = 35f;

        public static List<ViewPreset> Build()
        {
            LandFrame land = EconomyStage.Land();
            int year = EconomyState.CutYear;
            string y = year.ToString(CultureInfo.InvariantCulture);
            WarpState w = EconomyStage.TimelineWarp();
            bool portrait = ScreenLayout.IsPortrait;

            // the overview: from the road's outer side, the road from 1950 to the bowl's far rim between the camera's
            // edges; on a portrait screen it looks down along the road from above the past, the bowl at the top
            Vector3 from1950 = EconomyStage.OnRoad(1950, 0.3f, EconomyStyle.FramingRho);
            Vector3 farRim = land.World(LandFrame.Polar(LandStyle.RimR, 90f, LandStyle.RimY));
            Vector3 overviewTarget = Vector3.Lerp(from1950, farRim, OverviewToBowl) + Vector3.up * OverviewLift;
            Vector3 n = GraphWarp.NormalAt(EconomyStage.U(1990), w);
            float overviewYaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + OverviewYaw;
            ViewPreset overview = new ViewPreset
            {
                Id = "overview", Title = "The economy, 1946 - 2026",
                Subtitle = "The road of time; one year cut out and opened into a land",
                Key = KeyCode.Alpha1, Pitch = OverviewPitch, Distance = OverviewDistance, PortraitWidth = 26
            };
            if (portrait)
            {
                overview.Pitch = OverviewPortraitPitch;
                overview.PortraitWidth = OverviewPortraitWidth;
                overviewYaw = land.Yaw;
                overviewTarget = Vector3.Lerp(from1950, farRim, OverviewPortraitToBowl);
            }

            return new List<ViewPreset>
            {
                Fixed(overview, overviewTarget, overviewYaw),
                Pose(Fixed(new ViewPreset
                {
                    Id = "section", Title = "The cut through " + y,
                    Subtitle = "Every industry's band and every life that pierces " + y,
                    Key = KeyCode.Alpha2, Pitch = 6, Distance = 6.2f, PortraitWidth = 4f
                }, EconomyStage.OnRoad(year + 0.5, SectionY, SectionRho), land.Yaw)),
                OnLand(land, new ViewPreset
                {
                    Id = "landscape", Title = "Where value is created",
                    Subtitle = "Each industry a sector of its stratum, as large as the value it adds; gold is what owners keep",
                    Key = KeyCode.Alpha3, Pitch = 40, Distance = 17.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 0.7f, 0.5f), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "capture", Title = "Where value is captured",
                    Subtitle = "Profit rises into the crown; the companies that capture the most",
                    Key = KeyCode.Alpha4, Pitch = portrait ? CapturePortraitPitch : 20, Distance = 12, PortraitWidth = 13
                }, portrait ? new Vector3(0, 1.4f, 1.0f) : new Vector3(0, 1.6f, 2.0f), portrait ? 0 : -12),
                OnLand(land, new ViewPreset
                {
                    Id = "people", Title = "Who stands where", Subtitle = PeopleSubtitle(),
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
                    Key = KeyCode.Alpha7, Pitch = portrait ? RaisedPortraitPitch : 18, Distance = 13, PortraitWidth = 13
                }, new Vector3(0, 2.0f, -0.6f), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "society", Title = "How people organize",
                    Subtitle = "Tit for tat between groups: who cooperates, how forgiveness helps, how coalitions form",
                    Key = KeyCode.Alpha8, Pitch = 64, Distance = 17f, PortraitWidth = 13.5f
                }, new Vector3(0, 1.8f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "roots", Title = "What each industry stands on",
                    Subtitle = "Purchases between industries rise into each buyer from below",
                    Pitch = portrait ? RootsPortraitPitch : 12, Distance = 12.5f, PortraitWidth = 13
                }, new Vector3(0, 1.0f, 0), RootsYaw),
                OnLand(land, new ViewPreset
                {
                    Id = "betrayal", Title = "One betrayal",
                    Subtitle = "Tit for tat echoes it; forgiveness ends it",
                    Pitch = 64, Distance = 17f, PortraitWidth = 13.5f
                }, new Vector3(0, 1.8f, 0), 0),
                OnLand(land, new ViewPreset
                {
                    Id = "y1972", Title = "The land in 1972",
                    Subtitle = "The same cut, fifty-three years earlier",
                    Pitch = 40, Distance = 17.5f, PortraitWidth = 13.5f
                }, new Vector3(0, 0.7f, 0.5f), 0),
            };
        }

        /// <summary>
        /// The people view's subtitle from the land on screen: its number of players and their median size in people
        /// (adults and children) rounded to millions ("116 players, about 2 million people each, ...").
        /// </summary>
        static string PeopleSubtitle()
        {
            const string Tail = " standing where their money comes from";
            Player[] players = LandService.Current?.Players?.Players;
            if (players == null || players.Length == 0) return "The players of the year," + Tail;
            double[] people = new double[players.Length];
            for (int k = 0; k < players.Length; k++) people[k] = players[k].People;
            Array.Sort(people);
            double median = people.Length % 2 == 1
                ? people[people.Length / 2]
                : 0.5 * (people[people.Length / 2 - 1] + people[people.Length / 2]);
            long millions = Math.Max(1, (long)Math.Round(median / 1e6));
            return players.Length.ToString(CultureInfo.InvariantCulture) + " players, about " +
                   millions.ToString(CultureInfo.InvariantCulture) + " million people each," + Tail;
        }

        /// <summary>A view of the land: the shared lens, the camera on a land-local target, turned from the land's yaw.</summary>
        static ViewPreset OnLand(LandFrame land, ViewPreset p, Vector3 localTarget, float yawOffset)
        {
            Fixed(p, land.World(localTarget), land.Yaw + yawOffset);
            return Pose(p);
        }

        /// <summary>
        /// On a portrait screen: the target moves down the camera's up axis by <see cref="PortraitAim"/> of the height the
        /// view shows at its portrait distance, so its subject sits in the upper part of the tall frame.
        /// </summary>
        static ViewPreset Pose(ViewPreset p)
        {
            if (!ScreenLayout.IsPortrait || !p.FixedTarget.HasValue) return p;
            float tan = Mathf.Tan(CameraRig.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Clamp(ScreenLayout.Aspect, 0.4f, 1f);
            float distance = Mathf.Max(p.Distance, p.PortraitWidth / (2f * tan * aspect));
            Vector3 up = Quaternion.Euler(p.Pitch, p.FixedYaw, 0) * Vector3.up;
            p.FixedTarget = p.FixedTarget.Value - up * (PortraitAim * 2f * distance * tan);
            return p;
        }

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
