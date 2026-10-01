using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The person inspector's panel: a framed column of one person's life in one year (<see cref="PersonFacts"/>). Who
    /// they are (sex, birth, generation, tribe, the strategy they play with others and how often they cooperate) and, for
    /// the model's notable people, their role and why; the year's household and work; their money (income by source,
    /// taxes, what is left, spending, saving, net worth, debt, wealth group); where the spending goes (six bars colored
    /// from desire to fear, the fantasy part underlined); their mind (the share of fear in the spending's motive, reason
    /// as the higher OS, orientation to the future, agency against the cut for control, the strongest desires and fears at
    /// their age); and their whole life as two small bar charts (agency, gold while in control; net worth).
    ///
    /// Text is neutral grey and white at 13 - 14 px; hue appears only as small swatches and bars that carry the scene's
    /// meanings (rose desire, ice fear, gold capital and control, blue people). <see cref="Layout"/> places everything for
    /// a width: the landscape column, or a compact full-width sheet on a portrait screen (three money columns with short
    /// labels, two spending columns, the three gauges in one row, no notes, no lifetime charts).
    /// </summary>
    public sealed class PersonPanel
    {
        /// <summary>Width of the landscape column (reference pixels).</summary>
        public const float LandscapeWidth = 360f;

        const float Pad = HudKit.Pad;

        /// <summary>A line of 13 - 14 px text; a small caps heading; space between sections.</summary>
        const float Row = 18f, HeadingRow = 16f, SectionGap = 10f;

        const float CloseSize = 22f, ColumnGap = 14f;

        /// <summary>Bars: thickness, the swatch dot, the fantasy underline, the tick of a gauge.</summary>
        const float BarHeight = 7f, SwatchSize = 8f, FantasyLine = 2f, TickWidth = 2f, TickOverhang = 3f;

        /// <summary>
        /// Columns of the bar rows (reference pixels): a category's swatch and name (the longest, "Self-improvement",
        /// is ~102 px at 13 px), its percent; a gauge's name ("Reason (higher OS)" ~116 px) and value, and in the
        /// portrait sheet's single row of three gauges the short names ("Reason", "Future", "Agency"); the poles' ends.
        /// </summary>
        const float CategoryName = 124f, PercentWidth = 36f;

        const float GaugeLabel = 126f, ValueWidth = 40f, CompactGaugeLabel = 54f, CompactValueWidth = 30f, PoleLabel = 82f;

        static readonly string[] GaugeNames = { "Reason (higher OS)", "Future orientation", "Agency" };
        static readonly string[] CompactGaugeNames = { "Reason", "Future", "Agency" };

        /// <summary>The lifetime charts: height, label and value columns; texture size (the record has at most 81 years).</summary>
        const float SparkHeight = 22f, SparkLabel = 70f, SparkValue = 92f;

        const int SparkTexels = 24, MaxYears = 128;

        const float FontSmall = HudKit.SizeSmall, FontBody = HudKit.SizeBody;

        /// <summary>Space kept between a money label and its value (reference pixels).</summary>
        const float LabelValueGap = 6f;

        static readonly System.Globalization.CultureInfo Ci = System.Globalization.CultureInfo.InvariantCulture;

        static readonly Color Track = new Color(1, 1, 1, 0.07f);
        static readonly Color GaugeColor = new Color(0.86f, 0.87f, 0.90f, 0.6f);
        static readonly Color TickColor = new Color(1, 1, 1, 0.92f);
        static readonly Color FantasyColor = new Color(1, 1, 1, 0.75f);
        static readonly Color32 YearMark = new Color32(255, 255, 255, 70);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        const float BarAlpha = 0.85f;

        /// <summary>A labeled bar: a spending category, or a gauge of the mind.</summary>
        sealed class Bar
        {
            public Image Swatch, Track, Fill, Extra;
            public TextMeshProUGUI Name, Value;
        }

        /// <summary>A lifetime chart: a texture one texel column per year.</summary>
        sealed class Spark
        {
            public TextMeshProUGUI Label, Value;
            public Image Back;
            public RawImage Image;
            public Texture2D Texture;
            public Color32[] Pixels;
        }

        readonly TextMeshProUGUI kicker, title, meta, role, yearHeading, status, moneyNote, spendingHeading, spendingLegend,
            mindHeading, gaugeLegend, verdict, wants, fears, lifeHeading, hint;

        readonly RectTransform close;
        readonly TextMeshProUGUI[] moneyLabels = new TextMeshProUGUI[PersonFacts.MoneyRows];
        readonly TextMeshProUGUI[] moneyValues = new TextMeshProUGUI[PersonFacts.MoneyRows];
        readonly Bar[] categories = new Bar[PersonFacts.Categories];
        readonly Bar poles, reason, future, agency;
        readonly Spark agencySpark, wealthSpark;
        PersonFacts facts;

        /// <summary>The panel's rect (the framed rim); the owner places and scales it.</summary>
        public RectTransform Rect { get; }

        public PersonPanel(Transform parent, Action onClose)
        {
            Rect = HudKit.FramedPanel(parent, "PersonInspector", 0.9f, true);
            Color dim = GraphStyle.TextDim, text = GraphStyle.Text;

            kicker = Heading("Kicker");
            close = (RectTransform)HudKit.Button(Rect, "Close", "\u00D7", FontBody + 2, onClose).transform;
            title = HudKit.Line(Rect, "Title", "", HudKit.SizeTitle, text, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.Normal;
            meta = Wrapped("Meta", FontSmall, dim);
            role = Wrapped("Role", FontSmall, dim);

            yearHeading = Heading("YearHeading");
            status = Wrapped("Status", FontBody, text);
            for (int i = 0; i < PersonFacts.MoneyRows; i++)
            {
                moneyLabels[i] = HudKit.Line(Rect, "MoneyLabel" + i, "", FontSmall, dim, FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft);
                moneyLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                moneyValues[i] = HudKit.Line(Rect, "MoneyValue" + i, "", FontSmall, text, FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight);
            }

            moneyNote = Wrapped("MoneyNote", FontSmall - 1, HudKit.TextFaint);

            spendingHeading = Heading("SpendingHeading");
            for (int c = 0; c < categories.Length; c++) categories[c] = NewBar("Category" + c, true);
            spendingLegend = Wrapped("SpendingLegend", FontSmall - 1, HudKit.TextFaint);
            spendingLegend.text = "Bar color: from desire (rose) to fear (ice) as the money's motive.  Underline: the part " +
                                  "that buys a fantasy.";

            mindHeading = Heading("MindHeading");
            mindHeading.text = "MIND";
            poles = NewBar("DesireFear", false);
            poles.Extra = Pill(poles.Track.transform, "Fear", EconomyStyle.Fear, BarHeight); // the fear part of the bar
            reason = NewBar("Reason", false);
            future = NewBar("Future", false);
            agency = NewBar("Agency", false);
            reason.Extra = Plain(reason.Track.transform, "Mean", TickColor);
            future.Extra = Plain(future.Track.transform, "Mean", TickColor);
            agency.Extra = Plain(agency.Track.transform, "Cut", TickColor);
            gaugeLegend = Wrapped("GaugeLegend", FontSmall - 1, HudKit.TextFaint);
            gaugeLegend.text = "Tick: the population's mean that year; for agency, the cut for being in control.";
            verdict = Wrapped("Verdict", FontSmall, text);
            wants = Wrapped("Wants", FontSmall, text);
            fears = Wrapped("Fears", FontSmall, text);

            lifeHeading = Heading("LifeHeading");
            agencySpark = NewSpark("AgencySpark", "agency");
            wealthSpark = NewSpark("WealthSpark", "net worth");
            hint = Wrapped("Hint", FontSmall - 1, HudKit.TextFaint);
            hint.text = "Esc or \u00D7 closes  \u00B7  ,  and  .  change the year  \u00B7  N meets the next notable person";
        }

        // ------------------------------------------------------------------ building

        TextMeshProUGUI Heading(string name)
        {
            TextMeshProUGUI t = HudKit.Line(Rect, name, "", FontSmall - 1, HudKit.TextFaint, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            t.characterSpacing = 10;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        TextMeshProUGUI Wrapped(string name, float size, Color color)
        {
            TextMeshProUGUI t = UiFactory.Text(Rect, name, "", size, color);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        Bar NewBar(string name, bool swatch)
        {
            Bar b = new Bar
            {
                Name = HudKit.Line(Rect, name + "Name", "", FontSmall, GraphStyle.TextDim, FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft),
                Value = HudKit.Line(Rect, name + "Value", "", FontSmall, GraphStyle.Text, FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight),
                Track = Pill(Rect, name + "Track", Track, BarHeight)
            };
            b.Name.overflowMode = TextOverflowModes.Ellipsis;
            b.Fill = Pill(b.Track.transform, "Fill", GraphStyle.Text, BarHeight);
            if (swatch)
            {
                b.Swatch = Pill(Rect, name + "Swatch", GraphStyle.Text, SwatchSize);
                b.Extra = Plain(b.Track.transform, "Fantasy", FantasyColor);
            }

            return b;
        }

        Spark NewSpark(string name, string label)
        {
            Spark s = new Spark
            {
                Label = HudKit.Line(Rect, name + "Label", label, FontSmall, GraphStyle.TextDim, FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft),
                Value = HudKit.Line(Rect, name + "Value", "", FontSmall, GraphStyle.Text, FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight),
                Back = Plain(Rect, name + "Back", Track),
                Texture = new Texture2D(MaxYears, SparkTexels, TextureFormat.RGBA32, false)
                {
                    name = "Why" + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                },
                Pixels = new Color32[MaxYears * SparkTexels]
            };
            RectTransform rt = UiFactory.Rect(Rect, name);
            s.Image = rt.gameObject.AddComponent<RawImage>();
            s.Image.texture = s.Texture;
            s.Image.raycastTarget = false;
            return s;
        }

        /// <summary>A rounded bar or dot (the factory sprite's corners shrunk to half the height).</summary>
        static Image Pill(Transform parent, string name, Color color, float height)
        {
            Image img = UiFactory.Panel(parent, name, color, false);
            img.pixelsPerUnitMultiplier = 14f / (height * 0.5f);
            return img;
        }

        /// <summary>A plain rectangle (ticks, underlines, chart backgrounds).</summary>
        static Image Plain(Transform parent, string name, Color color)
        {
            Image img = UiFactory.Rect(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Frees the charts' textures (the module's OnDestroy).</summary>
        public void Dispose()
        {
            if (agencySpark.Texture != null) UnityEngine.Object.Destroy(agencySpark.Texture);
            if (wealthSpark.Texture != null) UnityEngine.Object.Destroy(wealthSpark.Texture);
        }

        // ------------------------------------------------------------------ content

        /// <summary>Shows a person's facts (texts, colors, charts); <see cref="Layout"/> must follow (sizes depend on the
        /// width). The panel must be active, so the texts can be measured.</summary>
        public void Fill(PersonFacts f, double peoplePerLine)
        {
            facts = f;
            if (f == null) return;
            string textHex = UiFactory.Hex(GraphStyle.Text), dimHex = UiFactory.Hex(GraphStyle.TextDim);

            kicker.text = "ONE LIFELINE  \u00B7  " + HudKit.FormatCount(peoplePerLine).ToUpperInvariant() + " PEOPLE";
            title.text = Escape(f.Title);
            meta.text = Escape(f.Meta);
            // the role's sentence describes the last simulated year: say so when another year is shown
            string roleYear = f.RoleYear != f.Year
                ? " <color=#" + UiFactory.Hex(HudKit.TextFaint) + ">(" + f.RoleYear.ToString(Ci) + ")</color>"
                : "";
            role.text = f.Role != null
                ? "<color=#" + textHex + "><b>" + Escape(f.Role.ToUpperInvariant()) + "</b></color>" + roleYear + "  " +
                  Escape(f.RoleWhy ?? "")
                : "";
            yearHeading.text = Escape(f.YearHeading);
            status.text = Escape(f.Status);
            // the money labels depend on the width (Layout)
            for (int i = 0; i < PersonFacts.MoneyRows; i++) moneyValues[i].text = Escape(f.MoneyValues[i] ?? "");

            moneyNote.text = Escape(f.MoneyNote);
            spendingHeading.text = Escape(f.SpendingHeading);
            for (int c = 0; c < categories.Length; c++)
            {
                Bar b = categories[c];
                Color motive = EconomyStyle.Motive(f.CategoryFear[c]);
                b.Swatch.color = motive;
                b.Fill.color = new Color(motive.r, motive.g, motive.b, BarAlpha);
                b.Name.text = Escape(f.CategoryNames[c]);
                b.Value.text = PersonFacts.Pct(f.CategoryShares[c]);
            }

            poles.Fill.color = new Color(EconomyStyle.Desire.r, EconomyStyle.Desire.g, EconomyStyle.Desire.b, BarAlpha);
            poles.Extra.color = new Color(EconomyStyle.Fear.r, EconomyStyle.Fear.g, EconomyStyle.Fear.b, BarAlpha);
            poles.Name.text = "Desire " + PersonFacts.Pct(1 - f.FearShare);
            poles.Value.text = PersonFacts.Pct(f.FearShare) + " Fear";
            reason.Value.text = f.Reason.ToString("0.00", Ci);
            future.Value.text = f.Future.ToString("0.00", Ci);
            agency.Value.text = f.Agency.ToString("0.00", Ci);
            reason.Fill.color = GaugeColor;
            future.Fill.color = GaugeColor;
            Color person = f.InControl ? EconomyStyle.Capital : GraphStyle.Humans;
            agency.Fill.color = new Color(person.r, person.g, person.b, BarAlpha);
            verdict.text = Escape(f.Verdict);
            wants.text = "<color=#" + dimHex + ">Wants</color>   " + Escape(f.Wants);
            fears.text = "<color=#" + dimHex + ">Fears</color>   " + Escape(f.Fears);

            lifeHeading.text = "LIFETIME  " + f.LifeFirstYear.ToString(Ci) + " - " + f.LifeLastYear.ToString(Ci);
            agencySpark.Value.text = Escape(f.AgencyPeak);
            wealthSpark.Value.text = Escape(f.WealthPeak);
            DrawAgency(f);
            DrawWealth(f);
        }

        /// <summary>Agency per year, gold while in control and the people's blue otherwise; the shown year marked.</summary>
        void DrawAgency(PersonFacts f)
        {
            Spark s = agencySpark;
            int n = Begin(s, f);
            Color32 gold = EconomyStyle.Capital, blue = GraphStyle.Humans;
            for (int k = 0; k < n; k++)
            {
                int h = Mathf.RoundToInt(Mathf.Clamp01(f.LifeAgency[k]) * SparkTexels);
                Color32 c = f.LifeControl[k] ? gold : blue;
                for (int y = 0; y < h; y++) s.Pixels[y * MaxYears + k] = c;
            }

            End(s, f, n);
        }

        /// <summary>
        /// Net worth per year on a signed log scale (log of thousands of dollars, so a life from $5K to $5M reads), gold above
        /// the baseline and grey below it (owing more than one owns).
        /// </summary>
        void DrawWealth(PersonFacts f)
        {
            Spark s = wealthSpark;
            int n = Begin(s, f);
            float up = 0, down = 0;
            float[] v = new float[n];
            for (int k = 0; k < n; k++)
            {
                float w = f.LifeWealth[k];
                v[k] = Mathf.Sign(w) * Mathf.Log10(1 + Mathf.Abs(w) / 1000f);
                up = Mathf.Max(up, v[k]);
                down = Mathf.Max(down, -v[k]);
            }

            float range = Mathf.Max(1e-3f, up + down);
            int baseline = Mathf.RoundToInt(down / range * (SparkTexels - 1));
            Color32 gold = EconomyStyle.Capital, grey = GraphStyle.TextDim;
            for (int k = 0; k < n; k++)
            {
                int h = Mathf.RoundToInt(v[k] / range * (SparkTexels - 1));
                int from = Mathf.Min(baseline, baseline + h), to = Mathf.Max(baseline, baseline + h);
                Color32 c = v[k] >= 0 ? gold : grey;
                for (int y = Mathf.Max(0, from); y <= Mathf.Min(SparkTexels - 1, to) && h != 0; y++) s.Pixels[y * MaxYears + k] = c;
                s.Pixels[baseline * MaxYears + k] = v[k] >= 0 ? gold : grey; // the baseline itself shows every year
            }

            End(s, f, n);
        }

        /// <summary>Clears a chart and marks the shown year's column; returns the number of years.</summary>
        static int Begin(Spark s, PersonFacts f)
        {
            int n = Mathf.Clamp(f.LifeAgency.Length, 0, MaxYears);
            for (int i = 0; i < s.Pixels.Length; i++) s.Pixels[i] = Clear;
            int mark = f.Year - f.LifeFirstYear;
            if (mark >= 0 && mark < n)
            {
                for (int y = 0; y < SparkTexels; y++) s.Pixels[y * MaxYears + mark] = YearMark;
            }

            return n;
        }

        static void End(Spark s, PersonFacts f, int n)
        {
            // the shown year's bar stays white over its mark
            int mark = f.Year - f.LifeFirstYear;
            if (mark >= 0 && mark < n)
            {
                for (int y = 0; y < SparkTexels; y++)
                {
                    Color32 c = s.Pixels[y * MaxYears + mark];
                    if (c.a > YearMark.a) s.Pixels[y * MaxYears + mark] = new Color32(255, 255, 255, 255);
                }
            }

            s.Texture.SetPixels32(s.Pixels);
            s.Texture.Apply(false);
            s.Image.uvRect = new UnityEngine.Rect(0, 0, Mathf.Max(1, n) / (float)MaxYears, 1);
        }

        /// <summary>Text from the model is data: keep it from opening rich-text tags.</summary>
        static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<noparse><</noparse>");

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Places everything for a width (reference pixels) and returns the panel's height. Compact (a portrait sheet)
        /// leaves out the notes, the legends and the lifetime charts and spreads money and spending over more columns.
        /// The panel must be active.
        /// </summary>
        public float Layout(float width, bool compact)
        {
            PersonFacts f = facts;
            if (f == null) return 0;
            float inner = width - 2 * Pad, x = Pad, y = Pad - 2;
            bool adult = !f.Child;

            // who
            Line(kicker, x, y, inner - CloseSize - 6, HeadingRow);
            HudKit.PlaceTopLeft(close, width - Pad - CloseSize + 4, Pad - 6, new Vector2(CloseSize, CloseSize));
            y += HeadingRow + 2;
            title.fontSize = compact ? HudKit.SizeTitle - 2 : HudKit.SizeTitle;
            y = Block(title, x, y, inner) + 2;
            y = Block(meta, x, y, inner);
            y = Show(role, f.Role != null) ? Block(role, x, y + 6, inner) : y;

            // the year
            y += SectionGap;
            Line(yearHeading, x, y, inner, HeadingRow);
            y += HeadingRow + 2;
            y = Block(status, x, y, inner);
            for (int i = 0; i < PersonFacts.MoneyRows; i++)
            {
                Show(moneyLabels[i], adult);
                Show(moneyValues[i], adult);
            }

            if (adult)
            {
                int columns = compact ? 3 : 2, rows = (PersonFacts.MoneyRows + columns - 1) / columns;
                float cell = (inner - (columns - 1) * ColumnGap) / columns;
                y += 4;
                for (int i = 0; i < PersonFacts.MoneyRows; i++)
                {
                    // the value right-aligned at its measured width, the label in what is left (an ellipsis rather
                    // than running under the value)
                    float cx = x + i / rows * (cell + ColumnGap), cy = y + i % rows * Row;
                    moneyLabels[i].text = Escape((compact ? f.MoneyShort[i] : f.MoneyLabels[i]) ?? "");
                    float value = Mathf.Min(cell, HudKit.Measure(moneyValues[i], moneyValues[i].text).x);
                    HudKit.PlaceTopLeft(moneyLabels[i].rectTransform, cx, cy,
                        new Vector2(Mathf.Max(0, cell - value - LabelValueGap), Row));
                    HudKit.PlaceTopLeft(moneyValues[i].rectTransform, cx + cell - value, cy, new Vector2(value, Row));
                }

                y += rows * Row;
            }

            y = Show(moneyNote, adult && !compact) ? Block(moneyNote, x, y + 3, inner) : y;

            // spending
            y += SectionGap;
            Line(spendingHeading, x, y, inner, HeadingRow);
            y += HeadingRow + 2;
            {
                int columns = compact ? 2 : 1, rows = (categories.Length + columns - 1) / columns;
                float cell = (inner - (columns - 1) * ColumnGap) / columns;
                for (int c = 0; c < categories.Length; c++)
                {
                    PlaceCategory(categories[c], x + c / rows * (cell + ColumnGap), y + c % rows * Row, cell, CategoryName,
                        f.CategoryShares[c], f.CategoryFantasy[c]);
                }

                y += rows * Row;
            }

            y = Show(spendingLegend, !compact) ? Block(spendingLegend, x, y + 3, inner) : y;

            // the mind
            y += SectionGap;
            Line(mindHeading, x, y, inner, HeadingRow);
            y += HeadingRow + 2;
            PlacePoles(x, y, inner, f.FearShare);
            y += Row;
            ShowBar(reason, adult);
            ShowBar(future, adult);
            ShowBar(agency, adult);
            string[] gaugeNames = compact ? CompactGaugeNames : GaugeNames;
            reason.Name.text = gaugeNames[0];
            future.Name.text = gaugeNames[1];
            agency.Name.text = gaugeNames[2];
            if (adult && compact)
            {
                // the portrait sheet: the three gauges side by side
                float cell = (inner - 2 * ColumnGap) / 3;
                PlaceGauge(reason, x, y, cell, CompactGaugeLabel, CompactValueWidth, f.Reason, f.MeanReason);
                PlaceGauge(future, x + cell + ColumnGap, y, cell, CompactGaugeLabel, CompactValueWidth, f.Future, f.MeanFuture);
                PlaceGauge(agency, x + 2 * (cell + ColumnGap), y, cell, CompactGaugeLabel, CompactValueWidth, f.Agency,
                    f.ControlCut);
                y += Row;
            }
            else if (adult)
            {
                PlaceGauge(reason, x, y, inner, GaugeLabel, ValueWidth, f.Reason, f.MeanReason);
                PlaceGauge(future, x, y + Row, inner, GaugeLabel, ValueWidth, f.Future, f.MeanFuture);
                PlaceGauge(agency, x, y + 2 * Row, inner, GaugeLabel, ValueWidth, f.Agency, f.ControlCut);
                y += 3 * Row;
            }

            y = Show(gaugeLegend, adult && !compact) ? Block(gaugeLegend, x, y + 2, inner) : y;
            y = Show(verdict, adult && f.Verdict.Length > 0) ? Block(verdict, x, y + 5, inner) : y;
            y = Show(wants, adult && f.Wants.Length > 0) ? Block(wants, x, y + 5, inner) : y;
            y = Show(fears, adult && f.Fears.Length > 0) ? Block(fears, x, y + 2, inner) : y;

            // the whole life
            bool life = !compact;
            Show(lifeHeading, life);
            ShowSpark(agencySpark, life);
            ShowSpark(wealthSpark, life);
            if (life)
            {
                y += SectionGap;
                Line(lifeHeading, x, y, inner, HeadingRow);
                y += HeadingRow + 4;
                PlaceSpark(agencySpark, x, y, inner);
                y += SparkHeight + 4;
                PlaceSpark(wealthSpark, x, y, inner);
                y += SparkHeight;
            }

            y = Show(hint, !compact) ? Block(hint, x, y + 10, inner) : y;
            return Mathf.Ceil(y + Pad - 2);
        }

        /// <summary>A one-line text at (x, y) of the panel, width and height given.</summary>
        static void Line(TextMeshProUGUI t, float x, float y, float width, float height)
        {
            t.gameObject.SetActive(true);
            HudKit.PlaceTopLeft(t.rectTransform, x, y, new Vector2(width, height));
        }

        /// <summary>A wrapped text at (x, y), as tall as its lines; returns the y below it.</summary>
        static float Block(TextMeshProUGUI t, float x, float y, float width)
        {
            t.gameObject.SetActive(true);
            float h = t.text.Length > 0 ? HudKit.Measure(t, t.text, width).y : 0;
            HudKit.PlaceTopLeft(t.rectTransform, x, y, new Vector2(width, h));
            return y + h;
        }

        static bool Show(Component c, bool show)
        {
            if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
            return show;
        }

        static void ShowBar(Bar b, bool show)
        {
            Show(b.Name, show);
            Show(b.Value, show);
            Show(b.Track, show);
            if (b.Swatch != null) Show(b.Swatch, show);
        }

        static void ShowSpark(Spark s, bool show)
        {
            Show(s.Label, show);
            Show(s.Value, show);
            Show(s.Back, show);
            Show(s.Image, show);
        }

        /// <summary>A spending category: swatch, name, the share as a bar of the whole (the fantasy part underlined), percent.</summary>
        static void PlaceCategory(Bar b, float x, float y, float width, float nameWidth, float share, float fantasy)
        {
            ShowBar(b, true);
            float mid = y + Row * 0.5f;
            HudKit.PlaceTopLeft(b.Swatch.rectTransform, x, mid - SwatchSize * 0.5f, new Vector2(SwatchSize, SwatchSize));
            float nameX = x + SwatchSize + 6;
            HudKit.PlaceTopLeft(b.Name.rectTransform, nameX, y, new Vector2(nameWidth - (nameX - x) - 4, Row));
            HudKit.PlaceTopLeft(b.Value.rectTransform, x + width - PercentWidth, y, new Vector2(PercentWidth, Row));
            float track = Mathf.Max(10, width - nameWidth - PercentWidth - 6);
            HudKit.PlaceTopLeft(b.Track.rectTransform, x + nameWidth, mid - BarHeight * 0.5f - 1, new Vector2(track, BarHeight));
            HudKit.PlaceTopLeft(b.Fill.rectTransform, 0, 0, new Vector2(track * Mathf.Clamp01(share), BarHeight));
            Show(b.Extra, fantasy > 0.0005f);
            HudKit.PlaceTopLeft(b.Extra.rectTransform, 0, BarHeight + 1, new Vector2(track * Mathf.Clamp01(fantasy), FantasyLine));
        }

        /// <summary>Desire against fear: one bar, rose for the desire part and ice for the fear part of the spending's motive.</summary>
        void PlacePoles(float x, float y, float width, float fearShare)
        {
            ShowBar(poles, true);
            HudKit.PlaceTopLeft(poles.Name.rectTransform, x, y, new Vector2(PoleLabel, Row));
            HudKit.PlaceTopLeft(poles.Value.rectTransform, x + width - PoleLabel, y, new Vector2(PoleLabel, Row));
            float track = Mathf.Max(10, width - 2 * PoleLabel - 8);
            float desire = track * (1 - Mathf.Clamp01(fearShare));
            HudKit.PlaceTopLeft(poles.Track.rectTransform, x + PoleLabel + 4, y + (Row - BarHeight) * 0.5f, new Vector2(track, BarHeight));
            HudKit.PlaceTopLeft(poles.Fill.rectTransform, 0, 0, new Vector2(desire, BarHeight));
            HudKit.PlaceTopLeft(poles.Extra.rectTransform, desire, 0, new Vector2(track - desire, BarHeight));
        }

        /// <summary>A gauge from 0 to 1 with a tick (a mean, or the cut for control); no tick when it is negative.</summary>
        static void PlaceGauge(Bar b, float x, float y, float width, float labelWidth, float valueWidth, float value,
            float tick)
        {
            ShowBar(b, true);
            HudKit.PlaceTopLeft(b.Name.rectTransform, x, y, new Vector2(labelWidth - 4, Row));
            HudKit.PlaceTopLeft(b.Value.rectTransform, x + width - valueWidth, y, new Vector2(valueWidth, Row));
            float track = Mathf.Max(10, width - labelWidth - valueWidth - 6);
            HudKit.PlaceTopLeft(b.Track.rectTransform, x + labelWidth, y + (Row - BarHeight) * 0.5f, new Vector2(track, BarHeight));
            HudKit.PlaceTopLeft(b.Fill.rectTransform, 0, 0, new Vector2(track * Mathf.Clamp01(value), BarHeight));
            Show(b.Extra, tick >= 0);
            HudKit.PlaceTopLeft(b.Extra.rectTransform, track * Mathf.Clamp01(tick) - TickWidth * 0.5f, -TickOverhang,
                new Vector2(TickWidth, BarHeight + 2 * TickOverhang));
        }

        static void PlaceSpark(Spark s, float x, float y, float width)
        {
            HudKit.PlaceTopLeft(s.Label.rectTransform, x, y, new Vector2(SparkLabel, SparkHeight));
            HudKit.PlaceTopLeft(s.Value.rectTransform, x + width - SparkValue, y, new Vector2(SparkValue, SparkHeight));
            Vector2 size = new Vector2(Mathf.Max(10, width - SparkLabel - SparkValue - 6), SparkHeight);
            HudKit.PlaceTopLeft(s.Back.rectTransform, x + SparkLabel, y, size);
            HudKit.PlaceTopLeft(s.Image.rectTransform, x + SparkLabel, y, size);
        }
    }
}
