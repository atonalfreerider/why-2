using System.Collections.Generic;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// A named way of looking at the graph: which period is unrolled and how, plus a camera pose.
    /// </summary>
    public sealed class ViewPreset
    {
        public string Id;
        public string Title;
        public string Subtitle;
        public KeyCode Key;

        /// <summary>Window in years ago (old to new). Ignored for polar presets except for framing.</summary>
        public double YaOld, YaNew;

        public bool Polar;
        public double LogOffset = 1e3;
        public float Length = 10;
        public float RhoScale = 1;
        public float YScale = 1;

        // camera framing
        public float TargetRho = 0.5f;
        public float TargetY = GraphStyle.LifeY;
        public float Pitch = 50;
        public float Distance = 8;
        public float YawOffset;

        /// <summary>For polar presets: the arc the camera looks at (0.5 = 12 o'clock); &lt;0 = whole clock.</summary>
        public float PolarArc = -1;

        public WarpState Warp()
        {
            if (Polar)
            {
                WarpState s = WarpState.Polar;
                s.FocusArc = PolarArc >= 0 ? PolarArc : 0.5f;
                s.RhoScale = RhoScale;
                s.YScale = YScale;
                return s;
            }

            return WarpState.Window(YaOld, YaNew, LogOffset, Length, 1, RhoScale, YScale);
        }

        /// <summary>Camera pose for this preset evaluated against its final warp.</summary>
        public CameraPose Pose()
        {
            WarpState w = Warp();
            if (Polar && PolarArc < 0)
            {
                // the whole graph (clock + human branch), seen from the south so the clock reads like a clock face
                return new CameraPose
                {
                    Target = new Vector3(0.6f, TargetY, -0.9f),
                    Yaw = 0 + YawOffset,
                    Pitch = Pitch,
                    Distance = Distance
                };
            }

            float u = Polar ? PolarArc : DeepTime.Arc(w.FocusYearsAgo);
            Vector3 target = GraphWarp.ToWorld(u, TargetY, TargetRho, w);
            // look outward from inside the path: past on the left, present on the right
            Vector3 n = GraphWarp.NormalAt(u, w);
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + YawOffset;
            return new CameraPose { Target = target, Yaw = yaw, Pitch = Pitch, Distance = Distance };
        }
    }

    public struct CameraPose
    {
        public Vector3 Target;
        public float Yaw;      // degrees, 0 = looking along +z
        public float Pitch;    // degrees down from horizontal
        public float Distance;
    }

    /// <summary>The preset catalog. Ids are referenced by the director's tour.json.</summary>
    public static class ViewPresets
    {
        static List<ViewPreset> all;

        public static IReadOnlyList<ViewPreset> All => all ??= Build();

        public static ViewPreset Get(string id)
        {
            foreach (ViewPreset p in All)
            {
                if (p.Id == id) return p;
            }

            return All[0];
        }

        static double Ya(double calendarYear) => DeepTime.NowYear - calendarYear;

        static List<ViewPreset> Build()
        {
            return new List<ViewPreset>
            {
                new ViewPreset
                {
                    Id = "overview", Title = "Everything", Subtitle = "From the Big Bang to this moment",
                    Key = KeyCode.Alpha1, Polar = true, Pitch = 64, Distance = 15.5f, TargetY = GraphStyle.LifeY
                },
                new ViewPreset
                {
                    Id = "cosmos", Title = "Cosmos", Subtitle = "Big Bang to the Sun",
                    Key = KeyCode.Alpha2, YaOld = 13.8e9, YaNew = 4.5e9, LogOffset = 2e8, Length = 12,
                    TargetRho = 1.2f, TargetY = GraphStyle.MatterY, Pitch = 55, Distance = 10
                },
                new ViewPreset
                {
                    Id = "stars", Title = "Stars", Subtitle = "Galaxy, solar nebula, Sun",
                    YaOld = 5.2e9, YaNew = 4.4e9, LogOffset = 1e7, Length = 10,
                    TargetRho = 0.8f, TargetY = GraphStyle.MatterY, Pitch = 55, Distance = 8
                },
                new ViewPreset
                {
                    Id = "earth", Title = "Early Earth", Subtitle = "4.6 to 3.5 billion years ago",
                    YaOld = 4.65e9, YaNew = 3.4e9, LogOffset = 1e7, Length = 10,
                    TargetRho = 0.5f, TargetY = GraphStyle.MatterY, Pitch = 55, Distance = 7
                },
                new ViewPreset
                {
                    Id = "life", Title = "Life", Subtitle = "4.2 billion years of evolution",
                    Key = KeyCode.Alpha3, Polar = true, PolarArc = 0.47f, TargetRho = 1.2f,
                    TargetY = GraphStyle.LifeY, Pitch = 55, Distance = 7.5f
                },
                new ViewPreset
                {
                    Id = "complex_life", Title = "Complex life", Subtitle = "The last billion years",
                    YaOld = 1.0e9, YaNew = 2e3, LogOffset = 3e6, Length = 14,
                    TargetRho = 1.2f, TargetY = GraphStyle.LifeY, Pitch = 58, Distance = 11
                },
                new ViewPreset
                {
                    Id = "mammals", Title = "Mammals", Subtitle = "The last 250 million years",
                    YaOld = 2.5e8, YaNew = 2e3, LogOffset = 1e6, Length = 14,
                    TargetRho = 0.8f, TargetY = GraphStyle.LifeY, Pitch = 58, Distance = 10
                },
                new ViewPreset
                {
                    Id = "hominins", Title = "Hominins", Subtitle = "The last 10 million years",
                    Key = KeyCode.Alpha4, YaOld = 1.0e7, YaNew = 1e3, LogOffset = 5e4, Length = 12,
                    TargetRho = 0.3f, TargetY = GraphStyle.LifeY, Pitch = 55, Distance = 7
                },
                new ViewPreset
                {
                    Id = "prehistory", Title = "Prehistory", Subtitle = "300,000 years of Homo sapiens",
                    YaOld = 3.2e5, YaNew = Ya(-2000), LogOffset = 8e3, Length = 12,
                    TargetRho = 0.3f, TargetY = GraphStyle.HumansY, Pitch = 55, Distance = 7
                },
                new ViewPreset
                {
                    // the human branch is already a straight, near-linear timeline: just look along it
                    Id = "civilizations", Title = "Civilizations", Subtitle = "Relative power, 3000 BCE to now",
                    Key = KeyCode.Alpha5, Polar = true, PolarArc = DeepTime.Arc(Ya(-300)), YaOld = Ya(-3000), YaNew = 0,
                    TargetRho = 1.1f, TargetY = GraphStyle.HumansY, Pitch = 60, Distance = 9
                },
                new ViewPreset
                {
                    Id = "modern", Title = "Modern era", Subtitle = "1776 to now",
                    Key = KeyCode.Alpha6, YaOld = Ya(1770), YaNew = 0, LogOffset = 400, Length = 14,
                    TargetRho = 1.3f, TargetY = GraphStyle.HumansY, Pitch = 58, Distance = 10
                },
                new ViewPreset
                {
                    Id = "smv", Title = "United States 1950 - now",
                    Subtitle = "Gender-separated lifelines rising and falling with social market value",
                    Key = KeyCode.Alpha7, YaOld = Ya(1948), YaNew = 0, LogOffset = 600, Length = 14,
                    RhoScale = 2.5f, YScale = 1.6f,
                    TargetRho = 1.5f, TargetY = GraphStyle.HumansY, Pitch = 38, Distance = 8
                },
                new ViewPreset
                {
                    Id = "present", Title = "The present moment", Subtitle = "Where every line arrives",
                    Key = KeyCode.Alpha8, Polar = true, PolarArc = DeepTime.NowArc, TargetRho = 0.9f,
                    TargetY = GraphStyle.HumansY, Pitch = 42, Distance = 3.2f
                },
            };
        }
    }
}
