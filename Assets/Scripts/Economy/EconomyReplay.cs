using System;
using System.Globalization;
using System.Threading.Tasks;
using Why.Economy.Land;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;

namespace Why.Economy
{
    /// <summary>
    /// The replay of the wave: the crest advancing a year at a time from the first year the land opens
    /// (<see cref="EconomyState.MinYear"/>; the ribs of 1947-49 already lie in its wake) to the latest year with data, so
    /// cause and effect plays out on the wave instead of being read off it. It moves <see cref="EconomyState.Year"/> only
    /// once <see cref="LandService"/> has put the year on screen (every land layer swapped to it), and keeps at most one
    /// land build in flight: while a year is shown the next year's snapshot is built on a worker into the service's cache
    /// (<see cref="LandService.BuildBlocking(int)"/>), and the step waits for that build, so the request that follows is
    /// answered from the cache and nothing piles up. Its pace comes from the data (<see cref="ReplayFacts"/>): about a
    /// second a year, <see cref="QuietSeconds"/> through quiet years, and a longer hold at each turning point. It pauses
    /// when the viewer moves the year (keys, the chip, the scrubber, a view with a year of its own), stops when the guided
    /// tour starts, and ends at the latest year. Main thread; <see cref="Tick"/> runs every frame (the replay's panel).
    /// </summary>
    public static class EconomyReplay
    {
        /// <summary>Seconds a year stays, from the request: a quiet year, any other year, a turning point (from when it
        /// shows: time to read it). Every year shows at least <see cref="MinShownSeconds"/>.</summary>
        public const float QuietSeconds = .6f, YearSeconds = 1f, TurnSeconds = 2.6f, MinShownSeconds = .35f;

        /// <summary>The readout stays this long after the replay reaches the latest year.</summary>
        public const float EndHoldSeconds = 6f;

        /// <summary>A year not on screen this long after it was asked for is a stall: logged as an error, and the replay pauses.</summary>
        public const float StallSeconds = 20f;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The crest is advancing.</summary>
        public static bool Playing { get; private set; }

        /// <summary>A replay is under way: playing, paused before its end, or just ended (its readout still shows).</summary>
        public static bool Active { get; private set; }

        /// <summary>Incremented on every change of <see cref="Playing"/> or <see cref="Active"/> (the controls' icons).</summary>
        public static int Version { get; private set; }

        /// <summary>What each year changed and the turning points (set by the replay's panel once the data is loaded).</summary>
        public static ReplayFacts Facts { get; set; }

        /// <summary>The year the replay asked for last (-1: none), and how long ago it asked and the year has shown.</summary>
        static int asked = -1;

        /// <summary>The last year the replay saw on screen (-1: none yet): the readout holds it while the next one builds.</summary>
        static int lastShown = -1;

        static float sinceAsked, sinceShown, sinceEnd;
        static bool shown, ended, stallLogged;

        /// <summary>The next year's land, building on a worker while the year is shown (null: none in flight).</summary>
        static Task<LandSnapshot> prefetch;

        static int prefetchYear = -1;

        /// <summary>A frame longer than this while a year is on screen counts as a hitch in the closing log line.</summary>
        const float HitchSeconds = .25f;

        // what the replay took, for its closing log line: the land's waits, the frames while years were on screen
        static int firstYear, steps, frames, hitches, frameYear;
        static float elapsed, waitSum, waitMax, frameSum, frameMax;
        static string frameView;

        /// <summary>Back to rest (a new scene load).</summary>
        public static void Reset()
        {
            Playing = Active = false;
            asked = prefetchYear = lastShown = -1;
            prefetch = null;
            shown = ended = stallLogged = false;
            sinceAsked = sinceShown = sinceEnd = 0;
            Facts = null;
            Version++;
        }

        /// <summary>Play or pause (R, the chip's button, the headset menu).</summary>
        public static void Toggle()
        {
            if (Playing) Pause();
            else Play();
        }

        /// <summary>
        /// Plays: from where it was paused, else from the year the viewer chose; from the first year when the crest stands at
        /// the present (the default year or later) or <paramref name="restart"/>.
        /// </summary>
        public static void Play(bool restart = false)
        {
            int year = EconomyState.Year;
            bool fromStart = restart || year >= EconomyState.MaxYear || !Active && year >= EconomyState.DefaultYear(EconomyState.MaxYear);
            if (fromStart) year = EconomyState.MinYear;
            if (!Active || fromStart)
            {
                lastShown = -1;
                firstYear = year;
                steps = frames = hitches = 0;
                elapsed = waitSum = waitMax = frameSum = frameMax = 0;
                frameView = null;
            }

            Playing = Active = true;
            ended = stallLogged = false;
            Ask(year);
            Version++;
        }

