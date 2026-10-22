using System;
using System.Collections.Generic;

namespace Why
{
    /// <summary>
    /// Views that show one of several states of their own (the economy's regimes view: the same hills on five
    /// governments): a layer registers how many there are, how to show one and its name, and the smoke test captures each
    /// after the view itself (<see cref="SmokeTest"/>), so every state renders without errors. Showing -1 returns the view
    /// to its default.
    /// </summary>
    public static class ViewVariants
    {
        public sealed class Entry
        {
            public Func<int> Count;
            public Action<int> Show;
            public Func<int, string> Name;
        }

        static readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public static void Register(string view, Func<int> count, Action<int> show, Func<int, string> name)
        {
            lock (map) map[view] = new Entry { Count = count, Show = show, Name = name };
        }

        public static bool TryGet(string view, out Entry entry)
        {
            lock (map) return map.TryGetValue(view, out entry);
        }
    }
}
