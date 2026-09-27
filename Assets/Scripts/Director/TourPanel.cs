using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.Director
{
    /// <summary>What the narration panel shows.</summary>
    public sealed class PanelContent
    {
        /// <summary>Centered card typography (tour title and end cards) instead of a side popup.</summary>
        public bool Card;

        /// <summary>Small upper-case line above the title (step counter and section).</summary>
        public string Kicker;

        public string Title;
        public string Body;

        /// <summary>Small dim line under the body (cards only), e.g. the keyboard controls.</summary>
        public string Footnote;

        /// <summary>Reveal the body with a typewriter.</summary>
        public bool Typewriter;

        /// <summary>Back / play-pause / next buttons and a close button (steps).</summary>
        public bool Transport;

        /// <summary>Card buttons (right = primary, left = secondary); null labels hide them.</summary>
        public string PrimaryLabel, SecondaryLabel;

        public Action Primary, Secondary;
    }

    /// <summary>
    /// The narration popup: a rounded panel with a step counter, a bold title, the narration revealed by a
    /// typewriter, transport buttons and a thin autoplay progress bar. Content changes fade and slide the
    /// panel out and back in (CanvasGroup, smoothstep, unscaled time); the slot is chosen when the new
    /// content is laid out, so the panel lands on the side of the screen away from its target.
    /// </summary>
    public sealed class TourPanel
    {
        public const float StepWidth = 520f;
        public const float CardWidth = 640f;
        public const float CharactersPerSecond = 60f;

        const float StepPadding = 26f;
        const float CardPadding = 36f;
        const float KickerSize = 12.5f;
        const float StepTitleSize = 22f;
        const float CardTitleSize = 30f;
        const float BodySize = 17f;
        const float FootnoteSize = 13f;
        const float FooterHeight = 32f;
        const float InSeconds = 0.42f;
        const float OutSeconds = 0.2f;
        const float SlideDistance = 28f;
        const float TypeDelay = 0.12f;

        /// <summary>Height handed to TMP when measuring wrapped text.</summary>
        const float Unbounded = 32767f;

        static readonly Vector3[] Corners = new Vector3[4];

        enum Phase
        {
            Hidden,
            In,
            Shown,
            Out
        }

        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly TextMeshProUGUI kicker, title, body, footnote, status;
        readonly Button back, play, next, close, primary, secondary;
        readonly TextMeshProUGUI playLabel, primaryLabel, secondaryLabel;
        readonly RectTransform progressTrack, progressFill;
        readonly Image progressImage;

        Phase phase = Phase.Hidden;
        float visibility, typeClock;
        int totalCharacters;
        PanelContent content, pendingContent;
        Func<Vector2, PanelSlot> pendingPlacement;
        PanelSlot slot = PanelSlot.Center;

        /// <summary>Builds the panel (hidden) under a canvas. Callbacks are the transport buttons.</summary>
        public TourPanel(Transform parent, Action onBack, Action onPlayPause, Action onNext, Action onClose)
        {
            root = UiFactory.Rect(parent, "NarrationPanel");
            group = root.gameObject.AddComponent<CanvasGroup>();

            // a soft drop shadow extending past the panel (a little further below it)
            RectTransform shadowRect = UiFactory.Rect(root, "Shadow").Fill(-30, -38, -30, -24);
            Image shadowImage = shadowRect.gameObject.AddComponent<Image>();
            shadowImage.sprite = ShadowSprite;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0, 0, 0, 0.55f);
            shadowImage.raycastTarget = false;

            UiFactory.Panel(root, "Border", UiFactory.PanelBorder, false).rectTransform.Fill();
            Image fill = UiFactory.Panel(root, "Fill", UiFactory.PanelColor);
            fill.rectTransform.Fill(1, 1, 1, 1);
            fill.gameObject.AddComponent<PanelClick>().Clicked = CompleteTyping;

            Color bodyColor = Color.Lerp(GraphStyle.TextDim, GraphStyle.Text, 0.8f);
            kicker = Label("Kicker", KickerSize, GraphStyle.TextDim, FontStyles.UpperCase);
            kicker.characterSpacing = 6;
            kicker.textWrappingMode = TextWrappingModes.NoWrap;
            kicker.overflowMode = TextOverflowModes.Ellipsis;
            title = Label("Title", StepTitleSize, GraphStyle.Text, FontStyles.Bold);
            body = Label("Body", BodySize, bodyColor, FontStyles.Normal);
            body.lineSpacing = 8;
            footnote = Label("Footnote", FootnoteSize, GraphStyle.TextDim, FontStyles.Normal);
            status = Label("Status", KickerSize, GraphStyle.TextDim, FontStyles.Normal);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;

            back = FooterButton("Back", "\u2039 Back", 15, onBack, out _);
            play = FooterButton("PlayPause", "Pause", 15, onPlayPause, out playLabel);
            next = FooterButton("Next", "Next \u203A", 15, onNext, out _);
            close = FooterButton("Close", "\u00D7", 20, onClose, out _);
            secondary = FooterButton("Secondary", "", 15, () => content?.Secondary?.Invoke(), out secondaryLabel);
            primary = FooterButton("Primary", "", 15, () => content?.Primary?.Invoke(), out primaryLabel);
            primary.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            progressTrack = UiFactory.Rect(root, "Progress");
            Image track = progressTrack.gameObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.07f);
            track.raycastTarget = false;
            progressFill = UiFactory.Rect(progressTrack, "Fill");
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0, 1);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            progressImage = progressFill.gameObject.AddComponent<Image>();
            progressImage.raycastTarget = false;

            root.gameObject.SetActive(false);
        }

        /// <summary>0..1 eased visibility (the arrow fades with the panel).</summary>
        public float Visibility => Smooth(visibility);

        public bool IsHidden => phase == Phase.Hidden;

        /// <summary>Fully shown and not transitioning.</summary>
        public bool IsSettled => phase == Phase.Shown;

        /// <summary>True while showing (or entering) and the content is still being typed.</summary>
        public bool IsTyping =>
            (phase == Phase.In || phase == Phase.Shown) && content != null && content.Typewriter &&
            VisibleCharacters < totalCharacters;

        int VisibleCharacters => Mathf.Max(0, Mathf.FloorToInt(typeClock * CharactersPerSecond));

        /// <summary>
        /// Shows new content. If something is shown it slides out first. placement receives the laid out
        /// panel size and returns the slot to show it in.
        /// </summary>
        public void Present(PanelContent newContent, Func<Vector2, PanelSlot> placement)
        {
            pendingContent = newContent;
            pendingPlacement = placement;
            if (phase == Phase.Hidden) ApplyPending();
            else phase = Phase.Out;
        }

        /// <summary>Moves the current content to another slot (out and back in), keeping the typewriter.</summary>
        public void Relocate(Func<Vector2, PanelSlot> placement)
        {
            if (content == null || phase == Phase.Hidden) return;
            Present(content, placement);
        }

        /// <summary>Slides the panel out and deactivates it.</summary>
        public void Hide()
        {
            pendingContent = null;
            pendingPlacement = null;
            if (phase != Phase.Hidden) phase = Phase.Out;
        }

        /// <summary>Reveals the whole text at once.</summary>
        public void CompleteTyping() => typeClock = Mathf.Max(typeClock, (totalCharacters + 1) / CharactersPerSecond);

        /// <summary>Autoplay progress (0..1); a negative value hides the bar. Dimmer while paused.</summary>
        public void SetProgress(float value, bool running)
        {
            bool shown = value >= 0;
            if (progressTrack.gameObject.activeSelf != shown) progressTrack.gameObject.SetActive(shown);
            if (!shown) return;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            progressImage.color = new Color(1f, 1f, 1f, running ? 0.55f : 0.22f);
        }

        /// <summary>Play/pause button label and the status line in the footer.</summary>
        public void SetTransport(bool playing, string statusText)
        {
            playLabel.text = playing ? "Pause" : "Play";
            status.text = statusText;
        }

        /// <summary>The panel's current rectangle in canvas units (origin bottom-left).</summary>
        public Rect CanvasRect(float scaleFactor)
        {
            // overlay canvas: world corners are screen pixels
            root.GetWorldCorners(Corners);
            float k = 1f / Mathf.Max(scaleFactor, 1e-4f);
            return Rect.MinMaxRect(Corners[0].x * k, Corners[0].y * k, Corners[2].x * k, Corners[2].y * k);
        }

        /// <summary>Advances transitions and the typewriter (unscaled time).</summary>
        public void Tick(float dt)
        {
            switch (phase)
            {
                case Phase.In:
                    visibility = Mathf.Min(1, visibility + dt / InSeconds);
                    if (visibility >= 1) phase = Phase.Shown;
                    break;
                case Phase.Out:
                    visibility = Mathf.Max(0, visibility - dt / OutSeconds);
                    if (visibility <= 0)
                    {
                        if (pendingContent != null) ApplyPending();
                        else
                        {
                            phase = Phase.Hidden;
                            content = null;
                            root.gameObject.SetActive(false);
                        }
                    }

                    break;
            }

            if (phase == Phase.Hidden) return;

            if (content != null && content.Typewriter && (phase == Phase.In || phase == Phase.Shown))
            {
                typeClock += dt;
                int visible = Mathf.Min(VisibleCharacters, totalCharacters);
                if (body.maxVisibleCharacters != visible) body.maxVisibleCharacters = visible;
            }

            float e = Smooth(visibility);
            group.alpha = e;
            group.interactable = group.blocksRaycasts = phase != Phase.Out;
            root.anchoredPosition = PanelPlacement.SlotOffset(slot) +
                                    PanelPlacement.SlideDirection(slot) * (SlideDistance * (1 - e));
            float scale = 0.985f + 0.015f * e;
            root.localScale = new Vector3(scale, scale, 1);
        }

        void ApplyPending()
        {
            PanelContent c = pendingContent;
            Func<Vector2, PanelSlot> placement = pendingPlacement;
            pendingContent = null;
            pendingPlacement = null;
            if (c == null) return;

            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            bool changed = !ReferenceEquals(c, content);
            content = c;
            Vector2 size = Layout(c);
            if (changed)
            {
                typeClock = c.Typewriter ? -TypeDelay : 0;
                totalCharacters = CountCharacters();
                body.maxVisibleCharacters = c.Typewriter ? 0 : 99999;
            }

            slot = placement != null ? placement(size) : PanelSlot.Center;
            Vector2 anchor = PanelPlacement.SlotAnchor(slot);
            root.anchorMin = root.anchorMax = anchor;
            root.pivot = anchor;
            root.sizeDelta = size;
            visibility = 0;
            phase = Phase.In;
            Tick(0);
        }

        /// <summary>Positions every element for the content (top-down) and returns the panel size.</summary>
        Vector2 Layout(PanelContent c)
        {
            float width = c.Card ? CardWidth : StepWidth;
            float pad = c.Card ? CardPadding : StepPadding;
            float inner = width - 2 * pad;
            TextAlignmentOptions align = c.Card ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
            float y = pad;

            bool hasKicker = !string.IsNullOrEmpty(c.Kicker);
            kicker.gameObject.SetActive(hasKicker);
            if (hasKicker)
            {
                kicker.text = c.Kicker;
                kicker.alignment = align;
                float kickerWidth = c.Transport ? inner - 36 : inner;
                Put(kicker.rectTransform, pad, y, kickerWidth, 16);
                y += 16 + (c.Card ? 14 : 10);
            }

            title.text = c.Title ?? "";
            title.fontSize = c.Card ? CardTitleSize : StepTitleSize;
            title.alignment = align;
            float titleHeight = title.GetPreferredValues(title.text, inner, Unbounded).y;
            Put(title.rectTransform, pad, y, inner, titleHeight);
            y += titleHeight + 12;

            bool hasBody = !string.IsNullOrEmpty(c.Body);
            body.gameObject.SetActive(hasBody);
            if (hasBody)
            {
                body.text = c.Body;
                body.alignment = align;
                float bodyHeight = body.GetPreferredValues(body.text, inner, Unbounded).y;
                Put(body.rectTransform, pad, y, inner, bodyHeight);
                y += bodyHeight + 12;
            }

            bool hasFootnote = !string.IsNullOrEmpty(c.Footnote);
            footnote.gameObject.SetActive(hasFootnote);
            if (hasFootnote)
            {
                footnote.text = c.Footnote;
                footnote.alignment = align;
                float footHeight = footnote.GetPreferredValues(footnote.text, inner, Unbounded).y;
                Put(footnote.rectTransform, pad, y + 2, inner, footHeight);
                y += footHeight + 12;
            }

            y += c.Card ? 10 : 6;
            LayoutFooter(c, width, pad, y);
            y += FooterHeight + pad - 4;

            progressTrack.anchorMin = progressTrack.anchorMax = Vector2.zero;
            progressTrack.pivot = Vector2.zero;
            progressTrack.anchoredPosition = new Vector2(pad, 12);
            progressTrack.sizeDelta = new Vector2(inner, 2);
            return new Vector2(width, Mathf.Ceil(y));
        }

        void LayoutFooter(PanelContent c, float width, float pad, float y)
        {
            back.gameObject.SetActive(c.Transport);
            play.gameObject.SetActive(c.Transport);
            next.gameObject.SetActive(c.Transport);
            close.gameObject.SetActive(c.Transport);
            status.gameObject.SetActive(c.Transport);
            bool hasPrimary = !c.Transport && !string.IsNullOrEmpty(c.PrimaryLabel);
            bool hasSecondary = !c.Transport && !string.IsNullOrEmpty(c.SecondaryLabel);
            primary.gameObject.SetActive(hasPrimary);
            secondary.gameObject.SetActive(hasSecondary);

            if (c.Transport)
            {
                const float nextWidth = 92, smallWidth = 80, gap = 8;
                float x = width - pad - nextWidth;
                Put(next.transform as RectTransform, x, y, nextWidth, FooterHeight);
                x -= gap + smallWidth;
                Put(play.transform as RectTransform, x, y, smallWidth, FooterHeight);
                x -= gap + smallWidth;
                Put(back.transform as RectTransform, x, y, smallWidth, FooterHeight);
                Put(status.rectTransform, pad, y, x - pad - 10, FooterHeight);
                status.alignment = TextAlignmentOptions.MidlineLeft;
                Put(close.transform as RectTransform, width - pad - 22, pad - 12, 30, 30);
                return;
            }

            const float cardButtonWidth = 150, cardGap = 12;
            primaryLabel.text = c.PrimaryLabel ?? "";
            secondaryLabel.text = c.SecondaryLabel ?? "";
            int count = (hasPrimary ? 1 : 0) + (hasSecondary ? 1 : 0);
            float total = count * cardButtonWidth + (count - 1) * cardGap;
            float left = (width - total) * 0.5f;
            if (hasSecondary)
            {
                Put(secondary.transform as RectTransform, left, y, cardButtonWidth, FooterHeight);
                left += cardButtonWidth + cardGap;
            }

            if (hasPrimary) Put(primary.transform as RectTransform, left, y, cardButtonWidth, FooterHeight);
        }

        int CountCharacters()
        {
            if (!body.gameObject.activeSelf || string.IsNullOrEmpty(body.text)) return 0;
            body.maxVisibleCharacters = 99999;
            body.ForceMeshUpdate();
            return body.textInfo.characterCount;
        }

        TextMeshProUGUI Label(string name, float size, Color color, FontStyles style)
        {
            TextMeshProUGUI t = UiFactory.Text(root, name, "", size, color, TextAlignmentOptions.TopLeft, style);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        Button FooterButton(string name, string text, float fontSize, Action onClick, out TextMeshProUGUI label)
        {
            Button b = UiFactory.Button(root, name, text, fontSize, onClick);
            // keyboard navigation would fight the tour's own keys (Space / Enter / arrows)
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            label = b.GetComponentInChildren<TextMeshProUGUI>();
            return b;
        }

        /// <summary>Places a child by its top-left corner, measured down from the panel's top-left.</summary>
        static void Put(RectTransform rt, float x, float yFromTop, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(width, height);
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        static Sprite shadowSprite;

        /// <summary>A soft, 9-sliced drop shadow (a rounded rectangle blurred outward), generated once.</summary>
        static Sprite ShadowSprite
        {
            get
            {
                if (shadowSprite != null) return shadowSprite;
                const int size = 64;
                const float soft = 26f;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "WhyPanelShadow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                Color32[] px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(soft - x - 0.5f, x + 0.5f - (size - soft)));
                        float dy = Mathf.Max(0, Mathf.Max(soft - y - 0.5f, y + 0.5f - (size - soft)));
                        float k = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / soft);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * k * k * (3 - 2 * k)));
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(soft, soft, soft, soft));
                return shadowSprite;
            }
        }
    }
}
