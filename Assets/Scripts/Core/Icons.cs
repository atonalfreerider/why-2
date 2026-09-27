using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// Procedural label icons (no image assets): the ids, and the atlas texture, sprites and material built
    /// from <see cref="IconRaster"/>. Id lookups and <see cref="Prepare"/> are thread safe; everything that
    /// touches Unity objects (<see cref="Texture"/>, <see cref="GetSprite"/>, <see cref="Material"/>) is main
    /// thread only and built on first use.
    /// </summary>
    /// <remarks>
    /// Rendering: icons are <see cref="SpriteRenderer"/>s with one shared material on the built-in
    /// <c>Sprites/Default</c> shader. That shader has no LightMode tag, so URP's Universal (forward) renderer
    /// draws it as an <c>SRPDefaultUnlit</c> pass in its transparent queue, exactly like the TextMeshPro
    /// shaders of the labels; it premultiplies in the fragment shader and blends One/OneMinusSrcAlpha, with
    /// ZWrite Off and Cull Off. It is in this project's Always Included Shaders (GraphicsSettings, built-in
    /// fileID 10753), so <see cref="Shader.Find"/> also finds it in player builds. The renderer's default
    /// sprite material is not relied on, because under URP it comes from the pipeline's 2D renderer data.
    /// </remarks>
    public static class Icons
    {
        /// <summary>The Big Bang / the whole universe: tapered rays of varied length bursting from a bright core.</summary>
        public const string Universe = "universe";

        /// <summary>Intergalactic gas: nodes joined by filaments.</summary>
        public const string CosmicWeb = "cosmic_web";

        /// <summary>First stars: a four-pointed sparkle and a small companion.</summary>
        public const string FirstStars = "first_stars";

        /// <summary>Laniakea: streamlines converging on one attractor.</summary>
        public const string Laniakea = "laniakea";

        /// <summary>Virgo Supercluster: a clump of dots, denser toward the centre.</summary>
        public const string Supercluster = "supercluster";

        /// <summary>Local Group: a larger and a smaller spiral with dwarf companions.</summary>
        public const string GalaxyGroup = "galaxy_group";

        /// <summary>Milky Way: a two-arm spiral with a bright bulge.</summary>
        public const string Galaxy = "galaxy";

        /// <summary>Solar nebula / protoplanetary disk: a young star in a tilted disk.</summary>
        public const string Nebula = "nebula";

        /// <summary>Sun: a disc with eight rays.</summary>
        public const string Sun = "sun";

        /// <summary>Other planets: a ringed planet.</summary>
        public const string Planets = "planets";

        /// <summary>Earth: a globe.</summary>
        public const string Earth = "earth";

        /// <summary>Moon: a crescent.</summary>
        public const string Moon = "moon";

        /// <summary>Sampling bias toward the sharper mip level; icons are small and a little crispness reads better.</summary>
        const float MipBias = -0.5f;

        /// <summary>The atlas mip chain expanded to RGBA32 texels (white, coverage in alpha), built off the main thread by <see cref="Prepare"/>.</summary>
        static readonly System.Lazy<byte[][]> texels = new System.Lazy<byte[][]>(ExpandTexels);

        static Texture2D texture;
        static Material material;
        static Sprite[] sprites;
        static int warming;

        /// <summary>True when the id names an icon. Thread safe.</summary>
        public static bool Has(string id) => IconRaster.IndexOf(id) >= 0;

        /// <summary>The icon of a matter item (the <c>id</c> in matter.json), or null when it has none. Thread safe.</summary>
        public static string ForMatter(string itemId)
        {
            switch (itemId)
            {
                case "universe": return Universe;
                case "intergalactic_gas": return CosmicWeb;
                case "first_stars": return FirstStars;
                case "laniakea": return Laniakea;
                case "virgo_supercluster": return Supercluster;
                case "local_group": return GalaxyGroup;
                case "milky_way": return Galaxy;
                case "solar_nebula": return Nebula;
                case "sun": return Sun;
                case "other_planets": return Planets;
                case "earth": return Earth;
                case "moon": return Moon;
                default: return null;
            }
        }

        /// <summary>
        /// Starts rasterizing the atlas pixels on the thread pool (once), so the main thread only uploads them.
        /// Thread safe and cheap to call repeatedly.
        /// </summary>
        public static void Prepare()
        {
            if (Interlocked.Exchange(ref warming, 1) == 0) Task.Run(() => texels.Value);
        }

        /// <summary>Normalized UV rect of an icon's cell in <see cref="Texture"/>, or <c>Rect.zero</c> for an unknown id. Thread safe.</summary>
        public static Rect Uv(string id)
        {
            int index = IconRaster.IndexOf(id);
            if (index < 0) return Rect.zero;
            IconRaster.CellOrigin(index, out int x, out int y);
            const float inv = 1f / IconRaster.AtlasPx;
            return new Rect(x * inv, y * inv, IconRaster.CellPx * inv, IconRaster.CellPx * inv);
        }

        /// <summary>
        /// The icon atlas: RGBA32 white with the glyph coverage in alpha, full mip chain (box filtered by
        /// <see cref="IconRaster"/>), trilinear, clamped, not CPU readable. Main thread only.
        /// </summary>
        public static Texture2D Texture
        {
            get
            {
                if (texture != null) return texture;
                byte[][] mips = texels.Value; // waits for Prepare() if it is still running
                Texture2D tex = new Texture2D(IconRaster.AtlasPx, IconRaster.AtlasPx, TextureFormat.RGBA32, mips.Length, true)
                {
                    name = "Icons",
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 1,
                    mipMapBias = MipBias
                };

                for (int level = 0; level < mips.Length; level++) tex.SetPixelData(mips[level], level);
                tex.Apply(false, true);
                texture = tex;
                sprites = null; // any sprites of a destroyed atlas are stale
                return texture;
            }
        }

        /// <summary>RGBA32 texels of every mip level: white, with the glyph coverage in alpha. Thread safe (pure C#).</summary>
        static byte[][] ExpandTexels()
        {
            byte[][] mips = IconRaster.Mips;
            byte[][] levels = new byte[mips.Length][];
            for (int level = 0; level < mips.Length; level++)
            {
                byte[] alpha = mips[level];
                byte[] rgba = new byte[alpha.Length * 4];
                for (int i = 0; i < alpha.Length; i++)
                {
                    int j = i * 4;
                    rgba[j] = 255;
                    rgba[j + 1] = 255;
                    rgba[j + 2] = 255;
                    rgba[j + 3] = alpha[i];
                }

                levels[level] = rgba;
            }

            return levels;
        }

        /// <summary>
        /// The sprite of an icon, or null for an unknown id. The sprite covers the whole cell (glyph plus the
        /// <see cref="IconRaster.Margin"/> border), is one unit square and pivots at its centre. Main thread only;
        /// created on first use and cached.
        /// </summary>
        public static Sprite GetSprite(string id)
        {
            int index = IconRaster.IndexOf(id);
            if (index < 0) return null;
            Texture2D tex = Texture;
            if (sprites == null) sprites = new Sprite[IconRaster.Ids.Length];
            Sprite sprite = sprites[index];
            if (sprite != null) return sprite;
            IconRaster.CellOrigin(index, out int x, out int y);
            sprite = Sprite.Create(tex, new Rect(x, y, IconRaster.CellPx, IconRaster.CellPx), new Vector2(0.5f, 0.5f),
                IconRaster.CellPx, 0, SpriteMeshType.FullRect);
            sprite.name = "Icon " + id;
            sprites[index] = sprite;
            return sprite;
        }

        /// <summary>
        /// Shared material for icon sprites (see the remarks on <see cref="Icons"/> for why this shader), or
        /// null if no suitable shader exists, in which case renderers keep their default. Main thread only.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("UI/Default"); // also always included, also alpha blended
                if (shader == null) return null;
                material = new Material(shader) { name = "Icons" };
                return material;
            }
        }
    }
}
