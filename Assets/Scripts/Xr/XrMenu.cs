using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Why.Director;
using UnityEngine.UI;

namespace Why.Xr
{
    /// <summary>
    /// The headset menu, held above the left controller (X shows and hides it): the numbered views of the current
    /// graph, the guided tour, the other scene (universe / economy) and recenter. Point with the right controller and
    /// pull the trigger. It is the headset's stand-in for the number keys and the HUD's preset bar.
    /// </summary>
    public sealed class XrMenu : MonoBehaviour
    {
        const float Width = 520f;
        const float ButtonHeight = 46f;
        const float Gap = 8f;
        const float MetresPerUnit = 0.00045f;

        GraphRoot root;
        Transform hand;
        Canvas canvas;
        RectTransform panel;
        readonly List<GameObject> buttons = new List<GameObject>();
        int builtVersion = -1;

        public bool Hidden => canvas == null || !canvas.gameObject.activeSelf;

        public void Init(GraphRoot graphRoot, Transform leftHand)
        {
            root = graphRoot;
            hand = leftHand;
            canvas = UiFactory.CreateCanvas("XR Menu", 400);
            Transform t = canvas.transform;
            t.SetParent(hand, false);
            t.localPosition = new Vector3(0, 0.14f, 0.04f);
            t.localScale = Vector3.one * MetresPerUnit;
            ((RectTransform)t).sizeDelta = new Vector2(Width, 600);
            panel = UiFactory.Panel(t, "Panel").rectTransform;
            panel.Fill();
            canvas.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (canvas == null) return;
            bool show = Hidden;
            if (show) Rebuild();
            canvas.gameObject.SetActive(show);
        }

        void LateUpdate()
        {
            if (Hidden) return;
            // face the eyes
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 toMenu = canvas.transform.position - cam.transform.position;
            if (toMenu.sqrMagnitude > 1e-8f) canvas.transform.rotation = Quaternion.LookRotation(toMenu, cam.transform.up);
            if (builtVersion != ViewPresets.All.Count) Rebuild();
        }

        void Rebuild()
        {
            foreach (GameObject b in buttons) Destroy(b);
            buttons.Clear();
            builtVersion = ViewPresets.All.Count;

            List<(string label, System.Action action)> items = new List<(string label, System.Action action)>();
            foreach (ViewPreset p in ViewPresets.All.Where(p => p.Key != KeyCode.None))
            {
                ViewPreset preset = p;
                items.Add((preset.Title, (System.Action)(() => root.Focus(preset))));
            }

            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (director != null)
            {
                items.Add(("Guided tour  (A next, B back)", (System.Action)(() =>
                {
                    if (director.TourRunning) director.ExitTour();
                    else director.StartTour();
                    Toggle();
                })));
            }

            items.Add((GraphScene.IsEconomy ? "Open the universe" : "Open the economy", (System.Action)GraphRoot.OpenOtherScene));
            items.Add(("Recenter  (left stick click)", (System.Action)(() => XrRig.Instance?.Recenter())));

            int columns = 2;
            float colWidth = (Width - Gap * (columns + 1)) / columns;
            int rows = (items.Count + columns - 1) / columns;
            float height = Gap + rows * (ButtonHeight + Gap) + 40f;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(Width, height);

            TextMeshProUGUI title = UiFactory.Text(panel, "Title", GraphScene.IsEconomy ? "The economy" : "The universe",
                22f, GraphStyle.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(Width, 32));
            buttons.Add(title.gameObject);

            for (int i = 0; i < items.Count; i++)
            {
                int row = i / columns, col = i % columns;
                (string label, System.Action action) = items[i];
                Button b = UiFactory.Button(panel, "Item", label, 17f, action);
                RectTransform rt = (RectTransform)b.transform;
                rt.Place(new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(Gap + col * (colWidth + Gap), -40f - row * (ButtonHeight + Gap)),
                    new Vector2(colWidth, ButtonHeight));
                buttons.Add(b.gameObject);
            }
        }
    }
}
