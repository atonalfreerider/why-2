using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Why
{
    /// <summary>A named point of the graph in data space, used by labels, the director and the camera.</summary>
    public sealed class Anchor
    {
        public string Key;          // "kind:id"
        public string Label;        // display name
        public string Blurb;        // one or two sentences, may be null
        public GraphLevel Level;
        public double YearsAgo;     // start (or the moment) of the thing
        public double EndYearsAgo;  // end of the thing (0 = extant); equal to YearsAgo for moments
        public float Y;
        public float Rho;
        public IdRange Ids = IdRange.Empty;
        public int Tier = 2;        // 1 = always labeled, 2 = mid zoom, 3 = close

        /// <summary>Arc of the anchor point (the start of the thing).</summary>
        public float U => DeepTime.Arc(YearsAgo);

        public Vector3 Data => new Vector3(U, Y, Rho);

        public Vector3 World => GraphWarp.ToWorld(U, Y, Rho);
    }

    /// <summary>
    /// Thread-safe registry of anchors keyed "kind:id" (see Docs/ARCHITECTURE.md). Layers register while
    /// preparing; "time:&lt;yearsAgo&gt;" and "now" resolve dynamically.
    /// </summary>
    public static class Anchors
    {
        static readonly Dictionary<string, Anchor> map = new Dictionary<string, Anchor>(StringComparer.Ordinal);
        static readonly object gate = new object();

        public static void Clear()
        {
            lock (gate) map.Clear();
        }

        public static void Register(Anchor anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(anchor.Key)) return;
            lock (gate) map[anchor.Key] = anchor;
        }

        public static IReadOnlyList<Anchor> All()
        {
            lock (gate) return new List<Anchor>(map.Values);
        }

        public static bool TryGet(string key, out Anchor anchor)
        {
            anchor = null;
            if (string.IsNullOrEmpty(key)) return false;
            key = key.Trim();

            if (key == "now")
            {
                anchor = new Anchor
                {
                    Key = key, Label = "Now", Level = GraphLevel.Matter, YearsAgo = 0, EndYearsAgo = 0,
                    Y = GraphStyle.HumansY, Rho = 0, Tier = 1
                };
                return true;
            }

            if (key.StartsWith("time:", StringComparison.Ordinal))
            {
                if (!double.TryParse(key.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double ya)) return false;
                anchor = new Anchor
                {
                    Key = key, Label = DeepTime.FormatYearsAgo(ya, DeepTime.NowYear), Level = GraphLevel.Matter,
                    YearsAgo = ya, EndYearsAgo = ya, Y = GraphStyle.MatterY, Rho = -0.05f, Tier = 1
                };
                return true;
            }

            lock (gate) return map.TryGetValue(key, out anchor);
        }
    }
}
