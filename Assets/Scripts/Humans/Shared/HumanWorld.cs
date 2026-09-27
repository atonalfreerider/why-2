using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Humans
{
    /// <summary>A stream of the human layer (a civilization, or prehistoric humanity).</summary>
    public sealed class Civ
    {
        public int Index;            // stable index -> GraphIds.Civ(Index)
        public string Id, Key, Name, Region, Blurb, Source;
        public string[] Parents = Array.Empty<string>();
        public double StartYear, EndYear; // calendar; EndYear = +inf when extant

        /// <summary>Histomap samples sorted by calendar year: left position and width in Histomap units.</summary>
        public readonly List<Sample> Samples = new List<Sample>();

        public bool Extant => double.IsPositiveInfinity(EndYear);

        public struct Sample
        {
            public double Year;
            public float Lo, Width;
        }
    }

    public sealed class LifeTable
    {
        public string id;
        public double fromYear, toYear, e0, femaleE0Advantage;
        public double[] ages, survival;
        public string source;
    }

    public sealed class War
    {
        public string id, name, blurb;
        public double startYear, endYear, deaths, maleExcessMortality;
        public string[] civs = Array.Empty<string>();
    }

    public sealed class Generation
    {
        public string id, name;
        public double from, to;
    }

    /// <summary>
    /// Shared model of the BLUE layer, built once and published through the graph context as
    /// "humans.world". Civilization bands come from the Histomap (Sparks 1931, powerByYearsAgo.json)
    /// plus civilizations.json's modern extension; widths are relative power. The whole human layer is
    /// scaled radially by world population (log), so the stream of humanity widens as it grows.
    ///
    /// Radial coordinate: rho = HumanRho0 + histomapPosition * RhoPerUnit(year). West (position 0) is
    /// innermost.
    /// </summary>
    public sealed class HumanWorld
    {
        public const string PowerPath = "powerByYearsAgo";
        public const string CivPath = "Data/civilizations";
        public const string DemographyPath = "Data/demography";
        public const string SharedKey = "humans.world";

        public const float TotalUnits = 1699f;
        public const float HumanRho0 = 0.0f;

        /// <summary>Homo sapiens emerges from the Hominidae lineage (years ago).</summary>
        public const double SapiensYearsAgo = 300_000;

        /// <summary>Years over which a stream emerges from (or dissolves into) its neighbors.</summary>
        public const double EmergeYears = 30;

        public readonly List<Civ> Civs = new List<Civ>();
        public readonly Dictionary<string, Civ> ById = new Dictionary<string, Civ>();
        public readonly Dictionary<string, Civ> ByKey = new Dictionary<string, Civ>();
        public readonly List<LifeTable> LifeTables = new List<LifeTable>();
        public readonly List<War> Wars = new List<War>();
        public readonly List<Generation> UsGenerations = new List<Generation>();
        public double SexRatioAtBirth = 1.05;

        public double NowYear;
        public double EpochYear = 2024;

        /// <summary>The prehistoric stream (Homo sapiens before the Histomap begins).</summary>
        public Civ Humanity { get; private set; }

        /// <summary>First calendar year covered by the Histomap.</summary>
        public double HistomapStartYear { get; private set; }

        readonly List<(double year, double pop)> worldPop = new List<(double, double)>();
        readonly List<(double year, double rate)> birthRate = new List<(double, double)>();

        sealed class Slot
        {
            public float Item1, Item2;
        }

        sealed class CivDto
        {
            public string id, key, name, region, blurb, source;
            public double? startYear, endYear;
            public string[] parents;
        }

        sealed class CivFile
        {
            public double epochYear = 2024;
            public float totalWidth = 1699;
            public List<CivDto> civs = new List<CivDto>();
            public Dictionary<string, Dictionary<string, Slot>> extension = new Dictionary<string, Dictionary<string, Slot>>();
        }

        sealed class DemographyFile
        {
            public List<double[]> worldPopulation = new List<double[]>();
            public List<double[]> birthRatePer1000 = new List<double[]>();
            public List<LifeTable> lifeTables = new List<LifeTable>();
            public double sexRatioAtBirth = 1.05;
            public List<War> wars = new List<War>();
            public List<Generation> usGenerations = new List<Generation>();
        }

        /// <summary>Parses the three data files. Thread safe (pure managed code).</summary>
        public static HumanWorld Load(string powerJson, string civJson, string demographyJson, double nowYear)
        {
            HumanWorld w = new HumanWorld { NowYear = nowYear };

            Dictionary<string, Dictionary<string, Slot>> power = Parse(powerJson,
                new Dictionary<string, Dictionary<string, Slot>>());
            CivFile civFile = Parse(civJson, new CivFile());
            DemographyFile demo = Parse(demographyJson, new DemographyFile());
            w.EpochYear = civFile.epochYear;

            // merge the digitized Histomap with the extension (extension wins for the same slice/key)
            Dictionary<double, Dictionary<string, Slot>> slices = new Dictionary<double, Dictionary<string, Slot>>();
            void Merge(Dictionary<string, Dictionary<string, Slot>> src)
            {
                foreach (KeyValuePair<string, Dictionary<string, Slot>> kv in src)
                {
                    if (!double.TryParse(kv.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out double ya)) continue;
                    double year = w.EpochYear - ya;
                    if (!slices.TryGetValue(year, out Dictionary<string, Slot> slice))
                    {
                        slices[year] = slice = new Dictionary<string, Slot>();
                    }

                    foreach (KeyValuePair<string, Slot> s in kv.Value) slice[s.Key] = s.Value;
                }
            }

            Merge(power);
            Merge(civFile.extension ?? new Dictionary<string, Dictionary<string, Slot>>());

            // civ metadata: every key that has data, described by civilizations.json when available
            Dictionary<string, CivDto> meta = new Dictionary<string, CivDto>();
            foreach (CivDto c in civFile.civs ?? new List<CivDto>())
            {
                if (c?.key != null) meta[c.key] = c;
            }

            HashSet<string> keys = new HashSet<string>(slices.Values.SelectMany(s => s.Keys));

            w.Humanity = new Civ
            {
                Index = 0, Id = "humanity", Key = "Humanity", Name = "Homo sapiens",
                Region = "Africa, then the world",
                Blurb = "Our species: foragers who spread out of Africa, learned to farm and settled into the first cities.",
                Source = "Population: HYDE 3.2 / Our World in Data",
                StartYear = nowYear - SapiensYearsAgo
            };
            w.Civs.Add(w.Humanity);

            foreach (string key in keys.OrderBy(k => FirstYear(slices, k)).ThenBy(k => k, StringComparer.Ordinal))
            {
                meta.TryGetValue(key, out CivDto m);
                Civ civ = new Civ
                {
                    Index = w.Civs.Count,
                    Key = key,
                    Id = m?.id ?? key.ToLowerInvariant().Replace(' ', '_'),
                    Name = m?.name ?? key,
                    Region = m?.region,
                    Blurb = m?.blurb,
                    Source = m?.source ?? "Histomap (Sparks 1931)",
                    Parents = m?.parents ?? Array.Empty<string>()
                };

                foreach (KeyValuePair<double, Dictionary<string, Slot>> slice in slices.OrderBy(s => s.Key))
                {
                    if (slice.Value.TryGetValue(key, out Slot s) && s.Item2 > 0)
                    {
                        civ.Samples.Add(new Civ.Sample { Year = slice.Key, Lo = s.Item1, Width = s.Item2 });
                    }
                }

                if (civ.Samples.Count == 0) continue;
                civ.StartYear = civ.Samples[0].Year;
                double last = civ.Samples[civ.Samples.Count - 1].Year;
                // streams present in the newest slice continue to the present moment
                civ.EndYear = last >= w.EpochYear - 1e-6 ? double.PositiveInfinity : last;
                w.Civs.Add(civ);
            }

            foreach (Civ c in w.Civs)
            {
                w.ById[c.Id] = c;
                w.ByKey[c.Key] = c;
            }

            w.HistomapStartYear = w.Civs.Count > 1 ? w.Civs.Skip(1).Min(c => c.StartYear) : -2000;
            w.Humanity.EndYear = w.HistomapStartYear;
            w.Humanity.Samples.Add(new Civ.Sample { Year = w.Humanity.StartYear, Lo = 0, Width = TotalUnits });
            w.Humanity.Samples.Add(new Civ.Sample { Year = w.HistomapStartYear, Lo = 0, Width = TotalUnits });

            foreach (double[] p in demo.worldPopulation ?? new List<double[]>())
            {
                if (p != null && p.Length >= 2) w.worldPop.Add((p[0], p[1]));
            }

            if (w.worldPop.Count == 0)
            {
                // fallback (OWID/HYDE, rounded)
                w.worldPop.AddRange(new[]
                {
                    (-10000.0, 4.4e6), (-3000.0, 45e6), (-2000.0, 72e6), (-1000.0, 115e6), (1.0, 232e6),
                    (1000.0, 323e6), (1500.0, 500e6), (1800.0, 985e6), (1900.0, 1.65e9), (1950.0, 2.5e9),
                    (2000.0, 6.1e9), (2024.0, 8.1e9)
                });
            }

            // deep prehistory before the first data point
            w.worldPop.Add((nowYear - SapiensYearsAgo, 1e5));
            w.worldPop.Add((nowYear - 70_000, 1e6));
            w.worldPop.Sort((a, b) => a.year.CompareTo(b.year));

            foreach (double[] p in demo.birthRatePer1000 ?? new List<double[]>())
            {
                if (p != null && p.Length >= 2) w.birthRate.Add((p[0], p[1]));
            }

            w.birthRate.Sort((a, b) => a.year.CompareTo(b.year));
            if (demo.lifeTables != null) w.LifeTables.AddRange(demo.lifeTables.Where(t => t?.survival != null && t.ages != null));
            if (demo.wars != null) w.Wars.AddRange(demo.wars.Where(x => x != null));
            if (demo.usGenerations != null) w.UsGenerations.AddRange(demo.usGenerations.Where(x => x != null));
            if (demo.sexRatioAtBirth > 0.5) w.SexRatioAtBirth = demo.sexRatioAtBirth;
            return w;
        }

        static double FirstYear(Dictionary<double, Dictionary<string, Slot>> slices, string key) =>
            slices.Where(s => s.Value.ContainsKey(key)).Select(s => s.Key).DefaultIfEmpty(0).Min();

        static T Parse<T>(string json, T fallback) where T : class
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            try
            {
                return JsonConvert.DeserializeObject<T>(json) ?? fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] HumanWorld could not parse {typeof(T).Name}: {e.Message}");
                return fallback;
            }
        }

        // ---------------------------------------------------------------- population

        /// <summary>World population (people) at a calendar year, log-interpolated.</summary>
        public double WorldPopulation(double year) => LogInterp(worldPop, year);

        /// <summary>Crude birth rate per 1000 at a calendar year (default 38 pre-modern).</summary>
        public double BirthRatePer1000(double year)
        {
            if (birthRate.Count == 0) return year < 1850 ? 38 : year < 1950 ? 32 : 20;
            return Interp(birthRate, year);
        }

        /// <summary>Share of world power of a stream at a year (0..1), from its band width.</summary>
        public double PowerShare(Civ c, double year) =>
            TryUnits(c, year, out float _, out float width) ? width / TotalUnits : 0;

        /// <summary>
        /// Population proxy for a stream: world population x power share. (Relative power is not
        /// population, but it is the only per-civilization series we have; see Docs/DATA.md.)
        /// </summary>
        public double Population(Civ c, double year) => WorldPopulation(year) * PowerShare(c, year);

        /// <summary>Life table covering a birth year (nearest era when out of range).</summary>
        public LifeTable LifeTableFor(double year)
        {
            LifeTable best = null;
            foreach (LifeTable t in LifeTables)
            {
                if (year >= t.fromYear && year < t.toYear) return t;
                if (best == null || Math.Abs(Mid(t) - year) < Math.Abs(Mid(best) - year)) best = t;
            }

            return best;

            static double Mid(LifeTable t) => 0.5 * (t.fromYear + t.toYear);
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>Width of the human layer today (world units at rhoScale 1).</summary>
        public const float WidthNow = 5f;

        /// <summary>Exponent of the width/population relation (1 = proportional).</summary>
        public const double WidthExponent = 0.8;

        /// <summary>
        /// Total radial width of the human layer at a year. It follows world population almost linearly,
        /// so the stream of humanity starts as a thread and explodes outward with population growth.
        /// </summary>
        public float LayerWidth(double year)
        {
            double pop = Math.Max(WorldPopulation(year), 1e4);
            double now = Math.Max(WorldPopulation(NowYear), 1e9);
            return (float)(0.004 + WidthNow * Math.Pow(pop / now, WidthExponent));
        }

        public float RhoPerUnit(double year) => LayerWidth(year) / TotalUnits;

        /// <summary>Radial coordinate of a Histomap position at a year.</summary>
        public float Rho(float histomapUnits, double year) => HumanRho0 + histomapUnits * RhoPerUnit(year);

        /// <summary>
        /// Band of a stream in Histomap units at a calendar year: interpolated between samples, emerging
        /// from its parent's band over <see cref="EmergeYears"/> before its first sample and dissolving
        /// after its last one. Returns false when the stream does not exist at that year.
        /// </summary>
        public bool TryUnits(Civ c, double year, out float lo, out float width)
        {
            lo = width = 0;
            List<Civ.Sample> s = c.Samples;
            if (s.Count == 0) return false;

            double first = s[0].Year, last = s[s.Count - 1].Year;
            double end = c.Extant ? double.PositiveInfinity : last + EmergeYears;
            double start = c == Humanity ? c.StartYear : first - EmergeYears;
            if (year < start || year > end) return false;

            if (c == Humanity)
            {
                lo = 0;
                width = TotalUnits;
                return true;
            }

            if (year < first)
            {
                // emerge from the parent's band (or from nothing at the first sample's center)
                float f = (float)((year - start) / EmergeYears);
                f = f * f * (3 - 2 * f);
                float center = s[0].Lo + s[0].Width * 0.5f;
                float from = center;
                foreach (string pid in c.Parents)
                {
                    if (ById.TryGetValue(pid, out Civ p) && p != c && p.Samples.Count > 0 &&
                        TryUnitsNoEmerge(p, year, out float plo, out float pw))
                    {
                        from = plo + pw * 0.5f;
                        break;
                    }
                }

                float mid = Mathf.Lerp(from, center, f);
                width = s[0].Width * f;
                lo = mid - width * 0.5f;
                return true;
            }

            if (year > last)
            {
                if (c.Extant)
                {
                    lo = s[s.Count - 1].Lo;
                    width = s[s.Count - 1].Width;
                    return true;
                }

                float f = 1 - (float)((year - last) / EmergeYears);
                f = f * f * (3 - 2 * f);
                float center = s[s.Count - 1].Lo + s[s.Count - 1].Width * 0.5f;
                width = s[s.Count - 1].Width * f;
                lo = center - width * 0.5f;
                return true;
            }

            return Sampled(s, year, out lo, out width);
        }

        bool TryUnitsNoEmerge(Civ c, double year, out float lo, out float width)
        {
            lo = width = 0;
            List<Civ.Sample> s = c.Samples;
            if (s.Count == 0) return false;
            if (c == Humanity)
            {
                if (year < c.StartYear || year > c.EndYear) return false;
                width = TotalUnits;
                return true;
            }

            if (year < s[0].Year) return false;
            if (year > s[s.Count - 1].Year)
            {
                if (!c.Extant) return false;
                lo = s[s.Count - 1].Lo;
                width = s[s.Count - 1].Width;
                return true;
            }

            return Sampled(s, year, out lo, out width);
        }

        static bool Sampled(List<Civ.Sample> s, double year, out float lo, out float width)
        {
            int i = 0;
            while (i < s.Count - 1 && s[i + 1].Year < year) i++;
            if (i == s.Count - 1)
            {
                lo = s[i].Lo;
                width = s[i].Width;
                return true;
            }

            Civ.Sample a = s[i], b = s[i + 1];
            float t = (float)((year - a.Year) / Math.Max(b.Year - a.Year, 1e-6));
            t = Mathf.Clamp01(t);
            // gaps in the digitization (missing slices) are bridged by interpolation
            lo = Mathf.Lerp(a.Lo, b.Lo, t);
            width = Mathf.Lerp(a.Width, b.Width, t);
            return true;
        }

        /// <summary>Band of a stream in data-space rho at a year.</summary>
        public bool TryBand(Civ c, double year, out float rhoLo, out float rhoHi)
        {
            rhoLo = rhoHi = 0;
            if (!TryUnits(c, year, out float lo, out float width)) return false;
            float k = RhoPerUnit(year);
            rhoLo = HumanRho0 + lo * k;
            rhoHi = HumanRho0 + (lo + width) * k;
            return true;
        }

        /// <summary>Calendar years a stream exists, including its emergence and dissolution.</summary>
        public (double start, double end) Span(Civ c)
        {
            if (c == Humanity) return (c.StartYear, c.EndYear);
            if (c.Samples.Count == 0) return (0, 0);
            double end = c.Extant ? NowYear : c.Samples[c.Samples.Count - 1].Year + EmergeYears;
            return (c.Samples[0].Year - EmergeYears, end);
        }

        // ---------------------------------------------------------------- utils

        static double Interp(List<(double x, double y)> pts, double x)
        {
            if (pts.Count == 0) return 0;
            if (x <= pts[0].x) return pts[0].y;
            if (x >= pts[pts.Count - 1].x) return pts[pts.Count - 1].y;
            int i = 0;
            while (pts[i + 1].x < x) i++;
            double t = (x - pts[i].x) / (pts[i + 1].x - pts[i].x);
            return pts[i].y + (pts[i + 1].y - pts[i].y) * t;
        }

        static double LogInterp(List<(double x, double y)> pts, double x)
        {
            if (pts.Count == 0) return 0;
            if (x <= pts[0].x) return pts[0].y;
            if (x >= pts[pts.Count - 1].x)
            {
                return pts[pts.Count - 1].y;
            }

            int i = 0;
            while (pts[i + 1].x < x) i++;
            double t = (x - pts[i].x) / (pts[i + 1].x - pts[i].x);
            double la = Math.Log(Math.Max(pts[i].y, 1)), lb = Math.Log(Math.Max(pts[i + 1].y, 1));
            return Math.Exp(la + (lb - la) * t);
        }
    }
}
