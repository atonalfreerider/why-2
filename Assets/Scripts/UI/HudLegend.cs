using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Humans;
using Why.Matter;

namespace Why.UI
{
    /// <summary>
    /// Bottom-left key to the graph: the current view's swatches (the only hue in the UI) and how to read its marks, and
    /// above it two readouts: the scale of space at the centre of the screen ("Space here: 1 px² ≈ 10^6.2 m² · waves 10^5.0 m
    /// apart - 7.91 billion years ago") while the centre lies on the red layer's waves (<see cref="SpaceLod"/>), and above
    /// that the lifeline readout ("1 line = 100,000 people at this zoom - United States 1950 - now") while lifelines are
    /// visible; both stay through the tour with the key. The universe keys its three levels with the clock's grammar (the
    /// civilization views with their own:
    /// the streams run west to east there). The economy keys each view by its own key (<see cref="Economy.HillPresets.Key"/>)
    /// or, for the views of the whole wave, the hills' hues and grammar, so the key stays true in every view and at every
    /// tour stop (it is the one key the tour keeps on screen), and in every year (the attention cloud's swatch only where the
    /// cloud is drawn).
    /// </summary>
    public sealed class HudLegend
    {
        const string Grammar = "around: time (log clock)  |  up: hierarchy  |  inward: relevance to us";

        /// <summary>The civilization views draw the Histomap's streams side by side: across the road runs west to east, not
        /// relevance, and their rise after 1800 is width (a share of power), not rank.</summary>
        const string CivGrammar = "Each band is a civilization, the western ones nearest you. Its width is its share of world power; " +
                                  "the whole layer widens as world population grows. Lifeline height: status, as the model rates it.";

        /// <summary>
        /// The hills' grammar (the views of the whole wave): time and the year shown, what is measured (ridge widths) and what
        /// is a drawing choice (heights), and the compressed scale of the company lines. The hills run time back from the crest,
        /// so no screen direction here: the past lies behind the crest, and the arrow the wave draws in the world beyond it
        /// (in the views of the whole wave, which this key serves) points to later years whichever way the camera looks; the
        /// flat charts say "left to right" in their own keys. The arrow comes last: a portrait screen shows only the first
        /// two lines of the reading.
        /// </summary>
        const string EconomyGrammar =
            "Road and ribs: time, one rib a year; the bright crest is the year shown, the past behind it.\nEach ridge is an industry, as wide as its share of what the economy produces.\nHigher layers depend on lower ones; the heights are a drawing choice, not measured.\nGold lines are companies, rising with their market value (compressed).\nEach moving dot is a fifth of a group of households.\nThe arrow beyond the crest points to later years.";

        /// <summary>The attention cloud's swatch: its stars are the most-followed Americans.</summary>
        const string Attention = "The most-followed Americans";

        /// <summary>The hills' hues: capital, people, the two motives money is spent with, the state, attention, and the
        /// universe's matter and life, which the raw industries keep.</summary>
        static readonly (string, Color)[] EconomySwatches =
        {
            ("Owners and profit", Economy.EconomyStyle.Capital), ("People and wages", Economy.EconomyStyle.Wages), ("Spent on wants", Economy.EconomyStyle.Desire),
            ("Spent from fear", Economy.EconomyStyle.Fear), ("Government", Economy.EconomyStyle.Government), (Attention, Economy.HillPresets.White),
            ("Raw matter", Economy.EconomyStyle.Matter), ("Life (farming)", Economy.EconomyStyle.Life),
        };

        /// <summary>The same where the attention cloud is not drawn (1972's view, a year before it; see
        /// <see cref="Economy.HillPresets.AttentionDrawn"/>): no swatch for a mark that is not on screen.</summary>
        static readonly (string, Color)[] EconomySwatchesNoAttention = System.Array.FindAll(EconomySwatches, s => s.Item1 != Attention);

        static readonly (string, Color)[] Levels = { ("Matter", GraphStyle.Matter), ("Life", GraphStyle.Life), ("Humans", GraphStyle.Humans) };

        /// <summary>The views of the Histomap's streams (the civilization lens and the two that look at its present end).</summary>
        static bool CivView(string id) => id == "civilizations" || id == "modern" || id == "present";

