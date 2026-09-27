using System;
using System.Collections.Generic;
using System.Globalization;

namespace Why.Axis
{
    /// <summary>Visual weight of a tick, derived from its rank.</summary>
    public enum TickClass
    {
        Fine = 0,
        Minor = 1,
        Medium = 2,
        Major = 3
    }

    /// <summary>One "nice" moment marked on the time axis.</summary>
    public struct TimeTick
    {
        /// <summary>Years before now (0 = the present moment).</summary>
        public double YearsAgo;

        /// <summary>Clock arc of the tick (never below <see cref="DeepTime.NowArc"/>).</summary>
        public float U;

        /// <summary>
        /// Importance. Ranks the tick for geometric level of detail (a tick fades when a tick of equal or
        /// higher rank is too close on screen) and sets its class; see <see cref="LabelPriority"/>.
        /// </summary>
        public float Rank;

        /// <summary>Axis label, or null for an unlabeled tick.</summary>
        public string Text;

        /// <summary>Index of the nearest older tick with a rank >= this one, or -1.</summary>
        public int Older;

        /// <summary>Index of the nearest younger tick with a rank >= this one, or -1.</summary>
        public int Younger;

        public TickClass Class =>
            Rank >= TimeTicks.MajorRank ? TickClass.Major
            : Rank >= TimeTicks.MediumRank ? TickClass.Medium
            : Rank >= TimeTicks.MinorRank ? TickClass.Minor
            : TickClass.Fine;

        /// <summary>
        /// Label priority on the shared label scale (life clades tier 1 = 40, leaves ~0.25): majors 25-30,
        /// medium 8-16, minor 3-6, and fine detail below 1 so dense axis numbers never crowd out content.
        /// </summary>
        public float LabelPriority => Rank >= TimeTicks.MinorRank ? Rank : 0.3f + 0.2f * Rank;
    }

    /// <summary>
    /// The catalog of axis ticks at every scale: billions of years, geologic and archaeological time, the
    /// calendar of recorded history, and the present quadrant of the super-log clock where the last months
    /// shrink to days, seconds and finally the Planck time. Pure and thread safe.
    /// </summary>
    public static class TimeTicks
    {
        public const float MajorRank = 24;
        public const float MediumRank = 8;
        public const float MinorRank = 3;

        /// <summary>Rank of the "now" tick: it always survives level of detail.</summary>
        public const float NowRank = 99;

        /// <summary>Seconds in a Julian year (the unit of "years ago").</summary>
        public const double SecondsPerYear = 365.25 * 86400;

        const double DaysPerYear = 365.25;
        const double PlanckTimeSeconds = 5.39e-44;

        /// <summary>Two ticks closer than this in ln(years ago) are one moment; the higher rank wins.</summary>
        const double DuplicateLn = 0.004;

        /// <summary>
        /// Builds the ticks ordered from the Big Bang to now, with duplicates merged and level-of-detail
        /// neighbors resolved.
        /// </summary>
        public static List<TimeTick> Build(double nowYear)
        {
            List<TimeTick> all = new List<TimeTick>(512);
            AddDeepTime(all, nowYear);
            AddCalendar(all, nowYear);
            AddPresent(all);

            all.Sort((a, b) => b.YearsAgo.CompareTo(a.YearsAgo));
            List<TimeTick> ticks = Deduplicate(all);
            for (int i = 0; i < ticks.Count; i++)
            {
                TimeTick t = ticks[i];
                t.U = Math.Max(DeepTime.Arc(t.YearsAgo), DeepTime.NowArc);
                t.Older = FindNeighbor(ticks, i, -1);
                t.Younger = FindNeighbor(ticks, i, 1);
                ticks[i] = t;
            }

            return ticks;
        }

        // --- deep time: Ga, Ma, ka --------------------------------------------------------------------

