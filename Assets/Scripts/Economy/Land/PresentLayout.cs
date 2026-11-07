using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Why.Economy.Data;

namespace Why.Economy.Land
{
    /// <summary>
    /// The present, taken apart (design "exploded", E1): the crest's cut face pulled forward out of the wave into the empty
    /// future beyond it, extruded into depth and taken apart like an engineering exploded drawing. The state's bedrock drops
    /// to the bottom, the four tiers lift apart into four terraces, and each industry's ridge swells into a hill.
    /// <para>The frame is the land's own (land-local x across, y up, z into the future): the present's near edge stands
    /// <see cref="Gap0"/> past the crest (<see cref="NearZ"/>), and depth d runs from there into the future. Every part is
    /// built in the crest's own units (its x and its strata's y, d in exploded units) and placed by a root pose
    /// (<see cref="Root"/>): at the choreography's start the parts lie on the crest's cut face, scaled to it and half a unit
    /// deep; at its end they stand <see cref="S"/> times larger, <see cref="D"/> deep and apart. Area is true: every terrace
    /// is D deep and S times its tier's band on the crest wide (its share of the year's value added), and every hill's
    /// footprint is S times its industry's band. Heights are the wave's, an assumed order of dependence, not measured.</para>
    /// <para>Terrace k is the column of the cut face over its tier's band, the riser before the band included (the last
    /// also takes the summit's fall), so the four columns tile the face from the hillside's foot to its far edge and come
    /// apart along x without overlap. The bedrock is the state's slab under all of them, its top on the valley floor.</para>
    /// <para>Pure C# over <see cref="HillLandscape"/> and the industries' data: no Unity objects, safe on any thread; the
    /// choreography's poses are pure functions of its clock (<see cref="Root"/>, <see cref="Rise"/>, <see cref="Reveal"/>,
    /// <see cref="Guides"/>), so the smoke test checks them (<see cref="AreaCheck"/>, <see cref="Coincidence"/>,
    /// <see cref="Disjoint"/>).</para>
    /// </summary>
    public sealed class PresentLayout
    {
        /// <summary>Scale over the crest's own x and y; the extrusion's depth; the gap from the crest to the present's near
        /// edge; the gaps between neighbouring terraces (x) and each tier's extra lift (y); the bedrock's drop; how far the
        /// bedrock reaches past the terraces toward the viewer (its apron, the valley floor of the exploded present).</summary>
        public const float S = 2, D = 200, Gap0 = 40, Gx = 14, Gy = 16, Gb = 24, Apron = 60;

        /// <summary>The bedrock's depth (exploded units) per $1T a year of the state's outlays.</summary>
        public const float StateDepthPerTrillion = 4;

        /// <summary>The choreography's length, forward and in reverse (s).</summary>
        public const float Seconds = 6.4f, ReverseSeconds = 2f;

        /// <summary>The choreography's phases, in order.</summary>
        public enum Phase
        {
            /// <summary>The camera flies to the cut face; the wave eases to context.</summary>
            Face,

            /// <summary>The copy of the cut face slides forward from the crest to the present's near edge.</summary>
            Cut,

            /// <summary>It grows to full scale and full depth about the foot.</summary>
            Grow,

            /// <summary>The tiers lift apart, the bedrock drops; the guides show where each part came from.</summary>
            Separate,

            /// <summary>The hills rise out of the terraces.</summary>
            Rise,

            /// <summary>The firms arrive on their hills; the labels show.</summary>
            Arrive
        }

        /// <summary>Each phase's start and end (s), by <see cref="Phase"/>.</summary>
        static readonly float[] PhaseStart = { 0, 1, 2, 3.4f, 4.6f, 5.4f }, PhaseEnd = { 1, 2, 3.4f, 4.6f, 5.4f, 6.4f };

        /// <summary>The cut face's copy before it grows: this deep (exploded units).</summary>
        const float StartDepth = .5f;

        /// <summary>A hill's centre along d (share of <see cref="D"/>) and its reach either side: its relief falls to 0 a tenth
        /// of the way in from the near edge and at the far edge.</summary>
        public const float HillCenter = .55f, HillReach = .45f;

