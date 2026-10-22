using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;

namespace Why.Economy.UI
{
    /// <summary>
    /// "Where you stand": the viewer tells the model about their household with steppers (no keyboard, so it works with
    /// a mouse and with hands in the headset): age, household income, net worth, debt, saving, the industry they work in,
    /// whether they live with a partner, own their home or a business. The "you" view draws them on the hill
    /// (<see cref="HillYouLayer"/>) and this panel reads out the place: percentiles, rung, where each dollar they spend
    /// goes and which way it flows, how far they are from living on what they own, and the odds of people who stood there
    /// a decade ago. Nothing entered is stored or sent anywhere. Shown in the "you" view.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class YouPanel : GraphModule
    {
        static readonly int[] Ages = { 18, 20, 22, 25, 28, 30, 33, 35, 38, 40, 43, 45, 48, 50, 53, 55, 58, 60, 63, 65, 68, 70, 75, 80, 85, 90 };
        static readonly double[] Incomes = { 0, 10e3, 15e3, 20e3, 25e3, 30e3, 35e3, 40e3, 45e3, 50e3, 60e3, 70e3, 80e3, 85e3, 90e3, 100e3, 120e3, 140e3, 160e3,
            180e3, 200e3, 250e3, 300e3, 400e3, 500e3, 750e3, 1e6, 2e6, 5e6, 10e6, 50e6, 100e6 };
        static readonly double[] Worths = { -100e3, -50e3, -20e3, -10e3, 0, 5e3, 10e3, 25e3, 50e3, 75e3, 100e3, 150e3, 190e3, 250e3, 300e3, 400e3, 500e3, 750e3,
            1e6, 1.5e6, 2e6, 3e6, 5e6, 10e6, 20e6, 50e6, 100e6, 1e9, 10e9, 100e9 };
        static readonly double[] Debts = { 0, 5e3, 10e3, 20e3, 30e3, 50e3, 75e3, 100e3, 120e3, 150e3, 200e3, 300e3, 400e3, 500e3, 750e3, 1e6, 2e6 };
        static readonly double[] Savings = { double.NaN, -.2, -.1, -.05, 0, .03, .05, .08, .1, .15, .2, .25, .3, .4, .5 };

        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI title, reading;
        readonly List<(TextMeshProUGUI value, Func<string> text)> values = new List<(TextMeshProUGUI, Func<string>)>();
        readonly List<int> industries = new List<int>();
        int shownVersion = -1; bool seeded;
        float rowY;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Where you stand", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "You", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(392, 700);
            title = Text("Title", 15, 12, 10, 368, 22); title.text = "<b>WHERE YOU STAND</b>";
            var note = Text("Note", 10.5f, 12, 32, 368, 30);
            note.text = "Tell the model about your household. Nothing you enter is stored or sent: it places you among the year's simulated population.";
            note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            rowY = 68;
            Stepper("Age", () => YouState.Inputs.Age.ToString(LandFacts.Ci), d => Change(i => { i.Age = Step(Ages, i.Age, d); return i; }));
            Stepper("Household income", () => Dollars(YouState.Inputs.Income) + " a year", d => Change(i => { i.Income = Step(Incomes, i.Income, d); return i; }));
            Stepper("Net worth", () => Dollars(YouState.Inputs.NetWorth), d => Change(i => { i.NetWorth = Step(Worths, i.NetWorth, d); return i; }));
            Stepper("Debt", () => Dollars(YouState.Inputs.Debt), d => Change(i => { i.Debt = Step(Debts, i.Debt, d); return i; }));
            Stepper("Saving", () => double.IsNaN(YouState.Inputs.Saving) ? "typical at your income" : LandFacts.Percent(YouState.Inputs.Saving) + " of after-tax income",
                d => Change(i => { i.Saving = StepSaving(i.Saving, d); return i; }));
            Stepper("Works in", () => IndustryName(YouState.Inputs.Industry), d => Change(i => { i.Industry = StepIndustry(i.Industry, d); return i; }));
            Toggle("Partner", 12, () => YouState.Inputs.Partnered ? "with a partner" : "single", () => Change(i => { i.Partnered = !i.Partnered; return i; }));
            Toggle("Home", 136, () => YouState.Inputs.Homeowner ? "owns a home" : "rents", () => Change(i => { i.Homeowner = !i.Homeowner; return i; }));
            Toggle("Business", 260, () => YouState.Inputs.Owner ? "owns a business" : "no business", () => Change(i => { i.Owner = !i.Owner; return i; }));
            rowY += 30;
            reading = Text("Reading", 12, 12, rowY, 368, 700 - rowY - 44);
            reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            var median = UiFactory.Button(panel, "Median", "Start from the median household", 11, Seed);
            Place((RectTransform)median.transform, 12, 700 - 34, 368, 24);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            industries.Clear(); industries.Add(-1);
            var data = LandService.Model?.Data;
            if (data == null) return;
            for (int i = 0; i < data.Industries.Count; i++) if (data.Industries[i].TierIndex > 0) industries.Add(i);
            for (int i = 0; i < data.Industries.Count; i++) if (data.Industries[i].TierIndex == 0) industries.Add(i);
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == "you";   // also during the tour: its "you" stop asks the viewer to use it
            panel.gameObject.SetActive(show);
            if (!show) return;
            var r = YouState.Reading;
            if (!seeded && r != null && !YouState.Entered) Seed();
            // portrait: a bottom sheet over the preset bar; landscape: the right column under the year
            Vector2 canvas = UiFactory.CanvasSize;
            // portrait: a bottom sheet over the preset bar, its reading in a smaller face; landscape: the right column under the year
            bool portrait = ScreenLayout.IsPortrait;
            if (portrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(Mathf.Min(420, canvas.x - 16), Mathf.Min(700, canvas.y * .62f)); }
            else { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(392, Mathf.Min(700, canvas.y - 230)); }
            reading.fontSize = portrait ? 9.5f : 12;
            if (r == null || shownVersion == YouState.Version && reading.text.Length > 0) return;
            shownVersion = YouState.Version;
            foreach (var v in values) v.value.text = v.text();
            title.text = "<b>WHERE YOU STAND</b> · " + r.Year;
            reading.text = Describe(r, YouState.Inputs);
        }

