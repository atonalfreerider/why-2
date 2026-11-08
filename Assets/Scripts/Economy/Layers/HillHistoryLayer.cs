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
using Roles = Why.Economy.EconomyStyle.Roles;

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
    /// <item><b>Flows</b>: money paid and capital lent or invested from one actor to another (Britain's subsidies to the
    /// coalitions, the French King's loans to the Americans, the rounds into Intel and Anthropic), as packets that move on the
    /// flow clock while the year stands still: rungs between two lanes across the years a flow ran, rivers between two bodies
    /// at the year (<see cref="Flows"/>).</item>
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

        /// <summary>The meshes the view draws. The flows' bands and packets have no row of their own in the emphasis table:
        /// they draw with the part of what they join, the tubes' (lanes) compressed and the bodies' opened (<see cref="FlowId"/>),
        /// so only the first five have keys.</summary>
        enum Part { Tubes, Marks, Bodies, Walls, Outlines, Flows, Packets, Count }

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

        /// <summary>The flows' labels of the last build (<see cref="MakeLabels"/> adds them after the rest) and how many packet
        /// lanes it drew (the smoke sequence checks them).</summary>
        readonly List<HillLabel> flowLabels = new List<HillLabel>();
        int flowLanes;

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

            /// <summary>How far its advantage ring, walls and their names reach beyond the body (0 without a ring).</summary>
            public float Reach;
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
                ("Money paid", Roles.Encoded(Roles.Commerce)), ("Capital lent or invested", Roles.Encoded(Roles.Owners)),
            },
            "Each tube is one actor through time, as thick as the cube root of what it measured then (troops, people, ships, money, users); faint where nothing was measured. Sparks: the moments that turned an advantage. Opened at a year: each actor's measures as lobes, its advantage a ring walled by its sources (thicker where Jev judged the wall higher). Rungs between lanes, and rivers between bodies when opened: money paid (dots) and capital lent or invested (ingots) moving from payer to payee, as wide as the cube root of a year's amount (a total spread evenly over its years).");

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
            Flows(story, L);
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
                                                              Main = MainOf(it.a, Year, forcesStanding), LabelBelow = below, TopLine = top,
                                                              Reach = reach.TryGetValue(it.a.Id, out float ring) ? ring : 0 };
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
            // the flows: the rungs with the tubes, the rivers once the bodies stand at their places
            foreach (var p in new[] { Part.Flows, Part.Packets })
                if (partRenderer[(int)p]) partRenderer[(int)p].enabled = !Exploded || e > .95f;
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

        // ------------------------------------------------------------------ the flows

        /// <summary>Path units a second every packet of the view travels, on every rung and river: about 50 pixels a second in
        /// the view's poses (110 to 400 units away), so a rung between neighbouring lanes is crossed in under a second and a
        /// river across the cross-section in a few.</summary>
        const float PacketSpeed = 12;

        /// <summary>A flow over years draws a rung every this many years of its span, each near the middle of its share (a
        /// one-year flow draws one; <see cref="RungTimes"/>): a few rungs, not a fence.</summary>
        const float RungYears = 4;

        /// <summary>The flows whose amount is labelled: the largest this many by a year's amount (compressed: over the whole
        /// story, at the rung nearest the year; opened: of the year's rivers). The label system culls what does not fit.</summary>
        const int LabelledFlows = 5;

        /// <summary>The compressed view's flow labels rank below every other label of it, so the label system culls them first:
        /// below the periods (60) and below a moment as far from the year as the story allows (90 less its years, over 8).</summary>
        const float CompressedFlowRank = 5;

        /// <summary>A flow's band, under the tubes' rings; its packets, over every mark of the view (the bodies' occluders still
        /// hide what passes behind a body).</summary>
        const int FlowQueue = 3190, PacketQueue = 3196;

        /// <summary>A rung from outside the story rises half a lane's gap into its lane over this many units of time (one to
        /// outside as far out of it); a short river at a body reaches at least this far beyond the body's edge (past its
        /// advantage ring, where it has one).</summary>
        const float StubRun = 4, StubLength = 12;

        const int RungSegments = 8, RiverSegments = 16;

        /// <summary>
        /// A flow's width (world units) for a year's amount in the story's unit of size (a million pounds, a billion dollars):
        /// 0.8 times its cube root, as the tubes and lobes draw sizes, at most 6. The Church's lands (£125 million) draw 4,
        /// Britain's subsidies of 1815 (£5 million a year) 1.4, Reichenbach's to Prussia (£666,666) 0.7, Microsoft's billion
        /// into OpenAI 0.8, Busicom's $60,000 a hairline. A flow's amount is its width, never its packets' rate.
        /// </summary>
        static float FlowWidth(double perYear) => Mathf.Min(.8f * Mathf.Pow((float)perYear, 1f / 3), 6f);

        /// <summary>Packets a second for a year's amount: one at one unit of size and 0.35 more for each tenfold, from 0.5 to
        /// 2.5 (a lane at most four: faster ones strobe at 30 frames a second). The rate only follows the amount, gently; the
        /// width shows it.</summary>
        static float FlowRate(double perYear) => Mathf.Clamp(1 + .35f * Mathf.Log10((float)perYear), .5f, 2.5f);

        /// <summary>Whether a flow draws: its amount was measured (a share of a company's capital draws nothing).</summary>
        static bool Drawn(History.Flow f) => f.PerYear > 0 && !double.IsInfinity(f.PerYear);

        /// <summary>What a flow carries: capital lent, invested, bought or seized as ingots; money paid (subsidies, aid, grants,
        /// payments, purchases, indemnities) as dots.</summary>
        static PacketKind Carries(History.Flow f) =>
            f.Kind == "loan" || f.Kind == "investment" || f.Kind == "acquisition" || f.Kind == "confiscation" ? PacketKind.Capital : PacketKind.Money;

        /// <summary>A flow's palette role, as the advantage view's rivers take theirs: capital in the owners' gold, money paid in
        /// commerce's.</summary>
        static Color FlowRole(PacketKind carries) => carries == PacketKind.Capital ? Roles.Owners : Roles.Commerce;

        /// <summary>The highlight id the flows draw with, and their labels' emphasis key: the part of what they join, the
        /// lanes' (tubes) compressed and the bodies' opened, so the emphasis table needs no row of their own.</summary>
        static float FlowId() => EconomyEmphasis.Id(Keys[(int)(Exploded ? Part.Bodies : Part.Tubes)]);

        static string FlowPart() => Exploded ? "history.bodies" : "history.tubes";

        /// <summary>
        /// The flows (HISTORY.md section 5): money paid and capital lent or invested from one actor to another, as packets on
        /// the flow clock (<see cref="FlowClock"/>) that move while the year stands still, a tour holds or the play pauses.
        /// Compressed, rungs between the payer's lane and the payee's across the years each flow ran (<see cref="Rungs"/>);
        /// opened at a year, a river between the two bodies for each flow running then (<see cref="Rivers"/>). A flow's width is
        /// a year's amount (a total spread evenly over its years, <see cref="History.Flow.PerYear"/>) and its packets come at a
        /// rate that follows it gently, one speed for the view: money paid as dots in commerce's colour, capital as ingots in the
        /// owners' gold. An unmeasured amount draws nothing; a flow neither of whose ends is the selection dims with the rest.
        /// Built with the view (its story, year, selection or cross-section changing), never a frame at a time: the packets'
        /// motion is the shader's.
        /// </summary>
        void Flows(History.Story s, Layout L)
        {
            flowLabels.Clear();
            var bands = new LineMeshBuilder(1024);
            var packets = new PacketMeshBuilder(1024);
            if (Exploded) Rivers(s, L, bands, packets);
            else Rungs(s, L, bands, packets);
            flowLanes = packets.LaneCount;
            if (bands.VertexCount > 0) Lines(bands, Part.Flows, FlowQueue);
            else Drop(Part.Flows);
            UploadPackets(packets, Part.Packets, PacketQueue);
        }

        /// <summary>
        /// The compressed view's flows: for each, a rung every <see cref="RungYears"/> years of its span (outside its gaps),
        /// from the payer's lane to the payee's, bowed a little toward the future so it never reads as the year's cursor or a
        /// moment's straight tie. An end that had ended is its successor on the same lane (Britain after 1800); a rung a little
        /// past an end's lane stands at its end (Google's purchase of DNNresearch in March 2013, the AlexNet team's lane
        /// ending in December 2012). A flow from outside the story rises into the payee's lane from below and before, one to
        /// outside rises out of the payer's lane above and after, named by the research's words where the label system has room.
        /// </summary>
        void Rungs(History.Story s, Layout L, LineMeshBuilder bands, PacketMeshBuilder packets)
        {
            float id = FlowId();
            var amounts = s.Flows.Where(Drawn).OrderByDescending(f => f.PerYear).ThenBy(f => f.Id, StringComparer.Ordinal).Take(LabelledFlows).ToList();
            foreach (var f in s.Flows)
            {
                // a flow needs an end in the story to stand on (history.py never writes one with neither; a hand-edited file might)
                if (!Drawn(f) || (f.From == null && f.To == null)) continue;
                double perYear = f.PerYear;
                float width = FlowWidth(perYear), rate = FlowRate(perYear), seed = Organism.Hash01(f.Id);
                PacketKind carries = Carries(f);
                Color role = FlowRole(carries);
                // the label stands at the rung nearest the year
                float nearest = float.PositiveInfinity;
                Vector3 labelAt = default;
                int labelSide = 0;
                int k = 0;
                foreach (float t in RungTimes(f))
                {
                    string payer = f.From != null ? StandingFor(s, L, f.From, t) : null, payee = f.To != null ? StandingFor(s, L, f.To, t) : null;
                    if (!RungAt(L, payer, payee, t, out float at)) continue;
                    float z = T(s, at), ahead = Mathf.Sign(T(s, at + 1) - z);
                    Vector3[] path;
                    int side = 0;
                    if (payer != null && payee != null)
                    {
                        int la = L.LaneOf[payer], lb = L.LaneOf[payee];
                        if (la == lb) continue;    // one lane (an actor and its successor): nothing between them to draw
                        path = Bowed(new Vector3(LaneX, L.LaneY[la], z), new Vector3(LaneX, L.LaneY[lb], z), ahead);
                    }
                    else if (payee != null)
                    {
                        // from outside the story: it rises into the payee's lane from below and before
                        Vector3 into = new Vector3(LaneX, L.LaneY[L.LaneOf[payee]], z);
                        path = new[] { into + new Vector3(0, -LaneGap * .5f, -StubRun * ahead), into };
                        side = -1;
                    }
                    else
                    {
                        // to outside the story: it rises out of the payer's lane above and after
                        Vector3 from = new Vector3(LaneX, L.LaneY[L.LaneOf[payer]], z);
                        path = new[] { from, from + new Vector3(0, LaneGap * .5f, StubRun * ahead) };
                        side = 1;
                    }

                    bool lit = Selected == null || Selected == payer || Selected == payee || Selected == f.From || Selected == f.To;
                    FlowLane(bands, packets, path, carries, role, width, rate, (seed + k++ * .37f) % 1f, lit, id);
                    if (Mathf.Abs(t - Year) < nearest)
                    {
                        nearest = Mathf.Abs(t - Year);
                        labelSide = side;
                        labelAt = side < 0 ? path[0] : side > 0 ? path[path.Length - 1] : path[path.Length / 2];
                    }
                }

                if (float.IsPositiveInfinity(nearest)) continue;
                int rank = amounts.IndexOf(f);
                string name = labelSide < 0 ? Named("from", f.FromName) : labelSide > 0 ? Named("to", f.ToName) : null;
                // beside a rung, to its right (later); past a rise from outside, before it; past a rise to outside, after it
                FlowLabel(f, labelAt, name, rank >= 0, labelSide < 0 ? TMPro.TextAlignmentOptions.Right : TMPro.TextAlignmentOptions.Left,
                    new Vector2(labelSide < 0 ? -4 : labelSide > 0 ? 4 : 8, 0), rank >= 0 ? CompressedFlowRank - rank * .5f : CompressedFlowRank - 3);
            }
        }

        /// <summary>
        /// The cross-section's flows at the year: for each flow running then (outside its gaps), largest first, a river from the
        /// payer's body to the payee's, bowed in the cross-section's plane (a second river between the same two bodies bows
        /// the other way). An end with no body in the picture (outside the story, or not drawn this year) makes a short river
        /// from beside the body that has one, pointing away from the picture's middle and named; a flow neither of whose ends
        /// has a body draws nothing.
        /// </summary>
        void Rivers(History.Story s, Layout L, LineMeshBuilder bands, PacketMeshBuilder packets)
        {
            float id = FlowId();
            Vector3 middle = (arenaMin + arenaMax) * .5f;
            var between = new Dictionary<(string, string), int>();
            var beside = new Dictionary<string, int>();
            int order = 0;
            foreach (var f in s.Flows.Where(x => Drawn(x) && x.Runs(Year)).OrderByDescending(x => x.PerYear).ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                string payer = f.From != null ? StandingFor(s, L, f.From, Year) : null, payee = f.To != null ? StandingFor(s, L, f.To, Year) : null;
                Shown pa = BodyOf(payer, f.From), pb = BodyOf(payee, f.To);
                if ((pa == null && pb == null) || pa == pb) continue;
                double perYear = f.PerYear;
                float width = FlowWidth(perYear), rate = FlowRate(perYear);
                PacketKind carries = Carries(f);
                Color role = FlowRole(carries);
                Vector3[] path;
                Vector3 labelAt;
                string name = null;
                var align = TMPro.TextAlignmentOptions.Center;
                if (pa != null && pb != null)
                {
                    // the pair in one order, so the rivers between two bodies alternate sides whichever way each runs
                    bool forward = string.CompareOrdinal(payer, payee) < 0;
                    var key = forward ? (payer, payee) : (payee, payer);
                    between.TryGetValue(key, out int n);
                    between[key] = n + 1;
                    path = RiverPath(pa, pb, forward, n);
                    if (path == null) continue;    // the bodies touch
                    // the amount stands off the river's middle on the outside of its bend, clear of its packets
                    Vector3 mid = path[path.Length / 2], bend = mid - (path[0] + path[path.Length - 1]) * .5f;
                    labelAt = mid + (bend.sqrMagnitude > 1e-4f ? bend.normalized : Vector3.up) * (width * .5f + 3);
                }
                else
                {
                    Shown at = pa ?? pb;
                    string atId = pa != null ? payer : payee;
                    beside.TryGetValue(atId, out int n);
                    beside[atId] = n + 1;
                    // it reaches past the body's advantage ring and its walls' names, so its own name stands clear of them
                    Vector3 o = Outward(at.To, middle, n);
                    Vector3 edge = at.To + o * (at.Shape.MaxRadius + 1), far = at.To + o * (at.Shape.MaxRadius + Mathf.Max(StubLength, at.Reach + 4));
                    path = pa != null ? new[] { edge, far } : new[] { far, edge };
                    labelAt = far;
                    name = pa != null ? Named("to", payee != null ? s.Get(payee)?.Name ?? payee : f.ToName)
                        : Named("from", payer != null ? s.Get(payer)?.Name ?? payer : f.FromName);
                    // from the future +x is screen-left: a name past a short river to the left ends at it, one to the right begins there
                    align = Mathf.Abs(o.x) < .35f ? TMPro.TextAlignmentOptions.Center : o.x > 0 ? TMPro.TextAlignmentOptions.Right : TMPro.TextAlignmentOptions.Left;
                }

                bool lit = Selected == null || Selected == payer || Selected == payee || Selected == f.From || Selected == f.To;
                FlowLane(bands, packets, path, carries, role, width, rate, Organism.Hash01(f.Id), lit, id);
                bool amount = order < LabelledFlows;
                FlowLabel(f, labelAt, name, amount, align, Vector2.zero, (amount ? 200 : 180) - order);
                order++;
            }
        }

        /// <summary>The body standing for an end in the cross-section: its own, or (an actor whose lane has just ended still
        /// standing for its moment) its predecessor's; null when neither is drawn.</summary>
        Shown BodyOf(string standing, string original) =>
            standing != null && bodies.TryGetValue(standing, out var b) ? b : original != null && bodies.TryGetValue(original, out var o) ? o : null;

        /// <summary>One rung's or river's band (a hairline at least, as wide as the year's amount) and its packets, which leave
        /// the path's first point for its last.</summary>
        static void FlowLane(LineMeshBuilder bands, PacketMeshBuilder packets, Vector3[] path, PacketKind carries, Color role, float width, float rate,
            float seed, bool lit, float id)
        {
            bands.AddPolyline(path, WithAlpha(role, lit ? .3f : .1f), .8f, width, id);
            packets.AddLane(path, carries, WithAlpha(role, lit ? .92f : .35f), rate, PacketSpeed, lit ? 3.5f : 2.5f, id, seed: seed,
                sizeWorld: Mathf.Min(width * .85f, 2.4f));
        }

        /// <summary>When a flow's rungs stand: its years outside its gaps, in runs of consecutive years, each run cut into shares
        /// of at most <see cref="RungYears"/> years with a rung near each share's middle (one year: near its middle), moved
        /// within the share by up to a quarter of it on the flow's own hash, so two flows of one year from one lane (Britain's
        /// two subsidies of Reichenbach, 1813) stand apart instead of on each other.</summary>
        static IEnumerable<float> RungTimes(History.Flow f)
        {
            float shift = (Organism.Hash01(f.Id + "|rung") - .5f) * .5f;
            for (int y = f.FromYear; y <= f.ToYear; y++)
            {
                if (Array.IndexOf(f.Gaps, y) >= 0) continue;
                int end = y;
                while (end + 1 <= f.ToYear && Array.IndexOf(f.Gaps, end + 1) < 0) end++;
                int years = end - y + 1, n = Mathf.Max(1, Mathf.CeilToInt(years / RungYears));
                for (int k = 0; k < n; k++) yield return y + (k + .5f + shift) * years / n;
                y = end;
            }
        }

        /// <summary>The actor an id stands for at a time: itself, or its successor once its lane has ended (Britain's subsidies
        /// after 1800 are the United Kingdom's, on the same lane and, opened, from its body).</summary>
        static string StandingFor(History.Story s, Layout L, string id, float year)
        {
            for (int i = 0; i < 8; i++)
            {
                if (!L.Span.TryGetValue(id, out var span) || year < span.to) return id;
                string next = s.Get(id)?.Successor;
                if (next == null || !L.Span.ContainsKey(next)) return id;
                id = next;
            }

            return id;
        }

        /// <summary>Where a rung meant for time <paramref name="t"/> stands: at t when both ends' lanes run then; else at the
        /// nearest time both run, when that is within a year (a flow just past an actor's lane is drawn at its end); else
        /// nowhere.</summary>
        static bool RungAt(Layout L, string payer, string payee, float t, out float at)
        {
            float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
            at = t;
            foreach (string id in new[] { payer, payee })
            {
                if (id == null) continue;
                if (!L.Span.TryGetValue(id, out var span)) return false;
                lo = Mathf.Max(lo, span.from);
                hi = Mathf.Min(hi, span.to);
            }

            if (lo > hi) return false;
            float margin = Mathf.Min(.02f, (hi - lo) * .5f);
            at = Mathf.Clamp(t, lo + margin, hi - margin);
            return Mathf.Abs(at - t) <= 1;
        }

        /// <summary>A rung from one lane to another at one time, bowed toward the future (<paramref name="ahead"/>: the sign of
        /// later times along z) by a twelfth of its height, at most 3 units.</summary>
        static Vector3[] Bowed(Vector3 a, Vector3 b, float ahead)
        {
            Vector3 p1 = (a + b) * .5f + Vector3.forward * (ahead * Mathf.Min(3, Mathf.Abs(b.y - a.y) / 12));
            var path = new Vector3[RungSegments + 1];
            for (int i = 0; i <= RungSegments; i++)
            {
                float t = i / (float)RungSegments;
                path[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * p1 + t * t * b;
            }

            return path;
        }

        /// <summary>A river in the cross-section's plane: a curve from the payer's edge to the payee's, bowed to one side by an
        /// eighth of its length (the n-th between the same two bodies further, on alternate sides); null when the bodies touch.</summary>
        static Vector3[] RiverPath(Shown a, Shown b, bool forward, int n)
        {
            Vector3 d = b.To - a.To;
            d.z = 0;
            float dist = d.magnitude;
            if (dist < 1e-3f) return null;
            Vector3 dir = d / dist, p0 = a.To + dir * a.Shape.MaxRadius, p2 = b.To - dir * b.Shape.MaxRadius;
            float length = Vector3.Dot(p2 - p0, dir);
            if (length < 2) return null;
            Vector3 canon = forward ? dir : -dir, side = new Vector3(-canon.y, canon.x, 0);
            Vector3 p1 = (p0 + p2) * .5f + side * (length * .12f * (1 + n / 2) * (n % 2 == 0 ? 1 : -1));
            var path = new Vector3[RiverSegments + 1];
            for (int i = 0; i <= RiverSegments; i++)
            {
                float t = i / (float)RiverSegments;
                path[i] = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
            }

            return path;
        }

        /// <summary>The way a short river leaves a body in the cross-section's plane: away from the picture's middle (up for a
        /// body at it); a second and a third at the same body turn 35 degrees either way.</summary>
        static Vector3 Outward(Vector3 at, Vector3 middle, int n)
        {
            Vector3 o = at - middle;
            o.z = 0;
            o = o.sqrMagnitude < 1e-4f ? Vector3.up : o.normalized;
            return Quaternion.AngleAxis((n + 1) / 2 * 35f * (n % 2 == 1 ? 1 : -1), Vector3.forward) * o;
        }

        /// <summary>The name of an end outside the picture, as a label gives it ("from Amazon", "to 17 European countries");
        /// null when the source names no one.</summary>
        static string Named(string way, string name) => string.IsNullOrEmpty(name) ? null : way + " " + name;

        /// <summary>A flow as its label gives it: what moved, the fact's own number and unit, "in total" for a total over years or
        /// "a year" for a rate, and its years: "Loan · 18 million livres in total, 1778-1782", "Subsidy · £5,000,000 a year,
        /// 1814".</summary>
        public static string FlowText(History.Flow f)
        {
            string unit = f.Unit ?? "", number = Number(f.Value);
            string amount = unit == "£" || unit == "$" ? unit + number : number + (unit.Length > 0 ? " " + unit : "");
            string how = !f.Total ? " a year" : f.Years > 1 ? " in total" : "";
            string years = f.FromYear.ToString(CultureInfo.InvariantCulture) + (f.ToYear != f.FromYear ? "-" + f.ToYear.ToString(CultureInfo.InvariantCulture) : "");
            string word = string.IsNullOrEmpty(f.Kind) ? "" : char.ToUpperInvariant(f.Kind[0]) + f.Kind.Substring(1) + " · ";
            return word + amount + how + ", " + years;
        }

        /// <summary>A flow's label, when it has something to say: the name of an end outside the picture and, for the
        /// <see cref="LabelledFlows"/> largest, <see cref="FlowText"/>; in the flow's colour on the ink's backing, after every
        /// other label of the view (lower ranks), so the label system culls these first.</summary>
        void FlowLabel(History.Flow f, Vector3 at, string name, bool amount, TMPro.TextAlignmentOptions align, Vector2 offset, float rank)
        {
            string text = (name != null ? "<b>" + (name.Length > 34 ? Wrap(name, 32) : name) + "</b>" : "") +
                          (amount ? (name != null ? "\n" : "") + FlowText(f) : "");
            if (text.Length == 0) return;
            flowLabels.Add(new HillLabel
            {
                Text = text, At = at, Role = LabelRole.Note, Part = FlowPart(), Rank = rank, Color = Roles.Encoded(FlowRole(Carries(f))),
                Plate = Roles.Encoded(Roles.InkBacking), OnTop = true, Align = align, PixelOffset = offset,
            });
        }

        /// <summary>Uploads a part's packets (Why/Packets, placed in the view's own space), replacing its last mesh and
        /// material; none when no lane moves.</summary>
        void UploadPackets(PacketMeshBuilder p, Part part, int queue)
        {
            Drop(part);
            if (p.VertexCount == 0) return;
            var mesh = p.ToMesh("History " + part);
            owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, queue));
            owned.Add(m);
            var r = AddMesh("History " + part, mesh, m);
            r.transform.SetParent(content.transform, false);
            partRenderer[(int)part] = r;
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

            // the flows' amounts and the names of their ends outside the picture, after everything else (lower ranks)
            labels.AddRange(flowLabels);
        }

        static readonly Vector2[] AboveFallbacks = Steps(1, 0), BelowFallbacks = Steps(-1, 0);
        const float Stagger = 22;

        /// <summary>Places further out, 18 pixels at a time and four at most (two names' height), up (+1) or down (-1) from a
        /// first offset: a name that would wander onto another body is better left to its card.</summary>
        static Vector2[] Steps(int direction, float from) =>
            Enumerable.Range(1, 4).Select(k => new Vector2(0, direction * (from + 18 * k))).ToArray();

        /// <summary>A wall whose judgment Jev was unsure of: its confidence under 0.15 (its answer spread over the levels).</summary>
        public static bool Uncertain(History.MoatSource x) => !double.IsNaN(x.Confidence) && x.Confidence < .15;

        /// <summary>A source's name on a wall: history's own words for the sources a state's or a company's advantage drew on,
        /// else the advantage view's name (which also names the standing army's four: command of the sea, nuclear weapons,
        /// alliances and military spending).</summary>
        public static string SourceWord(string source) => source switch
        {
            "legitimacy" => "Legitimacy", "geography" => "Geography", "capital" => "Capital", "supplyChain" => "Supply",
            _ => HillAdvantageLayer.SourceName(source),
        };

        public static string YearText(float year)
        {
            int y = Mathf.FloorToInt(year), month = Mathf.Clamp(Mathf.FloorToInt((year - y) * 12), 0, 11);
            if (year - y < .02f) return y.ToString(CultureInfo.InvariantCulture);
            return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month + 1) + " " + y;
        }

        /// <summary>The number as the fact gives it, in its own unit (never the size it is drawn by).</summary>
        public static string Raw(History.Value v) => Number(double.IsNaN(v.Raw) ? v.V : v.Raw);

        /// <summary>A number as a fact gives it: as many digits as it has, so a small amount never shows as 0 and a precise one
        /// is not rounded away.</summary>
        static string Number(double x)
        {
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
        /// lanes, moments and bodies, and that a story's flows draw their rungs compressed (the view must never stand still).
        /// Two states are there for the flows: 2019 opened (Microsoft's billion into OpenAI, a river between two bodies) and
        /// 1795 opened (Britain's loan to Austria and its subsidies to the coalitions, short rivers from Britain's body, the
        /// other ends not drawn that year).</summary>
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
                Debug.Log($"[Why] history {s.Id}: {s.Actors.Count} actors in {L.LaneY.Count} lanes ({s.Actors.Sum(a => a.Series.Count)} values), {s.Moments.Count} moments, {s.Periods.Count} periods, {s.Moats.Count} moats, {s.People.Count} people, {s.Flows.Count} flows ({s.Flows.Count(Drawn)} with an amount)");
                if (s.Actors.Count == 0 || s.Moments.Count == 0) Debug.LogError($"[Why] history: story {s.Id} has no actors or moments");
            }

            foreach (string state in new[] { "history:ai:1971.9", "history:ai:1947.9:x", "history:ai:2017.5:x:sel=google", "history:ai:2019.5:x", "history:ai:2025.8:x",
                         "history:rev:1789.5", "history:rev:1781.8:x", "history:rev:1795.5:x", "history:rev:1805.9:x", "history:rev:1813.8:x" })
            {
                ShowState(state);
                yield return new WaitForSecondsRealtime(3.2f);
                Debug.Log($"[Why] history {state}: {bodies.Count} bodies, {labels.Count} labels, {flowLanes} flow lanes ({flowLabels.Count} labelled)");
                if (state.Contains(":x") && bodies.Count == 0) Debug.LogError($"[Why] history: {state} opened no bodies");
                // compressed, every flow with an amount draws its rungs, so the lanes never stand still
                var shown = h.StoryById(StoryId);
                if (!state.Contains(":x") && shown != null && shown.Flows.Any(Drawn) && flowLanes == 0)
                    Debug.LogError($"[Why] history: {state} drew none of its story's {shown.Flows.Count(Drawn)} flows");
                yield return SmokeChecks.Capture("economy-" + state.Replace(':', '-').Replace('.', '_'), root.CurrentPreset);
            }

            ShowState(null);
        }
    }
}
