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

        const float RowGap = 18, BodyGap = 9, SideGap = 22;

        /// <summary>The share of the screen's width left of the panel or a story's card (430 of 1920 pixels and margins).</summary>
        const float FreeWidth = .74f;

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
            public bool Measured;
            public int Side;
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

        static float TubeR(double size) => double.IsNaN(size) ? .45f : Mathf.Clamp(.36f * Mathf.Pow((float)size, 1f / 3), .45f, 3.4f);

        static float LobeR(double size) => Mathf.Clamp(2.2f * Mathf.Pow((float)Math.Max(0, size), 1f / 3), 1.6f, 26f);

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
            "army" or "troops" => new Color(.95f, .46f, .34f), "navy" or "ships" => new Color(.32f, .58f, .98f),
            "revenue" or "gdp" => new Color(.55f, .85f, .55f), "debt" => new Color(.86f, .30f, .26f), "users" => new Color(.98f, .60f, .85f),
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
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => "history." + p), new[] { "activity.markers" },
                new[] { "wave.crest", "wave.face", "wave.ribs", "wave.ridges" }, synthesis: true);
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
            return s.People.Where(p => p.Actors.Any(ids.Contains)).Take(3).Select(p => p.Id).ToArray();
        }

        public static History.Moment NearestMoment(History.Story s, float year, float within) =>
            s?.Moments.Where(m => !float.IsNaN(m.Year) && Mathf.Abs(m.Year - year) <= within).OrderBy(m => Mathf.Abs(m.Year - year)).FirstOrDefault();

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
        /// there, faint where nothing was. Opened at a year, only the past stays, fading with distance, so the cross-section
        /// stands at the front of its own history.</summary>
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
                float to = Exploded ? Mathf.Min(span.to, Year) : span.to;
                for (float t = span.from; t <= to + .001f; t += step)
                {
                    string part = a.MainPartAt(t);
                    double size = part != null ? a.SizeAt(part, t) : double.NaN;
                    float r = TubeR(size), z = T(s, t);
                    bool measured = !double.IsNaN(size);
                    int n = Mathf.Clamp(Mathf.RoundToInt(r * 3.6f), 5, 16);
                    float alpha = (measured ? .82f : .26f) * (lit ? 1 : .3f);
                    // opened: the last dozen years trail behind the cross-section, faint, so the bodies are the subject
                    if (Exploded) alpha *= .3f * Mathf.Clamp01(1 - (Year - t) / 12f) * Mathf.Clamp01((Year - t) / 1.2f);
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
                if (Exploded && sp.from > Year) continue;
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
        static List<ContestNetwork.Part> PartsAt(History.Actor a, float year, out bool measured)
        {
            var parts = new List<ContestNetwork.Part>();
            string main = a.MainPartAt(year);
            measured = main != null;
            Color kind = KindColor(a.Kind);
            if (main == null)
            {
                parts.Add(new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, 2.2f, Vol(2.2f), new Color(.55f, .58f, .64f), .3f));
                return parts;
            }

            float coreR = LobeR(a.SizeAt(main, year));
            parts.Add(new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, coreR, Vol(coreR), Color.Lerp(kind, PartColor(main), .25f), .7f));
            foreach (string part in a.Series.Select(v => v.Part).Distinct().Where(p => p != main && Lobed(p)).Take(5))
            {
                double size = a.SizeAt(part, year);
                if (double.IsNaN(size) || size <= 0) continue;
                bool below = part == "debt" || part == "casualties" || part == "executions";
                float r = LobeR(size);
                parts.Add(new ContestNetwork.Part(below ? ContestNetwork.PartKind.Debt : ContestNetwork.PartKind.Built, Vector3.zero, r, Vol(r), PartColor(part), below ? .9f : .8f));
            }

            return parts;
        }

        static double Vol(float r) => Math.Pow(r / ContestNetwork.KR, 3);

        /// <summary>Whether a measure is a part of the body (what the actor holds, fields or owes: people, troops, ships,
        /// money, members, users), not a property of its product (a chip's transistors, a model's parameters) or a count of
        /// the few who made it, which the label names instead.</summary>
        static bool Lobed(string part) => part switch
        {
            "transistors" or "parameters" or "people" or "defections" or "gdpShare" or "share" => false,
            _ => true,
        };

        /// <summary>The bodies of the year, built once per story, year and selection: the actors measured then, and the
        /// moment's participants that were not (small and grey); arranged in rows of polities, forces and enterprises, the
        /// moment's holder and allies on the left, its rivals on the right, around the tubes' cross-section.</summary>
        void Bodies(History.Story s, Layout L)
        {
            DropBodies();
            if (occluderMaterial == null) { occluderMaterial = PointBodies.OccluderMaterial(); owned.Add(occluderMaterial); }
            var moment = NearestMoment(s, Year, 3);
            var side = new Dictionary<string, int>();
            if (moment != null)
            {
                if (moment.Holder != null) side[moment.Holder] = -1;
                foreach (var x in moment.Allies) side[x] = -1;
                foreach (var x in moment.Rivals) side[x] = 1;
            }

            var near = NearestMoment(s, Year, 1.5f);
            var participants = near == null ? new HashSet<string>() : new HashSet<string>(new[] { near.Holder }.Concat(near.Allies).Concat(near.Rivals).Where(x => x != null));
            var items = new List<(History.Actor a, ContestNetwork.Body body, Organism shape, bool measured, int row, int side)>();
            foreach (var a in s.Actors)
            {
                if (!L.Span.TryGetValue(a.Id, out var span)) continue;
                bool alive = Year >= span.from - .01f && Year <= span.to + .01f;
                var parts = PartsAt(a, Year, out bool measured);
                if (!measured && !(participants.Contains(a.Id) && (alive || Mathf.Abs(Year - span.from) < 1.5f))) continue;
                if (measured && !alive) continue;
                var body = new ContestNetwork.Body { Id = a.Id, Name = a.Name, Kind = ContestNetwork.Kind.Company, CustomParts = parts, Alive = true, Value = double.NaN };
                var shape = new Organism(body, null, measured ? 3 : 2);
                string g = GroupOf(a.Kind);
                int row = g == "polities" || g == "alliances" || g == "peoples" ? 0 : g == "forces" ? 1 : 2;
                items.Add((a, body, shape, measured, row, side.TryGetValue(a.Id, out int sd) ? sd : 0));
            }

            // rows top to bottom, each centred on the lanes' x; from the future +x is screen-left, so the left side gets the high x
            var rows = items.GroupBy(x => x.row).OrderBy(g => g.Key).Select(g => g.OrderBy(x => x.side).ThenByDescending(x => x.shape.MaxRadius).ToList()).ToList();
            var heights = rows.Select(r => r.Max(x => x.shape.MaxRadius)).ToList();
            float total = heights.Sum(h => 2 * h) + RowGap * Mathf.Max(0, rows.Count - 1);
            float mid = (L.Top + L.Bottom) * .5f, yTop = mid + total * .5f, z = T(s, Year);
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                float yRow = yTop - heights[r];
                yTop -= 2 * heights[r] + RowGap;
                float width = row.Sum(x => 2 * x.shape.MaxRadius + BodyGap) - BodyGap + SideGap * (row.Select(x => x.side).Distinct().Count() - 1);
                float x0 = LaneX + width * .5f;
                int lastSide = row[0].side;
                foreach (var it in row)
                {
                    if (it.side != lastSide) { x0 -= SideGap; lastSide = it.side; }
                    float rad = it.shape.MaxRadius;
                    var to = new Vector3(x0 - rad, yRow, z);
                    x0 -= 2 * rad + BodyGap;
                    int lane = L.LaneOf.TryGetValue(it.a.Id, out int ln) ? ln : 0;
                    var from = new Vector3(LaneX, L.LaneY.Count > 0 ? L.LaneY[Mathf.Clamp(lane, 0, L.LaneY.Count - 1)] : LaneBase, z);
                    it.body.BodyR = rad;
                    var (mr, or) = BuildBody(it.a, it.shape, it.measured);
                    bodies[it.a.Id] = new Shown { Body = mr, Occluder = or, Shape = it.shape, From = from, To = to, Measured = it.measured, Side = it.side };
                }
            }

            arenaMin = new Vector3(LaneX, mid, z);
            arenaMax = arenaMin;
            foreach (var v in bodies.Values)
            {
                arenaMin = Vector3.Min(arenaMin, v.To - Vector3.one * v.Shape.MaxRadius);
                arenaMax = Vector3.Max(arenaMax, v.To + Vector3.one * v.Shape.MaxRadius);
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
                    bool judged = !double.IsNaN(w);
                    float thick = judged ? 1.2f + 2.4f * (float)w : 1.2f;
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

        IEnumerable<History.Moat> ActiveMoats(History.Story s) =>
            s.Moats.Where(m => Year >= m.From - .01f && Year <= (Mathf.Approximately(m.To, Mathf.Round(m.To)) ? m.To + 1 : m.To) + .01f);

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
            var cam = root.Rig.Cam;
            float tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad), tanH = tanV * Mathf.Max(1, cam.aspect);
            // the panel (or a story's card) takes the right quarter of the screen: the picture fits the rest, its middle
            // left of the screen's by half the panel's share
            float shift = (1 - FreeWidth) * .5f;
            ViewPreset p;
            if (Exploded && bodies.Count > 0)
            {
                // down the time axis from the future (+x runs to the left), the cross-section filling the free screen
                Vector3 c = (arenaMin + arenaMax) * .5f;
                float w = arenaMax.x - arenaMin.x + 26, h = arenaMax.y - arenaMin.y + 40;
                float d = Mathf.Max(w * .5f / (tanH * FreeWidth * .94f), h * .5f / (tanV * .8f), 110);
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

        void ShowPopups()
        {
            var story = (model ?? History.Get())?.StoryById(StoryId);
            if (story == null || LandView.PresetId != View) { HistoryPopups.Clear(); return; }
            var items = new List<(string, string, string)>();
            foreach (string id in popups)
            {
                var person = story.PersonById(id);
                if (person != null) { items.Add((person.Image ?? person.Id, person.Name, person.Role)); continue; }
                var actor = story.Get(id);
                if (actor != null) items.Add((actor.Image ?? actor.Id, actor.Name, actor.Role));
            }

            HistoryPopups.Show(items);
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
                foreach (var kv in bodies)
                {
                    var a = s.Get(kv.Key);
                    if (a == null) continue;
                    string main = a.MainPartAt(Year);
                    var v = main != null ? a.Shown(main, Year) : null;
                    string measure = v != null ? "\n" + Raw(v) + " " + v.Unit + (Mathf.FloorToInt(v.Year) != Mathf.FloorToInt(Year) ? " (" + Mathf.FloorToInt(v.Year) + ")" : "") : "\n<i>not measured</i>";
                    labels.Add(new HillLabel
                    {
                        Text = "<b>" + a.Name + "</b> <size=80%>" + KindWord(a.Kind) + "</size><size=86%>" + measure + "</size>", At = kv.Value.To + Vector3.up * (kv.Value.Shape.MaxRadius + 2.5f),
                        Role = LabelRole.Name, Part = "history.bodies", Rank = 300 + kv.Value.Shape.MaxRadius + (Selected == a.Id ? 400 : 0), Plate = new Color(0, 0, 0, .78f),
                        Color = KindColor(a.Kind), OnTop = true,
                    });
                }

                foreach (var moat in ActiveMoats(s))
                {
                    if (!bodies.TryGetValue(moat.Actor, out var hb)) continue;
                    float R = hb.Shape.MaxRadius + 4;
                    var srcs = Sources(moat);
                    for (int k = 0; k < srcs.Count; k++)
                    {
                        float a = (k + .5f) * 2 * Mathf.PI / srcs.Count;
                        double w = srcs[k].Wall;
                        float thick = double.IsNaN(w) ? 1.2f : 1.2f + 2.4f * (float)w;
                        Vector3 at = hb.To + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (R + thick + 4);
                        labels.Add(new HillLabel
                        {
                            Text = "<b>" + SourceWord(srcs[k].Source) + "</b>" + (double.IsNaN(w) ? "" : " <size=80%>wall " + w.ToString("0.0", CultureInfo.InvariantCulture) + " of 4</size>"),
                            At = at, Role = LabelRole.Note, Part = "history.walls", Rank = 250 + (float)(double.IsNaN(w) ? 0 : w), Color = HillAdvantageLayer.SourceColor(srcs[k].Source),
                            Plate = new Color(0, 0, 0, .7f), OnTop = true,
                        });
                    }
                }

                var m = NearestMoment(s, Year, .75f);
                if (m != null && bodies.Count > 0)
                    labels.Add(new HillLabel
                    {
                        Text = "<b>" + YearText(m.Year) + " · " + m.Title + "</b>", At = new Vector3((arenaMin.x + arenaMax.x) * .5f, arenaMax.y + 9, T(s, Year)),
                        Role = LabelRole.Name, Part = "history.walls", Rank = 600, Color = TriadColor(m.Triad), OnTop = true,
                    });
            }
        }

        static string SourceWord(string source) => source switch
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
            double x = double.IsNaN(v.Raw) ? v.V : v.Raw;
            return Math.Abs(x - Math.Round(x)) < 1e-6 ? x.ToString("#,0", CultureInfo.InvariantCulture) : x.ToString("#,0.##", CultureInfo.InvariantCulture);
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