        static void AddDeepTime(List<TimeTick> list, double nowYear)
        {
            Add(list, DeepTime.AgeU, 30, nowYear);

            // billions of years, with 100 Ma steps for the unrolled cosmic and early-Earth views
            for (int k = 10; k <= 137; k++)
            {
                float rank;
                if (k % 10 == 0)
                {
                    int ga = k / 10;
                    rank = ga == 1 ? 29 : ga == 10 ? 27 : ga == 5 ? 26 : ga == 4 ? 14 : ga <= 3 ? 12 : 10;
                }
                else
                {
                    rank = k % 5 == 0 ? 3 : 1;
                }

                Add(list, k * 1e8, rank, nowYear);
            }

            // millions of years: 1..9 x 10^k Ma for 1, 10 and 100 Ma, plus the half steps between them
            AddDecade(list, 1e8, 28, 14, 5, nowYear);
            AddDecade(list, 1e7, 27, 12, 4, nowYear);
            AddDecade(list, 1e6, 29, 12, 4, nowYear);

            // thousands of years down to 10 ka, where the calendar takes over
            AddDecade(list, 1e5, 27, 12, 4, nowYear);
            AddDecade(list, 1e4, 28, 11, 4, nowYear);
        }

        /// <summary>m x unit for m = 1..9 (1 = major, 5 = medium, others minor) and (m + 0.5) x unit (fine).</summary>
        static void AddDecade(List<TimeTick> list, double unit, float oneRank, float fiveRank, float otherRank,
            double nowYear)
        {
            for (int m = 1; m <= 9; m++)
            {
                Add(list, m * unit, m == 1 ? oneRank : m == 5 ? fiveRank : otherRank, nowYear);
                Add(list, (m + 0.5) * unit, 1.5f, nowYear);
            }
        }

        static void Add(List<TimeTick> list, double yearsAgo, float rank, double nowYear) =>
            list.Add(new TimeTick { YearsAgo = yearsAgo, Rank = rank, Text = DeepTime.FormatShort(yearsAgo, nowYear) });

        // --- recorded history: calendar years -----------------------------------------------------------

        static void AddCalendar(List<TimeTick> list, double nowYear)
        {
            // BCE: millennia, half millennia, and centuries close enough to matter in the civilizations view
            for (int bce = 8000; bce >= 100; bce -= 100)
            {
                float rank;
                if (bce % 1000 == 0) rank = bce == 1000 ? 13 : bce == 3000 ? 12 : bce == 5000 ? 10 : 8;
                else if (bce == 500) rank = 12;
                else if (bce % 500 == 0) rank = 2;
                else if (bce <= 900) rank = 3;
                else if (bce <= 2900) rank = 1.5f;
                else continue;
                AddYear(list, 1 - bce, rank, nowYear);
            }

            AddYear(list, 1, 28, nowYear);

            // CE centuries and half centuries up to 1650; decades from 1710 for the modern view
            for (int year = 50; year <= 1900; year += 50)
            {
                if (year % 100 != 0)
                {
                    if (year < 1700) AddYear(list, year, 1.5f, nowYear);
                    continue;
                }

                float rank = year == 1900 ? 26 : year == 1000 ? 16 : year == 1500 ? 15 : year == 500 ? 12 : 4;
                AddYear(list, year, rank, nowYear);
            }

            for (int year = 1710; year < 1900; year += 10)
            {
                if (year % 100 != 0) AddYear(list, year, 2, nowYear);
            }

            // the twentieth century and after, year by year
            int currentYear = (int)Math.Floor(nowYear);
            for (int year = 1901; year <= currentYear; year++)
            {
                float rank;
                if (year == 2000) rank = 29;
                else if (year == currentYear) rank = 15;
                else if (year == 2020) rank = 14;
                else if (year == 1950) rank = 12;
                else if (year == 2010) rank = 10;
                else if (year % 10 == 0 || year > 2020) rank = 5;
                else rank = year % 5 == 0 ? 1.5f : 1;
                AddYear(list, year, rank, nowYear);
            }
        }

        /// <summary>Adds January 1 of an astronomical year (0 = 1 BCE, -499 = 500 BCE).</summary>
        static void AddYear(List<TimeTick> list, int astronomicalYear, float rank, double nowYear)
        {
            double yearsAgo = nowYear - astronomicalYear;
            if (yearsAgo <= 0) return;
            list.Add(new TimeTick { YearsAgo = yearsAgo, Rank = rank, Text = FormatYear(astronomicalYear) });
        }

