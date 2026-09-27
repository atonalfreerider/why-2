using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Humans;
using Why.UI;

namespace Why.Figures
{
    /// <summary>
    /// Tracks famous historical figures through time. A panel lists the people alive at the moment under
    /// the camera; choosing one follows their life: the lens opens on their lifetime, the camera travels
    /// along their lifeline from birth to death, and their influences glow as threads of cause and effect
    /// (who shaped them, whom they shaped) - each of which can be followed in turn.
    ///
    /// F toggles the panel, Esc stops following. In landscape both panels sit at the right edge; on a
    /// portrait screen they become a compact full-width sheet under the HUD title (the people in two columns,
    /// the card's links side by side), clear of the preset bar at the bottom. There the people panel starts
    /// hidden (it would cover the old end of the turned views; F shows it). Both step aside for the tour.
    /// </summary>
    public sealed class FigureTracker : GraphModule
    {
        const int MaxRows = 9;
        const float PanelWidth = 340;
        const float TravelSeconds = 7f;

        /// <summary>Beyond this camera distance names are too small to be worth listing (as the landscape view frames it).</summary>
        const float MaxListDistance = 16f;

        /// <summary>
        /// Portrait: the glide keeps the current moment of the life between these screen heights (normalized device
        /// y): this far under the card at the top, down to just above the legend and the lifeline readout (-0.42).
        /// </summary>
        const float GlideCardClearance = 0.05f, GlideBottom = -0.38f;

        // portrait sheet (canvas units of the 500-wide portrait canvas, shared with the HUD)
        const int PortraitRows = 6;
        const float PortraitMargin = HudKit.PortraitMargin;

        /// <summary>
        /// Portrait: top of the sheet, below the HUD title block (16 + up to ~100 with a three-line subtitle) and
        /// the F3 stats panel (to 128).
        /// </summary>
        const float PortraitTop = 132f;
        const float PortraitHeader = 40f;
        const float RowPitch = 34f;
        const float RowHeight = 30f;

        /// <summary>Card links: row and header heights and the gap between rows.</summary>
        const float LinkRowHeight = 24f, LinkHeaderHeight = 18f, LinkGap = 2f;

        GraphRoot root;
        List<Figure> figures = new List<Figure>();
        HumanWorld world;

        Canvas canvas;
        CanvasGroup panelGroup, cardGroup;
        RectTransform panelRect, cardRect;
        TextMeshProUGUI panelTitle, panelYear;
        readonly List<Button> rows = new List<Button>();
        readonly List<Figure> rowFigures = new List<Figure>();
        TextMeshProUGUI cardName, cardMeta, cardBlurb;
        RectTransform cardLinks;
        readonly List<Button> linkButtons = new List<Button>();

        // F toggles the people panel of the current orientation; portrait starts without it
        bool panelEnabled = true, portraitPanelEnabled;
        float nextSample;
        double yearUnderCamera = double.NaN;
        Figure following;
        ViewPreset returnPreset, followPreset;
        float travelStart = -1, travelHold;
        int layoutVersion = -1;
        bool portraitLayout;

        // portrait glide (see FrameGlide): where the camera target starts, the share of the lifeline it travels, the
        // year the lens is centered on, and the screen shape they are for
        Vector3 glideOrigin;
        float glideShare = 1;
        double glideFocusYearsAgo;
        int glideOrientation;

        public override int Order => 20;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            root.OrientationChanged += OnOrientationChanged;
            BuildUi();
            panelGroup.alpha = 0;
            cardGroup.alpha = 0;
        }

        void OnDestroy()
        {
            if (root != null) root.OrientationChanged -= OnOrientationChanged;
        }

