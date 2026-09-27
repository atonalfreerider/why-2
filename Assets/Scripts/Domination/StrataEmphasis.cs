using System.Collections.Generic;
using UnityEngine;

namespace Why.Domination
{
    /// <summary>
    /// How strongly a stratum beneath the civilizations (livestock and crops, mineral extraction) is shown.
    /// Human-focused views keep the strata faint (<see cref="ViewPreset.StrataEmphasis"/>) so the
    /// civilizations read clearly; whenever the director points at something in a stratum it comes back to
    /// full strength. Eases smoothly and hides the stratum's labels while faint.
    /// </summary>
    public sealed class StrataEmphasis
    {
        const float LabelsBelow = 0.35f;
        const float Speed = 1.6f;

        readonly IdRange ids;
        readonly List<(Material material, float baseAlpha)> materials = new List<(Material, float)>();
        readonly List<LabelSpec> labels = new List<LabelSpec>();
        float current = -1;

        public StrataEmphasis(IdRange ids) => this.ids = ids;

        public float Current => current;

        public void Add(Material material, float baseAlpha) => materials.Add((material, baseAlpha));

        public void Add(LabelSpec label)
        {
            if (label != null) labels.Add(label);
        }

        /// <summary>Main thread, every frame.</summary>
        public void Tick(float dt)
        {
            GraphRoot root = GraphRoot.Instance;
            float target = 1f;
            if (root != null && root.CurrentPreset != null) target = root.CurrentPreset.StrataEmphasis;
            if (Highlighter.IsHighlighted(ids)) target = 1f;

            float next = current < 0 ? target : Mathf.MoveTowards(current, target, dt * Speed);
            if (Mathf.Abs(next - current) < 1e-4f) return;
            bool wasShown = current >= LabelsBelow;
            current = next;

            foreach ((Material m, float a) in materials) GraphMaterials.SetAlpha(m, a * current);
            bool shown = current >= LabelsBelow;
            if (shown != wasShown || labels.Count > 0 && labels[0].Hidden == shown)
            {
                foreach (LabelSpec l in labels) l.Hidden = !shown;
                root?.Labels.MarkDirty();
            }
        }
    }
}
