using UnityEngine;
using UnityEngine.UI;

namespace Why
{
    /// <summary>
    /// Keeps a canvas's scaler on the reference for the current orientation (see
    /// <see cref="UiFactory.ApplyLayout"/>). Runs before the scaler itself, so a canvas never draws a frame
    /// at the other orientation's scale; added to every canvas by <see cref="UiFactory.CreateCanvas"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasLayout : MonoBehaviour
    {
        CanvasScaler scaler;
        int appliedVersion = -1;

        void OnEnable() => Apply();

        void Update()
        {
            ScreenLayout.Refresh();
            if (appliedVersion != ScreenLayout.OrientationVersion) Apply();
        }

        void Apply()
        {
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            ScreenLayout.Refresh();
            UiFactory.ApplyLayout(scaler);
            appliedVersion = ScreenLayout.OrientationVersion;
        }
    }
}
