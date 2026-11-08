using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Director;
using Why.Economy.UI;

namespace Why.UI
{
    /// <summary>
    /// The narration's picture cards (the user, 7 Nov 2026: "we for sure need picture reference popups to go along with the
    /// narrations"; Docs/research/pictures/README.md): while the universe's guided tour plays a stop whose script names pictures
    /// (<see cref="TourStep.Pictures"/>), up to <see cref="MaxCards"/> cards show what the narration names - a galaxy, a fossil, a
    /// person, a map. Each card is the image at 16:9 (cropped to fill it, or letterboxed whole for <c>"fit": "contain"</c>), its
    /// caption under it, and its credit line, small and dim, under that; an AI-generated picture's credit is shown in full,
    /// exactly as the index gives it, never cut short. The pictures are Resources/Data/pictures/&lt;id&gt;.jpg or .png, described
    /// in Data/pictures/index.json: <c>{ "pictures": { "&lt;id&gt;": { "caption", "credit", "generated", "fit" } } }</c>.
    /// <para>
    /// Cards fade in as the stop starts (a moment after it, as the narration card comes in) and out as it ends; a picture the
    /// next stop shows too stays on screen and glides to its new place. In landscape they stand in a column at the left edge,
    /// below the place of the view's title (which steps aside for the tour, and comes back there) and above the HUD's legend
    /// block (the key and the readouts over it, <see cref="PanelPlacement.LegendTop"/>); the narration card's left slots stand
    /// right of the column meanwhile (<see cref="PanelPlacement.LeftInset"/>). In portrait they stand in a row under the title's
    /// place, or under the narration sheet when it docks at the top; while the narration card moves between slots they keep
    /// clear of where it stood, so a picture two stops share does not move up into its place and back. They keep clear of the
    /// point the narration's arrow points at (with room for its caption): when the room is short they get smaller, and then
    /// fewer of them show; when not even the stop's first picture keeps clear of it, that picture alone stands where it covers
    /// the least of it (one warning for the stop). A stop that shows fewer of its pictures than it names (up to
    /// <see cref="MaxCards"/>) is warned about once, when its narration card has landed.
    /// </para>
    /// <para>
    /// A picture the index does not list, or whose image is missing, is skipped with one warning, and a caption or credit with
    /// characters the cards' font and its fallbacks lack (TMP draws each as a box) is warned about once as the index loads.
    /// Cards animate on <see cref="GraphTime"/> (so a recording shows what a viewer sees), the graph's labels keep out from
    /// under them, and an image is unloaded as soon as its card has faded out. The smoke test captures each stop with its cards
    /// settled (<see cref="Settle"/>) and checks them there (the "tour-pictures" check, which also reports a stop that shows
    /// fewer pictures than it names, as a warning). Not in a headset: the canvas is not one the headset keeps.
    /// </para>
    /// </summary>
    [GraphScenes(GraphScene.Why)]
    public sealed class TourPictures : GraphModule
    {
        /// <summary>Resources folder of the pictures and their index (index.json beside the images).</summary>
        public const string Folder = "Data/pictures/";

        /// <summary>The most cards a stop shows; further pictures it names are left out.</summary>
        public const int MaxCards = 3;

        /// <summary>The smoke check's name (<see cref="SmokeChecks"/>).</summary>
        public const string CheckName = "tour-pictures";

        /// <summary>Above the HUD (40), under the narration card and its arrow (the director's 50).</summary>
        const int SortingOrder = 45;

        /// <summary>A card's inner padding, the gap under its picture and over its credit line, and the gap between cards.</summary>
        const float Pad = 9f, ImageGap = 7f, CreditGap = 3f, CardGap = 10f;

        /// <summary>The picture's frame: 16:9, whatever the image's own shape.</summary>
        const float FrameAspect = 16f / 9f;

        /// <summary>
        /// Landscape: the column's width, and the narrowest it gets before a card is left out (canvas units of the 1080p canvas:
        /// a 302 x 170 picture at 1080p).
        /// </summary>
        const float ColumnWidth = 320f, ColumnMinWidth = 200f;

        /// <summary>Portrait: a card's widest (one or two in the row) and narrowest (three, on the 500-unit canvas).</summary>
        const float RowCardWidth = 250f, RowCardMinWidth = 120f;

        /// <summary>Cards shrink in steps of this much while they do not fit.</summary>
        const float WidthStep = 10f;

        /// <summary>
        /// Landscape: between the column and the narration card in a left slot (<see cref="PanelPlacement.LeftInset"/>): the card
        /// slides in 28 units from its left, so it never slides over a picture.
        /// </summary>
        const float ColumnGap = 30f;

        /// <summary>Below the title's place, and above the legend block (as the narration card stands above it).</summary>
        const float TitleGap = HudKit.Gap, LegendGap = 14f;

        /// <summary>The caption's and credit's type (the portrait canvas draws them 2.16 times larger on a phone frame).</summary>
        const float CaptionSize = 13f, CreditSize = 10f, PortraitCaptionSize = 11.5f, PortraitCreditSize = 9f;

        /// <summary>The most lines of a caption (an ellipsis ends a longer one); the credit is never cut.</summary>
        const int CaptionLines = 4, PortraitCaptionLines = 3;

        /// <summary>
        /// The point the narration's arrow points at, kept clear by the cards: a band this far above and below it (its ring, 15
        /// units and breathing, and the caption plate beside it), wherever the cards come within <see cref="TargetReach"/> of it
        /// across (the caption stands beside the ring, on the side away from the arrow). When not even one card keeps clear of
        /// it, the stop's first picture alone stands where it covers the least of it.
        /// </summary>
        const float TargetBand = 32f, TargetReach = 340f;

        /// <summary>
        /// Fades: in and out (s), the wait of a card that arrives with a new stop (the narration card slides out for 0.2 s
        /// first, and so do the leaving stop's cards), the slide from the left while it fades, and how quickly a shown card
        /// glides to a new place (per second, exponential).
        /// </summary>
        const float InSeconds = 0.45f, OutSeconds = 0.25f, InDelay = 0.2f, SlideDistance = 30f, MoveRate = 9f;

