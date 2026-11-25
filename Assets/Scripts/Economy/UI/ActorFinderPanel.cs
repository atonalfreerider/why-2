using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.Humans.Social;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// Individual Actor Finder &amp; Causal Behavioral Predictor (Economy scene, toggled with <c>F</c>).
    /// <para>
    /// Lets the viewer search for any individual actor — either among the 4,996 simulated Americans along the
    /// economic frontier (by ID, industry, stratum, family mobility, ownership, or fear level) or among the
    /// 1,139 most-followed public figures in the Instagram social network cloud — inspect their Jev-calibrated
    /// causal behavioral forecast (fear vs. desire vector, economic river siphoning channels, fame-to-mobility
    /// vs. happiness trade-off), and fly directly to them in the 3D scene (<c>atom</c>, <c>ladder</c>,
    /// <c>dilemma</c>, or <c>social</c>).
    /// </para>
    /// Starts hidden so default views, captures, and smoke checks remain completely unobstructed.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class ActorFinderPanel : GraphModule
    {
        const int SortingOrder = 48;
        const int MaxResultRows = 8;
        const float PanelWidth = 820f, PanelHeight = 600f;

        GraphRoot root;
        RectTransform panel;
        TMP_InputField searchInput;
        Button simTab, publicTab, flyPrimaryBtn, flySecondaryBtn, flyTertiaryBtn;
        TextMeshProUGUI simTabLabel, publicTabLabel, flyPrimaryLabel, flySecondaryLabel, flyTertiaryLabel, statusText, forecastText;
        readonly List<Button> filterButtons = new List<Button>();
        readonly List<Button> rowButtons = new List<Button>();
        readonly List<TextMeshProUGUI> rowLabels = new List<TextMeshProUGUI>();
        readonly List<Action> rowActions = new List<Action>();

        bool open, publicMode;
        ActorPredictor.SimFilter simFilter = ActorPredictor.SimFilter.All;
        int selectedSim = -1;
        SocialPeople.Person selectedPublic;
        int lastYear = -1;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("ActorFinder", SortingOrder, transform);
            panel = UiFactory.Panel(canvas.transform, "ActorFinderPanel", new Color(.018f, .025f, .04f, .96f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var title = Text("Title", "<b>FIND AN ACTOR · CAUSAL BEHAVIORAL PREDICTOR</b>  <size=78%><color=#8FA6BE>(F to toggle · Esc closes · Jev priors)</color></size>", 15, 16, 12, 740, 24);
            title.richText = true;
            var close = UiFactory.Button(panel, "Close", "\u00D7", 16, () => SetOpen(false));
            Place((RectTransform)close.transform, PanelWidth - 38, 10, 26, 26);

            // Mode tabs
            simTab = UiFactory.Button(panel, "SimTab", "Simulated Frontier (Americans)", 12, () => { publicMode = false; RefreshResults(); });
            simTabLabel = simTab.GetComponentInChildren<TextMeshProUGUI>();
            Place((RectTransform)simTab.transform, 16, 44, 240, 26);

            publicTab = UiFactory.Button(panel, "PublicTab", "Social Cloud (Instagram / Public)", 12, () => { publicMode = true; RefreshResults(); });
            publicTabLabel = publicTab.GetComponentInChildren<TextMeshProUGUI>();
            Place((RectTransform)publicTab.transform, 264, 44, 240, 26);

            // Search box (TMP_InputField so HudKit.TypingInField() suppresses hotkeys while typing)
            searchInput = CreateSearchInput(panel, 16, 78, 344, 28);
            searchInput.onValueChanged.AddListener(_ => RefreshResults());

            // Quick filter chips (simulated mode)
            var filters = new (string label, ActorPredictor.SimFilter f)[]
            {
                ("All", ActorPredictor.SimFilter.All),
                ("Climbers", ActorPredictor.SimFilter.Climbers),
                ("Bottom 20%", ActorPredictor.SimFilter.Bottom20),
                ("Top 1%", ActorPredictor.SimFilter.Top1),
                ("Owners", ActorPredictor.SimFilter.Owners),
                ("High Fear", ActorPredictor.SimFilter.HighFear),
                ("Notables", ActorPredictor.SimFilter.Notables),
            };
            float fx = 16;
            for (int i = 0; i < filters.Length; i++)
            {
                var entry = filters[i];
                float w = i == 0 ? 34 : i == 2 ? 72 : 60;
                Button b = UiFactory.Button(panel, "Filter_" + entry.label, entry.label, 10, () => { simFilter = entry.f; RefreshResults(); });
                Place((RectTransform)b.transform, fx, 112, w, 22);
                filterButtons.Add(b);
                fx += w + 4;
            }

            statusText = Text("Status", "", 11, 16, 140, 344, 18);
            statusText.color = new Color(.68f, .76f, .86f);

            // Result rows (left column)
            for (int i = 0; i < MaxResultRows; i++)
            {
                int idx = i;
                Button b = UiFactory.Button(panel, "Row_" + i, "", 11, () => { if (idx < rowActions.Count) rowActions[idx](); });
                TextMeshProUGUI lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.textWrappingMode = TextWrappingModes.Normal;
                Place((RectTransform)b.transform, 16, 162 + i * 48, 344, 44);
                rowButtons.Add(b);
                rowLabels.Add(lbl);
            }

            // Forecast card (right column)
            Image rightBg = UiFactory.Panel(panel, "ForecastBg", new Color(.03f, .045f, .07f, .92f), false);
            Place(rightBg.rectTransform, 376, 78, PanelWidth - 392, PanelHeight - 134);

            forecastText = Text("Forecast", "Select an actor on the left to generate their causal behavioral & mobility prediction.", 12, 388, 88, PanelWidth - 416, PanelHeight - 152);
            forecastText.textWrappingMode = TextWrappingModes.Normal;
            forecastText.overflowMode = TextOverflowModes.Overflow;
            forecastText.richText = true;

            // Fly / navigate action buttons at bottom right
            flyPrimaryBtn = UiFactory.Button(panel, "FlyPrimary", "Fly to Mind (Atom)", 11, () => FlyTo(0));
            flyPrimaryLabel = flyPrimaryBtn.GetComponentInChildren<TextMeshProUGUI>();
            Place((RectTransform)flyPrimaryBtn.transform, 376, PanelHeight - 44, 138, 28);

            flySecondaryBtn = UiFactory.Button(panel, "FlySecondary", "View Life Ladder", 11, () => FlyTo(1));
            flySecondaryLabel = flySecondaryBtn.GetComponentInChildren<TextMeshProUGUI>();
            Place((RectTransform)flySecondaryBtn.transform, 522, PanelHeight - 44, 138, 28);

            flyTertiaryBtn = UiFactory.Button(panel, "FlyTertiary", "Tit-for-Tat Arena", 11, () => FlyTo(2));
            flyTertiaryLabel = flyTertiaryBtn.GetComponentInChildren<TextMeshProUGUI>();
            Place((RectTransform)flyTertiaryBtn.transform, 668, PanelHeight - 44, 136, 28);

            panel.gameObject.SetActive(false);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            ActorPredictor.LoadPriors();
            lastYear = EconomyState.Year;
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && !root.TourActive)
            {
                if (open && kb.escapeKey.wasPressedThisFrame)
                {
                    SetOpen(false);
                    return;
                }
                if (!HudKit.TypingInField() && kb.fKey.wasPressedThisFrame)
                {
                    SetOpen(!open);
                    return;
                }
            }
            if (root.TourActive && open) SetOpen(false);
            if (open && lastYear != EconomyState.Year)
            {
                lastYear = EconomyState.Year;
                RefreshResults();
                RefreshForecast();
            }
        }

        void SetOpen(bool show)
        {
            open = show;
            panel.gameObject.SetActive(open);
            if (open)
            {
                lastYear = EconomyState.Year;
                if (selectedSim < 0 && EconomyState.Person >= 0) selectedSim = EconomyState.Person;
                RefreshResults();
                RefreshForecast();
            }
            else
            {
                HudKit.ReleaseSelection();
            }
        }

        void RefreshResults()
        {
            for (int i = 0; i < filterButtons.Count; i++) filterButtons[i].gameObject.SetActive(!publicMode);
            simTabLabel.fontStyle = publicMode ? FontStyles.Normal : FontStyles.Bold;
            publicTabLabel.fontStyle = publicMode ? FontStyles.Bold : FontStyles.Normal;

            rowActions.Clear();
            string query = searchInput != null ? searchInput.text : "";
            int year = EconomyState.Year;

            if (!publicMode)
            {
                List<ActorPredictor.SimMatch> list = ActorPredictor.FindSimulated(LandService.Model, year, query, simFilter, MaxResultRows);
                statusText.text = $"{list.Count} simulated actors shown ({year}) · filter: {simFilter}";
                for (int i = 0; i < MaxResultRows; i++)
                {
                    bool active = i < list.Count;
                    rowButtons[i].gameObject.SetActive(active);
                    if (!active) continue;
                    ActorPredictor.SimMatch m = list[i];
                    rowLabels[i].text = $"<b>{m.Label}</b>\n<size=85%><color=#9AB0C8>{m.Sublabel}</color></size>";
                    int pid = m.PersonId;
                    rowActions.Add(() =>
                    {
                        selectedSim = pid;
                        selectedPublic = null;
                        EconomyState.SetPerson(pid);
                        RefreshForecast();
                    });
                }
                if (selectedSim < 0 && list.Count > 0) selectedSim = list[0].PersonId;
            }
            else
            {
                SocialPeople social = HillSocialLayer.People;
                List<ActorPredictor.PublicMatch> list = ActorPredictor.FindPublic(social, query, MaxResultRows);
                statusText.text = $"{list.Count} public figures shown (social cloud: {social?.people?.Length ?? 0})";
                for (int i = 0; i < MaxResultRows; i++)
                {
                    bool active = i < list.Count;
                    rowButtons[i].gameObject.SetActive(active);
                    if (!active) continue;
                    ActorPredictor.PublicMatch m = list[i];
                    rowLabels[i].text = $"<b>{m.Label}</b>\n<size=85%><color=#9AB0C8>{m.Sublabel}</color></size>";
                    SocialPeople.Person p = m.Person;
                    rowActions.Add(() =>
                    {
                        selectedPublic = p;
                        RefreshForecast();
                    });
                }
                if (selectedPublic == null && list.Count > 0) selectedPublic = list[0].Person;
            }
            RefreshForecast();
        }

        void RefreshForecast()
        {
            int year = EconomyState.Year;
            ActorPredictor.Forecast f = publicMode
                ? ActorPredictor.PredictPublic(HillSocialLayer.People, selectedPublic, LandService.Model, year)
                : (selectedSim >= 0 ? ActorPredictor.PredictSimulated(LandService.Model, LandService.Current, selectedSim, year) : null);

            if (f == null)
            {
                forecastText.text = "No actor selected or data not yet ready.";
                flyPrimaryBtn.interactable = flySecondaryBtn.interactable = flyTertiaryBtn.interactable = false;
                return;
            }

            forecastText.text = f.SummaryText;
            flyPrimaryBtn.interactable = true;
            if (f.IsPublic)
            {
                flyPrimaryLabel.text = "Fly to Social Cloud";
                flySecondaryBtn.gameObject.SetActive(false);
                flyTertiaryBtn.gameObject.SetActive(false);
            }
            else
            {
                flyPrimaryLabel.text = "Fly to Mind (Atom)";
                flySecondaryBtn.gameObject.SetActive(true);
                flyTertiaryBtn.gameObject.SetActive(true);
                flySecondaryBtn.interactable = flyTertiaryBtn.interactable = true;
                flySecondaryLabel.text = "View Life Ladder";
                flyTertiaryLabel.text = "Tit-for-Tat Arena";
            }
        }

        void FlyTo(int mode)
        {
            if (publicMode)
            {
                SetOpen(false);
                root.Focus("social");
                if (selectedPublic != null && HillSocialLayer.Placed.TryGetValue(selectedPublic.Index, out var placed))
                {
                    var pose = root.Rig.Pose;
                    pose.Target = EconomyStage.Land().World(placed.at);
                    pose.Distance = 24f;
                    root.Rig.FlyTo(pose, 1.4f);
                }
                return;
            }

            if (selectedSim < 0) return;
            EconomyState.SetPerson(selectedSim);
            SetOpen(false);
            root.Focus(mode == 0 ? "atom" : mode == 1 ? "ladder" : "dilemma");
        }

        static TMP_InputField CreateSearchInput(Transform parent, float x, float y, float w, float h)
        {
            Image bg = UiFactory.Panel(parent, "SearchBg", new Color(.05f, .07f, .11f, .96f));
            Place(bg.rectTransform, x, y, w, h);

            RectTransform area = UiFactory.Rect(bg.transform, "TextArea");
            area.Fill(8, 2, 8, 2);

            TextMeshProUGUI placeholder = UiFactory.Text(area, "Placeholder", "Search name, industry, or #id...", 12, new Color(.52f, .60f, .70f), TextAlignmentOptions.MidlineLeft);
            placeholder.rectTransform.Fill();
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI text = UiFactory.Text(area, "Text", "", 12, GraphStyle.Text, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Fill();
            text.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_InputField input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = 12;
            input.caretColor = Color.white;
            return input;
        }

        TextMeshProUGUI Text(string name, string content, float size, float x, float y, float w, float h)
        {
            TextMeshProUGUI t = UiFactory.Text(panel, name, content, size, GraphStyle.Text);
            Place(t.rectTransform, x, y, w, h);
            return t;
        }

        static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }
    }
}
