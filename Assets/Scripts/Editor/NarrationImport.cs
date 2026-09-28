using UnityEditor;
using UnityEngine;

namespace Why.EditorTools
{
    /// <summary>
    /// Import settings for the tour narration clips (Resources/Audio/Tour): mono, streamed from disk and
    /// never preloaded, so loading a step's clip costs nothing until it plays and the 63 clips (about half
    /// an hour of speech) never sit decoded in memory.
    /// </summary>
    sealed class NarrationImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Audio/Tour/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
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
