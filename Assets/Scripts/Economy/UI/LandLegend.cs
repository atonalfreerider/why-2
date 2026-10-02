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
    /// $30.8T · the circuit balances within 1.1% (tier:raw)" and the circuit's notes: the approximations and residuals are
    /// printed, never hidden behind the headline). Generated from the snapshot on screen (<see cref="LandLegendFacts"/>),
    /// refreshed when it changes.
    /// <para>Landscape: a column at the bottom-left above the HUD's legend and lifeline readout (the social panel stands
    /// above it), all the notes printed. Portrait: the same place across the width, compact (no swatches, the HUD's legend
    /// has them) with the notes collapsed to their first line, which a tap on "all notes" opens. Where the bowl's
    /// silhouette takes its place the legend collapses its notes, then drops its swatches, or stands at the top-left under
    /// the title instead, and as a last resort shrinks to a strip (the heading and the year's readout)
    /// (<see cref="EconomyUiLayout.LandLegendPlace"/>): the readout is never hidden with it. Wherever the bowl leaves the
    /// year's readout no place under the chip (a portrait screen, or a view whose bowl reaches the top right) the legend
    /// shows that readout first. It steps aside while a portrait bottom sheet is up (the social panel or an inspector).
    /// Hidden on the road views (the overview only shows it to carry a displaced readout) and during the tour. Its box is
    /// <see cref="Occupied"/>.</para>
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

        /// <summary>The most content variants tried: everything; the notes collapsed; also no swatches; the strip.</summary>
        const int Variants = 4;

        GraphRoot root;
        RectTransform canvasRect, panel;
        TextMeshProUGUI heading, readout, units, accounts, notes;
        Button notesToggle;
        TextMeshProUGUI notesToggleText;
        YearSeries trust;

        /// <summary>A portrait screen with the land in view: no swatches (the HUD's legend has them), the notes collapsed until opened.</summary>
        bool compact;

        /// <summary>The viewer opened (true) or closed (false) the notes; null: open in landscape, collapsed in portrait.</summary>
        bool? notesOpen;

        /// <summary>Whether the social panel was up at the last layout (the notes' default follows it).</summary>
        bool laidOutSocial;

        /// <summary>The variants tried (swatches shown, notes open), their sizes; the notes' full text and their first line.</summary>
        readonly bool[] variantSwatches = new bool[Variants], variantOpen = new bool[Variants], variantStrip = new bool[Variants];
        readonly Vector2[] sizes = new Vector2[Variants];
        string allNotes = "", firstNote = "";

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
            notesToggle = HudKit.Button(panel, "NotesToggle", "all notes \u25B8", HudKit.SizeSmall - 2f, ToggleNotes);
            notesToggleText = notesToggle.GetComponentInChildren<TextMeshProUGUI>();
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
            // the overview shows it only to carry the year's readout when the bowl took the readout's place (a portrait sheet
            // that is up is kept clear of in the layout: the legend stands at the top, or steps aside while it is up)
            bool view = inView || EconomyControls.ReadoutMoved && root.CurrentPreset?.Id == "overview";
            bool show = view && !root.TourActive && LandService.Current != null;
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
            f.SocialPanel = UiBox.Empty; // the social panel stands above the legend, laid out from it
            f.Sheet = SheetUp(f);
            float width = f.Portrait ? f.Canvas.x - 2 * f.Margin : EconomyUiLayout.LeftColumnWidth;
            bool wantCompact = EconomyUiLayout.ReadoutInLegend(f), wantReadout = EconomyControls.ReadoutMoved;
            if (wantCompact != compact || wantReadout != withReadout || HudBlocks.SocialPanelShown != laidOutSocial) contentDirty = true;
            laidOutSocial = HudBlocks.SocialPanelShown;
            if (!contentDirty && f.Near(laidOutFrame) && Mathf.Abs(width - laidOutWidth) < 0.5f) return;
            compact = wantCompact;
            withReadout = wantReadout;
            if (contentDirty) Fill();
            contentDirty = false;
            laidOutFrame = f;
            laidOutWidth = width;

            // the variants, longest first: the notes as the viewer left them (open in landscape, collapsed in portrait),
            // then collapsed, then (landscape) without the swatches; their heights; the first that fits
            // (the notes start collapsed where the social panel stands above the legend: the season's controls come first)
            int count = 0;
            bool open = notesOpen ?? !compact && !HudBlocks.SocialPanelShown;
            Variant(!compact, open, false, ref count);
            if (open) Variant(!compact, false, false, ref count);
            if (!compact) Variant(false, false, false, ref count);
            Variant(false, false, true, ref count);
            for (int v = 0; v < count; v++) sizes[v] = new Vector2(width, Content(width, variantSwatches[v], variantOpen[v], variantStrip[v]));
            // the viewer's own choice to open the notes is kept down to the smallest readable scale
            float comfort = notesOpen == true ? EconomyUiLayout.MinScale : EconomyUiLayout.LegendComfort;
            Occupied = EconomyUiLayout.LandLegendPlace(f, EconomyControls.Occupied, sizes, count, comfort, out int chosen, out float scale);
            float height = Content(width, variantSwatches[chosen], variantOpen[chosen], variantStrip[chosen]);
            if (Occupied.IsEmpty)
            {
                // no room beside the bowl in this view: the legend steps aside (it comes back with a wider view)
                panel.localScale = Vector3.zero;
                return;
            }

            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Occupied.X, -Occupied.Y), new Vector2(width, height));
            panel.localScale = new Vector3(scale, scale, 1);

            problems.Clear();
            EconomyUiLayout.Check(f, "land legend", Occupied, problems);
            string text = string.Join("; ", problems);
            if (text == loggedProblems) return;
            loggedProblems = text;
            if (text.Length > 0) Debug.LogWarning("[Why] LandLegend layout: " + text);
        }

        /// <summary>The portrait sheet that is up (the social panel's, the player's or the person's), or empty.</summary>
        static UiBox SheetUp(HudFrame f)
        {
            if (!f.Portrait) return UiBox.Empty;
            if (!SocialPanel.Occupied.IsEmpty) return SocialPanel.Occupied;
            if (PlayerPanel.SheetShown) return PlayerPanel.Occupied;
            return PersonInspector.SheetShown ? PersonInspector.Shown : UiBox.Empty;
        }

        /// <summary>The texts of the snapshot on screen.</summary>
        void Fill()
        {
            LandSnapshot s = LandService.Current;
            heading.text = "THE LAND  ·  " + LandFacts.Year(s?.Year ?? EconomyState.Year,
                s != null && LandService.Model?.Data != null && LandService.Model.Data.IsEstimate(s.Year)).ToUpperInvariant();
            units.text = LandLegendFacts.Units(s, pop?.Sim?.PeoplePerLine ?? 100_000);
            accounts.text = LandLegendFacts.Accounts(s, LandService.Model?.Data);
            allNotes = LandLegendFacts.Notes(s, LandLegendFacts.NoteLines);
            firstNote = LandLegendFacts.Notes(s, 1);
            readout.text = withReadout
                ? YearFacts.Line(LandService.Model?.Lives, trust, s, LandService.Model?.Data, EconomyState.Year, true, UiFactory.Hex(GraphStyle.Text))
                : "";
        }

        void Variant(bool swatched, bool open, bool strip, ref int count)
        {
            variantSwatches[count] = swatched;
            variantOpen[count] = open;
            variantStrip[count] = strip;
            count++;
        }

        /// <summary>Opens or collapses the notes (the "all notes" button): laid out again on the next frame.</summary>
        void ToggleNotes()
        {
            notesOpen = !(notesOpen ?? !compact && !HudBlocks.SocialPanelShown);
            contentDirty = true;
        }

        /// <summary>
        /// Places the heading, the readout (when it moved here), the units, the swatches (wrapping, when
        /// <paramref name="swatched"/>), the accounts and the notes: all of them, wrapped, when <paramref name="open"/>; else
        /// their first line on one row beside the "all notes" button that opens them. The <paramref name="strip"/> (the last
        /// resort where the bowl leaves no more room) is the heading and the year's readout when it moved here, else the units.
        /// Returns the height.
        /// </summary>
        float Content(float width, bool swatched, bool open, bool strip)
        {
            float pad = HudKit.Pad, inner = width - 2 * pad, y = pad - 3;
            Vector2 h = HudKit.FitText(heading);
            HudKit.PlaceTopLeft(heading.rectTransform, pad, y, h);
            y += h.y + RowGap;
            readout.gameObject.SetActive(withReadout);
            if (withReadout) y = Wrapped(readout, pad, y, inner) + RowGap;
            bool rest = !strip || !withReadout;
            units.gameObject.SetActive(rest);
            if (rest) y = Wrapped(units, pad, y, inner) + RowGap + 2;
            if (strip)
            {
                for (int i = 0; i < swatches.Count; i++)
                {
                    swatches[i].gameObject.SetActive(false);
                    names[i].gameObject.SetActive(false);
                }

                accounts.gameObject.SetActive(false);
                notes.gameObject.SetActive(false);
                notesToggle.gameObject.SetActive(false);
                return Mathf.Ceil(y - RowGap - (rest ? 2 : 0) + pad - 3);
            }

            accounts.gameObject.SetActive(true);

            // the swatches flow in rows (the compact variants leave them to the HUD's legend)
            float x = pad, rowHeight = HudKit.SizeSmall + 4;
            for (int i = 0; i < swatches.Count; i++)
            {
                swatches[i].gameObject.SetActive(swatched);
                names[i].gameObject.SetActive(swatched);
                if (!swatched) continue;
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

            if (swatched) y += rowHeight + RowGap;
            y = Wrapped(accounts, pad, y, inner);

            // the notes: all of them, or the first on one row with the button that opens the rest
            bool hasNotes = allNotes.Length > 0, more = hasNotes && allNotes != firstNote;
            bool expanded = open || !more;
            notes.gameObject.SetActive(hasNotes);
            notesToggle.gameObject.SetActive(more);
            if (!hasNotes) return Mathf.Ceil(y + pad - 3);
            y += 2;
            notesToggleText.text = expanded ? "fewer notes \u25B4" : "all notes \u25B8";
            Vector2 button = HudKit.FitText(notesToggleText) + new Vector2(10, 4);
            if (expanded)
            {
                notes.textWrappingMode = TextWrappingModes.Normal;
                notes.overflowMode = TextOverflowModes.Overflow;
                notes.text = allNotes;
                y = Wrapped(notes, pad, y, inner);
                if (more)
                {
                    HudKit.PlaceTopLeft((RectTransform)notesToggle.transform, pad + inner - button.x, y + 2, button);
                    y += button.y + 4;
                }
            }
            else
            {
                // one line, cut with an ellipsis, beside the button
                notes.textWrappingMode = TextWrappingModes.NoWrap;
                notes.overflowMode = TextOverflowModes.Ellipsis;
                notes.text = firstNote;
                float line = Mathf.Max(HudKit.Measure(notes, "Ag").y, button.y);
                HudKit.PlaceTopLeft(notes.rectTransform, pad, y + (line - HudKit.Measure(notes, "Ag").y) * 0.5f,
                    new Vector2(Mathf.Max(1, inner - button.x - 6), HudKit.Measure(notes, "Ag").y));
                HudKit.PlaceTopLeft((RectTransform)notesToggle.transform, pad + inner - button.x, y + (line - button.y) * 0.5f, button);
                y += line;
            }

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