        /// <summary>Space between swatches (tighter in the economy, which has more of them).</summary>
        const float EconomyItemGap = 13f;
        const float SwatchSize = 10f;
        const float SwatchGap = 7f;
        const float ItemGap = 22f;
        const float RowGap = 6f;

        /// <summary>The widest a key's swatch rows and reading run before they wrap (reference pixels).</summary>
        const float MaxTextWidth = 580f;

        /// <summary>The legend's widest (the preset bar keeps clear of it, so it never moves with the view).</summary>
        public static float MaxWidth => MaxTextWidth + 2 * HudKit.Pad;

        /// <summary>Fill of the legend's panel: dark enough to read over the bloomed hills.</summary>
        const float Fill = 0.8f;

        /// <summary>How quickly the legend eases to a new height (per second, exponential).</summary>
        const float MoveRate = 9f;

        readonly RectTransform legend;
        readonly TextMeshProUGUI grammar;
        readonly List<(Image dot, TextMeshProUGUI label)> swatches = new List<(Image, TextMeshProUGUI)>();
        readonly RectTransform readout;
        readonly TextMeshProUGUI readoutText;
        readonly UiFade readoutFade;
        readonly RectTransform space;
        readonly TextMeshProUGUI spaceText;
        readonly UiFade spaceFade;

        /// <summary>What the space readout shows, to the precision it shows it: tenths of a decade of area and of the waves'
        /// spacing, and the moment (none: hidden). Its text is built again only when this changes.</summary>
        (int area, int waves, int when) shownSpace = NoSpace;

        /// <summary>Whether the readouts were last laid out with the space readout in its slot (<see cref="SpaceHeld"/>).</summary>
        bool spaceLaidOut;

        static readonly (int, int, int) NoSpace = (int.MinValue, 0, 0);
        (string, Color)[] shownItems;
        string shownReading;
        float shownGap, maxTextWidth = MaxTextWidth, firstLine, lineAdvance;
        int maxLines;
        double shownPeople = -1;
        string shownContext;
        float bottom, bottomTarget;
        float left = HudKit.Margin;

        /// <summary>Legend width in reference pixels (at most <see cref="MaxWidth"/>).</summary>
        public float Width { get; private set; }

        /// <summary>Legend height in reference pixels.</summary>
        public float Height { get; private set; }

        public HudLegend(Transform parent)
        {
            legend = HudKit.FramedPanel(parent, "Legend", Fill);
            legend.Place(Vector2.zero, Vector2.zero, new Vector2(HudKit.Margin, HudKit.Margin), Vector2.one);
            grammar = UiFactory.Text(legend, "Grammar", "", HudKit.SizeSmall - 0.5f, Color.Lerp(GraphStyle.TextDim, GraphStyle.Text, 0.4f));
            grammar.overflowMode = TextOverflowModes.Ellipsis;
            firstLine = HudKit.Measure(grammar, "Ag").y;
            lineAdvance = HudKit.Measure(grammar, "Ag\nAg").y - firstLine;
            SetView(null);

            readout = HudKit.FramedPanel(parent, "LifelineReadout", Fill);
            readout.Place(Vector2.zero, Vector2.zero, Vector2.zero, readout.sizeDelta);
            readoutText = HudKit.Line(readout, "Text", "", HudKit.SizeSmall, GraphStyle.Text);
            readoutFade = new UiFade(readout.gameObject, 0, 4f, false);

            // the scale of space wraps to the key's width (a portrait screen is narrower than its one line)
            space = HudKit.FramedPanel(parent, "SpaceReadout", Fill);
            space.Place(Vector2.zero, Vector2.zero, Vector2.zero, space.sizeDelta);
            spaceText = UiFactory.Text(space, "Text", "", HudKit.SizeSmall, GraphStyle.Text);
            spaceFade = new UiFade(space.gameObject, 0, 4f, false);
            SetBottom(HudKit.Margin, true);

            // the graph's labels keep out from under the key and the readouts (a label there showed through the panel)
            LabelSystem.KeepClear(legend);
            LabelSystem.KeepClear(readout);
            LabelSystem.KeepClear(space);
        }