        /// <summary>A hill's relief fades this close to its terrace's band edges (crest units): no cliff where a ridge wider
        /// than its band meets the riser.</summary>
        const float EdgeTaper = 3;

        /// <summary>The summit's fall past the top tier's band (crest units): HillLandscape's riser and its back slope.</summary>
        const float BackFall = 20;

        /// <summary>The bedrock's margin past the hillside's foot and past the last terrace (exploded units).</summary>
        const float BedrockMargin = 10;

        /// <summary>The bedrock's depth (crest units) when the year has no state account.</summary>
        const float DefaultBedrock = 18;

        /// <summary>Industries below this share of their year's value added stay flat ground in their band.</summary>
        public const double FlatShare = .005;

        /// <summary>One terrace: a tier's column of the cut face (crest units), its band (the plateau, value added), the strata's
        /// top over the band, its hills and its value added ($B).</summary>
        public sealed class Terrace
        {
            public int Tier;
            public float X0, X1, Band0, Band1, Top, MaxRelief;
            public double ValueAdded;
            public readonly List<Hill> Hills = new List<Hill>();

            /// <summary>The plateau's footprint at full scale (exploded units²): its band's width × S × D.</summary>
            public float Area => S * (Band1 - Band0) * D;

            /// <summary>Its separation at the choreography's end (exploded units).</summary>
            public Vector3 Offset => new Vector3(Tier * Gx, Tier * Gy, 0);
        }

        /// <summary>One industry's hill (crest units): its band's centre and half width (value added), its ridge's half width
        /// and peak (HillLandscape's), and its value added ($B).</summary>
        public sealed class Hill
        {
            public int Industry, Tier;
            public float Center, Band, Half, Peak;
            public double ValueAdded;
            public bool Flat;

            /// <summary>The footprint at full scale (exploded units²): its band × S × D.</summary>
            public float Area => S * 2 * Band * D;
        }

        /// <summary>A root's pose in the land's frame: a crest-unit point maps to Position + Scale × point.</summary>
        public readonly struct RootPose
        {
            public readonly Vector3 Position, Scale;

            public RootPose(Vector3 position, Vector3 scale)
            {
                Position = position;
                Scale = scale;
            }

            public Vector3 Apply(Vector3 local) => Position + Vector3.Scale(Scale, local);
        }

        public readonly HillLandscape Land;
        public readonly int Year;

        /// <summary>The crest's z and the present's near edge (land-local).</summary>
        public readonly float CrestZ, NearZ;

        /// <summary>The four terraces, raw materials (index 0) to tech (3).</summary>
        public readonly Terrace[] Terraces = new Terrace[4];

        /// <summary>Every market industry's hill.</summary>
        public readonly List<Hill> Hills = new List<Hill>();

        /// <summary>The bedrock's span across (crest units) and its depth under the valley floor (crest units).</summary>
        public readonly float BedrockX0, BedrockX1, BedrockDepth;

        /// <summary>The market's value added ($B) and the dollars a year 1,000 units² of footprint stand for ($B).</summary>
        public readonly double ValueAdded, PerThousand;

