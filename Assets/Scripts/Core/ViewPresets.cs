using System.Collections.Generic;
using UnityEngine;

namespace Why
{
    /// <summary>How an unrolled preset frames its subject on a portrait (9:16) screen.</summary>
    public enum PortraitFraming
    {
        /// <summary>
        /// The camera turns 90 degrees to look along the timeline into the past: the past recedes to the top
        /// of the screen, the present is near at the bottom, and relevance (rho) spreads across. For views of a
        /// stretch of time with things splitting off outward, which is most of them.
        /// </summary>
        Turn,

        /// <summary>
        /// The landscape orientation stays and the time window is compressed to the narrow width (heights get
        /// a little more room): for views whose message is height - the strata of domination, lifelines
        /// rising and falling - which would collapse if the timeline ran down the screen.
        /// </summary>
        Narrow
    }

    /// <summary>
    /// A named way of looking at the graph: which period is unrolled and how, plus a camera pose. On a
    /// portrait screen (<see cref="ScreenLayout.IsPortrait"/>) the same preset frames its subject for the tall
    /// frame (see <see cref="PortraitFraming"/>); landscape framing is exactly the authored one.
    /// </summary>
    public sealed class ViewPreset
    {
        /// <summary>Aspect (width / height) the landscape framings were authored for.</summary>
        const float LandscapeAspect = 16f / 9f;

        /// <summary>
        /// Portrait: the screen band (normalized device y, -1 bottom .. 1 top) a turned timeline (or a polar arc's
        /// band of relevance) is fitted into, clear of the HUD on the 500x889 portrait canvas: the title block with
        /// a three-line subtitle ends at 0.76, the preset bar (four rows) and the legend reach up to -0.51 (the
        /// lifeline readout, while shown, to -0.42 at the left).
        /// </summary>
        const float PortraitTop = 0.76f, PortraitBottom = -0.48f;

        /// <summary>
        /// Portrait pitch range of turned views: steep enough that perspective does not crush the far (older)
        /// end of the timeline (at 50 degrees the far end still shows at ~0.6x the scale of the near end).
        /// </summary>
        const float TurnMinPitch = 50, TurnMaxPitch = 60;

        /// <summary>Narrow views step back this much from their landscape distance.</summary>
        const float NarrowPullback = 1.25f;

        /// <summary>Share of the screen width a narrowed time window fills.</summary>
        const float NarrowFill = 0.92f;

        /// <summary>Narrow views stretch heights this much: the tall frame has room for them.</summary>
        const float NarrowHeight = 1.5f;

        /// <summary>
        /// Turned views: the inner edge of the frame's narrowest (nearest) row stays this many world units inside
        /// the base path, room for the time axis and its labels; the rest of the width shows relevance.
        /// </summary>
        const float PortraitInnerMargin = 0.4f;

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

        /// <summary>
        /// Emphasis (0..1) of the strata beneath the civilizations (livestock and crops, mineral extraction).
        /// Human-focused views keep them faint unless the director is pointing at them.
        /// </summary>
        public float StrataEmphasis = 1;

        /// <summary>
        /// Emphasis (0..1) of the matter (red) layer. Life-focused views dim it so the tree of life reads
        /// against it; whenever the director points at matter it comes back to full strength.
        /// </summary>
        public float MatterEmphasis = 1;

        // camera framing
        public float TargetRho = 0.5f;
        public float TargetY = GraphStyle.LifeY;
        public float Pitch = 50;
        public float Distance = 8;
        public float YawOffset;

        /// <summary>For polar presets: the arc the camera looks at (0.5 = 12 o'clock); &lt;0 = whole clock.</summary>
        public float PolarArc = -1;

        /// <summary>How an unrolled preset frames its subject on a portrait screen.</summary>
        public PortraitFraming Portrait = PortraitFraming.Turn;

        /// <summary>
        /// Polar presets on a portrait screen keep their orientation and step back until this many world units
        /// across the target fit the narrow width (unless <see cref="PortraitRho"/> frames them).
        /// </summary>
        public float PortraitWidth = 10;

