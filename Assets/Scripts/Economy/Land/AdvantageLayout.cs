using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where the 2026 contest stands (design Docs/hills-design/ADVANTAGE.md, section 3), in the present's land-local frame
    /// (+z along time away from the past, +x across, +y up), past the crest of the year:
    /// <list type="bullet">
    /// <item>the supply chain's bands run outward from the crest: energy nearest the wave (the past), then tools, fabs and
    /// memory, chips, clouds, models, apps, and the buyers nearest the future; in a band, rivals stand side by side, the
    /// largest at the centre;</item>
    /// <item>the flanks: capital on one side (a column along the depth), the rest of the economy on the other (each actor in
    /// the row of the band nearest its role); the states stand on a row of plinths between the crest and the arena;</item>
    /// <item>an actor's footprint is its market cap (area proportional: radius <see cref="KR"/> times the square root of
    /// $B), its altitude its advantage (<see cref="H0"/> + <see cref="KH"/> times A, A from 0 to 7);</item>
    /// <item>a river runs on the ground from the payer's mouth to the payee's: one mouth per actor and direction, toward the
    /// weighted mean of its counterparties, so the rivers into a choke point converge on one place.</item>
    /// </list>
    /// Pure (no Unity objects but vectors): built once per land; heights take a scenario.
    /// </summary>
    public sealed class AdvantageLayout
    {
        /// <summary>The arena's middle across the land, its back edge past the crest, the footprint and height scales, the
        /// smallest drawn radius, the gaps between bands, between rivals and to the flanks (land units).</summary>
        public const float X0 = 290, Back = 64, KR = .32f, MinR = 2.6f, H0 = 2.5f, KH = 8.5f, BandGap = 14, Gap = 4.5f, FlankGap = 46;

        /// <summary>The buyers' plains and the states' plinths: radius and height (they have no market cap of their own).</summary>
        public const float PlainR = 17, PlainH = 1.2f, PlinthR = 9, UsPlinthR = 15, PlinthH = 2.2f, StateZ = 34;

        public enum Flank { Center, Capital, Economy, State }

        public sealed class Place
        {
            public Advantage.Actor Actor;
            public Vector3 Base;      // land-local centre of the footprint on the ground
            public float R;           // footprint radius
            public int Band = -1;     // band index, or -1 for the states
            public Flank Flank;
            public bool Plain => Actor.Band == "buyers";
            public bool State => Flank == Flank.State;
        }

        public readonly Advantage Model;
        public readonly float CrestZ;
        public readonly List<Place> Places = new List<Place>();
        public readonly float[] BandZ, BandMinX, BandMaxX, BandHalfDepth;
        public float MinX { get; private set; } = float.MaxValue;
        public float MaxX { get; private set; } = float.MinValue;
        public float FrontZ { get; private set; }
        public float CapitalX { get; private set; }
        public float EconomyX { get; private set; }
        readonly Dictionary<string, Place> byId = new Dictionary<string, Place>();
        readonly Dictionary<(string, bool), Vector3> mouths = new Dictionary<(string, bool), Vector3>();

        public Place Get(string id) => id != null && byId.TryGetValue(id, out Place p) ? p : null;

        public static float Radius(Advantage.Actor a)
        {
            if (a.Band == "buyers") return PlainR;
            return a.Sized ? Mathf.Max(MinR, KR * Mathf.Sqrt((float)a.SizeUsdBn)) : MinR;
        }

        /// <summary>An actor's altitude in a scenario (the plains and plinths keep theirs).</summary>
        public float Height(Place p, string scenario)
        {
            if (p.Plain) return PlainH;
            if (p.State) return PlinthH;
            double a = p.Actor.AdvantageIn(scenario);
            return double.IsNaN(a) ? H0 : H0 + KH * (float)a;
        }

        public Vector3 Summit(Place p, string scenario) => p.Base + Vector3.up * Height(p, scenario);

        public AdvantageLayout(Advantage model, float crestZ)
        {
            Model = model;
            CrestZ = crestZ;
            int nb = model.Bands.Count;
            BandZ = new float[nb]; BandMinX = new float[nb]; BandMaxX = new float[nb]; BandHalfDepth = new float[nb];
            var rows = new List<Place>[nb];
            var economyRows = new List<Place>[nb];
            for (int b = 0; b < nb; b++) { rows[b] = new List<Place>(); economyRows[b] = new List<Place>(); }
            var capital = new List<Place>();
            var states = new List<Place>();
            foreach (var a in model.Actors)
            {
                var p = new Place { Actor = a, R = Radius(a) };
                int band = model.Bands.FindIndex(x => x.Id == a.Band);
                switch (a.Flank)
                {
                    case "capital": p.Flank = Flank.Capital; capital.Add(p); break;
                    case "state": p.Flank = Flank.State; p.R = a.Id == "us-gov" ? UsPlinthR : PlinthR; states.Add(p); break;
                    case "economy": p.Flank = Flank.Economy; p.Band = Mathf.Max(0, band); economyRows[p.Band].Add(p); break;
                    default: p.Flank = Flank.Center; p.Band = band < 0 ? nb - 2 : band; rows[p.Band].Add(p); break;
                }
                Places.Add(p);
                byId[a.Id] = p;
            }

            // the bands' depths: each as deep as its largest footprint (both sides), the economy flank's included
            float z = crestZ + Back;
            for (int b = 0; b < nb; b++)
            {
                float half = 6;
                foreach (var p in rows[b]) half = Mathf.Max(half, p.R);
                foreach (var p in economyRows[b]) half = Mathf.Max(half, p.R);
                BandHalfDepth[b] = half;
                z += half;
                BandZ[b] = z;
                z += half + BandGap;
            }
            FrontZ = z - BandGap;

            // rivals side by side: the largest at the centre, then alternately to either side
            float halfWidth = 30;
            for (int b = 0; b < nb; b++)
            {
                rows[b].Sort((p, q) => q.R.CompareTo(p.R));
                float right = X0, left = X0;
                for (int i = 0; i < rows[b].Count; i++)
                {
                    var p = rows[b][i];
                    float x;
                    if (i == 0) { x = X0; right = X0 + p.R; left = X0 - p.R; }
                    else if (i % 2 == 1) { x = right + Gap + p.R; right = x + p.R; }
                    else { x = left - Gap - p.R; left = x - p.R; }
                    p.Base = new Vector3(x, 0, BandZ[b]);
                }
                BandMinX[b] = rows[b].Count > 0 ? left : X0 - 20;
                BandMaxX[b] = rows[b].Count > 0 ? right : X0 + 20;
                halfWidth = Mathf.Max(halfWidth, Mathf.Max(BandMaxX[b] - X0, X0 - BandMinX[b]));
            }

            // the rest of the economy: on the -x side, in the row of its band, outward by size
            EconomyX = X0 - halfWidth - FlankGap;
            for (int b = 0; b < nb; b++)
            {
                economyRows[b].Sort((p, q) => q.R.CompareTo(p.R));
                float edge = EconomyX + 20;
                foreach (var p in economyRows[b])
                {
                    float x = edge - p.R;
                    p.Base = new Vector3(x, 0, BandZ[b]);
                    edge = x - p.R - Gap;
                }
                MinX = Mathf.Min(MinX, edge);
            }

            // capital: on the +x side, one column along the depth, largest nearest the clouds and labs it finances
            CapitalX = X0 + halfWidth + FlankGap;
            capital.Sort((p, q) => q.R.CompareTo(p.R));
            float span = FrontZ - (crestZ + Back), cz = crestZ + Back + span * .5f;
            {
                float down = cz, up = cz;
                for (int i = 0; i < capital.Count; i++)
                {
                    var p = capital[i];
                    float zz;
                    if (i == 0) { zz = cz; down = cz - p.R; up = cz + p.R; }
                    else if (i % 2 == 1) { zz = up + Gap + p.R; up = zz + p.R; }
                    else { zz = down - Gap - p.R; down = zz - p.R; }
                    p.Base = new Vector3(CapitalX + p.R, 0, zz);
                    MaxX = Mathf.Max(MaxX, CapitalX + 2 * p.R);
                }
            }

            // the states: a row of plinths between the crest and the arena, the US in the middle
            states.Sort((p, q) => q.R.CompareTo(p.R));
            {
                float right = X0, left = X0;
                for (int i = 0; i < states.Count; i++)
                {
                    var p = states[i];
                    float x;
                    if (i == 0) { x = X0; right = X0 + p.R; left = X0 - p.R; }
                    else if (i % 2 == 1) { x = right + 6 + p.R; right = x + p.R; }
                    else { x = left - 6 - p.R; left = x - p.R; }
                    p.Base = new Vector3(x, 0, crestZ + StateZ);
                }
            }

            foreach (var p in Places)
            {
                MinX = Mathf.Min(MinX, p.Base.x - p.R);
                MaxX = Mathf.Max(MaxX, p.Base.x + p.R);
            }
            Mouths();
        }

        /// <summary>Each actor's mouths: where its inflows end (toward the weighted mean of its payers) and its outflows start
        /// (toward its payees), on the edge of its footprint.</summary>
        void Mouths()
        {
            var sums = new Dictionary<(string, bool), Vector3>();
            foreach (var f in Model.Flows)
            {
                Place a = Get(f.From), b = Get(f.To);
                if (a == null || b == null) continue;
                Vector3 d = b.Base - a.Base; d.y = 0;
                float w = Mathf.Sqrt((float)f.UsdBn);
                Add((f.From, false), d.normalized * w);
                Add((f.To, true), -d.normalized * w);
            }

            foreach (var kv in sums)
            {
                Place p = Get(kv.Key.Item1);
                Vector3 dir = kv.Value.sqrMagnitude > 1e-6f ? kv.Value.normalized : Vector3.back;
                // a mouth faces the band its money comes from (or goes to): straight up or down the supply chain unless the
                // counterparties lie mostly to the side, so the rivers of a band enter together
                if (Mathf.Abs(dir.z) > .35f) dir = new Vector3(0, 0, Mathf.Sign(dir.z));
                mouths[kv.Key] = p.Base + dir * (p.R * .92f);
            }

            void Add((string, bool) key, Vector3 v) => sums[key] = (sums.TryGetValue(key, out Vector3 s) ? s : Vector3.zero) + v;
        }

        /// <summary>An actor's in-mouth (where its rivers end), or its footprint's centre when nothing flows in.</summary>
        public Vector3 InMouth(string id)
        {
            var p = Get(id);
            if (p == null) return Vector3.zero;
            return mouths.TryGetValue((id, true), out Vector3 m) ? m : p.Base;
        }

        /// <summary>A flow's river on the ground (land-local points, y just above the floor): from the payer's out-mouth to
        /// the payee's in-mouth, bowed sideways a little so parallel rivers part.</summary>
        public List<Vector3> River(Advantage.Flow f, int salt)
        {
            Place a = Get(f.From), b = Get(f.To);
            var pts = new List<Vector3>(24);
            if (a == null || b == null) return pts;
            Vector3 p0 = mouths.TryGetValue((f.From, false), out Vector3 m0) ? m0 : a.Base;
            Vector3 p2 = mouths.TryGetValue((f.To, true), out Vector3 m2) ? m2 : b.Base;
            Vector3 d = p2 - p0; d.y = 0;
            Vector3 side = Vector3.Cross(Vector3.up, d.normalized);
            float bow = d.magnitude * .07f * ((salt & 1) == 0 ? 1 : -1) * (1 + .5f * ((salt >> 1) % 3));
            Vector3 p1 = (p0 + p2) * .5f + side * bow;
            const int n = 22;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                p.y = .35f;
                pts.Add(p);
            }

            return pts;
        }

        /// <summary>Footprints that overlap (the same plane), as pairs of names, at most a few; the count in <paramref name="count"/>.</summary>
        public string Overlaps(out int count)
        {
            count = 0;
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < Places.Count; i++)
                for (int j = i + 1; j < Places.Count; j++)
                {
                    Place p = Places[i], q = Places[j];
                    Vector3 d = p.Base - q.Base; d.y = 0;
                    if (d.magnitude >= p.R + q.R - .05f) continue;
                    count++;
                    if (count <= 5) s.Append(count > 1 ? ", " : "").Append(p.Actor.Name).Append('/').Append(q.Actor.Name);
                }

            return s.ToString();
        }
    }
}
