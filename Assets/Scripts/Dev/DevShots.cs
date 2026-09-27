using UnityEngine;

namespace Why.Dev
{
    /// <summary>
    /// Playtest helper for automated screenshots: jump to a preset (or pose) instantly and report when
    /// the view has settled. Used from the Editor via eval + wait_for(DevShots.Ready).
    /// </summary>
    public static class DevShots
    {
        static float readyAt = float.MaxValue;

        public static bool Ready =>
            Time.realtimeSinceStartup >= readyAt && !GraphWarp.Animating &&
            GraphRoot.Instance != null && GraphRoot.Instance.IsLoaded && !GraphRoot.Instance.Rig.Flying;

        public static string Focus(string presetId, float settleSeconds = 0.6f)
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null || !root.IsLoaded) return "not loaded";
            root.Focus(presetId, 0);
            readyAt = Time.realtimeSinceStartup + settleSeconds;
            return presetId;
        }

        /// <summary>Animated focus (to review transitions); Ready turns true when it completes.</summary>
        public static string FocusAnimated(string presetId, float seconds = 2.2f)
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null || !root.IsLoaded) return "not loaded";
            root.Focus(presetId, seconds);
            readyAt = Time.realtimeSinceStartup + seconds + 0.3f;
            return presetId;
        }

        public static string Pose(Vector3 target, float yaw, float pitch, float distance, float settleSeconds = 0.6f)
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) return "no root";
            root.Rig.SetPose(new CameraPose { Target = target, Yaw = yaw, Pitch = pitch, Distance = distance });
            readyAt = Time.realtimeSinceStartup + settleSeconds;
            return "ok";
        }

        /// <summary>Point the camera at an anchor with the current warp.</summary>
        public static string LookAt(string anchorKey, float distance, float pitch = 55, float settleSeconds = 0.6f)
        {
            if (!Anchors.TryGet(anchorKey, out Anchor a)) return "no anchor " + anchorKey;
            GraphRoot root = GraphRoot.Instance;
            CameraPose p = root.Rig.Pose;
            p.Target = a.World;
            p.Distance = distance;
            p.Pitch = pitch;
            root.Rig.SetPose(p);
            readyAt = Time.realtimeSinceStartup + settleSeconds;
            return a.Label;
        }

        public static void Wait(float seconds) => readyAt = Time.realtimeSinceStartup + seconds;
    }
}
