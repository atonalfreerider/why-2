using System.Collections.Generic;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// Drives the shaders' highlight globals: up to 8 glowing id ranges, and a dim factor applied to
    /// everything else so attention lands on the highlighted parts. Glow and dim ease in and out.
    /// </summary>
    public static class Highlighter
    {
        const int MaxRanges = 8;

        static readonly int HiId = Shader.PropertyToID("_WhyHi");
        static readonly int HiCountId = Shader.PropertyToID("_WhyHiCount");
        static readonly int DimId = Shader.PropertyToID("_WhyDim");
        static readonly int TimeId = Shader.PropertyToID("_WhyTime");

        struct Entry
        {
            public IdRange Range;
            public float Glow;
            public float Current;
        }

        static readonly List<Entry> active = new List<Entry>();
        static readonly Dictionary<string, List<Entry>> persistent = new Dictionary<string, List<Entry>>();
        static readonly Vector4[] buffer = new Vector4[MaxRanges];
        static float dimTarget, dimCurrent;
        static float fade = 1;

        /// <summary>
        /// Ranges that always glow a little (e.g. our lineage), independent of the director. Each owner
        /// (layer) replaces only its own entries.
        /// </summary>
        public static void SetPersistent(string owner, IEnumerable<(IdRange range, float glow)> ranges)
        {
            List<Entry> list = new List<Entry>();
            foreach ((IdRange range, float glow) in ranges)
            {
                list.Add(new Entry { Range = range, Glow = glow, Current = glow });
            }

            persistent[owner] = list;
        }

        /// <summary>Replace the attention highlight. dim = how much to dim everything else (0..1).</summary>
        public static void Set(IEnumerable<IdRange> ranges, float glow = GraphStyle.HighlightGlow, float dim = 0.7f)
        {
            active.Clear();
            foreach (IdRange r in ranges)
            {
                if (r.IsEmpty) continue;
                active.Add(new Entry { Range = r, Glow = glow, Current = 1 });
            }

            fade = 0;
            dimTarget = active.Count > 0 ? dim : 0;
        }

        public static void Clear()
        {
            active.Clear();
            dimTarget = 0;
        }

        public static bool HasHighlight => active.Count > 0;

        /// <summary>True if the range overlaps an active (attention) highlight.</summary>
        public static bool IsHighlighted(IdRange r)
        {
            foreach (Entry e in active)
            {
                if (r.Min <= e.Range.Max && r.Max >= e.Range.Min) return true;
            }

            return false;
        }

        public static void Tick(float dt)
        {
            fade = Mathf.MoveTowards(fade, 1, dt * 1.5f);
            dimCurrent = Mathf.MoveTowards(dimCurrent, dimTarget, dt * 1.2f);

            int n = 0;
            float ease = fade * fade * (3 - 2 * fade);
            for (int i = 0; i < active.Count && n < MaxRanges; i++)
            {
                Entry e = active[i];
                buffer[n++] = new Vector4(e.Range.Min, e.Range.Max, Mathf.Lerp(1, e.Glow, ease), 1);
            }

            foreach (List<Entry> list in persistent.Values)
            {
                for (int i = 0; i < list.Count && n < MaxRanges; i++)
                {
                    Entry e = list[i];
                    buffer[n++] = new Vector4(e.Range.Min, e.Range.Max, e.Glow, 0);
                }
            }

            for (int i = n; i < MaxRanges; i++) buffer[i] = new Vector4(-1, -2, 1, 0);

            Shader.SetGlobalVectorArray(HiId, buffer);
            Shader.SetGlobalFloat(HiCountId, n);
            Shader.SetGlobalFloat(DimId, dimCurrent);
            Shader.SetGlobalFloat(TimeId, Time.time);
        }
    }
}
