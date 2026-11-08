using UnityEditor;
using UnityEngine;

namespace Why.EditorTools
{
    /// <summary>
    /// Import settings for the narration's pictures (Resources/Data/pictures, shown by UI.TourPictures): each keeps its own
    /// size (960 x 540, or whatever shape it has), since the card crops or letterboxes it into its 16:9 frame by the image's
    /// true aspect, which a power-of-two rescale (the importer's default for a 960 x 540 image is 1024 x 512) would change;
    /// mipmapped and trilinear, as a card draws it at a third of its size; clamped at its edges (a cropped image samples up to
    /// them); high-quality compression, as these are photographs looked at up close; not readable from scripts.
    /// </summary>
    sealed class PictureImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Data/pictures/";

        /// <summary>Raised when the settings change, so pictures imported before are imported again with them.</summary>
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
