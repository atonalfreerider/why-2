using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>
    /// Famous historical figures (figures.json) as highlighted lifelines inside their civilization's band
    /// (worker thread). Each is drawn from birth to death (or to the present) the way the statistical lives
    /// are, but bright enough to bloom: a status curve from <see cref="SmvModel"/> with a base and wealth
    /// that grow with prominence, kept near the top of the ranking (close to the band center) so it stays
    /// visible among the crowd. Before its stream appears (Franklin before the United States, Murasaki
    /// Shikibu before Japan is a stream of its own) a figure lives in the stream's first parent.
    ///
    /// Registers "figure:&lt;id&gt;" anchors and a name label at each figure's peak, and records every drawn
    /// lifeline in <see cref="Paths"/>.
    /// </summary>
    public sealed class FigureLines
    {
        const double StepYears = 1;

        /// <summary>Longer lives are data errors (a missing death year read as 0, say) and are skipped.</summary>
        const double MaxLifeYears = 125;

        /// <summary>Figure ids of a civ end where its lifeline ids begin (<see cref="GraphIds"/>).</summary>
        const int MaxFiguresPerCiv = GraphIds.CivLifelineBase - GraphIds.CivFigureBase;

        // look: bright, wide, blooming
        const float WidthPx = 2f;
        const float ChildWidthPx = 1.4f;
        const float WidthWorld = 0.0012f;
        const float Intensity = 3.5f;
        const float ChildIntensity = 1.8f;
        const float Alpha = 0.95f;
        const float ChildAlpha = 0.6f;

        // status: base = min(10, 6 + 0.4 x prominence), wealth = prominence / 10
        const float BaseValue = 6f;
        const float BasePerProminence = 0.4f;
        const float MaxProminence = 10f;
        const float DefaultProminence = 5f;
        const int DefaultTier = 2;

        // near the top of the ranking: the most prominent closest to the center
        const float OffsetNear = 0.1f;
        const float OffsetFar = 0.3f;
        const float OffsetJitter = 0.04f;

        static readonly float[] TierPriority = { 36f, 18f, 6f };
        static readonly float[] TierSize = { 13f, 12f, 11f };

        public readonly LineMeshBuilder Lines = new LineMeshBuilder(24_000);

        /// <summary>Every drawn figure's lifeline by year, for modules that follow or connect figures.</summary>
        public readonly FigurePaths Paths = new FigurePaths();

        /// <summary>Figures drawn / skipped (unknown stream, no band during their life, or implausible dates).</summary>
        public int Drawn { get; private set; }

        public int Skipped { get; private set; }

        readonly HumanWorld world;
        readonly CivLives[] streams;
        readonly List<LinePoint> pts = new List<LinePoint>(128);
        readonly List<double> pathYears = new List<double>(128);
        readonly List<Vector3> pathPoints = new List<Vector3>(128);

        sealed class FigureDto
        {
            // nullable, so one incomplete entry cannot break the whole file
            public string id, name, gender, civ, role, blurb;
            public double? born, died;
            public float? prominence;
            public int? tier;
        }

        sealed class FigureFile
        {
            public List<FigureDto> figures = new List<FigureDto>();
        }

        /// <param name="world">the shared human model</param>
        /// <param name="streams">every stream's lives, indexed by <see cref="Civ.Index"/></param>
        public FigureLines(HumanWorld world, CivLives[] streams)
        {
            this.world = world;
            this.streams = streams;
        }

        /// <summary>
        /// Parses figures.json and draws every figure; a missing or broken file draws none. Samples the streams'
        /// grids, which are not thread safe: nothing else may use them meanwhile.
        /// </summary>
        public void Build(string json, Action<LabelSpec> addLabel)
        {
            FigureFile file = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    file = JsonConvert.DeserializeObject<FigureFile>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Why] LifelinesLayer could not parse figures: {e.Message}");
                }
            }

            if (file?.figures == null) return;
            Dictionary<string, int> perCiv = new Dictionary<string, int>();
            foreach (FigureDto f in file.figures)
            {
                if (f == null || string.IsNullOrEmpty(f.id) || string.IsNullOrEmpty(f.civ)) continue;
                // the k-th figure of a stream keeps its id whether or not earlier ones could be drawn
                perCiv.TryGetValue(f.civ, out int k);
                perCiv[f.civ] = k + 1;
                if (f.born.HasValue && k < MaxFiguresPerCiv && world.ById.TryGetValue(f.civ, out Civ civ) &&
                    Draw(f, f.born.Value, civ, k, addLabel))
                {
                    Drawn++;
                }
                else
                {
                    Skipped++;
                }
            }
        }

        bool Draw(FigureDto f, double born, Civ civ, int k, Action<LabelSpec> addLabel)
        {
            double now = world.NowYear;
            double end = f.died ?? now; // the living run into the present moment
            if (end <= born || end - born > MaxLifeYears) return false;

            bool male = string.Equals(f.gender, "m", StringComparison.OrdinalIgnoreCase);
            float side = male ? 1f : -1f;
            float prominence = Mathf.Clamp(f.prominence ?? DefaultProminence, 1f, MaxProminence);
            float baseValue = Mathf.Min(10f, BaseValue + BasePerProminence * prominence);
            float wealth = prominence / MaxProminence;
            float jitter = ((k * 0.618034f) % 1f - 0.5f) * OffsetJitter;
            float share = Mathf.Lerp(OffsetNear, OffsetFar, 1f - (prominence - 1f) / (MaxProminence - 1f)) + jitter;
            int id = GraphIds.Figure(civ.Index, k);

            float peakValue = -1f;
            Vector3 peak = Vector3.zero;
            pts.Clear();
            pathYears.Clear();
            pathPoints.Clear();
            int steps = Math.Max(1, (int)Math.Ceiling((end - born) / StepYears));
            for (int i = 0; i <= steps; i++)
            {
                double t = Math.Min(end, born + i * StepYears);
                if (!Locate(civ, t, out float u, out float center, out float envWomen, out float envMen))
                {
                    Flush(id); // outside every band: a gap, if the figure outlived its stream or came early
                    continue;
                }

                float age = (float)(t - born);
                bool adult = age >= SmvModel.AdultAge;
                float value = SmvModel.Value(male, baseValue, age, false, 0, wealth);
                float env = male ? envMen : envWomen;
                float offset = adult ? share * env : CivLives.ChildSpread * age / SmvModel.AdultAge * env;
                Vector3 data = new Vector3(u, SmvModel.Height(value, age), center + side * offset);
                Color32 tint = male ? LifelineMeshes.MenTint : LifelineMeshes.WomenTint;
                tint.a = (byte)((adult ? Alpha : ChildAlpha) * 255f);
                pts.Add(new LinePoint(data, tint, adult ? WidthPx : ChildWidthPx, WidthWorld,
                    adult ? Intensity : ChildIntensity));
                pathYears.Add(t);
                pathPoints.Add(data);
                if (value > peakValue)
                {
                    peakValue = value;
                    peak = data;
                }
            }

            Flush(id);
            if (peakValue < 0) return false;
            Paths.Add(f.id, pathYears, pathPoints);
            Register(f, born, id, peak, addLabel);
            return true;
        }

        void Flush(int id)
        {
            if (pts.Count >= 2) Lines.AddPolyline(pts, id);
            pts.Clear();
        }

        /// <summary>Clock arc, band center and envelope of the figure's stream at a moment, or of the first parent alive then.</summary>
        bool Locate(Civ civ, double year, out float u, out float center, out float envWomen, out float envMen)
        {
            if (Locate(Stream(civ), year, out u, out center, out envWomen, out envMen)) return true;
            foreach (string pid in civ.Parents)
            {
                if (pid != null && world.ById.TryGetValue(pid, out Civ parent) && parent != civ &&
                    Locate(Stream(parent), year, out u, out center, out envWomen, out envMen))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>From the stream's grid where it has one (exactly where its lifelines are), else from the model.</summary>
        bool Locate(CivLives s, double year, out float u, out float center, out float envWomen, out float envMen)
        {
            u = center = envWomen = envMen = 0;
            if (s == null) return false;
            if (s.Grid != null && year >= s.Grid.Time(0) && year <= s.LineEnd)
            {
                s.Grid.At(year, out u, out center, out envWomen, out envMen);
                return true;
            }

            if (!s.Sample(year, out center, out envWomen, out envMen)) return false;
            u = DeepTime.Arc(world.NowYear - year);
            return true;
        }

        /// <summary>The simulated stream of a civ (null if it has none).</summary>
        CivLives Stream(Civ civ) => civ.Index >= 0 && civ.Index < streams.Length ? streams[civ.Index] : null;

        void Register(FigureDto f, double born, int id, Vector3 peak, Action<LabelSpec> addLabel)
        {
            int tier = Mathf.Clamp(f.tier ?? DefaultTier, 1, 3);
            string name = string.IsNullOrEmpty(f.name) ? f.id : f.name;
            Anchor anchor = new Anchor
            {
                Key = "figure:" + f.id,
                Label = name + " (" + Years(born, f.died) + ")",
                Blurb = f.blurb,
                Level = GraphLevel.Humans,
                YearsAgo = world.NowYear - born,
                EndYearsAgo = f.died.HasValue ? Math.Max(0, world.NowYear - f.died.Value) : 0,
                Y = peak.y,
                Rho = peak.z,
                Ids = IdRange.Single(id),
                Tier = tier
            };
            Anchors.Register(anchor);

            addLabel(new LabelSpec
            {
                Text = name,
                Data = peak,
                Priority = TierPriority[tier - 1],
                SizePx = TierSize[tier - 1],
                Color = GraphStyle.Text,
                PixelOffset = new Vector2(5, 8),
                AnchorKey = anchor.Key,
                Ids = anchor.Ids
            });
        }

        /// <summary>"1809-1865", "100-44 BCE", "63 BCE-14 CE", "b. 1961" (negative years are BCE).</summary>
        public static string Years(double born, double? died)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string Year(double y) => Math.Abs(Math.Round(y)).ToString("0", c);
            if (!died.HasValue) return "b. " + Year(born) + (born < 0 ? " BCE" : "");
            double d = died.Value;
            if (born < 0 && d < 0) return Year(born) + "-" + Year(d) + " BCE";
            if (born < 0) return Year(born) + " BCE-" + Year(d) + " CE";
            return Year(born) + "-" + Year(d);
        }
    }
}
