using System;
using TMPro;
using UnityEngine;

namespace Why.Economy.Draw
{
    /// <summary>
    /// Shared helper for world-space TextMeshPro labels that sit over bright geometry: raises their font material
    /// to a dedicated render queue and attaches a shared unit-square dark plate behind them. Eliminates duplicated
    /// per-layer quad/material creation and guarantees cleanup via <see cref="Clear"/>.
    /// </summary>
    public sealed class WorldLabelPlates
    {
        /// <summary>Default plate padding in TMP local units (0.35 em horizontal, 0.12 em vertical on each side at 2.4 units/em).</summary>
        public static readonly Vector2 DefaultPadding = new Vector2(.7f, .24f) * 2.4f;

        readonly string meshName;
        readonly float plateAlpha;
        readonly int plateQueue;
        readonly int textQueue;
        readonly float highlightId;

        Material raisedText;
        Material plateMaterial;
        Mesh plateQuad;

        public WorldLabelPlates(string meshName, float plateAlpha = .9f, int plateQueue = 3251, int textQueue = 3252, float highlightId = 0)
        {
            this.meshName = meshName;
            this.plateAlpha = plateAlpha;
            this.plateQueue = plateQueue;
            this.textQueue = textQueue;
            this.highlightId = highlightId;
        }

        /// <summary>The raised TMP font material, lazily cloned from the first woken label.</summary>
        public Material RaisedMaterial => raisedText;

        /// <summary>Assigns the raised font material (at <c>textQueue</c>) to <paramref name="t"/> and returns it.</summary>
        public TextMeshPro Raise(TextMeshPro t)
        {
            if (t == null) return null;
            if (raisedText == null && t.fontSharedMaterial != null)
                raisedText = new Material(t.fontSharedMaterial) { renderQueue = textQueue };
            if (raisedText != null) t.fontSharedMaterial = raisedText;
            return t;
        }

        /// <summary>Creates a dark backing plate child on <paramref name="t"/> using a single shared unit-square mesh and material.</summary>
        public Transform AttachPlate(TextMeshPro t, Func<string, Mesh, Material, MeshRenderer> addMesh, bool sizeToText = true, bool active = true)
        {
            EnsurePlate();
            MeshRenderer r = addMesh(meshName, plateQuad, plateMaterial);
            r.transform.SetParent(t.transform, false);
            if (sizeToText) r.transform.localScale = PlateSize(t);
            if (t.alignment == TextAlignmentOptions.Left)
                r.transform.localPosition = new Vector3(t.rectTransform.rect.xMin + t.GetPreferredValues(t.text).x * .5f, 0, 0);
            if (!active) r.gameObject.SetActive(false);
            return r.transform;
        }

        /// <summary>Returns the plate's local scale for <paramref name="t"/>'s current text.</summary>
        public static Vector3 PlateSize(TextMeshPro t)
        {
            Vector2 s = t.GetPreferredValues(t.text) + DefaultPadding;
            return new Vector3(s.x, s.y, 1);
        }

        /// <summary>Destroys the shared mesh and materials created by this helper.</summary>
        public void Clear()
        {
            if (raisedText) UnityEngine.Object.Destroy(raisedText);
            if (plateMaterial) UnityEngine.Object.Destroy(plateMaterial);
            if (plateQuad) UnityEngine.Object.Destroy(plateQuad);
            raisedText = null;
            plateMaterial = null;
            plateQuad = null;
        }

        void EnsurePlate()
        {
            if (plateQuad != null) return;
            var b = new SurfaceMeshBuilder();
            b.AddBand(
                new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) },
                new[] { new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                new[] { (Color32)GraphStyle.Background.WithAlpha(plateAlpha) }, highlightId, 1);
            plateQuad = b.ToMesh(meshName);
            plateMaterial = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, plateQueue));
            plateMaterial.SetFloat("_EdgeSoft", 0);
        }
    }
}
