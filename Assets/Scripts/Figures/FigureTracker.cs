using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Humans;

namespace Why.Figures
{
    /// <summary>
    /// Tracks famous historical figures through time. A panel lists the people alive at the moment under
    /// the camera; choosing one follows their life: the lens opens on their lifetime, the camera travels
    /// along their lifeline from birth to death, and their influences glow as threads of cause and effect
    /// (who shaped them, whom they shaped) - each of which can be followed in turn.
    ///
    /// F toggles the panel, Esc stops following.
    /// </summary>
    public sealed class FigureTracker : GraphModule
    {
        const int MaxRows = 9;
        const float PanelWidth = 340;
        const float TravelSeconds = 7f;

        GraphRoot root;
        List<Figure> figures = new List<Figure>();
        HumanWorld world;

        Canvas canvas;
        CanvasGroup panelGroup, cardGroup;
        TextMeshProUGUI panelTitle, panelYear;
        readonly List<Button> rows = new List<Button>();
        readonly List<Figure> rowFigures = new List<Figure>();
        TextMeshProUGUI cardName, cardMeta, cardBlurb;
        RectTransform cardLinks;
        readonly List<Button> linkButtons = new List<Button>();

        bool panelEnabled = true;
        float nextSample;
        double yearUnderCamera = double.NaN;
        Figure following;
        ViewPreset returnPreset;
        float travelStart = -1;

        public override int Order => 20;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            BuildUi();
            panelGroup.alpha = 0;
            cardGroup.alpha = 0;
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
            panel.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 30),
                new Vector2(PanelWidth, 76 + MaxRows * 34));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelTitle = UiFactory.Text(panel.transform, "Title", "PEOPLE OF THIS TIME", 13, GraphStyle.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            panelTitle.characterSpacing = 6;
            panelTitle.rectTransform.Fill(16, 0, 16, 14);
            panelYear = UiFactory.Text(panel.transform, "Year", "", 22, GraphStyle.Text, TextAlignmentOptions.TopLeft);
            panelYear.rectTransform.Fill(16, 0, 16, 32);
            for (int i = 0; i < MaxRows; i++)
            {
                int index = i;
                Button b = UiFactory.Button(panel.transform, "Row" + i, "", 14, () => Follow(rowFigures[index]));
                RectTransform rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(10, 0);
                rt.offsetMax = new Vector2(-10, 0);
                rt.anchoredPosition = new Vector2(0, -70 - i * 34);
                rt.sizeDelta = new Vector2(-20, 30);
                TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.richText = true;
                rows.Add(b);
                rowFigures.Add(null);
            }

            // the followed figure
            Image card = UiFactory.Panel(canvas.transform, "FigureCard");
            card.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 30),
                new Vector2(PanelWidth + 40, 420));
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
            cardLinks.anchorMin = new Vector2(0, 0);
            cardLinks.anchorMax = new Vector2(1, 0);
            cardLinks.pivot = new Vector2(0.5f, 0);
            cardLinks.offsetMin = new Vector2(12, 12);
            cardLinks.offsetMax = new Vector2(-12, 212);
        }

        void Update()
        {
            if (root == null || !root.IsLoaded) return;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame) panelEnabled = !panelEnabled;
                if (kb.escapeKey.wasPressedThisFrame && following != null) StopFollowing();
            }

            if (root.TourActive && following != null) StopFollowing();

            if (Time.unscaledTime >= nextSample && following == null)
            {
                nextSample = Time.unscaledTime + 0.25f;
                SampleTime();
            }

            bool showPanel = panelEnabled && following == null && !root.TourActive && !double.IsNaN(yearUnderCamera) &&
                             rowFigures.Any(f => f != null);
            Fade(panelGroup, showPanel);
            Fade(cardGroup, following != null && !root.TourActive);
            Travel();
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
            // only on the human branch and close enough to read names
            if (year < -3500 || root.Rig.Pose.Distance > 16f)
            {
                yearUnderCamera = double.NaN;
                return;
            }

            yearUnderCamera = year;
            double now = DeepTime.NowYear;
            List<Figure> alive = figures.Where(f => f.AliveIn(year, now))
                .OrderByDescending(f => f.prominence).Take(MaxRows).ToList();
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
                TargetRho = a.Rho,
                TargetY = a.Y,
                Pitch = 42,
                Distance = 5.5f
            };
            root.Focus(p, 2.2f);
            travelStart = Time.unscaledTime + 2.3f;

            List<IdRange> ids = new List<IdRange> { a.Ids };
            foreach (Figure other in f.Influencers.Concat(f.Influenced))
            {
                if (Anchors.TryGet(other.AnchorKey, out Anchor oa)) ids.Add(oa.Ids);
            }

            Highlighter.Set(ids.Take(8), GraphStyle.HighlightGlow, 0.65f);
            FillCard(f, a);
        }

        void FillCard(Figure f, Anchor a)
        {
            string civ = world != null && world.ById.TryGetValue(f.civ, out Civ c) ? c.Name : f.civ;
            cardName.text = f.name;
            cardMeta.text = f.Years + "  |  " + civ + (string.IsNullOrEmpty(f.role) ? "" : "  |  " + f.role);
            cardBlurb.text = f.blurb ?? a.Blurb ?? "";

            foreach (Button b in linkButtons) Destroy(b.gameObject);
            linkButtons.Clear();
            float y = 200;
            AddLinkHeader("SHAPED BY", ref y, f.Influencers.Count);
            foreach (Figure other in f.Influencers.Take(4)) AddLink(other, f.Note(other), ref y);
            AddLinkHeader("SHAPED", ref y, f.Influenced.Count);
            foreach (Figure other in f.Influenced.Take(4)) AddLink(other, other.Note(f), ref y);
        }

        void AddLinkHeader(string text, ref float y, int count)
        {
            if (count == 0) return;
            Button header = UiFactory.Button(cardLinks, "Header", text, 11, null);
            header.interactable = false;
            header.GetComponent<Image>().color = Color.clear;
            PlaceLink(header, ref y, 18);
            TextMeshProUGUI t = header.GetComponentInChildren<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = GraphStyle.TextDim;
            t.characterSpacing = 4;
        }

        void AddLink(Figure other, string note, ref float y)
        {
            Button b = UiFactory.Button(cardLinks, "Link", "", 13, () => Follow(other));
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.richText = true;
            t.text = $"<b>{other.name}</b>" + (string.IsNullOrEmpty(note)
                ? ""
                : $"  <color=#{UiFactory.Hex(GraphStyle.TextDim)}><size=85%>{note}</size></color>");
            t.overflowMode = TextOverflowModes.Ellipsis;
            PlaceLink(b, ref y, 24);
        }

        void PlaceLink(Button b, ref float y, float height)
        {
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(0, height);
            y -= height + 2;
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

            if (!Anchors.TryGet(following.AnchorKey, out Anchor a)) return;
            float t = Mathf.Clamp01((Time.unscaledTime - travelStart) / TravelSeconds);
            float e = t * t * (3 - 2 * t);
            double now = DeepTime.NowYear;
            double year = following.born + (following.End(now) - following.born) * e;
            Vector3 target = GraphWarp.ToWorld(DeepTime.Arc(Math.Max(now - year, 1e-6)), a.Y, a.Rho);
            CameraPose pose = root.Rig.Pose;
            pose.Target = Vector3.Lerp(pose.Target, target, 0.08f);
            root.Rig.SetPose(pose);
            if (t >= 1) travelStart = -1;
        }

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