        /// <summary>
        /// Shows the key of a view (null: the scene's own): its swatches in rows, then its reading, wrapped to the legend's
        /// width. True when the legend changed size (the HUD lays its bottom edge out again). Nothing is laid out when the key
        /// is the one shown, so the HUD asks every frame in the economy, whose wave key follows the year on screen.
        /// </summary>
        public bool SetView(ViewPreset preset)
        {
            string id = preset?.Id;
            (string, Color)[] items;
            string reading;
            float gap;
            if (GraphScene.IsEconomy)
            {
                Economy.HillPresets.ViewKey key = Economy.HillPresets.Key(id);
                items = key != null ? key.Swatches : Economy.HillPresets.AttentionDrawn ? EconomySwatches : EconomySwatchesNoAttention;
                reading = key != null ? key.Reading : EconomyGrammar;
                gap = EconomyItemGap;
            }
            else
            {
                items = Levels;
                reading = CivView(id) ? CivGrammar : Grammar;
                gap = ItemGap;
            }

            if (ReferenceEquals(items, shownItems) && reading == shownReading) return false;
            shownItems = items;
            shownReading = reading;
            shownGap = gap;
            return Layout();
        }

        /// <summary>The legend's widest on this screen (a portrait screen is narrower than its usual width) and the most lines
        /// of reading it shows (0: all; an ellipsis past them): the key wraps again to them.</summary>
        public void SetMaxWidth(float outer, int lines)
        {
            float width = Mathf.Clamp(outer - 2 * HudKit.Pad, 160f, MaxTextWidth);
            if (Mathf.Approximately(width, maxTextWidth) && lines == maxLines) return;
            maxTextWidth = width;
            maxLines = lines;
            if (shownItems != null) Layout();
            // the space readout wraps again to the new width on the next tick
            if (shownSpace != NoSpace) shownSpace = (int.MinValue + 1, 0, 0);
        }

        /// <summary>Lays the shown key out: the swatches in rows, then the reading, wrapped to the legend's width. True when
        /// the legend changed size.</summary>
        bool Layout()
        {
            (string, Color)[] items = shownItems;
            string reading = shownReading;
            float gap = shownGap;

            // the swatches in rows that wrap at the legend's width (a pool: views differ in how many they have)
            float rowHeight = HudKit.SizeBody + 6, x = HudKit.Pad, y = HudKit.Pad, right = HudKit.Pad;
            for (int i = 0; i < items.Length; i++)
            {
                if (i == swatches.Count) swatches.Add(MakeSwatch());
                (Image dot, TextMeshProUGUI label) = swatches[i];
                dot.gameObject.SetActive(true);
                label.gameObject.SetActive(true);
                dot.color = items[i].Item2;
                label.text = items[i].Item1;
                Vector2 size = HudKit.FitText(label);
                float w = SwatchSize + SwatchGap + size.x;
                if (x > HudKit.Pad && x + w > HudKit.Pad + maxTextWidth)
                {
                    x = HudKit.Pad;
                    y += rowHeight;
                }

                HudKit.PlaceTopLeft(dot.rectTransform, x, y + (rowHeight - SwatchSize) * 0.5f, new Vector2(SwatchSize, SwatchSize));
                HudKit.PlaceTopLeft(label.rectTransform, x + SwatchSize + SwatchGap, y + (rowHeight - size.y) * 0.5f, size);
                right = Mathf.Max(right, x + w);
                x += w + gap;
            }

            for (int i = items.Length; i < swatches.Count; i++)
            {
                swatches[i].dot.gameObject.SetActive(false);
                swatches[i].label.gameObject.SetActive(false);
            }

            if (items.Length > 0) y += rowHeight + RowGap;

            // how to read the marks, wrapped to the swatches' width
            grammar.text = reading;
            Vector2 g = HudKit.Measure(grammar, reading, maxTextWidth);
            if (maxLines > 0) g.y = Mathf.Min(g.y, Mathf.Ceil(firstLine + (maxLines - 1) * lineAdvance));
            HudKit.PlaceTopLeft(grammar.rectTransform, HudKit.Pad, y, g);

            float width = Mathf.Ceil(Mathf.Max(right, HudKit.Pad + g.x) + HudKit.Pad), height = Mathf.Ceil(y + g.y + HudKit.Pad - 2);
            bool resized = width != Width || height != Height;
            Width = width;
            Height = height;
            legend.sizeDelta = new Vector2(Width, Height);
            if (readout != null && space != null) ApplyBottom();
            return resized;
        }