        /// <summary>
        /// Polar presets that look at one arc of the clock, on a portrait screen: the band of relevance (data rho,
        /// x = inner .. y = outer) fitted between the title block and the bottom bar, so it fills the tall frame
        /// instead of shrinking to fit <see cref="PortraitWidth"/> across. Unused while y &lt;= x.
        /// </summary>
        public Vector2 PortraitRho;

        /// <summary>Pitch on a portrait screen for polar presets (0 = <see cref="Pitch"/>).</summary>
        public float PortraitPitch;

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

            if (ScreenLayout.IsPortrait && Portrait == PortraitFraming.Narrow)
            {
                // the stretch of time the landscape view shows, squeezed into the portrait width
                float narrow = Length * Mathf.Min(1f, NarrowExtent() / LandscapeExtent());
                return WarpState.Window(YaOld, YaNew, LogOffset, narrow, 1, RhoScale, YScale * NarrowHeight);
            }

            return WarpState.Window(YaOld, YaNew, LogOffset, Length, 1, RhoScale, YScale);
        }

        /// <summary>Camera pose for this preset evaluated against its final warp.</summary>
        public CameraPose Pose()
        {
            WarpState w = Warp();
            bool portrait = ScreenLayout.IsPortrait;
            if (Polar && PolarArc < 0)
            {
                // the whole graph (clock + human branch), seen from the south so the clock reads like a clock face;
                // on a portrait screen the branch hangs down toward the viewer, so only the width needs room
                return new CameraPose
                {
                    Target = new Vector3(1.6f, TargetY, -2.4f),
                    Yaw = 0 + YawOffset,
                    Pitch = Pitch,
                    Distance = portrait ? PolarPortraitDistance() : Distance
                };
            }

            float u = Polar ? PolarArc : DeepTime.Arc(w.FocusYearsAgo);
            Vector3 target = GraphWarp.ToWorld(u, TargetY, TargetRho, w);
            // look outward from inside the path: past on the left, present on the right
            Vector3 n = GraphWarp.NormalAt(u, w);
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + YawOffset;
            CameraPose pose = new CameraPose { Target = target, Yaw = yaw, Pitch = Pitch, Distance = Distance };
            if (!portrait) return pose;

            if (Polar) PolarArcPortrait(ref pose);
            else if (Portrait == PortraitFraming.Narrow) pose.Distance = Distance * NarrowPullback;
            else Turn(ref pose);
            return pose;
        }