        /// <summary>Height handed to TMP when measuring wrapped text.</summary>
        const float Unbounded = 32767f;

        /// <summary>
        /// How many frames after <see cref="Settle"/> the cards stay settled: the smoke test captures the frame after it and
        /// checks the cards two frames after that, and a layout the HUD or the narration card changes meanwhile must not glide
        /// or fade into the frame or the check.
        /// </summary>
        const int SettleFrames = 4;

        /// <summary>The card's fill (the economy's history popups', <see cref="HistoryPopups"/>), and the picture's frame, which
        /// shows as bars around a letterboxed image.</summary>
        static readonly Color CardColor = new Color(.02f, .03f, .05f, .9f), FrameColor = new Color(0f, 0f, 0f, .6f);

        /// <summary>The credit line: dim (the history popups' credit), and brighter for a generated picture, whose credit says so.</summary>
        static readonly Color CreditColor = new Color(.55f, .58f, .64f), GeneratedCreditColor = new Color(.74f, .76f, .81f);

        /// <summary>A generated picture whose index entry gives no credit is still marked as generated, in these words.</summary>
        const string GeneratedFallbackCredit = "AI-generated image";

        /// <summary>What has been warned about this run (one warning each).</summary>
        static readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        static TourPictures current;

        /// <summary>A picture of the index.</summary>
        sealed class Entry
        {
            public string Id, Caption, Credit;
            public bool Generated, Contain;

            /// <summary>The credit line as the card shows it: the index's own, or for a generated picture without one, that it is.</summary>
            public string CreditShown => Generated && string.IsNullOrWhiteSpace(Credit) ? GeneratedFallbackCredit : Credit;
        }

        /// <summary>One card: its picture and texts, where it is and where it is going, and how far it has faded in.</summary>
        sealed class Card
        {
            public RectTransform Rect, Frame;
            public CanvasGroup Group;
            public RawImage Picture;
            public TextMeshProUGUI Caption, Credit;

            /// <summary>The picture shown (null: the card is free) and its image, loaded for it.</summary>
            public Entry Entry;

            public Texture2D Texture;
            public float Alpha, Target, Delay;

            /// <summary>Top-left corner (canvas units down from the screen's top-left corner) and width: now, and where it goes.</summary>
            public Vector2 Pos, GoalPos;

            public float Width, GoalWidth;

            /// <summary>The caption's and the credit's heights at the goal width.</summary>
            public float CaptionH, CreditH;

            /// <summary>Given a place by the layout being applied (the others fade out).</summary>
            public bool Keep;

            // what was applied last: nothing is touched while it holds
            public Vector2 ShownPos = new Vector2(float.NaN, float.NaN);
            public float ShownWidth = float.NaN, ShownAlpha = -1;

            public string Id => Entry?.Id;
        }

        /// <summary>
        /// A layout: how many of the stop's pictures show, at what width, and the top-left corner of their block; and whether it
        /// covers part of the band around the arrow's point (no card kept clear of it).
        /// </summary>
        struct Plan
        {
            public int Count;
            public float Width, X, Y;
            public bool Portrait, CoversTarget;
        }

        GraphRoot root;
        DirectorMode director;
        Hud hud;
        Canvas canvas;
        RectTransform canvasRect;
        TextMeshProUGUI measure;
        readonly List<Card> cards = new List<Card>();
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>The index (loaded once the graph has), and the pictures whose image could not be loaded.</summary>
        Dictionary<string, Entry> index;

        readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The stop shown and its pictures that are in the index (in its order).</summary>
        TourStep shownStep;

        readonly List<Entry> entries = new List<Entry>();
        Plan plan;

        // what the last layout was made for; a change lays the cards out again
        Vector2 laidCanvas, laidTarget;
        Rect laidCard;
        bool laidPortrait, laidHasTarget, laidHasCard, dirty, snap;
        float laidTitle, laidLegend;

        /// <summary>The highest the legend block has reached during the stop on this canvas (it only rises within a stop: the
        /// readouts over the key come and go as the camera flies, and the cards would grow and shrink with them; a new canvas
        /// size or orientation starts it again, in the new canvas's units).</summary>
        float stopLegendTop;

        /// <summary>The frame the canvas last changed size or orientation.</summary>
        int canvasFrame = -1;

        /// <summary>The stop <see cref="Settle"/> settled the cards for, and the last frame they stay settled.</summary>
        TourStep settleStep;

        int settleUntil = -1;

        /// <summary>This visit of the stop has warned that it shows fewer pictures than it names, or that they cover part of
        /// the arrow's point (each once per stop for the run, <see cref="WarnOnce"/>).</summary>
        bool roomWarned, targetWarned;

        /// <summary>Measured text heights by text, width and size (quarter units).</summary>
        readonly Dictionary<(string, int, int), float> heights = new Dictionary<(string, int, int), float>();

        // the stretches a block is kept out of, and the stretches left clear of them (canvas units down from the top)
        readonly List<(float start, float end)> blocked = new List<(float, float)>();
        readonly List<(float start, float end)> gaps = new List<(float, float)>();

        public override int Order => 52;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            current = this;
            canvas = UiFactory.CreateCanvas("TourPictures", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            // measures the cards' texts; it shows nothing (its own text stays empty)
            measure = UiFactory.Text(canvasRect, "Measure", "", CaptionSize, Color.clear);
            measure.richText = false;
            measure.raycastTarget = false;
            SmokeChecks.Register(CheckName, _ => Problems());
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            hud = root.Modules.OfType<Hud>().FirstOrDefault();
            LoadIndex();
        }

        void OnDestroy()
        {
            foreach (Card c in cards) Release(c);
            if (current != this) return;
            current = null;
            PanelPlacement.LeftInset = 0;
        }

