using UnityEngine;

namespace Why.UI
{
    /// <summary>
    /// Eases a <see cref="CanvasGroup"/> toward a target alpha. The group takes input only while it is
    /// (being) shown, and its GameObject is deactivated once fully faded out so hidden UI costs nothing.
    /// </summary>
    public sealed class UiFade
    {
        readonly CanvasGroup group;
        readonly bool interactive;

        /// <summary>Alpha units per second.</summary>
        public float Speed;

        /// <summary>Alpha the group is easing toward.</summary>
        public float Target { get; private set; }

        /// <summary>True when the group is (becoming) visible.</summary>
        public bool Shown => Target > 0.5f;

        public float Alpha => group.alpha;

        public UiFade(GameObject go, float alpha, float speed, bool interactive)
        {
            group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            this.interactive = interactive;
            Speed = speed;
            Snap(alpha);
        }

        /// <summary>Fade toward fully shown or hidden.</summary>
        public void Show(bool show) => SetTarget(show ? 1f : 0f);

        public void SetTarget(float alpha)
        {
            Target = Mathf.Clamp01(alpha);
            if (Target > 0 && !group.gameObject.activeSelf) group.gameObject.SetActive(true);
            ApplyInput();
        }

        /// <summary>Jump to an alpha without easing.</summary>
        public void Snap(float alpha)
        {
            SetTarget(alpha);
            group.alpha = Target;
            Tick(0);
        }

        /// <summary>Restart a fade-in from transparent (e.g. when the content changes).</summary>
        public void Replay()
        {
            group.alpha = 0;
            Show(true);
        }

        public void Tick(float dt)
        {
            float a = Mathf.MoveTowards(group.alpha, Target, dt * Speed);
            if (!Mathf.Approximately(a, group.alpha)) group.alpha = a;
            if (a <= 0f && Target <= 0f && group.gameObject.activeSelf) group.gameObject.SetActive(false);
        }

        void ApplyInput()
        {
            bool input = interactive && Target > 0.5f;
            group.blocksRaycasts = input;
            group.interactable = input;
        }
    }
}
