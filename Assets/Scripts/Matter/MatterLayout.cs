using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Matter
{
    /// <summary>
    /// One radial band of the matter stack: a body of matter (or, for the root, the patch of the early
    /// universe that became our galaxy) that eases in when it forms and out when it ends. A band's region
    /// contains its children's regions; the band itself draws only the part its children do not cover.
    /// Times are kept in years and in clock arcs (u, 1 = Big Bang, 0 = now).
    /// </summary>
    public sealed class MatterBand
    {
        /// <summary>The item this band draws (the root item for <see cref="IsRootHome"/>).</summary>
        public MatterItem Item;

        /// <summary>
        /// The root's inner band: the part of the young universe that became our galaxy. It is our "home"
        /// until the galaxy forms, then flows into it (the rest of the universe is the outer envelope).
        /// </summary>
        public bool IsRootHome;

        /// <summary>Position in the stack, 0 = innermost.</summary>
        public int Index;

        /// <summary>Highlight id (<see cref="GraphIds.Matter"/>); a band's subtree is a contiguous range.</summary>
        public int Id;

        /// <summary>Last id of this band's subtree (the band and everything that formed out of it).</summary>
        public int LastId;

        /// <summary>Stack index of the parent band, -1 for bands directly inside the universe.</summary>
        public int Parent = -1;

        /// <summary>Stack indices of the child bands.</summary>
        public readonly List<int> Children = new List<int>();

        /// <summary>Nominal width in data units (log mass), before the early-universe cap.</summary>
        public float Width;

        /// <summary>Depth along our lineage (0 = root, 1 = galaxy ...), or -1 when not on our path.</summary>
        public int PathDepth = -1;

        /// <summary>The innermost path item that still exists (Earth): it stays lit up to the human branch.</summary>
        public bool IsHomeNow;

        /// <summary>Formation starts / completes, decay starts / completes (years ago; EndYa 0 = extant).</summary>
        public double StartYa, FullYa, DecayYa, EndYa;

        /// <summary>The same moments as clock arcs.</summary>
        public float UStart, UFull, UDecay, UEnd;

        /// <summary>
        /// Arc where the band's group branches off: the start of its outermost ancestor that formed together
        /// with it (the solar nebula for the Sun, planets, Earth and Moon). The group opens gradually from here,
        /// over <see cref="BranchLength"/> of arc.
        /// </summary>
        public float UBranch, BranchLength = MatterLayout.BranchArc;

        /// <summary>
        /// Mean density (kg/m^3) of the band's own region: its matter minus what formed out of it, over its
        /// volume minus theirs (the space between the stars for the Milky Way). 0 when the data has no size.
        /// </summary>
        public double OwnDensity;

        // style (alpha and HDR intensity only: the hue is always the matter red)
        public float FillAlpha, FillIntensity, Noise;
        public float LineAlpha, LineIntensity, LineWidthPx;

        /// <summary>
        /// How early the band dissolves ahead of the human branch, on top of the materials' handoff fade
        /// (see <see cref="MatterLayout.HandoffBias"/>): 0 = our home (Earth) stays lit up to 3 o'clock,
        /// larger = the bulk of matter is gone sooner.
        /// </summary>
        public float HandoffExponent;

        /// <summary>True for our lineage (universe, galaxy, solar nebula, Sun, Earth).</summary>
        public bool InPath => PathDepth >= 0;

        /// <summary>True when the band still exists today.</summary>
        public bool Extant => EndYa <= 0;

        /// <summary>Highlight ids of the band and everything that formed out of it.</summary>
        public IdRange Ids => new IdRange(Id, LastId);

        /// <summary>True while the band exists at arc u (including its transitions).</summary>
        public bool AliveAt(float u) => u <= UStart && (Extant || u >= UEnd);

        /// <summary>Width factor at arc u: 0 before formation, S-curve up to 1, S-curve down to 0 at the end.</summary>
        public float Ramp(float u)
        {
            if (u >= UStart) return 0;
            float rise = u <= UFull ? 1 : Smooth((UStart - u) / Mathf.Max(UStart - UFull, 1e-9f));
            if (Extant || u >= UDecay) return rise;
            if (u <= UEnd) return 0;
            return rise * (1 - Smooth((UDecay - u) / Mathf.Max(UDecay - UEnd, 1e-9f)));
        }

        /// <summary>
        /// Opening of the band's group at arc u (0..1 over <see cref="BranchLength"/> after <see cref="UBranch"/>):
        /// at the scale of the expanded universe a group that forms within a few million years would otherwise
        /// open as a radial wall; this way it splits off like a root.
        /// </summary>
        public float Branch(float u) => Smooth((UBranch - u) / BranchLength);

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>
    /// Lays out the RED layer in data space. At every moment the bodies of matter are nested radially: our
    /// current home innermost (universe, then galaxy, solar nebula, Sun, Earth), each region holding the
    /// matter that formed out of it, so other matter splits off outward along the way without disturbing
    /// the bands outside. Beyond the stack the rest of the universe (the intergalactic gas, once it forms)
    /// fills an envelope that expands exponentially with time since the Big Bang.
    /// The layer runs from the Big Bang (6 o'clock) clockwise to the 3 o'clock handoff, where the clock
    /// becomes the straight human branch: matter has dissolved by then, so nothing is laid out beyond it.
    /// Pure math on data structs: safe on a worker thread and usable outside Unity.
    /// </summary>
    public sealed class MatterLayout
    {
        // --- widths: log10(mass) above a floor ---
        /// <summary>Band width (data units) per decade of mass above <see cref="FloorDex"/>.</summary>
        public const float WidthPerDecade = 0.02f;

        /// <summary>log10 of the mass (kg) that maps to zero width.</summary>
        public const float FloorDex = 22f;

        /// <summary>Narrowest nominal band (tiny or massless items stay visible).</summary>
        public const float MinWidth = 0.01f;

        // --- formation / decay transitions ---
        /// <summary>A transition lasts at most this fraction of the item's age at the event...</summary>
        const double TransitionAgeFraction = 0.03;

        /// <summary>...and at most this fraction of the item's lifetime.</summary>
        const double TransitionLifeFraction = 0.4;

        /// <summary>
        /// Bands that form within this arc of their parent belong to its group. A group straight out of the
        /// universe opens over this arc: the flare of the young universe (see <see cref="MatterBand.Branch"/>).
        /// </summary>
        public const float BranchArc = 0.1f;

        /// <summary>
        /// A group that forms inside an older body (the solar system inside the Milky Way) opens over this much
        /// longer arc (4.6 billion to about 20 million years ago), so it splits off like a root at a gentle angle
        /// instead of pushing a wall through the expanded universe.
        /// </summary>
        public const float NestedBranchArc = 0.25f;

        /// <summary>Every region keeps at least this fraction of its nominal width for itself.</summary>
        public const float MinOwnFraction = 0.4f;

        /// <summary>A child of the root holding at least this fraction of all matter fills the envelope.</summary>
        const double EnvelopeMassFraction = 0.5;

        // --- envelope E(u): a fast initial burst, then the real expansion of space (the cosmic scale factor),
        // which does most of its growth in the first few billion years (6 to 12 o'clock), and a tail that
        // keeps the fan opening toward the present ---
        /// <summary>Width the envelope reaches in the initial burst: inflation, the scale exploding open at the cusp.</summary>
        public const float BurstWidth = 3f;

        /// <summary>Arc over which the initial burst happens.</summary>
        public const float BurstTau = 0.02f;

        /// <summary>Envelope width today from the expansion of space alone (scale factor a = 1).</summary>
        public const float CosmicWidth = 24f;

        // flat Lambda-CDM (Planck 2018): dark-energy fraction and Hubble time 1/H0 in years (the matter
        // fraction only sets a constant prefactor, which the normalization to a = 1 today cancels)
        const double OmegaL = 0.69, HubbleTimeYears = 14.4e9;

        /// <summary>The tail grows by GrowthScale * (exp(GrowthRate * tau) - 1), tau = arc since the Big Bang.</summary>
        public const float GrowthScale = 0.25f;

        /// <summary>Exponential growth rate of the tail per unit of arc since the Big Bang.</summary>
        public const float GrowthRate = 6.4f;

        /// <summary>The stacked bands may use at most this fraction of the envelope (early universe).</summary>
        public const float CapFraction = 0.7f;

        // --- the rest of the universe: radial strips that thin out and break up outward ---
        /// <summary>
        /// Number of radial strips the envelope is drawn with (alpha falls and noise rises outward). A strip
        /// blends into the next, so the whole fan fades smoothly to transparent black.
        /// </summary>
        public const int EnvelopeStrips = 16;
        const float EnvelopeAlpha0 = 0.5f;
        const float EnvelopeAlphaWidth = 3f;

        /// <summary><see cref="HandoffBias"/> exponent of the envelope: the expanding universe dissolves first.</summary>
        public const float EnvelopeHandoffExponent = 1f;

        // --- sampling ---
        /// <summary>Base arc step: dense enough to bend smoothly around the ring.</summary>
        public const float ArcStep = 0.002f;

        const int TransitionSamples = 16;

        /// <summary>
        /// Arc where the red layer ends: the 3 o'clock handoff to the straight human branch. The materials'
        /// handoff fade is exactly 0 from there on, so geometry beyond it would only cost fill rate.
        /// </summary>
        public static float EndArc => Mathf.Max(DeepTime.NowArc, GraphWarp.BasePath.HandoffArc);

        /// <summary>Stacked bands, innermost first.</summary>
        public readonly List<MatterBand> Bands = new List<MatterBand>();

        /// <summary>Our lineage from the root (universe) to our home today (Earth).</summary>
        public readonly List<MatterItem> Path = new List<MatterItem>();

        /// <summary>The whole universe: drawn as the Big Bang, the envelope and (early on) its home band.</summary>
        public MatterItem Root { get; private set; }

        /// <summary>The universe's home band: our lineage before the galaxy forms.</summary>
        public MatterBand RootHome { get; private set; }

        /// <summary>The root's child that fills the envelope once it forms (the intergalactic gas), or null.</summary>
        public MatterItem EnvelopeItem { get; private set; }

        /// <summary>Years ago of the Big Bang (the root's start), where every band begins.</summary>
        public double BigBangYa { get; private set; }

        /// <summary>Clock arc of the Big Bang (just short of 1: the clock's own origin is slightly older).</summary>
        public float BigBangArc { get; private set; }

        /// <summary>Arc at which the envelope starts to belong to <see cref="EnvelopeItem"/>.</summary>
        public float EnvelopeItemArc { get; private set; }

        /// <summary>Id of the Big Bang burst (and of the epochs the clock cannot separate from it).</summary>
        public int BurstId => GraphIds.Matter(0);

        /// <summary>Id of the envelope before <see cref="EnvelopeItem"/> forms (the universe itself).</summary>
        public int UniverseEnvelopeId => GraphIds.Matter(1);

        /// <summary>Id of the envelope once <see cref="EnvelopeItem"/> forms.</summary>
        public int EnvelopeItemId { get; private set; }

        /// <summary>Every matter id: the Big Bang, the envelope and all bands.</summary>
        public IdRange AllIds { get; private set; } = IdRange.Empty;

        /// <summary>Ids of our lineage (root home band, galaxy ... Earth): one contiguous range.</summary>
        public IdRange PathIds { get; private set; } = IdRange.Empty;

        readonly Dictionary<MatterItem, MatterBand> bandOf = new Dictionary<MatterItem, MatterBand>();
        readonly Dictionary<MatterItem, List<MatterItem>> children = new Dictionary<MatterItem, List<MatterItem>>();
        readonly List<int> postorder = new List<int>();
        readonly List<int> rootLevel = new List<int>();
        float nominalStackMax;

        MatterLayout() { }

        /// <summary>The band that draws an item (for the root: its home band); null for the envelope item.</summary>
        public MatterBand BandOf(MatterItem item)
        {
            if (item == null) return null;
            if (item == Root) return RootHome;
            return bandOf.TryGetValue(item, out MatterBand b) ? b : null;
        }

        /// <summary>Highlight ids of an item and everything that formed out of it.</summary>
        public IdRange IdsOf(MatterItem item)
        {
            if (item == null) return IdRange.Empty;
            if (item == Root) return AllIds;
            if (item == EnvelopeItem) return IdRange.Single(EnvelopeItemId);
            MatterBand band = BandOf(item);
            return band != null ? band.Ids : IdRange.Empty;
        }

        /// <summary>Builds the tree, the radial order, ids, widths, styles and transitions of matter.json.</summary>
        public static MatterLayout Build(MatterFile file)
        {
            MatterLayout layout = new MatterLayout();
            if (file == null || file.Items.Count == 0) return layout;
            layout.BuildTree(file.Items);
            layout.BigBangYa = layout.Root.StartYa;
            layout.BigBangArc = DeepTime.Arc(layout.Root.StartYa);
            layout.BuildBands();
            layout.MeasureStack();
            return layout;
        }

        /// <summary>True when there was nothing to lay out.</summary>
        public bool IsEmpty => Root == null;

        void BuildTree(List<MatterItem> items)
        {
            Dictionary<string, MatterItem> byId = new Dictionary<string, MatterItem>(StringComparer.Ordinal);
            foreach (MatterItem item in items)
            {
                if (!byId.ContainsKey(item.Id)) byId.Add(item.Id, item);
            }

            // the root is the parentless item that holds everything (prefer our lineage, then the largest)
            foreach (MatterItem item in byId.Values)
            {
                if (!string.IsNullOrEmpty(item.Parent) && byId.ContainsKey(item.Parent)) continue;
                if (Root == null || (item.InPath && !Root.InPath) ||
                    (item.InPath == Root.InPath && item.MassKg > Root.MassKg))
                {
                    Root = item;
                }
            }

            if (Root == null)
            {
                // every parent chain loops: fall back to the most massive item
                foreach (MatterItem item in byId.Values)
                {
                    if (Root == null || item.MassKg > Root.MassKg) Root = item;
                }
            }

            Dictionary<MatterItem, int> fileOrder = new Dictionary<MatterItem, int>();
            foreach (MatterItem item in byId.Values)
            {
                fileOrder[item] = fileOrder.Count;
                children[item] = new List<MatterItem>();
            }

            foreach (MatterItem item in byId.Values)
            {
                if (item != Root) children[ResolveParent(item, byId)].Add(item);
            }

            foreach (List<MatterItem> list in children.Values)
            {
                list.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : fileOrder[a].CompareTo(fileOrder[b]));
            }

            for (MatterItem node = Root; node != null; node = PathChild(node)) Path.Add(node);

            foreach (MatterItem c in children[Root])
            {
                if (c.InPath || c.EndYa > 0 || c.MassKg < EnvelopeMassFraction * Root.MassKg) continue;
                if (EnvelopeItem == null || c.MassKg > EnvelopeItem.MassKg) EnvelopeItem = c;
            }

            if (EnvelopeItem != null)
            {
                // anything nested in the envelope item is laid out directly inside the universe
                children[Root].Remove(EnvelopeItem);
                children[Root].AddRange(children[EnvelopeItem]);
                children[EnvelopeItem].Clear();
            }
        }

        /// <summary>The item's parent, or the root when the parent is missing or the chain loops.</summary>
        MatterItem ResolveParent(MatterItem item, Dictionary<string, MatterItem> byId)
        {
            if (string.IsNullOrEmpty(item.Parent) || !byId.TryGetValue(item.Parent, out MatterItem parent) ||
                parent == item)
            {
                return Root;
            }

            MatterItem walk = parent;
            for (int guard = 0; guard < 64 && walk != null && walk != Root; guard++)
            {
                if (walk == item) return Root;
                walk = !string.IsNullOrEmpty(walk.Parent) && byId.TryGetValue(walk.Parent, out MatterItem up) ? up : null;
            }

            return walk == Root ? parent : Root;
        }

        MatterItem PathChild(MatterItem node)
        {
            foreach (MatterItem c in children[node])
            {
                if (c.InPath) return c;
            }

            return null;
        }

        void BuildBands()
        {
            // radial order: a path item's lineage-child region lies inside it and its other children
            // outside, so our home is always innermost and everything else splits off next to its parent
            List<MatterItem> order = new List<MatterItem>();
            AppendRadial(Root, order);
            foreach (MatterItem item in order)
            {
                MatterBand band = new MatterBand
                {
                    Item = item,
                    IsRootHome = item == Root,
                    Index = Bands.Count,
                    PathDepth = Path.IndexOf(item)
                };
                Bands.Add(band);
                if (item == Root) RootHome = band;
                else bandOf[item] = band;
            }

            foreach (MatterBand band in Bands)
            {
                if (band.IsRootHome) continue;
                foreach (MatterItem c in children[band.Item]) band.Children.Add(bandOf[c].Index);
            }

            foreach (MatterBand band in Bands)
            {
                foreach (int c in band.Children) Bands[c].Parent = band.Index;
            }

            // ids in preorder (lineage child first) so every subtree is a contiguous range:
            // 0 = Big Bang, 1 = the universe's envelope, 2 = the universe's home band, then the bands
            int next = 2;
            RootHome.Id = RootHome.LastId = GraphIds.Matter(next++);
            foreach (MatterItem c in OrderedChildren(Root)) AssignIds(bandOf[c], ref next);
            EnvelopeItemId = GraphIds.Matter(next);
            AllIds = new IdRange(GraphIds.Matter(0), EnvelopeItem != null ? EnvelopeItemId : GraphIds.Matter(next - 1));
            PathIds = new IdRange(GraphIds.Matter(2), GraphIds.Matter(2 + Path.Count - 1));

            foreach (MatterItem c in children[Root]) rootLevel.Add(bandOf[c].Index);
            rootLevel.Add(RootHome.Index);
            foreach (MatterItem c in children[Root]) AppendPostorder(bandOf[c]);

            MatterItem homeNow = Path[Path.Count - 1];
            foreach (MatterBand band in Bands)
            {
                MatterItem pathNext = Path.Count > 1 ? Path[1] : null;
                band.Width = band.IsRootHome ? (pathNext != null ? WidthOf(pathNext) : 4 * MinWidth) : WidthOf(band.Item);
                band.IsHomeNow = !band.IsRootHome && band.Item == homeNow && homeNow.EndYa <= 0;
                band.OwnDensity = band.IsRootHome ? 0 : OwnDensityOf(band);
                Style(band);
            }

            Transitions();
            foreach (MatterBand band in Bands)
            {
                if (band.IsRootHome || !band.InPath || !band.Item.Bound || band.Item.SizeM <= 0) continue;
                collapsing.Add(band);
            }

            collapsing.Sort((x, y) => x.Item.SizeM.CompareTo(y.Item.SizeM));
        }

        /// <summary>The widest the unscaled stack ever gets (normalizes the early-universe cap).</summary>
        void MeasureStack()
        {
            float[] own = new float[Bands.Count], footprint = new float[Bands.Count];
            foreach (float u in Samples()) nominalStackMax = Mathf.Max(nominalStackMax, NominalWidths(u, own, footprint));
        }

        void AppendRadial(MatterItem node, List<MatterItem> order)
        {
            MatterItem pathChild = PathChild(node);
            if (pathChild != null) AppendRadial(pathChild, order);
            order.Add(node);
            foreach (MatterItem c in children[node])
            {
                if (c != pathChild) AppendRadial(c, order);
            }
        }

        /// <summary>Children with our lineage first, then by rank.</summary>
        IEnumerable<MatterItem> OrderedChildren(MatterItem node)
        {
            MatterItem pathChild = PathChild(node);
            if (pathChild != null) yield return pathChild;
            foreach (MatterItem c in children[node])
            {
                if (c != pathChild) yield return c;
            }
        }

        void AssignIds(MatterBand band, ref int next)
        {
            band.Id = GraphIds.Matter(next++);
            foreach (MatterItem c in OrderedChildren(band.Item)) AssignIds(bandOf[c], ref next);
            band.LastId = GraphIds.Matter(next - 1);
        }

        void AppendPostorder(MatterBand band)
        {
            foreach (int c in band.Children) AppendPostorder(Bands[c]);
            postorder.Add(band.Index);
        }

        /// <summary>Nominal band width: log10 of the mass above a floor, so Earth is thin and the galaxy wide.</summary>
        public static float WidthOf(MatterItem item)
        {
            double dex = item.MassKg > 0 ? Math.Log10(item.MassKg) : FloorDex;
            return Mathf.Max(MinWidth, WidthPerDecade * (float)(dex - FloorDex));
        }

        static void Style(MatterBand band)
        {
            MatterItem item = band.Item;
            double dex = item.MassKg > 0 ? Math.Log10(item.MassKg) : FloorDex;
            // diffuse, cosmic-scale matter and things that disperse read as wisps
            float diffuse = Mathf.Clamp01((float)(dex - 35) / 25f) + (item.EndYa > 0 ? 0.25f : 0f);

            if (band.IsRootHome)
            {
                // the young universe was hot and dense: bright, then it flows into the galaxy
                band.FillAlpha = 0.36f;
                band.FillIntensity = 1.3f;
                band.Noise = 0.35f;
                band.LineAlpha = 0.3f;
                band.LineIntensity = 1f;
                band.LineWidthPx = 1f;
                band.HandoffExponent = 2f;
            }
            else if (band.InPath)
            {
                // calm fills (the density factor below sets the contrast); the contour lines carry the glow
                band.FillAlpha = band.IsHomeNow ? 0.34f : Mathf.Min(0.28f, 0.14f + 0.03f * band.PathDepth);
                band.FillIntensity = 1f;
                band.Noise = Mathf.Clamp01(diffuse);
                // the density profile shows the regions; the contours stay as fine, quiet strands
                band.LineAlpha = 0.45f;
                band.LineIntensity = 0.9f;
                band.LineWidthPx = 1.1f;
                // our home (Earth) stays lit up to the handoff, the rest of our lineage a little less long
                band.HandoffExponent = band.IsHomeNow ? 0f : 1f;
            }
            else
            {
                band.FillAlpha = 0.15f;
                band.FillIntensity = 0.9f;
                band.Noise = Mathf.Clamp01(diffuse);
                band.LineAlpha = 0.22f;
                band.LineIntensity = 0.8f;
                band.LineWidthPx = 1f;
                band.HandoffExponent = 2f;
            }

            // brightness follows the real density of the band's own region, so the gaps show: Earth and the
            // Sun glow, the space between the stars is faint and the space between the galaxies nearly dark,
            // while the contour lines keep every region readable
            if (!band.IsRootHome)
            {
                float density = DensityFactor(band.OwnDensity);
                band.FillAlpha *= density;
                band.LineAlpha *= Mathf.Lerp(0.45f, 1f, density);
            }

            // the regions are decades wide on the comb: calm, translucent fills and little noise, so the lines - the
            // scale's branches and the bodies' edges - carry the picture
            band.FillAlpha *= FillSubdue;
            band.Noise = Mathf.Min(band.Noise, MaxNoise);
        }

        const float FillSubdue = 0.55f, MaxNoise = 0.3f;

        /// <summary>Mass of a hydrogen atom (kg): densities are quoted as atoms per cubic metre.</summary>
        const double HydrogenKg = 1.6735e-27;

        // brightness from density: log10(kg/m^3) mapped from the cosmic mean (-28) to rock (+4)
        const double DensityDexLow = -28, DensityDexHigh = 4;
        const float DensityFloor = 0.06f, DensityGamma = 0.85f;

        static double VolumeOf(MatterItem item)
        {
            double r = item.SizeM * 0.5;
            return 4.0 / 3.0 * Math.PI * r * r * r;
        }

        double OwnDensityOf(MatterBand band)
        {
            MatterItem item = band.Item;
            if (item.SizeM <= 0 || item.MassKg <= 0) return 0;
            double mass = item.MassKg, volume = VolumeOf(item);
            // a body that ends hands its matter to what forms out of it (the solar nebula): its own mean density
            if (item.EndYa > 0) return mass / volume;
            double ownMass = mass, ownVolume = volume;
            foreach (int c in band.Children)
            {
                MatterItem child = Bands[c].Item;
                if (child.MassKg <= 0 || child.SizeM <= 0) continue;
                ownMass -= child.MassKg;
                ownVolume -= VolumeOf(child);
            }

            // children that hold nearly everything (or overlap) fall back to the whole body's mean density
            if (ownMass < 0.01 * mass || ownVolume < 0.01 * volume) return mass / volume;
            return ownMass / ownVolume;
        }

        /// <summary>
        /// Brightness factor (<see cref="DensityFloor"/> .. 1) from a density in kg/m^3: about 1 for Earth and the
        /// Sun (10^3), 0.3 for the space between the stars (10^-22), under 0.1 between the galaxies (10^-28).
        /// </summary>
        public static float DensityFactor(double kgPerM3)
        {
            if (kgPerM3 <= 0) return 1f;
            float t = Mathf.Clamp01((float)((Math.Log10(kgPerM3) - DensityDexLow) / (DensityDexHigh - DensityDexLow)));
            return DensityFloor + (1 - DensityFloor) * Mathf.Pow(t, DensityGamma);
        }

        /// <summary>
        /// A density as atoms (hydrogen masses) per cubic metre: "3x10^30" for Earth, "~0.3" for the space between
        /// galaxies. <paramref name="rich"/> gives TextMeshPro rich text for labels ("10&lt;sup&gt;30&lt;/sup&gt;
        /// atoms/m&lt;sup&gt;3&lt;/sup&gt;"), otherwise plain text for tooltips. Null when unknown.
        /// </summary>
        public static string DensityText(double kgPerM3, bool rich)
        {
            if (kgPerM3 <= 0) return null;
            double n = kgPerM3 / HydrogenKg;
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            string value;
            if (n >= 0.095 && n < 9.5)
            {
                value = "~" + n.ToString(n < 1 ? "0.0" : "0", inv);
            }
            else
            {
                // one significant figure: 3x10^30 (Earth), 8x10^29 (the Sun)
                int exponent = (int)Math.Floor(Math.Log10(n));
                int mantissa = (int)Math.Round(n / Math.Pow(10, exponent));
                if (mantissa >= 10)
                {
                    mantissa = 1;
                    exponent++;
                }

                string power = rich ? "10<sup>" + exponent.ToString(inv) + "</sup>" : "10^" + exponent.ToString(inv);
                value = mantissa == 1 ? power : mantissa.ToString(inv) + "\u00d7" + power;
            }

            return value + (rich ? " atoms/m<sup>3</sup>" : " atoms per cubic metre");
        }

        /// <summary>
        /// Unbound structures (superclusters, the universe) thin out as space expands: their brightness falls
        /// with the growth of the envelope since they formed. Bound bodies keep their density.
        /// </summary>
        public float ExpansionDim(MatterBand band, float u)
        {
            if (band.IsRootHome || band.Item.Bound) return 1f;
            float formed = Envelope(Mathf.Min(band.UFull, BigBangArc - BurstTau));
            float now = Envelope(u);
            if (now <= formed || formed <= 1e-5f) return 1f;
            return Mathf.Clamp(Mathf.Pow(formed / now, 0.3f), 0.2f, 1f);
        }

        /// <summary>
        /// Formation and decay windows. A band that ends hands its matter to the children that form during
        /// its life (solar nebula -> Sun and planets): it narrows over exactly the window in which they
        /// grow. The root's home band hands over to the galaxy the same way.
        /// </summary>
        void Transitions()
        {
            foreach (MatterBand band in Bands)
            {
                if (band.IsRootHome) continue;
                MatterItem item = band.Item;
                band.StartYa = item.StartYa;
                band.FullYa = item.StartYa - DefaultTransition(item.StartYa, Life(item));
                band.EndYa = Math.Max(0, item.EndYa);
                band.DecayYa = band.EndYa > 0 ? band.EndYa + DefaultTransition(band.EndYa, Life(item)) : 0;
            }

            foreach (MatterBand parent in Bands)
            {
                if (parent.IsRootHome || parent.EndYa <= 0) continue;
                double firstChild = 0;
                foreach (int c in parent.Children)
                {
                    double start = Bands[c].StartYa;
                    if (start <= parent.StartYa && start > parent.EndYa) firstChild = Math.Max(firstChild, start);
                }

                if (firstChild <= 0) continue;
                parent.DecayYa = firstChild;
                // every child forming in the handover window is complete when the parent is gone, so the
                // stack keeps its width (a late child gets a shorter ramp instead of one running past now)
                foreach (int c in parent.Children)
                {
                    MatterBand child = Bands[c];
                    if (child.StartYa <= parent.StartYa && child.StartYa > parent.EndYa)
                    {
                        child.FullYa = parent.EndYa;
                    }
                }
            }

            MatterBand home = RootHome;
            MatterBand pathNext = Path.Count > 1 ? BandOf(Path[1]) : null;
            home.StartYa = Root.StartYa;
            if (pathNext != null && pathNext.StartYa < Root.StartYa)
            {
                home.FullYa = Root.StartYa - DefaultTransition(Root.StartYa, Root.StartYa - pathNext.StartYa);
                home.DecayYa = pathNext.StartYa;
                home.EndYa = Math.Max(pathNext.FullYa, 1e-9);
            }
            else
            {
                home.FullYa = Root.StartYa - DefaultTransition(Root.StartYa, Life(Root));
                home.DecayYa = home.EndYa = 0;
            }

            foreach (MatterBand band in Bands)
            {
                band.FullYa = Math.Min(band.FullYa, band.StartYa);
                band.UStart = DeepTime.Arc(band.StartYa);
                band.UFull = DeepTime.Arc(band.FullYa);
                band.UDecay = band.EndYa > 0 ? DeepTime.Arc(band.DecayYa) : 0;
                band.UEnd = band.EndYa > 0 ? DeepTime.Arc(band.EndYa) : 0;
            }

            // a group branches off where its outermost member starts: walk up while the parent formed
            // within BranchArc (the root's direct children open with the universe itself)
            foreach (MatterBand band in Bands)
            {
                MatterBand top = band;
                while (top.Parent >= 0 && !Bands[top.Parent].IsRootHome &&
                       Bands[top.Parent].UStart - top.UStart < BranchArc)
                {
                    top = Bands[top.Parent];
                }

                band.UBranch = band.IsRootHome ? band.UStart : top.UStart;
                bool nested = top.Parent >= 0 && !Bands[top.Parent].IsRootHome;
                band.BranchLength = nested ? NestedBranchArc : BranchArc;
            }

            EnvelopeItemArc = EnvelopeItem != null ? DeepTime.Arc(EnvelopeItem.StartYa) : 0;
        }

        static double Life(MatterItem item) => item.StartYa - Math.Max(0, item.EndYa);

        static double DefaultTransition(double atYa, double life) =>
            Math.Max(0, Math.Min(TransitionLifeFraction * life, TransitionAgeFraction * atYa));

        /// <summary>
        /// Outer edge of the red layer at arc u: a fast initial burst, then the expansion of space itself
        /// (<see cref="ScaleFactor"/>), plus a tail that keeps growing with the arc since the Big Bang.
        /// </summary>
        public float Envelope(float u)
        {
            float tau = BigBangArc - u;
            if (tau <= 0) return 0;
            return BurstWidth * (1 - Mathf.Exp(-tau / BurstTau)) + CosmicWidth * ScaleFactor(u) +
                   GrowthScale * (Mathf.Exp(GrowthRate * tau) - 1);
        }

        /// <summary>
        /// Cosmic scale factor at arc u (1 today), for a flat Lambda-CDM universe:
        /// a(t) proportional to sinh^(2/3)(1.5 sqrt(OL) t / tH), normalized to 1 at the age of the universe in the data.
        /// </summary>
        public float ScaleFactor(float u)
        {
            double t = BigBangYa - DeepTime.YearsAgo(u);
            if (t <= 0) return 0;
            return (float)(RawScaleFactor(t) / RawScaleFactor(BigBangYa));
        }

        static double RawScaleFactor(double t) =>
            Math.Pow(Math.Sinh(1.5 * Math.Sqrt(OmegaL) * t / HubbleTimeYears), 2.0 / 3.0);

        // ------------------------------------------------------------------ the field
        //
        // One continuous scale for everything in the red layer - the bands and the grid alike, so no two lines ever
        // cross: a comb of comoving distances from our lineage, one cell per decade, from the trunk (the inner track:
        // a kilometre, the most granular scale) out to the observable universe, Decades of them. The comb's spacing is
        // the fan's width at that moment over those decades, so at the Big Bang every line lies on the trunk and as
        // space inflates - the burst of inflation, then the cosmic scale factor - the comb opens: the outermost lines
        // race outward fastest, every inner cell's growth pushes everything beyond it further out, and each finer line
        // separates from its parent only once their cell has grown, so the scale branches open like a tree. A bound
        // body keeps its physical size, so its edge (and the grid inside it) drifts gently inward through the comoving
        // comb once it has formed; before, its matter's region rides the expansion, CollapseFactor times its radius.

        /// <summary>A bound body's matter collapsed from a comoving region this many times its radius.</summary>
        public const double CollapseFactor = 6;

        /// <summary>log10 of the trunk's comoving distance (metres today): a kilometre, the most granular scale.</summary>
        public const double TrunkLog10 = 3;

        /// <summary>Decades of comoving distance from the trunk to the observable universe (about twenty-three).</summary>
        public static readonly double Decades = Math.Log10(ObservableRadiusM) - TrunkLog10;

        /// <summary>log10 of the cosmic scale factor at arc u.</summary>
        public double Log10Scale(float u) => Math.Log10(Mathf.Max(ScaleFactor(u), 1e-30f));

        /// <summary>log10 radius (metres) of the horizon at arc u: the observable universe, scaled back.</summary>
        public double OuterLog10(float u) => Math.Log10(ObservableRadiusM) + Log10Scale(u);

        /// <summary>log10 of the physical distance (metres) at the trunk at arc u: a comoving kilometre, scaled back.</summary>
        public double InnerLog10(float u) => TrunkLog10 + Log10Scale(u);

        /// <summary>World units per decade of comoving distance at arc u: the fan's width over the decades it holds.</summary>
        public double UnitsPerDecade(float u) => Envelope(u) / Decades;

        /// <summary>Comoving cell coordinate (decades above the trunk) of a physical distance at arc u.</summary>
        public double Cell(float u, double log10Radius) => log10Radius - Log10Scale(u) - TrunkLog10;

        /// <summary>rho of a physical distance at arc u (negative below the trunk).</summary>
        public float Rho(float u, double log10Radius) => (float)(Cell(u, log10Radius) * UnitsPerDecade(u));

        /// <summary>How far formed a body is at arc u: 0 before it starts, 1 once its band is fully open.</summary>
        public static float Formation(MatterBand band, float u)
        {
            if (u >= band.UStart) return 0;
            return u <= band.UFull ? 1f : Smooth((band.UStart - u) / Mathf.Max(band.UStart - band.UFull, 1e-9f));
        }

        /// <summary>The bound bodies of our lineage (every one that ever forms), innermost first: the matter inside each collapsed with it.</summary>
        readonly List<MatterBand> collapsing = new List<MatterBand>();

        /// <summary>
        /// How much further out, comoving, the matter that is now 10^<paramref name="log10Radius"/> metres from us
        /// lay before the bound bodies around it collapsed (log10): the sum over every bound body of our lineage
        /// that encloses that distance of the collapse still to come at arc u. Each body's share relaxes from the
        /// whole collapse factor to nothing as the body forms, so the field is continuous from the Big Bang on.
        /// </summary>
        public double CollapseLog10(float u, double log10Radius)
        {
            double sum = 0;
            foreach (MatterBand band in collapsing)
            {
                if (Math.Log10(0.5 * band.Item.SizeM) < log10Radius - 1e-9) continue;
                sum += (1 - Formation(band, u)) * Math.Log10(CollapseFactor);
            }

            return sum;
        }

        /// <summary>log10 of the physical distance from our lineage (metres) to a body's outer edge at arc u.</summary>
        public double EdgeLog10(MatterBand band, float u)
        {
            double radius = Math.Log10(0.5 * band.Item.SizeM);
            double logA = Log10Scale(u);
            if (!band.Item.Bound) return radius + logA;
            return radius + logA + CollapseLog10(u, radius);
        }

        /// <summary>The radius of the observable universe today (metres).</summary>
        public const double ObservableRadiusM = 4.4e26;

        /// <summary>
        /// Radial layout at arc u on the field: inner / outer edge of every band's own part (index = stack order), the
        /// outer edge of the stack and of the envelope. A path body's region reaches from the band inside it to its
        /// own edge; a body that split off (the Moon, the planets, the first stars) lies just outside its parent, as
        /// wide as its size, opening with its formation. Arrays must hold <see cref="Bands"/>.Count entries.
        /// </summary>
        public void Evaluate(float u, float[] inner, float[] outer, out float stackOuter, out float envelope)
        {
            envelope = Envelope(u);
            double unitsPerDecadeNow = UnitsPerDecade(u);
            double logA = Log10Scale(u);
            float cum = 0;
            for (int i = 0; i < Bands.Count; i++)
            {
                MatterBand band = Bands[i];
                inner[i] = outer[i] = cum;
                if (!band.AliveAt(u)) continue;
                float edge;
                if (band.IsRootHome)
                {
                    // the young universe's home: the region that became our lineage, out to the first body's edge
                    MatterBand first = Path.Count > 1 ? BandOf(Path[1]) : null;
                    edge = first != null ? Rho(u, EdgeLog10(first, u)) : envelope;
                }
                else if (band.InPath)
                {
                    edge = Rho(u, EdgeLog10(band, u));
                }
                else
                {
                    // just outside its parent's edge, as wide as its size (in comoving decades), opening with its formation
                    double atCum = cum / unitsPerDecadeNow + TrunkLog10 + logA;   // physical log10 radius at the edge
                    double span = Math.Log10(Math.Pow(10, atCum) + Math.Max(band.Item.SizeM, 1)) - atCum;
                    edge = cum + (float)(Math.Max(span, MinSplitDecades) * unitsPerDecadeNow) * Formation(band, u) * band.Branch(u);
                }

                edge = Mathf.Clamp(edge, cum, envelope);
                outer[i] = edge;
                cum = edge;
            }

            stackOuter = cum;
        }

        /// <summary>A body that split off is drawn at least this many decades wide, so it stays visible beside its parent.</summary>
        const double MinSplitDecades = 0.25;

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        /// <summary>
        /// Nominal (unscaled) own width and region footprint of every band at arc u, computed children
        /// first; returns the width of the whole stack.
        /// </summary>
        float NominalWidths(float u, float[] own, float[] footprint)
        {
            foreach (int i in postorder)
            {
                MatterBand band = Bands[i];
                float sum = 0;
                foreach (int c in band.Children) sum += footprint[c];
                // a region is as wide as its own matter or what formed out of it, and always keeps some room
                // of its own (the Local Group is barely heavier than the Milky Way, but must stay visible)
                float width = band.Width * band.Ramp(u) * band.Branch(u);
                footprint[i] = Mathf.Max(width, sum + MinOwnFraction * width);
                own[i] = footprint[i] - sum;
            }

            float total = RootHome.Width * RootHome.Ramp(u);
            own[RootHome.Index] = footprint[RootHome.Index] = total;
            foreach (int i in rootLevel)
            {
                if (i != RootHome.Index) total += footprint[i];
            }

            return total;
        }

        /// <summary>
        /// Scale applied to the whole stack: the bodies take <see cref="CapFraction"/> of the envelope and grow
        /// with it. It depends only on the expansion, so a forming band pushes the bands outside it outward
        /// instead of squeezing them.
        /// </summary>
        public float StackScale(float u)
        {
            // the nested bodies grow with the expanding universe for its whole history, so everything from
            // Earth to Laniakea fans out together as a set of diverging contours
            if (nominalStackMax <= 1e-9f) return 1;
            return CapFraction * Envelope(u) / nominalStackMax;
        }

        /// <summary>
        /// Arc samples from the Big Bang to the handoff (descending, <see cref="EndArc"/> last): a uniform
        /// base step plus dense samples inside every formation / decay window and right after the Big Bang,
        /// where the clock is compressed.
        /// </summary>
        public List<float> Samples()
        {
            List<float> s = new List<float>(1024);
            float top = BigBangArc, bottom = EndArc;
            int n = Mathf.Max(2, Mathf.CeilToInt((top - bottom) / ArcStep));
            for (int k = 0; k < n; k++) s.Add(top - (top - bottom) * k / n);
            s.Add(bottom); // exactly: top - (top - bottom) can round below it and would be filtered out

            for (float d = 2e-5f; d < 4 * BurstTau; d *= 1.5f) s.Add(top - d);

            foreach (MatterBand band in Bands)
            {
                AddWindow(s, band.UStart, band.UFull);
                if (!band.Extant) AddWindow(s, band.UDecay, band.UEnd);
            }

            if (EnvelopeItem != null) s.Add(EnvelopeItemArc);

            s.Sort((a, b) => b.CompareTo(a));
            List<float> result = new List<float>(s.Count);
            foreach (float u in s)
            {
                if (u > top || u < bottom) continue;
                if (result.Count > 0 && result[result.Count - 1] - u < 2e-7f) continue;
                result.Add(u);
            }

            return result;
        }

        static void AddWindow(List<float> s, float from, float to)
        {
            if (from - to <= 0) return;
            for (int k = 0; k <= TransitionSamples; k++) s.Add(from + (to - from) * k / TransitionSamples);
        }

        /// <summary>
        /// Per-vertex dissolve ahead of the human branch, multiplied onto the materials' own handoff fade
        /// (<see cref="GraphMaterials.FadeBeforeHumanBranch"/>): with exponent 2 the bulk of matter fades as
        /// the cube of <see cref="GraphStyle.HandoffFade"/> (like the life layer's other lineages) and is
        /// gone well before 3 o'clock, while our home (exponent 0) stays lit until the handoff itself.
        /// </summary>
        public static float HandoffBias(float u, float exponent) =>
            exponent <= 0 ? 1f : Mathf.Pow(GraphStyle.HandoffFade(u), exponent);

        /// <summary>
        /// Opacity of the rest of the universe at arc u: dense and bright right after the Big Bang, thinning
        /// as it spreads over a wider envelope, and dissolving first ahead of the human branch.
        /// </summary>
        public static float EnvelopeAlpha(float u, float stackOuter, float envelope)
        {
            float spread = Mathf.Max(0, envelope - stackOuter);
            return EnvelopeAlpha0 / Mathf.Pow(1 + spread / EnvelopeAlphaWidth, 0.18f) *
                   HandoffBias(u, EnvelopeHandoffExponent);
        }

        /// <summary>Fraction (0..1) of the envelope's width at the inner edge of strip k (strips thicken outward).</summary>
        public static float StripEdge(int k) => Mathf.Pow(k / (float)EnvelopeStrips, 1.35f);

        /// <summary>
        /// Relative opacity at the inner edge of envelope strip k: densest next to the stack, fading outward
        /// continuously to exactly 0 at the outer boundary.
        /// </summary>
        public static float StripEdgeAlpha(int k) => Mathf.Pow(Mathf.Clamp01(1 - k / (float)EnvelopeStrips), 1.2f);

        /// <summary>Noise amount of envelope strip k: the outer wisps break up into nebula.</summary>
        public static float StripNoise(int k) => 0.3f + 0.7f * k / (EnvelopeStrips - 1f);
    }
}
