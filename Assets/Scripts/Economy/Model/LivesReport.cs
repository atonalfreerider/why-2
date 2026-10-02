using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Why.Economy.Data;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>
    /// What the economic lives say about themselves once run: the cut on agency that puts the target share of adults
    /// in control (calibrated once, then applied to every year so history shows how the share changed), the notable
    /// people, and the model's log (calibration factors, the population against the data by decade, cross-checks, and
    /// the thesis tested).
    /// </summary>
    internal static class LivesReport
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Years of the log's tables.</summary>
        static readonly int[] TableYears = { 1950, 1970, 1990, 2010, 2025 };

        // ================================================================ in control

        /// <summary>
        /// The agency cut: among the adults of <paramref name="year"/> who pass the autonomy gate (psyche.json
        /// gateAutonomy), the agency of the last one inside the target share of all adults. Agency saturates (its
        /// components are clamped), so ties at the cut are broken by reason, the continuous measure behind it. Sets every
        /// record's InControl and every year's share; returns the cut (the reason cut is in <paramref name="reasonCut"/>).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        public static float CalibrateControl(LivesInputs inp, PersonYear[] store, int[] offset, int[] first, int[] count,
            PopulationYear[] years, int y0, int year, out float reasonCut, out string note)
        {
            List<(float agency, float reason)> gated = new List<(float, float)>();
            int adults = 0;
            for (int i = 0; i < offset.Length; i++)
            {
                int k = year - first[i];
                if (count[i] == 0 || k < 0 || k >= count[i]) continue;
                PersonYear r = store[offset[i] + k];
                if (!r.Adult) continue;
                adults++;
                if (!inp.GateAutonomy || r.Autonomy >= 0.999f) gated.Add((r.Agency, r.Reason));
            }

            gated.Sort((a, b) => a.agency != b.agency ? b.agency.CompareTo(a.agency) : b.reason.CompareTo(a.reason));
            int want = (int)Math.Round(inp.InControlTarget * adults);
            float cut;
            reasonCut = 0;
            if (want <= 0 || gated.Count == 0)
            {
                cut = 1.01f;
                note = "no adult can be in control";
            }
            else if (gated.Count <= want)
            {
                cut = gated[gated.Count - 1].agency;
                reasonCut = gated[gated.Count - 1].reason;
                note = $"the autonomy gate admits only {Pct(gated.Count / (double)Math.Max(1, adults))} of adults " +
                       $"(target {Pct(inp.InControlTarget)}): everyone past it is in control";
            }
            else
            {
                cut = gated[want - 1].agency;
                reasonCut = gated[want - 1].reason;
                note = $"agency >= {cut.ToString("0.000", Inv)}" + (inp.GateAutonomy ? " with material autonomy" : "") +
                       $" ({Pct(gated.Count / (double)Math.Max(1, adults))} of adults have autonomy)";
            }

            for (int i = 0; i < offset.Length; i++)
            {
                for (int k = 0; k < count[i]; k++)
                {
                    ref PersonYear r = ref store[offset[i] + k];
                    r.InControl = r.Adult && (!inp.GateAutonomy || r.Autonomy >= 0.999f) &&
                                  (r.Agency > cut || (r.Agency == cut && r.Reason >= reasonCut));
                }
            }

            for (int y = 0; y < years.Length; y++)
            {
                if (years[y] == null) continue;
                int inControl = 0, n = 0;
                for (int i = 0; i < offset.Length; i++)
                {
                    int k = y0 + y - first[i];
                    if (count[i] == 0 || k < 0 || k >= count[i]) continue;
                    PersonYear r = store[offset[i] + k];
                    if (!r.Adult) continue;
                    n++;
                    if (r.InControl) inControl++;
                }

                years[y].InControlShare = n > 0 ? inControl / (float)n : 0;
            }

            return cut;
        }

        // ================================================================ notables

        /// <summary>
        /// Nine people alive in the last year who show the model's types (deterministic: the extreme of a measure,
        /// ties to the earlier born; nobody twice): an owner in control, an heir, a striver, an escapist, the most
        /// indebted, the oldest retiree, a young adult, a forgiver in a long marriage, an avenger who divorced.
        /// </summary>
        public static List<Notable> PickNotables(EconomicLives lives, LivesInputs inp, SmvSimulation sim, PersonTraits[] traits,
            float[] inheritedReal, int lastYear)
        {
            List<Notable> list = new List<Notable>();
            HashSet<int> taken = new HashSet<int>();
            List<SmvPerson> people = sim.People;
            int n = people.Count;
            double toReal = inp.Prices(2025, lastYear);

            // marriages: the current one's start, and who has divorced (and when last)
            double[] marriedSince = new double[n], divorcedAt = new double[n];
            for (int i = 0; i < n; i++)
            {
                marriedSince[i] = double.NaN;
                divorcedAt[i] = double.NaN;
            }

            double t = lastYear + 0.5;
            foreach (SmvMarriage m in sim.MarriageLog)
            {
                if (m.Wife < 0 || m.Husband < 0 || m.Wife >= n || m.Husband >= n) continue;
                if (m.Divorced)
                {
                    divorcedAt[m.Wife] = double.IsNaN(divorcedAt[m.Wife]) ? m.End : Math.Max(divorcedAt[m.Wife], m.End);
                    divorcedAt[m.Husband] = double.IsNaN(divorcedAt[m.Husband]) ? m.End : Math.Max(divorcedAt[m.Husband], m.End);
                }
                else if (m.Start <= t && t < m.End)
                {
                    marriedSince[m.Wife] = m.Start;
                    marriedSince[m.Husband] = m.Start;
                }
            }

            Pick(list, taken, n, "owner", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || !r.InControl) return double.NaN;
                return r.Wealth * (r.SelfEmployed ? 1 : 1e-3);
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                string what = r.SelfEmployed ? $"Runs their own business ({Money(r.Earned)} a year) and is" : "Is";
                return $"{what} in control of their path: net worth {Money(r.Wealth)}, saves {Pct(r.Category(6))} of " +
                       $"{Money(r.Disposable)} disposable income, agency {r.Agency.ToString("0.00", Inv)}.";
            });

            Pick(list, taken, n, "heir", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || inheritedReal[i] <= 0) return double.NaN;
                double worth = Math.Max(1, r.Wealth * toReal);
                return inheritedReal[i] >= 0.5 * worth ? inheritedReal[i] : inheritedReal[i] * 1e-3;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Inherited {Money(inheritedReal[i])} (2025 dollars) from parents; now worth {Money(r.Wealth * toReal)} (2025 dollars), " +
                       $"agency {r.Agency.ToString("0.00", Inv)}" + (r.InControl ? ", in control." : ", not in control.");
            });

            Pick(list, taken, n, "striver", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || r.InControl || r.Age < 25 || r.Age > 50) return double.NaN;
                if (traits[i].C < 0.8f || !lives.TryGet(i, lastYear - 10, out PersonYear before) || !before.Adult) return double.NaN;
                return r.IncomeRank - before.IncomeRank;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                lives.TryGet(i, lastYear - 10, out PersonYear b);
                return $"Conscientious (C {traits[i].C.ToString("+0.0;-0.0", Inv)} sd) and climbing: household income rank " +
                       $"{Pct(b.IncomeRank)} ten years ago, {Pct(r.IncomeRank)} now; not yet in control (agency " +
                       $"{r.Agency.ToString("0.00", Inv)}, autonomy {r.Autonomy.ToString("0.00", Inv)}).";
            });

            Pick(list, taken, n, "escapist", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || r.Spending <= 0) return double.NaN;
                return r.Fantasy;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Buys fantasy with {Pct(r.Fantasy)} of {Money(r.Spending)} of spending ({Pct(r.Escapism)} escapism, " +
                       $"{Pct(r.Status)} status); reason {r.Reason.ToString("0.00", Inv)}, present bias beta " +
                       $"{traits[i].Beta.ToString("0.00", Inv)}.";
            });

            Pick(list, taken, n, "indebted", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || r.ConsumerDebt <= 0) return double.NaN;
                return r.ConsumerDebt / Math.Max(1000.0, r.Disposable);
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Owes {Money(r.ConsumerDebt)} of consumer debt against {Money(r.Disposable)} of disposable income; " +
                       $"debt payments take {Pct(r.DebtService)} of it, and jeopardy {Pct(r.Jeopardy)} of spending.";
            });

            Pick(list, taken, n, "retiree", i =>
            {
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult || r.SocialSecurity <= 0) return double.NaN;
                return r.Age;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"At {Math.Floor(r.Age).ToString("0", Inv)} the oldest on Social Security: {Money(r.SocialSecurity)} a year, " +
                       $"net worth {Money(r.Wealth)}, jeopardy (health, insurance) {Pct(r.Jeopardy)} of spending.";
            });

            Pick(list, taken, n, "young", i =>
            {
                SmvPerson p = people[i];
                if (p.Birth < 2000 || p.Birth >= 2007) return double.NaN;
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult) return double.NaN;
                return r.Agency;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Born {people[i].Birth.ToString("0", Inv)}: earns {Money(r.Earned)}, saves {Pct(r.Category(6))}, " +
                       $"{Pct(r.Growth)} of spending on growth; agency {r.Agency.ToString("0.00", Inv)}.";
            });

            Pick(list, taken, n, "forgiver", i =>
            {
                if (traits[i].Strategy != PdStrategy.GenerousTitForTat || double.IsNaN(marriedSince[i])) return double.NaN;
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult) return double.NaN;
                double years = t - marriedSince[i];
                return years >= 25 ? years : double.NaN;
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Plays generous tit for tat (forgives a defection one time in three) and has been married " +
                       $"{(t - marriedSince[i]).ToString("0", Inv)} years; cooperates on {Pct(r.Cooperation)} of moves.";
            });

            Pick(list, taken, n, "avenger", i =>
            {
                if (traits[i].Strategy != PdStrategy.Grim || double.IsNaN(divorcedAt[i])) return double.NaN;
                if (!lives.TryGet(i, lastYear, out PersonYear r) || !r.Adult) return double.NaN;
                return divorcedAt[i];
            }, i =>
            {
                lives.TryGet(i, lastYear, out PersonYear r);
                return $"Plays grim trigger (one defection and never again) and divorced in " +
                       $"{Math.Floor(divorcedAt[i]).ToString("0", Inv)}; cooperates on {Pct(r.Cooperation)} of moves.";
            });

            return list;
        }

        [MethodImpl(LivesMath.Hot)]
        static void Pick(List<Notable> list, HashSet<int> taken, int n, string role, Func<int, double> score, Func<int, string> why)
        {
            int best = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (taken.Contains(i)) continue;
                double s = score(i);
                if (double.IsNaN(s) || s <= bestScore) continue;
                best = i;
                bestScore = s;
            }

            if (best < 0) return;
            taken.Add(best);
            list.Add(new Notable { Person = best, Role = role, Why = why(best) });
        }

        // ================================================================ the log

        public static string Build(EconomicLives lives, LivesInputs inp, SmvSimulation sim, PersonTraits[] traits,
            LivesSimulation run, TraitSampler sampler, string controlNote, long traitsMs, long setupMs, long yearsMs, long totalMs)
        {
            StringBuilder sb = new StringBuilder();
            int y0 = lives.FirstYear, y1 = lives.LastYear;
            double recordsMb = run.Store.Length * (double)System.Runtime.InteropServices.Marshal.SizeOf<PersonYear>() / 1e6;
            sb.Append($"{sim.People.Count} lines x {sim.PeoplePerLine.ToString("N0", Inv)} people, {y0}-{y1}: ")
                .Append($"{run.Store.Length.ToString("N0", Inv)} person-years ({recordsMb.ToString("0.0", Inv)} MB); ")
                .Append($"{totalMs} ms (traits {traitsMs}, setup {setupMs}, years {yearsMs - setupMs}:");
            for (int k = 0; k < run.PhaseTicks.Length; k++)
            {
                sb.Append(' ').Append(LivesSimulation.PhaseNames[k]).Append(' ')
                    .Append((run.PhaseTicks[k] * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0", Inv));
            }

            sb.Append($"; report {totalMs - traitsMs - yearsMs})\n");

            // calibration factors
            Range(run, y0, c => c.Wages, out double wLo, out double wHi);
            Range(run, y0, c => c.Business, out double bLo, out double bHi);
            Range(run, y0, c => c.Capital, out double kLo, out double kHi);
            Range(run, y0, c => c.Taxes, out double tLo, out double tHi);
            Range(run, y0, c => c.OtherTransfers, out double oLo, out double oHi);
            Range(run, y0, c => c.SocialSecurity, out double sLo, out double sHi);
            Range(run, y0, c => c.SavingShift * 100, out double shLo, out double shHi);
            Range(run, y0, c => c.EarningsMale, out double emLo, out double emHi);
            Range(run, y0, c => c.EarningsFemale, out double efLo, out double efHi);
            sb.Append($"calibration x(min..max over years): compensation {F2(wLo)}..{F2(wHi)}, business {F2(bLo)}..{F2(bHi)}, ")
                .Append($"capital {F2(kLo)}..{F2(kHi)}, taxes {F2(tLo)}..{F2(tHi)}, means-tested transfers {F2(oLo)}..{F2(oHi)} (per poor member, x a quarter of the poverty line), ")
                .Append($"social security {F2(sLo)}..{F2(sHi)}; saving-rate shift {shLo.ToString("+0.0;-0.0", Inv)}..")
                .Append($"{shHi.ToString("+0.0;-0.0", Inv)} pp; median earnings men {F2(emLo)}..{F2(emHi)}, women {F2(efLo)}..")
                .Append($"{F2(efHi)}\n");
            YearCalibration c25 = Cal(run, y0, Math.Min(2025, y1));
            if (c25 != null)
            {
                sb.Append("2025 category factors:");
                for (int c = 0; c < LivesInputs.Spend; c++)
                {
                    sb.Append(' ').Append(EconomyData.CategoryIds[c].Substring(0, 4)).Append(' ').Append(F2(c25.Categories[c]));
                }

                sb.Append($"; employment scale m {F2(c25.EmploymentScaleMale)} / w {F2(c25.EmploymentScaleFemale)}");
                sb.Append($"; transfers beyond SS+Medicare {Bn(c25.OtherTransfersTarget)}\n");
            }

            sb.Append("emergency transfers above the trend (2020 on, to households under 6.5 poverty lines):");
            int emergencies = 0;
            for (int i = 0; i < run.Calibration.Length; i++)
            {
                YearCalibration c = run.Calibration[i];
                if (c == null || c.StimulusTransfers <= 0) continue;
                sb.Append(' ').Append(y0 + i).Append(' ').Append(Bn(c.StimulusTransfers));
                emergencies++;
            }

            sb.Append(emergencies == 0 ? " none\n" : "\n");

            sb.Append("strategy offsets");
            for (int s = 0; s < 7; s++)
            {
                sb.Append(' ').Append(Short((PdStrategy)s)).Append(' ').Append(sampler.StrategyOffsets[s].ToString("+0.00;-0.00", Inv));
            }

            sb.Append($"; reason offset {sampler.ReasonOffset.ToString("+0.00;-0.00", Inv)} (adult sd {F2(sampler.ReasonSd)}); ");
            sb.Append($"in control: {controlNote}, target {Pct(inp.InControlTarget)} of adults in {Math.Min(inp.InControlYear, y1)}\n");

            // money by decade
            sb.Append("year adultsM | compensation $T (labor share x GDP) | proprietors | capital | transfers | taxes | outlays (PCE + ")
                .Append("interest) | saving | saving rate % (history) | homeowners % (history) | wealth top1 / top10 / bottom50 % (DFA; WID)\n");
            foreach (int year in TableYears)
            {
                if (year < y0 || year > y1) continue;
                PopulationYear a = lives.Aggregate(year);
                if (a == null) continue;
                double rate = a.Disposable > 0 ? a.Saving / a.Disposable : 0;
                string savingData = D1(inp.SavingRate, year), ownData = D1(inp.Homeownership, year);
                sb.Append(year).Append(' ').Append((a.AdultLines * sim.PeoplePerLine / 1e6).ToString("0.0", Inv).PadLeft(6))
                    .Append(" | ").Append(T(a.Wages)).Append(" (").Append(T(inp.CompensationTarget(year))).Append(')')
                    .Append(" | ").Append(T(a.Business)).Append(" | ").Append(T(a.CapitalIncome))
                    .Append(" | ").Append(T(a.Transfers)).Append(" | ").Append(T(a.Taxes))
                    .Append(" | ").Append(T(a.Spending)).Append(" | ").Append(T(a.Saving))
                    .Append(" | ").Append((100 * rate).ToString("0.0", Inv)).Append(" (").Append(savingData).Append(')')
                    .Append(" | ").Append((100 * a.Homeownership).ToString("0.0", Inv)).Append(" (").Append(ownData).Append(')')
                    .Append(" | ").Append(Shares(a.WealthShares[3], Dfa(inp, 3, year), D1(inp.Top1WealthWid, year)))
                    .Append(" / ").Append(Shares(a.WealthShares[2] + a.WealthShares[3], Dfa(inp, 2, year), D1(inp.Top10WealthWid, year)))
                    .Append(" / ").Append(Shares(a.WealthShares[0], Dfa(inp, 0, year), D1(inp.Bottom50WealthWid, year))).Append('\n');
            }

            // minds by decade
            sb.Append("year | in control % | fantasy | fear | reason | future | coop | strategies % TFT GTFT WSLS ALLC ALLD Grim Rand | ")
                .Append("tribes % D/R/I (data)\n");
            foreach (int year in TableYears)
            {
                if (year < y0 || year > y1) continue;
                PopulationYear a = lives.Aggregate(year);
                if (a == null) continue;
                sb.Append(year).Append(" | ").Append(P1(a.InControlShare).PadLeft(5))
                    .Append(" | ").Append(F2(a.MeanFantasy)).Append(" | ").Append(F2(a.MeanFear))
                    .Append(" | ").Append(F2(a.MeanReason)).Append(" | ").Append(F2(a.MeanFuture)).Append(" | ").Append(F2(a.MeanCooperation))
                    .Append(" |");
                for (int s = 0; s < 7; s++) sb.Append(' ').Append((100 * a.Strategies[s]).ToString("0", Inv));
                sb.Append(" | ").Append(P0(a.Parties[0])).Append('/').Append(P0(a.Parties[1])).Append('/').Append(P0(a.Parties[2]))
                    .Append(" (").Append(P0(inp.Dem.At(year))).Append('/').Append(P0(inp.Rep.At(year))).Append('/')
                    .Append(P0(inp.Ind.At(year))).Append(")\n");
            }

            // the circuit's year against its personal income
            int cy = inp.CircuitYear;
            PopulationYear ca = lives.Aggregate(cy);
            if (ca != null && inp.CircuitCompensation > 0)
            {
                double ssSum = 0, unit = sim.PeoplePerLine / 1e9;
                for (int i = 0; i < sim.People.Count; i++)
                {
                    if (lives.TryGet(i, cy, out PersonYear r)) ssSum += r.SocialSecurity * unit;
                }

                string rateData = LivesInputs.Rate(inp.SavingRate, cy, 0).ToString("0.0", Inv);
                sb.Append($"{cy} vs circuit.json personal ($T): compensation {T(ca.Wages)} ({T(inp.CircuitCompensation)}), ")
                    .Append($"proprietors {T(ca.Business)} ({T(inp.CircuitProprietors)}), ")
                    .Append($"capital {T(ca.CapitalIncome)} ({T(inp.CircuitCapital)}), transfers {T(ca.Transfers)} ")
                    .Append($"({T(inp.CircuitTransfers)}; social security {T(ssSum)} vs {T(inp.CircuitSocialSecurity)}), ")
                    .Append($"taxes {T(ca.Taxes)} ({T(inp.CircuitTaxes)}), disposable {T(ca.Disposable)} ({T(inp.CircuitDisposable)}), ")
                    .Append($"outlays {T(ca.Spending)} ({T(inp.CircuitOutlays)}; PCE {T(inp.CircuitPce)}), saving {T(ca.Saving)} ")
                    .Append($"({T(inp.CircuitSaving)} before BEA's Sept-2026 revision; history.json rate {rateData}%)\n");
            }

            // cross-checks of things the model was not calibrated to
            int checkYear = Math.Min(2025, y1);
            PopulationYear ck = lives.Aggregate(checkYear);
            if (ck != null)
            {
                Checks(sb, lives, inp, sim, checkYear, ck);
            }

            Money(sb, run, y0, checkYear);
            Thesis(sb, lives, inp, sim, traits, run, Math.Min(inp.InControlYear, y1));
            if (inp.Warnings.Count > 0)
            {
                sb.Append("data fallbacks: ").Append(string.Join("; ", inp.Warnings)).Append('\n');
            }

            sb.Append("notables:");
            foreach (Notable nb in lives.Notables) sb.Append(' ').Append(nb.Role).Append(" #").Append(nb.Person);
            return sb.ToString();
        }

        [MethodImpl(LivesMath.Hot)]
        static void Checks(StringBuilder sb, EconomicLives lives, LivesInputs inp, SmvSimulation sim, int year, PopulationYear a)
        {
            double debtRatio = a.Disposable > 0 ? a.Debt / a.Disposable : 0, credit = a.Disposable > 0 ? a.ConsumerDebt / a.Disposable : 0;
            sb.Append($"checks {year}: employed {Pct(a.EmploymentRate)} of adults, self-employed {Pct(a.SelfEmployedShare)} of them ")
                .Append($"(data {LivesInputs.Rate(inp.SelfEmployment, year, 0).ToString("0.0", Inv)}%), debt/DPI {Pct(debtRatio)} ")
                .Append($"(data {LivesInputs.Rate(inp.DebtToIncome, year, 0).ToString("0", Inv)}%), consumer debt/DPI {Pct(credit)} ")
                .Append($"(consumer credit {LivesInputs.Rate(inp.ConsumerCreditToIncome, year, 0).ToString("0", Inv)}%), ")
                .Append($"net worth {T(a.Wealth)}T (DFA {inp.DfaNetWorth().ToString("0", Inv)}T, 2026Q1, incl. ~8T of cars and furniture the ")
                .Append($"model does not hold), autonomy {Pct(a.AutonomyShare)} of adults ")
                .Append($"(anchors: runway 3y+ {Pct(inp.Target("runway_3y", 0.268))} of families, business owners ")
                .Append($"{Pct(inp.Target("business_owners", 0.13))}), saving 10%+ {Pct(a.SaverShare)} ({Pct(inp.Target("saving_10pct", 0.3))} of ")
                .Append($"households), large uninsured losses {P1(a.LossShare)}% of households ({T(a.Losses)}T), debt discharged ")
                .Append($"{P1(a.DischargeShare)}%\n");

            // ownership by age and wealth by age (households: the head's age; couples counted once)
            double[] ages = { 28, 39.5, 49.5, 59.5, 75 };
            int[] own = new int[5], hh = new int[5];
            List<double>[] worth = new List<double>[6];
            for (int k = 0; k < 6; k++) worth[k] = new List<double>();
            int nwYear = Math.Min(2022, lives.LastYear);
            double millionaires = 0, adults = 0;
            List<SmvPerson> people = sim.People;
            for (int i = 0; i < people.Count; i++)
            {
                if (lives.TryGet(i, year, out PersonYear r) && r.Adult)
                {
                    adults++;
                    if (r.Wealth * inp.Prices(2025, year) >= 1e6) millionaires++;
                    bool counted = !r.Married || !people[i].Male;
                    if (counted && r.OwnHousehold)
                    {
                        int band = r.Age < 35 ? 0 : r.Age < 45 ? 1 : r.Age < 55 ? 2 : r.Age < 65 ? 3 : 4;
                        hh[band]++;
                        if (r.Homeowner) own[band]++;
                    }
                }

                // SCF families: a household's primary economic unit (singles sharing a home with family are part of it)
                if (lives.TryGet(i, nwYear, out PersonYear w) && w.Adult && w.OwnHousehold && (!w.Married || !people[i].Male))
                {
                    double household = w.Married ? 2 * w.Wealth : w.Wealth;
                    int band = w.Age < 35 ? 0 : w.Age < 45 ? 1 : w.Age < 55 ? 2 : w.Age < 65 ? 3 : w.Age < 75 ? 4 : 5;
                    worth[band].Add(household);
                }
            }

            sb.Append($"homeownership by age {year} % (data):");
            string[] bands = { "<35", "35-44", "45-54", "55-64", "65+" };
            for (int k = 0; k < 5; k++)
            {
                sb.Append(' ').Append(bands[k]).Append(' ').Append(hh[k] > 0 ? (100.0 * own[k] / hh[k]).ToString("0", Inv) : "-")
                    .Append(" (").Append(LivesInputs.Rate(inp.HomeownershipByAge, ages[k], 0).ToString("0", Inv)).Append(')');
            }

            sb.Append($"; dollar millionaires {Pct(millionaires / Math.Max(1, adults))} of adults ({P1(inp.Target("millionaire_adults", 0.089))}%, UBS)\n");
            sb.Append($"median household net worth by age {nwYear} $K (SCF 2022, which leaves out the DFA's $18T of DB pensions):");
            double[] nwAges = { 28, 39.5, 49.5, 59.5, 69.5, 80 };
            string[] nwBands = { "<35", "35-44", "45-54", "55-64", "65-74", "75+" };
            for (int k = 0; k < 6; k++)
            {
                worth[k].Sort();
                double median = worth[k].Count > 0 ? worth[k][worth[k].Count / 2] : 0;
                sb.Append(' ').Append(nwBands[k]).Append(' ').Append((median / 1e3).ToString("0", Inv))
                    .Append(" (").Append((LivesInputs.Rate(inp.NetWorthByAge, nwAges[k], 0) / 1e3).ToString("0", Inv)).Append(')');
            }

            sb.Append('\n');
        }

        /// <summary>
        /// The money check: everyone's net worth may change only by saving, holding gains, business closures, debt
        /// discharged, balance sheets brought in and estates leaving the record; the largest unexplained yearly change
        /// shows whether any money was made or lost elsewhere (0 up to rounding).
        /// </summary>
        static void Money(StringBuilder sb, LivesSimulation run, int y0, int year)
        {
            if (run.MoneyNetWorth == null || run.MoneyNetWorth.Length == 0) return;
            double worst = 0;
            int worstYear = y0;
            for (int k = 1; k < run.MoneyNetWorth.Length; k++)
            {
                double r = Math.Abs(run.MoneyResidual(k));
                if (r <= worst) continue;
                worst = r;
                worstYear = y0 + k;
            }

            int c = Math.Max(0, Math.Min(run.MoneyNetWorth.Length - 1, year - y0));
            sb.Append($"money check: net worth moves only by saving, holding gains, closures, discharges, arrivals and estates; ")
                .Append($"largest unexplained change ${worst.ToString("0.0", Inv)}B ({worstYear}); {y0 + c}: net worth ")
                .Append($"{T(run.MoneyNetWorth[c])}T, saving {T(run.MoneySaving[c])}T, holding gains {T(run.MoneyGains[c])}T, ")
                .Append($"business closures -{T(run.MoneyClosures[c])}T, debt discharged +{T(run.MoneyDischarged[c])}T, arrivals ")
                .Append($"+{T(run.MoneyArrivals[c])}T, estates {T(run.MoneyEstates[c])}T\n");
        }

        /// <summary>
        /// The thesis tested: who is in control and how they differ, whether fantasy is bought more by the poor or the
        /// rich, and how the share in control moved over the decades (with the cut fixed in the target year).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        static void Thesis(StringBuilder sb, EconomicLives lives, LivesInputs inp, SmvSimulation sim, PersonTraits[] traits,
            LivesSimulation run, int year)
        {
            List<SmvPerson> people = sim.People;
            double[] sum = new double[2 * 8];
            int[] count = new int[2];
            double[] fantasyQ = new double[5], spendQ = new double[5], transfersG = new double[4];
            for (int i = 0; i < people.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                int g = r.InControl ? 1 : 0, o = g * 8;
                count[g]++;
                sum[o] += r.Fantasy;
                sum[o + 1] += r.Reason;
                sum[o + 2] += traits[i].ParentRank;
                sum[o + 3] += r.Age;
                sum[o + 4] += lives.InheritedFromParentsBy(i, year) ? 1 : 0;
                sum[o + 5] += r.FearShare;
                sum[o + 6] += r.SelfEmployed ? 1 : 0;
                sum[o + 7] += r.Cooperation;
                int q = Math.Min(4, (int)(r.IncomeRank * 5));
                fantasyQ[q] += r.Spending * r.Fantasy;
                spendQ[q] += r.Spending;
                transfersG[Math.Min(3, (int)r.WealthGroup)] += r.Transfers;
            }

            string Mean(int g, int k) => count[g] > 0 ? (sum[g * 8 + k] / count[g]).ToString(k == 3 ? "0" : "0.00", Inv) : "-";
            sb.Append($"thesis {year}: in control vs the rest: fantasy {Mean(1, 0)} vs {Mean(0, 0)}, reason {Mean(1, 1)} vs {Mean(0, 1)}, ")
                .Append($"parent's rank {Mean(1, 2)} vs {Mean(0, 2)}, age {Mean(1, 3)} vs {Mean(0, 3)}, inherited from parents ")
                .Append($"{Mean(1, 4)} vs {Mean(0, 4)}, fear share {Mean(1, 5)} vs {Mean(0, 5)}, self-employed {Mean(1, 6)} vs {Mean(0, 6)}, ")
                .Append($"cooperation {Mean(1, 7)} vs {Mean(0, 7)}\n");
            sb.Append($"fantasy share of spending {F2(lives.Aggregate(year)?.MeanFantasy ?? 0)} ({F2(inp.Target("fantasy_weighted_spending", 0.185))}); ")
                .Append("by income quintile (psyche.json fantasy_weighted_spending note: 0.12 0.12 0.15 0.18 0.23):");
            for (int q = 0; q < 5; q++) sb.Append(' ').Append(F2(spendQ[q] > 0 ? fantasyQ[q] / spendQ[q] : 0));
            double transfers = transfersG[0] + transfersG[1] + transfersG[2] + transfersG[3];
            sb.Append("\ntransfers by wealth group, bottom 50% / 50-90% / 90-99% / top 1% (DFA):");
            for (int g = 0; g < 4; g++)
            {
                double data = g < inp.Data.Groups.Count ? inp.Data.Groups[g].TransferShare : double.NaN;
                sb.Append(g > 0 ? " /" : "").Append(' ').Append(P1(transfers > 0 ? transfersG[g] / transfers : 0))
                    .Append(" (").Append(P1(data)).Append(')');
            }

            sb.Append("\nin control % by year (cut fixed in ").Append(year).Append("):");
            for (int y = 1950; y <= lives.LastYear; y += 5)
            {
                PopulationYear a = lives.Aggregate(y);
                if (a != null) sb.Append(' ').Append(y).Append(' ').Append(P1(a.InControlShare));
            }

            PopulationYear last = lives.Aggregate(lives.LastYear);
            if (last != null && lives.LastYear % 5 != 0) sb.Append(' ').Append(lives.LastYear).Append(' ').Append(P1(last.InControlShare));
            sb.Append($"; estates without heirs {Bn(run.UnclaimedEstates)} over the record\n");

            // mobility: earnings rank against the parent's (people with a parent in the population)
            double sx = 0, sy = 0, sxx = 0, sxy = 0, bottom = 0, bottomTop = 0;
            int pairs = 0;
            for (int i = 0; i < people.Count; i++)
            {
                SmvPerson p = people[i];
                if ((p.Mother < 0 || p.Mother >= i) && (p.Father < 0 || p.Father >= i)) continue;
                double x = traits[i].ParentRank, y = traits[i].Rank;
                sx += x;
                sy += y;
                sxx += x * x;
                sxy += x * y;
                pairs++;
                if (x >= 0.2) continue;
                bottom++;
                if (y >= 0.8) bottomTop++;
            }

            if (pairs > 1)
            {
                double slope = (sxy - sx * sy / pairs) / Math.Max(1e-9, sxx - sx * sx / pairs);
                sb.Append($"mobility: rank-rank slope {F2(slope)} (Chetty et al. 0.34), children of the bottom fifth reaching the top fifth ")
                    .Append($"{P1(bottomTop / Math.Max(1, bottom))}% ({P1(inp.Target("structure_bottom_to_top", 0.075))}%), over {pairs} children ")
                    .Append("with a parent in the population\n");
            }
        }

        // ================================================================ formatting

        static YearCalibration Cal(LivesSimulation run, int y0, int year)
        {
            int k = year - y0;
            return k >= 0 && k < run.Calibration.Length ? run.Calibration[k] : null;
        }

        static void Range(LivesSimulation run, int y0, Func<YearCalibration, double> f, out double lo, out double hi)
        {
            lo = double.MaxValue;
            hi = double.MinValue;
            foreach (YearCalibration c in run.Calibration)
            {
                if (c == null) continue;
                double v = f(c);
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
            }

            if (lo > hi) lo = hi = 0;
        }

        static string Short(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "TFT";
                case PdStrategy.GenerousTitForTat: return "GTFT";
                case PdStrategy.WinStayLoseShift: return "WSLS";
                case PdStrategy.AlwaysCooperate: return "ALLC";
                case PdStrategy.AlwaysDefect: return "ALLD";
                case PdStrategy.Grim: return "Grim";
                default: return "Rand";
            }
        }

        static string F2(double v) => v.ToString("0.00", Inv);
        static string T(double billions) => (billions / 1000).ToString("0.00", Inv);
        static string Bn(double billions) => "$" + billions.ToString("0", Inv) + "B";
        static string P0(double share) => (100 * share).ToString("0", Inv);
        static string P1(double share) => (100 * share).ToString("0.0", Inv);
        static string Pct(double share) => (100 * share).ToString("0", Inv) + "%";

        static string D1(Why.Economy.Data.YearSeries s, int year) => s.IsEmpty ? "-" : s.At(year).ToString("0.0", Inv);

        /// <summary>A simulated share (percent) with its data in parentheses: "31.7 (31.4; 34.8)".</summary>
        static string Shares(double sim, string dfa, string wid) => P1(sim) + " (" + dfa + "; " + wid + ")";

        static string Dfa(LivesInputs inp, int group, int year)
        {
            double v = inp.DfaShare(group, year);
            return v < 0 ? "-" : v.ToString("0.0", Inv);
        }

        /// <summary>Dollars in words-sized units: $850, $85K, $1.2M, $3.4B.</summary>
        public static string Money(double v)
        {
            double a = Math.Abs(v);
            string sign = v < 0 ? "-" : "";
            if (a >= 1e9) return sign + "$" + (a / 1e9).ToString("0.0", Inv) + "B";
            if (a >= 1e6) return sign + "$" + (a / 1e6).ToString("0.0", Inv) + "M";
            if (a >= 1e3) return sign + "$" + (a / 1e3).ToString("0", Inv) + "K";
            return sign + "$" + a.ToString("0", Inv);
        }
    }
}
