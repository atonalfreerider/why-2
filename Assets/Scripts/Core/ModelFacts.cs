using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace Why
{
    /// <summary>
    /// Numbers the models compute that a tour card, the narration or a doc quotes, each published under a key
    /// (<c>instant.io.up</c>) with its unit, its source and its basis (measured, judged, modelled or assumed), so every
    /// number on screen can be traced to the run that computed it. Models publish from any thread with
    /// <see cref="Set(string, double, string, string, string)"/>, or <see cref="Set(string, string, string, string, string)"/>
    /// for a word, a quarter or a date. Setting a key again replaces the fact: a model built for several years puts the
    /// year in the key, or publishes one year only, so the facts do not depend on which year was built last. A player
    /// started with <c>-smoketest -smokefacts</c> logs every fact at the end of the run (<see cref="Line"/>), and
    /// Tools/export-facts.py turns that log into Tools/model-facts.json; tour cards resolve <c>{key}</c> placeholders from
    /// here (<see cref="TryGet"/>). A fact that could not be traced or read back is refused and listed in
    /// <see cref="Problems"/>, which the smoke test logs as errors: a key that is not letters, digits, '.', '_' and '-', a
    /// number that is not finite, a missing source or basis.
    /// </summary>
    public static class ModelFacts
    {
        /// <summary>One published fact; it never changes, so a reader on any thread sees it whole.</summary>
        public sealed class Fact
        {
            /// <summary>Letters, digits, '.', '_' and '-' (<c>instant.io.up</c>).</summary>
            public readonly string Key;

            /// <summary>The number (NaN for a text fact).</summary>
            public readonly double Value;

            /// <summary>The value as text: a number's shortest invariant form that reads back exactly, or the text itself.</summary>
            public readonly string Text;

            /// <summary>What the number counts ("%", "$T", "people"); empty for a plain count or a text.</summary>
            public readonly string Unit;

            /// <summary>Where it comes from: the data file and field, or the model and what it reads.</summary>
            public readonly string Source;

            /// <summary>How it is known (measured, judged, modelled or assumed), with anything a reader must know to quote it.</summary>
            public readonly string Basis;

            internal Fact(string key, double value, string text, string unit, string source, string basis)
            {
                Key = key;
                Value = value;
                Text = text;
                Unit = unit;
                Source = source;
                Basis = basis;
            }

            /// <summary>A number (else a text).</summary>
            public bool IsNumber => !double.IsNaN(Value);
        }

        /// <summary>The most refusals listed; the rest are counted.</summary>
        const int MaxProblems = 40;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static readonly object gate = new object();
        static readonly Dictionary<string, Fact> facts = new Dictionary<string, Fact>(StringComparer.Ordinal);
        static readonly List<string> problems = new List<string>();
        static int unlisted;

        /// <summary>
        /// Publishes a number (pass a double: a float carries its rounding into the fact, 0.372f is 0.37200000882...).
        /// Returns false, and lists the problem, when the fact is refused.
        /// </summary>
        public static bool Set(string key, double value, string unit, string source, string basis)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return Refuse(key, "its value is " + value.ToString(Ci) + ", not a finite number");
            return Store(key, value, Number(value), unit, source, basis);
        }

        /// <summary>Publishes a text: a name, a quarter, a date. Returns false, and lists the problem, when it is refused.</summary>
        public static bool Set(string key, string value, string unit, string source, string basis)
        {
            if (value == null) return Refuse(key, "its text is null");
            return Store(key, double.NaN, value, unit, source, basis);
        }

        /// <summary>The fact published under a key.</summary>
        public static bool TryGet(string key, out Fact fact)
        {
            lock (gate) return facts.TryGetValue(key ?? "", out fact);
        }

        /// <summary>How many facts are published.</summary>
        public static int Count
        {
            get
            {
                lock (gate) return facts.Count;
            }
        }

        /// <summary>Every fact, sorted by key (ordinal).</summary>
        public static IReadOnlyList<Fact> All()
        {
            lock (gate) return facts.Values.OrderBy(f => f.Key, StringComparer.Ordinal).ToList();
        }

        /// <summary>The facts refused so far, one line each (the first <see cref="MaxProblems"/>, then a count of the rest).</summary>
        public static IReadOnlyList<string> Problems()
        {
            lock (gate)
            {
                List<string> list = new List<string>(problems);
                if (unlisted > 0) list.Add($"{unlisted} more refused");
                return list;
            }
        }

        /// <summary>
        /// The fact as the smoke test logs it, <c>[Why] fact key = value unit | source | basis</c>: a number in its
        /// <see cref="Fact.Text"/> form, a text in double quotes with JSON escapes (so spaces, bars and quotes in it read
        /// back); one space before the unit even when it is empty. The unit, source and basis hold no bar or line break
        /// (Set turned them into a slash and spaces). Tools/export-facts.py parses exactly this.
        /// </summary>
        public static string Line(Fact f) =>
            "[Why] fact " + f.Key + " = " + (f.IsNumber ? f.Text : JsonConvert.ToString(f.Text)) + " " + f.Unit + " | " + f.Source + " | " + f.Basis;

        static bool Store(string key, double value, string text, string unit, string source, string basis)
        {
            if (!IsKey(key)) return Refuse(key, "a key is letters, digits, '.', '_' and '-'");
            source = Clean(source);
            basis = Clean(basis);
            if (source.Length == 0 || basis.Length == 0) return Refuse(key, "a fact names its source and its basis");
            Fact fact = new Fact(key, value, text, Clean(unit), source, basis);
            lock (gate) facts[key] = fact;
            return true;
        }

        static bool Refuse(string key, string why)
        {
            string problem = $"refused the fact '{key}': {why}";
            lock (gate)
            {
                if (problems.Contains(problem)) return false;
                if (problems.Count < MaxProblems) problems.Add(problem);
                else unlisted++;
            }

            return false;
        }

        static bool IsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (char c in key)
            {
                bool ok = c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-';
                if (!ok) return false;
            }

            return true;
        }

        /// <summary>A unit, source or basis kept on its log line: line breaks and tabs become spaces, a bar a slash.</summary>
        static string Clean(string s) =>
            s == null ? "" : s.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('|', '/').Trim();

        /// <summary>
        /// The shortest invariant text that reads back as exactly this number ("37.2", "1E-07"): the first of 15, 16 and 17
        /// significant digits that does. "R" is not shortest on Mono (0.68228719991739994 for 0.6822871999174), and Mono's
        /// parser is itself one unit off for a few texts, so there such a number takes 16 digits (0.6822871999173999);
        /// Python reads both as the same number. TryParse, because Mono throws on text that rounds past the largest
        /// double (double.MaxValue at 15 digits).
        /// </summary>
        static string Number(double v)
        {
            for (int digits = 15; digits < 17; digits++)
            {
                string s = v.ToString("G" + digits, Ci);
                if (double.TryParse(s, NumberStyles.Float, Ci, out double back) && back.Equals(v)) return s;
            }

            return v.ToString("G17", Ci);
        }
    }
}
