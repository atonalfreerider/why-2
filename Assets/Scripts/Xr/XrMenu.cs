using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Director;

namespace Why.Xr
{
    /// <summary>
    /// The headset menu, held just above the left palm while the palm faces your eyes. It is the guided tour's remote:
    ///
    /// * a header with the scene and the stop being narrated,
    /// * the transport: back, play / pause, next, leave,
    /// * one icon per chapter of the tour (its views, in tour order, colored by level: red matter, green life, blue humans)
    ///   - pinch one to play the narrated tour from where it first reaches that view; the view on screen is lit,
    /// * the other scene (universe / economy) and recenter.
    ///
    /// Point with the right hand and pinch (<see cref="XrRig"/> does the pointing; this only builds and refreshes).
    /// </summary>
    public sealed class XrMenu : MonoBehaviour
    {
        const float Width = 600f;
        const float Pad = 14f;
        const float IconCell = 108f;
        const int Columns = 5;
        const float MetresPerUnit = 0.0004f;

        GraphRoot root;
        Canvas canvas;
        RectTransform panel;
        TextMeshProUGUI stop;
        Image playIcon;
        readonly List<(Image ring, string preset)> chapterButtons = new List<(Image, string)>();
        int builtChapters = -1;

        public bool Hidden => canvas == null || !canvas.gameObject.activeSelf;

        public Canvas Canvas => canvas;

        public void Init(GraphRoot graphRoot, Transform palm)
        {
            root = graphRoot;
            canvas = UiFactory.CreateCanvas("XR Menu", 400);
            Transform t = canvas.transform;
            t.SetParent(palm, false);
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one * MetresPerUnit;
            ((RectTransform)t).pivot = new Vector2(0.5f, 0f); // stands on the palm
            panel = UiFactory.Panel(t, "Panel").rectTransform;
            panel.Fill();
            canvas.gameObject.SetActive(false);
        }

        public void SetShown(bool show)
        {
            if (canvas == null || show != Hidden) return;
            canvas.gameObject.SetActive(show);
            if (show) Refresh(true);
        }

        void LateUpdate()
        {
            if (Hidden) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                // face the eyes, upright
                Vector3 toMenu = canvas.transform.position - cam.transform.position;
                if (toMenu.sqrMagnitude > 1e-8f) canvas.transform.rotation = Quaternion.LookRotation(toMenu, Vector3.up);
            }

            Refresh(false);
        }

        /// <summary>Rebuilds when the chapters change (the tour loads, the scene changes); otherwise updates the state.</summary>
        void Refresh(bool force)
        {
            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            IReadOnlyList<TourChapter> chapters = director != null ? director.Chapters : System.Array.Empty<TourChapter>();
            if (force || chapters.Count != builtChapters) Build(director, chapters);
            if (director == null) return;

            bool running = director.TourRunning;
            string current = director.CurrentPresetId;
            if (playIcon != null) playIcon.sprite = ChapterIcons.Get(running && !director.Paused ? "pause" : "play");
            stop.text = StopText(director);
            foreach ((Image ring, string preset) in chapterButtons)
            {
                ring.color = running && preset == current ? new Color(1, 1, 1, 0.55f) : UiFactory.ButtonColor;
            }
        }

        static string StopText(DirectorMode director)
        {
            if (!director.TourRunning) return "Pinch a chapter to hear it, or play from the start";
            int step = director.CurrentStep;
            if (step < 0) return "The guided tour";
            if (director.AtEndCard) return GraphScene.IsEconomy ? "End of the tour" : "End of the universe: next continues into the economy";
            string id = director.CurrentPresetId;
            string chapter = director.Chapters.FirstOrDefault(c => c.PresetId == id).Title ?? "";
            return $"Stop {step + 1}  ·  {chapter}{(director.Paused ? "  ·  paused" : "")}";
        }