        static float TanHalfFov => Mathf.Tan(CameraRig.FieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>Portrait aspect used for framing (clamped: a near-square window still frames sensibly).</summary>
        static float PortraitAspect => Mathf.Clamp(ScreenLayout.Aspect, 0.4f, 1f);

        /// <summary>World units of the timeline the landscape view shows across the screen (at most the window).</summary>
        float LandscapeExtent() => Mathf.Min(Length, 2f * Distance * TanHalfFov * LandscapeAspect);

        /// <summary>World units of the timeline a narrow view fits across the portrait width.</summary>
        float NarrowExtent()
        {
            float across = 2f * Distance * NarrowPullback * TanHalfFov * PortraitAspect;
            return NarrowFill * across / Mathf.Max(0.3f, Mathf.Cos(YawOffset * Mathf.Deg2Rad));
        }

        /// <summary>Polar presets on a portrait screen: step back until <see cref="PortraitWidth"/> fits across.</summary>
        float PolarPortraitDistance() => Mathf.Max(Distance, PortraitWidth / (2f * TanHalfFov * PortraitAspect));

        /// <summary>
        /// A polar preset that looks at one arc of the clock, on a portrait screen: it keeps its orientation and,
        /// with <see cref="PortraitRho"/> set, looks down more steeply with its band of relevance filling the frame
        /// between the bars (stepping back until the arc's landscape width fits would show the far side of the
        /// clock in the tall frame). Without it, the view steps back until <see cref="PortraitWidth"/> fits across.
        /// </summary>
        void PolarArcPortrait(ref CameraPose pose)
        {
            if (PortraitPitch > 0) pose.Pitch = PortraitPitch;
            if (PortraitRho.y <= PortraitRho.x)
            {
                pose.Distance = PolarPortraitDistance();
                return;
            }

            // the camera looks outward, so the band runs up the screen from its inner to its outer edge
            float extent = (PortraitRho.y - PortraitRho.x) * RhoScale;
            float middle = ((PortraitRho.x + PortraitRho.y) * 0.5f - TargetRho) * RhoScale;
            FitAlongView(ref pose, extent, middle);
        }

        /// <summary>
        /// Portrait framing of an unrolled window: look along the timeline into the past (the present is near,
        /// at the bottom of the screen), steep enough that the far end is not crushed by perspective, and far
        /// enough that the stretch of time the landscape view showed fits between the title block and the
        /// bottom bar. Relevance (rho) then runs across the screen, outward to the right: the view slides
        /// outward until the narrowest (nearest) row starts just inside the base path, so the width shows what
        /// splits off rather than the empty inside of the path.
        /// </summary>
        void Turn(ref CameraPose pose)
        {
            pose.Yaw -= 90;
            pose.Pitch = Mathf.Clamp(Pitch, TurnMinPitch, TurnMaxPitch);
            float nearEnd = FitAlongView(ref pose, LandscapeExtent(), 0);

            float cos = Mathf.Cos(pose.Pitch * Mathf.Deg2Rad);
            float halfWidthNear = (pose.Distance + nearEnd * cos) * TanHalfFov * PortraitAspect;
            float outward = Mathf.Max(0, halfWidthNear - PortraitInnerMargin - TargetRho * RhoScale);
            pose.Target += Quaternion.Euler(0, pose.Yaw, 0) * Vector3.right * outward;
        }

        /// <summary>
        /// Fits a stretch of the target's height plane that runs along the view (extent world units, its middle
        /// middleAhead units in front of the target) between PortraitBottom and PortraitTop: sets the distance
        /// and moves the target along the view. Returns where the stretch's near end lies relative to the new
        /// target (world units in front of it; negative, toward the camera).
        /// </summary>
        static float FitAlongView(ref CameraPose pose, float extent, float middleAhead)
        {
            float t = TanHalfFov;
            float sin = Mathf.Sin(pose.Pitch * Mathf.Deg2Rad), cos = Mathf.Cos(pose.Pitch * Mathf.Deg2Rad);

            // a point r world units further along the view than the target projects to
            // y = r sin / ((D + r cos) t); solve for the distance D and the target that put the ends of the
            // extent at PortraitBottom and PortraitTop
            float kTop = PortraitTop / (sin - PortraitTop * t * cos);
            float kBottom = PortraitBottom / (sin - PortraitBottom * t * cos);
            float distance = extent / (t * (kTop - kBottom));
            float targetToFarEnd = kTop * t * distance;
            Vector3 ahead = Quaternion.Euler(0, pose.Yaw, 0) * Vector3.forward;
            pose.Target += ahead * (middleAhead + extent * 0.5f - targetToFarEnd);
            pose.Distance = distance;
            return kBottom * t * distance;
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
        /// <summary>Matter emphasis of the life-focused views: the red layer recedes behind the tree of life.</summary>
        const float LifeMatterEmphasis = 0.45f;

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
                    Key = KeyCode.Alpha1, Polar = true, Pitch = 62, Distance = 22f, TargetY = GraphStyle.LifeY, StrataEmphasis = 0.6f,
                    PortraitWidth = 13.5f
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
                    TargetY = GraphStyle.LifeY, Pitch = 55, Distance = 7.5f, MatterEmphasis = LifeMatterEmphasis,
                    PortraitPitch = 70, PortraitRho = new Vector2(-1.5f, 4.5f)
                },
                new ViewPreset
                {
                    Id = "complex_life", Title = "Complex life", Subtitle = "The last billion years",
                    YaOld = 1.0e9, YaNew = 2e3, LogOffset = 3e6, Length = 14,
                    TargetRho = 1.2f, TargetY = GraphStyle.LifeY, Pitch = 58, Distance = 11, MatterEmphasis = LifeMatterEmphasis
                },
                new ViewPreset
                {
                    Id = "mammals", Title = "Mammals", Subtitle = "The last 250 million years",
                    YaOld = 2.5e8, YaNew = 2e3, LogOffset = 1e6, Length = 14,
                    TargetRho = 0.8f, TargetY = GraphStyle.LifeY, Pitch = 58, Distance = 10, MatterEmphasis = LifeMatterEmphasis
                },
                new ViewPreset
                {
                    Id = "hominins", Title = "Hominins", Subtitle = "The last 10 million years",
                    Key = KeyCode.Alpha4, YaOld = 1.0e7, YaNew = 1e3, LogOffset = 5e4, Length = 12,
                    TargetRho = 0.3f, TargetY = GraphStyle.LifeY, Pitch = 55, Distance = 7, MatterEmphasis = LifeMatterEmphasis
                },
                new ViewPreset
                {
                    Id = "prehistory", Title = "Prehistory", Subtitle = "300,000 years of Homo sapiens",
                    YaOld = 3.2e5, YaNew = Ya(-2000), LogOffset = 8e3, Length = 12,
                    TargetRho = 0.3f, TargetY = GraphStyle.HumansY, Pitch = 55, Distance = 7
                },
                new ViewPreset
                {
                    // the human branch time mapping (near linear), as a lens so the rest of the clock straightens away
                    Id = "civilizations", Title = "Civilizations", Subtitle = "Relative power, 3000 BCE to now",
                    Key = KeyCode.Alpha5, YaOld = Ya(-3100), YaNew = 0, LogOffset = 400, Length = 13, StrataEmphasis = 0.15f,
                    TargetRho = 1.3f, TargetY = GraphStyle.HumansY, Pitch = 60, Distance = 11
                },
                new ViewPreset
                {
                    Id = "modern", Title = "Modern era", Subtitle = "1776 to now",
                    Key = KeyCode.Alpha6, YaOld = Ya(1770), YaNew = 0, LogOffset = 400, Length = 14, YScale = 2f, StrataEmphasis = 0.15f,
                    TargetRho = 2.4f, TargetY = GraphStyle.HumansY, Pitch = 50, Distance = 12
                },
                new ViewPreset
                {
                    // the strata of domination seen edge-on: humans above the life they farm above the matter they dig
                    Id = "footprint", Title = "Human footprint",
                    Subtitle = "Humans above the livestock and crops they raise, above the minerals they extract",
                    Key = KeyCode.Alpha9, YaOld = Ya(-3100), YaNew = 0, LogOffset = 400, Length = 14, YScale = 6f,
                    TargetRho = 2.0f, TargetY = GraphStyle.FarmY, Pitch = 16, Distance = 11, YawOffset = -18,
                    Portrait = PortraitFraming.Narrow
                },
                new ViewPreset
                {
                    Id = "smv", Title = "United States 1950 - now",
                    Subtitle = "Gender-separated lifelines rising and falling with social market value",
                    Key = KeyCode.Alpha7, YaOld = Ya(1948), YaNew = 0, LogOffset = 600, Length = 14,
                    RhoScale = 2.5f, YScale = 3f, StrataEmphasis = 0.1f,
                    TargetRho = 0.7f, TargetY = GraphStyle.HumansY + 0.12f, Pitch = 30, Distance = 8f,
                    Portrait = PortraitFraming.Narrow
                },
                new ViewPreset
                {
                    Id = "present", Title = "The present moment", Subtitle = "Where every line arrives",
                    Key = KeyCode.Alpha8, YaOld = 150, YaNew = 0, LogOffset = 80, Length = 9, TargetRho = 2.2f, StrataEmphasis = 0.15f,
                    TargetY = GraphStyle.HumansY, Pitch = 40, Distance = 8f
                },
            };
        }
    }
}
