using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Bands
{
    /// <summary>What the geometry pass learned about one stream (used for its label and anchor).</summary>
    public sealed class StreamInfo
    {
        public Civ Civ;

        /// <summary>Largest share of world power (0..1) the stream ever held.</summary>
        public double MaxShare;

        /// <summary>True when the stream has a label position (it was drawn).</summary>
        public bool HasLabel;

        /// <summary>Arc and band center of the stream at its widest moment (label position).</summary>
        public float LabelU, LabelRho;

        /// <summary>Band center at the stream's first data slice (anchor position, on the band).</summary>
        public float AnchorRho;
    }

    /// <summary>Where a war was drawn (used for its anchor and label).</summary>
    public sealed class WarInfo
    {
        public War War;
        public int Id;

        /// <summary>Band center of the first listed stream alive when the war began.</summary>
        public float Rho;
    }

    /// <summary>
    /// Builds the civilization streams of the BLUE layer into three builders (worker thread):
    /// <list type="bullet">
    /// <item><see cref="Fill"/> - one translucent band per stream, its width the stream's relative power, plus
    /// the sheet where humans rise out of the life layer and faint overlays where wars scar a stream;</item>
    /// <item><see cref="Lines"/> - band edges, our path along the inner edge of the layer, lineage links
    /// between parent and child streams, and war ticks;</item>
    /// <item><see cref="LifeLines"/> - the green half of the level jump (drawn with the life color).</item>
    /// </list>
    /// All geometry follows <see cref="HumanWorld.TryBand"/> exactly, so other human layers can draw
    /// population curves and lifelines inside the same bands.
    /// </summary>
    public sealed class BandGeometry
    {
        /// <summary>Height of the war marks above the bands.</summary>
        public const float WarY = 0.01f;

        // fills
        const float CivFillAlpha = 0.16f;
        const float HumanityFillAlpha = 0.13f;
        const float HumanityNoise = 0.35f;
        const float CivIntensityMin = 0.85f;
        const float CivIntensityRange = 0.3f;

        // edges: additive, so where many thin streams crowd (the early branch, a few pixels wide in the
        // overview) each edge steps back instead of the bundle saturating into a blob
        const float EdgeAlpha = 0.5f;
        const float EdgeWidthPx = 0.9f;
        const float EdgeWidthWorld = 0.0004f;
        const float EdgeIntensity = 0.75f;
        const float EdgeFullWidth = 0.08f;     // band width (rho) at which edges reach full alpha
        const float EdgeNarrowAlpha = 0.3f;    // share of the edge alpha left on a band of zero width
        const float OuterHumanityEdgeAlpha = 0.35f;

        // our path (the inner edge of the human layer)
        const float PathAlpha = 0.9f;
        const float PathWidthPx = 1.8f;
        const float PathWidthWorld = 0.0015f;
        const float PrehistoryPathIntensity = 1.6f;
        const float HistoryPathIntensity = 1.4f;
        const double EnvelopeGridYears = 2;
        const int EnvelopeSmoothRadius = 3;    // grid cells, two passes: takes the kinks off hand-overs
        const float EnvelopeMinWidthUnits = 5; // emerging slivers do not claim the inner edge

        // the level jump: Homo sapiens rises out of the Hominidae lineage
        const double JumpBaseYearsAgo = 330_000;
        const int JumpSteps = 32;
        const float JumpIntensity = 2.5f;
        const float JumpWidthPx = 2.4f;
        const float JumpWidthWorld = 0.003f;
        const float FanAlphaBottom = 0.03f;
        const float FanIntensity = 1.2f;
        const float FanRimAlpha = 0.4f;

        // lineage links between streams
        const float LinkY = 0.004f;
        const float LinkLiftBase = 0.004f;    // links arch up over the bands they cross...
        const float LinkLiftPerRho = 0.02f;   // ...more for longer radial jumps
        const float LinkLiftMax = 0.035f;
        const float LinkAlpha = 0.6f;
        const float LinkGapFade = 0.6f;       // links spanning long gaps in time are fainter
        const double LinkGapFadeYears = 600;
        const float LinkWidthPx = 1.1f;
        const float LinkWidthWorld = 0.0006f;
        const float LinkIntensity = 1.1f;
        const double MaxLinkGapYears = 1000;

        // wars: subtle marks whose brightness grows with the death toll (x10 deaths = +WarIntensityPerDecade)
        const float WarOverlayAlpha = 0.08f;
        const float WarAlpha = 0.55f;
        const float WarEndTickAlpha = 0.5f;   // relative to the start tick
        const float WarLineAlpha = 0.6f;      // relative to the start tick
        const float WarTickWidthPx = 1.2f;
        const float WarLineWidthPx = 1.0f;
        const float WarIntensityMin = 0.8f;
        const float WarIntensityMax = 1.6f;
        const float WarIntensityPerDecade = 0.3f;

        public readonly SurfaceMeshBuilder Fill = new SurfaceMeshBuilder();
        public readonly LineMeshBuilder Lines = new LineMeshBuilder(40_000);
        public readonly LineMeshBuilder LifeLines = new LineMeshBuilder(64);
        public readonly List<StreamInfo> Streams = new List<StreamInfo>();
        public readonly List<WarInfo> Wars = new List<WarInfo>();

        readonly HumanWorld world;
        readonly double now;

        // scratch buffers (reused per stream)
        readonly List<StreamSample> samples = new List<StreamSample>(2048);
        readonly List<Vector3> inner = new List<Vector3>(2048);
        readonly List<Vector3> outer = new List<Vector3>(2048);
        readonly List<Color32> colors = new List<Color32>(2048);
        readonly List<Color32> outerColors = new List<Color32>(2048);
        readonly List<LinePoint> pts = new List<LinePoint>(2048);
        readonly List<LinePoint> pts2 = new List<LinePoint>(2048);
        readonly List<double> knots = new List<double>(128);

        /// <summary>Our path: the level jump, the inner edge of prehistory, then the inner envelope of history.</summary>
        readonly List<LinePoint> path = new List<LinePoint>(2048);

        // smoothed inner envelope of the layer (Histomap units) on a regular grid of years
        double envStart;
        float[] envelope = Array.Empty<float>();

        public BandGeometry(HumanWorld world)
        {
            this.world = world;
            now = world.NowYear;
        }

        /// <summary>Builds everything. Worker thread.</summary>
        public void Build()
        {
            BuildHumanity();
            for (int i = 0; i < world.Civs.Count; i++)
            {
                Civ c = world.Civs[i];
                if (c != world.Humanity && c.Samples.Count > 0) BuildStream(c);
            }

            BuildEnvelope();
            BuildHistoryPath();
            BuildLinks();
            BuildWars();
        }

        // ------------------------------------------------------------------ prehistory and the level jump

        /// <summary>
        /// Prehistoric humanity: one stream from 300 ka that widens with (log) world population and hands
        /// over to the first civilizations. It rises out of the life layer's inner track (Hominidae) through
        /// a bright connector that fans open into the band: the jump from the green level to the blue one.
        /// </summary>
        void BuildHumanity()
        {
            Civ h = world.Humanity;
            double start = h.StartYear;
            double handOff = world.HistomapStartYear;
            double end = handOff + HumanWorld.EmergeYears; // HumanWorld keeps the band until here
            double fadeStart = handOff - 2 * HumanWorld.EmergeYears;
            StreamSampler.Build(now, start, end, false, null, start, fadeStart, double.PositiveInfinity, samples);

            int id = GraphIds.Civ(h.Index);
            StreamInfo info = new StreamInfo { Civ = h, MaxShare = 1 };
            inner.Clear();
            outer.Clear();
            colors.Clear();
            outerColors.Clear();
            pts.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                StreamSample s = samples[i];
                if (!world.TryBand(h, s.Year, out float lo, out float hi)) continue;
                // the prehistoric stream fades out while the first civilizations emerge inside it
                float handOffFade = 1f - SmoothStep(fadeStart, end, s.Year);
                inner.Add(new Vector3(s.U, GraphStyle.HumansY, lo));
                outer.Add(new Vector3(s.U, GraphStyle.HumansY, hi));
                // humanity spans the whole layer: it fades across to nothing at the outer boundary (no rim)
                colors.Add(Tint(HumanityFillAlpha * handOffFade));
                outerColors.Add(Tint(0));
            }

            Fill.AddBand(inner, outer, colors, outerColors, id, 1f, HumanityNoise);

            // label at the log-middle of prehistory
            double midYa = Math.Sqrt((now - start) * (now - handOff));
            info.HasLabel = true;
            info.LabelU = DeepTime.Arc(midYa);
            info.LabelRho = Center(h, now - midYa);
            info.AnchorRho = Center(h, start);
            Streams.Add(info);

            BuildJump(h, start, handOff);
        }

        /// <summary>
        /// The level jump and the first part of our path: a connector from the Hominidae track (life layer,
        /// rho 0) up to the inner edge of the new stream, a translucent sheet opening from it to the stream's
        /// full width, then the inner edge of prehistory up to the first civilizations.
        /// </summary>
        void BuildJump(Civ h, double start, double handOff)
        {
            float uTop = DeepTime.Arc(now - start);
            float uBottom = DeepTime.Arc(Math.Max(JumpBaseYearsAgo, now - start + 1));
            if (!world.TryBand(h, start, out float lo, out float hi)) return;
            float width = hi - lo;

            // connector: leaves the Hominidae track at LifeY tangentially and merges into the inner edge of
            // the human layer at the moment Homo sapiens appears; green fades into blue on the way up
            path.Clear();
            pts2.Clear();
            inner.Clear();
            outer.Clear();
            colors.Clear();
            for (int k = 0; k <= JumpSteps; k++)
            {
                float f = k / (float)JumpSteps;
                float e = f * f * (3f - 2f * f);
                float u = Mathf.Lerp(uBottom, uTop, f);
                float y = Mathf.Lerp(GraphStyle.LifeY, GraphStyle.HumansY, e);
                Vector3 p = new Vector3(u, y, lo);
                path.Add(new LinePoint(p, Tint(e), JumpWidthPx, JumpWidthWorld, JumpIntensity));
                pts2.Add(new LinePoint(p, Tint(1f - e), JumpWidthPx, JumpWidthWorld, JumpIntensity));

                // the sheet opens from the lineage to the full width of the new stream
                float open = Mathf.Pow(e, 1.3f);
                inner.Add(p);
                outer.Add(new Vector3(u, y, lo + width * open));
                colors.Add(Tint(Mathf.Lerp(FanAlphaBottom, HumanityFillAlpha, e)));
            }

            Fill.AddBand(inner, outer, colors, GraphIds.Civ(h.Index), FanIntensity, HumanityNoise);
            LifeLines.AddPolyline(pts2, CivBandsLayer.LineageId, 1f);

            // the outer rim of the sheet as a faint ray
            pts2.Clear();
            for (int k = 0; k < outer.Count; k++)
            {
                float f = k / (float)JumpSteps;
                pts2.Add(new LinePoint(outer[k], Tint(FanRimAlpha * f), EdgeWidthPx, EdgeWidthWorld, 1f));
            }

            Lines.AddPolyline(pts2, GraphIds.Civ(h.Index), 1f);

            // our path continues along the inner edge of prehistory up to the first civilizations
            StreamSampler.Build(now, start, handOff, false, null, start, double.PositiveInfinity,
                double.PositiveInfinity, samples);
            for (int i = 1; i < samples.Count; i++)
            {
                StreamSample s = samples[i];
                if (!world.TryBand(h, s.Year, out float l, out float _)) continue;
                path.Add(new LinePoint(new Vector3(s.U, GraphStyle.HumansY, l), Tint(PathAlpha), PathWidthPx,
                    PathWidthWorld, PrehistoryPathIntensity));
            }
        }

        // ------------------------------------------------------------------ civilization streams

        void BuildStream(Civ c)
        {
            (double start, double end) = world.Span(c);
            List<Civ.Sample> data = c.Samples;
            double first = data[0].Year, last = data[data.Count - 1].Year;
            knots.Clear();
            for (int i = 0; i < data.Count; i++) knots.Add(data[i].Year);
            StreamSampler.Build(now, start, c.Extant ? last : end, c.Extant, knots, first,
                c.Extant ? double.PositiveInfinity : last, StreamSampler.MaxYearStep, samples);

            int id = GraphIds.Civ(c.Index);
            float intensity = CivIntensityMin + CivIntensityRange * Frac(c.Index * 0.618034f);
            StreamInfo info = new StreamInfo { Civ = c, AnchorRho = Center(c, c.StartYear) };

            // the label goes where the band is widest, preferably away from the ends of its span
            double inStart = start + 0.1 * (end - start), inEnd = end - 0.1 * (end - start);
            float widest = -1, widestInside = -1;
            float wideU = 0, wideRho = 0, insideU = 0, insideRho = 0;

            inner.Clear();
            outer.Clear();
            colors.Clear();
            pts.Clear();
            pts2.Clear();
            outerColors.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                StreamSample s = samples[i];
                if (!Band(c, s.Year, out float lo, out float hi, out float share)) continue;
                float w = hi - lo;
                // no hard outer border: streams fade with their position across the layer, and the layer's
                // own outer boundary dissolves into transparent black
                float layer = Mathf.Max(world.LayerWidth(s.Year), 1e-4f);
                float fadeLo = OuterFade(lo / layer), fadeHi = OuterFade(hi / layer);
                inner.Add(new Vector3(s.U, GraphStyle.HumansY, lo));
                outer.Add(new Vector3(s.U, GraphStyle.HumansY, hi));
                colors.Add(Tint(CivFillAlpha * fadeLo));
                outerColors.Add(Tint(CivFillAlpha * fadeHi));
                float edgeAlpha = EdgeAlphaFor(w);
                pts.Add(new LinePoint(new Vector3(s.U, GraphStyle.HumansY, lo), Tint(edgeAlpha * fadeLo), EdgeWidthPx,
                    EdgeWidthWorld, EdgeIntensity));
                pts2.Add(new LinePoint(new Vector3(s.U, GraphStyle.HumansY, hi), Tint(edgeAlpha * fadeHi), EdgeWidthPx,
                    EdgeWidthWorld, EdgeIntensity));

                if (share > info.MaxShare) info.MaxShare = share;
                if (w > widest)
                {
                    widest = w;
                    wideU = s.U;
                    wideRho = 0.5f * (lo + hi);
                }

                if (s.Year >= inStart && s.Year <= inEnd && w > widestInside)
                {
                    widestInside = w;
                    insideU = s.U;
                    insideRho = 0.5f * (lo + hi);
                }
            }

            Fill.AddBand(inner, outer, colors, outerColors, id, intensity);
            Lines.AddPolyline(pts, id);
            Lines.AddPolyline(pts2, id);

            info.HasLabel = widest > 0;
            info.LabelU = widestInside > 0 ? insideU : wideU;
            info.LabelRho = widestInside > 0 ? insideRho : wideRho;
            Streams.Add(info);
        }

        /// <summary>
        /// Band of a stream in rho (exactly <see cref="HumanWorld.TryBand"/>) and its share of world power,
        /// evaluating the layer width only once.
        /// </summary>
        bool Band(Civ c, double year, out float rhoLo, out float rhoHi, out float share)
        {
            rhoLo = rhoHi = share = 0;
            if (!world.TryUnits(c, year, out float lo, out float width)) return false;
            float k = world.RhoPerUnit(year);
            rhoLo = HumanWorld.HumanRho0 + lo * k;
            rhoHi = HumanWorld.HumanRho0 + (lo + width) * k;
            share = width / HumanWorld.TotalUnits;
            return true;
        }

        /// <summary>
        /// Fade by relative position across the human layer (0 inner .. 1 outer boundary): full inside, easing
        /// to a third toward the outer side, and to nothing right at the layer's outer boundary.
        /// </summary>
        static float OuterFade(float x)
        {
            float toward = Mathf.Lerp(1f, 0.35f, SmoothStep(0.55f, 1f, x));
            return toward * (1f - SmoothStep(0.97f, 1.0f, x));
        }

        /// <summary>Edge alpha of a band of the given width (rho): narrow, crowded bands have softer edges.</summary>
        static float EdgeAlphaFor(float width) =>
            EdgeAlpha * Mathf.Lerp(EdgeNarrowAlpha, 1f, SmoothStep(0, EdgeFullWidth, width));

        // ------------------------------------------------------------------ our path

        /// <summary>
        /// The inner envelope of the human layer: the inner edge of the westernmost (most relevant) stream at
        /// each moment. Hand-overs between streams follow HumanWorld's emergence slides; a light smoothing
        /// takes the remaining kinks off.
        /// </summary>
        void BuildEnvelope()
        {
            envStart = world.HistomapStartYear;
            double envEnd = envStart;
            foreach (Civ c in world.Civs)
            {
                if (c != world.Humanity && c.Samples.Count > 0) envEnd = Math.Max(envEnd, c.Samples[c.Samples.Count - 1].Year);
            }

            int n = Math.Max(2, (int)Math.Ceiling((envEnd - envStart) / EnvelopeGridYears) + 1);
            float[] env = new float[n];
            for (int i = 0; i < n; i++)
            {
                double year = envStart + i * EnvelopeGridYears;
                float min = float.NaN;
                foreach (Civ c in world.Civs)
                {
                    if (!world.TryUnits(c, year, out float lo, out float width) || width < EnvelopeMinWidthUnits) continue;
                    if (float.IsNaN(min) || lo < min) min = lo;
                }

                env[i] = min;
            }

            // bridge moments without any stream (gaps in the digitization) with the neighbors
            float carry = 0;
            for (int i = 0; i < n; i++)
            {
                if (float.IsNaN(env[i])) env[i] = carry;
                else carry = env[i];
            }

            envelope = BoxSmooth(BoxSmooth(env, EnvelopeSmoothRadius), EnvelopeSmoothRadius);
        }

        float EnvelopeUnits(double year)
        {
            double x = (year - envStart) / EnvelopeGridYears;
            if (x <= 0) return envelope[0];
            if (x >= envelope.Length - 1) return envelope[envelope.Length - 1];
            int i = (int)x;
            return Mathf.Lerp(envelope[i], envelope[i + 1], (float)(x - i));
        }

        /// <summary>
        /// Our path from the first civilizations to the present moment, appended to the level jump and the
        /// prehistoric inner edge so the whole path from the tree of life to now is one continuous line.
        /// </summary>
        void BuildHistoryPath()
        {
            double gridEnd = envStart + (envelope.Length - 1) * EnvelopeGridYears;
            StreamSampler.Build(now, envStart, gridEnd, true, null, envStart, double.PositiveInfinity,
                StreamSampler.MaxYearStep, samples);

            // the prehistoric part ends at the hand-over, where the history part begins
            float lastU = path.Count > 0 ? path[path.Count - 1].Data.x : float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                StreamSample s = samples[i];
                if (s.U >= lastU) continue;
                float rho = world.Rho(EnvelopeUnits(s.Year), s.Year);
                path.Add(new LinePoint(new Vector3(s.U, GraphStyle.HumansY, rho), Tint(PathAlpha), PathWidthPx,
                    PathWidthWorld, HistoryPathIntensity));
            }

            Lines.AddPolyline(path, CivBandsLayer.LineageId, 1f);
        }

        // ------------------------------------------------------------------ lineage links

        /// <summary>
        /// A stream emerges geometrically out of its first living parent (HumanWorld); every other parent
        /// gets a thin curved link from its band to the child's first full-width moment, so causes that
        /// are not neighbors (a second parent, or a parent that ended generations earlier) stay visible.
        /// </summary>
        void BuildLinks()
        {
            foreach (Civ c in world.Civs)
            {
                if (c == world.Humanity || c.Samples.Count == 0 || c.Parents == null || c.Parents.Length == 0) continue;
                double first = c.Samples[0].Year;
                double emerge = first - HumanWorld.EmergeYears;
                if (!world.TryBand(c, first, out float clo, out float chi)) continue;
                float rhoChild = 0.5f * (clo + chi);

                Civ direct = null;
                foreach (string pid in c.Parents)
                {
                    if (pid != null && world.ById.TryGetValue(pid, out Civ p) && p != c && Covers(p, emerge))
                    {
                        direct = p;
                        break;
                    }
                }

                foreach (string pid in c.Parents)
                {
                    if (pid == null || !world.ById.TryGetValue(pid, out Civ p) || p == c || p == direct ||
                        p.Samples.Count == 0) continue;

                    double from;
                    if (Covers(p, emerge)) from = emerge;
                    else if (!p.Extant && p.Samples[p.Samples.Count - 1].Year < emerge) from = p.Samples[p.Samples.Count - 1].Year;
                    else continue; // the parent begins after the child: no causal link

                    double gap = first - from;
                    if (gap > MaxLinkGapYears) continue;
                    float rhoParent = Center(p, from);
                    float alpha = LinkAlpha * (1f - LinkGapFade * Mathf.Clamp01((float)(gap / LinkGapFadeYears)));
                    AddLink(from, rhoParent, first, rhoChild, alpha, GraphIds.Civ(c.Index));
                }
            }
        }

        void AddLink(double from, float rhoFrom, double to, float rhoTo, float alpha, int id)
        {
            double gap = to - from;
            int n = Mathf.Clamp(Mathf.CeilToInt((float)(gap / 2)), 16, 96);
            float lift = Mathf.Min(LinkLiftMax, LinkLiftBase + LinkLiftPerRho * Mathf.Abs(rhoTo - rhoFrom));
            pts.Clear();
            for (int i = 0; i <= n; i++)
            {
                float f = i / (float)n;
                float e = f * f * (3f - 2f * f);
                double year = from + gap * f;
                float u = DeepTime.Arc(now - year);
                float y = GraphStyle.HumansY + LinkY + lift * Mathf.Sin(Mathf.PI * f);
                // fade in and out at the ends so links read as threads between bands
                float a = alpha * Mathf.Min(1f, 4f * Mathf.Min(f, 1f - f) + 0.35f);
                pts.Add(new LinePoint(new Vector3(u, y, Mathf.Lerp(rhoFrom, rhoTo, e)), Tint(a), LinkWidthPx,
                    LinkWidthWorld, LinkIntensity));
            }

            Lines.AddPolyline(pts, id, 1f);
        }

        // ------------------------------------------------------------------ wars

        /// <summary>
        /// Each war scars the streams that fought it: a tick across the band where it began (and ended),
        /// a thin line along the band through its span and a faint overlay. Brightness grows with deaths.
        /// </summary>
        void BuildWars()
        {
            for (int k = 0; k < world.Wars.Count; k++)
            {
                War war = world.Wars[k];
                if (war == null || string.IsNullOrEmpty(war.id)) continue;
                int id = CivBandsLayer.WarId(k);
                double start = war.startYear, end = Math.Max(war.endYear, war.startYear);
                float intensity = Mathf.Clamp(
                    WarIntensityMin + WarIntensityPerDecade * (float)Math.Log10(Math.Max(war.deaths, 1e4) / 1e4),
                    WarIntensityMin, WarIntensityMax);
                WarInfo info = new WarInfo { War = war, Id = id, Rho = float.NaN };

                foreach (string cid in war.civs ?? Array.Empty<string>())
                {
                    if (cid == null || !world.ById.TryGetValue(cid, out Civ c)) continue;
                    (double cs, double ce) = world.Span(c);
                    double a = Math.Max(start, cs), b = Math.Min(end, ce);
                    if (a > b) continue;
                    if (float.IsNaN(info.Rho) && world.TryBand(c, start, out float slo, out float shi))
                    {
                        info.Rho = 0.5f * (slo + shi);
                    }

                    AddWarMark(c, a, b, id, intensity);
                }

                if (float.IsNaN(info.Rho))
                {
                    // nobody listed was alive at the start: fall back to the first one alive during the war
                    foreach (string cid in war.civs ?? Array.Empty<string>())
                    {
                        if (cid == null || !world.ById.TryGetValue(cid, out Civ c)) continue;
                        (double cs, double ce) = world.Span(c);
                        double a = Math.Max(start, cs);
                        if (a <= Math.Min(end, ce))
                        {
                            info.Rho = Center(c, a);
                            break;
                        }
                    }
                }

                if (float.IsNaN(info.Rho)) info.Rho = 0.5f * world.LayerWidth(start);
                Wars.Add(info);
            }
        }

        void AddWarMark(Civ c, double a, double b, int id, float intensity)
        {
            float y = GraphStyle.HumansY + WarY;
            AddWarTick(c, a, y, WarAlpha, id, intensity);
            if (b - a < 1) return;
            AddWarTick(c, b, y, WarEndTickAlpha * WarAlpha, id, intensity);

            // through the span: a center line and a faint overlay on the band, sampled like the stream itself
            knots.Clear();
            foreach (Civ.Sample s in c.Samples) knots.Add(s.Year);
            double first = c.Samples[0].Year, last = c.Samples[c.Samples.Count - 1].Year;
            StreamSampler.Build(now, a, b, false, knots, first, c.Extant ? double.PositiveInfinity : last,
                StreamSampler.MaxYearStep, samples);
            pts.Clear();
            inner.Clear();
            outer.Clear();
            colors.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                StreamSample s = samples[i];
                if (!world.TryBand(c, s.Year, out float l, out float h)) continue;
                pts.Add(new LinePoint(new Vector3(s.U, y, 0.5f * (l + h)), Tint(WarLineAlpha * WarAlpha),
                    WarLineWidthPx, EdgeWidthWorld, intensity));
                inner.Add(new Vector3(s.U, GraphStyle.HumansY + 0.002f, l));
                outer.Add(new Vector3(s.U, GraphStyle.HumansY + 0.002f, h));
                colors.Add(Tint(WarOverlayAlpha));
            }

            Lines.AddPolyline(pts, id);
            Fill.AddBand(inner, outer, colors, id, intensity);
        }

        /// <summary>A short tick across a stream at one moment (skipped where the stream has no width yet).</summary>
        void AddWarTick(Civ c, double year, float y, float alpha, int id, float intensity)
        {
            if (!world.TryBand(c, year, out float lo, out float hi) || hi - lo < 1e-5f) return;
            float u = DeepTime.Arc(now - year);
            Lines.AddSegment(new Vector3(u, y, lo), new Vector3(u, y, hi), Tint(alpha), WarTickWidthPx, EdgeWidthWorld, id,
                intensity);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// True where <see cref="HumanWorld"/> lets a child emerge from this parent: between the parent's
        /// first and last data slice (or on to the present when it is extant).
        /// </summary>
        static bool Covers(Civ p, double year)
        {
            if (p.Samples.Count == 0 || year < p.Samples[0].Year) return false;
            return p.Extant || year <= p.Samples[p.Samples.Count - 1].Year;
        }

        float Center(Civ c, double year) => world.TryBand(c, year, out float lo, out float hi) ? 0.5f * (lo + hi) : 0f;

        /// <summary>Moving average over [i - radius, i + radius], replicating the end values.</summary>
        static float[] BoxSmooth(float[] src, int radius)
        {
            int n = src.Length;
            float[] dst = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int j = -radius; j <= radius; j++) sum += src[Mathf.Clamp(i + j, 0, n - 1)];
                dst[i] = sum / (2 * radius + 1);
            }

            return dst;
        }

        static float SmoothStep(double a, double b, double x)
        {
            float t = Mathf.Clamp01((float)((x - a) / Math.Max(b - a, 1e-9)));
            return t * t * (3f - 2f * t);
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));
    }
}