        /// <summary>Starts from the year's median household (the model's medians: household income, net worth per adult
        /// doubled for a couple).</summary>
        void Seed()
        {
            var r = YouState.Reading; seeded = true;
            if (r == null) return;
            var i = YouState.Inputs;
            i.Income = Round(r.MedianIncome); i.NetWorth = Round(r.MedianWealth * (i.Partnered ? 2 : 1)); i.Saving = double.NaN;
            if (r.ModalIndustry >= 0) i.Industry = r.ModalIndustry;
            YouState.Set(i, false);
        }

        static string Describe(YouModel.Result r, YouModel.Inputs you)
        {
            var data = LandService.Model.Data;
            string Pct(double v) => LandFacts.Percent(v);
            string place = you.Industry >= 0 && you.Industry < data.Industries.Count && data.Industries[you.Industry].TierIndex > 0
                ? "on the hill of <b>" + data.Industries[you.Industry].Name + "</b>" : you.Industry >= 0 ? "on the valley floor beside the government that pays you" : "on the valley floor (no paying hill)";
            string wealthGroup = r.WealthGroup == 3 ? "the top 1% (the clouds)" : r.WealthGroup == 2 ? "the next 9%" : r.WealthGroup == 1 ? "the 50-90%" : "the bottom half";
            string s = "<b>Your altitude.</b> The " + Ord(r.IncomeRank) + " percentile of household income (" + Mobility.QuintileNames[r.Quintile] + ") and the " +
                Ord(r.WealthRank) + " of net worth among adults: " + wealthGroup + " by wealth. You stand " + place + ".\n\n";
            s += "<b>Your dollar.</b> People at your income spend " + Pct(r.Fear) + " of their money moving away from a fear and buy fantasy with " + Pct(r.Fantasy) + ". " +
                "Of each dollar you spend, " + Cents(r.Wages) + " becomes wages, " + Cents(r.Owners) + " owners' surplus (" + Pct(r.Persuaded) + " of it sold by persuasion), " +
                Cents(r.Taxes) + " taxes, " + Cents(r.Upkeep) + " upkeep and " + Cents(r.Imports) + " leaves for imports: about " + PersonLife.Money(r.PaysOwners) +
                " a year to owners. <color=#FFC04A>" + Pct(r.Uphill) + " of the income it pays flows uphill</color> to people ranked above you; the top 1% hold " +
                Pct(r.OwnersToTop1) + " of all income from ownership.\n\n";
            string years = r.YearsToIndependence == 0 ? "you are there" : double.IsNaN(r.YearsToIndependence) ? "not within a century at this saving" : r.YearsToIndependence.ToString("0", LandFacts.Ci) + " years";
            s += "<b>Your autonomy.</b> Saving " + Pct(r.Saving) + " (typical here " + Pct(r.TypicalSaving) + "), your net worth covers " + r.Runway.ToString("0.0", LandFacts.Ci) +
                " years of spending; the model counts three in financial assets as material autonomy. Living on what you own takes about " + PersonLife.Money(r.Independence) +
                " (25 years of spending at a 4% yield): " + years + " at a 4% real return.\n\n";
            if (r.OddsLines > 0)
            {
                s += "<b>Your odds.</b> Of adults in your fifth aged " + Mobility.AgeBandNames[r.AgeBand] + " ten years ago, " + Pct(r.Up) + " climbed, " + Pct(r.Stay) + " stayed and " + Pct(r.Down) + " fell.";
                var m = HillMobilityLayer.Shown;
                if (m != null)
                {
                    var falls = m.Leading(false, 1.3, .04, 3); var climbs = m.Leading(true, 1.3, .04, 3);
                    if (falls.Count > 0) s += " Falls came with " + Join(falls, m, false) + ";";
                    if (climbs.Count > 0) s += " climbs with " + Join(climbs, m, true) + ".";
                }
            }
            s += "\n\n<size=85%><color=#8A96A6>A reading of a calibrated simulation, not advice: " + r.Neighbors + " simulated adults at your income stand behind the averages; each stands for 100,000 people.</color></size>";
            return s;
        }