        public PresentLayout(HillLandscape land, IReadOnlyList<Industry> industries, double stateOutlays)
        {
            Land = land;
            Year = land.Year;
            CrestZ = land.CrestZ;
            NearZ = CrestZ + Gap0;
            for (int k = 1; k <= 4; k++)
            {
                Vector2 band = land.TierBand(Year, k);
                Terraces[k - 1] = new Terrace
                {
                    Tier = k, Band0 = band.x, Band1 = band.y, Top = land.LayerTop(Year, k),
                    X0 = k == 1 ? HillLandscape.FootX : land.TierBand(Year, k - 1).y, X1 = k == 4 ? band.y + BackFall : band.y
                };
            }

            double total = 0;
            for (int i = 0; i < industries.Count && i < land.Hills.Length; i++)
            {
                int tier = land.Hills[i].Tier;
                if (tier < 1 || tier > 4) continue;
                Vector2 b = land.IndustryBand(Year, i), r = land.IndustryRidge(Year, i);
                var hill = new Hill { Industry = i, Tier = tier, Center = b.x, Band = b.y, Half = Mathf.Max(r.x, 1e-3f), Peak = r.y, ValueAdded = Math.Max(0, industries[i].ValueAdded.GrowthAt(Year)) };
                total += hill.ValueAdded;
                Hills.Add(hill);
                Terraces[tier - 1].Hills.Add(hill);
                Terraces[tier - 1].ValueAdded += hill.ValueAdded;
            }

            ValueAdded = total;
            foreach (Hill h in Hills) h.Flat = total <= 0 || h.ValueAdded / total < FlatShare;
            foreach (Terrace t in Terraces)
            {
                float peak = 0;
                foreach (Hill h in t.Hills) if (!h.Flat) peak = Mathf.Max(peak, h.Peak);
                t.MaxRelief = peak;
            }

            BedrockX0 = HillLandscape.FootX - BedrockMargin / S;
            BedrockX1 = Terraces[3].X1 + (4 * Gx + BedrockMargin) / S;
            BedrockDepth = stateOutlays > 0 ? (float)(stateOutlays / 1000 * StateDepthPerTrillion / S) : DefaultBedrock;
            float area = 0;
            foreach (Terrace t in Terraces) area += t.Area;
            PerThousand = area > 0 ? 1000 * total / area : 0;
        }

        // ------------------------------------------------------------------ the ground

        /// <summary>The top of the crest's layer stack up to a tier at x (crest units): the cut face (0 = the valley floor).</summary>
        public float Profile(int tier, float x) => Land.LayerAt(Year, tier, x);

        /// <summary>A hill's falloff along d (0..1): a cosine bump centred at <see cref="HillCenter"/> of the depth.</summary>
        public static float Falloff(float d)
        {
            float u = (d - HillCenter * D) / (HillReach * D);
            return u <= -1 || u >= 1 ? 0 : .5f + .5f * Mathf.Cos(u * Mathf.PI);
        }

        /// <summary>The hills' relief above terrace k's plateau at (x, d), crest units: the highest of its industries' ridges
        /// (HillLandscape's profile), times the falloff along d, fading at the band's edges; flat industries are ground.</summary>
        public float Relief(int tier, float x, float d)
        {
            Terrace t = Terraces[tier - 1];
            if (x <= t.Band0 || x >= t.Band1) return 0;
            float f = Falloff(d);
            if (f <= 0) return 0;
            float r = 0;
            List<Hill> hills = t.Hills;
            for (int i = 0; i < hills.Count; i++)
            {
                Hill h = hills[i];
                if (h.Flat) continue;
                float u = (x - h.Center) / h.Half;
                if (u <= -1 || u >= 1) continue;
                float v = 1 - u * u;
                r = Mathf.Max(r, h.Peak * v * v);
            }

            return r * f * Smooth(Mathf.Min(x - t.Band0, t.Band1 - x) / EdgeTaper);
        }

        /// <summary>A point on terrace k's ground (crest-unit x, exploded d) at the choreography's end, land-local: its plateau
        /// plus the hills' relief, lifted by <paramref name="lift"/> exploded units.</summary>
        public Vector3 OnTerrace(int tier, float x, float d, float lift = 0)
        {
            Terrace t = Terraces[tier - 1];
            float y = x >= t.Band0 && x <= t.Band1 ? t.Top + Relief(tier, x, d) : Profile(tier, x);
            return Root(tier, Seconds).Apply(new Vector3(x, y, d)) + Vector3.up * lift;
        }

        // ------------------------------------------------------------------ the choreography (pure functions of its clock)

        static float Smooth(float v)
        {
            v = Mathf.Clamp01(v);
            return v * v * (3 - 2 * v);
        }

        /// <summary>A phase's progress at the clock (0..1, eased).</summary>
        public static float Progress(Phase phase, float seconds)
        {
            int p = (int)phase;
            return Smooth((seconds - PhaseStart[p]) / (PhaseEnd[p] - PhaseStart[p]));
        }

