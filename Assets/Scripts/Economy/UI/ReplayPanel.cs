using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The replay of the wave on screen (<see cref="EconomyReplay"/>): R plays or pauses it (Shift+R from the start; the
    /// year chip's button and the headset menu do the same), and while it runs a compact readout stands at the bottom of
    /// the screen like a caption, above the preset bar: the year on screen and what changed in it, from data already in
    /// the project (<see cref="ReplayFacts"/>): GDP and its real growth with the market's real value added (the wave's
    /// width), the tiers' shares of the market, the federal funds and tariff rates, federal outlays, the companies born and
    /// ended, the decade's ladder while the mobility view is shown (there the tiers and companies step aside, so the
    /// ladder's own labels stay clear), and the turning point when the year is one. Views
    /// framed on the crest follow it as it advances (the camera's target moves with the view's own pose, so the viewer's
    /// orbit, zoom and pan stay theirs); views of the whole time axis (the overview, the companies, the money chart) hold
    /// still and the wave grows across them. On a portrait screen the readout stands under the title and the year chip.
    /// It is a canvas the headset keeps (something to read), at the bottom of the HUD plane.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class ReplayPanel : GraphModule
    {
        /// <summary>The canvas's name (the headset keeps it: <see cref="Xr.XrSession"/>).</summary>
        public const string CanvasName = "EconomyReplay";

        /// <summary>Above the inspectors (44) and the social panel (45), below the strategy panel (47) and the tour (50).</summary>
        const int SortingOrder = 46;

        /// <summary>The widest the readout runs (canvas units) and its text sizes.</summary>
        const float MaxWidth = 780f, YearSize = HudKit.SizeTitle + 4, BodySize = HudKit.SizeSmall;

        const float FadeSpeed = 4f;

        /// <summary>Seconds over which a followed view's camera closes most of the way on the crest's move (time constant).</summary>
        const float FollowSeconds = .3f;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The readout's box while it is shown (canvas units of any canvas made by <see cref="UiFactory.CreateCanvas"/>).</summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        GraphRoot root;
        RectTransform canvasRect, panel;
        TextMeshProUGUI yearText, bodyText;
        UiFade fade;
        HudBlocks hud;
        EconomyModel model;
        string moneyJson;
        bool loaded, dirty = true;
        int factsTries;

        /// <summary>What the readout was written for: the year, the replay's state, the view, the mobility model, the screen's shape.</summary>
        int textYear = -1, textVersion = -1;

        string textView;
        Mobility textMobility;
        bool textPortrait;
        HudFrame laidOutFrame;

        // the camera following the crest: the view's pose last seen, and the move still to make
        ViewPreset followed;
        Vector3? followedTarget;
        Vector3 pending;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            EconomyReplay.Reset();
            Canvas canvas = UiFactory.CreateCanvas(CanvasName, SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = HudKit.FramedPanel(canvasRect, "Replay", 0.84f);
            yearText = HudKit.Line(panel, "Year", "", YearSize, GraphStyle.Text, FontStyles.Bold);
            bodyText = HudKit.Line(panel, "Body", "", BodySize, GraphStyle.TextDim);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 6;
            // created last: a hidden fade deactivates its panel
            fade = new UiFade(panel.gameObject, 0, FadeSpeed, false);
            root.FocusChanged += OnFocusChanged;
            root.OrientationChanged += OnOrientation;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            moneyJson = root.Context.Text(ReplayFacts.MoneyPath) ?? Resources.Load<TextAsset>(ReplayFacts.MoneyPath)?.text;
            hud = new HudBlocks(root);
            loaded = true;
        }

        void OnDestroy()
        {
            if (root != null)
            {
                root.FocusChanged -= OnFocusChanged;
                root.OrientationChanged -= OnOrientation;
            }

            EconomyReplay.Reset();
            Occupied = UiBox.Empty;
        }

        /// <summary>A new view: the root flies to its pose as it is now, so nothing is left to follow.</summary>
        void OnFocusChanged(ViewPreset preset)
        {
            followed = preset;
            followedTarget = preset?.FixedTarget;
            pending = Vector3.zero;
            dirty = true;
        }

        /// <summary>The screen turned: the land's view layer flies to the view's new pose itself.</summary>
        void OnOrientation()
        {
            followedTarget = root.CurrentPreset?.FixedTarget;
            pending = Vector3.zero;
            dirty = true;
        }

        void Update()
        {
            if (!loaded) return;
            if (EconomyReplay.Facts == null) BuildFacts();
            if (!root.TourActive) HandleKeys();
            float dt = GraphTime.Delta;
            EconomyReplay.Tick(dt, root.TourActive);
            Follow(dt);
        }

        /// <summary>The facts once the data they read is loaded (the company lives come with the wave's first build).</summary>
        void BuildFacts()
        {
            if (model?.Data == null) return;
            if (CompanyLives.Get() == null && ++factsTries < 120) return;
            ReplayFacts facts = ReplayFacts.Build(model.Data, moneyJson, HillFoundationLayer.History, CompanyLives.Get(),
                model.Data.FirstYear, EconomyState.MaxYear);
            EconomyReplay.Facts = facts;
            Debug.Log("[Why] Replay facts " + facts.Log);
            foreach (string problem in facts.Problems()) Debug.LogError("[Why] Replay facts check failed: " + problem);
            dirty = true;
        }

        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || HudKit.TypingInField() || !kb.rKey.wasPressedThisFrame) return;
            if (kb.shiftKey.isPressed) EconomyReplay.Play(true);
            else EconomyReplay.Toggle();
        }

        // ------------------------------------------------------------------ the camera follows the crest

        /// <summary>
        /// While a replay is under way, a view whose pose stands on the crest (its target moves when the catalog is
        /// refreshed for the new year) takes the camera along: the move is added to the camera's own target and eased in,
        /// after any flight on its way. Views with camera logic of their own (the section's cut, the followed person, the
        /// viewer's place) and views of the whole time axis (whose targets do not move) are left alone.
        /// </summary>
        void Follow(float dt)
        {
            ViewPreset p = root.CurrentPreset;
            if (!ReferenceEquals(p, followed))
            {
                OnFocusChanged(p);
                return;
            }

            if (p == null || !p.FixedTarget.HasValue || root.Rig == null) return;
            Vector3 target = p.FixedTarget.Value;
            if (followedTarget.HasValue && target != followedTarget.Value && EconomyReplay.Active && FollowsCrest(p.Id))
            {
                pending += target - followedTarget.Value;
            }

            followedTarget = target;
            if (pending.sqrMagnitude < 1e-8f || root.Rig.Flying) return;
            Vector3 move = pending.sqrMagnitude < 1e-4f ? pending : pending * (1 - Mathf.Exp(-dt / FollowSeconds));
            CameraPose pose = root.Rig.Pose;
            pose.Target += move;
            root.Rig.SetPose(pose);
            pending -= move;
        }

        static bool FollowsCrest(string id) => id != "section" && id != "you" && Array.IndexOf(HillPresets.PersonViews, id) < 0;

        // ------------------------------------------------------------------ the readout

        void LateUpdate()
        {
            float dt = GraphTime.Delta;
            fade.Tick(dt);
            int year = EconomyReplay.ShownYear;
            bool show = loaded && EconomyReplay.Active && year > 0 && EconomyReplay.Facts != null && !root.TourActive;
            if (show != fade.Shown)
            {
                fade.Show(show);
                dirty = true;
            }

            if (!fade.Shown)
            {
                Occupied = UiBox.Empty;
                return;
            }

            string view = LandView.PresetId;
            Mobility mobility = view == "mobility" ? HillMobilityLayer.Shown : null;
            if (mobility != null && mobility.Year != year) mobility = null;
            bool portrait = ScreenLayout.IsPortrait;
            if (year != textYear || EconomyReplay.Version != textVersion || view != textView || mobility != textMobility || portrait != textPortrait)
            {
                textYear = year;
                textVersion = EconomyReplay.Version;
                textView = view;
                textMobility = mobility;
                textPortrait = portrait;
                yearText.text = Header(year);
                bodyText.text = Body(EconomyReplay.Facts, year, mobility, portrait, view == "mobility");
                dirty = true;
            }

            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            if (!dirty && f.Near(laidOutFrame)) return;
            dirty = false;
            laidOutFrame = f;
            Layout(f);
        }

        /// <summary>"1982  ▸ replay" in the year's own size, the state of the replay beside it.</summary>
        static string Header(int year)
        {
            string state = EconomyReplay.Playing ? "replaying the wave" : year >= EconomyState.MaxYear ? "the latest year" : "paused";
            return year.ToString(Ci) + "<size=60%><color=#" + UiFactory.Hex(GraphStyle.TextDim) + ">   " + state +
                   " · R " + (EconomyReplay.Playing ? "pauses" : "plays") + "</color></size>";
        }

        /// <summary>
        /// The year's lines (rich text: labels dim, values bright, a fall rose, the turning point in its color): the economy,
        /// the market's tiers, the state's levers, the companies, the decade's ladder (mobility view), the turning point.
        /// <paramref name="ladderView"/>: the mobility view, whose ladder fills the screen down to its labels, keeps only the
        /// economy, the state, the ladder and the turning point.
        /// </summary>
        public static string Body(ReplayFacts facts, int year, Mobility mobility, bool portrait, bool ladderView = false)
        {
            if (facts == null || year < facts.First || year > facts.Last) return "";
            ReplayFacts.Year y = facts.At(year);
            string v = "<color=#" + UiFactory.Hex(GraphStyle.Text) + ">", rose = "<color=#" + UiFactory.Hex(EconomyStyle.Desire) + ">",
                gold = "<color=#" + UiFactory.Hex(EconomyStyle.Capital) + ">", steel = "<color=#" + UiFactory.Hex(EconomyStyle.Government) + ">";
            const string end = "</color>", dot = "  ·  ";
            var b = new StringBuilder();

            // the economy: its size, its real growth, the wave's own width (the market's real value added)
            b.Append("GDP ").Append(v).Append(LandFacts.Money(y.Gdp, 2)).Append(end);
            if (y.Estimate) b.Append(" (estimate)");
            if (!double.IsNaN(y.RealGrowth)) b.Append(dot).Append("real growth ").Append(Change(y.RealGrowth, v, rose));
            if (!double.IsNaN(y.MarketGrowth))
            {
                b.Append(dot).Append("the market's real value added ").Append(Change(y.MarketGrowth, v, rose));
                if (y.Fell) b.Append(": ").Append(rose).Append("the wave narrows").Append(end);
            }

            // the market's tiers: their shares of its value added, and what moved by half a point or more
            if (!ladderView)
            {
                b.Append('\n').Append("the market  ");
                for (int k = 0; k < 4; k++)
                {
                    if (k > 0) b.Append(dot);
                    b.Append(facts.TierNames[k].ToLowerInvariant()).Append(' ').Append(v).Append(LandFacts.Percent(y.TierShare[k])).Append(end);
                    if (Math.Abs(y.TierChange[k]) >= .005) b.Append(" (").Append(ReplayFacts.Signed(100 * y.TierChange[k], 1)).Append(')');
                }
            }

            // the state's levers: the price of money, the edge of trade, the federal budget
            var state = new List<string>();
            if (!double.IsNaN(y.FedFunds)) state.Add("federal funds " + v + y.FedFunds.ToString("0.0", Ci) + "%" + end + Points(y.FedFundsChange));
            if (!double.IsNaN(y.Tariff)) state.Add("tariff " + v + y.Tariff.ToString("0.0", Ci) + "%" + end + Points(y.TariffChange));
            if (!double.IsNaN(y.Outlays))
            {
                string budget = "federal outlays " + v + LandFacts.Percent(y.Outlays, 1) + end + " of GDP";
                if (!portrait && !double.IsNaN(y.Receipts)) budget += ", receipts " + v + LandFacts.Percent(y.Receipts, 1) + end;
                state.Add(budget);
            }

            if (state.Count > 0) b.Append('\n').Append("the state  ").Append(string.Join(dot, state));

            // the companies born and ended on the wave
            if (!ladderView && (y.Born.Count > 0 || y.Ended.Count > 0))
            {
                b.Append('\n').Append("companies  ");
                if (y.Born.Count > 0) b.Append("born ").Append(v).Append(Names(y.Born)).Append(end);
                if (y.Born.Count > 0 && y.Ended.Count > 0) b.Append(dot);
                if (y.Ended.Count > 0) b.Append("ended ").Append(v).Append(Names(y.Ended)).Append(end);
            }

            // the decade's ladder (the mobility view): who climbed, who fell, who kept their fifth
            if (mobility != null && mobility.Lines > 0)
            {
                double lines = mobility.Lines;
                b.Append('\n').Append("the ladder ").Append(mobility.From.ToString(Ci)).Append("-").Append(mobility.Year.ToString(Ci)).Append("  ")
                    .Append(gold).Append(LandFacts.Percent(mobility.Up / lines)).Append(end).Append(" climbed a fifth or more").Append(dot)
                    .Append(rose).Append(LandFacts.Percent(mobility.Down / lines)).Append(end).Append(" fell").Append(dot)
                    .Append("bottom fifth stays ").Append(v).Append(LandFacts.Percent(mobility.PanelShare(0, 0))).Append(end)
                    .Append(", top ").Append(v).Append(LandFacts.Percent(mobility.PanelShare(Mobility.Quintiles - 1, Mobility.Quintiles - 1))).Append(end);
            }

            // the turning point, when the year is one
            foreach (ReplayFacts.TurningPoint t in y.Turns)
            {
                string color = t.Kind == ReplayFacts.Kind.Recession ? rose : t.Kind == ReplayFacts.Kind.Tariff ? steel : gold;
                b.Append('\n').Append(color).Append("<b>TURNING POINT</b>  ").Append(t.Text).Append(end);
            }

            return b.ToString();
        }

        /// <summary>A share's change as a signed percentage, rose when it is a fall.</summary>
        static string Change(double share, string v, string rose) =>
            (share < 0 ? rose : v) + ReplayFacts.Signed(100 * share, 1) + "%</color>";

        /// <summary>" (+3.0)" points of change, or nothing below a tenth of a point.</summary>
        static string Points(double change) =>
            double.IsNaN(change) || Math.Abs(change) < .05 ? "" : " (" + ReplayFacts.Signed(change, 1) + ")";

        /// <summary>Up to three names, then "+n more".</summary>
        static string Names(List<string> names)
        {
            const int max = 3;
            if (names.Count <= max) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, max)) + " +" + (names.Count - max).ToString(Ci) + " more";
        }

        /// <summary>
        /// Sizes the readout to its text and stands it at the bottom centre above the preset bar, clear of the HUD's legend
        /// and lifeline readout and the explorer's column on the left; on a portrait screen under the title and the year
        /// controls, across the screen.
        /// </summary>
        void Layout(HudFrame f)
        {
            float pad = HudKit.Pad;
            float maxWidth = f.Portrait ? f.Canvas.x - 2 * f.Margin : Mathf.Min(MaxWidth, f.Canvas.x - 2 * f.Margin);
            Vector2 year = HudKit.FitText(yearText);
            Vector2 body = HudKit.FitText(bodyText, maxWidth - 2 * pad);
            float w = Mathf.Min(maxWidth, Mathf.Max(year.x, body.x) + 2 * pad), h = year.y + body.y + 2 * pad + 2;
            HudKit.PlaceTopLeft(yearText.rectTransform, pad, pad - 2, year);
            HudKit.PlaceTopLeft(bodyText.rectTransform, pad, pad + year.y + 2, new Vector2(w - 2 * pad, body.y));

            UiBox box;
            if (f.Portrait)
            {
                float top = Mathf.Max(EconomyUiLayout.TitleBottom(f), EconomyControls.Occupied.Bottom);
                top = Mathf.Max(top, HillControls.Occupied.Bottom) + f.Gap;
                box = new UiBox(f.Margin, top, w, h);
            }
            else
            {
                float bottom = f.PresetBar.IsEmpty ? f.Canvas.y - f.Margin - f.SafeBottom : f.PresetBar.Y - f.Gap;
                float x = (f.Canvas.x - w) * .5f;
                // the left column's blocks (the HUD's legend and lifeline readout, the explorer) stand beside it, never under
                float left = Mathf.Max(LeftEdge(f.Legend, bottom - h, bottom), LeftEdge(f.Readout, bottom - h, bottom));
                left = Mathf.Max(left, HillExplorer.Occupied.IsEmpty || HillExplorer.Occupied.Bottom <= bottom - h ? 0 : HillExplorer.Occupied.Right);
                if (left > 0) x = Mathf.Max(x, left + f.Gap);
                w = Mathf.Min(w, f.Canvas.x - f.Margin - x);
                box = new UiBox(x, bottom - h, w, h);
            }

            panel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(box.X, -box.Y), new Vector2(box.W, box.H));
            Occupied = box;
        }

        /// <summary>The right edge of a block that shares rows with y0..y1 (0 when it does not, or is not shown).</summary>
        static float LeftEdge(UiBox b, float y0, float y1) => b.IsEmpty || b.Bottom <= y0 || b.Y >= y1 ? 0 : b.Right;
    }
}
