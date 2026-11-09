using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Why.Economy.Land;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Why.Economy.Emphasis
{
    /// <summary>The four levels a view's row gives a part (spec 3.1): its alpha is <see cref="EconomyEmphasis.Alpha"/>.</summary>
    public enum Level
    {
        /// <summary>0: the renderer is off, not faint.</summary>
        Absent,

        /// <summary>.12: the ground the view's story stands on.</summary>
        Context,

        /// <summary>.45: beside the story.</summary>
        Support,

        /// <summary>1: the story; at most 3 parts a row, synthesis views excepted.</summary>
        Subject
    }

    /// <summary>
    /// Highlight ids of the parts the emphasis table names (spec 3.1): each key a block of <see cref="Block"/> ids from
    /// <see cref="Base"/>, in the order the keys were first named (the table's, so the same in every run), exact in the
    /// float vertex attribute (below 2^24) and clear of every other id: GraphIds' civilizations end near 5.1 million,
    /// EconomyIds below 100,000. A hill mesh is built with its part's first id (<see cref="EconomyEmphasis.Id"/>), so
    /// <c>Highlighter.Set(new[] { EmphasisIds.Part("wave", "crest") })</c> lights that part and nothing else.
    /// </summary>
    public static class EmphasisIds
    {
        /// <summary>The first id of the first key's block, and the ids a key holds (room for one id per item of a part).</summary>
        public const int Base = 12_000_000, Block = 4_096;

        /// <summary>The keys the id space holds below 2^24.</summary>
        public const int MaxParts = ((1 << 24) - Base) / Block;

        /// <summary>The ids of a layer's part ("wave", "crest"): the key "wave.crest"'s block.</summary>
        public static IdRange Part(string layer, string part) => EconomyEmphasis.Ids(EconomyEmphasis.Key(layer + "." + part));

        /// <summary>A key's block (empty past the id space, which <see cref="EconomyEmphasis.Problems"/> reports), less its
        /// last id, which no mesh uses: the shaders test a highlight as <c>id &lt;= max + 0.5</c>, and above 2^23 a float
        /// holds whole numbers only, so max + 0.5 rounds to the next block's first id, the id its part's meshes carry
        /// (lighting the crest lit the spray). With the gap a range ends one short of its neighbour, and rounds at most onto
        /// the unused id.</summary>
        internal static IdRange Of(int handle) =>
            handle >= 0 && handle < MaxParts ? new IdRange(Base + handle * Block, Base + handle * Block + Block - 2) : IdRange.Empty;
    }

    /// <summary>
    /// One emphasis table every economy layer reads (spec 3.1, S01). Every drawable part is a key "layer.part" with an int
    /// handle (<see cref="Key"/>), so a layer's lookup is an array read. A view's row gives each key its value: a level
    /// (<see cref="Level"/>) or, in the table's first form, the literal value the layer drew there (<see cref="EmphasisTable"/>);
    /// a key a row does not list is absent, and a view with no row at all draws the table's "*" column. Precedence: the
    /// tour's override, then the view's variant ("view:variant"), then the view.
    /// <para>The land's view layer (Order 44, before every hill layer) moves the active row with the preset on screen
    /// (<see cref="Focus"/>) and eases every key toward it at <see cref="LandStyle.EmphasisRate"/> once a frame
    /// (<see cref="Advance"/>), as LandView eases the land's groups; a layer reads <see cref="Get"/> (the eased row times the
    /// key's named state multipliers, each at most 1) and sets it with <see cref="Apply"/>: absent turns the renderer off.
    /// The first focus after a scene loads snaps, as LandView's does.</para>
    /// <para>Hooks, so packages add rows without editing the table: <see cref="RegisterView"/>, <see cref="Contribute"/>,
    /// <see cref="Multiplier"/>, <see cref="PushOverride"/> and <see cref="PopOverride"/> for the tour, and
    /// <see cref="Problems"/>. Registration is safe on any thread; the rest runs on the main thread. The smoke test checks the
    /// rules on every capture (<see cref="Check"/>), and <c>-smokeextra emphasis</c> lights the crest by its part's ids
    /// (<see cref="HighlightCheck"/>).</para>
    /// </summary>
    public static class EconomyEmphasis
    {
        /// <summary>The levels' alphas (spec 3.1).</summary>
        public const float Subject = 1f, Support = .45f, Context = .12f, Absent = 0f;

        /// <summary>A level's alpha.</summary>
        public static float Alpha(Level level) =>
            level == Level.Subject ? Subject : level == Level.Support ? Support : level == Level.Context ? Context : Absent;

        /// <summary>The smoke sequence that lights the crest by its part's ids (<c>-smokeextra emphasis</c>).</summary>
        public const string Sequence = "emphasis";

        /// <summary>A row's subjects at most (synthesis views exempt), and the views a key may stand at .5 or more in before
        /// every view looks alike (a warning until WP14 retunes the views).</summary>
        const int MaxSubjects = 3, MaxBrightViews = 6;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>One value of a row: a level, or a literal value (the table's transcription, a tour's).</summary>
        readonly struct Entry
        {
            public readonly float Value;
            public readonly bool IsLevel;
            public readonly Level Level;

            Entry(float value, bool isLevel, Level level)
            {
                Value = value;
                IsLevel = isLevel;
                Level = level;
            }

            public static Entry Of(float value) => new Entry(value, false, default);
            public static Entry Of(Level level) => new Entry(Alpha(level), true, level);
        }

        /// <summary>A row: its keys' entries by handle, its wildcards ("social.*", the longest prefix wins), whether it is a
        /// synthesis view's, and who made it.</summary>
        sealed class ViewRow
        {
            public readonly Dictionary<int, Entry> Keys = new Dictionary<int, Entry>();
            public readonly List<(string prefix, Entry entry)> Wildcards = new List<(string, Entry)>();
            public bool Synthesis;
            public string Source;

            public bool TryGet(int handle, string name, out float value)
            {
                if (Keys.TryGetValue(handle, out Entry e))
                {
                    value = e.Value;
                    return true;
                }

                int best = -1;
                value = 0;
                foreach ((string prefix, Entry entry) in Wildcards)
                {
                    if (prefix.Length <= best || !name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    best = prefix.Length;
                    value = entry.Value;
                }

                return best >= 0;
            }

            public void Set(int handle, string key, Entry e)
            {
                if (key.EndsWith("*", StringComparison.Ordinal))
                {
                    string prefix = key.Substring(0, key.Length - 1);
                    Wildcards.RemoveAll(w => w.prefix == prefix);
                    Wildcards.Add((prefix, e));
                }
                else Keys[handle] = e;
            }

            public IEnumerable<Entry> Entries => Keys.Values.Concat(Wildcards.Select(w => w.entry));
        }

        /// <summary>A tour's override: its listed keys (and wildcards), the value of every other key (<see cref="Rest"/>, -1:
        /// none) or the most any other key may keep of the rows beneath (<see cref="Cap"/>, -1: none).</summary>
        sealed class Override
        {
            public int Token;
            public readonly ViewRow Values = new ViewRow { Source = "a tour's override" };
            public float Rest = -1, Cap = -1;
        }

        static readonly object gate = new object();
        static readonly List<string> names = new List<string>();
        static readonly Dictionary<string, int> handles = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Per key: named by a row (the table, <see cref="RegisterView"/>, <see cref="Contribute"/>); read by a layer;
        /// new since the last retarget (it starts at its target instead of easing in from 0).</summary>
        static readonly List<bool> declared = new List<bool>(), read = new List<bool>(), fresh = new List<bool>();

        static float[] target = new float[0], eased = new float[0];

        /// <summary>Per key: its named state multipliers (copied on write, so <see cref="Get"/> runs them outside the lock).</summary>
        static (string name, Func<float> factor)[][] factors = new (string, Func<float>)[0][];

        static readonly Dictionary<string, ViewRow> rows = new Dictionary<string, ViewRow>(StringComparer.Ordinal);

        /// <summary>The views a row names, by their own row or a variant's ("instant" for "instant:income"): a view none names
        /// draws the table's "*" column, one named only by its variants' rows has every part they do not list absent.</summary>
        static readonly HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<Override> overrides = new List<Override>();

        /// <summary>The table's own problems (its shape), found as it loads.</summary>
        static readonly List<string> tableProblems = new List<string>();

        static string view = "overview", variant;
        static bool snapped, dirty = true;
        static int nextToken;

        static EconomyEmphasis()
        {
            string[] views = EmphasisTable.Views;
            foreach ((string key, float[] values) in EmphasisTable.Rows)
            {
                if (handles.ContainsKey(key)) tableProblems.Add($"the table lists {key} twice");
                if (values.Length != views.Length)
                {
                    tableProblems.Add($"the table's row {key} has {values.Length} values for its {views.Length} views");
                    continue;
                }

                int h = Handle(key);
                declared[h] = true;
                for (int c = 0; c < views.Length; c++) RowOf(views[c], "the table").Keys[h] = Entry.Of(values[c]);
            }
        }

        // ------------------------------------------------------------------ keys

        /// <summary>A key's handle ("wave.crest"): registered as read, so a key no row names is a problem.</summary>
        public static int Key(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.IndexOf('*') >= 0) throw new ArgumentException("An emphasis key is layer.part, without wildcards: " + key);
            lock (gate)
            {
                int h = Handle(key);
                read[h] = true;
                return h;
            }
        }

        /// <summary>The handles of a layer's parts, in the order given ("wave", "ribs", "ridges", ...).</summary>
        public static int[] Keys(string layer, params string[] parts)
        {
            var keys = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) keys[i] = Key(layer + "." + parts[i]);
            return keys;
        }

        /// <summary>A key's name.</summary>
        public static string Name(int handle)
        {
            lock (gate) return handle >= 0 && handle < names.Count ? names[handle] : "";
        }

        /// <summary>The highlight id a part's meshes are built with: its block's first (<see cref="EmphasisIds"/>).</summary>
        public static float Id(int handle)
        {
            IdRange ids = EmphasisIds.Of(handle);
            return ids.IsEmpty ? GraphIds.None : ids.Min;
        }

        /// <summary>A part's highlight ids, for <c>Highlighter.Set</c>.</summary>
        public static IdRange Ids(int handle) => EmphasisIds.Of(handle);

        /// <summary>The handle of a key, added (not yet named by a row) when new. Under the lock.</summary>
        static int Handle(string key)
        {
            if (handles.TryGetValue(key, out int h)) return h;
            h = names.Count;
            names.Add(key);
            handles[key] = h;
            declared.Add(false);
            read.Add(false);
            fresh.Add(true);
            Array.Resize(ref target, names.Count);
            Array.Resize(ref eased, names.Count);
            Array.Resize(ref factors, names.Count);
            dirty = true;
            return h;
        }

        static ViewRow RowOf(string id, string source)
        {
            if (!rows.TryGetValue(id, out ViewRow r)) rows[id] = r = new ViewRow { Source = source };
            Know(id);
            return r;
        }

        /// <summary>Under the lock: a row's view is named (a variant's view by its id before the colon).</summary>
        static void Know(string id)
        {
            int colon = id.IndexOf(':');
            known.Add(colon > 0 ? id.Substring(0, colon) : id);
        }

        // ------------------------------------------------------------------ values

        /// <summary>The view whose row is active (the preset on screen, as <c>LandView.PresetId</c>).</summary>
        public static string View
        {
            get
            {
                lock (gate) return view;
            }
        }

        /// <summary>
        /// A key's value now: its eased row value times each of its state multipliers. Read it once a frame per key, not per
        /// item: a multiplier is a delegate call.
        /// </summary>
        public static float Get(int handle)
        {
            float v;
            (string name, Func<float> factor)[] f;
            lock (gate)
            {
                if (handle < 0 || handle >= names.Count) return 0;
                if (dirty) Retarget();
                v = eased[handle];
                f = factors[handle];
            }

            if (f != null)
            {
                for (int i = 0; i < f.Length; i++) v *= f[i].factor();
            }

            return v;
        }

        /// <summary>
        /// A part's renderer at its key's value: its material's alpha, and off while the part is absent (absent, not faint;
        /// an alpha of 0 drew nothing either).
        /// </summary>
        public static void Apply(Renderer renderer, int handle) => Set(renderer, renderer ? Get(handle) : 0);

        /// <summary>
        /// A renderer at an alpha a layer took from its keys for one item's state (a firm's beam: the largest of the states it
        /// is in): off at 0, as <see cref="Apply"/>.
        /// </summary>
        public static void Set(Renderer renderer, float a)
        {
            if (!renderer) return;
            bool on = a > 0;
            if (renderer.enabled != on) renderer.enabled = on;
            if (on) GraphMaterials.SetAlpha(renderer.sharedMaterial, a);
        }

        /// <summary>
        /// A key's value in a view's row (its variant's when given and listed), without the tour's overrides, the state
        /// multipliers or the easing: what the view asks of the part. A view with no row gives the table's "*" column.
        /// </summary>
        public static float Row(string viewId, int handle, string variantId = null)
        {
            viewId = viewId ?? "overview";
            lock (gate) return handle >= 0 && handle < names.Count ? RowValue(viewId, VariantRow(viewId, variantId), handle) : 0;
        }

        /// <summary>Whether a view has a row of its own or its variants' ("view:variant" rows).</summary>
        public static bool HasRow(string viewId)
        {
            lock (gate) return viewId != null && known.Contains(viewId);
        }

        /// <summary>Under the lock: a view's variant's row, or null.</summary>
        static ViewRow VariantRow(string viewId, string variantId) =>
            variantId != null && rows.TryGetValue(viewId + ":" + variantId, out ViewRow v) ? v : null;

        /// <summary>Under the lock: a key's value in a view's variant's row (where it lists the key), else in the view's own
        /// row; absent in a view named only by its variants; the table's "*" column in a view no row names.</summary>
        static float RowValue(string viewId, ViewRow variantRow, int h)
        {
            string name = names[h];
            if (variantRow != null && variantRow.TryGet(h, name, out float x)) return x;
            if (rows.TryGetValue(viewId, out ViewRow r)) return r.TryGet(h, name, out float y) ? y : 0;
            if (known.Contains(viewId)) return 0;
            return rows.TryGetValue(EmphasisTable.Other, out ViewRow o) && o.TryGet(h, name, out float z) ? z : 0;
        }

        // ------------------------------------------------------------------ registration hooks

        /// <summary>
        /// A view's row from the view's own code (a new view, a stage view): its subject, support and context parts (keys or
        /// wildcards such as "social.*"); every other part is absent there. Replaces the view's row, the table's too. A
        /// synthesis view may light more than three subjects.
        /// </summary>
        public static void RegisterView(string viewId, IEnumerable<string> subject, IEnumerable<string> support, IEnumerable<string> context, bool synthesis = false)
        {
            if (string.IsNullOrWhiteSpace(viewId)) throw new ArgumentException("A view's row needs the view's id.");
            var row = new ViewRow { Synthesis = synthesis, Source = "RegisterView" };
            lock (gate)
            {
                Fill(row, subject, Level.Subject);
                Fill(row, support, Level.Support);
                Fill(row, context, Level.Context);
                rows[viewId.Trim()] = row;
                Know(viewId.Trim());
                dirty = true;
            }
        }

        /// <summary>
        /// Parts of a view or variant ("instant:income") lit at a level by one of the several layers that draw it; the row
        /// is made on the first contribution, and a key contributed again takes the new level.
        /// </summary>
        public static void Contribute(string viewOrVariant, IEnumerable<string> keys, Level level)
        {
            if (string.IsNullOrWhiteSpace(viewOrVariant)) throw new ArgumentException("A contribution needs its view (or view:variant).");
            lock (gate)
            {
                Fill(RowOf(viewOrVariant.Trim(), "Contribute"), keys, level);
                dirty = true;
            }
        }

        /// <summary>Under the lock: a row's keys at a level; a named key counts as registered.</summary>
        static void Fill(ViewRow row, IEnumerable<string> keys, Level level)
        {
            if (keys == null) return;
            foreach (string k in keys)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                string key = k.Trim();
                int h = -1;
                if (!key.EndsWith("*", StringComparison.Ordinal))
                {
                    h = Handle(key);
                    declared[h] = true;
                }

                row.Set(h, key, Entry.Of(level));
            }
        }

        /// <summary>
        /// A named state multiplier on a key (a selection, a level of detail, a filter): <see cref="Get"/> multiplies the
        /// key's row by it. Keep it at most 1 and free of allocation: it runs whenever the key is read. Registering a name
        /// again on the same key replaces it (a layer registers its own in Upload, every time its scene loads).
        /// </summary>
        public static void Multiplier(string key, string name, Func<float> factor)
        {
            if (factor == null || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A multiplier needs a name and a function.");
            lock (gate)
            {
                int h = Handle(key);
                read[h] = true;
                (string name, Func<float> factor)[] old = factors[h] ?? new (string, Func<float>)[0];
                int at = Array.FindIndex(old, m => m.name == name);
                (string name, Func<float> factor)[] next = at >= 0 ? ((string, Func<float>)[])old.Clone() : new (string, Func<float>)[old.Length + 1];
                if (at < 0)
                {
                    Array.Copy(old, next, old.Length);
                    at = old.Length;
                }

                next[at] = (name, factor);
                factors[h] = next;
            }
        }

        /// <summary>The named multipliers on a key (for the log).</summary>
        public static IReadOnlyList<string> MultiplierNames(int handle)
        {
            lock (gate) return handle >= 0 && handle < names.Count && factors[handle] != null ? factors[handle].Select(m => m.name).ToList() : new List<string>();
        }

        /// <summary>
        /// A tour step's emphasis (spec 3.1): its keys at their values (a level's <see cref="Alpha"/> or a value; wildcards
        /// allowed; "*" for every other key), over the variant and the view. With <paramref name="cap"/> at 0 or more, every
        /// key it does not list keeps at most that much of the rows beneath (a step's "lit": its keys at 1, the rest at most
        /// .12, <see cref="PushLit"/>). Returns the token that pops it; the latest push wins.
        /// </summary>
        public static int PushOverride(IEnumerable<KeyValuePair<string, float>> values, float cap = -1)
        {
            var o = new Override { Cap = cap };
            lock (gate)
            {
                if (values != null)
                {
                    foreach (KeyValuePair<string, float> kv in values)
                    {
                        string key = kv.Key?.Trim();
                        if (string.IsNullOrEmpty(key)) continue;
                        if (key == "*")
                        {
                            o.Rest = kv.Value;
                            continue;
                        }

                        int h = key.EndsWith("*", StringComparison.Ordinal) ? -1 : Handle(key);
                        if (h >= 0) read[h] = true;
                        o.Values.Set(h, key, Entry.Of(kv.Value));
                    }
                }

                o.Token = ++nextToken;
                overrides.Add(o);
                dirty = true;
                return o.Token;
            }
        }

        /// <summary>A tour step's "lit": its keys at 1, every other key at most <see cref="Context"/>.</summary>
        public static int PushLit(IEnumerable<string> keys) =>
            PushOverride(keys?.Select(k => new KeyValuePair<string, float>(k, Subject)), Context);

        /// <summary>Removes the override a push returned (the step ended); unknown tokens are ignored.</summary>
        public static void PopOverride(int token)
        {
            lock (gate)
            {
                if (overrides.RemoveAll(o => o.Token == token) > 0) dirty = true;
            }
        }

        /// <summary>Shows a view's variant (its "view:variant" row, where it lists a key); null: the view's own row.</summary>
        public static void ShowVariant(string variantId)
        {
            lock (gate)
            {
                variant = string.IsNullOrWhiteSpace(variantId) ? null : variantId.Trim();
                dirty = true;
            }
        }

        // ------------------------------------------------------------------ driven by the land's view layer

        /// <summary>The preset on screen changed: its row becomes the target (snapped on the first focus after a load).</summary>
        internal static void Focus(string viewId)
        {
            lock (gate)
            {
                view = string.IsNullOrWhiteSpace(viewId) ? "overview" : viewId;
                variant = null;
                Retarget();
                if (snapped) return;
                Array.Copy(target, eased, target.Length);
                snapped = true;
            }
        }

        /// <summary>One frame: every key moves toward its target at <see cref="LandStyle.EmphasisRate"/> a second.</summary>
        internal static void Advance(float dt)
        {
            lock (gate)
            {
                if (dirty) Retarget();
                float step = LandStyle.EmphasisRate * dt;
                for (int h = 0; h < eased.Length; h++) eased[h] = Mathf.MoveTowards(eased[h], target[h], step);
            }
        }

        /// <summary>
        /// A new scene load: the overview's row, snapped (the hills draw it before the first focus, as they did), no override or
        /// variant, and the next focus snaps.
        /// </summary>
        internal static void ResetState()
        {
            lock (gate)
            {
                view = "overview";
                variant = null;
                overrides.Clear();
                Retarget();
                Array.Copy(target, eased, target.Length);
                snapped = false;
            }
        }

        /// <summary>Under the lock: every key's target from the overrides, the variant and the view; a new key starts there.</summary>
        static void Retarget()
        {
            ViewRow variantRow = VariantRow(view, variant);
            for (int h = 0; h < names.Count; h++)
            {
                target[h] = Resolve(h, variantRow);
                if (!fresh[h]) continue;
                eased[h] = target[h];
                fresh[h] = false;
            }

            dirty = false;
        }

        static float Resolve(int h, ViewRow variantRow)
        {
            float cap = float.MaxValue;
            string name = names[h];
            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                Override o = overrides[i];
                if (o.Values.TryGet(h, name, out float v)) return Mathf.Min(v, cap);
                if (o.Rest >= 0) return Mathf.Min(o.Rest, cap);
                if (o.Cap >= 0) cap = Mathf.Min(cap, o.Cap);
            }

            return Mathf.Min(RowValue(view, variantRow, h), cap);
        }

        // ------------------------------------------------------------------ the rules (spec 3.1's checks)

        /// <summary>
        /// What breaks the table's rules: a key read that no row registers, a catalog view without a row (the economy's
        /// ViewPresets.All; a smoke-only view such as the palette's card is not one), a row with more than three subjects
        /// (synthesis views exempt; a literal value of the transcription is not a level, so its rows have none until WP14
        /// sets them), a value outside 0..1 (no emphasis lifts a mark past its LDR colour), and keys past the id space.
        /// </summary>
        public static IReadOnlyList<string> Problems()
        {
            var found = new List<string>(tableProblems);
            lock (gate)
            {
                for (int h = 0; h < names.Count; h++)
                {
                    if (read[h] && !declared[h]) found.Add($"{names[h]} is read, but no row registers it (the table, RegisterView or Contribute)");
                }

                if (names.Count > EmphasisIds.MaxParts) found.Add($"{names.Count} keys, more than the {EmphasisIds.MaxParts} the part ids hold below 2^24");
                foreach (KeyValuePair<string, ViewRow> kv in rows)
                {
                    int subjects = kv.Value.Entries.Count(e => e.IsLevel && e.Level == Level.Subject);
                    if (subjects > MaxSubjects && !kv.Value.Synthesis)
                        found.Add($"the row of {kv.Key} ({kv.Value.Source}) has {subjects} subjects, more than {MaxSubjects} (only a synthesis view may)");
                    foreach (KeyValuePair<int, Entry> e in kv.Value.Keys)
                    {
                        if (e.Value.Value < 0 || e.Value.Value > 1) found.Add($"{names[e.Key]} is {e.Value.Value.ToString("0.###", Ci)} in the row of {kv.Key}, outside 0..1");
                    }
                }

                foreach (Override o in overrides)
                {
                    if (o.Rest > 1 || o.Values.Entries.Any(e => e.Value < 0 || e.Value > 1)) found.Add("a tour's override sets a value outside 0..1");
                }
            }

            if (GraphScene.IsEconomy)
            {
                foreach (ViewPreset p in ViewPresets.All)
                {
                    if (!HasRow(p.Id)) found.Add($"the catalog view {p.Id} has no row");
                }
            }

            return found;
        }

        /// <summary>
        /// The smoke check after every capture (SmokeChecks): <see cref="Problems"/>, and on the view on screen every key's
        /// value now (its row, the tour's override and the state multipliers together) within 0..1: no part's emphasis
        /// brightens a data mark past intensity 1.
        /// </summary>
        public static IEnumerable<string> Check(ViewPreset preset)
        {
            var found = new List<string>(Problems());
            int n;
            lock (gate) n = names.Count;
            for (int h = 0; h < n; h++)
            {
                float v = Get(h);
                if (v < 0 || v > 1.0001f) found.Add($"{Name(h)} is at {v.ToString("0.###", Ci)} in {preset?.Id ?? View}, past intensity 1");
            }

            return found;
        }

        /// <summary>
        /// "[Why] Emphasis: K keys, V views, N problems" (the problems follow as errors), the state multipliers, the part ids,
        /// and as a warning the keys at .5 or more in more than six catalog views (every view looks alike: a warning until
        /// WP14 retunes the views). Logged once a scene has loaded its layers (the land's view layer's first frame).
        /// </summary>
        internal static void LogSummary()
        {
            IReadOnlyList<string> problems = Problems();
            int keys, views, multipliers = 0, multiplied = 0;
            var bright = new List<string>();
            List<string> catalog = GraphScene.IsEconomy ? ViewPresets.All.Select(p => p.Id).ToList() : new List<string>();
            lock (gate)
            {
                keys = declared.Count(d => d);
                views = rows.Keys.Count(k => k != EmphasisTable.Other && k.IndexOf(':') < 0);
                for (int h = 0; h < names.Count; h++)
                {
                    if (factors[h] != null && factors[h].Length > 0)
                    {
                        multiplied++;
                        multipliers += factors[h].Length;
                    }

                    int at = catalog.Count(id => RowValue(id, null, h) >= .5f);
                    if (at > MaxBrightViews) bright.Add(names[h] + " (" + at.ToString(Ci) + ")");
                }
            }

            IdRange first = EmphasisIds.Of(0), last = EmphasisIds.Of(Math.Max(0, keys - 1));
            Debug.Log($"[Why] Emphasis: {keys} keys, {views} views, {problems.Count} problems; {multipliers} state multipliers on {multiplied} keys; " +
                      $"part ids {first.Min}-{last.Max} ({EmphasisIds.Block} a key, below 2^24 and clear of GraphIds and EconomyIds); " +
                      $"view now {View}, eased at {LandStyle.EmphasisRate.ToString("0.0", Ci)} a second");
            foreach (string p in problems) Debug.LogError("[Why] Emphasis problem: " + p);
            if (bright.Count > 0)
            {
                Debug.LogWarning($"[Why] Emphasis: {bright.Count} keys at 0.5 or more in more than {MaxBrightViews} of the {catalog.Count} views " +
                                 "(every view looks alike; a warning until WP14 retunes the views): " + string.Join(", ", bright));
            }
        }

        // ------------------------------------------------------------------ the smoke sequence: lighting a part by its ids

        /// <summary>A pixel brightens when a channel rises by more than this (of 255): the smoke diffs' threshold.</summary>
        const int Brighter = 8;

        /// <summary>How far past the crest line's projected box a lit pixel may fall (px): its 2.4 px width, anti-aliasing.</summary>
        const int BoxMarginPx = 6;

        /// <summary>The fewest pixels the crest must brighten, and the share of the frame that may brighten outside its box.</summary>
        const int MinLitPixels = 200;

        const float StrayShare = 1e-4f;

        /// <summary>
        /// <c>-smokeextra emphasis</c>: the overview, held still (time stopped and the flow clock held, which no time scale
        /// stops, so the flows' pulses, the markers and the rivers' sparks keep their place; bloom off, so a lit part's own
        /// pixels are measured, not its glow; the label system's labels hidden, which a highlight dims and lays out again), captured
        /// (economy-emphasis-crest-off), then wave.crest lit by its part's ids (<see cref="EmphasisIds"/>, nothing dimmed)
        /// and captured again (economy-emphasis-crest-on). Only pixels inside the crest line's projected box (grown by
        /// <see cref="BoxMarginPx"/>) may brighten: a part id another mesh shared would light pixels outside it. Logs the
        /// counts and the box (from the frame's top left, for Tools/emphasis-diff.py --crest); an error when the crest did not
        /// light or more than <see cref="StrayShare"/> of the frame brightened outside it.
        /// </summary>
        internal static IEnumerator HighlightCheck()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            if (Xr.XrSession.Active)
            {
                Debug.LogWarning("[Why] Emphasis highlight: not measured with -xr (it reads a flat screen's frame)");
                yield break;
            }

            int crest = Key("wave.crest");
            IdRange ids = Ids(crest);
            float timeScale = Time.timeScale;
            bool flowHeld = false;
            UnityEngine.Rendering.Universal.Bloom bloom = root.Bloom;
            bool bloomActive = bloom != null && bloom.active;
            GameObject labels = root.Labels != null ? root.Labels.gameObject : null;
            bool labelsActive = labels != null && labels.activeSelf;
            Texture2D off = null, on = null;
            try
            {
                // the crest is drawn in the overview, unless the overview is the sweep through time (HillSweepLayer): then in
                // the landscape view, which draws it at full strength as the overview did
                root.Focus(Layers.HillSweepLayer.On ? "landscape" : "overview", 0f);
                Highlighter.Clear();
                yield return new WaitForSecondsRealtime(3f);
                for (float waited = 0; waited < 3f && Shader.GetGlobalFloat("_WhyDim") > .002f; waited += Time.unscaledDeltaTime) yield return null;
                Time.timeScale = 0;
                FlowClock.Hold();
                flowHeld = true;
                if (bloom != null) bloom.active = false;
                if (labels != null) labels.SetActive(false);
                yield return new WaitForSecondsRealtime(.5f);
                yield return SmokeChecks.Shot("economy-emphasis-crest-off");
                yield return new WaitForEndOfFrame();
                off = ScreenCapture.CaptureScreenshotAsTexture();
                Highlighter.Set(new[] { ids }, GraphStyle.HighlightGlow, 0f);
                // the highlight eases in over about 0.7 s (Highlighter.Tick, on the unscaled clock)
                yield return new WaitForSecondsRealtime(1.5f);
                yield return SmokeChecks.Shot("economy-emphasis-crest-on");
                yield return new WaitForEndOfFrame();
                on = ScreenCapture.CaptureScreenshotAsTexture();
                if (off.width != on.width || off.height != on.height)
                {
                    Debug.LogError("[Why] Emphasis highlight: the two captures differ in size");
                    yield break;
                }

                RectInt box = CrestBox(root.Rig.Cam, off.width, off.height);
                Color32[] a = off.GetPixels32(), b = on.GetPixels32();
                int inside = 0, outside = 0, w = off.width;
                int sx0 = int.MaxValue, sy0 = int.MaxValue, sx1 = -1, sy1 = -1;
                for (int i = 0; i < a.Length; i++)
                {
                    if (b[i].r - a[i].r <= Brighter && b[i].g - a[i].g <= Brighter && b[i].b - a[i].b <= Brighter) continue;
                    int x = i % w, y = i / w;
                    if (x >= box.xMin && x < box.xMax && y >= box.yMin && y < box.yMax)
                    {
                        inside++;
                        continue;
                    }

                    outside++;
                    sx0 = Math.Min(sx0, x);
                    sy0 = Math.Min(sy0, y);
                    sx1 = Math.Max(sx1, x);
                    sy1 = Math.Max(sy1, y);
                }

                int h = off.height;
                // from the top left, as the frames' PNG rows run (the capture's rows run from the bottom)
                string Box(int x0, int y0, int x1, int y1) => $"x {x0}-{x1}, y {h - y1}-{h - y0}";
                string where = box.width > 0 ? Box(box.xMin, box.yMin, box.xMax, box.yMax) : "none on screen";
                Debug.Log($"[Why] Emphasis highlight wave.crest (ids {ids.Min}-{ids.Max}): {inside} px brighter inside its projected box " +
                          $"({where}, grown {BoxMarginPx} px; {w}x{h}), {outside} outside" +
                          (outside > 0 ? " (those within " + Box(sx0, sy0, sx1 + 1, sy1 + 1) + ")" : ""));
                if (inside < MinLitPixels) Debug.LogError($"[Why] Emphasis highlight: lighting wave.crest's ids brightened {inside} px of its box (at least {MinLitPixels} expected)");
                if (outside > StrayShare * a.Length)
                {
                    Debug.LogError($"[Why] Emphasis highlight: lighting wave.crest's ids brightened {outside} px outside its box (at most " +
                                   $"{(StrayShare * a.Length).ToString("0", Ci)}): another mesh carries the crest's ids");
                }
            }
            finally
            {
                Time.timeScale = timeScale;
                if (flowHeld) FlowClock.Release();
                if (bloom != null) bloom.active = bloomActive;
                if (labels != null) labels.SetActive(labelsActive);
                Highlighter.Clear();
                if (off) Object.Destroy(off);
                if (on) Object.Destroy(on);
            }
        }

        /// <summary>
        /// The crest line's box on the capture (its rows from the bottom), grown by <see cref="BoxMarginPx"/>: the points
        /// HillWaveLayer draws it through (the ground at the crest from 8 units toward the road to 18 past the hills' end),
        /// projected; empty when none is in front of the camera.
        /// </summary>
        static RectInt CrestBox(Camera cam, int width, int height)
        {
            HillLandscape land = Layers.HillLandscapeLayer.Current;
            if (land == null || cam == null) return new RectInt(0, 0, 0, 0);
            LandFrame frame = EconomyStage.Land();
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            float sx = width / (float)Mathf.Max(1, Screen.width), sy = height / (float)Mathf.Max(1, Screen.height);
            for (float x = HillLandscape.FootX - 8; x <= HillLandscape.FootX + land.CrestWidth + 18; x += .5f)
            {
                Vector3 s = cam.WorldToScreenPoint(frame.World(new Vector3(x, land.Ground(x, land.CrestZ) + .05f, land.CrestZ)));
                if (s.z <= 0) continue;
                x0 = Mathf.Min(x0, s.x * sx);
                x1 = Mathf.Max(x1, s.x * sx);
                y0 = Mathf.Min(y0, s.y * sy);
                y1 = Mathf.Max(y1, s.y * sy);
            }

            if (x1 < x0) return new RectInt(0, 0, 0, 0);
            int left = Mathf.Clamp(Mathf.FloorToInt(x0) - BoxMarginPx, 0, width), right = Mathf.Clamp(Mathf.CeilToInt(x1) + BoxMarginPx, 0, width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(y0) - BoxMarginPx, 0, height), top = Mathf.Clamp(Mathf.CeilToInt(y1) + BoxMarginPx, 0, height);
            return new RectInt(left, bottom, right - left, top - bottom);
        }
    }
}