        /// <summary>
        /// The screen flipped and GraphRoot is re-framing the view (<see cref="GraphRoot.ReframeSeconds"/>): a
        /// pending or running glide waits for that flight and then goes on where it was, around the new framing.
        /// </summary>
        void OnOrientationChanged()
        {
            glideOrientation = ScreenLayout.OrientationVersion;
            if (following == null || travelStart < 0) return;
            travelStart += GraphRoot.ReframeSeconds;
            travelHold = Time.unscaledTime + GraphRoot.ReframeSeconds;
            // the card was re-laid out for the new shape in Update (Layout), before this LateUpdate event
            if (followPreset != null && Anchors.TryGet(following.AnchorKey, out Anchor a)) FrameGlide(a);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            figures = graphRoot.Context.Shared<List<Figure>>(FigureLinksLayer.SharedKey) ?? new List<Figure>();
            world = graphRoot.Context.Shared<HumanWorld>(HumanWorld.SharedKey);
            // only figures the lifeline layer actually placed can be followed
            figures = figures.Where(f => Anchors.TryGet(f.AnchorKey, out Anchor _)).ToList();
        }

        void BuildUi()
        {
            canvas = UiFactory.CreateCanvas("FigureTracker", 30, transform);

            // people of this time
            Image panel = UiFactory.Panel(canvas.transform, "PeoplePanel");
            panelRect = panel.rectTransform;
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelTitle = UiFactory.Text(panel.transform, "Title", "PEOPLE OF THIS TIME", 13, GraphStyle.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            panelTitle.characterSpacing = 6;
            panelTitle.rectTransform.Fill(16, 0, 16, 14);
            panelYear = UiFactory.Text(panel.transform, "Year", "", 22, GraphStyle.Text, TextAlignmentOptions.TopLeft);
            for (int i = 0; i < MaxRows; i++)
            {
                int index = i;
                Button b = UiFactory.Button(panel.transform, "Row" + i, "", 14, () => Follow(rowFigures[index]));
                TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.richText = true;
                rows.Add(b);
                rowFigures.Add(null);
            }

            // the followed figure
            Image card = UiFactory.Panel(canvas.transform, "FigureCard");
            cardRect = card.rectTransform;
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI kicker = UiFactory.Text(card.transform, "Kicker", "FOLLOWING  -  Esc to return", 12,
                GraphStyle.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            kicker.characterSpacing = 4;
            kicker.rectTransform.Fill(18, 0, 18, 14);
            cardName = UiFactory.Text(card.transform, "Name", "", 24, GraphStyle.Text, TextAlignmentOptions.TopLeft,
                FontStyles.Bold);
            cardName.rectTransform.Fill(18, 0, 18, 34);
            cardMeta = UiFactory.Text(card.transform, "Meta", "", 14, GraphStyle.TextDim, TextAlignmentOptions.TopLeft);
            cardMeta.rectTransform.Fill(18, 0, 18, 68);
            cardBlurb = UiFactory.Text(card.transform, "Blurb", "", 15, GraphStyle.Text, TextAlignmentOptions.TopLeft);
            cardBlurb.rectTransform.Fill(18, 0, 18, 96);
            cardLinks = UiFactory.Rect(card.transform, "Links");
            Layout();
        }

        /// <summary>Places both panels for the current orientation (see the class summary).</summary>
        void Layout()
        {
            layoutVersion = ScreenLayout.OrientationVersion;
            portraitLayout = ScreenLayout.IsPortrait;
            if (portraitLayout)
            {
                // a full-width sheet under the HUD title: the year beside the heading, the people in two columns
                TopSheet(panelRect, PortraitHeader + RowPitch * (PortraitRows / 2) + 8);
                panelYear.alignment = TextAlignmentOptions.TopRight;
                panelYear.rectTransform.Fill(16, 0, 16, 6);
                for (int i = 0; i < rows.Count; i++)
                {
                    int column = i % 2;
                    PlaceRow((RectTransform)rows[i].transform, column * 0.5f, column * 0.5f + 0.5f,
                        column == 0 ? 10 : 4, column == 0 ? 4 : 10, PortraitHeader + (i / 2) * RowPitch);
                }

                // the same place; FillCard sizes it to its content
                TopSheet(cardRect, 300);
            }
            else
            {
                panelRect.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 30),
                    new Vector2(PanelWidth, 76 + MaxRows * RowPitch));
                panelYear.alignment = TextAlignmentOptions.TopLeft;
                panelYear.rectTransform.Fill(16, 0, 16, 32);
                for (int i = 0; i < rows.Count; i++)
                {
                    PlaceRow((RectTransform)rows[i].transform, 0, 1, 10, 10, 70 + i * RowPitch);
                }

                cardRect.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 30),
                    new Vector2(PanelWidth + 40, 420));
                cardLinks.anchorMin = new Vector2(0, 0);
                cardLinks.anchorMax = new Vector2(1, 0);
                cardLinks.pivot = new Vector2(0.5f, 0);
                cardLinks.offsetMin = new Vector2(12, 12);
                cardLinks.offsetMax = new Vector2(-12, 212);
            }

            // the portrait columns are narrow: a long name and civilization end in an ellipsis
            TextOverflowModes overflow = portraitLayout ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            foreach (Button row in rows) row.GetComponentInChildren<TextMeshProUGUI>().overflowMode = overflow;

            // the card's height and links depend on its content in portrait
            if (following != null && Anchors.TryGet(following.AnchorKey, out Anchor a)) FillCard(following, a);
            nextSample = 0; // re-list with the number of rows this layout shows
        }

        /// <summary>Portrait: a rect as a full-width sheet of this height under the HUD title.</summary>
        static void TopSheet(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(-2 * PortraitMargin, height);
            rt.anchoredPosition = new Vector2(0, -PortraitTop);
        }

        /// <summary>A row button spanning [x0, x1] of the panel's width (inset left / right), yFromTop down.</summary>
        static void PlaceRow(RectTransform rt, float x0, float x1, float left, float right, float yFromTop)
        {
            rt.anchorMin = new Vector2(x0, 1);
            rt.anchorMax = new Vector2(x1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, 0);
            rt.offsetMax = new Vector2(-right, 0);
            rt.anchoredPosition = new Vector2((left - right) * 0.5f, -yFromTop);
            rt.sizeDelta = new Vector2(-(left + right), RowHeight);
        }

        /// <summary>Rows the current layout shows.</summary>
        int VisibleRows => portraitLayout ? PortraitRows : MaxRows;

        void Update()
        {
            if (root == null) return;
            if (layoutVersion != ScreenLayout.OrientationVersion) Layout();
            if (!root.IsLoaded) return;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame)
                {
                    if (portraitLayout) portraitPanelEnabled = !portraitPanelEnabled;
                    else panelEnabled = !panelEnabled;
                }

                if (kb.escapeKey.wasPressedThisFrame && following != null) StopFollowing();
            }

            if (root.TourActive && following != null) StopFollowing();

            if (Time.unscaledTime >= nextSample && following == null)
            {
                nextSample = Time.unscaledTime + 0.25f;
                SampleTime();
            }

            bool enabled = portraitLayout ? portraitPanelEnabled : panelEnabled;
            bool showPanel = enabled && following == null && !root.TourActive && !double.IsNaN(yearUnderCamera) &&
                             HasRows();
            Fade(panelGroup, showPanel);
            Fade(cardGroup, following != null && !root.TourActive);
            Travel();
        }

        /// <summary>True when anyone is listed (a loop: this runs every frame and must not allocate).</summary>
        bool HasRows()
        {
            for (int i = 0; i < rowFigures.Count; i++)
            {
                if (rowFigures[i] != null) return true;
            }

            return false;
        }

        static void Fade(CanvasGroup g, bool on)
        {
            g.alpha = Mathf.MoveTowards(g.alpha, on ? 1 : 0, Time.unscaledDeltaTime * 4);
            g.interactable = g.blocksRaycasts = g.alpha > 0.5f;
        }

        /// <summary>Which moment of the human era is under the camera, and who was alive then.</summary>
        void SampleTime()
        {
            GraphWarp.Inverse(root.Rig.Pose.Target, out float u, out float _);
            double ya = DeepTime.YearsAgo(u);
            double year = DeepTime.NowYear - ya;
            // only on the human branch and close enough to read names; a portrait view frames its subject from its
            // own distance, so the limit follows the current preset's portrait / landscape distance ratio
            float maxDistance = MaxListDistance;
            ViewPreset preset = root.CurrentPreset;
            if (portraitLayout && preset != null)
            {
                maxDistance *= preset.Pose().Distance / Mathf.Max(preset.Distance, 1e-3f);
            }

            if (year < -3500 || root.Rig.Pose.Distance > maxDistance)
            {
                yearUnderCamera = double.NaN;
                return;
            }

            yearUnderCamera = year;
            double now = DeepTime.NowYear;
            int shown = VisibleRows;
            List<Figure> alive = figures.Where(f => f.AliveIn(year, now))
                .OrderByDescending(f => f.prominence).Take(shown).ToList();
            panelYear.text = FormatYear(year);
            for (int i = 0; i < MaxRows; i++)
            {
                Figure f = i < alive.Count ? alive[i] : null;
                rowFigures[i] = f;
                rows[i].gameObject.SetActive(f != null);
                if (f == null) continue;
                string civ = world != null && world.ById.TryGetValue(f.civ, out Civ c) ? c.Name : f.civ;
                int age = (int)(year - f.born);
                rows[i].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"<b>{f.name}</b>  <color=#{UiFactory.Hex(GraphStyle.TextDim)}><size=85%>{civ}, age {age}</size></color>";
            }
        }

        /// <summary>Open the lens on a figure's lifetime and travel along it.</summary>
        public void Follow(Figure f)
        {
            if (f == null || !Anchors.TryGet(f.AnchorKey, out Anchor a)) return;
            if (following == null) returnPreset = root.CurrentPreset;
            following = f;

            double now = DeepTime.NowYear;
            double born = f.born, end = f.End(now);
            double pad = Math.Max(12, (end - born) * 0.3);
            ViewPreset p = new ViewPreset
            {
                Id = "figure",
                Title = f.name,
                Subtitle = f.Years + (string.IsNullOrEmpty(f.role) ? "" : "  -  " + f.role),
                YaOld = now - (born - pad),
                YaNew = Math.Max(0, now - (end + pad)),
                LogOffset = 1e5,
                Length = 10,
                RhoScale = 1.5f,
                YScale = 2f,
                StrataEmphasis = 0.12f,
                TargetRho = a.Rho,
                TargetY = a.Y,
                Pitch = 42,
                Distance = 5.5f
            };
            root.Focus(p, 2.2f);
            travelStart = Time.unscaledTime + 2.3f;
            followPreset = p;
            glideFocusYearsAgo = p.Warp().FocusYearsAgo;
            glideOrientation = ScreenLayout.OrientationVersion;

            List<IdRange> ids = new List<IdRange> { a.Ids };
            foreach (Figure other in f.Influencers.Concat(f.Influenced))
            {
                if (Anchors.TryGet(other.AnchorKey, out Anchor oa)) ids.Add(oa.Ids);
            }

            Highlighter.Set(ids.Take(8), GraphStyle.HighlightGlow, 0.65f);
            FillCard(f, a);
            FrameGlide(a); // after the card: in portrait the glide keeps clear of it
        }

        /// <summary>
        /// Where the glide along the followed life runs. Landscape: the camera target follows the lifeline (see
        /// <see cref="Travel"/>). Portrait: the turned view shows the life down the screen, the card covers the top,
        /// so the current moment moves down the free part instead - from just under the card (birth) to just above
        /// the legend (death) - and the camera travels only the share of the lifeline that does not fit there
        /// (none when the whole life fits: it is then centered and the camera stays). Exact for the lifeline's
        /// points, which lie on the target's height plane along the view.
        /// </summary>
        void FrameGlide(Anchor a)
        {
            CameraPose pose = followPreset.Pose();
            glideOrigin = pose.Target;
            glideShare = 1;
            if (!ScreenLayout.IsPortrait) return;

            // positions along the view (world units ahead of the framed target) under the preset's final warp
            WarpState w = followPreset.Warp();
            double now = DeepTime.NowYear;
            Vector3 ahead = Quaternion.Euler(0, pose.Yaw, 0) * Vector3.forward;
            float birth = AheadOf(pose.Target, ahead, a, now - following.born, w);
            float death = AheadOf(pose.Target, ahead, a, now - following.End(now), w);
            float focus = AheadOf(pose.Target, ahead, a, glideFocusYearsAgo, w);

            // the free band as distances ahead of the camera target (a point r ahead shows at
            // y = r sin / ((distance + r cos) tan), solved for r)
            float tan = Mathf.Tan(CameraRig.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float cardBottom = 1 - 2 * (PortraitTop + cardRect.sizeDelta.y) / Mathf.Max(UiFactory.CanvasSize.y, 1);
            float top = AheadAtScreenY(Mathf.Max(cardBottom - GlideCardClearance, GlideBottom + 0.1f), pose, tan);
            float bottom = AheadAtScreenY(GlideBottom, pose, tan);

            // with the target at origin + drop + share (lifeline(year) - lifeline(focus)), the current moment shows
            // (1 - share) ahead(year) + share focus - drop ahead of it: the birth at the top when the glide starts,
            // the death at the bottom when it ends
            float life = Mathf.Max(birth - death, 1e-4f);
            glideShare = Mathf.Clamp01(1 - (top - bottom) / life);
            float drop = glideShare > 0
                ? (1 - glideShare) * birth + glideShare * focus - top
                : (birth + death - top - bottom) * 0.5f;
            glideOrigin += ahead * drop;
        }

        /// <summary>World units ahead (along the view) of the target at which a point of the followed lifeline lies.</summary>
        static float AheadOf(Vector3 target, Vector3 ahead, Anchor a, double yearsAgo, WarpState w) =>
            Vector3.Dot(GraphWarp.ToWorld(DeepTime.Arc(Math.Max(yearsAgo, 1e-6)), a.Y, a.Rho, w) - target, ahead);

        /// <summary>How far ahead of a pose's target (on its height plane) a point shows at normalized device y.</summary>
        static float AheadAtScreenY(float y, CameraPose pose, float tan)
        {
            float sin = Mathf.Sin(pose.Pitch * Mathf.Deg2Rad), cos = Mathf.Cos(pose.Pitch * Mathf.Deg2Rad);
            return y * tan * pose.Distance / (sin - y * tan * cos);
        }

        void FillCard(Figure f, Anchor a)
        {
            string civ = world != null && world.ById.TryGetValue(f.civ, out Civ c) ? c.Name : f.civ;
            cardName.text = f.name;
            cardMeta.text = f.Years + "  |  " + civ + (string.IsNullOrEmpty(f.role) ? "" : "  |  " + f.role);
            cardBlurb.text = f.blurb ?? a.Blurb ?? "";

            foreach (Button b in linkButtons) Destroy(b.gameObject);
            linkButtons.Clear();

            if (!portraitLayout)
            {
                // one column filling the bottom of the card from the top of its links area down
                float y = 0;
                AddLinkHeader("SHAPED BY", 0, 1, ref y, f.Influencers.Count);
                foreach (Figure other in f.Influencers.Take(4)) AddLink(other, f.Note(other), 0, 1, ref y);
                AddLinkHeader("SHAPED", 0, 1, ref y, f.Influenced.Count);
                foreach (Figure other in f.Influenced.Take(4)) AddLink(other, other.Note(f), 0, 1, ref y);
                return;
            }

            // portrait: as tall as its content, the two link lists side by side under the blurb
            const float inset = 18f, pad = 12f;
            float inner = UiFactory.CanvasSize.x - 2 * PortraitMargin - 2 * inset;
            float blurbHeight = cardBlurb.text.Length > 0 ? cardBlurb.GetPreferredValues(cardBlurb.text, inner, 0).y : 0;
            float linksTop = 96 + blurbHeight + 10;
            float left = 0, right = 0;
            AddLinkHeader("SHAPED BY", 0, 0.5f, ref left, f.Influencers.Count);
            foreach (Figure other in f.Influencers.Take(4)) AddLink(other, f.Note(other), 0, 0.5f, ref left);
            AddLinkHeader("SHAPED", 0.5f, 1, ref right, f.Influenced.Count);
            foreach (Figure other in f.Influenced.Take(4)) AddLink(other, other.Note(f), 0.5f, 1, ref right);
            float linksHeight = Mathf.Max(left, right);

            TopSheet(cardRect, Mathf.Ceil(linksTop + linksHeight + pad));
            cardLinks.anchorMin = new Vector2(0, 1);
            cardLinks.anchorMax = new Vector2(1, 1);
            cardLinks.pivot = new Vector2(0.5f, 1);
            cardLinks.offsetMin = new Vector2(pad, 0);
            cardLinks.offsetMax = new Vector2(-pad, 0);
            cardLinks.anchoredPosition = new Vector2(0, -linksTop);
            cardLinks.sizeDelta = new Vector2(-2 * pad, linksHeight);
        }

        void AddLinkHeader(string text, float x0, float x1, ref float y, int count)
        {
            if (count == 0) return;
            Button header = UiFactory.Button(cardLinks, "Header", text, 11, null);
            header.interactable = false;
            header.GetComponent<Image>().color = Color.clear;
            PlaceLink(header, x0, x1, ref y, LinkHeaderHeight);
            TextMeshProUGUI t = header.GetComponentInChildren<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = GraphStyle.TextDim;
            t.characterSpacing = 4;
        }

        void AddLink(Figure other, string note, float x0, float x1, ref float y)
        {
            Button b = UiFactory.Button(cardLinks, "Link", "", 13, () => Follow(other));
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.richText = true;
            t.text = $"<b>{other.name}</b>" + (string.IsNullOrEmpty(note)
                ? ""
                : $"  <color=#{UiFactory.Hex(GraphStyle.TextDim)}><size=85%>{note}</size></color>");
            t.overflowMode = TextOverflowModes.Ellipsis;
            PlaceLink(b, x0, x1, ref y, LinkRowHeight);
        }

        /// <summary>A link row spanning [x0, x1] of the links area's width, yFromTop down from its top edge.</summary>
        void PlaceLink(Button b, float x0, float x1, ref float yFromTop, float height)
        {
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x0, 1);
            rt.anchorMax = new Vector2(x1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -yFromTop);
            rt.sizeDelta = new Vector2(x1 - x0 < 1 ? -LinkGap * 2 : 0, height);
            yFromTop += height + LinkGap;
            linkButtons.Add(b);
        }

        /// <summary>After the lens opens, glide the camera along the life from birth to death.</summary>
        void Travel()
        {
            if (following == null || travelStart < 0 || Time.unscaledTime < travelStart) return;
            if (root.Rig.UserActive)
            {
                travelStart = -1; // the user took the wheel
                return;
            }

            // the view is about to re-frame, or re-framing, for a new screen shape (see OnOrientationChanged)
            if (Time.unscaledTime < travelHold || glideOrientation != ScreenLayout.OrientationVersion) return;

            if (!Anchors.TryGet(following.AnchorKey, out Anchor a)) return;
            float t = Mathf.Clamp01((Time.unscaledTime - travelStart) / TravelSeconds);
            float e = t * t * (3 - 2 * t);
            double now = DeepTime.NowYear;
            double year = following.born + (following.End(now) - following.born) * e;
            Vector3 target = LifelineAt(a, now - year);
            if (ScreenLayout.IsPortrait)
            {
                // the turned view shows the life down the screen: glide part of the way, clear of the card (FrameGlide)
                target = glideOrigin + (target - LifelineAt(a, glideFocusYearsAgo)) * glideShare;
            }

            CameraPose pose = root.Rig.Pose;
            pose.Target = Vector3.Lerp(pose.Target, target, 0.08f);
            root.Rig.SetPose(pose);
            if (t >= 1) travelStart = -1;
        }

        /// <summary>A point of the followed lifeline (its anchor's height and relevance) under the current warp.</summary>
        static Vector3 LifelineAt(Anchor a, double yearsAgo) => GraphWarp.ToWorld(DeepTime.Arc(Math.Max(yearsAgo, 1e-6)), a.Y, a.Rho);

        void StopFollowing()
        {
            following = null;
            travelStart = -1;
            Highlighter.Clear();
            if (returnPreset != null) root.Focus(returnPreset, 2f);
        }

        static string FormatYear(double year)
        {
            double y = Math.Round(year);
            return y < 1 ? Math.Max(1, -y).ToString("0") + " BCE" : y.ToString("0");
        }
    }
}
