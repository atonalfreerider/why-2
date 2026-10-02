using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Land
{
    /// <summary>
    /// The land's snapshots (8.3): builds a year's <see cref="LandSnapshot"/> (layout, players, money, season, circuit)
    /// on a worker or blocking, keeps the last <see cref="LandStyle.CacheYears"/> in an LRU cache, publishes the one on
    /// screen (<see cref="Current"/>, <see cref="Changed"/>) and tells when every land layer has swapped to it
    /// (<see cref="ShownYear"/>). A year set by a preset builds blocking (deterministic presets and harness renders); key,
    /// chip and click changes build asynchronously and coalesce (only the latest requested year is published; a stale
    /// build is discarded). <see cref="Init"/> and <see cref="BuildBlocking"/> are safe on any thread; the rest is the
    /// main thread's API (the land's view layer calls <see cref="Tick"/> every frame).
    /// </summary>
    public static class LandService
    {
        /// <summary>Shared key of the first snapshot (LandModelLayer, tier 4), read in Prepare by the tier-5 land layers.</summary>
        public const string SharedKey = "economy.land";

        static readonly object Gate = new object();
        static readonly List<LandSnapshot> cache = new List<LandSnapshot>(LandStyle.CacheYears);   // most recent last
        static readonly HashSet<string> layers = new HashSet<string>(StringComparer.Ordinal);
        static readonly HashSet<string> ready = new HashSet<string>(StringComparer.Ordinal);
        static EconomyModel model;
        static SmvPopulation pop;

        // asynchronous builds (main thread state; the task only returns its snapshot)
        static Task<LandSnapshot> yearTask, societyTask;

        /// <summary>The betrayal season being built on a worker, for the snapshot version <see cref="betrayalFor"/>; the
        /// main thread stores the finished result (<see cref="Tick"/>), so no worker writes a published snapshot.</summary>
        static Task<SocialSeasonResult> betrayalTask;
        static int wantedYear = -1;
        static SocialSettings wantedSettings;   // written under Gate (main thread, Init); other threads read it under Gate
        static bool societyPending;
        static int betrayalFor = -1;

        /// <summary>The snapshot on screen.</summary>
        public static LandSnapshot Current { get; private set; }

        /// <summary>The year every land layer shows (changes once all registered layers reported the current version).</summary>
        public static int ShownYear { get; private set; }

        /// <summary>Incremented by every publication.</summary>
        public static int Version { get; private set; }

        /// <summary>A new snapshot is on screen (main thread): land layers rebuild from it.</summary>
        public static event Action<LandSnapshot> Changed;

        /// <summary>The model the snapshots are built from (null before <see cref="Init"/>).</summary>
        public static EconomyModel Model => model;

        /// <summary>The population the players are built from (null before <see cref="Init"/>).</summary>
        public static SmvPopulation Population => pop;

        /// <summary>
        /// Starts a scene load: forgets every snapshot and layer, takes the model and population, and puts the first
        /// snapshot on screen (built by the land's model layer). Any thread, before the land layers prepare.
        /// </summary>
        public static void Init(EconomyModel economy, SmvPopulation population, LandSnapshot first)
        {
            lock (Gate)
            {
                model = economy;
                pop = population;
                cache.Clear();
                layers.Clear();
                ready.Clear();
                yearTask = societyTask = null;
                betrayalTask = null;
                wantedYear = -1;
                societyPending = false;
                betrayalFor = -1;
                Version = 1;
                if (first != null)
                {
                    first.Version = Version;
                    cache.Add(first);
                }

                Current = first;
                ShownYear = first?.Year ?? 0;
                wantedSettings = first?.Society?.Settings ?? SocialSettings.Default;
            }
        }

        // ------------------------------------------------------------------ building (pure, any thread)

        /// <summary>
        /// Builds a year's snapshot with the given season settings (no cache): LandLayout, PlayerCensus, MoneyRouting,
        /// SocialSeason, MoneyCircuit; the checks of every builder on one line. Pure and deterministic.
        /// </summary>
        public static LandSnapshot Build(EconomyModel economy, SmvPopulation population, int year, SocialSettings settings)
        {
            LandGeometry land = LandLayout.Build(economy.Data, year);
            PlayerSet players = PlayerCensus.Build(economy, population, land, year);
            MoneyFlows money = MoneyRouting.Build(economy, land, players, year);
            SocialSeasonResult society = SocialSeason.Run(economy.Data, land, players, settings, year);
            LandSnapshot s = new LandSnapshot
            {
                Year = year, Land = land, Players = players, Money = money, Society = society,
                Circuit = economy.Circuit.Build(year)
            };
            s.ChecksLine = ChecksLine(s, economy.Data);
            return s;
        }

        /// <summary>
        /// "layout 2/2, roots 1/1, players 3/3, money 8/8, society 2/2 PASS": every builder's "checks n/m" from its log
        /// (the roots' from <see cref="LandLayout.RootsLine"/>), PASS when all pass.
        /// </summary>
        public static string ChecksLine(LandSnapshot s, Data.EconomyData data)
        {
            (string name, string log)[] parts =
            {
                ("layout", s.Land?.Log), ("roots", s.Land != null && data != null ? LandLayout.RootsLine(data, s.Land) : null),
                ("players", s.Players?.Log), ("money", s.Money?.Log), ("society", s.Society?.Log)
            };
            StringBuilder b = new StringBuilder();
            bool all = true;
            foreach ((string name, string log) in parts)
            {
                if (b.Length > 0) b.Append(", ");
                b.Append(name).Append(' ');
                if (!TryChecks(log, out int pass, out int count))
                {
                    b.Append("?");
                    all = false;
                    continue;
                }

                b.Append(pass).Append('/').Append(count);
                all &= pass == count;
            }

            return b.Append(all ? " PASS" : " FAIL").ToString();
        }

        /// <summary>Reads "checks n/m" from a builder's log line.</summary>
        public static bool TryChecks(string log, out int pass, out int count)
        {
            pass = count = 0;
            if (string.IsNullOrEmpty(log)) return false;
            int i = log.LastIndexOf("checks ", StringComparison.Ordinal);
            if (i < 0) return false;
            int slash = log.IndexOf('/', i);
            if (slash < 0) return false;
            int end = slash + 1;
            while (end < log.Length && char.IsDigit(log[end])) end++;
            return int.TryParse(log.Substring(i + 7, slash - i - 7), out pass) &&
                   int.TryParse(log.Substring(slash + 1, end - slash - 1), out count);
        }

        /// <summary>A year's snapshot with the current season settings: from the cache at once, else built here (any thread).</summary>
        public static LandSnapshot BuildBlocking(int year)
        {
            SocialSettings settings;
            lock (Gate) settings = wantedSettings;
            return BuildBlocking(year, settings);
        }

        static LandSnapshot BuildBlocking(int year, SocialSettings settings)
        {
            EconomyModel economy;
            SmvPopulation population;
            lock (Gate)
            {
                for (int i = cache.Count - 1; i >= 0; i--)
                {
                    LandSnapshot c = cache[i];
                    if (c.Year != year || !Same(c.Society?.Settings ?? settings, settings)) continue;
                    cache.RemoveAt(i);
                    cache.Add(c);
                    return c;
                }

                economy = model;
                population = pop;
            }

            if (economy == null) return null;
            LandSnapshot built = Build(economy, population, year, settings);
            lock (Gate)
            {
                cache.Add(built);
                while (cache.Count > LandStyle.CacheYears) cache.RemoveAt(0);
            }

            return built;
        }

        /// <summary>Whether two season settings are the same (a season built with one is current for the other).</summary>
        public static bool Same(SocialSettings a, SocialSettings b) =>
            a.Noise == b.Noise && a.Continuation == b.Continuation && a.Polarization == b.Polarization && a.Rewire == b.Rewire &&
            a.Forgive == b.Forgive;

        // ------------------------------------------------------------------ requests (main thread)

        /// <summary>
        /// Shows a year: blocking (a preset; built here, published now) or on a worker (published by a later
        /// <see cref="Tick"/>; rapid requests coalesce to the latest).
        /// </summary>
        public static void Request(int year, bool blocking)
        {
            if (model == null) return;
            wantedYear = year;
            if (blocking)
            {
                if (Current != null && Current.Year == year && Same(Current.Society?.Settings ?? wantedSettings, wantedSettings)) return;
                Publish(BuildBlocking(year, wantedSettings), true);
                return;
            }

            if (Current != null && Current.Year == year && yearTask == null) return;
            if (yearTask == null) StartYear(year);
        }

        static void StartYear(int year)
        {
            SocialSettings settings = wantedSettings;
            yearTask = Task.Run(() => BuildBlocking(year, settings));
        }

        /// <summary>Reruns the season of the snapshot on screen with new settings (blocking, or on a worker).</summary>
        public static void RequestSociety(SocialSettings s, bool blocking)
        {
            lock (Gate) wantedSettings = s;   // BuildBlocking(int) reads it from any thread
            if (Current == null || model == null) return;
            if (blocking)
            {
                Publish(WithSociety(Current, s), true);
                return;
            }

            if (societyTask == null)
            {
                LandSnapshot from = Current;
                societyTask = Task.Run(() => WithSociety(from, s));
            }
            else societyPending = true;
        }

        static LandSnapshot WithSociety(LandSnapshot from, SocialSettings s)
        {
            SocialSeasonResult society = SocialSeason.Run(model.Data, from.Land, from.Players, s, from.Year);
            LandSnapshot n = new LandSnapshot
            {
                Year = from.Year, Land = from.Land, Players = from.Players, Money = from.Money, Society = society,
                Circuit = from.Circuit
            };
            n.ChecksLine = ChecksLine(n, model.Data);
            lock (Gate)
            {
                cache.Add(n);
                while (cache.Count > LandStyle.CacheYears) cache.RemoveAt(0);
            }

            return n;
        }

        /// <summary>
        /// The betrayal season of the snapshot on screen (5.4: the incident of <see cref="SocialSeason.BetrayalIncident"/>
        /// in round 48, measured against the baseline), computed the first time it is needed: blocking returns it at once;
        /// otherwise null until a later <see cref="Tick"/> has it (Current.Betrayal).
        /// </summary>
        public static SocialSeasonResult Betrayal(bool blocking)
        {
            LandSnapshot s = Current;
            if (s == null || model == null) return null;
            if (s.Betrayal != null) return s.Betrayal;
            if (blocking)
            {
                s.Betrayal = BuildBetrayal(model, s);
                return s.Betrayal;
            }

            if (betrayalTask == null || betrayalFor != s.Version)
            {
                betrayalFor = s.Version;
                EconomyModel economy = model;
                betrayalTask = Task.Run(() => BuildBetrayal(economy, s));
            }

            return null;
        }

        /// <summary>The betrayal season of a snapshot (pure; any thread).</summary>
        public static SocialSeasonResult BuildBetrayal(EconomyModel economy, LandSnapshot s)
        {
            if (s?.Society == null) return null;
            Incident? incident = SocialSeason.BetrayalIncident(s.Society, s.Players);
            return incident.HasValue
                ? SocialSeason.Run(economy.Data, s.Land, s.Players, s.Society.Settings, s.Year, incident)
                : null;
        }

        /// <summary>
        /// A land layer swapped to a snapshot version (after its rebuild and upload; once at its first upload, which also
        /// registers it). <see cref="ShownYear"/> moves when every registered layer reported the current version.
        /// </summary>
        public static void ReportReady(string layer, int version)
        {
            if (string.IsNullOrEmpty(layer)) return;
            layers.Add(layer);
            if (Current != null && version == Current.Version) ready.Add(layer);
            UpdateShown();
        }

        /// <summary>Publishes finished asynchronous builds (the land's view layer, every frame).</summary>
        public static void Tick()
        {
            if (yearTask != null && yearTask.IsCompleted)
            {
                LandSnapshot s = Take(ref yearTask);
                if (s != null && s.Year == wantedYear) Publish(s, false);
                else if (wantedYear >= 0 && (Current == null || Current.Year != wantedYear)) StartYear(wantedYear);
                // settings changed while the year was building: its season used the old ones, so rerun it
                if (Current != null && societyTask == null && !Same(Current.Society?.Settings ?? wantedSettings, wantedSettings))
                {
                    RequestSociety(wantedSettings, false);
                }
            }

            if (societyTask != null && societyTask.IsCompleted)
            {
                LandSnapshot s = Take(ref societyTask);
                if (s != null && Current != null && s.Year == Current.Year) Publish(s, false);
                if (societyPending || Current != null && yearTask == null && !Same(Current.Society?.Settings ?? wantedSettings, wantedSettings))
                {
                    // a newer setting, or a season run for a year that is no longer on screen
                    societyPending = false;
                    RequestSociety(wantedSettings, false);
                }
            }

            if (betrayalTask != null && betrayalTask.IsCompleted)
            {
                Task<SocialSeasonResult> t = betrayalTask;
                betrayalTask = null;
                if (t.IsFaulted) Debug.LogError("[Why] LandService: the betrayal season failed: " + t.Exception?.GetBaseException());
                else if (Current != null && Current.Version == betrayalFor && Current.Betrayal == null) Current.Betrayal = t.Result;
            }
        }

        static LandSnapshot Take(ref Task<LandSnapshot> task)
        {
            Task<LandSnapshot> t = task;
            task = null;
            if (t.IsFaulted)
            {
                Debug.LogError("[Why] LandService: a land build failed: " + t.Exception?.GetBaseException());
                return null;
            }

            return t.Result;
        }

        /// <summary>Puts a snapshot on screen: a new version (a shallow copy, so cached snapshots keep their own), Changed.</summary>
        static void Publish(LandSnapshot s, bool blocking)
        {
            if (s == null) return;
            Version++;
            LandSnapshot shown = new LandSnapshot
            {
                Year = s.Year, Version = Version, Blocking = blocking, Land = s.Land, Players = s.Players, Money = s.Money,
                Society = s.Society, Betrayal = s.Betrayal, Circuit = s.Circuit, ChecksLine = s.ChecksLine
            };
            Current = shown;
            ready.Clear();
            Changed?.Invoke(shown);
            UpdateShown();
        }

        static void UpdateShown()
        {
            if (Current == null) return;
            if (layers.Count == 0 || ready.IsSupersetOf(layers)) ShownYear = Current.Year;
        }
    }
}
