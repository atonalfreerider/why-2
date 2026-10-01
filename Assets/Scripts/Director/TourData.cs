using System;
using System.Collections.Generic;
using System.Reflection;
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
        /// <summary>Resources path of the current scene's tour (without extension).</summary>
        public static string ResourcePath => GraphScene.IsEconomy ? "Data/economy/tour" : "Data/tour";

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
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                // a value of the wrong type (e.g. "hold": null) skips that member instead of discarding the
                // whole tour; malformed JSON syntax still falls back to the built-in tour below
                Error = (sender, args) =>
                {
                    if (args.ErrorContext.Error is JsonReaderException) return;
                    Debug.LogWarning($"[Why] director: '{ResourcePath}' at {args.ErrorContext.Path}: " +
                                     args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            try
            {
                TourScript script = JsonConvert.DeserializeObject<TourScript>(json.TrimStart('\uFEFF'), settings);
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
                s.Focus = s.Focus?.Trim();
                s.HighlightKeys = s.HighlightKeys ?? new List<string>();
                s.HighlightKeys.RemoveAll(string.IsNullOrWhiteSpace);
            }

            script.Title = string.IsNullOrWhiteSpace(script.Title) ? "Why: from the Big Bang to this moment" : script.Title;
            return script.Steps.Count > 0;
        }

        /// <summary>The economy scene's short tour, used when Data/economy/tour.json is unavailable.</summary>
        static TourScript EconomyBuiltIn()
        {
            return new TourScript
            {
                Title = "The economy: where money goes, and why",
                Steps = new List<TourStep>
                {
                    Step("welcome", "overview", "now", 10, "Money, people and the things they fear and want",
                        "Blue lines are people in the United States since 1950, rising and falling with their social " +
                        "market value. Beneath them stands the wall of industries that pays them; money flows both ways."),
                    Step("created", "industries", null, 12, "Where value is created",
                        "Each band is an industry, as tall as the value it adds each year. The bright part of a band " +
                        "is what its owners keep; the rest pays the people who work in it."),
                    Step("circuit", "circuit", null, 12, "The circuit",
                        "Industries pay wages, profits and taxes; households spend what they get back into industries. " +
                        "Follow the money around once."),
                    Step("mind", "mind", null, 12, "Desire and fear",
                        "Every purchase moves toward something wanted or away from something feared. Most money goes to " +
                        "the present; few minds spend on a future they control."),
                    Step("games", "games", null, 12, "Cooperation",
                        "One round of the prisoner's dilemma ends in defection. Meet again and again, and cooperation " +
                        "can climb, as long as someone forgives."),
                }
            };
        }

        /// <summary>A short tour through the main stops, used when Data/tour.json is unavailable.</summary>
        public static TourScript BuiltIn()
        {
            if (GraphScene.IsEconomy) return EconomyBuiltIn();
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

        /// <summary>Autoplay duration: max(hold, reading time, narration clip + pause).</summary>
        public float Duration;

        /// <summary>The recorded narration for this stop (Resources/Audio/Tour), or null when none exists.</summary>
        public AudioClip Narration;

        /// <summary>Reading time for autoplay: seconds per character plus a pause.</summary>
        public const float SecondsPerCharacter = 0.06f;

        public const float ReadingPause = 2f;

        /// <summary>
        /// Distance from a lens window's center (fraction of its half length) beyond which an anchor is
        /// refitted: the window edge, where the lens starts fading. Presets may deliberately put anchors near
        /// their edge (the cosmos view packs the early universe against the Big Bang), and those stay as they are.
        /// </summary>
        const double FitThreshold = 1.0;

        /// <summary>Where a widened window puts the anchor (fraction of its half length from the center).</summary>
        const double FitTarget = 0.85;

        /// <summary>
        /// Resolves every step of a script. Call after the graph is loaded (anchors are registered while
        /// layers build); logs one warning per step with unresolved keys and counts them. Lens presets whose
        /// window does not contain the step's anchor are widened (see <see cref="FitToAnchor"/>).
        /// </summary>
        public static List<ResolvedStep> ResolveAll(TourScript script, out int unresolved)
        {
            List<ResolvedStep> result = new List<ResolvedStep>(script.Steps.Count);
            List<string> problems = new List<string>();
            List<string> widened = new List<string>();
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

                ViewPreset fitted = FitToAnchor(r.Preset, r.Target);
                if (fitted != r.Preset)
                {
                    widened.Add($"{step.Id} ({step.Focus}: {DeepTime.FormatShort(fitted.YaOld, DeepTime.NowYear)} to " +
                                $"{DeepTime.FormatShort(fitted.YaNew, DeepTime.NowYear)})");
                    r.Preset = fitted;
                }

                if (problems.Count > 0)
                {
                    unresolved += problems.Count;
                    Debug.LogWarning($"[Why] tour step '{step.Id}': unresolved {string.Join(", ", problems)}");
                }

                result.Add(r);
            }

            if (widened.Count > 0)
            {
                Debug.Log($"[Why] director: widened the time window of {widened.Count} steps whose anchor lies " +
                          $"outside their view: {string.Join(", ", widened)}");
            }

            return result;
        }

        /// <summary>
        /// Lens presets fade everything outside their time window, so an arrow at an anchor beyond it would
        /// point at nothing and the camera would drift off the window. Returns a copy of the preset whose
        /// window is extended (in lens time, on the anchor's side only) until the anchor sits well inside it,
        /// or the preset itself when it is polar, already contains the anchor, or there is nothing to fit.
        /// </summary>
        public static ViewPreset FitToAnchor(ViewPreset preset, Anchor anchor)
        {
            if (preset == null || anchor == null || preset.Polar) return preset;

            // the lens window in ln(yearsAgo + C), exactly as WarpState.Window lays it out
            double c = Math.Max(preset.LogOffset, 1e-9);
            double lo = Math.Log(Math.Max(preset.YaNew, 0) + c);
            double hi = Math.Log(Math.Max(preset.YaOld, 0) + c);
            if (hi - lo < 1e-9) return preset;
            double x = Math.Log(Math.Max(anchor.YearsAgo, 0) + c);
            double center = 0.5 * (lo + hi), half = 0.5 * (hi - lo);
            if (Math.Abs(x - center) <= FitThreshold * half) return preset;

            // move only the edge on the anchor's side so the anchor lands at FitTarget of the new half length
            double yaOld = preset.YaOld, yaNew = preset.YaNew;
            if (x > center) yaOld = Math.Min(DeepTime.AgeU, Math.Exp((2 * x - (1 - FitTarget) * lo) / (1 + FitTarget)) - c);
            else yaNew = Math.Max(0, Math.Exp((2 * x - (1 - FitTarget) * hi) / (1 + FitTarget)) - c);

            // e.g. "now" on the present edge of a window that already ends now
            if (yaOld <= preset.YaOld && yaNew >= preset.YaNew) return preset;

            ViewPreset fitted = Copy(preset);
            fitted.YaOld = Math.Max(yaOld, preset.YaOld);
            fitted.YaNew = Math.Min(yaNew, preset.YaNew);
            return fitted;
        }

        /// <summary>
        /// A field-by-field copy of a preset (by reflection, so fields added to ViewPreset later carry over).
        /// It keeps the id, so the HUD and the layers treat it as the original view.
        /// </summary>
        static ViewPreset Copy(ViewPreset source)
        {
            ViewPreset copy = new ViewPreset();
            foreach (FieldInfo f in typeof(ViewPreset).GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                                 BindingFlags.NonPublic))
            {
                f.SetValue(copy, f.GetValue(source));
            }

            return copy;
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
