using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Smv;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The land legend (SPEC 0.1, 7.6), above the HUD's legend in the views of the land: the unit system of the year's land
    /// ("1 square = $1.28T a year · width 0.1 = $3.1T a year · roots ×4 · height 1 = $5.1T of market value · a dot =
    /// 100,000 people"), the color grammar (gold capital, light blue wages, blue people, steel the state, ice fear, rose
    /// desire, white fantasy, red raw matter and green life) and the accounts readout of the year's money circuit ("GDP
    /// $30.8T · the circuit balances within 1.1% (tier:raw)" and the first lines of its notes: the approximations and
    /// residuals are printed, never hidden behind the headline). Generated from the snapshot on screen
    /// (<see cref="LandLegendFacts"/>), refreshed when it changes.
    /// <para>Landscape: a column at the bottom-left above the HUD's legend and lifeline readout (the social panel stands
    /// above it). Portrait: the same place across the width, compact (no swatches, the HUD's legend has them; no notes, the
    /// year chip's tooltip has them). Wherever the bowl leaves the year's readout no place under the chip (a portrait
    /// screen, or a view whose bowl reaches the top right) the legend shows that readout first. It
    /// steps aside while a bottom sheet is up (the social panel or an inspector). Hidden on the road views (the overview
    /// only shows it to carry a displaced readout) and during the
    /// tour. Its box is <see cref="Occupied"/>.</para>
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class LandLegend : GraphModule
    {
        /// <summary>With the economy's controls above the HUD (40), below the social panel (45) and the tour (50).</summary>
        const int SortingOrder = 43;

        const float SwatchSize = 9f, SwatchGap = 5f, ItemGap = 12f, RowGap = 4f, FadeSpeed = 4f;

        /// <summary>The color grammar: name and color of each swatch.</summary>
        static readonly (string name, Color color)[] Grammar =
        {
            ("capital", EconomyStyle.Capital), ("wages", EconomyStyle.Wages), ("people", EconomyStyle.People), ("state", EconomyStyle.State),
            ("fear", EconomyStyle.Fear), ("desire", EconomyStyle.Desire), ("fantasy", EconomyStyle.Fantasy), ("raw matter", EconomyStyle.Matter),
            ("life", EconomyStyle.Life)
        };

        /// <summary>The canvas box the legend takes while shown (empty otherwise).</summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        GraphRoot root;
        RectTransform canvasRect, panel;
        TextMeshProUGUI heading, readout, units, accounts, notes;
        YearSeries trust;

        /// <summary>Laid out compact: no swatches or notes (a portrait screen with the land in view).</summary>
        bool compact;

        /// <summary>The year's readout first: the year chip has no place for it in this view (<see cref="EconomyControls.ReadoutMoved"/>).</summary>
        bool withReadout;
        readonly List<Image> swatches = new List<Image>();
        readonly List<TextMeshProUGUI> names = new List<TextMeshProUGUI>();
        UiFade fade;
        HudBlocks hud;
        SmvPopulation pop;
        bool loaded, inView, contentDirty = true;
        int seenLand = -1, seenState = -1;
        HudFrame laidOutFrame;
        float laidOutWidth;
        string loggedProblems = "";
        readonly List<string> problems = new List<string>();

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("LandLegend", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = HudKit.FramedPanel(canvasRect, "LandLegend", 0.62f);
            heading = HudKit.Line(panel, "Heading", "", HudKit.SizeSmall - 1, HudKit.TextFaint, FontStyles.Bold);
            heading.characterSpacing = 10;
            readout = UiFactory.Text(panel, "Readout", "", HudKit.SizeSmall, GraphStyle.TextDim);
            units = UiFactory.Text(panel, "Units", "", HudKit.SizeSmall, GraphStyle.Text);
            foreach ((string name, Color color) in Grammar)
            {
                Image dot = UiFactory.Panel(panel, name + " swatch", color, false);
                dot.pixelsPerUnitMultiplier = 14f / (SwatchSize * 0.5f);
                swatches.Add(dot);
                names.Add(HudKit.Line(panel, name, name, HudKit.SizeSmall - 0.5f, GraphStyle.TextDim));
            }

            accounts = UiFactory.Text(panel, "Accounts", "", HudKit.SizeSmall - 0.5f, GraphStyle.TextDim);
            notes = UiFactory.Text(panel, "Notes", "", HudKit.SizeSmall - 1.5f, HudKit.TextFaint);
            fade = new UiFade(panel.gameObject, 0, FadeSpeed, false);
            root.FocusChanged += OnFocusChanged;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            trust = LandService.Model?.Data?.Games?.Tribes?.TrustSeries();
            hud = new HudBlocks(root);
            loaded = true;
            OnFocusChanged(root.CurrentPreset);
        }

        void OnDestroy()
        {
            if (root != null) root.FocusChanged -= OnFocusChanged;
            Occupied = UiBox.Empty;
        }

        void OnFocusChanged(ViewPreset preset) => inView = EconomyControls.IsLandView(preset);

        void Update()
        {
            if (!loaded) return;
            // a portrait bottom sheet covers the legend's place: the legend steps aside while one is up
            bool sheet = ScreenLayout.IsPortrait && (!SocialPanel.Occupied.IsEmpty || PlayerPanel.SheetShown || PersonInspector.SheetShown);
            // the overview shows it only to carry the year's readout when the bowl took the readout's place
            bool view = inView || EconomyControls.ReadoutMoved && root.CurrentPreset?.Id == "overview";
            bool show = view && !root.TourActive && LandService.Current != null && !sheet;
            if (show != fade.Shown)
            {
                fade.Show(show); // activates the panel before its texts are measured
                if (show) contentDirty = true;
            }

            if (LandService.Version != seenLand || EconomyState.Version != seenState)
            {
                seenLand = LandService.Version;
                seenState = EconomyState.Version;
                contentDirty = true;
            }
        }

        void LateUpdate()
        {
            fade.Tick(Time.unscaledDeltaTime);
            if (!loaded || !fade.Shown)
            {
                if (fade.Alpha <= 0.001f) Occupied = UiBox.Empty;
                return;
            }

            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            f.LandLegend = UiBox.Empty;
            float width = f.Portrait ? f.Canvas.x - 2 * f.Margin : EconomyUiLayout.LeftColumnWidth;
            bool wantCompact = EconomyUiLayout.ReadoutInLegend(f), wantReadout = EconomyControls.ReadoutMoved;
            if (wantCompact != compact || wantReadout != withReadout) contentDirty = true;
            if (!contentDirty && f.Near(laidOutFrame) && Mathf.Abs(width - laidOutWidth) < 0.5f) return;
            compact = wantCompact;
            withReadout = wantReadout;
            if (contentDirty) Fill();
            contentDirty = false;
            laidOutFrame = f;
            laidOutWidth = width;
            float height = Content(width);
            Occupied = EconomyUiLayout.LandLegendBox(f, new Vector2(width, height), out float scale);
            if (Occupied.IsEmpty)
            {
                // no room under the bowl in this view: the legend steps aside (it comes back with a wider view)
                panel.localScale = Vector3.zero;
                return;
            }

            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Occupied.X, -Occupied.Y), new Vector2(width, height));
            panel.localScale = new Vector3(scale, scale, 1);

            problems.Clear();
            f.SocialPanel = UiBox.Empty; // the social panel stands above the legend, laid out from it
            EconomyUiLayout.Check(f, "land legend", Occupied, problems);
            string text = string.Join("; ", problems);
            if (text == loggedProblems) return;
            loggedProblems = text;
            if (text.Length > 0) Debug.LogWarning("[Why] LandLegend layout: " + text);
        }

        /// <summary>The texts of the snapshot on screen.</summary>
        void Fill()
        {
            LandSnapshot s = LandService.Current;
            heading.text = "THE LAND  ·  " + LandFacts.Year(s?.Year ?? EconomyState.Year,
                s != null && LandService.Model?.Data != null && LandService.Model.Data.IsEstimate(s.Year)).ToUpperInvariant();
            units.text = LandLegendFacts.Units(s, pop?.Sim?.PeoplePerLine ?? 100_000);
            accounts.text = LandLegendFacts.Accounts(s, LandService.Model?.Data);
            notes.text = LandLegendFacts.Notes(s, LandLegendFacts.NoteLines);
            readout.text = withReadout
                ? YearFacts.Line(LandService.Model?.Lives, trust, s, LandService.Model?.Data, EconomyState.Year, true, UiFactory.Hex(GraphStyle.Text))
                : "";
        }

        /// <summary>Places the heading, the units, the swatches (wrapping), the accounts and the notes; returns the height.</summary>
        float Content(float width)
        {
            float pad = HudKit.Pad, inner = width - 2 * pad, y = pad - 3;
            Vector2 h = HudKit.FitText(heading);
            HudKit.PlaceTopLeft(heading.rectTransform, pad, y, h);
            y += h.y + RowGap;
            readout.gameObject.SetActive(withReadout);
            if (withReadout) y = Wrapped(readout, pad, y, inner) + RowGap;
            y = Wrapped(units, pad, y, inner) + RowGap + 2;

            // the swatches flow in rows (the compact legend leaves them to the HUD's legend)
            float x = pad, rowHeight = HudKit.SizeSmall + 4;
            for (int i = 0; i < swatches.Count; i++)
            {
                swatches[i].gameObject.SetActive(!compact);
                names[i].gameObject.SetActive(!compact);
                if (compact) continue;
                Vector2 n = HudKit.FitText(names[i]);
                float item = SwatchSize + SwatchGap + n.x;
                if (x > pad && x + item > pad + inner)
                {
                    x = pad;
                    y += rowHeight;
                }

                HudKit.PlaceTopLeft(swatches[i].rectTransform, x, y + (rowHeight - SwatchSize) * 0.5f, new Vector2(SwatchSize, SwatchSize));
                HudKit.PlaceTopLeft(names[i].rectTransform, x + SwatchSize + SwatchGap, y + (rowHeight - n.y) * 0.5f, n);
                x += item + ItemGap;
            }

            if (!compact) y += rowHeight + RowGap;
            y = Wrapped(accounts, pad, y, inner);
            bool hasNotes = !compact && notes.text.Length > 0;
            notes.gameObject.SetActive(hasNotes);
            if (hasNotes) y = Wrapped(notes, pad, y + 2, inner);
            return Mathf.Ceil(y + pad - 3);
        }

        static float Wrapped(TextMeshProUGUI t, float x, float y, float width)
        {
            float h = t.text.Length > 0 ? HudKit.Measure(t, t.text, width).y : 0;
            HudKit.PlaceTopLeft(t.rectTransform, x, y, new Vector2(width, h));
            return y + h;
        }
    }
}