        static string Join(List<Mobility.Cause> list, Mobility m, bool up)
        {
            var parts = new List<string>();
            foreach (var c in list) parts.Add(Mobility.CauseNames[(int)c] + " (" + LandFacts.Percent(m.ShareWith(c, up)) + ")");
            return string.Join(", ", parts);
        }

        // ------------------------------------------------------------------ steppers

        void Stepper(string label, Func<string> text, Action<int> step)
        {
            var name = Text(label, 12, 12, rowY + 3, 118, 20); name.text = label; name.color = new Color(.72f, .78f, .86f);
            var minus = UiFactory.Button(panel, label + " -", "−", 14, () => step(-1)); Place((RectTransform)minus.transform, 132, rowY, 26, 24);
            var value = Text(label + " value", 12.5f, 162, rowY + 3, 182, 20); value.alignment = TextAlignmentOptions.Center;
            var plus = UiFactory.Button(panel, label + " +", "+", 14, () => step(1)); Place((RectTransform)plus.transform, 348, rowY, 26, 24);
            values.Add((value, text)); rowY += 29;
        }

        void Toggle(string name, float x, Func<string> text, Action flip)
        {
            var b = UiFactory.Button(panel, name, text(), 11, flip); Place((RectTransform)b.transform, x, rowY + 2, 118, 24);
            values.Add((b.GetComponentInChildren<TextMeshProUGUI>(), text));
        }

        static void Change(Func<YouModel.Inputs, YouModel.Inputs> edit) => YouState.Set(edit(YouState.Inputs));

        static int Step(int[] ladder, int value, int d)
        {
            int k = 0; while (k < ladder.Length - 1 && ladder[k] < value) k++;
            if (ladder[k] != value && d < 0) return ladder[Math.Max(0, k - 1)];
            return ladder[Math.Max(0, Math.Min(ladder.Length - 1, k + d))];
        }

        static double Step(double[] ladder, double value, int d)
        {
            int k = 0; while (k < ladder.Length - 1 && ladder[k] < value - 1e-6) k++;
            if (Math.Abs(ladder[k] - value) > 1e-6 && d < 0) return ladder[Math.Max(0, k - 1)];
            return ladder[Math.Max(0, Math.Min(ladder.Length - 1, k + d))];
        }

        static double StepSaving(double value, int d)
        {
            int k = 0;
            if (!double.IsNaN(value)) { k = 1; while (k < Savings.Length - 1 && Savings[k] < value - 1e-6) k++; }
            return Savings[Math.Max(0, Math.Min(Savings.Length - 1, k + d))];
        }

        int StepIndustry(int value, int d)
        {
            if (industries.Count == 0) return value;
            int k = Math.Max(0, industries.IndexOf(value));
            return industries[(k + d + industries.Count) % industries.Count];
        }

        static string IndustryName(int i)
        {
            var data = LandService.Model?.Data;
            return i < 0 || data == null || i >= data.Industries.Count ? "not working / retired" : data.Industries[i].Name;
        }

        static double Round(double v)
        {
            if (v <= 0) return 0;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Round(v / mag) * mag;
        }

        static string Dollars(double v) => (v < 0 ? "−" : "") + PersonLife.Money(Math.Abs(v));
        static string Cents(double share) => Mathf.RoundToInt((float)share * 100).ToString(LandFacts.Ci) + "¢";
        static string Ord(double share)
        {
            int p = Mathf.Clamp(Mathf.RoundToInt((float)share * 100), 1, 99);
            string suffix = p % 100 >= 11 && p % 100 <= 13 ? "th" : p % 10 == 1 ? "st" : p % 10 == 2 ? "nd" : p % 10 == 3 ? "rd" : "th";
            return p.ToString(LandFacts.Ci) + suffix;
        }

        TextMeshProUGUI Text(string name, float size, float x, float y, float w, float h)
        {
            var t = UiFactory.Text(panel, name, "", size, GraphStyle.Text); Place(t.rectTransform, x, y, w, h); return t;
        }

        static void Place(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height);
        }
    }
}
