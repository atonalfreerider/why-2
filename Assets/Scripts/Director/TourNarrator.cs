using UnityEngine;

namespace Why.Director
{
    /// <summary>
    /// Speaks the guided tour: one recorded clip per stop (Resources/Audio/Tour/&lt;step id&gt;, written by
    /// Tools/generate-narration.ps1 and Tools/synthesize-narration.ps1 from Data/tour_narration.json).
    /// Clips are streamed and loaded on demand; a stop without a clip is silent and the tour reads its
    /// on-screen text at the usual pace.
    /// </summary>
    public sealed class TourNarrator
    {
        public static string ClipFolder => GraphScene.IsEconomy ? "Audio/EconomyTour/" : "Audio/Tour/";

        /// <summary>Seconds of silence after a clip before autoplay moves on.</summary>
        public const float PauseAfter = 1.5f;

        readonly AudioSource source;

        public TourNarrator(GameObject host, Camera camera)
        {
            // (a missing Unity component is a "fake null", so ?? would not fall through)
            if (!host.TryGetComponent(out source)) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;

            // the scene's camera is built in code and has no listener of its own
            if (camera != null && Object.FindAnyObjectByType<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
        }

        /// <summary>True while a clip is playing or paused mid-clip.</summary>
        public bool Speaking => source.clip != null && (source.isPlaying || paused);

        public bool Muted => source.mute;

        bool paused;

        /// <summary>The clip for a tour step, or null when none was recorded.</summary>
        public static AudioClip ClipFor(string stepId) =>
            string.IsNullOrEmpty(stepId) ? null : Resources.Load<AudioClip>(ClipFolder + stepId);

        public void Play(AudioClip clip)
        {
            Stop();
            if (clip == null) return;
            source.clip = clip;
            source.Play();
        }

        public void Stop()
        {
            paused = false;
            if (source.clip == null) return;
            source.Stop();
            source.clip = null;
        }

        public void Pause()
        {
            if (!source.isPlaying) return;
            source.Pause();
            paused = true;
        }

        public void Resume()
        {
            if (!paused) return;
            source.UnPause();
            paused = false;
        }

        public void ToggleMute() => source.mute = !source.mute;
    }
}
