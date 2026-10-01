using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Why.Economy.Model;
using Why.Humans;
using Why.Humans.Smv;

namespace Why.Economy.UI
{
    /// <summary>
    /// The selected person's lifeline drawn once more over the population: every quarter-year sample of their life
    /// (<see cref="SmvPopulation.PointAt"/>, warped like the bundle), opaque enough to read in the densest part of it,
    /// in the tint the economy gives their line (gold while in control of their path, the population's blue otherwise),
    /// and carrying their lifeline's id, so the highlighter's glow that lights their line and dims the rest lights this
    /// one too.
    /// <para>
    /// The highlight alone hardly shows where a line runs through the middle of the bundle: each fine line there is
    /// drawn at a few percent alpha (SmvGeometry: 0.32 / (1 + density / 1.7)), and five times a few percent does not
    /// stand out from dozens of dimmed neighbours. On harness renders of the people view the highlighted line of the
    /// model's escapist changed about twenty pixels of the frame.
    /// </para>
    /// Main thread; a mesh of a few hundred points, rebuilt when another person is selected.
    /// </summary>
    public sealed class PersonLine
    {
        /// <summary>Alpha of the line (children's years at half, as the bundle draws them).</summary>
        const float LineAlpha = 0.75f, ChildAlphaScale = 0.5f;

        /// <summary>
        /// Width: a little wider than a fine adult line (1 px, SmvGeometry), and some world width so it thickens close up.
        /// </summary>
        const float WidthPx = 1.8f, WidthWorld = 0.0006f;

        /// <summary>
        /// Brightness before the highlighter's glow (x5 while it lights the person): about the bundle's own, so the glow
        /// makes a bright thread that blooms a little rather than a white blaze.
        /// </summary>
        const float Intensity = 0.5f;

        /// <summary>Alpha units per second of the fade in and out.</summary>
        const float FadeSpeed = 3f;

        /// <summary>Above the lifelines (QueueHumans + 2), below the money threads (+10) and the stations' overlays.</summary>
        const int Queue = GraphMaterials.QueueHumans + 3;

        readonly MeshRenderer renderer;
        readonly MeshFilter filter;
        readonly Material material;
        readonly List<LinePoint> points = new List<LinePoint>(512);
        Mesh mesh;
        int built = -1;
        float alpha, target;

        public PersonLine(Transform parent)
        {
            GameObject go = new GameObject("SelectedLifeline");
            go.transform.SetParent(parent, false);
            filter = go.AddComponent<MeshFilter>();
            renderer = go.AddComponent<MeshRenderer>();
            material = GraphMaterials.Line(Color.white, 1f, Queue);
            GraphMaterials.SetAlpha(material, 0);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.enabled = false;
        }

        /// <summary>Shows a person's line (building it when another person was shown), or fades it out for -1.</summary>
        public void Show(SmvPopulation pop, EconomicLives lives, int person)
        {
            if (person < 0 || pop?.Sim == null || person >= pop.Sim.People.Count)
            {
                target = 0;
                return;
            }

            if (person != built) Build(pop, lives, person);
            target = mesh != null ? 1 : 0;
        }

        /// <summary>Eases the line in or out (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            if (Mathf.Approximately(alpha, target)) return;
            alpha = Mathf.MoveTowards(alpha, target, dt * FadeSpeed);
            GraphMaterials.SetAlpha(material, alpha);
            renderer.enabled = alpha > 0.003f;
        }

        void Build(SmvPopulation pop, EconomicLives lives, int person)
        {
            built = person;
            SmvSimulation sim = pop.Sim;
            SmvPerson p = sim.People[person];
            points.Clear();
            Color32 baseTint = p.Male ? GraphStyle.HumansMale : GraphStyle.HumansFemale;
            for (int step = p.FirstStep; step <= p.LastStep && p.SampleCount > 0; step++)
            {
                if (!pop.PointAt(p, step, out Vector3 data)) continue;
                double time = sim.TimeOf(step);
                bool child = time - p.Birth < SmvModel.AdultAge;
                Color32 tint = baseTint;
                float lineIntensity = 1; // the bundle's brightness for this point: this line has its own
                lives?.Restyle(p, time, child, ref tint, ref lineIntensity);
                tint.a = (byte)Mathf.RoundToInt(255 * LineAlpha * (child ? ChildAlphaScale : 1));
                points.Add(new LinePoint(data, tint, WidthPx, WidthWorld, Intensity));
            }

            if (mesh != null) Object.Destroy(mesh);
            mesh = null;
            if (points.Count < 2) return;
            LineMeshBuilder builder = new LineMeshBuilder(points.Count + 2);
            builder.AddPolyline(points, pop.Id(p));
            mesh = builder.ToMesh("WhySelectedLifeline");
            filter.sharedMesh = mesh;
        }

        /// <summary>Frees the mesh and the material (the module's OnDestroy).</summary>
        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