        /// <summary>
        /// The smoke test, before it captures a stop: every card at once where it is going, faded fully in or out (a card that
        /// has faded out lets go of its image), so the frame shows the stop's cards settled. They stay so for
        /// <see cref="SettleFrames"/> frames while the stop is on screen: the capture is a frame later and the check two frames
        /// after that, and a layout that changes meanwhile (the HUD's legend block, the narration card's slot) is settled too.
        /// </summary>
        public static void Settle()
        {
            if (current == null || current.root == null || !current.root.IsLoaded) return;
            TourPictures p = current;
            p.Tick(0);
            p.settleStep = p.shownStep;
            p.settleUntil = Time.frameCount + SettleFrames;
            p.SettleCards();
        }

        /// <summary>Every card at once where it is going, faded fully in or out; a card that has faded out lets go of its image.</summary>
        void SettleCards()
        {
            foreach (Card c in cards)
            {
                if (c.Entry == null) continue;
                c.Delay = 0;
                c.Alpha = c.Target;
                if (c.Target > 0)
                {
                    c.Pos = c.GoalPos;
                    c.Width = c.GoalWidth;
                }

                if (c.Alpha <= 0) Release(c);
                else Apply(c);
            }
        }

        // ------------------------------------------------------------------ the index

        void LoadIndex()
        {
            index = new Dictionary<string, Entry>(StringComparer.Ordinal);
            string path = Folder + "index";
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                WarnOnce("index", $"[Why] tour pictures: no index at Resources/{path}.json; the tour shows no pictures");
                return;
            }

            string json = asset.text;
            Resources.UnloadAsset(asset);
            JObject parsed;
            try
            {
                parsed = JObject.Parse(json.TrimStart('\uFEFF'));
            }
            catch (Exception e)
            {
                WarnOnce("index", $"[Why] tour pictures: could not parse Resources/{path}.json ({e.Message}); the tour shows no pictures");
                return;
            }

            if (!(parsed["pictures"] is JObject pictures))
            {
                WarnOnce("index", $"[Why] tour pictures: Resources/{path}.json has no \"pictures\" object; the tour shows no pictures");
                return;
            }

            // the cards' font: the default TMP font, which the measuring text was given as the cards are
            TMP_FontAsset font = measure != null && measure.font != null ? measure.font : TMP_Settings.defaultFontAsset;
            foreach (KeyValuePair<string, JToken> kv in pictures)
            {
                string id = kv.Key.Trim();
                if (!(kv.Value is JObject p) || id.Length == 0)
                {
                    WarnOnce("entry:" + id, $"[Why] tour pictures: index entry '{id}' is not a picture (an object with a caption and a credit); skipped");
                    continue;
                }

                string fit = Read(p["fit"]).ToLowerInvariant();
                if (fit.Length > 0 && fit != "cover" && fit != "contain")
                {
                    WarnOnce("fit:" + id, $"[Why] tour pictures: picture '{id}' has fit '{fit}' (cover or contain); it is cropped to fill its frame");
                }

                Entry entry = new Entry
                {
                    Id = id, Caption = Read(p["caption"]), Credit = Read(p["credit"]), Generated = Flag(p["generated"]), Contain = fit == "contain"
                };
                if (string.IsNullOrWhiteSpace(entry.Credit))
                {
                    WarnOnce("credit:" + id, $"[Why] tour pictures: picture '{id}' has no credit" +
                                             (entry.Generated ? $"; its card says \"{GeneratedFallbackCredit}\"" : ""));
                }

                index[id] = entry;
                CheckGlyphs(font, entry);
            }

            Debug.Log($"[Why] tour pictures: {index.Count} pictures in the index");
        }

        /// <summary>
        /// One warning for a picture whose caption or credit (as the card shows it) has characters that the cards' font and its
        /// fallbacks lack: TMP draws each as a box. A dynamic fallback is asked to add a character from its font file first, as
        /// drawing the text would, so only what no font can draw is named.
        /// </summary>
        static void CheckGlyphs(TMP_FontAsset font, Entry e)
        {
            if (font == null) return;
            string caption = MissingGlyphs(font, e.Caption), credit = MissingGlyphs(font, e.CreditShown);
            if (caption == null && credit == null) return;
            string where = caption != null && credit != null ? $"in its caption {caption}; in its credit {credit}"
                : caption != null ? "in its caption " + caption : "in its credit " + credit;
            WarnOnce("glyphs:" + e.Id, $"[Why] tour pictures: picture '{e.Id}' has characters that the cards' font ({font.name}) and " +
                                       $"its fallbacks lack, which show as boxes: {where}");
        }

