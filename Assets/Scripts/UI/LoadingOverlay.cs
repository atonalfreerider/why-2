using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// Full-screen title card shown while the graph builds: "WHY", a subtitle, a thin progress bar and the
    /// loader's status. When loading completes it shows the build time, fades out and disables itself.
    /// </summary>
    public sealed class LoadingOverlay : GraphModule
    {
        /// <summary>Above every other canvas (HUD, director).</summary>
        const int SortingOrder = 30000;

        const float BarWidth = 300f;

        /// <summary>Width of the title card and its lines; narrower on a portrait canvas (500 units wide) to fit it.</summary>
        const float CardWidth = 640f;

        const float HoldSeconds = 0.55f;
        const float FadeSeconds = 0.8f;

        /// <summary>Longest step the animations take per frame, so a long frame cannot skip the fade.</summary>
        const float MaxStep = 1f / 30f;

        /// <summary>
        /// Layer class names in the loader status ("CivBandsLayer", "HumanWorldLoader") read as words
        /// ("civ bands", "human world").
        /// </summary>
        static readonly Regex LayerName = new Regex(@"\b([A-Z][A-Za-z]*?)(?:Layer|Loader)\b");

        static readonly Regex CamelHump = new Regex("(?<=[a-z])(?=[A-Z])");

        GraphRoot root;
        Canvas canvas;
        CanvasGroup group;
        RectTransform barFill;
        TextMeshProUGUI status;
        RectTransform[] widthFitted;
        int fittedVersion = -1;
        string loaderStatus;
        float shownProgress;
        bool loaded;
        float doneTime = -1;

        public override int Order => -10;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Build();
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            // the hold starts on the next frame: this runs at the end of the (long) upload frame
            loaded = true;
            group.blocksRaycasts = false;
            status.text = "built in " + root.LoadSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        void Build()
        {
            canvas = UiFactory.CreateCanvas("LoadingOverlay", SortingOrder, transform);
            RectTransform rt = (RectTransform)canvas.transform;
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            // opaque backdrop (no rounded sprite: it covers the whole screen)
            RectTransform backdrop = UiFactory.Rect(rt, "Backdrop").Fill();
            backdrop.gameObject.AddComponent<Image>().color = GraphStyle.Background;

            RectTransform card = UiFactory.Rect(rt, "Card")
                .Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(CardWidth, 220));

            TextMeshProUGUI title = UiFactory.Text(card, "Title", "WHY", 64, GraphStyle.Text,
                TextAlignmentOptions.Center, FontStyles.Bold);
            title.characterSpacing = 28;
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(CardWidth, 80));

            TextMeshProUGUI subtitle = UiFactory.Text(card, "Subtitle", "From the Big Bang to this moment", 17,
                GraphStyle.TextDim, TextAlignmentOptions.Center);
            subtitle.characterSpacing = 4;
            subtitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86),
                new Vector2(CardWidth, 28));

            Image track = UiFactory.Rect(card, "Track")
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(BarWidth, 2))
                .gameObject.AddComponent<Image>();
            track.color = new Color(1, 1, 1, 0.08f);
            track.raycastTarget = false;

            barFill = UiFactory.Rect(track.transform, "Fill");
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0, 1);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            Image fill = barFill.gameObject.AddComponent<Image>();
            fill.color = new Color(1, 1, 1, 0.7f);
            fill.raycastTarget = false;

            status = UiFactory.Text(card, "Status", "", HudKit.SizeSmall, HudKit.TextFaint, TextAlignmentOptions.Center);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;
            status.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -154),
                new Vector2(CardWidth, 24));
            widthFitted = new[] { card, title.rectTransform, subtitle.rectTransform, status.rectTransform };
            FitWidth();
            FollowLoaderStatus();
        }

        /// <summary>The card and its lines at most the canvas width (less the HUD margins): a portrait canvas is narrower.</summary>
        void FitWidth()
        {
            fittedVersion = ScreenLayout.Version;
            float width = Mathf.Min(CardWidth, UiFactory.CanvasSize.x - 2 * HudKit.Margin);
            foreach (RectTransform r in widthFitted) r.sizeDelta = new Vector2(width, r.sizeDelta.y);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            if (fittedVersion != ScreenLayout.Version) FitWidth();
            if (loaded && doneTime < 0) doneTime = Time.unscaledTime;
            bool done = doneTime >= 0;

            float target = loaded ? 1f : root.LoadProgress;
            shownProgress = Mathf.Lerp(shownProgress, target, 1f - Mathf.Exp(-dt * 10f));
            barFill.anchorMax = new Vector2(shownProgress, 1);

            if (!done)
            {
                FollowLoaderStatus();
                return;
            }

            if (Time.unscaledTime - doneTime < HoldSeconds) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 0, dt / FadeSeconds);
            if (group.alpha > 0) return;

            canvas.gameObject.SetActive(false);
            enabled = false;
        }

        /// <summary>Show the loader's status in words, re-formatting only when it changes.</summary>
        void FollowLoaderStatus()
        {
            string raw = root.LoadStatus;
            if (raw == loaderStatus) return;
            loaderStatus = raw;
            status.text = Pretty(raw);
        }

        /// <summary>"Building LifeLayer, HumanWorldLoader" -> "Building life, human world".</summary>
        static string Pretty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return LayerName.Replace(raw, m => CamelHump.Replace(m.Groups[1].Value, " ").ToLowerInvariant());
        }
    }
}
