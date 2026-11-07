using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;

namespace Why.Economy.UI
{
    /// <summary>
    /// Biographical picture popups and logos for the history stories (the user, 7 Nov 2026: "be sure to include corporate
    /// logos, biographical picture popups"): up to four cards down the left of the screen, each an image from the actors'
    /// atlas (<see cref="ActorImages"/>), a name, a line on who they were in this moment, and the image's credit. Cards
    /// slide in when a story's scene names them and fade when it moves on.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HistoryPopups : GraphModule
    {
        const float CardW = 330, CardH = 96, Image = 80, Gap = 10, Top = 176, Left = 24;
        const int MaxCards = 4;

        static HistoryPopups current;
        GraphRoot root;
        RectTransform canvasRoot;

        sealed class Card
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public RawImage Picture;
            public TextMeshProUGUI Monogram, Name, Line, Credit;
            public string Id, Last;
            public float Alpha, Target;
        }

        readonly List<Card> cards = new List<Card>();

        /// <summary>Shows these people or organisations (id, name, a line about them), replacing the cards shown; an empty list
        /// clears them.</summary>
        public static void Show(IList<(string id, string name, string line)> items)
        {
            if (current == null) return;
            current.Set(items);
        }

        public static void Clear() => Show(new List<(string, string, string)>());

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            current = this;
            Canvas canvas = UiFactory.CreateCanvas("History popups", 46, transform);
            canvasRoot = (RectTransform)canvas.transform;
            for (int i = 0; i < MaxCards; i++) cards.Add(MakeCard(i));
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        Card MakeCard(int i)
        {
            var panel = UiFactory.Panel(canvasRoot, "Card " + i, new Color(.02f, .03f, .05f, .9f), false);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(CardW, CardH);
            rect.anchoredPosition = new Vector2(Left, -(Top + i * (CardH + Gap)));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;

            var pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pic.transform.SetParent(rect, false);
            pic.raycastTarget = false;
            var pr = pic.rectTransform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, .5f);
            pr.sizeDelta = new Vector2(Image, Image);
            pr.anchoredPosition = new Vector2(8, 0);

            var mono = UiFactory.Text(rect, "Monogram", "", 30, new Color(.9f, .92f, .96f));
            mono.alignment = TextAlignmentOptions.Center;
            var mr = mono.rectTransform;
            mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0, .5f);
            mr.sizeDelta = new Vector2(Image, Image);
            mr.anchoredPosition = new Vector2(8, 0);

            var name = UiFactory.Text(rect, "Name", "", 15, GraphStyle.Text);
            name.fontStyle = FontStyles.Bold;
            Place(name.rectTransform, Image + 18, 10, CardW - Image - 26, 20);
            var line = UiFactory.Text(rect, "Line", "", 11.5f, new Color(.78f, .82f, .88f));
            line.textWrappingMode = TextWrappingModes.Normal;
            Place(line.rectTransform, Image + 18, 31, CardW - Image - 26, 40);
            var credit = UiFactory.Text(rect, "Credit", "", 8.5f, new Color(.55f, .58f, .64f));
            credit.textWrappingMode = TextWrappingModes.Normal;
            Place(credit.rectTransform, Image + 18, CardH - 22, CardW - Image - 26, 18);
            return new Card { Rect = rect, Group = group, Picture = pic, Monogram = mono, Name = name, Line = line, Credit = credit };
        }

        static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        void Set(IList<(string id, string name, string line)> items)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (items == null || i >= items.Count)
                {
                    card.Target = 0;
                    card.Id = null;
                    continue;
                }

                var (id, name, line) = items[i];
                // a new face slides in afresh; the same face back after a clear (a tour's next stop) fades back from where it was
                if (card.Id != id && card.Last != id) card.Alpha = 0;
                card.Id = card.Last = id;
                card.Target = 1;
                card.Name.text = name ?? id;
                card.Line.text = line ?? "";
                // a company whose wordmark is wider than its tile shows the wordmark (two cells wide), not its monogram
                bool wide = ActorImages.TryGet(id + "@wide", out var cell);
                if (wide || ActorImages.TryGet(id, out cell))
                {
                    card.Picture.texture = cell.Page;
                    card.Picture.uvRect = cell.Uv;
                    card.Picture.enabled = true;
                    card.Monogram.text = "";
                    card.Credit.text = cell.Credit ?? "";
                }
                else
                {
                    card.Picture.enabled = false;
                    card.Monogram.text = Monogram(name ?? id);
                    card.Credit.text = "";
                }

                Layout(card, wide);
            }
        }

        /// <summary>The picture square, or two to one for a wordmark; the text beside it.</summary>
        static void Layout(Card card, bool wide)
        {
            float w = wide ? Image * 1.6f : Image, h = wide ? Image * .8f : Image;
            card.Picture.rectTransform.sizeDelta = new Vector2(w, h);
            float x = w + 18, tw = CardW - w - 26;
            Place(card.Name.rectTransform, x, 10, tw, 20);
            Place(card.Line.rectTransform, x, 31, tw, 40);
            Place(card.Credit.rectTransform, x, CardH - 22, tw, 18);
        }

        static string Monogram(string name)
        {
            var parts = name.Split(' ');
            string m = "";
            foreach (var p in parts)
                if (p.Length > 0 && char.IsUpper(p[0]) && m.Length < 2) m += p[0];
            return m.Length > 0 ? m : name.Substring(0, 1).ToUpperInvariant();
        }

        void Update()
        {
            if (root == null || !root.IsLoaded) return;
            float dt = GraphTime.Delta;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.Alpha = Mathf.MoveTowards(card.Alpha, card.Target, dt * 2.5f);
                card.Group.alpha = Mathf.SmoothStep(0, 1, card.Alpha);
                // a card slides in from the left as it appears
                card.Rect.anchoredPosition = new Vector2(Left - 40 * (1 - card.Group.alpha), -(Top + i * (CardH + Gap)));
            }
        }
    }
}
