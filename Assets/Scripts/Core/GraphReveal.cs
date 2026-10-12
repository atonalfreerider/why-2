using UnityEngine;

namespace Why
{
    /// <summary>
    /// The growth front of the graph (shader global <c>_WhyReveal</c>, see WhyCommon.hlsl): while it is on, only what lies
    /// behind the front - measured as arc length along the base path from the Big Bang - is drawn, and the front itself
    /// glows, so the causality graph sprouts from the Big Bang along time (<see cref="Director.Genesis"/>).
    /// </summary>
    public static class GraphReveal
    {
        static readonly int Id = Shader.PropertyToID("_WhyReveal");
        static readonly int Id2 = Shader.PropertyToID("_WhyReveal2");

        /// <summary>Arc length of the whole base path: the clock to 3 o'clock, then the human branch to the present.</summary>
        public static float Length => GraphWarp.BasePath.SigmaHandoff + GraphWarp.BasePath.BranchLength;

        public static bool On { get; private set; }

        /// <summary>
        /// Shows the graph up to <paramref name="front"/> (world units along the path) with glowing growing tips. Branches
        /// lag the front by <paramref name="lagPerRho"/> per unit of radius (the inner track first) plus up to
        /// <paramref name="lagSpread"/> by branch, so they sprout one after another; each tip trails a glow of length
        /// <paramref name="trail"/>.
        /// </summary>
        public static void Set(float front, float width, float glow, float lagPerRho = 0, float lagSpread = 0, float trail = 1)
        {
            On = true;
            Shader.SetGlobalVector(Id, new Vector4(front, width, glow, 1));
            Shader.SetGlobalVector(Id2, new Vector4(lagPerRho, lagSpread, trail, 0));
        }

        /// <summary>Everything shown, no cost in the shaders.</summary>
        public static void Off()
        {
            On = false;
            Shader.SetGlobalVector(Id, Vector4.zero);
        }
    }
}
