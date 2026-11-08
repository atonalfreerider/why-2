using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Life
{
    /// <summary>
    /// The mass extinctions laid over the tree of life as one multiplier B(u) on the living weight the tree's radius is
    /// drawn from (see <see cref="LifeLayer"/>). The TimeTree holds only families that survive today, so by itself the tree
    /// never shrinks; the fossil record's losses come from life_clades.json instead. Each extinction leaves B alone before
    /// it (u above its arc), drops it to 1 - loss at its arc and lets it rebound to 1 along an arc-space S-curve (the style
    /// of MatterBand.Ramp) by the time the data gives for the recovery. Overlapping windows multiply: the 75-million-year
    /// rebound from the end-Permian is still running when the end-Triassic strikes.
    ///
    /// The loss is a share of species (marine species, or all species, as each event's data says), not measured biomass:
    /// the depth of a dip stands for species lost.
    ///
    /// Immutable once built: pure functions of the arc, safe on the worker threads that build the layers.
    /// </summary>
    public sealed class MassExtinctions
    {
        /// <summary>
        /// Arc width of an extinction's cliff: the tree is sampled on both sides of it, so the drop reads as a wall (drawn by
        /// the outline; the lineages leave the cliff itself unlit). 1e-5 arc is 20 to 100 thousand years across the last 450
        /// million years, about the length of the end-Permian's main pulse.
        /// </summary>
        public const float Cliff = 1e-5f;

        /// <summary>A recovery window is sampled in at least this many steps (an ordinary step still applies where finer).</summary>
        public const int WindowSteps = 12;

        /// <summary>One mass extinction: when it struck, the share of species it removed and how long the rebound took.</summary>
        public readonly struct Extinction
        {
            public readonly string Id;
            public readonly double Mya;

            /// <summary>Share of species lost (0..1).</summary>
            public readonly float Loss;

            /// <summary>Millions of years until the measure the data names had recovered.</summary>
            public readonly double RecoveryMyr;

            /// <summary>Arc of the extinction: B drops here.</summary>
            public readonly float UEvent;

            /// <summary>Arc just before it, the top of the cliff.</summary>
            public readonly float UTop;

            /// <summary>Arc just after it, past the foot of the cliff.</summary>
            public readonly float UAfter;

            /// <summary>Arc where the rebound is complete.</summary>
            public readonly float URecovered;

            public Extinction(string id, double mya, double loss, double recoveryMyr)
            {
                Id = id;
                Mya = mya;
                Loss = (float)loss;
                RecoveryMyr = recoveryMyr;
                UEvent = DeepTime.Arc(mya * 1e6);
                UTop = UEvent + Cliff;
                UAfter = UEvent - Cliff;
                URecovered = DeepTime.Arc(Math.Max(mya - recoveryMyr, 0) * 1e6);
            }

            /// <summary>Arc length of the recovery window.</summary>
            public float Span => UEvent - URecovered;

            /// <summary>True inside the window, where this extinction's factor is below 1: from its arc to the end of the rebound.</summary>
            public bool Holds(float u) => u <= UEvent && u > URecovered;

            /// <summary>This extinction's factor of B: 1 before it, 1 - loss at it, rising along an S-curve to 1 at the recovery.</summary>
            public float Factor(float u)
            {
                if (!Holds(u)) return 1f;
                return Mathf.Lerp(1f - Loss, 1f, Smooth((UEvent - u) / Mathf.Max(Span, 1e-9f)));
            }

            /// <summary>What makes the data unusable, or null.</summary>
            public string Problem()
            {
                if (!(Loss > 0f && Loss < 1f)) return $"'{Id}': loss {Loss} must lie between 0 and 1";
                if (!(Mya > 0)) return $"'{Id}': mya {Mya} must be positive";
                if (!(RecoveryMyr > 0 && RecoveryMyr < Mya)) return $"'{Id}': recoveryMyr {RecoveryMyr} must lie between 0 and its mya ({Mya})";
                if (!(Span > 2f * Cliff)) return $"'{Id}': a recovery of {RecoveryMyr} million years is narrower than its cliff on the clock";
                return null;
            }
        }

        /// <summary>No extinctions: B is 1 everywhere.</summary>
        public static readonly MassExtinctions None = new MassExtinctions(null);

        readonly Extinction[] events;   // oldest first
        readonly float[] breaks;        // arcs the sampling lands on, descending

        /// <summary>
        /// Builds B from the extinctions the data gives. Unusable ones (a loss outside 0..1, a recovery longer than the time
        /// since the event or narrower than the cliff) are left out and described in problems.
        /// </summary>
        public MassExtinctions(IEnumerable<Extinction> extinctions, List<string> problems = null)
        {
            List<Extinction> list = new List<Extinction>();
            if (extinctions != null)
            {
                foreach (Extinction e in extinctions)
                {
                    string problem = e.Problem();
                    if (problem != null)
                    {
                        problems?.Add(problem);
                        continue;
                    }

                    list.Add(e);
                }
            }

            list.Sort((a, b) => b.UEvent.CompareTo(a.UEvent));
            events = list.ToArray();

            // each cliff gets a point on both sides (its top and its foot) and one just beyond each, so a line can leave the
            // cliff itself unlit; each rebound ends exactly on its arc, and the point just past it gives the outline's
            // highlight a sharp end (see LifeLayer)
            List<float> arcs = new List<float>(events.Length * 6);
            foreach (Extinction e in events)
            {
                arcs.Add(e.UTop + Cliff);
                arcs.Add(e.UTop);
                arcs.Add(e.UEvent);
                arcs.Add(e.UAfter);
                arcs.Add(e.URecovered);
                arcs.Add(e.URecovered - Cliff);
            }

            arcs.Sort((a, b) => b.CompareTo(a));
            breaks = arcs.ToArray();
        }

        public int Count => events.Length;

        public Extinction this[int index] => events[index];

        /// <summary>Index of the extinction with this id (-1: none).</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Id == id) return i;
            }

            return -1;
        }

        /// <summary>
        /// B(u): the share of the tree's living weight that stands at arc u, the product of every extinction's factor.
        /// </summary>
        public float B(float u)
        {
            float b = 1f;
            for (int i = 0; i < events.Length; i++) b *= events[i].Factor(u);
            return b;
        }

        /// <summary>
        /// True exactly on an extinction's cliff, its top or its foot (the sampling lands on both): the segment between
        /// them is where every line drops at once.
        /// </summary>
        public bool OnCliff(float u)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (u == events[i].UTop || u == events[i].UEvent) return true;
            }

            return false;
        }

        /// <summary>Arc below which a rebound is too narrow on the clock for its lineages to be lit: each lineage's rebound there
        /// is near-radial, so a thousand of them stack into one bright spoke in the zoomed-out views; the outline draws it.</summary>
        const float LitSpan = 0.002f;

        /// <summary>
        /// How much of a lineage's light to keep at arc u: 1 outside every window, Span / LitSpan inside a window narrower than
        /// LitSpan (the end-Ordovician, Late Devonian and end-Triassic rebounds), multiplied over the windows that hold u.
        /// </summary>
        public float LitShare(float u)
        {
            float share = 1f;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Holds(u))
                    share *= Mathf.Clamp01(events[i].Span / LitSpan);
            return share;
        }

        /// <summary>True where some extinction's window holds arc u (B may be below 1 there).</summary>
        public bool InWindow(float u)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Holds(u)) return true;
            }

            return false;
        }

        /// <summary>
        /// Index of the latest extinction whose dip holds arc u, from the top of its cliff to the end of its rebound (-1:
        /// none): where the outline carries that extinction's highlight id.
        /// </summary>
        public int DipAt(float u)
        {
            int k = -1;
            for (int i = 0; i < events.Length; i++)
            {
                if (u <= events[i].UTop && u >= events[i].URecovered) k = i;
            }

            return k;
        }

        /// <summary>
        /// The last extinction whose cliff lies inside extinction k's window (k itself if none). Sorted oldest first, those
        /// follow k directly, so k's dip and the dips nested in it are the consecutive indices k .. LastNested(k).
        /// </summary>
        public int LastNested(int k)
        {
            int j = k;
            while (j + 1 < events.Length && events[j + 1].UTop > events[k].URecovered) j++;
            return j;
        }

        /// <summary>
        /// The next sample when walking the arc from u toward the present with the given step: never across a cliff (both of
        /// its sides are sampled) or a window's end, and in at least <see cref="WindowSteps"/> steps through each window.
        /// Never below uEnd.
        /// </summary>
        public float Next(float u, float step, float uEnd)
        {
            float next = u - step;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Holds(u)) next = Mathf.Max(next, u - events[i].Span / WindowSteps);
            }

            foreach (float b in breaks)
            {
                if (b >= u) continue;
                if (next < b) next = b;
                break;
            }

            return Mathf.Max(next, uEnd);
        }

        /// <summary>
        /// The self-checks of B, sampled from arc uOld down to uNew (one line each in problems): exactly 1 outside every
        /// window, between 0 and 1 inside, a drop at every cliff, 1 - loss right after a lone extinction (one no other window
        /// holds), and 1 again where each rebound ends.
        /// </summary>
        public void Verify(float uOld, float uNew, List<string> problems)
        {
            const int Samples = 4000;
            for (int i = 0; i <= Samples; i++)
            {
                float u = Mathf.Lerp(uOld, uNew, i / (float)Samples);
                float b = B(u);
                if (!InWindow(u))
                {
                    if (b != 1f) problems.Add($"B is {b} at arc {u:0.000000}, outside every extinction's window");
                }
                else if (!(b > 0f && b <= 1f))
                {
                    problems.Add($"B is {b} at arc {u:0.000000}, outside 0..1");
                }
            }

            const float Eps = 1e-6f;
            for (int i = 0; i < events.Length; i++)
            {
                Extinction e = events[i];
                if (!(B(e.UEvent) < B(e.UTop))) problems.Add($"'{e.Id}': B does not drop at the extinction");
                if (e.Factor(e.URecovered) != 1f || e.Factor(e.UTop) != 1f)
                {
                    problems.Add($"'{e.Id}': its factor is not 1 before the extinction and after the rebound");
                }

                bool lone = true;
                for (int j = 0; j < events.Length; j++)
                {
                    if (j != i && (events[j].Holds(e.UTop) || events[j].Holds(e.UEvent - Eps))) lone = false;
                }

                if (!lone) continue;
                if (B(e.UTop) != 1f) problems.Add($"'{e.Id}': B is {B(e.UTop)} just before a lone extinction, not 1");
                float after = B(e.UEvent - Eps);
                if (Mathf.Abs(after - (1f - e.Loss)) > 1e-3f)
                {
                    problems.Add($"'{e.Id}': B is {after:0.0000} right after a lone extinction, not 1 - loss = {1f - e.Loss:0.0000}");
                }
            }
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }
    }
}