        /// <summary>The phase the clock is in.</summary>
        public static Phase PhaseAt(float seconds)
        {
            for (int p = 0; p < PhaseEnd.Length; p++)
                if (seconds < PhaseEnd[p]) return (Phase)p;
            return Phase.Arrive;
        }

        /// <summary>When a phase starts (s).</summary>
        public static float StartOf(Phase phase) => PhaseStart[(int)phase];

        /// <summary>A part's separation at the choreography's end (exploded units): the bedrock (0) drops, tier k lifts and
        /// moves across.</summary>
        public static Vector3 Offset(int part) => part == 0 ? new Vector3(0, -Gb, 0) : new Vector3(part * Gx, part * Gy, 0);

        /// <summary>
        /// A root's pose at the clock (part 0 the bedrock, 1-4 the tiers): on the crest's face at scale 1 and half a unit deep,
        /// slid forward to the present's near edge (Cut), grown to S and D about the foot (Grow), then apart (Separate).
        /// </summary>
        public RootPose Root(int part, float seconds)
        {
            float slide = Progress(Phase.Cut, seconds), grow = Progress(Phase.Grow, seconds), apart = Progress(Phase.Separate, seconds);
            Vector3 position = new Vector3(0, 0, CrestZ + Gap0 * slide) + Offset(part) * apart;
            Vector3 scale = Vector3.Lerp(new Vector3(1, 1, StartDepth / D), new Vector3(S, S, 1), grow);
            return new RootPose(position, scale);
        }

        /// <summary>The hills' relief scale (0 flat in their plateaus, 1 risen).</summary>
        public static float Rise(float seconds) => Progress(Phase.Rise, seconds);

        /// <summary>The objects' arrival (0 absent, 1 arrived).</summary>
        public static float Reveal(float seconds) => Progress(Phase.Arrive, seconds);

        /// <summary>The guides' share of their row (their fullest, during the separation): they show as the parts come apart
        /// and fall to .12 of an alpha (a third of it) as the hills rise.</summary>
        public static float Guides(float seconds)
        {
            float shown = Smooth((Progress(Phase.Separate, seconds) - .6f) / .4f);
            return Mathf.Lerp(shown, .12f / .35f, Rise(seconds));
        }

        // ------------------------------------------------------------------ the checks (design §10, A1-A3)

        /// <summary>
        /// A1, area is true: each terrace's share of the footprints against the tier's output share (<paramref name="tierShares"/>,
        /// InstantModel's; NaN where not known: the terrace's own value added is used), and each hill's share of its terrace
        /// against its industry's share of the tier's value added. The largest error in points of a share, the count of hills
        /// that are not flat, and one line per term over 1 point to <paramref name="problems"/>.
        /// </summary>
        public float AreaCheck(double[] tierShares, out int hills, List<string> problems)
        {
            float area = 0, worst = 0;
            hills = 0;
            foreach (Terrace t in Terraces) area += t.Area;
            for (int k = 0; k < 4; k++)
            {
                Terrace t = Terraces[k];
                double expected = tierShares != null && k < tierShares.Length && !double.IsNaN(tierShares[k]) ? tierShares[k] : ValueAdded > 0 ? t.ValueAdded / ValueAdded : 0;
                float err = 100 * Mathf.Abs((area > 0 ? t.Area / area : 0) - (float)expected);
                worst = Mathf.Max(worst, err);
                if (err > 1) problems?.Add($"terrace {t.Tier}'s footprint is {100 * t.Area / Mathf.Max(1e-6f, area):0.0}% of the present's, its tier's output {100 * expected:0.0}%");
                float tierArea = 0;
                foreach (Hill h in t.Hills) tierArea += h.Area;
                foreach (Hill h in t.Hills)
                {
                    if (!h.Flat) hills++;
                    double share = t.ValueAdded > 0 ? h.ValueAdded / t.ValueAdded : 0;
                    float e = 100 * Mathf.Abs((tierArea > 0 ? h.Area / tierArea : 0) - (float)share);
                    worst = Mathf.Max(worst, e);
                    if (e > 1) problems?.Add($"industry {h.Industry}'s hill is {100 * h.Area / Mathf.Max(1e-6f, tierArea):0.0}% of terrace {t.Tier}, its value added {100 * share:0.0}% of the tier's");
                }
            }

            return worst;
        }

