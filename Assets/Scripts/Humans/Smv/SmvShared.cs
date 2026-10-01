using UnityEngine;

namespace Why.Humans.Smv
{
    /// <summary>
    /// Restyles the United States lifelines for another scene's model (the economy scene tints the people who
    /// own their life path). Published under <see cref="SmvPopulation.StyleKey"/> by a layer that prepares before
    /// <see cref="SmvLayer"/> (Order below 30); the causality graph publishes none and its lines are unchanged.
    /// </summary>
    public interface ISmvLineStyle
    {
        /// <summary>
        /// Called once on the finished simulation, before any line is shaped (worker thread): run the model that
        /// decides the styles here.
        /// </summary>
        void Prepare(SmvSimulation sim);

        /// <summary>
        /// Restyles one point of a person's line at a calendar time: the tint's rgb (its alpha is set by the line
        /// density afterwards) and the HDR intensity. Called from several worker threads at once: read only.
        /// </summary>
        void Restyle(SmvPerson person, double time, bool child, ref Color32 tint, ref float intensity);
    }

    /// <summary>
    /// The finished United States population, published by <see cref="SmvLayer"/> under <see cref="SharedKey"/>
    /// for layers built on it (Order 40 and up read it in Prepare).
    /// </summary>
    public sealed class SmvPopulation
    {
        public const string SharedKey = "smv.population";
        public const string StyleKey = "smv.style";

        public SmvSimulation Sim;

        /// <summary>Civilization block of the lifeline ids (<see cref="GraphIds.Lifeline"/>).</summary>
        public int CivIndex;

        /// <summary>Every lifeline's highlight id.</summary>
        public IdRange LineIds;

        /// <summary>Clock arc of a calendar year (never below the "now" arc), as the lifelines are drawn.</summary>
        public float U(double year) => Mathf.Max(DeepTime.Arc(Sim.NowYear - year), DeepTime.NowArc);

        /// <summary>A person's lifeline highlight id.</summary>
        public int Id(SmvPerson p) => GraphIds.Lifeline(CivIndex, p.Index);

        /// <summary>
        /// Data-space point of a person's line at a simulation step (the sample written that step), or false when
        /// they are not alive then.
        /// </summary>
        public bool PointAt(SmvPerson p, int step, out Vector3 data)
        {
            data = default;
            if (step < p.FirstStep || step > p.LastStep) return false;
            int i = p.SampleOffset + step - p.FirstStep;
            data = new Vector3(U(Sim.TimeOf(step)), Sim.SampleY[i], Sim.SampleRho[i]);
            return true;
        }
    }
}
