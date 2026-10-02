using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The season's controls (SPEC 5.6), shown in the society and betrayal views (never during the tour): play / pause,
    /// one round forward and replay (<see cref="LandView"/>); the season's settings, each stepped with arrows (forgiveness
    /// by the higher OS, by everyone or by nobody; the chance of a mistake; the chance of meeting again; polarization;
    /// partner choice), every change rerunning the season on a worker through <see cref="EconomyState.SetSocial"/> while
    /// playback goes on at the current round; Betray, which makes the selected tie's player betray its partner at the
    /// next round (<see cref="EconomyState.QueueIncident"/>); and the generated readout of the round on screen
    /// ("Round 96 · cooperation 0.73 (opened at 0.33) · in-group 0.74 / out-group 0.73 · co-partisan 0.74 / cross 0.69 ·
    /// 4 coalitions") with the help line on why tit for tat wins.
    /// <para>Landscape: a framed panel at the bottom-left, above the HUD's legend. Portrait: a bottom sheet over the
    /// preset bar, at most <see cref="PortraitShare"/> of the height. The box it takes is <see cref="Occupied"/> (canvas
    /// units, empty while hidden), for the economy UI's layout (WP5).</para>
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class SocialPanel : GraphModule
    {
        /// <summary>Above the HUD (40), below the director's tour panel (50) and the HUD's overlay (200).</summary>
        const int SortingOrder = 45;

        /// <summary>Landscape width; rows, arrows, the name column; the readout's and the help's widths (reference pixels).</summary>
        const float LandscapeWidth = 380f, RowHeight = 26f, RowGap = 4f, ArrowWidth = 24f, NameWidth = 118f, TitleHeight = 18f,
            ButtonGap = 6f;

        /// <summary>The portrait sheet takes at most this share of the screen's height (7.2: ≤ 38%).</summary>
        const float PortraitShare = 0.38f;

        const float FadeSpeed = 4f;

        /// <summary>The views the panel belongs to.</summary>
        static readonly string[] Views = { "society", "betrayal" };

        /// <summary>The settings' steps (5.6 ranges: mistakes 0-0.20, meeting again 0.50-0.99, polarization 0-2).</summary>
        static readonly float[] NoiseSteps = { 0f, 0.01f, 0.02f, 0.05f, 0.1f, 0.15f, 0.2f };

        static readonly float[] ContinuationSteps = { 0.5f, 0.67f, 0.8f, 0.9f, 0.95f, 0.99f };
        static readonly float[] PolarizationSteps = { 0f, 0.5f, 1f, 1.5f, 2f };
        static readonly string[] ForgiveNames = { "by the higher OS", "everyone", "nobody" };

        const string Help =
            "Tit for tat cannot be exploited by always-defect once the chance of meeting again is at least (T − R)/(T − P) = 0.5; " +
            "it never outscores its partner head to head, it wins by doing well with everyone.";

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The canvas box the panel takes while shown (empty otherwise); canvas units of a <see cref="UiFactory.CreateCanvas"/> canvas.</summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        /// <summary>A labeled value with an arrow on each side.</summary>
        sealed class Stepper
        {
            public RectTransform Rect;
            public TextMeshProUGUI Name, Value;
            public Button Prev, Next;
        }

        enum Setting
        {
            Forgive,
            Noise,
            Continuation,
            Polarization,
            Rewire
        }

        GraphRoot root;
        RectTransform canvasRect, panel;
        TextMeshProUGUI title, readout, help;
        Button play, step, replay, betray;
        TextMeshProUGUI playLabel;
        Stepper[] steppers;
        UiFade fade;
        HudBlocks hud;
        bool loaded, inView;
        int seenVersion = -1, seenRound = -1, laidOutVersion = -1;
        bool seenPlaying;
        SocialSeasonResult seenSeason;
        Vector2 laidOutSize;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("SocialPanel", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = HudKit.FramedPanel(canvasRect, "Social", 0.86f, true);

            title = HudKit.Line(panel, "Title", "A SEASON OF TIT FOR TAT", HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Bold);
            title.characterSpacing = 12;
            play = HudKit.Button(panel, "Play", "Play", HudKit.SizeSmall, TogglePlay);
            playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            step = HudKit.Button(panel, "Step", "Step", HudKit.SizeSmall, LandView.Step);
            replay = HudKit.Button(panel, "Replay", "Replay", HudKit.SizeSmall, LandView.Replay);
            betray = HudKit.Button(panel, "Betray", "Betray", HudKit.SizeSmall, Betray);
            steppers = new[]
            {
                AddStepper("Forgiveness", Setting.Forgive),
                AddStepper("Mistakes", Setting.Noise),
                AddStepper("Meeting again", Setting.Continuation),
                AddStepper("Polarization", Setting.Polarization),
                AddStepper("Partner choice", Setting.Rewire)
            };
            readout = UiFactory.Text(panel, "Readout", "", HudKit.SizeSmall, GraphStyle.Text);
            help = UiFactory.Text(panel, "Help", Help, HudKit.SizeSmall - 1, HudKit.TextFaint);

            fade = new UiFade(panel.gameObject, 0, FadeSpeed, true);
            root.FocusChanged += OnFocusChanged;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            hud = new HudBlocks(root);
            loaded = true;
            OnFocusChanged(root.CurrentPreset);
            Sync();
        }

        void OnDestroy()
        {
            if (root != null) root.FocusChanged -= OnFocusChanged;
            Occupied = UiBox.Empty;
        }

        void OnFocusChanged(ViewPreset preset) => inView = preset != null && Array.IndexOf(Views, preset.Id) >= 0;

        Stepper AddStepper(string name, Setting setting)
        {
            Stepper s = new Stepper { Rect = UiFactory.Rect(panel, name) };
            s.Name = HudKit.Line(s.Rect, "Name", name, HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            s.Prev = HudKit.Button(s.Rect, "Prev", "<", HudKit.SizeBody, () => Change(setting, -1));
            s.Value = HudKit.Line(s.Rect, "Value", "", HudKit.SizeSmall, GraphStyle.Text, FontStyles.Normal, TextAlignmentOptions.Midline);
            s.Next = HudKit.Button(s.Rect, "Next", ">", HudKit.SizeBody, () => Change(setting, 1));
            return s;
        }

        // ------------------------------------------------------------------ actions

        static void TogglePlay() => LandView.Play(!LandView.Playing);

        /// <summary>The selected tie's betrayal at the next round (the social layer runs it and plays into it).</summary>
        static void Betray()
        {
            if (EconomyState.SelectedTie >= 0) EconomyState.QueueIncident(EconomyState.SelectedTie);
        }

        /// <summary>Moves one setting a step and hands them all to the shared state (the land reruns the season on a worker).</summary>
        static void Change(Setting setting, int direction)
        {
            SocialSettings s = EconomyState.Social;
            switch (setting)
            {
                case Setting.Forgive:
                    s.Forgive = (byte)((s.Forgive + direction + 3) % 3);
                    break;
                case Setting.Noise:
                    s.Noise = NoiseSteps[StepIndex(NoiseSteps, s.Noise, direction)];
                    break;
                case Setting.Continuation:
                    s.Continuation = ContinuationSteps[StepIndex(ContinuationSteps, s.Continuation, direction)];
                    break;
                case Setting.Polarization:
                    s.Polarization = PolarizationSteps[StepIndex(PolarizationSteps, s.Polarization, direction)];
                    break;
                default:
                    s.Rewire = !s.Rewire;
                    break;
            }

            EconomyState.SetSocial(s);
        }

        static int StepIndex(float[] steps, float value, int direction) => Mathf.Clamp(Nearest(steps, value) + direction, 0, steps.Length - 1);

        static int Nearest(float[] steps, float value)
        {
            int best = 0;
            for (int i = 1; i < steps.Length; i++)
            {
                if (Mathf.Abs(steps[i] - value) < Mathf.Abs(steps[best] - value)) best = i;
            }

            return best;
        }

        // ------------------------------------------------------------------ texts

        /// <summary>Shows the shared state's settings and what the buttons can do now.</summary>
        void Sync()
        {
            seenVersion = EconomyState.Version;
            SocialSettings s = EconomyState.Social;
            steppers[0].Value.text = ForgiveNames[Mathf.Clamp(s.Forgive, 0, 2)];
            steppers[1].Value.text = Percent(s.Noise);
            steppers[2].Value.text = Percent(s.Continuation);
            steppers[3].Value.text = "×" + s.Polarization.ToString("0.#", Ci);
            steppers[4].Value.text = s.Rewire ? "on" : "off";
            int noise = Nearest(NoiseSteps, s.Noise), again = Nearest(ContinuationSteps, s.Continuation),
                polar = Nearest(PolarizationSteps, s.Polarization);
            steppers[1].Prev.interactable = noise > 0;
            steppers[1].Next.interactable = noise < NoiseSteps.Length - 1;
            steppers[2].Prev.interactable = again > 0;
            steppers[2].Next.interactable = again < ContinuationSteps.Length - 1;
            steppers[3].Prev.interactable = polar > 0;
            steppers[3].Next.interactable = polar < PolarizationSteps.Length - 1;
            betray.interactable = EconomyState.SelectedTie >= 0 && EconomyState.PendingIncident < 0;
        }

        /// <summary>
        /// "Round 96 · cooperation 0.73 (opened at 0.33) · in-group 0.74 / out-group 0.73 · co-partisan 0.74 / cross 0.69 ·
        /// 4 coalitions" for the season and round on screen, plus the betrayal when the season has one.
        /// </summary>
        static string Readout(SocialSeasonResult s, int round)
        {
            if (s?.Cooperation == null || s.Cooperation.Length == 0) return "The season is not computed yet.";
            int t = Mathf.Clamp(round, 0, s.Cooperation.Length - 1);
            string line = "Round " + t.ToString(Ci) + " · cooperation " + Num(s.Cooperation[t]) + " (opened at " + Num(s.Cooperation[0]) +
                          ") · in-group " + Num(s.SameGroup[t]) + " / out-group " + Num(s.OtherGroup[t]) + " · co-partisan " +
                          Num(s.CoPartisan[t]) + " / cross " + Num(s.CrossPartisan[t]) + " · " +
                          SocialLayer.CoalitionsAt(s, t).ToString(Ci) + " coalitions";
            if (s.Incident.HasValue)
            {
                Incident inc = s.Incident.Value;
                line += "\nA betrayal in round " + inc.Round.ToString(Ci) + ": " + s.Hit.ToString(Ci) + " players hit, calm after " +
                        s.CalmAfter.ToString(Ci) + " rounds.";
            }

            return line;
        }

        static string Num(float v) => v.ToString("0.00", Ci);

        static string Percent(float v)
        {
            float p = 100 * v;
            return p.ToString(Mathf.Abs(p - Mathf.Round(p)) < 0.05f ? "0" : "0.#", Ci) + "%";
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            bool show = inView && !root.TourActive && SocialLayer.Shown != null;
            if (show != fade.Shown)
            {
                fade.Show(show);
                if (show) laidOutVersion = -1;   // measure the texts again now that they are active
            }

            if (EconomyState.Version != seenVersion) Sync();
            if (LandView.Playing != seenPlaying)
            {
                seenPlaying = LandView.Playing;
                playLabel.text = seenPlaying ? "Pause" : "Play";
            }

            SocialSeasonResult season = SocialLayer.Shown;
            if (LandView.Round != seenRound || !ReferenceEquals(season, seenSeason))
            {
                seenRound = LandView.Round;
                seenSeason = season;
                string text = Readout(season, seenRound);
                if (readout.text != text)
                {
                    bool lines = readout.text.Contains("\n") != text.Contains("\n");
                    readout.text = text;
                    if (lines) laidOutVersion = -1;
                }
            }
        }

        void LateUpdate()
        {
            fade.Tick(Time.unscaledDeltaTime);
            if (!loaded || !fade.Shown && fade.Alpha <= 0.001f)
            {
                Occupied = UiBox.Empty;
                return;
            }

            Vector2 size = canvasRect.rect.size;
            if (size == laidOutSize && laidOutVersion == ScreenLayout.Version) return;
            if (laidOutVersion != ScreenLayout.Version) size = UiFactory.CanvasSize;
            laidOutSize = canvasRect.rect.size;
            laidOutVersion = ScreenLayout.Version;
            HudFrame f = hud.Measure(canvasRect, size);
            Occupied = ScreenLayout.IsPortrait ? LayoutPortrait(f) : LayoutLandscape(f);
        }

        /// <summary>One column at the bottom-left, above the HUD's legend (or the screen's bottom margin).</summary>
        UiBox LayoutLandscape(HudFrame f)
        {
            float w = LandscapeWidth, inner = w - 2 * HudKit.Pad;
            float h = Content(inner, true);
            float bottom = !f.Legend.IsEmpty ? f.Legend.Y - f.Gap : f.Canvas.y - f.Margin;
            if (!f.PresetBar.IsEmpty && f.PresetBar.OverlapsX(f.Margin, f.Margin + w)) bottom = Mathf.Min(bottom, f.PresetBar.Y - f.Gap);
            float top = Mathf.Max(f.Margin, bottom - h);
            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(f.Margin, -top), new Vector2(w, h));
            return new UiBox(f.Margin, top, w, h);
        }

        /// <summary>A full-width bottom sheet over the preset bar: the buttons, the five steppers two to a row, the readout.</summary>
        UiBox LayoutPortrait(HudFrame f)
        {
            float m = f.Margin, w = Mathf.Max(200f, f.Canvas.x - 2 * m), inner = w - 2 * HudKit.Pad;
            float h = Mathf.Min(Content(inner, false), PortraitShare * f.Canvas.y);
            float bottom = !f.PresetBar.IsEmpty ? f.PresetBar.Y - f.Gap : f.Canvas.y - m - f.SafeBottom;
            float top = Mathf.Max(m + f.SafeTop, bottom - h);
            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(m, -top), new Vector2(w, h));
            return new UiBox(m, top, w, h);
        }

        /// <summary>Places the panel's content in a column of the given inner width; returns the panel's height.</summary>
        float Content(float inner, bool landscape)
        {
            float y = HudKit.Pad - 2;
            title.gameObject.SetActive(landscape);
            if (landscape)
            {
                HudKit.PlaceTopLeft(title.rectTransform, HudKit.Pad, y, new Vector2(inner, TitleHeight));
                y += TitleHeight + RowGap;
            }

            // play / step / replay / betray in one row
            float bw = (inner - 3 * ButtonGap) / 4;
            Button[] row = { play, step, replay, betray };
            for (int i = 0; i < row.Length; i++)
            {
                HudKit.PlaceTopLeft((RectTransform)row[i].transform, HudKit.Pad + i * (bw + ButtonGap), y, new Vector2(bw, RowHeight));
            }

            y += RowHeight + RowGap + 2;
            if (landscape)
            {
                foreach (Stepper s in steppers)
                {
                    PlaceStepper(s, HudKit.Pad, y, inner, NameWidth);
                    y += RowHeight + RowGap;
                }
            }
            else
            {
                float half = (inner - HudKit.Gap) * 0.5f;
                for (int i = 0; i < steppers.Length; i++)
                {
                    float x = HudKit.Pad + (i % 2) * (half + HudKit.Gap);
                    PlaceStepper(steppers[i], x, y, half, Mathf.Min(NameWidth, half * 0.42f));
                    if (i % 2 == 1 || i == steppers.Length - 1) y += RowHeight + RowGap;
                }
            }

            y += RowGap;
            y = Wrapped(readout, y, inner) + RowGap;
            help.gameObject.SetActive(landscape);
            if (landscape) y = Wrapped(help, y, inner);
            return y + HudKit.Pad - 2;
        }

        /// <summary>A stepper's name, arrows and value inside a cell of the given width (top-left at x, y).</summary>
        static void PlaceStepper(Stepper s, float x, float y, float width, float nameWidth)
        {
            HudKit.PlaceTopLeft(s.Rect, x, y, new Vector2(width, RowHeight));
            HudKit.PlaceTopLeft(s.Name.rectTransform, 0, 0, new Vector2(nameWidth, RowHeight));
            float valueWidth = width - nameWidth - 2 * ArrowWidth - 8;
            HudKit.PlaceTopLeft((RectTransform)s.Prev.transform, nameWidth + 2, 2, new Vector2(ArrowWidth, RowHeight - 4));
            HudKit.PlaceTopLeft(s.Value.rectTransform, nameWidth + ArrowWidth + 4, 0, new Vector2(valueWidth, RowHeight));
            HudKit.PlaceTopLeft((RectTransform)s.Next.transform, width - ArrowWidth, 2, new Vector2(ArrowWidth, RowHeight - 4));
        }

        /// <summary>A wrapped text below y at the width; returns the y below it.</summary>
        static float Wrapped(TextMeshProUGUI t, float y, float width)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            float height = HudKit.Measure(t, t.text, width).y;
            HudKit.PlaceTopLeft(t.rectTransform, HudKit.Pad, y, new Vector2(width, height));
            return y + height;
        }
    }
}
