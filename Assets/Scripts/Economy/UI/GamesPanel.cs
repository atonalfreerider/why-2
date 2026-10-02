using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The games station's controls, shown while the "games" or "tribes" view is on (and never during the tour): the
    /// strategies of Person A and Person B, the chance of a mistake, and the chance of meeting again (the shadow of the
    /// future), each stepped with arrows, and one line on how each chosen strategy plays. Every change goes through
    /// <see cref="EconomyState.SetGames"/>, so the games station restarts both games and recomputes the evolution.
    ///
    /// Landscape: a framed panel at the right edge, vertically centered (clear of the title top-left, the legend
    /// bottom-left, the tour and help buttons top-right and the preset bar along the bottom). Portrait: a full-width
    /// sheet docked under the HUD's title (above the station's headings; the bottom of a portrait screen belongs to the
    /// HUD's preset rows and legend), two steppers per row and nothing else: with the rules the sheet would reach down
    /// over the station's headings.
    /// </summary>
    [GraphScenes("economy-retired")]   // retired by the landscape (WP0); deleted by WP5
    public sealed class GamesPanel : GraphModule
    {
        /// <summary>Above the HUD (40), below the director's tour panel (50) and the HUD's overlay (200).</summary>
        const int SortingOrder = 45;

        /// <summary>Landscape panel width; row and arrow sizes; the name column of a stepper (reference pixels).</summary>
        const float LandscapeWidth = 336f;

        const float RowHeight = 28f, RowGap = 5f, ArrowWidth = 24f, NameWidth = 86f, TitleHeight = 18f;

        /// <summary>
        /// Height of the HUD's title block (brand, title, two subtitle lines; see Hud), which the portrait sheet docks
        /// under.
        /// </summary>
        const float HudTitleHeight = 90f;

        /// <summary>Name column of the portrait steppers: strategies ("A", "B") and numbers ("Mistakes", "Meet again").</summary>
        const float PortraitStrategyName = 14f, PortraitNumberName = 74f;

        const float FadeSpeed = 4f;

        /// <summary>The views the panel belongs to.</summary>
        static readonly string[] Views = { "games", "tribes" };

        /// <summary>Choices of the chance of a mistake and of meeting again.</summary>
        static readonly float[] NoiseSteps = { 0f, 0.01f, 0.02f, 0.05f, 0.1f, 0.2f };

        static readonly float[] ContinuationSteps = { 0f, 0.5f, 0.67f, 0.8f, 0.9f, 0.95f, 0.99f };

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>A labeled value with an arrow on each side.</summary>
        sealed class Stepper
        {
            public string FullName;
            public RectTransform Rect;
            public TextMeshProUGUI Name, Value;
            public Button Prev, Next;
        }

        enum Setting
        {
            StrategyA,
            StrategyB,
            Noise,
            Continuation
        }

        GraphRoot root;
        RectTransform canvasRect, panel;
        TextMeshProUGUI title, ruleA, ruleB, hint;
        Stepper[] steppers;
        UiFade fade;
        bool loaded, inView;
        int seenVersion = -1, laidOutVersion = -1;
        Vector2 laidOutSize;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("GamesPanel", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = HudKit.FramedPanel(canvasRect, "Games", 0.86f, true);

            title = HudKit.Line(panel, "Title", "THE PRISONER'S DILEMMA", HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Bold);
            title.characterSpacing = 12;
            steppers = new[]
            {
                AddStepper("Person A", Setting.StrategyA),
                AddStepper("Person B", Setting.StrategyB),
                AddStepper("Mistakes", Setting.Noise),
                AddStepper("Meet again", Setting.Continuation)
            };
            ruleA = UiFactory.Text(panel, "RuleA", "", HudKit.SizeSmall, GraphStyle.TextDim);
            ruleB = UiFactory.Text(panel, "RuleB", "", HudKit.SizeSmall, GraphStyle.TextDim);
            hint = HudKit.Line(panel, "Hint", "Changes restart both games and the evolution.", HudKit.SizeSmall,
                HudKit.TextFaint);

            fade = new UiFade(panel.gameObject, 0, FadeSpeed, true);
            root.FocusChanged += OnFocusChanged;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            loaded = true;
            OnFocusChanged(root.CurrentPreset);
            Sync();
        }

        void OnDestroy()
        {
            if (root != null) root.FocusChanged -= OnFocusChanged;
        }

        void OnFocusChanged(ViewPreset preset) => inView = preset != null && Array.IndexOf(Views, preset.Id) >= 0;

        Stepper AddStepper(string name, Setting setting)
        {
            Stepper s = new Stepper { FullName = name, Rect = UiFactory.Rect(panel, name) };
            s.Name = HudKit.Line(s.Rect, "Name", name, HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Normal,
                TextAlignmentOptions.Left);
            s.Prev = HudKit.Button(s.Rect, "Prev", "<", HudKit.SizeBody, () => Step(setting, -1));
            s.Value = HudKit.Line(s.Rect, "Value", "", HudKit.SizeBody, GraphStyle.Text, FontStyles.Normal,
                TextAlignmentOptions.Center);
            s.Next = HudKit.Button(s.Rect, "Next", ">", HudKit.SizeBody, () => Step(setting, 1));
            return s;
        }

        // ------------------------------------------------------------------ changes

        /// <summary>Moves one setting a step and hands all four to the shared state (which tells the games station).</summary>
        static void Step(Setting setting, int direction)
        {
            PdStrategy a = EconomyState.StrategyA, b = EconomyState.StrategyB;
            float noise = EconomyState.Noise, continuation = EconomyState.Continuation;
            switch (setting)
            {
                case Setting.StrategyA:
                    a = Cycle(a, direction);
                    break;
                case Setting.StrategyB:
                    b = Cycle(b, direction);
                    break;
                case Setting.Noise:
                    noise = NoiseSteps[StepIndex(NoiseSteps, noise, direction)];
                    break;
                default:
                    continuation = ContinuationSteps[StepIndex(ContinuationSteps, continuation, direction)];
                    break;
            }

            EconomyState.SetGames(a, b, noise, continuation);
        }

        /// <summary>The next strategy in the engine's order, wrapping around.</summary>
        static PdStrategy Cycle(PdStrategy s, int direction)
        {
            PdStrategy[] all = PrisonersDilemma.All;
            int i = Array.IndexOf(all, s);
            return all[((i < 0 ? 0 : i) + direction + all.Length) % all.Length];
        }

        /// <summary>The choice next to the current value (the nearest choice when the value is not one), clamped.</summary>
        static int StepIndex(float[] steps, float value, int direction) =>
            Mathf.Clamp(Nearest(steps, value) + direction, 0, steps.Length - 1);

        static int Nearest(float[] steps, float value)
        {
            int best = 0;
            for (int i = 1; i < steps.Length; i++)
            {
                if (Mathf.Abs(steps[i] - value) < Mathf.Abs(steps[best] - value)) best = i;
            }

            return best;
        }

        /// <summary>Shows the shared state's current choices.</summary>
        void Sync()
        {
            seenVersion = EconomyState.Version;
            PdStrategy a = EconomyState.StrategyA, b = EconomyState.StrategyB;
            steppers[0].Value.text = PrisonersDilemma.Name(a);
            steppers[1].Value.text = PrisonersDilemma.Name(b);
            steppers[2].Value.text = Percent(EconomyState.Noise);
            steppers[3].Value.text = Percent(EconomyState.Continuation);
            int noise = Nearest(NoiseSteps, EconomyState.Noise), again = Nearest(ContinuationSteps, EconomyState.Continuation);
            steppers[2].Prev.interactable = noise > 0;
            steppers[2].Next.interactable = noise < NoiseSteps.Length - 1;
            steppers[3].Prev.interactable = again > 0;
            steppers[3].Next.interactable = again < ContinuationSteps.Length - 1;

            string tag = "<color=#" + UiFactory.Hex(GraphStyle.Text) + ">";
            ruleA.text = tag + "A</color>  " + PrisonersDilemma.Rule(a);
            ruleB.text = tag + "B</color>  " + PrisonersDilemma.Rule(b);
            laidOutVersion = -1; // rule lengths changed: measure again
        }

        static string Percent(float v)
        {
            float p = 100 * v;
            return p.ToString(Mathf.Abs(p - Mathf.Round(p)) < 0.05f ? "0" : "0.#", Ci) + "%";
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            bool show = inView && !root.TourActive;
            if (show != fade.Shown)
            {
                fade.Show(show);
                if (show) laidOutVersion = -1; // measure the texts again now that they are active
            }

            if (EconomyState.Version != seenVersion) Sync();
        }

        void LateUpdate()
        {
            fade.Tick(Time.unscaledDeltaTime);
            if (!loaded) return;

            // on the frame the screen changes shape the canvas may still have its old size; lay out for the new one
            Vector2 size = canvasRect.rect.size;
            if (size == laidOutSize && laidOutVersion == ScreenLayout.Version) return;
            if (laidOutVersion != ScreenLayout.Version) size = UiFactory.CanvasSize;
            laidOutSize = canvasRect.rect.size;
            laidOutVersion = ScreenLayout.Version;
            if (ScreenLayout.IsPortrait) LayoutPortrait(size);
            else LayoutLandscape();
        }

        /// <summary>One column at the right edge, vertically centered.</summary>
        void LayoutLandscape()
        {
            float w = LandscapeWidth, inner = w - 2 * HudKit.Pad, y = HudKit.Pad;
            title.gameObject.SetActive(true);
            HudKit.PlaceTopLeft(title.rectTransform, HudKit.Pad, y, new Vector2(inner, TitleHeight));
            y += TitleHeight + RowGap + 2;
            ruleA.gameObject.SetActive(true);
            ruleB.gameObject.SetActive(true);
            foreach (Stepper s in steppers)
            {
                PlaceStepper(s, HudKit.Pad, y, inner, NameWidth);
                y += RowHeight + RowGap;
            }

            y += RowGap;
            y = PlaceRule(ruleA, y, inner) + 4;
            y = PlaceRule(ruleB, y, inner) + RowGap + 2;
            hint.gameObject.SetActive(true);
            HudKit.PlaceTopLeft(hint.rectTransform, HudKit.Pad, y, new Vector2(inner, 16));
            y += 16 + HudKit.Pad - 2;

            panel.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-HudKit.Margin, 0), new Vector2(w, y));
        }

        /// <summary>
        /// A full-width sheet under the HUD's title: the two strategies on one row, the two numbers on the next (the title,
        /// rules and hint stay in the landscape panel).
        /// </summary>
        void LayoutPortrait(Vector2 size)
        {
            const float m = HudKit.PortraitMargin;
            HudKit.SafeInsets(UiFactory.CanvasScale, out _, out float safeTop);
            float w = Mathf.Max(200f, size.x - 2 * m), inner = w - 2 * HudKit.Pad;
            float half = (inner - HudKit.Gap) * 0.5f, right = HudKit.Pad + half + HudKit.Gap;
            float y = HudKit.Pad - 2;
            title.gameObject.SetActive(false); // every line counts above the station's own headings
            PlaceStepper(steppers[0], HudKit.Pad, y, half, PortraitStrategyName, "A");
            PlaceStepper(steppers[1], right, y, half, PortraitStrategyName, "B");
            y += RowHeight + RowGap;
            PlaceStepper(steppers[2], HudKit.Pad, y, half, PortraitNumberName);
            PlaceStepper(steppers[3], right, y, half, PortraitNumberName);
            y += RowHeight + HudKit.Pad - 2;
            ruleA.gameObject.SetActive(false);
            ruleB.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);

            float top = m + safeTop + HudTitleHeight + HudKit.Gap;
            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(m, -top), new Vector2(w, y));
        }

        /// <summary>A stepper's name, arrows and value inside a cell of the given width (top-left at x, y).</summary>
        static void PlaceStepper(Stepper s, float x, float y, float width, float nameWidth, string shortName = null)
        {
            HudKit.PlaceTopLeft(s.Rect, x, y, new Vector2(width, RowHeight));
            s.Name.text = shortName ?? s.FullName;
            HudKit.PlaceTopLeft(s.Name.rectTransform, 0, 0, new Vector2(nameWidth, RowHeight));
            s.Name.alignment = TextAlignmentOptions.MidlineLeft;
            float valueWidth = width - nameWidth - 2 * ArrowWidth - 8;
            HudKit.PlaceTopLeft((RectTransform)s.Prev.transform, nameWidth + 2, 2, new Vector2(ArrowWidth, RowHeight - 4));
            HudKit.PlaceTopLeft(s.Value.rectTransform, nameWidth + ArrowWidth + 4, 0, new Vector2(valueWidth, RowHeight));
            s.Value.alignment = TextAlignmentOptions.Midline;
            HudKit.PlaceTopLeft((RectTransform)s.Next.transform, width - ArrowWidth, 2, new Vector2(ArrowWidth, RowHeight - 4));
        }

        /// <summary>A strategy's rule below y, wrapped to the width; returns the y below it.</summary>
        static float PlaceRule(TextMeshProUGUI rule, float y, float width)
        {
            rule.textWrappingMode = TextWrappingModes.Normal;
            rule.overflowMode = TextOverflowModes.Overflow;
            float height = HudKit.Measure(rule, rule.text, width).y;
            HudKit.PlaceTopLeft(rule.rectTransform, HudKit.Pad, y, new Vector2(width, height));
            return y + height;
        }
    }
}
