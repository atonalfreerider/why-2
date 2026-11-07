using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Competitive advantage through history (Docs/hills-design/HISTORY.md; the user, 7 Nov 2026, verbatim in
    /// Docs/research/advantage/brief-v4.md): the advantage view's language carried back in time for states, armies, navies,
    /// alliances, populations, faiths, companies and labs.
    /// <list type="bullet">
    /// <item><b>Compressed</b>: each actor a tube of point rings along time, as thick as the cube root of what it measured
    /// there (thin and faint where nothing was measured), in lanes stacked above the hill: a state and the regimes that
    /// succeeded it share a lane, as do the coalitions one after another; a company's heirs run beside it. Moments are
    /// sparks on the holder's lane with ties to its rivals and allies; periods are brackets above the lanes; a gold cursor
    /// stands at the year.</item>
    /// <item><b>Exploded</b>: at a year, the tubes' slices swell into occluding point-cloud bodies (their measures as lobes)
    /// and spread into a cross-section facing down the time axis: rows of polities, forces and enterprises, each row the
    /// moment's holder and allies on the left and its rivals on the right; every advantage documented for the period a
    /// ring around its holder, walled by its sources, each wall as thick as Jev judged it keeps rivals out.</item>
    /// </list>
    /// The AI story runs on the wave's own time axis, above the hills of its years; the age of revolutions, before the wave,
    /// on a stretched stretch of its own (<see cref="PreWaveScale"/> units a year). The stories' walkthroughs drive the view
    /// through states named in their cues: "history:&lt;story&gt;:&lt;year&gt;[:x][:sel=&lt;id&gt;][:pop=&lt;id,id&gt;]".
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillHistoryLayer : GraphLayer
    {
        public const string View = "history", Sequence = "history";

        public override int Order => 60;

        public override IEnumerable<string> RequiredTexts => new[] { History.Path };

        enum Part { Tubes, Marks, Bodies, Walls, Outlines, Count }

        static readonly string[] PartNames = { "tubes", "marks", "bodies", "walls", "outlines" };
        static readonly int[] Keys = EconomyEmphasis.Keys("history", PartNames);

        /// <summary>The lanes: tubes run along time over the hill's middle, the lowest lane this high above the hill.</summary>
        public const float LaneX = 130, LaneBase = 112, LaneGap = 8.5f, GroupGap = 7;

        /// <summary>A story before the wave runs on its own stretch of the axis: this many units a year, ending this far before
        /// the wave's first year (the wave's own two units a year would make forty years a sliver beside the lanes).</summary>
        public const float PreWaveScale = 7, PreWaveGap = 140;

        const float RowGap = 26, BodyGap = 15, ColGap = 40, LineGap = 28;    // a stacked body's name stands in the gap above it
        const int PerLine = 3;

        /// <summary>The free screen across its width: right of the portrait cards when they show (24 + 330 of 1920 pixels and a
        /// margin), left of the panel or a story's card (about 520 of 1920 and a margin).</summary>
        const float FreeRight = .70f, FreeLeftCards = .20f, FreeLeftBare = .10f;

        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");

        static HillHistoryLayer current;

        public static string StoryId { get; private set; } = "ai";
        public static float Year { get; private set; } = 1971.9f;
        public static bool Exploded { get; private set; }
        public static string Selected { get; private set; }

        /// <summary>Raised when the story, the year, the cross-section or the selection changes (the panel follows).</summary>
        public static event Action Changed;

        History model;
        GameObject content;
        readonly MeshRenderer[] partRenderer = new MeshRenderer[(int)Part.Count];
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        readonly Dictionary<string, Shown> bodies = new Dictionary<string, Shown>();
        Material occluderMaterial;
        bool labelsShown, dirty = true;
        float explode, explodeTarget, outlinesFor = -1;
        Vector3 outlineEye, arenaMin, arenaMax;
        readonly List<(int side, Vector3 at)> columnHeads = new List<(int, Vector3)>();
        string builtFor;
        List<string> stateNames = new List<string>();
        string[] popups = Array.Empty<string>();
        Layout layout;

        /// <summary>A body of the cross-section: its renderers, its shape, where it comes from (its tube's slice) and where it
        /// stands exploded, and whether anything was measured for it at the year.</summary>
        sealed class Shown
        {
            public MeshRenderer Body, Occluder;
            public Organism Shape;
            public Vector3 From, To;
            public bool Measured, LabelBelow, TopLine;
            public int Side;
            public string Main;
        }

        // ------------------------------------------------------------------ the axis and the lanes

        /// <summary>Land-local z of a year in a story: the wave's own axis for a story in its years (the slope of its first
        /// years before them), a stretch of its own ending before the wave for a story before it.</summary>
        public static float T(History.Story s, float year)
        {
            if (s != null && s.To < 1900) return HillLandscape.Z(1940) - PreWaveGap - (s.To + 1 - year) * PreWaveScale;
            if (year >= 1940) return HillLandscape.Z(year);
            return HillLandscape.Z(1940) + (year - 1940) * (HillLandscape.Z(1941) - HillLandscape.Z(1940));
        }

        /// <summary>A story's lanes: each actor's lane and the span it is drawn over, the lanes' heights (top first), the heirs'
        /// links, the periods' brackets and the extent.</summary>
        sealed class Layout
        {
            public History.Story Story;
            public readonly List<float> LaneY = new List<float>();
            public readonly List<string> LaneGroup = new List<string>();
            public readonly Dictionary<string, int> LaneOf = new Dictionary<string, int>();
            public readonly Dictionary<string, (float from, float to)> Span = new Dictionary<string, (float, float)>();
            public readonly List<(string from, string to)> Heirs = new List<(string, string)>();
            public readonly List<(History.Period p, int level)> Brackets = new List<(History.Period, int)>();
            public float Top, Bottom, Z0, Z1, BracketTop;
        }

        static readonly string[] Groups = { "polities", "alliances", "forces", "peoples", "enterprises", "labs" };

        static string GroupOf(string kind) => kind switch
        {
            "state" => "polities", "alliance" => "alliances", "army" or "navy" => "forces", "population" or "religion" or "movement" => "peoples",
            "lab" => "labs", _ => "enterprises",
        };

        /// <summary>A polity, an alliance or a force continues in its successor (one lane); a company's heir is another company
        /// (a lane of its own beside it).</summary>
        static bool SharesLane(string group) => group == "polities" || group == "alliances" || group == "forces" || group == "peoples";

        static Layout Lay(History.Story s)
        {
            var L = new Layout { Story = s };
            float storyEnd = s.To + 1;
            var lanes = new List<(string group, float end, List<string> ids)>();
            foreach (string group in Groups)
            {
                var members = s.Actors.Where(a => GroupOf(a.Kind) == group)
                    .OrderBy(a => float.IsNaN(a.From) ? s.From - 1 : a.From).ThenBy(a => a.Name, StringComparer.Ordinal).ToList();
                if (members.Count == 0) continue;
                // heirs right after their predecessors
                var ordered = new List<History.Actor>();
                void AddLine(History.Actor a)
                {
                    if (ordered.Contains(a)) return;
                    ordered.Add(a);
                    foreach (var h in members) if (a.Successor == h.Id) AddLine(h);
                }

                foreach (var a in members) if (!members.Any(p => p.Successor == a.Id)) AddLine(a);
                foreach (var a in members) if (!ordered.Contains(a)) ordered.Add(a);
                foreach (var a in ordered)
                {
                    float from = Mathf.Max(s.From, float.IsNaN(a.From) ? s.From : a.From);
                    float to = float.IsNaN(a.To) ? storyEnd : Mathf.Min(storyEnd, Mathf.Approximately(a.To, Mathf.Round(a.To)) ? a.To + 1 : a.To);
                    if (to <= from) to = Mathf.Min(storyEnd, from + 1);
                    var pred = ordered.FirstOrDefault(p => p.Successor == a.Id && L.LaneOf.ContainsKey(p.Id));
                    int lane = -1;
                    bool continues = pred != null && SharesLane(group);
                    if (continues)
                    {
                        lane = L.LaneOf[pred.Id];
                        // the predecessor ends where its successor begins (the Republic's government became the Consulate)
                        var ps = L.Span[pred.Id];
                        L.Span[pred.Id] = (ps.from, Mathf.Min(ps.to, from));
                    }
                    else if (group == "alliances")
                        lane = lanes.FindIndex(l => l.group == group && l.end <= from + .05f);
                    if (pred != null && !continues) L.Heirs.Add((pred.Id, a.Id));
                    if (lane < 0)
                    {
                        lanes.Add((group, to, new List<string>()));
                        lane = lanes.Count - 1;
                    }

                    var l0 = lanes[lane];
                    lanes[lane] = (l0.group, continues ? to : Mathf.Max(to, l0.end), l0.ids);
                    l0.ids.Add(a.Id);
                    L.LaneOf[a.Id] = lane;
                    L.Span[a.Id] = (from, to);
                }
            }

            // heights: the first lane on top, a gap between groups, the last lane at the base
            int groupsUsed = lanes.Select(l => l.group).Distinct().Count();
            float y = LaneBase + (lanes.Count - 1) * LaneGap + (groupsUsed - 1) * GroupGap;
            string last = null;
            foreach (var l in lanes)
            {
                if (last != null && l.group != last) y -= GroupGap;
                L.LaneY.Add(y);
                L.LaneGroup.Add(l.group);
                y -= LaneGap;
                last = l.group;
            }

            L.Top = L.LaneY.Count > 0 ? L.LaneY[0] : LaneBase;
            L.Bottom = L.LaneY.Count > 0 ? L.LaneY[L.LaneY.Count - 1] : LaneBase;
            L.Z0 = T(s, s.From);
            L.Z1 = T(s, storyEnd);
            // the periods' brackets above the lanes, each on the lowest level free over its years
            var ends = new List<float>();
            foreach (var p in s.Periods.Where(p => !float.IsNaN(p.From) && !float.IsNaN(p.To)).OrderBy(p => p.From))
            {
                int level = ends.FindIndex(e => e <= p.From + .05f);
                if (level < 0) { ends.Add(p.To); level = ends.Count - 1; }
                else ends[level] = p.To;
                L.Brackets.Add((p, level));
            }

            L.BracketTop = L.Top + 9 + Mathf.Max(0, ends.Count - 1) * 5.5f;
            return L;
        }

        Layout LayoutOf(History.Story s)
        {
            if (s == null) return null;
            if (layout == null || layout.Story != s) layout = Lay(s);
            return layout;
        }

        static float TubeR(double size) => double.IsNaN(size) ? .45f : Mathf.Clamp(.36f * Mathf.Pow((float)size, 1f / 3), .45f, 4f);

        /// <summary>A lobe's radius: 2.2 times the cube root of its size, up to 42 (a $5 trillion company, a nation's GDP): a
        /// lower cap flattened the AI story's largest comparisons into one size.</summary>
        static float LobeR(double size) => Mathf.Clamp(2.2f * Mathf.Pow((float)Math.Max(0, size), 1f / 3), 1.6f, 42f);

        public static Color KindColor(string kind) => kind switch
        {
            "state" => new Color(.48f, .66f, .95f), "army" => new Color(.95f, .44f, .34f), "navy" => new Color(.30f, .62f, .98f),
            "alliance" => new Color(1f, .82f, .40f), "religion" => new Color(.74f, .56f, 1f), "population" => new Color(1f, .74f, .62f),
            "movement" => new Color(1f, .50f, .62f), "company" => new Color(.40f, .90f, .86f), "lab" => new Color(.62f, .95f, .70f),
            _ => new Color(.80f, .82f, .86f),
        };

        public static string KindWord(string kind) => kind switch
        {
            "state" => "state", "army" => "army", "navy" => "navy", "alliance" => "alliance", "religion" => "church", "population" => "people",
            "movement" => "movement", "lab" => "lab", _ => "company",
        };

        static Color PartColor(string part) => part switch
        {
            "army" or "troops" or "subsidisedTroops" => new Color(.95f, .46f, .34f), "navy" or "ships" or "sailors" => new Color(.32f, .58f, .98f),
            "revenue" or "gdp" or "incomeTax" => new Color(.55f, .85f, .55f), "debt" or "deficit" or "debtInterest" => new Color(.86f, .30f, .26f),
            "spending" => new Color(1f, .62f, .40f), "users" => new Color(.98f, .60f, .85f),
            "parameters" or "compute" or "transistors" => new Color(.62f, .90f, 1f), "employees" or "people" or "defections" => new Color(1f, .78f, .62f),
            "marketValue" or "valuation" => new Color(.40f, .90f, .86f), "adherents" => new Color(.74f, .56f, 1f), "capital" or "capex" => new Color(1f, .82f, .45f),
            "casualties" or "executions" => new Color(.72f, .22f, .30f), "population" => new Color(1f, .74f, .62f),
            _ => new Color(.80f, .80f, .84f),
        };

        public static Color TriadColor(string triad) => triad switch
        {
            "information" => HillAdvantageLayer.InfoColor, "execution" => HillAdvantageLayer.ExecColor, "environment" => HillAdvantageLayer.EnvColor,
            _ => new Color(1f, .9f, .7f),
        };

        /// <summary>The view's key for the HUD's legend.</summary>
        public static readonly HillPresets.ViewKey Key = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("State", KindColor("state")), ("Army", KindColor("army")), ("Navy", KindColor("navy")), ("Alliance", KindColor("alliance")),
                ("People or church", KindColor("population")), ("Company", KindColor("company")), ("Lab", KindColor("lab")),
            },
            "Each tube is one actor through time, as thick as the cube root of what it measured then (troops, people, ships, money, users); faint where nothing was measured. Sparks: the moments that turned an advantage. Opened at a year: each actor's measures as lobes, its advantage a ring walled by its sources (thicker where Jev judged the wall higher).");

        // ------------------------------------------------------------------ life cycle

        public override void Prepare(GraphContext ctx) => History.Provide(ctx.Text(History.Path));

        public override void Upload(GraphContext ctx)
        {
            current = this;
            EconomyStage.Land().Place(transform);
            // drawn alone: behind a cross-section, or under the lanes, the wave and the hill's marks were only noise
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => "history." + p), Array.Empty<string>(), Array.Empty<string>(), synthesis: true);
            stateNames = CueNames();
            ViewVariants.Register(View, () => stateNames.Count, k => ShowState(k >= 0 && k < stateNames.Count ? stateNames[k] : null),
                k => k >= 0 && k < stateNames.Count ? stateNames[k] : k.ToString());
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        /// <summary>The states the stories' walkthroughs name in their cues (and each story's opening), so the director can
        /// show them by name.</summary>
        static List<string> CueNames()
        {
            var names = new List<string> { "history:ai:1971.9", "history:rev:1789.5:x" };
            foreach (string path in new[] { "Data/Walkthrough/story_ai_tour", "Data/Walkthrough/story_revolutions_tour" })
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(asset.text, "\"show\"\\s*:\\s*\"(history:[^\"]+)\""))
                    if (!names.Contains(m.Groups[1].Value)) names.Add(m.Groups[1].Value);
                Resources.UnloadAsset(asset);
            }

            return names;
        }

        /// <summary>Shows a named state: "history:&lt;story&gt;:&lt;year&gt;[:x][:sel=&lt;id&gt;][:pop=&lt;id,id&gt;]". Null keeps the
        /// story, the year and the cross-section and drops the selection and the portraits.</summary>
        public static void ShowState(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Selected = null;
                if (current != null) { current.popups = Array.Empty<string>(); current.dirty = true; }
                HistoryPopups.Clear();
                Changed?.Invoke();
                return;
            }

            var parts = name.Split(':');
            if (parts.Length < 3 || parts[0] != "history") return;
            float year = Year;
            if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) year = y;
            string pop = parts.FirstOrDefault(p => p.StartsWith("pop=", StringComparison.Ordinal))?.Substring(4);
            Set(parts[1], year, parts.Contains("x"), parts.FirstOrDefault(p => p.StartsWith("sel=", StringComparison.Ordinal))?.Substring(4),
                string.IsNullOrEmpty(pop) ? Array.Empty<string>() : pop.Split(','), SmokeRun ? 0 : 2.2f);
        }

        /// <summary>Shows a story at a year, compressed or exploded, with a selection and these portraits; the camera flies
        /// there in the given seconds.</summary>
        public static void Set(string story, float year, bool exploded, string selected, string[] portraits, float seconds = 1.6f)
        {
            StoryId = story ?? StoryId;
            Year = year;
            Exploded = exploded;
            Selected = selected;
            if (current != null)
            {
                current.popups = portraits ?? Array.Empty<string>();
                current.explodeTarget = Exploded ? 1 : 0;
                current.dirty = true;
                current.FlyToState(seconds);
                current.ShowPopups();
            }

            Changed?.Invoke();
        }

        /// <summary>The panel's controls: another story (at its first moment), the next or previous moment, the cross-section.</summary>
        public static void ShowStory(string story)
        {
            var s = History.Get()?.StoryById(story);
            if (s == null) return;
            float first = s.Moments.Where(m => !float.IsNaN(m.Year)).Select(m => m.Year).DefaultIfEmpty(s.From).Min();
            Set(story, first, Exploded, null, PeopleOf(s, first));
        }

        public static void Step(int direction)
        {
            var s = History.Get()?.StoryById(StoryId);
            if (s == null) return;
            var years = s.Moments.Where(m => !float.IsNaN(m.Year)).Select(m => m.Year).Distinct().OrderBy(y => y).ToList();
            if (years.Count == 0) return;
            float next = direction > 0 ? years.FirstOrDefault(y => y > Year + .02f) : years.LastOrDefault(y => y < Year - .02f);
            if (next == 0) next = direction > 0 ? years[years.Count - 1] : years[0];
            Set(StoryId, next, Exploded, null, PeopleOf(s, next));
        }

        public static void Explode(bool exploded) => Set(StoryId, Year, exploded, Selected, current != null ? current.popups : Array.Empty<string>());

        /// <summary>The people of the moment nearest a year (up to three), for the portraits when browsing without a tour.</summary>
        public static string[] PeopleOf(History.Story s, float year)
        {
            var m = NearestMoment(s, year, 1.5f);
            if (m == null) return Array.Empty<string>();
            var ids = new[] { m.Holder }.Concat(m.Allies).Concat(m.Rivals).ToList();
            // only people active at the moment's own date: grown (Wellington was 8 at Saratoga), alive (Pitt died in January
            // 1806) and in office where the research's dates say so (George III, under the Regency from 1811)
            return s.People.Where(p => p.Actors.Any(ids.Contains) && m.Year >= p.ActiveFrom && m.Year <= p.ActiveTo)
                .OrderBy(p => p.Actors.Select(a => ids.IndexOf(a)).Where(i => i >= 0).DefaultIfEmpty(99).Min())
                .Take(3).Select(p => p.Id).ToArray();
        }

        /// <summary>The moment nearest a year (within the given years); of two on the same date, the selected actor's own.</summary>
        public static History.Moment NearestMoment(History.Story s, float year, float within) =>
            s?.Moments.Where(m => !float.IsNaN(m.Year) && Mathf.Abs(m.Year - year) <= within)
                .OrderBy(m => Mathf.Round(Mathf.Abs(m.Year - year) * 100))
                .ThenBy(m => Selected != null && (m.Holder == Selected || m.Allies.Contains(Selected) || m.Rivals.Contains(Selected)) ? 0 : 1)
                .FirstOrDefault();

        public override void OnFocus(ViewPreset preset)
        {
            if (preset != null && preset.Id == View) { dirty = true; FlyToState(1.4f); ShowPopups(); }
            else HistoryPopups.Clear();
        }

        void OnDestroy()
        {
            Clear();
            if (current == this) current = null;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            bool here = LandView.PresetId == View;
            model = History.Get();
            if (!here || model == null)
            {
                if (content) content.SetActive(false);
                ShowLabels(false);
                return;
            }

            if (content == null)
            {
                content = new GameObject("History");
                content.transform.SetParent(transform, false);
            }

            content.SetActive(true);
            string key = StoryId + "|" + Year.ToString("0.000", CultureInfo.InvariantCulture) + "|" + Selected + "|" + Exploded;
            if (dirty || key != builtFor)
            {
                dirty = false;
                builtFor = key;
                Rebuild();
            }

            explode = Mathf.MoveTowards(explode, explodeTarget, GraphTime.Delta / 1.6f);
            Place();
            if (explode > .95f)
            {
                Vector3 eye = Eye();
                if (outlinesFor != 1 || (eye - outlineEye).sqrMagnitude > 4) { outlineEye = eye; outlinesFor = 1; Outlines(); }
            }
            else if (outlinesFor != 0) { outlinesFor = 0; Drop(Part.Outlines); }

            ShowLabels(true);
        }

        Vector3 Eye()
        {
            var cam = GraphRoot.Instance != null && GraphRoot.Instance.Rig != null ? GraphRoot.Instance.Rig.Cam : null;
            return cam != null && content != null ? content.transform.InverseTransformPoint(cam.transform.position) : Vector3.zero;
        }

        // ------------------------------------------------------------------ the build

        void Clear()
        {
            DropBodies();
            foreach (var o in owned) if (o) Destroy(o);
            owned.Clear();
            Array.Clear(partRenderer, 0, partRenderer.Length);
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
        }

        void DropBodies()
        {
            foreach (var v in bodies.Values)
            {
                if (!v.Body) continue;
                foreach (var mf in v.Body.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
                if (v.Body.sharedMaterial) { owned.Remove(v.Body.sharedMaterial); Destroy(v.Body.sharedMaterial); }
                Destroy(v.Body.gameObject);
            }

            bodies.Clear();
        }

        void Drop(Part part)
        {
            var r = partRenderer[(int)part];
            partRenderer[(int)part] = null;
            if (!r) return;
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
            if (r.sharedMaterial) { owned.Remove(r.sharedMaterial); Destroy(r.sharedMaterial); }
            Destroy(r.gameObject);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, int queue)
        {
            Drop(part);
            var mesh = b.ToMesh("History " + part);
            owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, false));
            owned.Add(m);
            var r = AddMesh("History " + part, mesh, m);
            r.transform.SetParent(content.transform, false);
            partRenderer[(int)part] = r;
            return r;
        }

        void Rebuild()
        {
            var story = model?.StoryById(StoryId) ?? model?.Stories.FirstOrDefault();
            if (story == null || content == null) return;
            var L = LayoutOf(story);
            Tubes(story, L);
            Marks(story, L);
            Bodies(story, L);
            Walls(story);
            outlinesFor = -1;
            MakeLabels(story, L);
            if (labelsShown) HillLabels.Set(this, labels);
        }

        /// <summary>Each actor's tube: a ring of points every 1.3 units of time, as thick as the cube root of what it measured
        /// there, faint where nothing was. Opened at a year, the tubes give way to the cross-section's bodies.</summary>
        void Tubes(History.Story s, Layout L)
        {
            var b = new LineMeshBuilder(65536);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Tubes]);
            float perYear = Mathf.Abs(T(s, s.From + 1) - T(s, s.From)), step = 1.3f / Mathf.Max(.5f, perYear);
            foreach (var a in s.Actors)
            {
                if (!L.LaneOf.TryGetValue(a.Id, out int lane) || !L.Span.TryGetValue(a.Id, out var span)) continue;
                float y = L.LaneY[lane];
                Color c = KindColor(a.Kind);
                bool lit = Selected == null || Selected == a.Id;
                if (Exploded) continue;
                float to = span.to;
                for (float t = span.from; t <= to + .001f; t += step)
                {
                    string part = a.MainPartAt(t);
                    double size = part != null ? a.SizeAt(part, t) : double.NaN;
                    float r = TubeR(size), z = T(s, t);
                    bool measured = !double.IsNaN(size);
                    int n = Mathf.Clamp(Mathf.RoundToInt(r * 3.6f), 5, 16);
                    float alpha = (measured ? .82f : .26f) * (lit ? 1 : .3f);
                    // opened, the cross-section stands alone: the tubes, seen end-on behind it, would only be noise
                    if (alpha < .02f) continue;
                    float phase = (t * 7.3f) % 1f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = (i + phase) * 2 * Mathf.PI / n;
                        Vector3 p = new Vector3(LaneX + Mathf.Cos(ang) * r, y + Mathf.Sin(ang) * r, z);
                        b.AddSegment(p, p + Vector3.up * PointBodies.DotLength, WithAlpha(c, alpha), 2.1f, 0, id, .93f);
                    }
                }
            }

            // a company's heir: a dotted arc from its parent's tube where it began to its own
            foreach (var (from, to) in L.Heirs)
            {
                if (!L.LaneOf.TryGetValue(from, out int la) || !L.LaneOf.TryGetValue(to, out int lb) || !L.Span.TryGetValue(to, out var sp)) continue;
                if (Exploded) continue;
                float z = T(s, sp.from);
                Vector3 a0 = new Vector3(LaneX, L.LaneY[la], z), a1 = new Vector3(LaneX, L.LaneY[lb], z + 2);
                for (int i = 0; i < 12; i += 2)
                {
                    Vector3 p = Vector3.Lerp(a0, a1, i / 12f) + Vector3.left * Mathf.Sin(i / 12f * Mathf.PI) * 3;
                    Vector3 q = Vector3.Lerp(a0, a1, (i + 1) / 12f) + Vector3.left * Mathf.Sin((i + 1) / 12f * Mathf.PI) * 3;
                    b.AddSegment(p, q, new Color(.75f, .95f, .92f, .55f), 1.4f, 0, id, .9f);
                }
            }

            Lines(b, Part.Tubes, 3191);
        }

        /// <summary>Moments as sparks on the holder's lane, dashed ties to its rivals (the turn's colour) and allies (gold); the
        /// periods' brackets above the lanes; the year's cursor across them.</summary>
        void Marks(History.Story s, Layout L)
        {
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Marks]);
            float y0 = L.Bottom - 6, y1 = L.BracketTop + 4;
            if (!Exploded)
            {
                foreach (var m in s.Moments)
                {
                    if (float.IsNaN(m.Year) || m.Holder == null || !L.LaneOf.TryGetValue(m.Holder, out int h)) continue;
                    float z = T(s, m.Year);
                    Vector3 at = new Vector3(LaneX, L.LaneY[h], z);
                    Color c = TriadColor(m.Triad);
                    float near = Mathf.Abs(m.Year - Year) < .8f ? 1.5f : 1;
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI / 4;
                        Vector3 ray = new Vector3(Mathf.Cos(ang) * .35f, Mathf.Sin(ang), Mathf.Cos(ang)) * (k % 2 == 0 ? 3.6f : 2.1f) * near;
                        b.AddSegment(at, at + ray, WithAlpha(c, .92f), 2.2f, 0, id, 1.6f);
                    }

                    foreach (var (other, gold) in m.Rivals.Select(x => (x, false)).Concat(m.Allies.Select(x => (x, true))))
                    {
                        if (!L.LaneOf.TryGetValue(other, out int o) || o == h) continue;
                        Vector3 to = new Vector3(LaneX, L.LaneY[o], z), d = to - at;
                        float len = d.magnitude;
                        for (float t = 1.6f; t < len - 1; t += 2.2f)
                            b.AddSegment(at + d / len * t, at + d / len * Mathf.Min(len, t + 1.1f), WithAlpha(gold ? new Color(1f, .84f, .45f) : c, .62f), 1.5f, 0, id, .95f);
                    }
                }

                foreach (var (p, level) in L.Brackets)
                {
                    float y = L.Top + 9 + level * 5.5f, z0 = T(s, p.From), z1 = T(s, p.To);
                    Color c = new Color(.84f, .86f, .92f, .55f);
                    for (float z = z0; z < z1; z += 2.2f) b.AddSegment(new Vector3(LaneX, y, z), new Vector3(LaneX, y, Mathf.Min(z1, z + 1.4f)), c, 1.4f, 0, id, .92f);
                    b.AddSegment(new Vector3(LaneX, y, z0), new Vector3(LaneX, y - 2.2f, z0), c, 1.4f, 0, id, .92f);
                    b.AddSegment(new Vector3(LaneX, y, z1), new Vector3(LaneX, y - 2.2f, z1), c, 1.4f, 0, id, .92f);
                }

                // the year's cursor: a gold line through the lanes
                float zc = T(s, Year);
                for (float y = y0; y < y1; y += 1.6f)
                    b.AddSegment(new Vector3(LaneX, y, zc), new Vector3(LaneX, Mathf.Min(y1, y + .9f), zc), new Color(1f, .86f, .5f, .8f), 1.8f, 0, id, 1.2f);
            }

            Lines(b, Part.Marks, 3192);
        }

        // ------------------------------------------------------------------ the cross-section

        /// <summary>An actor's body at a year: what it is drawn by at the core, what else it measured then as lobes (what it
        /// fields above, what it owes or lost below), each as wide as the cube root of its size. Each lobe's amount is the
        /// volume its radius stands for, so the fused body keeps the lobes' sizes. An actor measured nowhere near the year is a
        /// small grey core.</summary>
        static List<ContestNetwork.Part> PartsAt(History.Actor a, float year, IReadOnlyDictionary<string, string> forcesStanding, out bool measured)
        {
            var parts = new List<ContestNetwork.Part>();
            // a state whose own forces stand as their own actors is never drawn by their soldiers or ships
            var kept = OwnForceParts(a, forcesStanding);
            string main = MainOf(a, year, forcesStanding);
            measured = main != null;
            Color kind = KindColor(a.Kind);
            if (main == null)
            {
                parts.Add(new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, 2.2f, Vol(2.2f), new Color(.55f, .58f, .64f), .3f));
                return parts;
            }

            float coreR = LobeR(a.SizeAt(main, year));
            parts.Add(new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, coreR, Vol(coreR), Color.Lerp(kind, PartColor(main), .25f), .7f));
            // a state's own forces drawn as their own actors keep their soldiers there; an actor drawn by its own measures (the
            // crown by its money) keeps to them, its people being another actor's
            foreach (string part in a.Series.Select(v => v.Part).Distinct()
                         .Where(p => p != main && Lobed(p) && (kept == null || !kept.Contains(p)) &&
                                     (a.DrawnBy.Length == 0 || (a.DrawnBy.Contains(p) && p != "population"))).Take(5))
            {
                double size = a.SizeAt(part, year);
                if (double.IsNaN(size) || size <= 0) continue;
                // what it owes, spends away or loses hangs below; what it holds or fields stands above
                bool below = part == "debt" || part == "deficit" || part == "debtInterest" || part == "spending" || part == "casualties" || part == "executions";
                float r = LobeR(size);
                parts.Add(new ContestNetwork.Part(below ? ContestNetwork.PartKind.Debt : ContestNetwork.PartKind.Built, Vector3.zero, r, Vol(r), PartColor(part), below ? .9f : .8f));
            }

            return parts;
        }

        static double Vol(float r) => Math.Pow(r / ContestNetwork.KR, 3);

        static readonly string[] ArmyParts = { "army", "troops" }, NavyParts = { "navy", "ships", "sailors" };

        /// <summary>
        /// The parts a state's own forces keep while they stand as their own actors: an army's soldiers, a navy's ships and
        /// sailors. Only those: while the Republic's navy alone stands, its levy of 300,000 is still its own. Null when none.
        /// </summary>
        static string[] OwnForceParts(History.Actor a, IReadOnlyDictionary<string, string> forcesStanding)
        {
            bool army = false, navy = false;
            foreach (string id in a.Forces)
                if (forcesStanding.TryGetValue(id, out string kind))
                {
                    if (kind == "navy") navy = true;
                    else army = true;
                }

            if (!army && !navy) return null;
            return (army ? ArmyParts : Array.Empty<string>()).Concat(navy ? NavyParts : Array.Empty<string>()).ToArray();
        }

        /// <summary>The measure a body is drawn by at a year (its label and card name the same one): a state whose own forces
        /// stand as their own actors is never drawn by their soldiers or ships.</summary>
        static string MainOf(History.Actor a, float year, IReadOnlyDictionary<string, string> forcesStanding) =>
            a.MainPartAt(year, true, OwnForceParts(a, forcesStanding));

        /// <summary>The armies and navies measured at a year, by id, with their kind.</summary>
        static Dictionary<string, string> ForcesStanding(History.Story s, float year) =>
            s.Actors.Where(f => (f.Kind == "army" || f.Kind == "navy") && f.MainPartAt(year, true) != null).ToDictionary(f => f.Id, f => f.Kind);

        /// <summary>Whether a measure is a part of the body (what the actor holds, fields or owes: people, troops, ships,
        /// money, members, users), not a property of its product (a chip's transistors, a model's parameters) or a count of
        /// the few who made it, which the label names instead.</summary>
        static bool Lobed(string part) => part switch
        {
            "transistors" or "parameters" or "compute" or "people" or "defections" or "gdpShare" or "industrialShare" or "share" or "conscripted" => false,
            _ => true,
        };

        /// <summary>The bodies of the year, built once per story, year and selection: the actors measured then, and the
        /// moment's participants that were not (small and grey); arranged in rows of polities, forces and enterprises, the
        /// moment's holder and allies on the left, its rivals on the right, around the tubes' cross-section.</summary>
        void Bodies(History.Story s, Layout L)
        {
            DropBodies();
            if (occluderMaterial == null) { occluderMaterial = PointBodies.OccluderMaterial(); owned.Add(occluderMaterial); }
            // the sides come from a moment within a year and a half (the 1950 picture is no one's contest)
            var moment = NearestMoment(s, Year, 1.5f);
            var side = new Dictionary<string, int>();
            if (moment != null)
            {
                if (moment.Holder != null) side[moment.Holder] = -1;
                foreach (var x in moment.Allies) side[x] = -1;
                foreach (var x in moment.Rivals) side[x] = 1;
            }

            // the participants: of the nearest moment and of any on the same date (Waterloo and the Congress of Vienna), and every
            // holder of an advantage documented for the year, so its ring has a body to stand around
            var near = NearestMoment(s, Year, 1.5f);
            var participants = new HashSet<string>();
            if (near != null)
                foreach (var m in s.Moments.Where(m => !float.IsNaN(m.Year) && Mathf.Abs(m.Year - near.Year) < .05f))
                    foreach (var x in new[] { m.Holder }.Concat(m.Allies).Concat(m.Rivals))
                        if (x != null) participants.Add(x);
            foreach (var moat in ActiveMoats(s)) if (moat.Actor != null) participants.Add(moat.Actor);
            var forcesStanding = ForcesStanding(s, Year);
            // how far each holder's ring reaches beyond its body: the ring, its thickest wall and its sources' names
            var reach = new Dictionary<string, float>();
            foreach (var moat in ActiveMoats(s))
            {
                float wall = Sources(moat).Select(x => double.IsNaN(x.Wall) ? 0 : (float)x.Wall).DefaultIfEmpty(0).Max();
                float r = 4 + 1.2f + 1.2f + 2.4f * wall + 7;
                reach[moat.Actor] = Mathf.Max(reach.TryGetValue(moat.Actor, out float o) ? o : 0, r);
            }

            var items = new List<(History.Actor a, ContestNetwork.Body body, Organism shape, bool measured, int row, int side)>();
            foreach (var a in s.Actors)
            {
                if (!L.Span.TryGetValue(a.Id, out var span)) continue;
                bool alive = Year >= span.from - .01f && Year <= span.to + .01f;
                var parts = PartsAt(a, Year, forcesStanding, out bool measured);
                // a participant of the moment stands even unmeasured (grey); one its moment has just ended (the Republic at
                // Brumaire) stands for a quarter-year after its lane ends, so the moment never loses a side
                bool participant = participants.Contains(a.Id), ended = Year > span.to + .01f && Year - span.to <= .25f;
                if (!measured && !(participant && (alive || Mathf.Abs(Year - span.from) < 1.5f || ended))) continue;
                if (measured && !alive && !(participant && ended)) continue;
                var body = new ContestNetwork.Body { Id = a.Id, Name = a.Name, Kind = ContestNetwork.Kind.Company, CustomParts = parts, Alive = true, Value = double.NaN };
                var shape = new Organism(body, null, measured ? 3 : 2, trueVolume: true);
                string g = GroupOf(a.Kind);
                int row = g == "polities" || g == "alliances" || g == "peoples" ? 0 : g == "forces" ? 1 : 2;
                items.Add((a, body, shape, measured, row, side.TryGetValue(a.Id, out int sd) ? sd : 0));
            }

            // rows top to bottom, each centred on the lanes' x; from the future +x is screen-left, so the left side gets the high x
            float Foot(Organism shape, string id) => shape.MaxRadius + (reach.TryGetValue(id, out float e) ? e : 2);
            // the contest: a column for each side (the moment's holder and allies, the others, its rivals), a row for each kind
            // (polities, forces, enterprises), at most three bodies to a line within a cell; from the future +x is screen-left,
            // so the holder's column takes the high x
            var cols = new[] { -1, 0, 1 }.Where(sd => items.Any(x => x.side == sd)).ToList();
            var kinds = items.Select(x => x.row).Distinct().OrderBy(k => k).ToList();

            // the contest's width and height with n bodies to a line
            (float w, float h) Extent(int n)
            {
                var cw = new Dictionary<int, float>();
                var rh = new Dictionary<int, float>();
                foreach (int c in cols)
                    foreach (int k in kinds)
                    {
                        var list = items.Where(x => x.side == c && x.row == k).OrderByDescending(x => x.shape.MaxRadius).ToList();
                        if (list.Count == 0) continue;
                        float lw = 0, lh = 0;
                        int count = 0;
                        for (int i = 0; i < list.Count; i += n, count++)
                        {
                            var line = list.Skip(i).Take(n).ToList();
                            lw = Mathf.Max(lw, line.Sum(x => 2 * Foot(x.shape, x.a.Id)) + BodyGap * (line.Count - 1));
                            lh += 2 * line.Max(x => Foot(x.shape, x.a.Id));
                        }

                        lh += LineGap * (count - 1);
                        cw[c] = Mathf.Max(cw.TryGetValue(c, out float a) ? a : 0, lw);
                        rh[k] = Mathf.Max(rh.TryGetValue(k, out float b) ? b : 0, lh);
                    }

                return (cols.Sum(c => cw[c]) + ColGap * Mathf.Max(0, cols.Count - 1), kinds.Sum(k => rh[k]) + RowGap * Mathf.Max(0, kinds.Count - 1));
            }

            // how many bodies stand to a line: the count the camera can come nearest to, so a wide contest stacks into the free
            // screen's height instead of shrinking into its width (fewer lines when two counts fit alike)
            int perLine = PerLine;
            if (FitFrame(out float tanV, out float tanH, out float freeWidth, out _))
            {
                float best = float.PositiveInfinity;
                for (int n = PerLine; n >= 1; n--)
                {
                    var (ew, eh) = Extent(n);
                    float d = FitDistance(ew + 26, eh + 40, tanV, tanH, freeWidth);
                    if (d < best - .5f) { best = d; perLine = n; }
                }
            }
            var cells = new Dictionary<(int col, int kind), List<List<(History.Actor a, ContestNetwork.Body body, Organism shape, bool measured, int row, int side)>>>();
            var colW = new Dictionary<int, float>();
            var rowH = new Dictionary<int, float>();
            foreach (int c in cols)
                foreach (int k in kinds)
                {
                    var list = items.Where(x => x.side == c && x.row == k).OrderByDescending(x => x.shape.MaxRadius).ToList();
                    if (list.Count == 0) continue;
                    var lines = new List<List<(History.Actor a, ContestNetwork.Body body, Organism shape, bool measured, int row, int side)>>();
                    for (int i = 0; i < list.Count; i += perLine) lines.Add(list.Skip(i).Take(perLine).ToList());
                    cells[(c, k)] = lines;
                    float w = lines.Max(l => l.Sum(x => 2 * Foot(x.shape, x.a.Id)) + BodyGap * (l.Count - 1));
                    float h = lines.Sum(l => 2 * l.Max(x => Foot(x.shape, x.a.Id))) + LineGap * (lines.Count - 1);
                    colW[c] = Mathf.Max(colW.TryGetValue(c, out float cw) ? cw : 0, w);
                    rowH[k] = Mathf.Max(rowH.TryGetValue(k, out float rh) ? rh : 0, h);
                }

            float totalW = cols.Sum(c => colW[c]) + ColGap * Mathf.Max(0, cols.Count - 1);
            float totalH = kinds.Sum(k => rowH[k]) + RowGap * Mathf.Max(0, kinds.Count - 1);
            float mid = (L.Top + L.Bottom) * .5f, z = T(s, Year);
            columnHeads.Clear();
            float xCol = LaneX + totalW * .5f;
            foreach (int c in cols)
            {
                float colCentre = xCol - colW[c] * .5f;
                float yRow = mid + totalH * .5f;
                float colBottom = float.PositiveInfinity;
                foreach (int k in kinds)
                {
                    if (cells.TryGetValue((c, k), out var lines))
                    {
                        float yLine = yRow;
                        bool top = k == kinds[0];    // the arena's top line, where the columns' names stand side by side
                        foreach (var line in lines)
                        {
                            float lineH = line.Max(x => Foot(x.shape, x.a.Id));
                            float lw = line.Sum(x => 2 * Foot(x.shape, x.a.Id)) + BodyGap * (line.Count - 1), x0 = colCentre + lw * .5f;
                            int inLine = 0;
                            foreach (var it in line)
                            {
                                float rad = it.shape.MaxRadius, foot = Foot(it.shape, it.a.Id);
                                var to = new Vector3(x0 - foot, yLine - lineH, z);
                                colBottom = Mathf.Min(colBottom, to.y - foot);
                                bool below = inLine++ % 2 == 1;    // neighbours' names alternate above and below, so they do not cover each other
                                x0 -= 2 * foot + BodyGap;
                                int lane = L.LaneOf.TryGetValue(it.a.Id, out int ln) ? ln : 0;
                                var from = new Vector3(LaneX, L.LaneY.Count > 0 ? L.LaneY[Mathf.Clamp(lane, 0, L.LaneY.Count - 1)] : LaneBase, z);
                                it.body.BodyR = rad;
                                var (mr, or) = BuildBody(it.a, it.shape, it.measured);
                                bodies[it.a.Id] = new Shown { Body = mr, Occluder = or, Shape = it.shape, From = from, To = to, Measured = it.measured, Side = it.side,
                                                              Main = MainOf(it.a, Year, forcesStanding), LabelBelow = below, TopLine = top };
                            }

                            yLine -= 2 * lineH + LineGap;
                            top = false;
                        }
                    }

                    yRow -= rowH[k] + RowGap;
                }

                // the column's head under it, where no body's name stands
                if (moment != null && cols.Count > 1 && !float.IsPositiveInfinity(colBottom)) columnHeads.Add((c, new Vector3(colCentre, colBottom - 12, z)));
                xCol -= colW[c] + ColGap;
            }

            arenaMin = new Vector3(LaneX, mid, z);
            arenaMax = arenaMin;
            foreach (var kv in bodies)
            {
                float f = Foot(kv.Value.Shape, kv.Key);
                arenaMin = Vector3.Min(arenaMin, kv.Value.To - Vector3.one * f);
                arenaMax = Vector3.Max(arenaMax, kv.Value.To + Vector3.one * f);
            }
        }

        (MeshRenderer body, MeshRenderer occluder) BuildBody(History.Actor a, Organism shape, bool measured)
        {
            var lb = new LineMeshBuilder(8192);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Bodies]);
            float lit = (Selected == null || Selected == a.Id ? 1 : .35f) * (measured ? 1 : .55f);
            PointBodies.Cloud(lb, shape, id, 3, new System.Random(Hash(a.Id)), new Color(1f, .97f, .86f), lit);
            var mesh = lb.ToMesh("History body " + a.Id);
            owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3193, false));
            owned.Add(m);
            var mr = AddMesh("History body " + a.Id, mesh, m);
            mr.transform.SetParent(content.transform, false);
            var occ = PointBodies.Occluder(shape, 2, "History occluder " + a.Id);
            owned.Add(occ);
            var or = AddMesh("History occluder " + a.Id, occ, occluderMaterial);
            or.transform.SetParent(mr.transform, false);
            mr.enabled = or.enabled = false;
            return (mr, or);
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s ?? "") h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        /// <summary>The bodies' places and sizes this frame: from their tubes' slices (small) into the cross-section (full) as
        /// it opens, and back as it closes; walls and outlines only once it stands open.</summary>
        void Place()
        {
            float e = Mathf.SmoothStep(0, 1, explode);
            foreach (var v in bodies.Values)
            {
                if (!v.Body) continue;
                var tr = v.Body.transform;
                tr.localPosition = Vector3.Lerp(v.From, v.To, e);
                tr.localScale = Vector3.one * Mathf.Lerp(.06f, 1, e);
                v.Body.enabled = e > .02f;
                if (v.Occluder) v.Occluder.enabled = e > .5f;
            }

            foreach (var p in new[] { Part.Walls, Part.Outlines })
                if (partRenderer[(int)p]) partRenderer[(int)p].enabled = e > .95f;
        }

        /// <summary>The advantages documented for the year: each holder's boundary, a ring around its body in the
        /// cross-section's plane, walled by one arc per source, each as thick as Jev judged it keeps rivals out (a source not
        /// yet judged draws thin and dashed); and the year's moments as bursts from the holder toward its rivals.</summary>
        void Walls(History.Story s)
        {
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Walls]);
            foreach (var moat in ActiveMoats(s))
            {
                if (!bodies.TryGetValue(moat.Actor, out var hb)) continue;
                float R = hb.Shape.MaxRadius + 4;
                Vector3 c = hb.To;
                int n = Mathf.Clamp(Mathf.RoundToInt(R * 2.4f), 40, 220);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 2 * Mathf.PI / n;
                    Vector3 p = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * R, t = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0);
                    b.AddSegment(p - t * .16f, p + t * .16f, WithAlpha(new Color(1f, .88f, .5f), .7f), 2f, 0, id, .95f);
                }

                var srcs = Sources(moat);
                for (int k = 0; k < srcs.Count; k++)
                {
                    float a0 = (k * 2 * Mathf.PI / srcs.Count) + .07f, a1 = ((k + 1) * 2 * Mathf.PI / srcs.Count) - .07f;
                    double w = srcs[k].Wall;
                    // a wall Jev had no settled answer for (its answer spread over the levels) draws dashed, like one not judged
                    bool judged = !double.IsNaN(w) && !Uncertain(srcs[k]);
                    float thick = !double.IsNaN(w) ? 1.2f + 2.4f * (float)w : 1.2f;
                    Color col = HillAdvantageLayer.SourceColor(srcs[k].Source);
                    for (float d = 0; d <= thick + .01f; d += 1.1f)
                    {
                        float rr = R + 1.2f + d;
                        int segs = Mathf.Max(4, Mathf.RoundToInt((a1 - a0) * rr / (judged ? 1.2f : 2.4f)));
                        for (int i = 0; i < segs; i++)
                        {
                            float aa = Mathf.Lerp(a0, a1, i / (float)segs), ab = Mathf.Lerp(a0, a1, (i + (judged ? 1f : .5f)) / segs);
                            b.AddSegment(c + new Vector3(Mathf.Cos(aa), Mathf.Sin(aa), 0) * rr, c + new Vector3(Mathf.Cos(ab), Mathf.Sin(ab), 0) * rr,
                                WithAlpha(col, d == 0 ? .95f : .6f), d == 0 ? 2.4f : 1.6f, 0, id, 1f);
                        }
                    }
                }
            }

            foreach (var m in s.Moments.Where(m => !float.IsNaN(m.Year) && Mathf.Abs(m.Year - Year) < .75f))
            {
                if (m.Holder == null || !bodies.TryGetValue(m.Holder, out var hb)) continue;
                foreach (var rival in m.Rivals)
                {
                    if (!bodies.TryGetValue(rival, out var rb)) continue;
                    Vector3 dir = (rb.To - hb.To).normalized, at = hb.To + dir * (hb.Shape.MaxRadius + 2);
                    Color c = TriadColor(m.Triad);
                    for (int k = 0; k < 7; k++)
                    {
                        float ang = (k - 3) * 14f;
                        Vector3 ray = Quaternion.AngleAxis(ang, Vector3.forward) * dir * (k % 2 == 0 ? 7 : 4.5f);
                        b.AddSegment(at, at + ray, WithAlpha(c, .95f), 2.4f, 0, id, 1.8f);
                    }
                }
            }

            Lines(b, Part.Walls, 3194);
        }

        /// <summary>The advantages documented for the year, the latest begun per actor (where two of an actor's periods meet,
        /// one ring).</summary>
        IEnumerable<History.Moat> ActiveMoats(History.Story s) =>
            s.Moats.Where(m => MoatStands(s, m, Year)).GroupBy(m => m.Actor).Select(g => g.OrderByDescending(m => m.From).First());

        /// <summary>
        /// Whether a documented advantage stands at a year: within its period (a year-only end runs to that year's close), and
        /// not after its holder's lane ends, so no advantage the panel lists is left without a body to ring.
        /// </summary>
        public static bool MoatStands(History.Story s, History.Moat m, float year)
        {
            float to = Mathf.Approximately(m.To, Mathf.Round(m.To)) ? m.To + 1 : m.To;
            var L = current != null ? current.LayoutOf(s) : Lay(s);
            if (L != null && m.Actor != null && L.Span.TryGetValue(m.Actor, out var span)) to = Mathf.Min(to, span.to);
            return year >= m.From - .01f && year <= to + .01f;
        }

        /// <summary>A moat's sources in the advantage view's order, the same source twice joined (its strongest judged wall).</summary>
        static List<History.MoatSource> Sources(History.Moat moat) => moat.Sources.GroupBy(x => x.Source)
            .Select(g => g.OrderByDescending(x => double.IsNaN(x.Wall) ? -1 : x.Wall).First())
            .OrderBy(x => { int i = Array.IndexOf(HillAdvantageLayer.SourceOrder, x.Source); return i < 0 ? 99 : i; }).ToList();

        /// <summary>Each body's silhouette from where the camera stands (rebuilt as it moves), in its kind's colour.</summary>
        void Outlines()
        {
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Outlines]);
            Vector3 eye = Eye();
            var s = model?.StoryById(StoryId);
            foreach (var kv in bodies)
            {
                var a = s?.Get(kv.Key);
                float lit = Selected == null || Selected == kv.Key ? 1 : .35f;
                PointBodies.Silhouette(b, kv.Value.Shape, kv.Value.To, eye, id, (kv.Value.Measured ? .7f : .35f) * lit, KindColor(a?.Kind), 1.4f);
            }

            Lines(b, Part.Outlines, 3195);
            Place();
        }

        // ------------------------------------------------------------------ the camera

        void FlyToState(float seconds)
        {
            GraphRoot root = GraphRoot.Instance;
            model = model ?? History.Get();
            var story = model?.StoryById(StoryId);
            if (root == null || root.Rig == null || root.Rig.Cam == null || story == null) return;
            var L = LayoutOf(story);
            // the cross-section's extent decides the pose: build it now if the year changed
            string key = StoryId + "|" + Year.ToString("0.000", CultureInfo.InvariantCulture) + "|" + Selected + "|" + Exploded;
            if (content != null && (dirty || key != builtFor)) { dirty = false; builtFor = key; Rebuild(); }
            // the picture fits the screen the panel (or a story's card) and the portrait cards leave free, its middle moved there
            FitFrame(out float tanV, out float tanH, out float FreeWidth, out float shift);
            ViewPreset p;
            if (Exploded && bodies.Count > 0)
            {
                // down the time axis from the future (+x runs to the left), the cross-section filling the free screen
                Vector3 c = (arenaMin + arenaMax) * .5f;
                float d = FitDistance(arenaMax.x - arenaMin.x + 26, arenaMax.y - arenaMin.y + 40, tanV, tanH, FreeWidth);
                float dx = shift * 2 * d * tanH;
                p = HillPresets.HistoryPose(new Vector3(c.x - dx, c.y + 4, T(story, Year)), 180, 3, d);
            }
            else
            {
                // from beside the lanes, time running left to right: the whole story when it fits, else a window on the year
                float h = L.BracketTop + 8 - (L.Bottom - 8), dH = h * .5f / (tanV * .82f);
                float len = Mathf.Abs(L.Z1 - L.Z0), dW = len * .5f / (tanH * FreeWidth * .92f);
                float d = Mathf.Max(dH, dW);
                float zc = (L.Z0 + L.Z1) * .5f;
                if (d > dH * 1.5f)
                {
                    d = dH * 1.5f;
                    float half = d * tanH * FreeWidth * .92f;
                    zc = Mathf.Clamp(T(story, Year), Mathf.Min(L.Z0, L.Z1) + half, Mathf.Max(L.Z0, L.Z1) - half);
                }

                // time runs to the right: the camera's target moves forward in time so the lanes sit left of the panel
                p = HillPresets.HistoryPose(new Vector3(LaneX, (L.BracketTop + L.Bottom) * .5f, zc + shift * 2 * d * tanH), 266, 5, Mathf.Max(120, d));
            }

            root.Rig.FlyTo(p.Pose(), seconds);
        }

        /// <summary>
        /// The camera's half-angle tangents, and the share of the screen's width the panel (or a story's card) and the
        /// portrait cards leave free, with how far its middle sits from the screen's. False without a camera.
        /// </summary>
        bool FitFrame(out float tanV, out float tanH, out float freeWidth, out float shift)
        {
            float freeLeft = popups.Length > 0 ? FreeLeftCards : FreeLeftBare;
            freeWidth = FreeRight - freeLeft;
            shift = .5f - (freeLeft + FreeRight) * .5f;
            tanV = Mathf.Tan(30 * Mathf.Deg2Rad);
            tanH = tanV * 16 / 9f;
            GraphRoot root = GraphRoot.Instance;
            if (root == null || root.Rig == null || root.Rig.Cam == null) return false;
            var cam = root.Rig.Cam;
            tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            tanH = tanV * Mathf.Max(1, cam.aspect);
            return true;
        }

        /// <summary>
        /// How far the camera stands to fit a cross-section w wide and h high: the bodies take three quarters of the free
        /// width (their names reach past them, and a name that would run under the panel or a story's card is not shown).
        /// </summary>
        static float FitDistance(float w, float h, float tanV, float tanH, float freeWidth) =>
            Mathf.Max(w * .5f / (tanH * freeWidth * .74f), h * .5f / (tanV * .76f), 110);

        void ShowPopups()
        {
            var story = (model ?? History.Get())?.StoryById(StoryId);
            if (story == null || LandView.PresetId != View) { HistoryPopups.Clear(); return; }
            var items = new List<(string, string, string)>();
            foreach (string id in popups)
            {
                var person = story.PersonById(id);
                if (person != null) { items.Add((person.Image ?? person.Id, person.Name, person.Role)); continue; }
                // an actor's card says what it is and what it measured then, from the verified series (not the research's prose)
                var actor = story.Get(id);
                if (actor != null) items.Add((actor.Image ?? actor.Id, actor.Name, CardLine(story, actor)));
            }

            HistoryPopups.Show(items);
        }

        /// <summary>The measure a value is, when its unit does not say (money of any kind reads alike: "274.43 USD bn" is a
        /// market value beside a revenue).</summary>
        public static string MeasureWord(string part) => part switch
        {
            "marketValue" => "market value", "revenue" => "revenue", "gdp" => "GDP", "debt" => "debt", "valuation" => "valuation",
            "capital" => "capital", "capex" => "capital spending", "spending" => "spending", "incomeTax" => "income tax",
            "debtInterest" => "interest on the debt", _ => null,
        };

        /// <summary>A value as a label gives it: its measure when the unit does not say, the fact's own number and unit, and
        /// its year when it is not the year shown.</summary>
        public static string ValueText(History.Value v)
        {
            string word = MeasureWord(v.Part);
            // a value the fact gives for a span of years ("c. 1800-1815") is not of the year its row was filed under: no year
            bool dated = Mathf.FloorToInt(v.Year) != Mathf.FloorToInt(Year) && !Spans(v.Unit, Year);
            return (word != null ? word + ": " : "") + Raw(v) + " " + v.Unit + (dated ? " (" + Mathf.FloorToInt(v.Year) + ")" : "");
        }

        /// <summary>
        /// A value as a body's label gives it: the number line kept short, and a long note of its unit under it, wrapped, so
        /// the label stays narrow enough to stand beside its neighbours and clear of the panel and the story's card.
        /// </summary>
        static string LabelValue(History.Value v)
        {
            string full = ValueText(v);
            if (full.Length <= 46) return full;
            string unit = v.Unit ?? "";
            int open = unit.IndexOf(" (", StringComparison.Ordinal);
            if (open < 0 || !unit.EndsWith(")", StringComparison.Ordinal)) return Wrap(full, 44);
            string word = MeasureWord(v.Part);
            bool dated = Mathf.FloorToInt(v.Year) != Mathf.FloorToInt(Year) && !Spans(v.Unit, Year);
            string line = (word != null ? word + ": " : "") + Raw(v) + " " + unit.Substring(0, open) + (dated ? " (" + Mathf.FloorToInt(v.Year) + ")" : "");
            return line + "\n<size=76%>" + Wrap(unit.Substring(open + 2, unit.Length - open - 3), 44) + "</size>";
        }

        /// <summary>Text broken into lines of at most about this many characters, at spaces.</summary>
        static string Wrap(string text, int width)
        {
            var sb = new System.Text.StringBuilder();
            int col = 0;
            foreach (string w in text.Split(' '))
            {
                if (col > 0 && col + 1 + w.Length > width) { sb.Append('\n'); col = 0; }
                else if (col > 0) { sb.Append(' '); col++; }
                sb.Append(w);
                col += w.Length;
            }

            return sb.ToString();
        }

        static readonly System.Text.RegularExpressions.Regex Circa = new System.Text.RegularExpressions.Regex(@"c\.\s*(\d{4})\s*[-\u2013]\s*(\d{4})");

        /// <summary>Whether a unit gives its value for a span of years ("c. 1800-1815") that holds the year.</summary>
        static bool Spans(string unit, float year)
        {
            var m = Circa.Match(unit ?? "");
            return m.Success && year >= int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - .01f &&
                   year < int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 1;
        }

        static string CardLine(History.Story s, History.Actor a)
        {
            string kind = KindWord(a.Kind);
            kind = char.ToUpperInvariant(kind[0]) + kind.Substring(1);
            string main = MainOf(a, Year, ForcesStanding(s, Year));
            var v = main != null ? a.Shown(main, Year) : null;
            if (v == null) return kind;
            return kind + " · " + ValueText(v);
        }

        // ------------------------------------------------------------------ labels

        void MakeLabels(History.Story s, Layout L)
        {
            labels.Clear();
            if (!Exploded)
            {
                foreach (var a in s.Actors)
                {
                    if (!L.LaneOf.TryGetValue(a.Id, out int lane) || !L.Span.TryGetValue(a.Id, out var span)) continue;
                    labels.Add(new HillLabel
                    {
                        Text = a.Name, At = new Vector3(LaneX, L.LaneY[lane] + 2.6f, T(s, span.from) + 1), Role = LabelRole.Note, Part = "history.tubes",
                        Rank = 120 - lane + (Selected == a.Id ? 300 : 0), Color = KindColor(a.Kind), OnTop = true, Align = TMPro.TextAlignmentOptions.Left,
                    });
                }

                foreach (var m in s.Moments)
                {
                    if (float.IsNaN(m.Year) || m.Holder == null || !L.LaneOf.TryGetValue(m.Holder, out int h)) continue;
                    bool now = Mathf.Abs(m.Year - Year) < .8f;
                    labels.Add(new HillLabel
                    {
                        Text = (now ? "<b>" : "") + Mathf.FloorToInt(m.Year) + " · " + m.Title + (now ? "</b>" : ""), At = new Vector3(LaneX, L.LaneY[h] - 4.5f, T(s, m.Year)),
                        Role = LabelRole.Note, Part = "history.marks", Rank = now ? 400 : 90 - Mathf.Abs(m.Year - Year), Color = TriadColor(m.Triad), OnTop = true,
                        Plate = new Color(0, 0, 0, .66f),
                    });
                }

                foreach (var (p, level) in L.Brackets)
                    labels.Add(new HillLabel
                    {
                        Text = p.Title, At = new Vector3(LaneX, L.Top + 10.6f + level * 5.5f, T(s, (p.From + p.To) * .5f)), Role = LabelRole.Note, Part = "history.marks",
                        Rank = 60, Color = new Color(.88f, .9f, .95f), OnTop = true,
                    });
                labels.Add(new HillLabel
                {
                    Text = "<b>" + YearText(Year) + "</b>", At = new Vector3(LaneX, L.Bottom - 9, T(s, Year)), Role = LabelRole.Name, Part = "history.marks",
                    Rank = 500, Color = new Color(1f, .86f, .5f), OnTop = true,
                });
            }
            else
            {
                var ringed = new HashSet<string>(ActiveMoats(s).Select(m => m.Actor));
                bool three = bodies.Values.Select(b => b.Side).Distinct().Count() == 3;
                foreach (var kv in bodies)
                {
                    var a = s.Get(kv.Key);
                    if (a == null) continue;
                    string main = kv.Value.Main;
                    var v = main != null ? a.Shown(main, Year) : null;
                    string measure = v != null ? "\n" + LabelValue(v) : "\n<i>not measured</i>";
                    float above = ringed.Contains(kv.Key) ? kv.Value.Shape.MaxRadius + 4 + 3 : kv.Value.Shape.MaxRadius + 4.5f;
                    // the middle column's names stand half a name further out than its neighbours', so three columns' names
                    // at one height do not meet
                    bool middle = three && kv.Value.Side == 0 && kv.Value.TopLine;
                    labels.Add(new HillLabel
                    {
                        // a long name ("Jacobins and the Committee of Public Safety") takes two lines, so the label stays narrow
                        Text = "<b>" + (a.Name.Length > 32 ? Wrap(a.Name, 30) : a.Name) + "</b> <size=80%>" + KindWord(a.Kind) + "</size><size=86%>" + measure + "</size>",
                        At = kv.Value.To + Vector3.up * (kv.Value.LabelBelow ? -above : above),
                        Role = LabelRole.Name, Part = "history.bodies", Rank = 300 + kv.Value.Shape.MaxRadius + (Selected == a.Id ? 400 : 0), Plate = new Color(0, 0, 0, .78f),
                        Color = KindColor(a.Kind), OnTop = true,
                        // a name whose place a neighbour's name took stacks beyond it, away from its body, rather than going unshown
                        PixelOffset = middle ? new Vector2(0, kv.Value.LabelBelow ? -Stagger : Stagger) : Vector2.zero,
                        Fallbacks = Steps(kv.Value.LabelBelow ? -1 : 1, middle ? Stagger : 0),
                    });
                }

                var named = NearestMoment(s, Year, 1.5f)?.Holder;
                foreach (var moat in ActiveMoats(s))
                {
                    if (!bodies.TryGetValue(moat.Actor, out var hb)) continue;
                    // the walls are named for the moment's holder and the selection; the others' rings speak through the panel
                    if (moat.Actor != named && moat.Actor != Selected) continue;
                    float R = hb.Shape.MaxRadius + 4;
                    var srcs = Sources(moat);
                    for (int k = 0; k < srcs.Count; k++)
                    {
                        float a = (k + .5f) * 2 * Mathf.PI / srcs.Count;
                        double w = srcs[k].Wall;
                        float thick = double.IsNaN(w) ? 1.2f : 1.2f + 2.4f * (float)w;
                        Vector3 at = hb.To + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (R + thick + 4);
                        // from the future +x runs to screen-left: a name whose place is taken steps outward from the ring
                        var outward = new Vector2(-Mathf.Cos(a), Mathf.Sin(a));
                        labels.Add(new HillLabel
                        {
                            Text = "<b>" + SourceWord(srcs[k].Source) + "</b>" + (double.IsNaN(w) ? "" : " <size=80%>wall " + w.ToString("0.0", CultureInfo.InvariantCulture) + " of 4" +
                                   (Uncertain(srcs[k]) ? ", uncertain" : "") + "</size>"),
                            At = at, Role = LabelRole.Note, Part = "history.walls", Rank = 250 + (float)(double.IsNaN(w) ? 0 : w), Color = HillAdvantageLayer.SourceColor(srcs[k].Source),
                            Plate = new Color(0, 0, 0, .7f), OnTop = true, Fallbacks = new[] { outward * 16, outward * 32, outward * 48 },
                        });
                    }
                }

                // the columns' heads: whose side each stands on in the nearest moment
                var cm = NearestMoment(s, Year, 1.5f);
                if (cm != null)
                    foreach (var (side, at) in columnHeads)
                        labels.Add(new HillLabel
                        {
                            Text = side < 0 ? "<b>" + (s.Get(cm.Holder)?.Name ?? cm.Holder) + "</b>" + (cm.Allies.Length > 0 ? " and allies" : "")
                                 : side > 0 ? "<b>Rivals</b>" : "Others",
                            // placed after the bodies' names, so a name in the column's last line keeps its place and the head steps down
                            At = at, Role = LabelRole.Name, Part = "history.walls", Rank = 290, OnTop = true, Fallbacks = BelowFallbacks,
                            Color = side < 0 ? TriadColor(cm.Triad) : side > 0 ? new Color(.95f, .62f, .6f) : new Color(.72f, .76f, .82f),
                        });

                var m = NearestMoment(s, Year, .75f);
                if (m != null && bodies.Count > 0)
                    labels.Add(new HillLabel
                    {
                        // placed after the bodies' names, so a name keeps the place over its body and the title steps up
                        Text = "<b>" + YearText(m.Year) + " · " + m.Title + "</b>", At = new Vector3((arenaMin.x + arenaMax.x) * .5f, arenaMax.y + 26, T(s, Year)),
                        Role = LabelRole.Name, Part = "history.walls", Rank = 295, Color = TriadColor(m.Triad), OnTop = true,
                        Fallbacks = Enumerable.Range(1, 6).Select(k => new Vector2(0, 18 * k)).ToArray(),    // the title may climb further
                    });
            }
        }

        static readonly Vector2[] AboveFallbacks = Steps(1, 0), BelowFallbacks = Steps(-1, 0);
        const float Stagger = 22;

        /// <summary>Places further out, 18 pixels at a time and four at most (two names' height), up (+1) or down (-1) from a
        /// first offset: a name that would wander onto another body is better left to its card.</summary>
        static Vector2[] Steps(int direction, float from) =>
            Enumerable.Range(1, 4).Select(k => new Vector2(0, direction * (from + 18 * k))).ToArray();

        /// <summary>A wall whose judgment Jev was unsure of: its confidence under 0.15 (its answer spread over the levels).</summary>
        public static bool Uncertain(History.MoatSource x) => !double.IsNaN(x.Confidence) && x.Confidence < .15;

        public static string SourceWord(string source) => source switch
        {
            "legitimacy" => "Legitimacy", "geography" => "Geography", "alliances" => "Allies", "capital" => "Capital", "supplyChain" => "Supply",
            _ => HillAdvantageLayer.SourceName(source),
        };

        public static string YearText(float year)
        {
            int y = Mathf.FloorToInt(year), month = Mathf.Clamp(Mathf.FloorToInt((year - y) * 12), 0, 11);
            if (year - y < .02f) return y.ToString(CultureInfo.InvariantCulture);
            return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month + 1) + " " + y;
        }

        /// <summary>The number as the fact gives it, in its own unit (never the size it is drawn by).</summary>
        public static string Raw(History.Value v)
        {
            // as many digits as the fact gives, so a small amount never shows as 0 and a precise one is not rounded away
            double x = double.IsNaN(v.Raw) ? v.V : v.Raw;
            if (Math.Abs(x - Math.Round(x)) < 1e-9) return x.ToString("#,0", CultureInfo.InvariantCulture);
            return Math.Abs(x) >= 1000 ? x.ToString("#,0.###", CultureInfo.InvariantCulture) : x.ToString("#,0.######", CultureInfo.InvariantCulture);
        }

        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // ------------------------------------------------------------------ smoke

        /// <summary><c>-smokeextra history</c>: each story compressed and opened at its turns, with checks that every story has
        /// lanes, moments and bodies.</summary>
        IEnumerator Smoke()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            root.Focus(View, 0f);
            for (float w = 0; w < 10 && History.Get() == null; w += Time.unscaledDeltaTime) yield return null;
            var h = History.Get();
            if (h == null) { Debug.LogError("[Why] history: no stories.json"); yield break; }
            foreach (var s in h.Stories)
            {
                var L = Lay(s);
                Debug.Log($"[Why] history {s.Id}: {s.Actors.Count} actors in {L.LaneY.Count} lanes ({s.Actors.Sum(a => a.Series.Count)} values), {s.Moments.Count} moments, {s.Periods.Count} periods, {s.Moats.Count} moats, {s.People.Count} people");
                if (s.Actors.Count == 0 || s.Moments.Count == 0) Debug.LogError($"[Why] history: story {s.Id} has no actors or moments");
            }

            foreach (string state in new[] { "history:ai:1971.9", "history:ai:1947.9:x", "history:ai:2017.5:x:sel=google", "history:ai:2025.8:x", "history:rev:1789.5",
                         "history:rev:1781.8:x", "history:rev:1805.9:x", "history:rev:1813.8:x" })
            {
                ShowState(state);
                yield return new WaitForSecondsRealtime(3.2f);
                Debug.Log($"[Why] history {state}: {bodies.Count} bodies, {labels.Count} labels");
                if (state.Contains(":x") && bodies.Count == 0) Debug.LogError($"[Why] history: {state} opened no bodies");
                yield return SmokeChecks.Capture("economy-" + state.Replace(':', '-').Replace('.', '_'), root.CurrentPreset);
            }

            ShowState(null);
        }
    }
}