        void Build(DirectorMode director, IReadOnlyList<TourChapter> chapters)
        {
            foreach (Transform c in panel) Destroy(c.gameObject);
            chapterButtons.Clear();
            playIcon = null;
            builtChapters = chapters.Count;

            float y = -Pad;
            TextMeshProUGUI title = UiFactory.Text(panel, "Title", GraphScene.IsEconomy ? "The economy" : "Why: the universe", 26f,
                GraphStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, y), new Vector2(Width - 2 * Pad, 34));
            y -= 34;
            stop = UiFactory.Text(panel, "Stop", "", 17f, GraphStyle.TextDim, TextAlignmentOptions.Left);
            stop.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, y), new Vector2(Width - 2 * Pad, 24));
            y -= 34;

            // transport
            if (director != null)
            {
                float bw = (Width - 2 * Pad - 3 * 10) / 4f;
                string[] ids = { "back", "play", "next", "stop" };
                System.Action[] actions =
                {
                    () => { if (director.TourRunning) director.PreviousStep(); },
                    director.PlayPause,
                    () => { if (director.TourRunning) director.NextStep(); else director.StartTour(); },
                    director.ExitTour,
                };
                for (int i = 0; i < 4; i++)
                {
                    Image icon = IconButton(panel, ids[i], actions[i], new Vector2(Pad + i * (bw + 10), y), new Vector2(bw, 64), Color.white, null, out _);
                    if (ids[i] == "play") playIcon = icon;
                }

                y -= 64 + 14;
            }

            // chapters
            float cw = (Width - 2 * Pad) / Columns;
            for (int i = 0; i < chapters.Count; i++)
            {
                TourChapter c = chapters[i];
                int first = c.FirstStep;
                Vector2 at = new Vector2(Pad + i % Columns * cw, y - i / Columns * IconCell);
                IconButton(panel, c.PresetId, () => director.PlayFrom(first), at, new Vector2(cw - 6, IconCell - 6),
                    ChapterIcons.Tint(c.PresetId), c.Title, out Image ring);
                chapterButtons.Add((ring, c.PresetId));
            }

            y -= (chapters.Count + Columns - 1) / Columns * IconCell + 8;

            // the other scene, recenter
            float half = (Width - 2 * Pad - 10) / 2f;
            IconButton(panel, "scene", GraphRoot.OpenOtherScene, new Vector2(Pad, y), new Vector2(half, 56), Color.white,
                GraphScene.IsEconomy ? "The universe" : "The economy", out _);
            IconButton(panel, "recenter", () => XrRig.Instance?.Recenter(), new Vector2(Pad + half + 10, y), new Vector2(half, 56),
                Color.white, "Recenter", out _);
            y -= 56 + Pad;

            ((RectTransform)canvas.transform).sizeDelta = new Vector2(Width, -y);
        }

        /// <summary>A button with an icon (and an optional caption under or beside it); returns the icon image.</summary>
        static Image IconButton(RectTransform parent, string iconId, System.Action onClick, Vector2 topLeft, Vector2 size, Color tint,
            string caption, out Image background)
        {
            Button b = UiFactory.Button(parent, "Button " + iconId, "", 1f, onClick);
            RectTransform rt = (RectTransform)b.transform;
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), topLeft, size);
            background = b.GetComponent<Image>();

            bool tall = caption != null && size.y > 80;
            float iconSize = Mathf.Min(size.y - (tall ? 34 : 14), tall ? 62 : 40);
            RectTransform ir = UiFactory.Rect(rt, "Icon");
            Image icon = ir.gameObject.AddComponent<Image>();
            icon.sprite = ChapterIcons.Get(iconId);
            icon.color = tint;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            if (caption == null) ir.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            else if (tall) ir.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(iconSize, iconSize));
            else ir.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(iconSize, iconSize));

            if (caption != null)
            {
                TextMeshProUGUI t = UiFactory.Text(rt, "Caption", caption, tall ? 13f : 17f, GraphStyle.Text,
                    tall ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Left);
                t.textWrappingMode = TextWrappingModes.Normal;
                if (tall) t.rectTransform.Fill(4, 4, 4, iconSize + 8);
                else t.rectTransform.Fill(iconSize + 22, 0, 6, 0);
            }

            return icon;
        }
    }
}