        /// <summary>Holds the crest where it is; the readout stays.</summary>
        public static void Pause()
        {
            if (!Playing) return;
            Playing = false;
            Version++;
        }

        /// <summary>Ends the replay (the tour starts, the scene closes): nothing more is asked and the readout goes.</summary>
        public static void Stop()
        {
            if (!Playing && !Active) return;
            Playing = Active = false;
            asked = lastShown = -1;
            Version++;
        }

        /// <summary>The year the replay shows now (-1 when none): the last year it saw on screen, held while the next builds.</summary>
        public static int ShownYear => Active ? lastShown : -1;

        /// <summary>Seconds the year asked for stays (see <see cref="QuietSeconds"/>).</summary>
        public static float Pace(int year)
        {
            ReplayFacts.Year f = Facts?.At(year);
            if (f == null || year < Facts.First || year > Facts.Last) return YearSeconds;
            return f.Turns.Count > 0 ? TurnSeconds : f.Quiet ? QuietSeconds : YearSeconds;
        }

        static void Ask(int year)
        {
            asked = year;
            sinceAsked = sinceShown = 0;
            shown = false;
            EconomyState.SetYear(year);
        }

        /// <summary>
        /// One frame: follows the land, prefetches the next year, steps when the year has had its time; pauses when the year
        /// was moved by someone else, stops for the tour.
        /// </summary>
        public static void Tick(float dt, bool tourActive)
        {
            if (!Active) return;
            if (tourActive)
            {
                Stop();
                return;
            }

            if (ended)
            {
                sinceEnd += dt;
                if (sinceEnd >= EndHoldSeconds || EconomyState.Year != asked) Stop();
                return;
            }

            if (!Playing) return;
            if (EconomyState.Year != asked || EconomyState.PreviewYear >= 0)
            {
                // the viewer took the year (keys, the chip, the scrubber, a view of its own year): hold here
                Pause();
                return;
            }

            elapsed += dt;
            sinceAsked += dt;
            LandSnapshot s = LandService.Current;
            if (!shown)
            {
                shown = s != null && s.Year == asked && LandService.ShownYear == asked;
                if (!shown)
                {
                    if (sinceAsked > StallSeconds && !stallLogged)
                    {
                        stallLogged = true;
                        Debug.LogError("[Why] Replay: " + asked.ToString(Ci) + " was not on screen " + StallSeconds.ToString("0", Ci) +
                                       " s after it was asked for (land on screen " + (s?.Year ?? -1).ToString(Ci) + ")");
                        Pause();
                    }

                    return;
                }

                lastShown = asked;
                Version++;
                steps++;
                waitSum += sinceAsked;
                waitMax = Math.Max(waitMax, sinceAsked);
            }
            else
            {
                frames++;
                frameSum += dt;
                if (dt > HitchSeconds) hitches++;
                if (dt > frameMax)
                {
                    frameMax = dt;
                    frameYear = asked;
                    frameView = LandView.PresetId;
                }
            }

            sinceShown += dt;
            if (asked >= EconomyState.MaxYear)
            {
                if (sinceShown < Pace(asked)) return;
                Playing = false;
                ended = true;
                sinceEnd = 0;
                Version++;
                Debug.Log("[Why] Replay " + firstYear.ToString(Ci) + "-" + asked.ToString(Ci) + ": " + steps.ToString(Ci) + " years in " +
                          elapsed.ToString("0.0", Ci) + " s; waited for the land " + (waitSum / Math.Max(1, steps)).ToString("0.00", Ci) +
                          " s a year (longest " + waitMax.ToString("0.00", Ci) + " s); frames on screen: mean " +
                          (frameSum / Math.Max(1, frames)).ToString("0.000", Ci) + " s, " + hitches.ToString(Ci) + " over " +
                          HitchSeconds.ToString("0.00", Ci) + " s, longest " + frameMax.ToString("0.000", Ci) + " s (" +
                          frameYear.ToString(Ci) + ", " + (frameView ?? "?") + ")");
                return;
            }

            // the next year's land builds on a worker while this one is shown (one build in flight, never more)
            int next = asked + 1;
            if (prefetch == null && prefetchYear != next)
            {
                prefetchYear = next;
                prefetch = Task.Run(() => LandService.BuildBlocking(next));
            }

            if (prefetch != null)
            {
                if (!prefetch.IsCompleted) return;
                if (prefetch.IsFaulted) Debug.LogWarning("[Why] Replay: building " + prefetchYear.ToString(Ci) + " ahead failed: " + prefetch.Exception?.GetBaseException().Message);
                prefetch = null;
            }

            float pace = Pace(asked);
            bool turn = pace >= TurnSeconds;
            if (sinceAsked < pace || sinceShown < (turn ? pace - .4f : MinShownSeconds)) return;
            Ask(next);
        }
    }
}
