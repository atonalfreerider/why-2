using UnityEngine;

namespace Why
{
    /// <summary>
    /// Layout constants and the palette. Hue is reserved for the three levels of the universe:
    /// red = matter, green = life, blue = humans. Everything else is neutral.
    /// </summary>
    public static class GraphStyle
    {
        /// <summary>Radius of the base ring (rho = 0, the inner "our lineage" track).</summary>
        public const float R0 = 2f;

        // vertical hierarchy
        public const float MatterY = 0f;
        public const float LifeY = 0.35f;
        public const float HumansY = 0.7f;

        /// <summary>Height range used by human lifelines for social market value (0..10 maps to 0..SmvHeight).</summary>
        public const float SmvHeight = 0.3f;

        // level colors (linear, LDR base; intensity multipliers push them into HDR for bloom)
        public static readonly Color Matter = new Color(1f, 0.10f, 0.06f);
        public static readonly Color Life = new Color(0.16f, 1f, 0.22f);
        public static readonly Color Humans = new Color(0.20f, 0.42f, 1f);

        // gender shades stay inside the blue hue
        public static readonly Color HumansMale = new Color(0.14f, 0.30f, 1f);
        public static readonly Color HumansFemale = new Color(0.45f, 0.62f, 1f);

        // neutral UI / axis colors
        public static readonly Color Axis = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color AxisDim = new Color(0.32f, 0.33f, 0.36f);
        public static readonly Color Text = new Color(0.86f, 0.87f, 0.90f);
        public static readonly Color TextDim = new Color(0.55f, 0.56f, 0.60f);
        public static readonly Color Background = new Color(0.012f, 0.013f, 0.018f);

        /// <summary>HDR glow multiplier applied to highlighted geometry.</summary>
        public const float HighlightGlow = 5f;

        /// <summary>Years ago at which life and matter start dissolving ahead of the human branch.</summary>
        public const double HandoffFadeStartYearsAgo = 5e6;

        static float handoffFadeStartArc = -1;

        public static float HandoffFadeStartArc =>
            handoffFadeStartArc >= 0 ? handoffFadeStartArc : handoffFadeStartArc = DeepTime.Arc(HandoffFadeStartYearsAgo);

        /// <summary>
        /// The clock is not a clock by the end: at 3 o'clock the human branch leaves on a straight line, and
        /// the underlying life and matter layers must have faded out by then. 1 before the fade, 0 at the
        /// handoff (mirrors WhyHandoffFade in the shaders).
        /// </summary>
        public static float HandoffFade(float u)
        {
            float uH = GraphWarp.BasePath.HandoffArc;
            float t = Mathf.Clamp01((u - uH) / Mathf.Max(HandoffFadeStartArc - uH, 1e-5f));
            return t * t * (3 - 2 * t);
        }

        public static Color Level(GraphLevel level)
        {
            switch (level)
            {
                case GraphLevel.Matter: return Matter;
                case GraphLevel.Life: return Life;
                case GraphLevel.Humans: return Humans;
                default: return Axis;
            }
        }

        public static float LevelY(GraphLevel level)
        {
            switch (level)
            {
                case GraphLevel.Matter: return MatterY;
                case GraphLevel.Life: return LifeY;
                case GraphLevel.Humans: return HumansY;
                default: return MatterY;
            }
        }
    }

    public enum GraphLevel
    {
        None = 0,
        Matter = 1,
        Life = 2,
        Humans = 3
    }
}
