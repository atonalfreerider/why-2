using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// The scale grid of the red layer: the inflation of space drawn as a train track that grows with the universe,
    /// every line a real measurement on the map of <see cref="MatterScale"/>.
    ///
    /// Rails: one family only, so no two lines ever cross. A rail follows the matter that is a round distance from us
    /// today - 10^N metres, and the finer 2..9 x 10^N, 0.1 and 0.01 steps of a log ruler - back through time. While
    /// space is still small every rail hugs the inner track (it follows the circumference of time), then it bends
    /// outward as space inflates: outside the gravitationally bound bodies of our lineage its distance is 10^N times
    /// the cosmic scale factor (comoving), and the outermost rails, carried by the envelope, bend away hyperbolically
    /// toward the horizon. Inside a bound body (Earth, the Sun, the Milky Way, the Local Group) matter stopped riding
    /// the expansion when the body formed: those rails settle to their fixed physical radius over the body's
    /// formation and then hold still with it, so the grid follows each band's own scale exactly. Matter that fell into
    /// a bound body is pinned to its edge and fades out there.
    ///
    /// Crossbeams: between every two neighbouring rails of a level of the ruler (decades; then the 2..9 steps at a
    /// tenth of the width; then the 0.1 steps) radial ties at the same spacing as the rails, so each level is a track
    /// of square cells, and inside every cell the next level's track opens up as the camera closes in - recursively,
    /// down to the scale of the Earth. Between the knots the map is exponential, so decade cells are the same size
    /// everywhere on the map; out on the envelope, where space has stretched most, the cells open so wide that the
    /// lines fade to black. The HUD's probe (G) reads the same field: metres from our lineage, metres per world unit.
    /// Neutral grey, far below the bloom threshold, beneath the matter surfaces. Builds its own layout because it
    /// prepares concurrently with <see cref="MatterLayer"/>.
    /// </summary>
    public sealed class ExpansionGridLayer : GraphLayer
    {
        /// <summary>Anchor of the grid: tooltips of its labels and the director.</summary>
        public const string AnchorKey = "matter:_expansion";

        /// <summary>Shared key of the <see cref="MatterScale"/> the grid was built from (the HUD's probe reads it).</summary>
        public const string ScaleKey = "matter.scale";

        /// <summary>Highlight id of the whole grid (the last matter id, clear of the bands and epochs).</summary>
        public static readonly int GridId = GraphIds.Matter(998);

        // --- what is drawn ---
        /// <summary>Decades of distance drawn, 10^MinDecade (a kilometre: the finest cells) .. 10^MaxDecade metres (the observable universe is 4.4e26 m).</summary>
        const int MinDecade = 3, MaxDecade = 26;

        /// <summary>Relative weight of the four classes of line: decades, the 2..9 steps, the 0.1 steps, the 0.01 steps.</summary>
        static readonly float[] ClassWeight = { 1f, 0.55f, 0.2f, 0.12f };

        /// <summary>The finest class (0.01 steps) is generated from this decade outward (every decade: the closest tier peels it open).</summary>
        const int HundredthsFromDecade = MinDecade;

        /// <summary>The coarsest tier a class is baked into: the finer steps are details of the closer tiers.</summary>
        static readonly int[] ClassFirstTier = { 0, 0, 1, 3 };

        /// <summary>Crossbeams are drawn for these classes (the 0.01 steps have none: they are hairlines).</summary>
        const int TieClasses = 3;

        /// <summary>Crossbeams are a little fainter than the rails they span: the rails are the scale, the ties the cells.</summary>
        const float TieWeight = 0.7f;

        /// <summary>A crossbeam needs its track at least this many clock samples wide, or the ties would land on every sample.</summary>
        const float MinTieSamples = 1.5f;

        /// <summary>Vertices of crossbeams per tier, at most (finer classes are dropped first).</summary>
        const int TieVertexBudget = 200000;

        /// <summary>
        /// A decade line keeps this much of its opacity after the finer lines have taken over between it and
        /// the next decade, so the orders of magnitude stay readable; every other class fades out.
        /// </summary>
        const float DecadeFloor = 0.3f;

        // --- tiers: the same lines baked for a crowding threshold Zoom times finer per tier, crossfaded by pixel density ---
        public const int Tiers = 4;
        const float Zoom = 4f;

        /// <summary>World-unit gap below which a line is crowded out at the overview (~3 px at distance 22).</summary>
        const float CrowdGap = 0.05f;

        /// <summary>Pixel density (px per world unit) at which tier 0 is fully in; tier t at Zoom^t times more.</summary>
        const float OverviewPixelsPerUnit = 60f;

        /// <summary>A line is fully opaque once its gap is this many times the crowding threshold.</summary>
        const float EmergeRatio = 2.5f;

        /// <summary>Fainter as the gap opens beyond the threshold: opacity ~ (gap / crowd)^-Fainter.</summary>
        const float Fainter = 1.2f;

        /// <summary>World width of a line as a fraction of its gap, capped: wider as space opens.</summary>
        const float WidthPerGap = 0.05f, MaxWidthWorld = 0.08f, WidthPx = 1f;

        /// <summary>A rail fades in over this many decades as it leaves the inner track; it fades out over this many decades as it is pinned to a bound body's edge.</summary>
        const double InnerDecades = 0.5, BoundDecades = 0.3;

        // --- look ---
        const float GridAlpha = 0.3f;
        const float GridIntensity = 0.7f;
        const float GridRhoFade = 30f;
        const float HandoffExponent = 2f;
        const float CullAlpha = 0.008f;

        // --- sampling along the clock (arc): geometric out of the Big Bang, then uniform ---
        const float FirstTau = 1e-5f, TauGrowth = 1.08f, DenseUntilTau = 0.02f, ArcStep = 0.002f;

        // --- labels ---
        static readonly int[] LabelDecades = { 6, 9, 12, 15, 18, 21, 24, 26 };
        const float LabelPriority = 2.5f, LabelSize = 11f;

        const string Blurb =
            "A grid of real measurements: the inflation of space drawn as a train track. Each rail follows the matter " +
            "that lies a round distance from our own lineage today - ten metres, a hundred, and so on, to the edge of " +
            "the observable universe - back in time: while space is small the rails hug the clock, then they bend " +
            "outward as space stretches, and inside the Earth, the Sun and the Milky Way, which stopped expanding when " +
            "they formed, they hold still. Between two neighbouring rails the distance grows tenfold, and the " +
            "crossbeams space the track into square cells: inside every cell the next, finer track opens as you " +
            "close in. Space has stretched about 1,100-fold since the cosmic microwave background was released, " +
            "380,000 years after the Big Bang, and it is still stretching faster today. Press G and point at the red " +
            "layer to read the scale anywhere: metres from us, and how many metres one unit of the graph stands for.";

        /// <summary>A sample of the clock: the map's knots at that moment, the scale factor and the bound bodies forming.</summary>
        sealed class Sample
        {
            public float U, Envelope;
            public double Log10Scale, Log10Inner, Log10Universe, Handoff;
            public MatterScale.Knot[] Knots;

            /// <summary>The gravitationally bound bodies of our lineage alive at this moment: log10 radius (ascending) and how far formed (0..1).</summary>
            public double[] BoundLog;
            public float[] BoundProgress;

            /// <summary>rho of a distance, and the map's local decades per world unit there; false beyond the envelope.</summary>
            public bool Rho(double log10Radius, out float rho, out float decadesPerUnit)
            {
                rho = 0;
                decadesPerUnit = 0;
                if (log10Radius > Knots[Knots.Length - 1].Log10Radius) return false;
                for (int i = 0; i < Knots.Length - 1; i++)
                {
                    MatterScale.Knot a = Knots[i], b = Knots[i + 1];
                    if (log10Radius > b.Log10Radius) continue;
                    rho = MatterScale.RhoAt(a, b, log10Radius);
                    decadesPerUnit = (float)MatterScale.DecadesPerUnitAt(a, b, log10Radius);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Where the matter that is 10^<paramref name="log10Today"/> metres from us today lies at this moment (log10
            /// metres), and how far it is from being pinned to a bound body's edge (decades; large when free).
            /// Comoving outside the bound bodies; inside the innermost body that encloses it, it settles from comoving
            /// to its fixed radius over the body's formation; never inside the edge of a body it does not belong to.
            /// </summary>
            public double Place(double log10Today, out double freeDecades)
            {
                float f = 0;
                for (int i = 0; i < BoundLog.Length; i++)
                {
                    if (BoundLog[i] >= log10Today)
                    {
                        f = BoundProgress[i];
                        break;
                    }
                }

                double pos = log10Today + (1 - f) * Log10Scale;
                double pin = double.NegativeInfinity;
                for (int i = 0; i < BoundLog.Length && BoundLog[i] < log10Today; i++)
                {
                    pin = Math.Max(pin, BoundLog[i] + (1 - BoundProgress[i]) * Log10Scale);
                }

                freeDecades = double.IsNegativeInfinity(pin) ? 1e9 : pos - pin;
                return Math.Max(pos, pin);
            }
        }

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        LineMeshBuilder[] tiers;
        Material[] materials;
        readonly float[] lod = new float[Tiers];
        MatterScale scale;
        MatterLayout layout;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile file = MatterFile.Parse(ctx.Text(MatterLayer.DataPath), out string error);
            if (file == null)
            {
                Debug.LogWarning("[Why] ExpansionGridLayer: " + error);
                return;
            }

            layout = MatterLayout.Build(file);
            if (layout.IsEmpty) return;
            scale = new MatterScale(layout);
            List<Sample> samples = SampleClock();

            LineMeshBuilder[] builders = new LineMeshBuilder[Tiers];
            for (int t = 0; t < Tiers; t++) builders[t] = new LineMeshBuilder(32768);
            int rails = 0, ties = 0;
            int[] tieVertices = new int[Tiers];
            Thinner thin = new Thinner();
            // the last rail of each class or coarser bounds the next track of that class; a small pool of traces
            // is enough, since a rail is held only until the next rail of its class arrives
            Trace[] pool = new Trace[TieClasses + 1];
            for (int i = 0; i < pool.Length; i++) pool[i] = new Trace(samples.Count);
            Trace[] last = new Trace[TieClasses];
            List<Tie> tieList = new List<Tie>(4096);
            Trace[] labelled = new Trace[MaxDecade + 1];
            foreach ((double log10, int cls, double step) in Values())
            {
                Trace current = null;
                foreach (Trace t in pool)
                {
                    if (Array.IndexOf(last, t) < 0)
                    {
                        current = t;
                        break;
                    }
                }

                if (!BuildRail(builders, thin, current, samples, log10, cls, step)) continue;
                rails++;
                if (cls == 0 && log10 >= 0 && Math.Abs(log10 - Math.Round(log10)) < 1e-9)
                {
                    labelled[(int)Math.Round(log10)] = current.Copy();
                }

                // the crossbeams of every class this rail belongs to, across the track from the last rail of that class
                for (int c = cls; c < TieClasses; c++)
                {
                    if (last[c] != null) ties += BuildTies(builders, tieVertices, tieList, last[c], current, samples, c);
                }

                // a rail of class k is the next rail at every resolution c >= k
                for (int c = cls; c < TieClasses; c++) last[c] = current;
            }

            Register(ctx, samples, labelled, layout.BigBangYa);
            ctx.Share(ScaleKey, scale);
            tiers = builders;

            string perTier = "";
            for (int t = 0; t < Tiers; t++) perTier += (t > 0 ? ", " : "") + "tier " + t + " " + builders[t].VertexCount;
            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.ElapsedMilliseconds} ms: {samples.Count} samples, " +
                      $"{rails} rails and {ties} crossbeams; vertices {perTier}");
        }

        /// <summary>
        /// All values drawn (log10 metres today) with their class and their spacing to the next line of the ruler
        /// (decades): the log ruler, one class per level: 10^n, 2..9 x 10^n, the 0.1 steps, and (out in the
        /// fan, where the map is wide enough for them to ever resolve) the 0.01 steps. Ascending.
        /// </summary>
        static IEnumerable<(double log10, int cls, double step)> Values()
        {
            for (int n = MinDecade; n <= MaxDecade; n++)
            {
                int finest = n >= HundredthsFromDecade ? 3 : 2;
                int steps = finest == 3 ? 1000 : 100;
                int unit = steps / 10;
                for (int m = unit; m < steps; m++)
                {
                    int cls = m == unit ? 0 : m % unit == 0 ? 1 : m % (unit / 10) == 0 ? 2 : 3;
                    // a decade is judged against the next decade; a finer line against its nearest neighbour
                    // of its own class or coarser
                    int coarse = cls == 1 ? unit : cls == 2 ? unit / 10 : cls == 3 ? 1 : steps;
                    double step = cls == 0 ? 1 : Math.Log10((m + coarse) / (double)m);
                    yield return (n + Math.Log10(m / (double)unit), cls, step);
                }
            }
        }

        List<Sample> SampleClock()
        {
            List<Sample> samples = new List<Sample>(800);
            float top = layout.BigBangArc, bottom = MatterLayout.EndArc;
            float tau = FirstTau;
            while (true)
            {
                float u = top - tau;
                if (u <= bottom) break;
                samples.Add(Take(u));
                tau = tau < DenseUntilTau ? tau * TauGrowth : tau + ArcStep;
            }

            samples.Add(Take(bottom));
            return samples;
        }

        Sample Take(float u)
        {
            IReadOnlyList<MatterScale.Knot> knots = scale.Knots(u);
            MatterScale.Knot[] copy = new MatterScale.Knot[knots.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = knots[i];

            // the bound bodies of our lineage alive now, and how far each has formed (its band opens over UStart .. UFull)
            List<(double log, float progress)> bound = new List<(double, float)>(8);
            foreach (MatterBand band in layout.Bands)
            {
                if (band.IsRootHome || !band.InPath || !band.Item.Bound || band.Item.SizeM <= 0 || !band.AliveAt(u)) continue;
                float progress = u <= band.UFull ? 1f : Smooth((band.UStart - u) / Mathf.Max(band.UStart - band.UFull, 1e-9f));
                bound.Add((Math.Log10(0.5 * band.Item.SizeM), progress));
            }

            bound.Sort((x, y) => x.log.CompareTo(y.log));
            double[] boundLog = new double[bound.Count];
            float[] boundProgress = new float[bound.Count];
            for (int i = 0; i < bound.Count; i++)
            {
                boundLog[i] = bound[i].log;
                boundProgress[i] = bound[i].progress;
            }

            return new Sample
            {
                U = u,
                Envelope = scale.EnvelopeAt(u),
                Log10Scale = Math.Log10(Mathf.Max(layout.ScaleFactor(u), 1e-30f)),
                Log10Inner = Math.Log10(MatterScale.InnerMetres),
                Log10Universe = scale.Log10UniverseRadius(u),
                Handoff = Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent),
                Knots = copy,
                BoundLog = boundLog,
                BoundProgress = boundProgress
            };
        }

        /// <summary>
        /// One rail at every tier: walks the clock, placing the matter that is 10^log10 metres from us today on the
        /// map at every sample, and appends the stretches where it is resolvable, thinned to the points that shape
        /// them. Leaves the rail's trace in <paramref name="trace"/> for the crossbeams. Returns whether anything was drawn.
        /// </summary>
        static bool BuildRail(LineMeshBuilder[] builders, Thinner thin, Trace trace, List<Sample> samples, double log10, int cls, double step)
        {
            float weight = ClassWeight[cls];
            trace.Class = cls;
            trace.Log10 = log10;

            // the rail's place on the map and everything about its opacity that does not depend on the tier
            for (int j = 0; j < samples.Count; j++)
            {
                Sample s = samples[j];
                trace.Gap[j] = 0;
                trace.Base[j] = 0;
                double value = s.Place(log10, out double freeDecades);
                // while space is too small for this distance the rail rides the inner track and is not yet drawn
                double inner = s.Log10Inner;
                float emergeInner = Smooth((float)((value - inner) / InnerDecades));
                if (value < inner) value = inner;
                if (!s.Rho(value, out trace.Rho[j], out float decadesPerUnit) || decadesPerUnit <= 1e-9f) continue;
                float gap = (float)step / decadesPerUnit;
                // pinned to the edge of a body it fell into: it is that body's edge now, and fades out there
                float alpha = weight * (float)s.Handoff * emergeInner * Smooth((float)(freeDecades / BoundDecades));
                trace.Gap[j] = gap;
                trace.Base[j] = alpha;
                trace.Width[j] = Mathf.Min(WidthPerGap * gap, MaxWidthWorld);
            }

            bool drawn = false;
            for (int t = 0; t < Tiers; t++)
            {
                if (t < ClassFirstTier[cls]) continue;
                float crowd = CrowdGap / Mathf.Pow(Zoom, t);
                thin.Reset();
                bool drawnHere = false;
                for (int j = 0; j < samples.Count; j++)
                {
                    float gap = trace.Gap[j];
                    float alpha = gap > 0 ? trace.Base[j] * Emerge(gap, crowd) * Faint(gap, crowd, cls) : 0;
                    trace.Tier[t][j] = alpha;
                    if (alpha > CullAlpha)
                    {
                        thin.Add(samples[j].U, trace.Rho[j], alpha, trace.Width[j]);
                    }
                    else if (thin.Open)
                    {
                        // end the stretch softly on the first invisible sample
                        if (trace.Rho[j] > 0) thin.Add(samples[j].U, trace.Rho[j], 0, trace.Width[j]);
                        drawnHere |= thin.Flush(builders[t]);
                    }
                }

                drawnHere |= thin.Flush(builders[t]);
                drawn |= drawnHere;
            }

            return drawn;
        }

        /// <summary>A crossbeam: a radial tie across one track at one moment, with its opacity at every tier.</summary>
        sealed class Tie
        {
            public float U, RhoA, RhoB, Width;
            public readonly float[] Alpha = new float[Tiers];
        }

        /// <summary>
        /// The crossbeams of class <paramref name="cls"/> across the track between two neighbouring rails: ties spaced
        /// along the clock by the track's own local width (square cells), drawn where both rails are, as strong as the
        /// fainter of the two, resolving and fading with the track's width like the rails. Returns the number drawn.
        /// </summary>
        static int BuildTies(LineMeshBuilder[] builders, int[] tieVertices, List<Tie> list, Trace a, Trace b, List<Sample> samples, int cls)
        {
            list.Clear();
            double acc = 0;
            bool open = false;
            float weight = ClassWeight[cls] * TieWeight;
            for (int j = 1; j < samples.Count; j++)
            {
                float gap = b.Rho[j] - a.Rho[j];
                bool active = a.Base[j] > CullAlpha && b.Base[j] > CullAlpha && gap > 1e-6f;
                float dSigma = (samples[j - 1].U - samples[j].U) * GraphWarp.BasePath.SigmaPerArc;
                if (!active || gap < MinTieSamples * dSigma)
                {
                    open = false;
                    continue;
                }

                if (!open)
                {
                    open = true;
                    acc = 0;
                    continue;
                }

                double before = acc;
                acc += dSigma / gap;
                if (Math.Floor(acc) == Math.Floor(before)) continue;

                // interpolate the tie between the two samples where the count crossed an integer
                float f = (float)((Math.Floor(acc) - before) / (acc - before));
                Tie tie = new Tie
                {
                    U = Mathf.Lerp(samples[j - 1].U, samples[j].U, f),
                    RhoA = Mathf.Lerp(a.Rho[j - 1], a.Rho[j], f),
                    RhoB = Mathf.Lerp(b.Rho[j - 1], b.Rho[j], f),
                    Width = Mathf.Min(WidthPerGap * gap, MaxWidthWorld),
                };
                float baseAlpha = Mathf.Min(a.Base[j], b.Base[j]) / Mathf.Max(Mathf.Min(ClassWeight[a.Class], ClassWeight[b.Class]), 1e-6f) * weight;
                for (int t = 0; t < Tiers; t++)
                {
                    float crowd = CrowdGap / Mathf.Pow(Zoom, t);
                    float alpha = t < ClassFirstTier[cls] ? 0 : baseAlpha * Emerge(gap, crowd) * Faint(gap, crowd, cls);
                    // no tie across a track whose rails are not both there at this tier
                    if (a.Tier[t][j] <= CullAlpha || b.Tier[t][j] <= CullAlpha) alpha = 0;
                    tie.Alpha[t] = alpha;
                }

                list.Add(tie);
            }

            int drawn = 0;
            LinePoint[] pts = new LinePoint[2];
            for (int t = 0; t < Tiers; t++)
            {
                if (tieVertices[t] >= TieVertexBudget) continue;
                foreach (Tie tie in list)
                {
                    float alpha = tie.Alpha[t];
                    if (alpha <= CullAlpha) continue;
                    Color32 c = Tint(alpha);
                    pts[0] = new LinePoint(new Vector3(tie.U, GraphStyle.MatterY, tie.RhoA), c, WidthPx, tie.Width, 1f);
                    pts[1] = new LinePoint(new Vector3(tie.U, GraphStyle.MatterY, tie.RhoB), c, WidthPx, tie.Width, 1f);
                    builders[t].AddPolyline(pts, GridId);
                    tieVertices[t] += 4;
                    drawn++;
                }
            }

            return drawn;
        }

        /// <summary>One rail's place and opacity at every sample: tier-independent, and per tier (for the crossbeams).</summary>
        sealed class Trace
        {
            public readonly float[] Rho, Gap, Base, Width;
            public readonly float[][] Tier;
            public int Class;
            public double Log10;

            public Trace(int samples)
            {
                Rho = new float[samples];
                Gap = new float[samples];
                Base = new float[samples];
                Width = new float[samples];
                Tier = new float[Tiers][];
                for (int t = 0; t < Tiers; t++) Tier[t] = new float[samples];
            }

            public Trace Copy()
            {
                Trace c = new Trace(Rho.Length) { Class = Class, Log10 = Log10 };
                Array.Copy(Rho, c.Rho, Rho.Length);
                Array.Copy(Gap, c.Gap, Gap.Length);
                Array.Copy(Base, c.Base, Base.Length);
                Array.Copy(Width, c.Width, Width.Length);
                for (int t = 0; t < Tiers; t++) Array.Copy(Tier[t], c.Tier[t], Rho.Length);
                return c;
            }
        }

        /// <summary>
        /// Keeps only the samples that shape a stretch: a point is dropped while the stretch since the last kept
        /// point stays within a small deviation (in world-ish units: clock arc length and rho) and its opacity
        /// within a small step; the ends are always kept. Smooth lines shed most of their samples.
        /// </summary>
        sealed class Thinner
        {
            const float Deviation = 0.006f, AlphaStep = 0.035f, MaxSpan = 0.35f;

            struct Candidate
            {
                public float X, Rho, Alpha, Width, U;
            }

            readonly List<LinePoint> kept = new List<LinePoint>(256);
            readonly List<Candidate> pending = new List<Candidate>(64);
            Candidate last;

            public bool Open => kept.Count > 0;

            public void Reset()
            {
                kept.Clear();
                pending.Clear();
            }

            public void Add(float u, float rho, float alpha, float width)
            {
                Candidate c = new Candidate { X = (1f - u) * GraphWarp.BasePath.SigmaPerArc, Rho = rho, Alpha = alpha, Width = width, U = u };
                if (kept.Count == 0)
                {
                    Keep(c);
                    return;
                }

                // would the pending points still lie on the chord from the last kept point to this one?
                bool keepPrevious = false;
                if (pending.Count > 0)
                {
                    float dx = c.X - last.X, dr = c.Rho - last.Rho;
                    float len = Mathf.Sqrt(dx * dx + dr * dr);
                    if (len > MaxSpan) keepPrevious = true;
                    else
                    {
                        // long pending runs are straight by construction: checking every few points is enough
                        int stride = Mathf.Max(1, pending.Count / 12);
                        for (int i = pending.Count - 1; i >= 0; i -= stride)
                        {
                            Candidate p = pending[i];
                            float t = len > 1e-9f ? ((p.X - last.X) * dx + (p.Rho - last.Rho) * dr) / (len * len) : 0;
                            float ax = last.X + t * dx - p.X, ar = last.Rho + t * dr - p.Rho;
                            float chordAlpha = last.Alpha + t * (c.Alpha - last.Alpha);
                            if (ax * ax + ar * ar > Deviation * Deviation || Mathf.Abs(chordAlpha - p.Alpha) > AlphaStep)
                            {
                                keepPrevious = true;
                                break;
                            }
                        }
                    }
                }

                if (keepPrevious)
                {
                    Keep(pending[pending.Count - 1]);
                }

                pending.Add(c);
            }

            void Keep(Candidate c)
            {
                kept.Add(new LinePoint(new Vector3(c.U, GraphStyle.MatterY, c.Rho), Tint(c.Alpha), WidthPx, c.Width, 1f));
                last = c;
                pending.Clear();
            }

            public bool Flush(LineMeshBuilder builder)
            {
                if (pending.Count > 0) Keep(pending[pending.Count - 1]);
                bool ok = kept.Count >= 2;
                if (ok) builder.AddPolyline(kept, GridId);
                Reset();
                return ok;
            }
        }

        void Register(GraphContext ctx, List<Sample> samples, Trace[] labelled, double bigBangYa)
        {
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Scale of space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            // decade rails are labeled near the present, where they have settled at their distance today
            foreach (int n in LabelDecades)
            {
                if (n > MaxDecade || labelled[n] == null) continue;
                Trace rail = labelled[n];
                for (int j = samples.Count - 1; j >= 0; j--)
                {
                    if (rail.Base[j] < 0.3f * ClassWeight[0]) continue;
                    AddLabel(ctx, "10<sup>" + n + "</sup> m", new Vector3(samples[j].U, GraphStyle.MatterY, rail.Rho[j]));
                    break;
                }
            }
        }

        static void AddLabel(GraphContext ctx, string text, Vector3 data)
        {
            ctx.Labels.Add(new LabelSpec
            {
                Text = text,
                Data = data,
                Priority = LabelPriority,
                SizePx = LabelSize,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(5, 6),
                AnchorKey = AnchorKey,
                HandoffFade = true,
                Ids = IdRange.Single(GridId)
            });
        }

        public override void Upload(GraphContext ctx)
        {
            if (tiers == null) return;
            // neutral like the axes: hue is reserved for the levels, and grey reads as space, not as matter
            Color color = GraphStyle.Axis;
            color.a = GridAlpha;
            materials = new Material[Tiers];
            for (int t = 0; t < Tiers; t++)
            {
                // above the band fills (which would veil the finer tracks up close), additive with the band edges
                Material m = GraphMaterials.Line(color, GridIntensity, GraphMaterials.QueueMatter + 1, true, GridRhoFade, 0f);
                GraphMaterials.FadeBeforeHumanBranch(m);
                lod[t] = t == 0 ? 1f : 0f;
                GraphMaterials.SetAlpha(m, lod[t]);
                materials[t] = m;
                AddMesh("MatterScaleGrid" + t, tiers[t].ToMesh("MatterScaleGrid" + t), m);
            }

            tiers = null;
        }

        /// <summary>Crossfades the tiers by pixel density: the tier baked for the current zoom carries the grid.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (materials == null || rig == null || rig.Cam == null) return;
            float pixelsPerUnit = Screen.height / (2f * Mathf.Max(rig.Pose.Distance, 1e-3f) * Mathf.Tan(rig.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float z = Mathf.Log(Mathf.Max(pixelsPerUnit / OverviewPixelsPerUnit, 1e-3f)) / Mathf.Log(Zoom);
            for (int t = 0; t < Tiers; t++)
            {
                float target = TierWeight(t, z);
                if (Mathf.Abs(target - lod[t]) < 0.01f) continue;
                lod[t] = target;
                GraphMaterials.SetAlpha(materials[t], target);
            }
        }

        /// <summary>Weight of tier t at zoom level z (log base Zoom of the pixel density over the overview's): hats that sum to 1.</summary>
        public static float TierWeight(int t, float z)
        {
            if (t == 0 && z <= 0) return 1f;
            if (t == Tiers - 1 && z >= t) return 1f;
            return Mathf.Clamp01(1f - Mathf.Abs(z - t));
        }

        /// <summary>Opacity of a line whose gap to its neighbours is <paramref name="gap"/>: 0 while crowded, 1 once EmergeRatio times the threshold.</summary>
        static float Emerge(float gap, float crowd) => Smooth(Mathf.Log(Mathf.Max(gap, 1e-9f) / crowd) / Mathf.Log(EmergeRatio));

        /// <summary>Fainter as the gap opens beyond the threshold: the widest-spaced lines are the softest.</summary>
        static float Faint(float gap, float crowd, int cls)
        {
            // a line is at its strongest just as it resolves; once its gap has opened far beyond that the finer
            // lines between it and its neighbours carry the grid, and it is redundant: it fades out fast
            // (a bell in log gap: a line lives in a band of about one zoom step around the resolution limit)
            float excess = Mathf.Log(Mathf.Max(gap / (crowd * EmergeRatio), 1f));
            float faint = Mathf.Exp(-Fainter * excess * excess);
            return cls == 0 ? Mathf.Max(faint, DecadeFloor) : faint;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));
    }
}