        (Image, TextMeshProUGUI) MakeSwatch()
        {
            int n = swatches.Count;
            Image dot = UiFactory.Panel(legend, "Swatch " + n, Color.white, false);
            // shrink the factory sprite's 14 px corners to half the swatch size: a dot
            dot.pixelsPerUnitMultiplier = 14f / (SwatchSize * 0.5f);
            TextMeshProUGUI label = HudKit.Line(legend, "Swatch label " + n, "", HudKit.SizeBody, GraphStyle.Text);
            return (dot, label);
        }

        /// <summary>
        /// Move the legend (and the readouts above it) so its bottom edge sits at y (canvas units), at once or
        /// easing there (e.g. when the preset bar beneath it steps aside for the tour).
        /// </summary>
        public void SetBottom(float y, bool instant)
        {
            bottomTarget = Mathf.Round(y);
            ReportTop();
            if (!instant) return;
            bottom = bottomTarget;
            ApplyBottom();
        }

        /// <summary>Distance of the legend and the readouts from the left screen edge (canvas units).</summary>
        public void SetLeft(float x)
        {
            left = Mathf.Round(x);
            ApplyBottom();
        }

        /// <summary>The legend at the bottom, the scale of space just above it while it holds its slot, the lifelines above that.</summary>
        void ApplyBottom()
        {
            float y = Mathf.Round(bottom);
            legend.anchoredPosition = new Vector2(left, y);
            float top = y + Height + HudKit.Gap;
            space.anchoredPosition = new Vector2(left, top);
            spaceLaidOut = SpaceHeld;
            if (spaceLaidOut) top += space.sizeDelta.y + HudKit.Gap;
            readout.anchoredPosition = new Vector2(left, top);
            ReportTop();
        }

        /// <summary>
        /// True while the space readout holds its slot above the key: while it shows, and after it hides until it has faded
        /// out, so the lifeline readout above it moves down into the slot only once the panel is gone, never over it while it
        /// still fades (<see cref="Tick"/> lays the readouts out again when this changes).
        /// </summary>
        bool SpaceHeld => spaceFade != null && (spaceFade.Shown || spaceFade.Alpha > 0f);

        /// <summary>Tells the tour's panel placement where the legend block ends (canvas units above the bottom edge): the
        /// legend and, while they show (the space readout while it holds its slot), the readouts above it; bottom-left cards
        /// and the portrait sheet dock above it.</summary>
        void ReportTop() => Director.PanelPlacement.LegendTop =
            bottomTarget + Height +
            (SpaceHeld ? HudKit.Gap + space.sizeDelta.y : 0) +
            (readoutFade != null && readoutFade.Shown ? HudKit.Gap + readout.sizeDelta.y : 0);

        /// <summary>
        /// Follow <see cref="HumansLod"/>: show what one lifeline stands for, hide when none are drawn. While the graph grows
        /// from the Big Bang (<see cref="GraphReveal"/>) it stays hidden: the lines are not drawn yet, though the camera close
        /// to the flash makes the nearest stream's lines readable by zoom alone.
        /// </summary>
        public void Tick(float dt)
        {
            if (bottom != bottomTarget)
            {
                bottom = Mathf.Lerp(bottom, bottomTarget, 1f - Mathf.Exp(-MoveRate * dt));
                if (Mathf.Abs(bottom - bottomTarget) < 0.5f) bottom = bottomTarget;
                ApplyBottom();
            }

            double people = HumansLod.PeoplePerLine;
            if (double.IsNaN(people) || double.IsInfinity(people) || GraphReveal.On) people = 0;
            string context = HumansLod.Context ?? "";
            if (people != shownPeople || context != shownContext)
            {
                shownPeople = people;
                shownContext = context;
                readoutFade.Show(people > 0); // activates the panel before its text is measured
                if (people > 0) SetReadout(people, context);
                ReportTop();
            }

            TickSpace();
            readoutFade.Tick(dt);
            spaceFade.Tick(dt);
            // the space readout has faded out: only now the lifeline readout moves down into its slot above the key
            if (SpaceHeld != spaceLaidOut) ApplyBottom();
        }

