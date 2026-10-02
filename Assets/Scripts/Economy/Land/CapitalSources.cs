using System;
using System.Collections.Generic;
using Why.Economy.Data;
using WealthGroup = Why.Economy.Data.Group;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where capital income comes from (SPEC 2.7), per wealth group (bottom 50%, next 40%, next 9%, top 1%): dividends
    /// from every private industry by what its owners keep, rent from real estate, interest from banking. Each group's mix
    /// is its share of the nation's dividends, rent and interest (circuit.json groups, the Fed's distributional accounts)
    /// times their sizes in the national accounts. The rule moved unchanged from the money threads
    /// (MoneyThreadsLayer.CapitalSources), which drew one industry per person from it; the land sums it instead: the
    /// crown's payouts by sector are every player's capital income spread by its members' groups' mixes. Pure; any thread.
    /// </summary>
    public static class CapitalSources
    {
        /// <summary>The wealth groups (EconomyData.GroupIds order: bottom50, next40, next9, top1).</summary>
        public const int Groups = 4;

        /// <summary>
        /// Each wealth group's mix over the industries in a year: weights[g][i] ≥ 0 summing to 1 (all equity, spread by
        /// owners' dollars, when a group's shares are missing).
        /// </summary>
        public static double[][] Mix(EconomyData data, int year)
        {
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count;
            int realEstate = data.IndustryById("real_estate")?.Index ?? -1;
            int banking = data.IndustryById("banking")?.Index ?? -1;
            double[] owners = new double[n];
            double equityTotal = 0;
            for (int i = 0; i < n; i++)
            {
                if (inds[i].TierId == "gov" || i == realEstate) continue;
                owners[i] = Math.Max(0, inds[i].ValueAdded.GrowthAt(year)) * inds[i].OwnersShare;
                equityTotal += owners[i];
            }

            double dividends = data.Circuit?.IncomeOf("dividends") ?? 0, rent = data.Circuit?.IncomeOf("rental") ?? 0;
            double interest = data.Circuit?.IncomeOf("netInterest") ?? 0;
            double[][] mix = new double[Groups][];
            for (int g = 0; g < Groups; g++)
            {
                WealthGroup group = g < data.Groups.Count ? data.Groups[g] : null;
                double eq = (group?.EquityShare ?? 1) * dividends;
                double re = realEstate >= 0 ? (group?.RealEstateShare ?? 0) * rent : 0;
                double it = banking >= 0 ? (group?.InterestShare ?? 0) * interest : 0;
                double sum = eq + re + it;
                if (sum <= 0 || equityTotal <= 0)
                {
                    eq = 1;
                    re = it = 0;
                    sum = 1;
                }

                double[] w = new double[n];
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    w[i] = equityTotal > 0 ? eq / sum * owners[i] / equityTotal : 0;
                    if (i == realEstate) w[i] += re / sum;
                    if (i == banking) w[i] += it / sum;
                    total += w[i];
                }

                for (int i = 0; i < n && total > 0; i++) w[i] /= total;
                mix[g] = w;
            }

            return mix;
        }

        /// <summary>
        /// The share of a wealth group's capital income that is equity (dividends): the part of its mix spread over the
        /// industries by owners' dollars, the base of the ownership fan (7.5).
        /// </summary>
        public static double EquityFraction(EconomyData data, int group)
        {
            WealthGroup g = group >= 0 && group < data.Groups.Count ? data.Groups[group] : null;
            double eq = (g?.EquityShare ?? 1) * (data.Circuit?.IncomeOf("dividends") ?? 0);
            double re = (g?.RealEstateShare ?? 0) * (data.Circuit?.IncomeOf("rental") ?? 0);
            double it = (g?.InterestShare ?? 0) * (data.Circuit?.IncomeOf("netInterest") ?? 0);
            double sum = eq + re + it;
            return sum > 0 ? eq / sum : 1;
        }
    }
}
