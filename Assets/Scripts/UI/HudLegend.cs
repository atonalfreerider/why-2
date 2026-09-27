using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Humans;

namespace Why.UI
{
    /// <summary>
    /// Bottom-left key to the graph: the three level swatches (the only hue in the UI) with the one-line
    /// grammar of the axes, and above it the lifeline readout ("1 line = 100,000 people - United States
    /// 1950 - now") while lifelines are visible.
    /// </summary>
    public sealed class HudLegend
    {
        const string Grammar = "around: time (log clock)  |  up: hierarchy  |  inward: relevance to us";
        const float SwatchSize = 10f;
        const float SwatchGap = 7f;
        const float ItemGap = 22f;
        const float RowGap = 6f;

        /// <summary>How quickly the legend eases to a new height (per second, exponential).</summary>
        const float MoveRate = 9f;

        readonly RectTransform legend;
        readonly RectTransform readout;
        readonly TextMeshProUGUI readoutText;
        readonly UiFade readoutFade;
        double shownPeople = -1;
        string shownContext;
        float bottom, bottomTarget;
        float left = HudKit.Margin;

        /// <summary>Legend width in reference pixels (the preset bar keeps clear of it).</summary>
        public float Width { get; }

        /// <summary>Legend height in reference pixels.</summary>
        public float Height { get; }

        public HudLegend(Transform parent)
        {
            legend = HudKit.FramedPanel(parent, "Legend", 0.62f);

            // row 1: level swatches
            float x = HudKit.Pad;
            float rowHeight = HudKit.SizeBody + 6;
            x = Swatch("Matter", GraphStyle.Matter, x, rowHeight);
            x = Swatch("Life", GraphStyle.Life, x + ItemGap, rowHeight);
            x = Swatch("Humans", GraphStyle.Humans, x + ItemGap, rowHeight);

            // row 2: what the axes mean
            TextMeshProUGUI grammar = HudKit.Line(legend, "Grammar", Grammar, HudKit.SizeSmall - 0.5f, GraphStyle.TextDim);
            Vector2 g = HudKit.FitText(grammar);
            HudKit.PlaceTopLeft(grammar.rectTransform, HudKit.Pad, HudKit.Pad + rowHeight + RowGap, g);

            Width = Mathf.Ceil(Mathf.Max(x, HudKit.Pad + g.x) + HudKit.Pad);
            Height = Mathf.Ceil(HudKit.Pad + rowHeight + RowGap + g.y + HudKit.Pad - 2);
            legend.Place(Vector2.zero, Vector2.zero, new Vector2(HudKit.Margin, HudKit.Margin), new Vector2(Width, Height));

            readout = HudKit.FramedPanel(parent, "LifelineReadout", 0.62f);
            readout.Place(Vector2.zero, Vector2.zero, Vector2.zero, readout.sizeDelta);
            readoutText = HudKit.Line(readout, "Text", "", HudKit.SizeSmall, GraphStyle.Text);
            readoutFade = new UiFade(readout.gameObject, 0, 4f, false);
            SetBottom(HudKit.Margin, true);
        }

        float Swatch(string name, Color color, float x, float rowHeight)
        {
            Image dot = UiFactory.Panel(legend, name + " swatch", color, false);
            // shrink the factory sprite's 14 px corners to half the swatch size: a dot
            dot.pixelsPerUnitMultiplier = 14f / (SwatchSize * 0.5f);
            HudKit.PlaceTopLeft(dot.rectTransform, x, HudKit.Pad + (rowHeight - SwatchSize) * 0.5f,
                new Vector2(SwatchSize, SwatchSize));
            x += SwatchSize + SwatchGap;

            TextMeshProUGUI label = HudKit.Line(legend, name, name, HudKit.SizeBody, GraphStyle.Text);
            Vector2 size = HudKit.FitText(label);
            HudKit.PlaceTopLeft(label.rectTransform, x, HudKit.Pad + (rowHeight - size.y) * 0.5f, size);
            return x + size.x;
        }

        /// <summary>
        /// Move the legend (and the readout above it) so its bottom edge sits at y (canvas units), at once or
        /// easing there (e.g. when the preset bar beneath it steps aside for the tour).
        /// </summary>
        public void SetBottom(float y, bool instant)
        {
            bottomTarget = Mathf.Round(y);
            if (!instant) return;
            bottom = bottomTarget;
            ApplyBottom();
        }

        /// <summary>Distance of the legend and the readout from the left screen edge (canvas units).</summary>
        public void SetLeft(float x)
        {
            left = Mathf.Round(x);
            ApplyBottom();
        }

        void ApplyBottom()
        {
            float y = Mathf.Round(bottom);
            legend.anchoredPosition = new Vector2(left, y);
            readout.anchoredPosition = new Vector2(left, y + Height + HudKit.Gap);
        }

        /// <summary>Follow <see cref="HumansLod"/>: show what one lifeline stands for, hide when none are drawn.</summary>
        public void Tick(float dt)
        {
            if (bottom != bottomTarget)
            {
                bottom = Mathf.Lerp(bottom, bottomTarget, 1f - Mathf.Exp(-MoveRate * dt));
                if (Mathf.Abs(bottom - bottomTarget) < 0.5f) bottom = bottomTarget;
                ApplyBottom();
            }

            double people = HumansLod.PeoplePerLine;
            if (double.IsNaN(people) || double.IsInfinity(people)) people = 0;
            string context = HumansLod.Context ?? "";
            if (people != shownPeople || context != shownContext)
            {
                shownPeople = people;
                shownContext = context;
                readoutFade.Show(people > 0); // activates the panel before its text is measured
                if (people > 0) SetReadout(people, context);
            }

            readoutFade.Tick(dt);
        }

        void SetReadout(double people, string context)
        {
            string noun = people > 0.5 && people < 1.5 ? " person" : " people";
            string text = "1 line = <b>" + HudKit.FormatCount(people) + "</b>" + noun;
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