        /// <summary>The characters of a text that a font and its fallbacks lack, each once (<c>'ŋ' U+014B</c>), or null when
        /// it has them all (or cannot tell).</summary>
        static string MissingGlyphs(TMP_FontAsset font, string text)
        {
            if (string.IsNullOrEmpty(text) || font.HasCharacters(text, out uint[] codes, true, true) || codes == null || codes.Length == 0)
                return null;
            return string.Join(", ", codes.Distinct().Select(code =>
            {
                // a lone surrogate or a control character is named by its code alone
                bool drawable = code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF);
                string s = drawable ? char.ConvertFromUtf32((int)code) : null;
                return s != null && !char.IsControl(s, 0) && !char.IsWhiteSpace(s, 0) ? $"'{s}' U+{code:X4}" : $"U+{code:X4}";
            }));
        }

        static string Read(JToken t) => t == null || t.Type == JTokenType.Null ? "" : (t.Type == JTokenType.String ? (string)t ?? "" : t.ToString()).Trim();

        static bool Flag(JToken t) =>
            t != null && (t.Type == JTokenType.Boolean ? (bool)t
                : t.Type == JTokenType.String && string.Equals(((string)t ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase));

        static void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning(message);
        }

        /// <summary>The stop's pictures that are in the index and whose image has not failed, in its order, each once.</summary>
        void BuildEntries(TourStep step)
        {
            entries.Clear();
            heights.Clear();
            if (step == null || step.Pictures == null || index == null) return;
            foreach (string id in step.Pictures)
            {
                if (string.IsNullOrEmpty(id) || missing.Contains(id) || entries.Exists(e => e.Id == id)) continue;
                if (!index.TryGetValue(id, out Entry entry))
                {
                    WarnOnce("id:" + id, $"[Why] tour pictures: stop '{step.Id}' names picture '{id}', which Resources/{Folder}index.json " +
                                         "does not list; skipped");
                    continue;
                }

                entries.Add(entry);
            }

            if (entries.Count > MaxCards)
            {
                WarnOnce("many:" + step.Id, $"[Why] tour pictures: stop '{step.Id}' names {entries.Count} pictures; the first {MaxCards} show");
            }
        }

        // ------------------------------------------------------------------ per frame

        void LateUpdate()
        {
            // after the director has moved on (Update) and the HUD has laid its legend out, so this frame already shows the stop
            if (root == null || !root.IsLoaded) return;
            Tick(GraphTime.Delta);
        }

        void Tick(float dt)
        {
            // a headset does not keep this canvas (Xr.XrSession), so it shows no pictures and moves no card for them
            TourStep step = director != null && !Xr.XrSession.Active ? director.CurrentTourStep : null;
            bool stopChanged = !ReferenceEquals(step, shownStep);
            if (stopChanged)
            {
                shownStep = step;
                BuildEntries(step);
                roomWarned = targetWarned = false;
                dirty = true;
            }

            // what the cards keep clear of this frame; anything that moved lays them out again
            Vector2 size = UiFactory.CanvasSize;
            bool portrait = ScreenLayout.IsPortrait;
            bool canvasChanged = size != laidCanvas || portrait != laidPortrait;
            if (canvasChanged)
            {
                canvasFrame = Time.frameCount;
                dirty = true;
                snap = true; // other canvas units: every card goes straight to its new place
            }

            float title = hud != null ? hud.TitleBottom : (portrait ? HudKit.PortraitMargin : HudKit.Margin) + 100f;
            float legend = PanelPlacement.LegendTop > 0 ? PanelPlacement.LegendTop
                : portrait ? PanelPlacement.PortraitBottomMargin : PanelPlacement.LandscapeBottomMargin;
            // the stop's highest only within one canvas: a new stop, size or orientation starts from where the legend block
            // stands now, on the frame of the change and the next one too (the HUD may lay its legend out for the new screen
            // after this module in that frame: their LateUpdates have no set order)
            stopLegendTop = stopChanged || Time.frameCount - canvasFrame <= 1 ? legend : Mathf.Max(stopLegendTop, legend);
            Vector2 target = Vector2.zero;
            Rect card = default;
            bool hasTarget = step != null && director.TryGetTargetPoint(out target);
            bool landed = step != null && director.TryGetCardRect(out card);
            bool hasCard = landed;
            if (step != null && !landed && laidHasCard && !canvasChanged)
            {
                // the narration card is between slots (sliding out to come back elsewhere, or for the next stop's card): the
                // cards keep clear of where it stood until it lands, so a picture that stays on screen stays put instead of
                // moving into the room the card leaves and back (the portrait sheet docked at the top, before and after)
                hasCard = true;
                card = laidCard;
            }

            if (Mathf.Abs(title - laidTitle) > 0.5f || Mathf.Abs(stopLegendTop - laidLegend) > 0.5f || hasTarget != laidHasTarget ||
                (hasTarget && (target - laidTarget).sqrMagnitude > 0.25f) || hasCard != laidHasCard || (hasCard && !Same(card, laidCard)))
            {
                dirty = true;
            }

            if (dirty)
            {
                dirty = false;
                laidCanvas = size;
                laidPortrait = portrait;
                laidTitle = title;
                laidLegend = stopLegendTop;
                laidHasTarget = hasTarget;
                laidTarget = target;
                laidHasCard = hasCard;
                laidCard = card;
                // a picture whose image fails to load is dropped and the rest laid out again without it (each time one fewer, so
                // this ends: with no pictures left every card just fades out)
                plan = Layout(size, portrait, title, stopLegendTop, hasTarget, target, hasCard, card);
                while (!Assign(stopChanged)) plan = Layout(size, portrait, title, stopLegendTop, hasTarget, target, hasCard, card);
            }

            // while the smoke test captures and checks this stop (Settle), every card stays where it is going, faded fully in or out
            if (Time.frameCount <= settleUntil && ReferenceEquals(step, settleStep)) SettleCards();
            else Animate(dt, snap);
            snap = false;

            // the narration card's left slots stand right of the column while it is wanted (the card is laid out a moment after
            // the stop starts, so this is in place when it lands)
            PanelPlacement.LeftInset = plan.Count > 0 && !plan.Portrait ? plan.X + plan.Width + ColumnGap : 0;

            // what the stop could not show is told once its narration card has landed (until then the cards keep clear of where
            // the card that left stood, and may be short of room for a moment)
            if (landed) WarnShort(step);
        }

        static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) < 0.5f && Mathf.Abs(a.yMin - b.yMin) < 0.5f && Mathf.Abs(a.width - b.width) < 0.5f &&
            Mathf.Abs(a.height - b.height) < 0.5f;

        /// <summary>
        /// The stop's warnings, each once per stop for the run: when no card could keep clear of the point the narration's arrow
        /// points at (<see cref="Plan.CoversTarget"/>), and when the stop shows fewer of its pictures than it names (up to
        /// <see cref="MaxCards"/>), each naming the room the cards had.
        /// </summary>
        void WarnShort(TourStep step)
        {
            if (plan.CoversTarget && !targetWarned)
            {
                targetWarned = true;
                Vector2 point = Down(laidTarget, laidCanvas.y);
                WarnOnce("target:" + step.Id, $"[Why] tour pictures: stop '{step.Id}': no card keeps clear of the point the narration " +
                                              $"points at ({point.x:0},{point.y:0} from the top left), so its first picture alone stands " +
                                              $"where it covers the least of it; {Room()}");
            }

            int wanted = Mathf.Min(entries.Count, MaxCards);
            if (plan.Count < wanted && !roomWarned)
            {
                roomWarned = true;
                WarnOnce("room:" + step.Id, $"[Why] tour pictures: stop '{step.Id}' shows {plan.Count} of its {wanted} pictures; {Room()}");
            }
        }

        /// <summary>
        /// The room the last layout had, for the warnings: the stretch between the title's place and the legend block, the
        /// tallest part of it that the stop's cards (as many as it names, up to <see cref="MaxCards"/>, at their narrowest) find
        /// clear of the narration card and the arrow's point, and the height they need.
        /// </summary>
        string Room()
        {
            int wanted = Mathf.Min(entries.Count, MaxCards);
            if (wanted == 0) return "it has no pictures to show";
            float x = laidPortrait ? HudKit.PortraitMargin : HudKit.Margin;
            float top = laidTitle + TitleGap, bottom = laidCanvas.y - laidLegend - LegendGap;
            float narrowest = laidPortrait ? RowCardMinWidth : ColumnMinWidth;
            float across = laidPortrait ? wanted * narrowest + (wanted - 1) * CardGap : narrowest;
            Gaps(x, across, top, bottom, laidHasCard, Down(laidCard, laidCanvas.y), laidHasTarget, Down(laidTarget, laidCanvas.y), gaps);
            float clear = 0;
            foreach ((float start, float end) in gaps) clear = Mathf.Max(clear, end - start);
            string row = laidPortrait ? $", in a row {laidCanvas.x - 2 * x:0} wide (they need {across:0})" : "";
            return $"the room between the title's place and the legend block is {Mathf.Max(0, bottom - top):0} units tall, {clear:0} of " +
                   $"them in one stretch clear of the narration card{(laidHasTarget ? " and the arrow's point" : "")}, and {wanted} cards " +
                   $"need {BlockHeight(wanted, narrowest, laidPortrait):0} at their narrowest{row}";
        }

        /// <summary>The director's rectangle or point (canvas units, origin bottom-left) in the cards' terms: canvas units down from
        /// the top-left corner of a canvas this tall.</summary>
        static Rect Down(Rect r, float height) => Rect.MinMaxRect(r.xMin, height - r.yMax, r.xMax, height - r.yMin);

        static Vector2 Down(Vector2 p, float height) => new Vector2(p.x, height - p.y);

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Where the stop's cards go: as many of its pictures as fit (at most <see cref="MaxCards"/>), as wide as they can be,
        /// in the topmost place that holds them between the title's place and the legend block, clear of the narration card
        /// and of the band around the point the arrow points at. More pictures at a smaller width come before fewer at a larger
        /// one, down to the stop's first picture alone. When not even that keeps clear of the band, the first picture alone
        /// stands, clear of the narration card, where it covers the least of the band (<see cref="Plan.CoversTarget"/>).
        /// Landscape: a column at the left edge. Portrait: a row from the left edge.
        /// </summary>
        Plan Layout(Vector2 size, bool portrait, float title, float legendTop, bool hasTarget, Vector2 target, bool hasCard, Rect card)
        {
            int most = Mathf.Min(entries.Count, MaxCards);
            if (most == 0) return default;

            float x = portrait ? HudKit.PortraitMargin : HudKit.Margin;
            float top = title + TitleGap;
            float bottom = size.y - legendTop - LegendGap;
            float row = size.x - 2 * x;
            float narrowest = portrait ? RowCardMinWidth : ColumnMinWidth;

            // obstacles, in canvas units down from the top
            Rect cardBox = hasCard ? Down(card, size.y) : default;
            Vector2 point = Down(target, size.y);

            for (int n = most; n >= 1; n--)
            {
                float widest = portrait ? Mathf.Min(RowCardWidth, (row - (n - 1) * CardGap) / n) : ColumnWidth;
                for (float w = Mathf.Floor(widest); w >= narrowest; w -= WidthStep)
                {
                    float blockWidth = portrait ? n * w + (n - 1) * CardGap : w;
                    float blockHeight = BlockHeight(n, w, portrait);
                    if (!Fits(x, blockWidth, blockHeight, top, bottom, hasCard, cardBox, hasTarget, point, out float y)) continue;
                    return new Plan { Count = n, Width = w, X = x, Y = y, Portrait = portrait };
                }
            }

            if (!hasTarget) return default;

            // not even the first picture keeps clear of the arrow's point: it alone (more cards would only cover more), at the
            // width and in the place clear of the narration card where it covers the least of the band. Along a clear stretch
            // that is at one of its ends (as a card moves down through the band, what it covers only grows, then only shrinks);
            // a tie keeps the wider card, then the higher place.
            Plan best = default;
            float least = float.MaxValue;
            for (float w = Mathf.Floor(portrait ? Mathf.Min(RowCardWidth, row) : ColumnWidth); w >= narrowest; w -= WidthStep)
            {
                float height = BlockHeight(1, w, portrait);
                Gaps(x, w, top, bottom, hasCard, cardBox, false, point, gaps);
                foreach ((float start, float end) in gaps)
                {
                    if (end - start < height) continue;
                    for (int side = 0; side < 2; side++)
                    {
                        float y = side == 0 ? start : end - height;
                        float cover = Cover(x, w, y, height, point);
                        if (cover >= least - 0.5f) continue;
                        least = cover;
                        best = new Plan { Count = 1, Width = w, X = x, Y = y, Portrait = portrait, CoversTarget = true };
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The topmost place between <paramref name="top"/> and <paramref name="bottom"/> (canvas units down from the top) where a
        /// block this wide, at <paramref name="x"/>, and this tall stands clear of the narration card and (when asked) of the
        /// band around the arrow's point.
        /// </summary>
        bool Fits(float x, float width, float height, float top, float bottom, bool hasCard, Rect card, bool avoidTarget, Vector2 point,
            out float y)
        {
            Gaps(x, width, top, bottom, hasCard, card, avoidTarget, point, gaps);
            foreach ((float start, float end) in gaps)
            {
                if (end - start < height) continue;
                y = start;
                return true;
            }

            y = top;
            return false;
        }

        /// <summary>
        /// The stretches between <paramref name="top"/> and <paramref name="bottom"/> (canvas units down from the top), top first,
        /// where a block this wide at <paramref name="x"/> stands clear of the narration card and (when asked) of the band around
        /// the arrow's point, which it keeps out of wherever it comes within <see cref="TargetReach"/> of the point across.
        /// </summary>
        void Gaps(float x, float width, float top, float bottom, bool hasCard, Rect card, bool avoidTarget, Vector2 point,
            List<(float start, float end)> clear)
        {
            blocked.Clear();
            if (hasCard && card.xMin < x + width + CardGap && card.xMax > x - CardGap) blocked.Add((card.yMin - CardGap, card.yMax + CardGap));
            if (avoidTarget && point.x > x - TargetReach && point.x < x + width + TargetReach)
            {
                blocked.Add((point.y - TargetBand, point.y + TargetBand));
            }

            blocked.Sort((a, b) => a.start.CompareTo(b.start));
            clear.Clear();
            float y = top;
            foreach ((float start, float end) in blocked)
            {
                if (end <= y) continue;
                if (Mathf.Min(start, bottom) > y) clear.Add((y, Mathf.Min(start, bottom)));
                y = Mathf.Max(y, end);
            }

            if (bottom > y) clear.Add((y, bottom));
        }

        /// <summary>
        /// How much of the band around the arrow's point a block covers, in square canvas units (the block and the point in
        /// canvas units down from the top): 0 when it keeps clear of it as <see cref="Gaps"/> asks, above or below the band or
        /// beyond <see cref="TargetReach"/> across.
        /// </summary>
        static float Cover(float x, float width, float y, float height, Vector2 point)
        {
            float across = Mathf.Min(x + width, point.x + TargetReach) - Mathf.Max(x, point.x - TargetReach);
            float down = Mathf.Min(y + height, point.y + TargetBand) - Mathf.Max(y, point.y - TargetBand);
            return across > 0 && down > 0 ? across * down : 0;
        }

        /// <summary>The height of the first <paramref name="n"/> pictures' block: stacked (landscape) or side by side (portrait).</summary>
        float BlockHeight(int n, float width, bool portrait)
        {
            float h = 0;
            for (int i = 0; i < n; i++)
            {
                float card = CardHeight(entries[i], width, portrait, out _, out _);
                h = portrait ? Mathf.Max(h, card) : h + card + (i > 0 ? CardGap : 0);
            }

            return h;
        }

        /// <summary>A card's height at a width: the picture at 16:9, the caption (at most its lines), the whole credit line.</summary>
        float CardHeight(Entry e, float width, bool portrait, out float captionH, out float creditH)
        {
            float inner = width - 2 * Pad;
            float captionSize = portrait ? PortraitCaptionSize : CaptionSize, creditSize = portrait ? PortraitCreditSize : CreditSize;
            captionH = TextHeight(e.Caption, inner, captionSize);
            // a caption past its lines ends in an ellipsis (TMP cuts a last line that only just fits: one unit more)
            if (captionH > 0) captionH = Mathf.Min(captionH, LinesHeight(captionSize, portrait ? PortraitCaptionLines : CaptionLines)) + 1;
            creditH = TextHeight(e.CreditShown, inner, creditSize);
            if (creditH > 0) creditH += 1;
            float h = Pad + ImageHeight(width);
            if (captionH > 0) h += ImageGap + captionH;
            if (creditH > 0) h += (captionH > 0 ? CreditGap : ImageGap) + creditH;
            return Mathf.Ceil(h + Pad);
        }

        static float ImageHeight(float width) => Mathf.Round((width - 2 * Pad) / FrameAspect);

        /// <summary>A text's height wrapped at a width, in the cards' type at a size (cached; text that is not there takes none).</summary>
        float TextHeight(string text, float width, float size)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            (string, int, int) key = (text, Mathf.RoundToInt(width * 4), Mathf.RoundToInt(size * 4));
            if (heights.TryGetValue(key, out float h)) return h;
            if (heights.Count > 4000) heights.Clear();
            measure.fontSize = size;
            h = Mathf.Ceil(measure.GetPreferredValues(text, width, Unbounded).y);
            heights[key] = h;
            return h;
        }

        /// <summary>The height of this many lines at a size.</summary>
        float LinesHeight(float size, int lines)
        {
            float one = TextHeight("Ag", 1000f, size), two = TextHeight("Ag\nAg", 1000f, size);
            return Mathf.Ceil(one + (lines - 1) * (two - one));
        }

        /// <summary>
        /// Gives the plan's pictures their cards: one already showing that picture keeps it (it fades back in if it was leaving,
        /// and glides to its new place), any other gets a free card and its image, and waits for the leaving cards when the stop
        /// has just changed; every other card fades out. False when an image could not be loaded: that picture is dropped (one
        /// warning) and the stop is laid out again.
        /// </summary>
        bool Assign(bool stopChanged)
        {
            foreach (Card c in cards) c.Keep = false;
            float y = plan.Y;
            for (int i = 0; i < plan.Count; i++)
            {
                Entry e = entries[i];
                Card c = cards.Find(k => k.Entry != null && k.Id == e.Id);
                if (c == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(Folder + e.Id);
                    if (texture == null)
                    {
                        WarnOnce("image:" + e.Id, $"[Why] tour pictures: picture '{e.Id}' has no image at Resources/{Folder}{e.Id}.jpg or .png; skipped");
                        missing.Add(e.Id);
                        entries.RemoveAt(i);
                        return false;
                    }

                    c = FreeCard();
                    c.Entry = e;
                    c.Texture = texture;
                    c.Picture.texture = texture;
                    c.Caption.text = e.Caption;
                    c.Credit.text = e.CreditShown;
                    c.Credit.color = e.Generated ? GeneratedCreditColor : CreditColor;
                    c.Alpha = 0;
                    c.Delay = stopChanged ? InDelay : 0;
                }

                c.Keep = true;
                c.Target = 1;
                c.Caption.fontSize = plan.Portrait ? PortraitCaptionSize : CaptionSize;
                c.Credit.fontSize = plan.Portrait ? PortraitCreditSize : CreditSize;
                float height = CardHeight(e, plan.Width, plan.Portrait, out c.CaptionH, out c.CreditH);
                c.GoalWidth = plan.Width;
                c.GoalPos = plan.Portrait ? new Vector2(plan.X + i * (plan.Width + CardGap), plan.Y) : new Vector2(plan.X, y);
                y += height + CardGap;
                c.ShownWidth = float.NaN; // its texts' heights may have changed: lay it out again
            }

            foreach (Card c in cards)
            {
                if (!c.Keep && c.Entry != null) c.Target = 0;
            }

            return true;
        }

        Card FreeCard()
        {
            foreach (Card c in cards)
            {
                if (c.Entry == null) return c;
            }

            Card made = MakeCard(cards.Count);
            cards.Add(made);
            return made;
        }

        // ------------------------------------------------------------------ cards

        /// <summary>A card (hidden): the panel, the picture's 16:9 frame with the image in it, the caption and the credit line.</summary>
        Card MakeCard(int i)
        {
            Image panel = UiFactory.Panel(canvasRect, "Picture " + i, CardColor, false);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            frame.transform.SetParent(rect, false);
            frame.color = FrameColor;
            frame.raycastTarget = false;
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0, 1);

            RawImage picture = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            picture.transform.SetParent(frameRect, false);
            picture.raycastTarget = false;
            RectTransform pictureRect = picture.rectTransform;
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(.5f, .5f);
            pictureRect.anchoredPosition = Vector2.zero;

            // captions and credits are data: shown as written, never read as rich-text tags
            TextMeshProUGUI caption = UiFactory.Text(rect, "Caption", "", CaptionSize, GraphStyle.Text);
            caption.richText = false;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI credit = UiFactory.Text(rect, "Credit", "", CreditSize, CreditColor);
            credit.richText = false;
            credit.overflowMode = TextOverflowModes.Overflow;

            // the graph's labels keep out from under a card while it shows (the universe's labels, and the economy's hills' labels
            // through the HUD's blocks)
            LabelSystem.KeepClear(rect);
            HudBlocks.Register(rect);
            panel.gameObject.SetActive(false);
            return new Card { Rect = rect, Frame = frameRect, Group = group, Picture = picture, Caption = caption, Credit = credit };
        }

        /// <summary>
        /// Fades each card toward its target (a card arriving with a new stop waits <see cref="InDelay"/> first) and moves it: a
        /// card not yet seen stands at its place at once, a shown one glides there, a leaving one stays where it is. A card
        /// that has faded out lets go of its image.
        /// </summary>
        void Animate(float dt, bool jump)
        {
            float k = 1f - Mathf.Exp(-MoveRate * dt);
            foreach (Card c in cards)
            {
                if (c.Entry == null) continue;
                if (c.Target > 0 && c.Delay > 0) c.Delay = Mathf.Max(0, c.Delay - dt);
                else c.Alpha = Mathf.MoveTowards(c.Alpha, c.Target, dt / (c.Target > c.Alpha ? InSeconds : OutSeconds));

                if (c.Alpha <= 0 && c.Target <= 0)
                {
                    Release(c);
                    continue;
                }

                if (c.Target > 0)
                {
                    if (jump || c.Alpha <= 0.02f || dt <= 0)
                    {
                        c.Pos = c.GoalPos;
                        c.Width = c.GoalWidth;
                    }
                    else
                    {
                        c.Pos = Vector2.Lerp(c.Pos, c.GoalPos, k);
                        c.Width = Mathf.Lerp(c.Width, c.GoalWidth, k);
                        if ((c.Pos - c.GoalPos).sqrMagnitude < 0.01f) c.Pos = c.GoalPos;
                        if (Mathf.Abs(c.Width - c.GoalWidth) < 0.1f) c.Width = c.GoalWidth;
                    }
                }

                Apply(c);
            }
        }

        /// <summary>Shows a card as it is now: its fade and slide, its place, and its picture and texts laid out at its width.</summary>
        static void Apply(Card c)
        {
            if (!c.Rect.gameObject.activeSelf) c.Rect.gameObject.SetActive(true);
            float e = Mathf.SmoothStep(0, 1, c.Alpha);
            if (e != c.ShownAlpha)
            {
                c.ShownAlpha = e;
                c.Group.alpha = e;
            }

            // it slides in from the left as it appears (and out to the left as it goes), as the history popups do
            Vector2 pos = new Vector2(Mathf.Round(c.Pos.x - SlideDistance * (1 - e)), Mathf.Round(c.Pos.y));
            if (pos != c.ShownPos)
            {
                c.ShownPos = pos;
                c.Rect.anchoredPosition = new Vector2(pos.x, -pos.y);
            }

            float width = Mathf.Round(c.Width);
            if (width == c.ShownWidth) return;
            c.ShownWidth = width;
            float inner = width - 2 * Pad, image = ImageHeight(width);
            Place(c.Frame, Pad, Pad, inner, image);
            FitPicture(c, inner, image);
            float y = Pad + image;
            bool hasCaption = c.CaptionH > 0;
            c.Caption.gameObject.SetActive(hasCaption);
            if (hasCaption)
            {
                y += ImageGap;
                Place(c.Caption.rectTransform, Pad, y, inner, c.CaptionH);
                y += c.CaptionH;
            }

            bool hasCredit = c.CreditH > 0;
            c.Credit.gameObject.SetActive(hasCredit);
            if (hasCredit)
            {
                y += hasCaption ? CreditGap : ImageGap;
                Place(c.Credit.rectTransform, Pad, y, inner, c.CreditH);
                y += c.CreditH;
            }

            c.Rect.sizeDelta = new Vector2(width, Mathf.Ceil(y + Pad));
        }

        /// <summary>
        /// The image in its 16:9 frame by the image's own shape (an imported texture keeps its size, Editor/PictureImport.cs):
        /// cropped about its centre to fill the frame ("cover"), or whole, centred, with the frame's dark bars around it
        /// ("contain").
        /// </summary>
        static void FitPicture(Card c, float width, float height)
        {
            Texture2D t = c.Texture;
            float aspect = t != null && t.width > 0 && t.height > 0 ? t.width / (float)t.height : FrameAspect;
            float frame = width / Mathf.Max(height, 1f);
            RectTransform r = c.Picture.rectTransform;
            if (c.Entry != null && c.Entry.Contain)
            {
                c.Picture.uvRect = new Rect(0, 0, 1, 1);
                r.sizeDelta = aspect > frame ? new Vector2(width, width / aspect) : new Vector2(height * aspect, height);
                return;
            }

            c.Picture.uvRect = aspect > frame
                ? new Rect((1 - frame / aspect) * .5f, 0, frame / aspect, 1)
                : new Rect(0, (1 - aspect / frame) * .5f, 1, aspect / frame);
            r.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Places a child by its top-left corner, measured down from its parent's top-left.</summary>
        static void Place(RectTransform rt, float x, float yFromTop, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Frees a card: its image unloaded (nothing else holds it: one card per picture), the card hidden.</summary>
        static void Release(Card c)
        {
            if (c.Picture != null) c.Picture.texture = null;
            if (c.Texture != null) Resources.UnloadAsset(c.Texture);
            c.Texture = null;
            c.Entry = null;
            c.Alpha = c.Target = c.Delay = 0;
            c.ShownAlpha = -1;
            c.ShownWidth = float.NaN;
            c.ShownPos = new Vector2(float.NaN, float.NaN);
            if (c.Rect != null && c.Rect.gameObject.activeSelf)
            {
                c.Group.alpha = 0;
                c.Rect.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ smoke check

        /// <summary>
        /// The smoke check after each capture: at a stop with pictures, every card settled (faded fully in and standing where
        /// the layout puts it, or gone) and, on screen, inside the screen, below the title's place, clear of the HUD's legend and
        /// readouts, of the narration card and of each other. A stop that shows fewer of its pictures than it names (up to
        /// <see cref="MaxCards"/>) is reported as a warning, not a failure.
        /// </summary>
        IEnumerable<string> Problems()
        {
            List<string> problems = new List<string>();
            if (current != this || shownStep == null || canvas == null) return problems;
            string stop = shownStep.Id;
            float scale = Mathf.Max(canvas.scaleFactor, 1e-4f);
            Vector2 size = canvasRect.rect.size;

            int wanted = Mathf.Min(entries.Count, MaxCards);
            if (plan.Count < wanted)
            {
                Debug.LogWarning($"[Why] smoke check '{CheckName}' warns at stop '{stop}' (not a failure): it shows {plan.Count} of its " +
                                 $"{wanted} pictures; {Room()}");
            }

            List<(string id, Rect box)> shown = new List<(string, Rect)>();
            foreach (Card c in cards)
            {
                if (c.Entry == null) continue;
                // faded fully in or out, and a card that shows at its place, not on its way there (where that is on screen is
                // checked below)
                bool settled = c.Target > 0 ? c.Delay <= 0 && c.Alpha >= 1 : c.Alpha <= 0;
                if (!settled) problems.Add($"stop '{stop}': picture '{c.Id}' is still {(c.Target > 0 ? "coming in" : "going")} (alpha {c.Alpha:0.00})");
                if (c.Target > 0 && ((c.Pos - c.GoalPos).sqrMagnitude > 0.25f || Mathf.Abs(c.Width - c.GoalWidth) > 0.5f))
                {
                    problems.Add($"stop '{stop}': picture '{c.Id}' is still on its way to its place (at {c.Pos.x:0},{c.Pos.y:0}, " +
                                 $"{c.Width:0} wide; going to {c.GoalPos.x:0},{c.GoalPos.y:0}, {c.GoalWidth:0} wide)");
                }

                if (c.Target > 0 && Box(c.Rect, scale, size, out Rect box)) shown.Add((c.Id, box));
            }

            float title = hud != null ? hud.TitleBottom : 0;
            foreach ((string id, Rect box) in shown)
            {
                if (box.xMin < -0.5f || box.yMin < -0.5f || box.xMax > size.x + 0.5f || box.yMax > size.y + 0.5f)
                    problems.Add($"stop '{stop}': picture '{id}' runs off the screen ({Describe(box)})");
                if (box.yMin < title - 0.5f) problems.Add($"stop '{stop}': picture '{id}' stands in the view title's place (above {title:0})");
            }

            List<(string what, Rect box)> blocks = new List<(string, Rect)>();
            Transform content = hud != null ? hud.transform.Find("Hud/Content") : null;
            foreach (string name in new[] { "Legend", "SpaceReadout", "LifelineReadout" })
            {
                if (content != null && Box(content.Find(name) as RectTransform, scale, size, out Rect r)) blocks.Add(("the HUD's " + name, r));
            }

            if (director != null && director.TryGetCardRect(out Rect card)) blocks.Add(("the narration card", Down(card, size.y)));

            for (int i = 0; i < shown.Count; i++)
            {
                foreach ((string what, Rect box) in blocks)
                {
                    if (Overlap(shown[i].box, box)) problems.Add($"stop '{stop}': picture '{shown[i].id}' ({Describe(shown[i].box)}) overlaps {what} ({Describe(box)})");
                }

                for (int j = i + 1; j < shown.Count; j++)
                {
                    if (Overlap(shown[i].box, shown[j].box)) problems.Add($"stop '{stop}': pictures '{shown[i].id}' and '{shown[j].id}' overlap");
                }
            }

            return problems;
        }

        /// <summary>A block's box on screen in canvas units down from the top-left corner; false while it is not shown.</summary>
        bool Box(RectTransform rt, float scale, Vector2 size, out Rect box)
        {
            box = default;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(corners); // an overlay canvas's world corners are screen pixels
            float x0 = corners[0].x / scale, y0 = corners[0].y / scale, x1 = corners[2].x / scale, y1 = corners[2].y / scale;
            box = Rect.MinMaxRect(Mathf.Min(x0, x1), size.y - Mathf.Max(y0, y1), Mathf.Max(x0, x1), size.y - Mathf.Min(y0, y1));
            return true;
        }

        /// <summary>Two boxes share more than half a unit each way.</summary>
        static bool Overlap(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.5f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.5f;

        static string Describe(Rect r) => $"{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}";
    }
}
