using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Humans.Lives;

namespace Why.Humans.Social
{
    /// <summary>
    /// The popular people's lifelines (worker thread), drawn the way <see cref="FigureLines"/> draws the historical
    /// figures: from birth to death or the present, on the status curve of <see cref="SmvModel"/>, women on the inner
    /// side and men on the outer. A person lives in their region's band: the United States stream (the same envelope
    /// the population's lifelines follow), India for South Asia, China and Japan together for East Asia, the European
    /// streams together for Europe, and for the rest of the world a band just outside the outermost stream. Fame
    /// (followers) sets their base status and brightness; the lines are white, so popularity reads apart from the
    /// civilizations' blue and from capital's gold. People who are also historical figures are drawn there instead.
    /// </summary>
    public sealed class PopularLines
    {
        public const string PathsKey = "social.paths";
        public const int IdBase = 900_000;
        const float WidthPx = 1.1f, WidthWorld = 0.0005f;
        const float OffsetNear = 0.06f, OffsetFar = 0.42f;

        public static readonly Color32 Tint = new Color32(230, 244, 255, 235);
        public readonly LineMeshBuilder Lines = new LineMeshBuilder(32_000);
        /// <summary>Each drawn person's last point (data space): the present for the living, where the clouds take over.</summary>
        public readonly Dictionary<int, Vector3> Ends = new Dictionary<int, Vector3>();
        /// <summary>Each region's band at the present: (u, center rho, half width).</summary>
        public readonly Vector3[] RegionNow;
        public int Drawn { get; private set; }

        readonly HumanWorld world;
        readonly CivLives[] streams;
        readonly SocialPeople people;
        readonly Civ[][] regionCivs;
        readonly List<LinePoint> pts = new List<LinePoint>(96);

        public PopularLines(HumanWorld world, CivLives[] streams, SocialPeople people)
        {
            this.world = world; this.streams = streams; this.people = people;
            regionCivs = new Civ[people.regions.Length][];
            RegionNow = new Vector3[people.regions.Length];
            for (int r = 0; r < regionCivs.Length; r++)
            {
                var list = new List<Civ>();
                foreach (string id in people.regions[r].civs) if (world.ById.TryGetValue(id, out Civ c)) list.Add(c);
                regionCivs[r] = list.ToArray();
            }
        }

        public static int Id(SocialPeople.Person p) => IdBase + p.Index;

        /// <summary>Samples the streams' grids, which are not thread safe: run it on the thread that builds the figures.</summary>
        public void Build(Action<LabelSpec> addLabel)
        {
            double now = world.NowYear;
            for (int r = 0; r < RegionNow.Length; r++)
                if (Locate(r, now, out float u, out float lo, out float hi)) RegionNow[r] = new Vector3(u, .5f * (lo + hi), .5f * (hi - lo));
            foreach (var p in people.people)
            {
                if (!string.IsNullOrEmpty(p.fig) || !p.born.HasValue) continue;
                if (Draw(p, now, addLabel)) Drawn++;
            }
        }

        bool Draw(SocialPeople.Person p, double now, Action<LabelSpec> addLabel)
        {
            double born = p.born.Value, end = p.died ?? now;
            if (end <= born || end - born > 125) return false;
            bool male = p.g == "m";
            float side = p.g == "f" ? -1 : p.g == "m" ? 1 : ((p.Index & 1) == 0 ? 1 : -1);
            float fame = p.Fame, prominence = 1 + 9 * fame;
            float baseValue = Mathf.Min(10f, 6f + .4f * prominence), wealth = fame;
            float share = Mathf.Lerp(OffsetFar, OffsetNear, fame) + (((p.Index * .618034f) % 1f) - .5f) * .05f;
            // Subtle among the population: only the most followed approach the figures' brightness.
            float intensity = .45f + 1.2f * fame * fame, alphaAdult = .14f + .38f * fame;
            int id = Id(p);
            pts.Clear();
            Vector3 last = default; bool any = false;
            for (double t = born; ; t = Math.Min(end, t + 1))
            {
                if (Locate(p.RegionIndex, t, out float u, out float lo, out float hi))
                {
                    float age = (float)(t - born), center = .5f * (lo + hi), env = .5f * (hi - lo);
                    bool adult = age >= SmvModel.AdultAge;
                    float value = p.Group ? baseValue : SmvModel.Value(male, baseValue, age, false, 0, wealth);
                    float offset = adult || p.Group ? share * env : CivLives.ChildSpread * age / SmvModel.AdultAge * env;
                    var data = new Vector3(u, SmvModel.Height(value, p.Group ? 30 : age), center + side * offset);
                    Color32 tint = Tint; tint.a = (byte)((adult ? alphaAdult : alphaAdult * .5f) * 255);
                    pts.Add(new LinePoint(data, tint, adult ? WidthPx : WidthPx * .7f, WidthWorld, adult ? intensity : intensity * .5f));
                    last = data; any = true;
                }
                else if (pts.Count >= 2) { Lines.AddPolyline(pts, id); pts.Clear(); }
                if (t >= end) break;
            }
            if (pts.Count >= 2) Lines.AddPolyline(pts, id);
            pts.Clear();
            if (!any) return false;
            Ends[p.Index] = last;
            if (fame > .55f)
                addLabel(new LabelSpec { Text = p.name, Data = last, Priority = 4 + 10 * fame, Rank = fame * 10, SizePx = 10.5f,
                    Color = new Color(.85f, .93f, 1f), PixelOffset = new Vector2(5, 6), Ids = IdRange.Single(id), Hidden = false });
            return true;
        }

        /// <summary>The region's band at a year: clock arc and the rho range of its streams (or beyond them all).</summary>
        bool Locate(int region, double year, out float u, out float lo, out float hi)
        {
            u = 0; lo = float.MaxValue; hi = float.MinValue; bool found = false;
            var civs = regionCivs[region];
            if (civs.Length > 0)
            {
                foreach (var civ in civs)
                    if (LocateCiv(civ, year, out float cu, out float center, out float w, out float m))
                    { u = cu; lo = Mathf.Min(lo, center - w); hi = Mathf.Max(hi, center + m); found = true; }
                return found;
            }
            // The rest of the world: a band just outside every stream alive then.
            foreach (var civ in world.Civs)
                if (civ.Extant && LocateCiv(civ, year, out float cu, out float center, out float w, out float m))
                { u = cu; lo = Mathf.Min(lo, center - w); hi = Mathf.Max(hi, center + m); found = true; }
            if (!found) return false;
            float width = Mathf.Max(.02f, .12f * (hi - lo));
            lo = hi + width * .4f; hi = lo + width;
            return true;
        }

        bool LocateCiv(Civ civ, double year, out float u, out float center, out float envWomen, out float envMen)
        {
            u = center = envWomen = envMen = 0;
            var s = civ.Index >= 0 && civ.Index < streams.Length ? streams[civ.Index] : null;
            if (s == null) return false;
            if (s.Grid != null && year >= s.Grid.Time(0) && year <= s.LineEnd) { s.Grid.At(year, out u, out center, out envWomen, out envMen); return true; }
            if (!s.Sample(year, out center, out envWomen, out envMen)) return false;
            u = DeepTime.Arc(world.NowYear - year);
            return true;
        }
    }
}
