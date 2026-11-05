using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Draw;
using Why.Economy.Land;
using Why.Economy.UI;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy
{
    /// <summary>Where a hill label's position is given (spec 0's conventions).</summary>
    public enum HillFrame
    {
        /// <summary>Land-local (<see cref="EconomyStage.Land"/>): x across the wave, y up, z along time.</summary>
        Land,

        /// <summary>Stage coordinates (u, v, d) of the new views (spec 4): u right for the stage camera, v up, d toward it.</summary>
        Stage,

        /// <summary>A world position.</summary>
        World
    }

    /// <summary>
    /// A label of the hills or the new views, as a layer gives it to <see cref="HillLabels.Set(object, IEnumerable{HillLabel})"/>
    /// (spec 3.5): a position in the land's or the stage's frame, a size role, the emphasis part it belongs to, how it is
    /// known (the mark code, spec 3.3) and what a click on it does. Built on any thread; read once by Set.
    /// </summary>
    public sealed class HillLabel
    {
        /// <summary>The words (TextMeshPro rich text), twelve or fewer (spec rule 7). May be empty with an <see cref="Icon"/>.</summary>
        public string Text;

        /// <summary>The point the label names, in <see cref="Frame"/>'s coordinates.</summary>
        public Vector3 At;

        public HillFrame Frame = HillFrame.Land;

        /// <summary>Its size role: the height and the priority (title 18 px and 10, name 15 and 8, value 13 and 6, note 12 and 4).</summary>
        public LabelRole Role = LabelRole.Name;

        /// <summary>A height at 1080p other than the role's (0: the role's), clamped to 12-22 px like every role label.</summary>
        public float SizePx;

        /// <summary>The emphasis key it belongs to ("capture.owners"): its alpha follows <see cref="HillLabels.PartAlpha"/>, hidden below 0.3.</summary>
        public string Part;

        /// <summary>
        /// How what it says is known (spec 3.3): measured in regular type; judged in italics with its "J 0.74" tag, and not
        /// drawn below a confidence of 0.5; modelled with "≈" before it and its runs; an earlier value in the ghost's grey.
        /// </summary>
        public Mark Provenance = Mark.Measured;

        /// <summary>A judged label's confidence (0-1).</summary>
        public float Confidence = 1f;

        /// <summary>A simulated label's runs (n), printed after it; 0 prints none.</summary>
        public int Runs;

        /// <summary>Its colour as text takes it (sRGB, LDR): the ink's text by default.</summary>
        public Color Color = Roles.Encoded(Roles.InkText);

        /// <summary>A plate behind it (alpha 0: none), as text takes it, for a label over bright marks.</summary>
        public Color Plate;

        public TextAlignmentOptions Align = TextAlignmentOptions.Center;

        /// <summary>Its own place's offset from the point (px at 1080p at its role's size; it grows with the label in a headset).</summary>
        public Vector2 PixelOffset;

        /// <summary>Further offsets of its own, tried before the four candidate places.</summary>
        public Vector2[] Fallbacks;

        /// <summary>Breaks ties between labels of one priority (higher first).</summary>
        public float Rank;

        /// <summary>The tooltip's source (an anchor's key), and the ids it brightens with.</summary>
        public string AnchorKey;

        public IdRange Ids = IdRange.Empty;

        public string Icon;

        public bool Undimmed;

        /// <summary>What a click or a pinch on it does (main thread): the overview's doors.</summary>
        public Action OnClick;

        /// <summary>The label on screen after the last <see cref="HillLabels.Set(object, IEnumerable{HillLabel})"/> (for SetText and Hidden); null when not drawn.</summary>
        public LabelSpec Spec { get; internal set; }
    }

    /// <summary>
    /// The adapter the hill layers and the new views label through (spec 3.5, S04): one backend, the scene's
    /// <see cref="LabelSystem"/>, for the desktop, portrait and the headset. A layer replaces its whole set of labels from its
    /// build, on any thread (<see cref="Set(object, IEnumerable{HillLabel})"/>, every year of the replay), as role labels at
    /// world positions converted from the land's or the stage's frame: sized by role, clamped to 12-22 px and at least a
    /// degree in a headset, placed by priority with four candidate places and leaders, clear of the HUD's blocks
    /// (<see cref="HudBlocks"/>), LDR with a dark halo, billboards (so S07's mirror never flips text), faded with their
    /// emphasis part (<see cref="PartAlpha"/>, wired to the emphasis table in WP13). In the smoke test it registers the
    /// per-view label metrics (<see cref="CheckName"/>) and the <c>labels-stress</c> sequence (<c>-smokeextra labels-stress</c>).
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillLabels : GraphModule
    {
        /// <summary>The smoke check of every captured view's labels, and the stress sequence (<c>-smokeextra labels-stress</c>).</summary>
        public const string CheckName = "labels", StressName = "labels-stress";

        /// <summary>
        /// The stage's origin in land-local x and z (spec 4: the crest's middle, 240 units ahead of the present, where
        /// nothing of the wave is drawn).
        /// </summary>
        const float StageOriginX = 120, StageOriginZ = 240;

        /// <summary>
        /// The labels the stress sequence places (spec 3.5's acceptance: 300 over the overview), how often it replaces them,
        /// and the worker's pause between two replacements (ms), so that they straddle frames.
        /// </summary>
        const int StressCount = 300, StressReplacements = 100, ReplaceGapMs = 3;

        /// <summary>The stress grid's columns and rows (one label a cell, 300 in all).</summary>
        const int StressColumns = 20, StressRows = 15;

        /// <summary>The cells of the door (row 11, column 4) and of the two names that ask for 8 and 30 px (row 9, columns 14 and 15).</summary>
        const int DoorIndex = 11 * StressColumns + 4, SmallIndex = 9 * StressColumns + 14, LargeIndex = 9 * StressColumns + 15;

        /// <summary>
        /// Seconds the stress sequence waits for the overview to settle (as the smoke test waits on a view) and for the
        /// labels to fade in (at 5 alpha a second, with room).
        /// </summary>
        const float SettleSeconds = 2.6f, FadeSeconds = 1.2f;

        /// <summary>The most a label's pixel may exceed its colour (spec 3.5's acceptance: no bloom halo), and 8-bit rounding.</summary>
        const float LdrLimit = 1.05f, LdrRounding = 1.5f / 255f;

        /// <summary>The stress set's parts: a subject (alpha 1), a support (0.45) and one below the floor, which hides its labels.</summary>
        const string StressSubject = "stress.subject", StressSupport = "stress.support", StressHidden = "stress.hidden";

        /// <summary>
        /// Stand-ins for two panels that stood over the hills' world labels in the smoke frames of 4 Nov 2026, as fractions of
        /// the frame from its top-left (measured on 1920x1080 captures): the tour's card at stop 13 over the mobility bars'
        /// captions and records (economy-tour-13-hills_mobility: 57-575 across, 440-935 down), and the replay's readout over
        /// the money board's OPEN TO TRADE strip (economy-replay-1983-money: 638-1350 across, 851-978 down). The stress labels
        /// under them must move to a candidate place or be culled, as the mobility and money labels will once on HillLabels.
        /// </summary>
        static readonly (string name, Rect frame)[] StandIns =
        {
            ("Tour card (stand-in, tour stop 13)", Rect.MinMaxRect(57 / 1920f, 440 / 1080f, 575 / 1920f, 935 / 1080f)),
            ("Replay readout (stand-in, 1983 money)", Rect.MinMaxRect(638 / 1920f, 851 / 1080f, 1350 / 1920f, 978 / 1080f))
        };

        /// <summary>
        /// A stand-in for the director's caption plate (14 px text on a 22-unit plate, DirectorMode), registered to move labels
        /// aside (<see cref="LabelSystem.MoveAside"/>), at the middle of the frame: the labels it stands over must move to a
        /// candidate place with a leader, not hide or stay cut.
        /// </summary>
        static readonly Rect CaptionStandIn = Rect.MinMaxRect(0.42f, 0.49f, 0.58f, 0.51f);

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The ghost's grey (MarkCode's: the dim ink, a little faded), as text takes it.</summary>
        static readonly Color GhostText = WithAlpha(Roles.Encoded(Roles.InkDim), 0.8f);

        /// <summary>The stress test's own group (an owner no layer uses).</summary>
        static readonly object StressOwner = new object();

        GraphRoot root;
        HudBlocks hud;

        /// <summary>Clicks on the stress set's door (its action counts them).</summary>
        int doorClicks;

        /// <summary>
        /// The emphasis of a part for the labels bound to it (<see cref="HillLabel.Part"/>): its alpha, read on the main thread
        /// every frame (<see cref="LabelSystem.PartAlpha"/>); null counts every part at 1. WP13 wires it to the emphasis table.
        /// </summary>
        public static Func<string, float> PartAlpha
        {
            get => LabelSystem.PartAlpha;
            set => LabelSystem.PartAlpha = value;
        }

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            // the leaders in the ink's rule (spec 3.2), as sprites take it
            root.Labels.LeaderColor = Roles.Encoded(Roles.InkRule);
            if (!Environment.GetCommandLineArgs().Contains("-smoketest")) return;
            SmokeChecks.Register(CheckName, Check);
            SmokeChecks.RegisterSequence(StressName, Stress());
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            // the HUD's blocks, the economy's panels and the tour's card, measured every frame the labels are laid out: found
            // by name once every module has built its canvas
            hud = new HudBlocks(root);
            Camera cam = root.Rig.Cam;
            root.Labels.HudRects = rects => hud.ScreenRects(rects, cam);
        }

        void OnDestroy()
        {
            if (root != null && root.Labels != null) root.Labels.HudRects = null;
        }

        // ------------------------------------------------------------------ the adapter

        /// <summary>
        /// Thread safe: makes <paramref name="labels"/> the whole of <paramref name="owner"/>'s labels in this scene from the
        /// next frame on (<see cref="LabelSystem.Replace"/>): call it from the layer's build, every time it rebuilds. Returns
        /// the labels put on screen (a judged label under 0.5 is not), each also on its <see cref="HillLabel.Spec"/>.
        /// </summary>
        public static IReadOnlyList<LabelSpec> Set(object owner, IEnumerable<HillLabel> labels) => Set(GraphRoot.Instance?.Labels, owner, labels);

        /// <summary>Thread safe: as <see cref="Set(object, IEnumerable{HillLabel})"/>, in a given label system (a layer's <c>ctx.Labels</c>).</summary>
        public static IReadOnlyList<LabelSpec> Set(LabelSystem system, object owner, IEnumerable<HillLabel> labels)
        {
            List<LabelSpec> specs = new List<LabelSpec>();
            if (labels != null)
            {
                foreach (HillLabel l in labels)
                {
                    if (l == null) continue;
                    l.Spec = Spec(l);
                    if (l.Spec != null) specs.Add(l.Spec);
                }
            }

            if (!(system is null)) system.Replace(owner, specs);
            return specs;
        }

        /// <summary>Thread safe: takes <paramref name="owner"/>'s labels off the screen from the next frame on.</summary>
        public static void Clear(object owner) => Clear(GraphRoot.Instance?.Labels, owner);

        /// <summary>Thread safe: as <see cref="Clear(object)"/>, in a given label system.</summary>
        public static void Clear(LabelSystem system, object owner)
        {
            if (!(system is null)) system.Clear(owner);
        }

        /// <summary>
        /// The label system's spec of a hill label (any thread): a role label at a world position, its text in the mark code's
        /// form; null when the mark code draws nothing (a judgment under 0.5: spec 3.3) or there is nothing to show.
        /// </summary>
        public static LabelSpec Spec(HillLabel l)
        {
            if (l == null || (string.IsNullOrEmpty(l.Text) && string.IsNullOrEmpty(l.Icon))) return null;
            string text = l.Text ?? "";
            Color color = l.Color;
            switch (l.Provenance)
            {
                case Mark.Judged:
                    // the panel lists it as "not drawn: too uncertain" (MarkCode.NotDrawn)
                    if (!MarkCode.Drawn(l.Confidence)) return null;
                    if (text.Length > 0) text = MarkCode.Judged(text, l.Confidence);
                    break;
                case Mark.Modelled:
                    if (text.Length > 0 && text.IndexOf('≈') < 0) text = "≈" + text;
                    if (l.Runs > 0 && text.IndexOf("n = ", StringComparison.Ordinal) < 0) text += " (n = " + l.Runs.ToString("N0", Ci) + ")";
                    break;
                case Mark.Ghost:
                    color = GhostText;
                    break;
            }

            LabelRole role = l.Role == LabelRole.None ? LabelRole.Name : l.Role;
            return new LabelSpec
            {
                Text = text,
                Data = ToWorld(l.Frame, l.At),
                Fixed = true,
                Role = role,
                SizePx = l.SizePx > 0 ? l.SizePx : LabelSystem.RolePx(role),
                Priority = LabelSystem.RolePriority(role),
                Rank = l.Rank,
                Color = color,
                Plate = l.Plate,
                Align = l.Align,
                PixelOffset = l.PixelOffset,
                Fallbacks = l.Fallbacks,
                AnchorKey = l.AnchorKey,
                Ids = l.Ids,
                Icon = l.Icon,
                Undimmed = l.Undimmed,
                Part = l.Part,
                OnClick = l.OnClick,
                Candidates = true
            };
        }

        /// <summary>A world position from a hill label's frame (any thread).</summary>
        public static Vector3 ToWorld(HillFrame frame, Vector3 at) =>
            frame == HillFrame.Land ? EconomyStage.Land().World(at) : frame == HillFrame.Stage ? StageToWorld(at) : at;

        /// <summary>
        /// The world position of a stage point (u, v, d), as spec 4 defines the stage: origin O = Land().World(120, 0, 240),
        /// rotation R = LookRotation(-F, up) with F the land's forward, World(u, v, d) = O + R(u, v, -d); built from world
        /// vectors, so never mirrored. Until WP19's StageFrame is the one definition (any thread).
        /// </summary>
        public static Vector3 StageToWorld(Vector3 uvd)
        {
            LandFrame land = EconomyStage.Land();
            return land.World(StageOriginX, 0, StageOriginZ) + Quaternion.LookRotation(-land.Forward, Vector3.up) * new Vector3(uvd.x, uvd.y, -uvd.z);
        }

        /// <summary>The stage point (u, v, d) of a world position (the inverse of <see cref="StageToWorld"/>).</summary>
        public static Vector3 WorldToStage(Vector3 world)
        {
            LandFrame land = EconomyStage.Land();
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(-land.Forward, Vector3.up)) * (world - land.World(StageOriginX, 0, StageOriginZ));
            return new Vector3(local.x, local.y, -local.z);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------------ the smoke test

        /// <summary>
        /// The per-view label metrics (spec 3.5): after every capture, the role labels the layout placed, logged as
        /// "[Why] labels &lt;view&gt;: placed N, culled M, hidden H, moved K (L leaders), overlaps 0, min 12.0 px, max 18.0 px,
        /// under HUD 0" (in a headset with the least angle), then the other labels, and the hill layers' own world text that
        /// stands under the HUD's blocks (not yet on HillLabels: information, not a problem). Each problem with the role labels
        /// fails the run: an overlap, a label under a block, a size outside 12-22 px or under a degree in a headset.
        /// </summary>
        IEnumerable<string> Check(ViewPreset view)
        {
            List<string> problems = new List<string>();
            string line = root.Labels.Metrics(problems);
            Debug.Log($"[Why] labels {view?.Id ?? "-"}: {line}; {WorldText()}");
            return problems;
        }

        /// <summary>
        /// "world text not on the label system: N shown, K under the HUD's blocks (HillMobilityLayer 5, ...)": the scene's
        /// TextMeshPro labels in the world, besides the label system's, that show now (active, drawn, seen by the camera, not
        /// faded out), and those whose text stands under a block the role labels keep out of, by layer. Spec 3.5 removes them;
        /// until each layer moves to HillLabels this counts what it still leaves under the panels.
        /// </summary>
        string WorldText()
        {
            Camera cam = root.Rig.Cam;
            Transform own = root.Labels.transform;
            SortedDictionary<string, int> under = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int shown = 0, total = 0;
            foreach (TextMeshPro t in FindObjectsByType<TextMeshPro>())
            {
                if (!t.isActiveAndEnabled || t.transform.IsChildOf(own) || string.IsNullOrEmpty(t.text)) continue;
                Renderer r = t.renderer;
                if (r == null || !r.enabled || !r.isVisible || t.color.a * t.alpha <= 0.05f) continue;
                if (!ScreenBox(t, cam, out Rect box)) continue;
                shown++;
                // the letters' box, a slack inside the text's bounds (as the label system's blocks test letters)
                float slack = 2f * ScreenLayout.UiScale;
                if (box.width <= 2 * slack || box.height <= 2 * slack) continue;
                if (!root.Labels.UnderHud(Rect.MinMaxRect(box.xMin + slack, box.yMin + slack, box.xMax - slack, box.yMax - slack))) continue;
                GraphLayer layer = t.GetComponentInParent<GraphLayer>();
                string name = layer != null ? layer.GetType().Name : "not a layer";
                under[name] = under.TryGetValue(name, out int n) ? n + 1 : 1;
                total++;
            }

            return $"world text not on the label system: {shown} shown, {total} under the HUD's blocks" +
                   (total > 0 ? " (" + string.Join(", ", under.Select(kv => kv.Key + " " + kv.Value)) + ")" : "");
        }

        /// <summary>A world text's bounds on screen (pixels), false when it stands behind the camera.</summary>
        static bool ScreenBox(TextMeshPro t, Camera cam, out Rect box)
        {
            box = default;
            Bounds b = t.textBounds;
            if (b.size.x <= 0 || b.size.y <= 0) return false;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                Vector3 local = new Vector3(c % 2 == 0 ? b.min.x : b.max.x, c < 2 ? b.min.y : b.max.y, b.center.z);
                Vector3 p = cam.WorldToScreenPoint(t.transform.TransformPoint(local));
                if (p.z <= 0) return false;
                x0 = Mathf.Min(x0, p.x);
                y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x);
                y1 = Mathf.Max(y1, p.y);
            }

            box = Rect.MinMaxRect(x0, y0, x1, y1);
            return true;
        }

        /// <summary>
        /// <c>-smokeextra labels-stress</c> (spec 3.5's acceptance): 300 role labels over the overview, of every role, some
        /// judged, modelled, plated, oversized or undersized, in the land's, the stage's and the world's frames, three parts
        /// (one hidden); stand-ins for the tour's card at stop 13 and the 1983 replay readout as HUD blocks, and for the
        /// director's caption plate as a block that moves labels aside. The group is replaced 100 times from a worker thread
        /// (its count logged before and after), the frame is captured (economy-labels-stress, with every check) and its metrics
        /// logged: overlaps 0, min at least 12 px, max at most 22 px, under HUD 0. Then the label pixels are read back: none
        /// may exceed its LDR colour by more than 5% (no bloom halo); a click on the stress set's door runs its action; the
        /// group is cleared and the label count is back where it was.
        /// </summary>
        IEnumerator Stress()
        {
            LabelSystem labels = root.Labels;
            Func<string, float> parts = LabelSystem.PartAlpha;
            GameObject standIns = null;
            int before = labels.Count;
            try
            {
                root.Focus(ViewPresets.Get("overview"), 0.6f);
                Highlighter.Clear();
                yield return new WaitForSecondsRealtime(SettleSeconds);

                // the parts at spec 3.1's levels (subject 1, support 0.45) and one under the 0.3 floor
                LabelSystem.PartAlpha = key => key == StressHidden ? 0.2f : key == StressSupport ? 0.45f : 1f;
                standIns = StandInPanels();
                Camera cam = root.Rig.Cam;
                float depth = cam.WorldToScreenPoint(root.Rig.Pose.Target).z;
                List<HillLabel> set = StressSet(cam, depth > cam.nearClipPlane ? depth : root.Rig.Pose.Distance, () => doorClicks++);
                Set(labels, StressOwner, set);
                yield return null;
                int drawn = set.Count(l => l.Spec != null), once = labels.Count, heldOnce = labels.Held(StressOwner);

                // replaced again and again from a worker thread, as a layer's build would, a little apart so that the
                // replacements land across frames, between the label system's flushes
                int firstFrame = Time.frameCount;
                Task replacing = Task.Run(() =>
                {
                    for (int i = 0; i < StressReplacements; i++)
                    {
                        Set(labels, StressOwner, set);
                        Thread.Sleep(ReplaceGapMs);
                    }
                });
                while (!replacing.IsCompleted) yield return null;
                if (replacing.IsFaulted) Debug.LogError("[Why] labels-stress: replacing the group from a worker thread failed: " + replacing.Exception);
                yield return null;
                int after = labels.Count, held = labels.Held(StressOwner), group = labels.GroupCount(StressOwner);
                Debug.Log($"[Why] labels-stress: a group of {drawn} labels replaced {StressReplacements} times from a worker thread over " +
                          $"{Time.frameCount - firstFrame} frames: the system holds {heldOnce} of its labels after the first and {held} after " +
                          $"the last ({group} in the group); {once} labels in all after the first, {after} after the last ({before} without it)");
                if (heldOnce != drawn || held != drawn || group != drawn)
                    Debug.LogError($"[Why] labels-stress: replacing the group left {held} of its labels in the system, not {drawn}");

                yield return new WaitForSecondsRealtime(FadeSeconds);
                // the capture runs every check, the labels' too (which logs its problems as errors); the metrics again here, named
                yield return SmokeChecks.Capture($"{GraphScene.Current}-{StressName}", root.CurrentPreset);
                string line = labels.Metrics(new List<string>());
                Debug.Log($"[Why] labels-stress: {set.Count} labels over the overview: {line}");

                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Ldr(shot, labels, set);
                }
                finally
                {
                    Destroy(shot);
                }

                Door(labels, set[DoorIndex]);
            }
            finally
            {
                Clear(labels, StressOwner);
                LabelSystem.PartAlpha = parts;
                if (standIns != null) Destroy(standIns);
            }

            // the clear above ran at the end of a frame (after its capture's WaitForEndOfFrame), past that frame's flush: the
            // group leaves at the next frame's LateUpdate, which the check must wait for
            yield return null;
            yield return new WaitForEndOfFrame();
            int left = labels.Held(StressOwner);
            Debug.Log($"[Why] labels-stress: the group cleared: {left} of its labels left, {labels.Count} labels in all ({before} before it)");
            if (left != 0 || labels.GroupCount(StressOwner) != 0) Debug.LogError($"[Why] labels-stress: clearing the group left {left} of its labels");
            root.Focus(ViewPresets.Get("overview"), 0f);
        }

        /// <summary>
        /// The stress set: one label a cell of a 20 by 15 grid over the frame (rows counted up from the bottom), its point moved
        /// within the cell by a fixed jitter (the capture repeats from run to run), at the depth of the view's target. Roles in
        /// turn; every sixth judged (J 0.74), every thirteenth judged too uncertain to draw (J 0.40), every eighth modelled;
        /// every third plated; parts in turn (one in seven hidden). Three stand where the HUD leaves room (row 9 or 11, left
        /// of the middle and right of it, clear of the title, the stand-in card and the bar): two names asking for 8 and 30 px
        /// (drawn at 12 and 22) and a door that counts its clicks. One in three is given in the land's frame and one in five of
        /// the rest in the stage's, so both conversions are run both ways.
        /// </summary>
        static List<HillLabel> StressSet(Camera cam, float depth, Action door)
        {
            LabelRole[] roles = { LabelRole.Title, LabelRole.Name, LabelRole.Value, LabelRole.Note };
            Color[] inks =
            {
                Roles.Encoded(Roles.InkText), Roles.Encoded(Roles.YearShown), Roles.Encoded(Roles.Commerce), Roles.Encoded(Roles.InkDim),
                Roles.Encoded(Roles.Attention)
            };
            LandFrame land = EconomyStage.Land();
            List<HillLabel> set = new List<HillLabel>(StressCount);
            for (int i = 0; i < StressCount; i++)
            {
                int column = i % StressColumns, row = i / StressColumns % StressRows;
                float x = (column + 0.5f + 0.6f * (Jitter(i, 1) - 0.5f)) / StressColumns * cam.pixelWidth;
                float y = (row + 0.5f + 0.6f * (Jitter(i, 2) - 0.5f)) / StressRows * cam.pixelHeight;
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(x, y, depth));
                LabelRole role = roles[i % roles.Length];
                string n = (i + 1).ToString("000", Ci);
                HillLabel l = new HillLabel
                {
                    Text = role == LabelRole.Title ? "STRESS TITLE " + n
                        : role == LabelRole.Name ? "Name " + n
                        : role == LabelRole.Value ? "$" + (1 + i % 9) + "." + (i % 100).ToString("00", Ci) + "T a year"
                        : "note " + n + ", assumed",
                    Role = role,
                    Rank = StressCount - i,
                    Color = inks[i % inks.Length],
                    Part = i % 7 == 3 ? StressHidden : i % 5 == 4 ? StressSupport : StressSubject,
                    Plate = i % 3 == 0 ? Roles.Encoded(Roles.InkBacking) : default
                };
                if (i % 6 == 5)
                {
                    l.Provenance = Mark.Judged;
                    l.Confidence = 0.74f;
                }

                if (i % 13 == 12)
                {
                    l.Provenance = Mark.Judged;
                    l.Confidence = 0.4f;
                }
                else if (i % 8 == 7)
                {
                    l.Provenance = Mark.Modelled;
                    l.Runs = 400;
                }

                if (i == SmallIndex || i == LargeIndex)
                {
                    l.Text = "Name " + n + (i == SmallIndex ? " asks for 8 px" : " asks for 30 px");
                    l.Role = LabelRole.Name;
                    l.SizePx = i == SmallIndex ? 8 : 30;
                    l.Rank = 2 * StressCount;
                    l.Part = StressSubject;
                    l.Provenance = Mark.Measured;
                }

                if (i == DoorIndex)
                {
                    l.Text = "Door: one year, all at once ›";
                    l.Role = LabelRole.Title;
                    l.Rank = 3 * StressCount;
                    l.Part = StressSubject;
                    l.Provenance = Mark.Measured;
                    l.OnClick = door;
                }

                if (i % 3 == 1)
                {
                    l.Frame = HillFrame.Land;
                    l.At = land.Local(world);
                }
                else if (i % 5 == 2)
                {
                    l.Frame = HillFrame.Stage;
                    l.At = WorldToStage(world);
                }
                else
                {
                    l.Frame = HillFrame.World;
                    l.At = world;
                }

                set.Add(l);
            }

            return set;
        }

        /// <summary>A fixed pseudo-random fraction for label i (0-1), so the stress grid's jitter repeats.</summary>
        static float Jitter(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>
        /// The stand-in panels on a canvas of their own: the tour's card and the replay's readout registered with the HUD's
        /// blocks (<see cref="HudBlocks.Register"/>), the caption plate with the blocks that move labels aside. Each says what
        /// it stands for. Destroyed at the end of the sequence (the registries drop destroyed blocks).
        /// </summary>
        GameObject StandInPanels()
        {
            Canvas canvas = UiFactory.CreateCanvas("Labels stress stand-ins", 45, transform);
            foreach ((string name, Rect frame) in StandIns) HudBlocks.Register(StandIn(canvas.transform, name, frame, 0.9f));
            LabelSystem.MoveAside(StandIn(canvas.transform, "Caption plate (stand-in)", CaptionStandIn, 0.95f));
            return canvas.gameObject;
        }

        /// <summary>A stand-in panel in the dark of the director's caption plate (DirectorMode's CaptionPlateColor), named on its face.</summary>
        static RectTransform StandIn(Transform canvas, string name, Rect frame, float alpha)
        {
            Image panel = UiFactory.Panel(canvas, name, new Color(0.02f, 0.027f, 0.047f, alpha), false);
            RectTransform rt = panel.rectTransform;
            // fractions from the frame's top-left; anchors count up from its bottom-left
            rt.anchorMin = new Vector2(frame.xMin, 1 - frame.yMax);
            rt.anchorMax = new Vector2(frame.xMax, 1 - frame.yMin);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            TextMeshProUGUI text = UiFactory.Text(rt, "Name", name, 12, GraphStyle.TextDim, TextAlignmentOptions.Center);
            text.rectTransform.Fill(4, 2, 4, 2);
            text.raycastTarget = false;
            return rt;
        }

        /// <summary>
        /// The capture's label pixels against their colours (spec 3.5: label pixels never exceed their LDR colour by more than
        /// 5%, so no bloom halo): in each plated stress label of the subject part (its plate at full alpha keeps the frame's own
        /// bright marks out) the brightest channel of any pixel in its letters over the label's own, where that channel is
        /// bright enough to compare.
        /// </summary>
        static void Ldr(Texture2D shot, LabelSystem labels, List<HillLabel> set)
        {
            // a support part's label fades with its plate, which then lets the frame's bright marks through: subjects only
            HashSet<LabelSpec> plated = new HashSet<LabelSpec>(set.Where(l => l.Spec != null && l.Plate.a > 0 && l.Part == StressSubject).Select(l => l.Spec));
            List<(LabelSpec spec, Rect letters, float em)> shown = new List<(LabelSpec, Rect, float)>();
            labels.Shown(shown);
            Color32[] px = shot.GetPixels32();
            float worst = 0;
            string worstText = null;
            int measured = 0;
            foreach ((LabelSpec spec, Rect letters, float _) in shown)
            {
                if (!plated.Contains(spec)) continue;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(letters.xMin), 0, shot.width - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(letters.xMax), 0, shot.width - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(letters.yMin), 0, shot.height - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(letters.yMax), 0, shot.height - 1);
                Color c = spec.Color;
                float ratio = 0;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Color32 p = px[y * shot.width + x];
                        ratio = Mathf.Max(ratio, Over(p.r, c.r), Over(p.g, c.g), Over(p.b, c.b));
                    }
                }

                measured++;
                if (ratio <= worst) continue;
                worst = ratio;
                worstText = spec.Text;
            }

            Debug.Log($"[Why] labels-stress: label pixels reach {worst.ToString("0.00", Ci)} of their colour at most (limit " +
                      $"{LdrLimit.ToString("0.00", Ci)}) in {measured} plated labels" + (worstText != null ? $" ('{worstText}')" : ""));
            if (measured == 0) Debug.LogError("[Why] labels-stress: no plated label was shown to read its pixels");
            if (worst > LdrLimit) Debug.LogError($"[Why] labels-stress: '{worstText}' is drawn {worst.ToString("0.00", Ci)} times its colour: a bloom halo");
        }

        /// <summary>A pixel's channel over a label colour's (sRGB), with 8-bit rounding forgiven; 0 where the colour is too dark to compare.</summary>
        static float Over(byte pixel, float colour) =>
            colour < 0.4f ? 0 : Mathf.Max(0, pixel / 255f - LdrRounding) / colour;

        /// <summary>A click at the door's place runs its action once (<see cref="LabelSystem.ClickAt"/>; the smoke test has no pointer).</summary>
        void Door(LabelSystem labels, HillLabel door)
        {
            List<(LabelSpec spec, Rect letters, float em)> shown = new List<(LabelSpec, Rect, float)>();
            labels.Shown(shown);
            int at = shown.FindIndex(s => s.spec == door.Spec);
            if (door.Spec == null || at < 0)
            {
                Debug.LogWarning("[Why] labels-stress: the door was not placed, so its click was not tried");
                return;
            }

            int was = doorClicks;
            bool ran = labels.ClickAt(shown[at].letters.center);
            Debug.Log($"[Why] labels-stress: a click on '{door.Spec.Text}' " + (ran && doorClicks == was + 1 ? "ran its action" : "did not run its action"));
            if (!ran || doorClicks != was + 1) Debug.LogError("[Why] labels-stress: a click on the door did not run its action once");
        }
    }
}