        /// <summary>
        /// Follow <see cref="SpaceLod"/>: the scale of space at the centre of the screen while the grid publishes it (the red
        /// layer's waves under the centre; never in a headset, while the graph grows from the Big Bang, or in the economy).
        /// The text is built again only when what it shows changes.
        /// </summary>
        void TickSpace()
        {
            bool show = SpaceLod.Published && !GraphScene.IsEconomy && !GraphReveal.On;
            SpaceLod.Reading r = SpaceLod.Current;
            (int, int, int) key = show ? SpaceKey(r) : NoSpace;
            if (key == shownSpace) return;
            bool moved = spaceFade.Shown != show || shownSpace == NoSpace;
            shownSpace = key;
            spaceFade.Show(show); // activates the panel before its text is measured
            if (show) SetSpace(r);
            if (show || moved) ApplyBottom();
        }

        /// <summary>The reading to the precision the readout shows it.</summary>
        static (int, int, int) SpaceKey(SpaceLod.Reading r)
        {
            bool waves = r.FinestLevel >= 0 && !double.IsNaN(r.Log10WaveSpacing) && !double.IsInfinity(r.Log10WaveSpacing);
            return ((int)Math.Round(r.Log10AreaPerPixel * 10), waves ? (int)Math.Round(r.Log10WaveSpacing * 10) : int.MaxValue,
                (int)Math.Round(Math.Log10(Math.Max(r.YearsAgo, 1)) * 2000));
        }

        /// <summary>
        /// "Space here: 1 px² ≈ 10^6.2 m² · waves 10^5.0 m apart - 7.91 billion years ago": the area one pixel covers at the
        /// centre of the screen and the spacing of the finest waves fully open there (physical metres at that moment), and the
        /// moment, which names the point.
        /// </summary>
        void SetSpace(SpaceLod.Reading r)
        {
            string text = "Space here: " + SpaceLod.Describe(r, true) +
                          "<color=#" + UiFactory.Hex(GraphStyle.TextDim) + ">  -  " +
                          DeepTime.FormatYearsAgo(r.YearsAgo, DeepTime.NowYear) + "</color>";
            spaceText.text = text;
            Vector2 size = HudKit.Measure(spaceText, text, maxTextWidth);
            HudKit.PlaceTopLeft(spaceText.rectTransform, HudKit.Pad, 7, size);
            space.sizeDelta = new Vector2(Mathf.Ceil(size.x + 2 * HudKit.Pad), Mathf.Ceil(size.y + 14));
        }

        /// <summary>
        /// What one line stands for at the current zoom; in the economy it names the mark it counts (the road's lines), since
        /// the moving dots on the hills count something else.
        /// </summary>
        void SetReadout(double people, string context)
        {
            string noun = people > 0.5 && people < 1.5 ? " person" : " people";
            string text = (GraphScene.IsEconomy ? "Road: " : "") + "1 line = <b>" + HudKit.FormatCount(people) + "</b>" + noun + " at this zoom";
            if (context.Length > 0)
            {
                text += "<color=#" + UiFactory.Hex(GraphStyle.TextDim) + ">  -  " + Escape(context) + "</color>";
            }

            readoutText.text = text;
            Vector2 size = HudKit.FitText(readoutText);
            HudKit.PlaceTopLeft(readoutText.rectTransform, HudKit.Pad, 7, size);
            readout.sizeDelta = new Vector2(Mathf.Ceil(size.x + 2 * HudKit.Pad), Mathf.Ceil(size.y + 14));
        }

        /// <summary>Context strings are data: keep them from opening rich-text tags.</summary>
        static string Escape(string s) => s.Replace("<", "<noparse><</noparse>");
    }
}