        /// <summary>
        /// A2, it comes out of the crest: at the clock's start every terrace's near-face top (its column of the cut face) and
        /// the bedrock's top lie on the crest's cut face (HillLandscape.LayerAt of the whole stack at the crest's z). The
        /// largest distance (land units), sampled every half unit across.
        /// </summary>
        public float Coincidence()
        {
            float worst = 0;
            for (int k = 1; k <= 4; k++)
            {
                Terrace t = Terraces[k - 1];
                RootPose pose = Root(k, 0);
                for (float x = t.X0; x <= t.X1 + 1e-3f; x += .5f)
                {
                    Vector3 at = pose.Apply(new Vector3(x, Profile(k, x), 0)), face = new Vector3(x, Profile(4, x), CrestZ);
                    worst = Mathf.Max(worst, Vector3.Distance(at, face));
                }
            }

            RootPose bed = Root(0, 0);
            for (float x = Terraces[0].X0; x <= Terraces[3].X1 + 1e-3f; x += .5f)
                worst = Mathf.Max(worst, Vector3.Distance(bed.Apply(new Vector3(x, 0, 0)), new Vector3(x, Profile(0, x), CrestZ)));
            return worst;
        }

        /// <summary>A part's bounds at the clock's end (land-local min and max): a terrace with its hills risen, the bedrock with
        /// its apron.</summary>
        public void Bounds(int part, out Vector3 min, out Vector3 max)
        {
            RootPose pose = Root(part, Seconds);
            Vector3 a, b;
            if (part == 0)
            {
                a = new Vector3(BedrockX0, -BedrockDepth, 0);
                b = new Vector3(BedrockX1, 0, D + Apron);
            }
            else
            {
                Terrace t = Terraces[part - 1];
                a = new Vector3(t.X0, 0, 0);
                b = new Vector3(t.X1, Mathf.Max(t.Top, Profile(part, t.X0)) + t.MaxRelief, D);
            }

            min = pose.Apply(a);
            max = pose.Apply(b);
        }

        /// <summary>
        /// A3, it is apart: at the clock's end the terraces' and the bedrock's bounds are pairwise disjoint, neighbouring
        /// terraces at least <see cref="Gx"/> apart across and every terrace at least <see cref="Gy"/> above the bedrock. One
        /// clause per pair (its gap and axis) in <paramref name="log"/>, one line per failure in <paramref name="problems"/>;
        /// the smallest gap.
        /// </summary>
        public float Disjoint(StringBuilder log, List<string> problems)
        {
            var min = new Vector3[5];
            var max = new Vector3[5];
            for (int p = 0; p < 5; p++) Bounds(p, out min[p], out max[p]);
            float smallest = float.MaxValue;
            for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
            {
                float gx = Mathf.Max(min[b].x - max[a].x, min[a].x - max[b].x);
                float gy = Mathf.Max(min[b].y - max[a].y, min[a].y - max[b].y);
                float gz = Mathf.Max(min[b].z - max[a].z, min[a].z - max[b].z);
                float gap = Mathf.Max(gx, Mathf.Max(gy, gz));
                string axis = gap == gx ? "x" : gap == gy ? "y" : "z";
                string name = (a == 0 ? "bedrock" : "T" + a) + "/T" + b;
                log?.Append(log.Length > 0 ? ", " : "").Append(name).Append(' ').Append(axis).Append(' ').Append(gap.ToString("0.0", LandFacts.Ci));
                smallest = Mathf.Min(smallest, gap);
                const float slack = 1e-3f;
                if (gap <= 0) problems?.Add($"{name} overlap at the choreography's end (gap {gap:0.00})");
                else if (a == 0 && gy < Gy - slack) problems?.Add($"{name}: the terrace stands {gy:0.00} above the bedrock, less than {Gy}");
                else if (a > 0 && b == a + 1 && gx < Gx - slack) problems?.Add($"{name}: {gx:0.00} apart across, less than {Gx}");
            }

            return smallest;
        }
    }
}
