using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The replay's turning points on the wave itself (<see cref="EconomyReplay"/>, <see cref="ReplayFacts"/>): once the
    /// crest has passed a turning point, the rib of its year is traced faintly in the turning point's color (rose: the
    /// market's real value added fell; gold: the federal funds rate turned; steel: the tariff rate jumped) and a pin rises
    /// from the rib's highest ridge to its tag, so the wake keeps the history the crest has just been through without
    /// drowning the companies' lines that run along it. The rib the replay holds on pulses, in its opacity: every mark is
    /// drawn at intensity 1 and blended, under the bloom, so the turning points' hues stay true. Shown only while a replay
    /// is under way, in the views of the wave (the overview, the landscape, the companies' lives); the ribs are the wave's
    /// own cross-sections (<see cref="HillLandscape.Ground"/> at the year), so nothing is drawn that the data did not put
    /// there. Each tag stands on a dark plate.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillReplayLayer : GraphLayer
    {
        public override int Order => 66;

        /// <summary>The wave is seen from a few hundred units: the tags are drawn a little larger than the wave's own labels.</summary>
        const float LabelSize = .55f * 2.2f;

        /// <summary>The pin's height above the rib's highest ridge (world units), and the step up a tag takes while its box on
        /// screen meets an earlier tag's or a company's name (at most <see cref="MaxLevel"/> steps): the far wake is
        /// foreshortened, so turning points ten years apart (1981 and 1993) print on one another from the overview.</summary>
        const float PinHeight = 9f, PinStep = 5f;

        const int MaxLevel = 5;

        /// <summary>Where a tag stands on its step, tried in turn: centred on its pin, ending at it, starting at it (half its
        /// width along the camera's right).</summary>
        static readonly float[] Sides = { 0, -1, 1 };

        /// <summary>Pulses per second of the rib the replay holds on.</summary>
        const float PulseRate = 1.6f;

        /// <summary>The tags: each one's place over its pin and how far it stands beside it (world units along the camera's
        /// right).</summary>
        readonly List<(TextMeshPro text, Vector3 at, float shift)> labels = new List<(TextMeshPro, Vector3, float)>();
        readonly List<Object> owned = new List<Object>();
        readonly List<Vector3> rib = new List<Vector3>();
        readonly List<Rect> placed = new List<Rect>();
        readonly List<(Vector3 at, Vector2 half)> names = new List<(Vector3, Vector2)>();
        HillLandscape land;
        ReplayFacts facts;
        GameObject content;
        MeshRenderer ribs, now;
        int nowYear = -1;
        float clock;

        /// <summary>The held year's rib's pulse (0 when the replay has moved on), its multiplier on replay.now.</summary>
        float held;

        /// <summary>The emphasis keys of the turning points' ribs and pins and of the held year's rib (EmphasisTable: full in the
        /// views of the whole wave, absent elsewhere), whose highlight ids their meshes carry.</summary>
        static class Part
        {
            public static readonly int Ribs = EconomyEmphasis.Key("replay.ribs"), Now = EconomyEmphasis.Key("replay.now");
        }

        /// <summary>The view the tags were stacked for: their levels follow its pose.</summary>
        string builtView;

        /// <summary>The tags were stacked while the camera flew, against the wave's names where they stood for the pose it was
        /// leaving (they keep their places until it lands, <see cref="HillWaveLayer"/>): stacked again once it has landed.</summary>
        bool landing;

        /// <summary>The tags' text drawn after the hills (a copy of the font's material, queue 3252, as the wave's raised names):
        /// at the font's queue the crowns at the crest's tech end and the wake's lines drew over them.</summary>
        Material raisedText;

        /// <summary>The dark plate behind every tag (a unit square scaled to each tag's text) and its material, drawn after the
        /// crowns and the pins (queue 3251) and before the tags' text: the TECH crowns' bloom washed out 2020's value, and an
        /// upper tag's pin ran through the tags stacked under it. Made once, kept across builds.</summary>
        Mesh plateQuad;

        Material plateMaterial;

        /// <summary>A tag's plate margin around its text (local units): the label system's, .35 em sideways and .12 em above and
        /// below, TextMeshPro setting 2.4 local units per em at its font size 24.</summary>
        static readonly Vector2 PlateMargin = new Vector2(.35f, .12f) * 2.4f;

        /// <summary>How much of what is behind a tag its plate hides: all of it. The crowns and the star cloud bloom, and a
        /// quarter of them (the label system's .75) still showed bright through the letters; at .9 a tenth of an upper tag's
        /// pin still ran through the tags stacked under it (1983's gold through 1982 and 1981 in the end overview). The tags'
        /// text is drawn after every plate (3252), so plates that overlap never cover a tag's words.</summary>
        const float PlateAlpha = 1f;

        public override void Prepare(GraphContext ctx) { }

        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            EconomyEmphasis.Multiplier("replay.now", "the held year's pulse", () => held);
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            string view = LandView.PresetId;
            // the Wave view drawn as the sweep through time (HillSweepLayer) has no wave to tag
            bool on = EconomyReplay.Active && (view == "overview" && !HillSweepLayer.On || view == "landscape" || view == "companies");
            if (!on)
            {
                if (content) content.SetActive(false);
                return;
            }

            HillLandscape current = HillLandscapeLayer.Current;
            if (current == null || EconomyReplay.Facts == null) return;
            if (current != land || EconomyReplay.Facts != facts || view != builtView || landing && !rig.Flying)
            {
                if (!HillRebuilds.Take()) return;
                Build(current, EconomyReplay.Facts, rig, view);
            }
            if (content == null) return;
            content.SetActive(true);
            // a tag beside its pin stands along the camera's right, so its end stays on the pin whichever way the camera looks
            Vector3 right = transform.InverseTransformDirection(rig.Cam.transform.right);
            foreach ((TextMeshPro text, Vector3 at, float shift) in labels)
            {
                text.transform.rotation = rig.Cam.transform.rotation;
                text.transform.localPosition = at + right * shift;
            }
            if (now != null)
            {
                clock += GraphTime.Delta;
                bool holding = EconomyReplay.ShownYear == nowYear;
                held = holding ? .55f + .45f * Mathf.Sin(clock * PulseRate * 2 * Mathf.PI) : 0;
            }
            EconomyEmphasis.Apply(ribs, Part.Ribs); EconomyEmphasis.Apply(now, Part.Now);
        }

        void Build(HillLandscape current, ReplayFacts replay, CameraRig rig, string view)
        {
            Clear();
            land = current;
            facts = replay;
            builtView = view;
            landing = rig.Flying;
            content = new GameObject("Replay turning points");
            content.transform.SetParent(transform, false);
            var all = new LineMeshBuilder();
            var pulse = new LineMeshBuilder();
            nowYear = -1;
            // the tags are stacked as they will stand once the camera has flown to the view's pose (from the pose it is leaving
            // they would stack for the last view); without a pose, from where the camera stands
            ViewPreset preset = GraphRoot.Instance != null ? GraphRoot.Instance.CurrentPreset : null;
            CameraPose pose = preset != null && preset.FixedTarget.HasValue ? preset.Pose() : rig.Pose;
            Quaternion look = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 eye = pose.Target - look * Vector3.forward * pose.Distance;
            float focal = rig.Cam.pixelHeight * .5f / Mathf.Tan(rig.Cam.fieldOfView * .5f * Mathf.Deg2Rad);
            // the wave's company names shown in the view (AT&T's ran into 1974's tag in the overview): the tags keep clear of
            // them as of one another
            placed.Clear();
            names.Clear();
            HillWaveLayer.ShownNames(names);
            foreach ((Vector3 at, Vector2 half) in names) placed.Add(OnScreen(eye, look, focal, transform.TransformPoint(at), half));
            foreach (ReplayFacts.TurningPoint t in replay.TurningPoints)
            {
                if (t.Year < land.FirstYear || t.Year > land.Year) continue;
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

                // in the years' order, so a new year never moves the tags before it
                TextMeshPro tag = Label("<b>" + t.Year + "</b> · " + t.Tag, top, Color.Lerp(c, Color.white, .35f), out Vector2 size);
                Vector2 half = size * (.5f * LabelSize);
                float pin = PinHeight + Place(top, half, eye, look, focal, out float side) * PinStep;
                Vector3 place = top + Vector3.up * (pin + 2.2f);
                tag.transform.localPosition = place;
                labels.Add((tag, place, side * half.x));
                all.AddPolyline(rib, WithAlpha(c, .35f), 1.6f, 0, EconomyEmphasis.Id(Part.Ribs));
                all.AddSegment(top, top + Vector3.up * pin, WithAlpha(c, .9f), 2f, 0, EconomyEmphasis.Id(Part.Ribs));
                if (t.Year == land.Year)
                {
                    pulse.AddPolyline(rib, WithAlpha(c, 1), 3.5f, 0, EconomyEmphasis.Id(Part.Now));
                    nowYear = t.Year;
                }
            }

            ribs = Lines(all, "Turning point ribs");
            now = nowYear > 0 ? Lines(pulse, "Turning point now") : null;
        }

        /// <summary>The lowest step over its pin (0 to <see cref="MaxLevel"/>) where a tag's box on screen meets none placed so
        /// far, trying each of <see cref="Sides"/> on a step before the next (<paramref name="side"/>); the top step, centred,
        /// when none is clear. The box joins those placed.</summary>
        int Place(Vector3 top, Vector2 half, Vector3 eye, Quaternion look, float focal, out float side)
        {
            Vector3 right = look * Vector3.right;
            for (int level = 0; level <= MaxLevel; level++)
            {
                Vector3 at = transform.TransformPoint(top + Vector3.up * (PinHeight + level * PinStep + 2.2f));
                foreach (float s in Sides)
                {
                    Rect box = OnScreen(eye, look, focal, at + right * (s * half.x), half);
                    if (Meets(box, placed)) continue;
                    placed.Add(box);
                    side = s;
                    return level;
                }
            }

            side = 0;
            placed.Add(OnScreen(eye, look, focal, transform.TransformPoint(top + Vector3.up * (PinHeight + MaxLevel * PinStep + 2.2f)), half));
            return MaxLevel;
        }

        /// <summary>A box on screen (pixels from the screen's middle) from <paramref name="eye"/> looking along
        /// <paramref name="look"/>: its half size (world units) about its place; empty behind the camera.</summary>
        static Rect OnScreen(Vector3 eye, Quaternion look, float focal, Vector3 at, Vector2 half)
        {
            Vector3 v = Quaternion.Inverse(look) * (at - eye);
            if (v.z <= 1e-3f) return Rect.zero;
            float k = focal / v.z;
            return new Rect((v.x - half.x) * k, (v.y - half.y) * k, 2 * half.x * k, 2 * half.y * k);
        }

        static bool Meets(Rect box, List<Rect> boxes)
        {
            foreach (Rect b in boxes)
            {
                if (box.Overlaps(b)) return true;
            }

            return false;
        }

        static Color KindColor(ReplayFacts.Kind kind) =>
            kind == ReplayFacts.Kind.Recession ? EconomyStyle.Desire : kind == ReplayFacts.Kind.Tariff ? EconomyStyle.Government : EconomyStyle.Capital;

        MeshRenderer Lines(LineMeshBuilder b, string name)
        {
            Mesh mesh = b.ToMesh(name);
            owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3206, false));
            owned.Add(material);
            MeshRenderer r = AddMesh(name, mesh, material);
            r.transform.SetParent(content.transform, false);
            return r;
        }

        /// <summary>A tag on its plate (<see cref="plateQuad"/>), its text's size (local units) in <paramref name="size"/>.</summary>
        TextMeshPro Label(string text, Vector3 at, Color color, out Vector2 size)
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
            if (raisedText == null && t.fontSharedMaterial != null)
            {
                raisedText = new Material(t.fontSharedMaterial) { renderQueue = 3252 };
                owned.Add(raisedText);
            }

            if (raisedText != null) t.fontSharedMaterial = raisedText;
            if (plateQuad == null)
            {
                var b = new SurfaceMeshBuilder();
                b.AddBand(new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) }, new[] { new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                    new[] { (Color32)WithAlpha(GraphStyle.Background, PlateAlpha) }, 0, 1);
                plateQuad = b.ToMesh("Turning point plate");
                plateMaterial = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3251));
                plateMaterial.SetFloat("_EdgeSoft", 0);
            }

            // the plate turns, moves and hides with its tag
            size = t.GetPreferredValues(text);
            Vector2 plate = size + 2 * PlateMargin;
            MeshRenderer r = AddMesh("Turning point plate", plateQuad, plateMaterial);
            r.transform.SetParent(go.transform, false);
            r.transform.localScale = new Vector3(plate.x, plate.y, 1);
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
            builtView = null;
            raisedText = null;
        }

        void OnDestroy()
        {
            Clear();
            if (plateQuad) Destroy(plateQuad);
            if (plateMaterial) Destroy(plateMaterial);
        }
    }
}