        /// <summary>"500 BCE", "1 CE", "800 CE", "1776".</summary>
        public static string FormatYear(int astronomicalYear)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (astronomicalYear <= 0) return (1 - astronomicalYear).ToString(c) + " BCE";
            return astronomicalYear < 1000 ? astronomicalYear.ToString(c) + " CE" : astronomicalYear.ToString(c);
        }

        // --- the present quadrant: months to the Planck time ---------------------------------------------

        static void AddPresent(List<TimeTick> list)
        {
            AddAgo(list, 0.5, 10, "6 months ago");
            AddAgo(list, 0.25, 5, "3 months ago");
            AddAgo(list, 1.0 / 12, 12, "1 month ago");
            AddAgo(list, 14 / DaysPerYear, 5, "2 weeks ago");
            AddAgo(list, 7 / DaysPerYear, 11, "1 week ago");
            AddAgo(list, 3 / DaysPerYear, 5, "3 days ago");
            AddAgo(list, 1 / DaysPerYear, 26, "1 day ago");
            AddAgo(list, 12 * 3600 / SecondsPerYear, 5, "12 hours ago");
            AddAgo(list, 6 * 3600 / SecondsPerYear, 4, "6 hours ago");
            AddAgo(list, 3600 / SecondsPerYear, 25, "1 hour ago");
            AddAgo(list, 1800 / SecondsPerYear, 4, "30 min ago");
            AddAgo(list, 600 / SecondsPerYear, 8, "10 min ago");
            AddAgo(list, 60 / SecondsPerYear, 14, "1 min ago");
            AddAgo(list, 10 / SecondsPerYear, 5, "10 s ago");
            AddAgo(list, 1 / SecondsPerYear, 27, "1 second ago");

            // below a second the clock keeps shrinking: each tick is another factor of a thousand
            AddAgo(list, 0.1 / SecondsPerYear, 4, "0.1 s ago");
            AddAgo(list, 1e-3 / SecondsPerYear, 10, "1 ms ago");
            AddAgo(list, 1e-6 / SecondsPerYear, 9, "1 µs ago");
            AddAgo(list, 1e-9 / SecondsPerYear, 13, "1 ns ago");
            AddAgo(list, 1e-12 / SecondsPerYear, 4, "1 ps ago");
            AddAgo(list, 1e-15 / SecondsPerYear, 5, "1 fs ago");
            AddAgo(list, 1e-18 / SecondsPerYear, 4, "1 as ago");
            AddAgo(list, PlanckTimeSeconds / SecondsPerYear, 25, "Planck time");

            // the present moment itself; its label is the "Now" beacon
            list.Add(new TimeTick { YearsAgo = 0, Rank = NowRank, Text = null });
        }

        static void AddAgo(List<TimeTick> list, double yearsAgo, float rank, string text) =>
            list.Add(new TimeTick { YearsAgo = yearsAgo, Rank = rank, Text = text });

        // --- ordering ----------------------------------------------------------------------------------

        /// <summary>Merges ticks that mark the same moment (e.g. 8000 BCE and 10 ka), keeping the higher rank.</summary>
        static List<TimeTick> Deduplicate(List<TimeTick> sorted)
        {
            List<TimeTick> result = new List<TimeTick>(sorted.Count);
            foreach (TimeTick t in sorted)
            {
                if (result.Count > 0)
                {
                    TimeTick last = result[result.Count - 1];
                    bool same = last.YearsAgo > 0 && t.YearsAgo > 0 &&
                                Math.Abs(Math.Log(last.YearsAgo / t.YearsAgo)) < DuplicateLn;
                    if (same)
                    {
                        if (t.Rank > last.Rank) result[result.Count - 1] = t;
                        continue;
                    }
                }

                result.Add(t);
            }

            return result;
        }

        static int FindNeighbor(List<TimeTick> ticks, int i, int step)
        {
            float rank = ticks[i].Rank;
            for (int j = i + step; j >= 0 && j < ticks.Count; j += step)
            {
                if (ticks[j].Rank >= rank) return j;
            }

            return -1;
        }
    }
}
