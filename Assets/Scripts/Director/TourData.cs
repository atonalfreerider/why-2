using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Director
{
    /// <summary>One narrated stop of the guided tour (a step of Data/tour.json, see Docs/DATA.md).</summary>
    public sealed class TourStep
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        [JsonProperty("text")] public string Text;

        /// <summary>Id of the view preset (<see cref="ViewPresets"/>) the graph is re-scaled to.</summary>
        [JsonProperty("focus")] public string Focus;

        /// <summary>Anchor key ("kind:id") the attention arrow points at.</summary>
        [JsonProperty("anchor")] public string AnchorKey;

        /// <summary>Further anchor keys whose geometry glows during the step.</summary>
        [JsonProperty("highlight")] public List<string> HighlightKeys = new List<string>();

        /// <summary>Minimum seconds on screen during autoplay (reading time may extend it).</summary>
        [JsonProperty("hold")] public float Hold;
    }

    /// <summary>The narrated guide through the whole graph: a title and an ordered list of steps.</summary>
    public sealed class TourScript
    {
        /// <summary>Resources path of the tour (without extension).</summary>
        public const string ResourcePath = "Data/tour";

        [JsonProperty("title")] public string Title;
        [JsonProperty("steps")] public List<TourStep> Steps = new List<TourStep>();

        /// <summary>
        /// Loads Data/tour.json (main thread). Falls back to <see cref="BuiltIn"/> when the file is missing,
        /// malformed or empty, so the director always has something to narrate.
        /// </summary>
        public static TourScript Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Why] director: '{ResourcePath}' not found, using the built-in tour");
                return BuiltIn();
            }

            string json = asset.text;
            Resources.UnloadAsset(asset);
            try
            {
                TourScript script = JsonConvert.DeserializeObject<TourScript>(json.TrimStart('\uFEFF'));
                if (script != null && Normalize(script)) return script;
                Debug.LogWarning($"[Why] director: '{ResourcePath}' has no steps, using the built-in tour");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] director: could not parse '{ResourcePath}' ({e.Message}), using the built-in tour");
            }

            return BuiltIn();
        }

        /// <summary>Drops empty steps and replaces missing strings; false if nothing is left.</summary>
        static bool Normalize(TourScript script)
        {
            if (script.Steps == null) return false;
            script.Steps.RemoveAll(s => s == null);
            for (int i = 0; i < script.Steps.Count; i++)
            {
                TourStep s = script.Steps[i];
                s.Id = string.IsNullOrEmpty(s.Id) ? "step" + (i + 1) : s.Id;
                s.Title = s.Title ?? "";
                s.Text = s.Text ?? "";
                s.HighlightKeys = s.HighlightKeys ?? new List<string>();
                s.HighlightKeys.RemoveAll(string.IsNullOrWhiteSpace);
            }

            script.Title = string.IsNullOrWhiteSpace(script.Title) ? "Why: from the Big Bang to this moment" : script.Title;
            return script.Steps.Count > 0;
        }

        /// <summary>A short tour through the main stops, used when Data/tour.json is unavailable.</summary>
        public static TourScript BuiltIn()
        {
            return new TourScript
            {
                Title = "Why: from the Big Bang to this moment",
                Steps = new List<TourStep>
                {
                    Step("welcome", "overview", "now", 10, "Why are you here, right now?",
                        "This path is one chain of causes. It starts with the Big Bang at 6 o'clock, runs clockwise " +
                        "through deep time and ends at this exact moment. Red is matter, green is life, blue is " +
                        "humans; the closer to the inner track, the more directly it led to you."),
                    Step("big_bang", "cosmos", "epoch:big_bang", 12, "Everything from almost nothing",
                        "13.8 billion years ago space itself expanded from a hot, dense state. Every atom in your " +
                        "body was made in the minutes and the billions of years that followed.",
                        "matter:universe"),
                    Step("earth", "earth", "matter:earth", 12, "A rocky planet in the right place",
                        "Out of a collapsing cloud of gas and dust came the Sun and, 4.5 billion years ago, the " +
                        "Earth: close enough to its star for liquid water, far enough not to boil it away.",
                        "matter:sun"),
                    Step("life", "life", "lifeevent:earliest_life", 12, "Matter that copies itself",
                        "Within a few hundred million years, chemistry in the oceans crossed a threshold: " +
                        "molecules that make copies of themselves. Every living thing descends from them.",
                        "clade:life"),
                    Step("sapiens", "hominins", "lifeevent:homo_sapiens", 12, "Our lineage rises",
                        "Of all the branches of the tree of life, one line of apes learned to shape stone, tame " +
                        "fire and talk. 300,000 years ago Homo sapiens rises out of the green layer into the blue.",
                        "clade:hominidae"),
                    Step("civilizations", "civilizations", "civ:egyptians", 12, "Civilizations",
                        "At 3 o'clock the clock becomes a road. The blue streams are civilizations, their width " +
                        "their relative power; peoples closest to our own story run on the inside.",
                        "civ:chinese"),
                    Step("lifelines", "smv", "smv:us", 12, "Individual lives",
                        "Up close, the human layer resolves into lifelines, men and women, rising and falling " +
                        "with their social market value. Each line stands for many people."),
                    Step("now", "present", "now", 10, "This moment",
                        "Every line arrives here. Everything you have seen, from the first particles to the last " +
                        "generation, is part of the reason this moment exists."),
                }
            };
        }

        static TourStep Step(string id, string focus, string anchor, float hold, string title, string text,
            params string[] highlight)
        {
            return new TourStep
            {
                Id = id, Focus = focus, AnchorKey = anchor, Hold = hold, Title = title, Text = text,
                HighlightKeys = new List<string>(highlight)
            };
        }
    }

    /// <summary>A tour step with its preset and anchors resolved against the loaded graph.</summary>
    public sealed class ResolvedStep
    {
        public TourStep Step;

        /// <summary>The view to re-scale to; null keeps the current view.</summary>
        public ViewPreset Preset;

        /// <summary>What the attention arrow points at; null shows the text without an arrow.</summary>
        public Anchor Target;

        /// <summary>Resolved highlight anchors (the arrow target is not repeated).</summary>
        public readonly List<Anchor> Highlights = new List<Anchor>();

        /// <summary>Autoplay duration: max(hold, reading time).</summary>
        public float Duration;

        /// <summary>Reading time for autoplay: seconds per character plus a pause.</summary>
        public const float SecondsPerCharacter = 0.06f;

        public const float ReadingPause = 2f;

        /// <summary>
        /// Resolves every step of a script. Call after the graph is loaded (anchors are registered while
        /// layers build); logs one warning per step with unresolved keys and counts them.
        /// </summary>
        public static List<ResolvedStep> ResolveAll(TourScript script, out int unresolved)
        {
            List<ResolvedStep> result = new List<ResolvedStep>(script.Steps.Count);
            List<string> problems = new List<string>();
            unresolved = 0;
            foreach (TourStep step in script.Steps)
            {
                problems.Clear();
                ResolvedStep r = new ResolvedStep
                {
                    Step = step,
                    Duration = Mathf.Max(step.Hold, SecondsPerCharacter * step.Text.Length + ReadingPause)
                };

                if (!string.IsNullOrWhiteSpace(step.Focus))
                {
                    r.Preset = FindPreset(step.Focus);
                    if (r.Preset == null) problems.Add($"focus '{step.Focus}'");
                }

                if (!string.IsNullOrWhiteSpace(step.AnchorKey) && !Anchors.TryGet(step.AnchorKey, out r.Target))
                {
                    problems.Add($"anchor '{step.AnchorKey}'");
                }

                foreach (string key in step.HighlightKeys)
                {
                    if (!Anchors.TryGet(key, out Anchor a))
                    {
                        problems.Add($"highlight '{key}'");
                        continue;
                    }

                    if (r.Target != null && a.Key == r.Target.Key) continue;
                    r.Highlights.Add(a);
                }

                if (problems.Count > 0)
                {
                    unresolved += problems.Count;
                    Debug.LogWarning($"[Why] tour step '{step.Id}': unresolved {string.Join(", ", problems)}");
                }

                result.Add(r);
            }

            return result;
        }

        /// <summary>The preset with this id, or null (ViewPresets.Get silently falls back to the overview).</summary>
        public static ViewPreset FindPreset(string id)
        {
            foreach (ViewPreset p in ViewPresets.All)
            {
                if (p.Id == id) return p;
            }

            return null;
        }
    }
}
