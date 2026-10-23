using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The replay's turning points on the wave itself (<see cref="EconomyReplay"/>, <see cref="ReplayFacts"/>): once the
    /// crest has passed a turning point, the rib of its year is traced faintly in the turning point's color (rose: the
    /// market's real value added fell; gold: the federal funds rate turned; steel: the tariff rate jumped) and a pin rises
    /// from the rib's highest ridge to its tag, so the wake keeps the history the crest has just been through without
    /// drowning the companies' lines that run along it. The rib the replay holds on pulses. Shown only while a replay is
    /// under way, in the views of the wave (the overview, the landscape, the companies' lives); the ribs are the wave's
    /// own cross-sections (<see cref="HillLandscape.Ground"/> at the year), so nothing is drawn that the data did not put
    /// there.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillReplayLayer : GraphLayer
    {
        public override int Order => 66;

        /// <summary>The wave is seen from a few hundred units: the tags are drawn a little larger than the wave's own labels.</summary>
        const float LabelSize = .55f * 2.2f;

        /// <summary>The pin's height above the rib's highest ridge (world units), and the step up for each turning point
        /// that follows another within <see cref="ClusterYears"/> (1981-83, 2020 and 2026): their tags stack instead of
        /// overlapping, at most <see cref="MaxLevel"/> steps up.</summary>
        const float PinHeight = 9f, PinStep = 5f;

        const int ClusterYears = 8, MaxLevel = 3;

        /// <summary>Pulses per second of the rib the replay holds on.</summary>
        const float PulseRate = 1.6f;

        readonly List<(TextMeshPro text, int year)> labels = new List<(TextMeshPro, int)>();
        readonly List<Object> owned = new List<Object>();
        readonly List<Vector3> rib = new List<Vector3>();
        HillLandscape land;
        ReplayFacts facts;
        GameObject content;
        MeshRenderer ribs, now;
        int nowYear = -1;
        float clock;

        public override void Prepare(GraphContext ctx) { }

        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            string view = LandView.PresetId;
            bool on = EconomyReplay.Active && (view == "overview" || view == "landscape" || view == "companies");
            if (!on)
            {
                if (content) content.SetActive(false);
                return;
            }

            HillLandscape current = HillLandscapeLayer.Current;
            if (current == null || EconomyReplay.Facts == null) return;
            if (current != land || EconomyReplay.Facts != facts)
            {
                if (!HillRebuilds.Take()) return;
                Build(current, EconomyReplay.Facts);
            }
            if (content == null) return;
            content.SetActive(true);
            foreach ((TextMeshPro text, int _) in labels) text.transform.rotation = rig.Cam.transform.rotation;
            if (now != null)
            {
                clock += GraphTime.Delta;
                bool holding = EconomyReplay.ShownYear == nowYear;
                GraphMaterials.SetAlpha(now.sharedMaterial, holding ? .55f + .45f * Mathf.Sin(clock * PulseRate * 2 * Mathf.PI) : 0);
            }
        }

        void Build(HillLandscape current, ReplayFacts replay)
        {
            Clear();
            land = current;
            facts = replay;
            content = new GameObject("Replay turning points");
            content.transform.SetParent(transform, false);
            var all = new LineMeshBuilder();
            var pulse = new LineMeshBuilder();
            nowYear = -1;
            int level = 0, previous = int.MinValue;
            foreach (ReplayFacts.TurningPoint t in replay.TurningPoints)
            {
                if (t.Year < land.FirstYear || t.Year > land.Year) continue;
                level = t.Year - previous <= ClusterYears ? Mathf.Min(MaxLevel, level + 1) : 0;
                previous = t.Year;
                float pin = PinHeight + level * PinStep;
                Color c = KindColor(t.Kind);
                float z = HillLandscape.Z(t.Year), end = HillLandscape.FootX + land.SliceWidth(t.Year) + 18;
                rib.Clear();
                Vector3 top = new Vector3(HillLandscape.FootX, 0, z);
                for (float x = HillLandscape.FootX - 8; x <= end; x += 1.25f)
                {
                    Vector3 p = new Vector3(x, land.Ground(x, z) + .2f, z);
                    rib.Add(p);
                    if (p.y > top.y) top = p;
                }

                all.AddPolyline(rib, WithAlpha(c, .35f), 1.6f, 0, 0, 1.2f);
                all.AddSegment(top, top + Vector3.up * pin, WithAlpha(c, .9f), 2f, 0, 0, 1.6f);
                if (t.Year == land.Year)
                {
                    pulse.AddPolyline(rib, WithAlpha(c, 1), 3.5f, 0, 0, 2.4f);
                    nowYear = t.Year;
                }

                labels.Add((Label("<b>" + t.Year + "</b> · " + t.Tag, top + Vector3.up * (pin + 2.2f), Color.Lerp(c, Color.white, .35f)), t.Year));
            }

            ribs = Lines(all, "Turning point ribs");
            now = nowYear > 0 ? Lines(pulse, "Turning point now") : null;
        }

        static Color KindColor(ReplayFacts.Kind kind) =>
            kind == ReplayFacts.Kind.Recession ? EconomyStyle.Desire : kind == ReplayFacts.Kind.Tariff ? EconomyStyle.Government : EconomyStyle.Capital;

        MeshRenderer Lines(LineMeshBuilder b, string name)
        {
            Mesh mesh = b.ToMesh(name);
            owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3206, true));
            owned.Add(material);
            MeshRenderer r = AddMesh(name, mesh, material);
            r.transform.SetParent(content.transform, false);
            return r;
        }

        TextMeshPro Label(string text, Vector3 at, Color color)
        {
            var go = new GameObject("Turning point label");
            go.transform.SetParent(content.transform, false);
            go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = 24;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(60, 8);
            go.transform.localScale = Vector3.one * LabelSize;
            return t;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        void Clear()
        {
            foreach (Object o in owned)
            {
                if (o) Destroy(o);
            }

            if (content) Destroy(content);
            owned.Clear();
            labels.Clear();
            content = null;
            ribs = now = null;
            land = null;
            facts = null;
        }

        void OnDestroy() => Clear();
    }
}
