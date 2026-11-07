using UnityEditor;
using UnityEngine;

namespace Why.EditorTools
{
    /// <summary>
    /// Import settings for the narration clips (Resources/Audio/Tour, and Resources/Audio/EconomyTour with the history
    /// stories' 33 clips): mono, streamed from disk and never preloaded, so loading a step's clip costs nothing until it
    /// plays and the clips (about an hour of speech in all) never sit decoded in memory.
    /// </summary>
    sealed class NarrationImport : AssetPostprocessor
    {
        static readonly string[] Folders = { "Assets/Resources/Audio/Tour/", "Assets/Resources/Audio/EconomyTour/" };

        /// <summary>Raised when the folders change, so clips imported before are imported again with these settings.</summary>
        public override uint GetVersion() => 2;

        void OnPreprocessAudio()
        {
            if (!System.Array.Exists(Folders, f => assetPath.StartsWith(f))) return;
            AudioImporter importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
