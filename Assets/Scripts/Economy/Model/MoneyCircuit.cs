using System;
using System.Collections.Generic;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>The columns of the money circuit, left to right (the return arcs close the loop to the first).</summary>
    public enum CircuitColumn
    {
        /// <summary>Industries (one node per tier of the wall) and the value they add.</summary>
        Industries = 0,

        /// <summary>How that value is split: wages, owners' share, production taxes, depreciation.</summary>
        Income = 1,

        /// <summary>Who receives it: the four wealth groups, the government, holders abroad, reinvestment.</summary>
        Recipients = 2,

        /// <summary>The four wealth groups again after taxes and transfers: what households have to spend.</summary>
        Households = 3,

        /// <summary>What the money is used for: the seven categories, public goods, investment.</summary>
        Uses = 4
    }

    /// <summary>A node of the circuit: a sum of money in a year ($B, nominal).</summary>
    public sealed class CircuitNode
    {
        public string Id;
        public string Name;
        public CircuitColumn Column;

        /// <summary>Position in the node list (link endpoints refer to it).</summary>
        public int Index;

        /// <summary>Money through the node in the year, $B nominal.</summary>
        public double Value;

        /// <summary>
        /// What the node is: "tier", "wages", "owners", "taxes", "depreciation", "group", "government", "abroad", "reinvest",
        /// "category", "public", "investment", "world".
        /// </summary>
        public string Kind;

        /// <summary>Hue: "capital", "gov", "matter", "life", "people", "desire", "fear", "neutral".</summary>
        public string Level = "neutral";

        /// <summary>For spending nodes: share of the motive that is fear (0..1), -1 otherwise.</summary>
        public float FearShare = -1;

        /// <summary>One or two sentences with the year's numbers for the tooltip.</summary>
        public string Blurb;
    }

    /// <summary>A flow of money between two nodes in a year ($B, nominal).</summary>
    public sealed class CircuitLink
    {
        public int From, To;
        public double Value;

        /// <summary>
        /// What the money is: "value" (industry -> income type), "wages", "payout", "rent", "interest", "business", "retained",
        /// "tax", "transfer", "keep" (recipient -> household), "spend", "save", "public", "invest", "return" (use -> industry).
        /// </summary>
        public string Kind;

        /// <summary>For spending and return links: share of the motive that is fear (0..1), -1 otherwise.</summary>
        public float FearShare = -1;

        /// <summary>True for the arcs that carry money from its uses back into the industries (closing the loop).</summary>
        public bool Return;
    }

    /// <summary>The circuit of one year: nodes by column, links between them, and how well it balances.</summary>
    public sealed class CircuitYear
    {
        public int Year;
        public readonly List<CircuitNode> Nodes = new List<CircuitNode>();
        public readonly List<CircuitLink> Links = new List<CircuitLink>();

        /// <summary>GDP of the year ($B nominal): the size of the first column.</summary>
        public double Gdp;

        /// <summary>Largest gap between what enters and leaves a node, as a share of GDP (0 = balanced).</summary>
        public double Imbalance;

        /// <summary>How the year was derived (calibration year, scaled history, approximations).</summary>
        public string Notes;

        public CircuitNode Node(string id)
        {
            foreach (CircuitNode n in Nodes)
            {
                if (n.Id == id) return n;
            }

            return null;
        }

        public IEnumerable<CircuitNode> InColumn(CircuitColumn c)
        {
            foreach (CircuitNode n in Nodes)
            {
                if (n.Column == c) yield return n;
            }
        }
    }

    /// <summary>
    /// The money circuit: for any year since 1947, how the value industries add is paid out (wages, profits, taxes),
    /// who receives it (wealth groups, the state, holders abroad), what households have after taxes and transfers, what
    /// they spend it on (the notebook's categories) and which industries the spending returns to. Calibrated on the
    /// latest year of circuit.json and carried back in time with the industries' value added, the labor share, the
    /// wealth groups' history and the spending history. Years are built on demand and cached; thread safe.
    /// </summary>
    public sealed class MoneyCircuit
    {
        readonly EconomyData data;
        readonly Dictionary<int, CircuitYear> cache = new Dictionary<int, CircuitYear>();
        readonly object gate = new object();

        public MoneyCircuit(EconomyData data)
        {
            this.data = data;
        }

        public EconomyData Data => data;

        /// <summary>The circuit of a calendar year (clamped to the data's years).</summary>
        public CircuitYear Build(int year)
        {
            year = Math.Max(data.FirstYear, Math.Min(data.LastYear, year));
            lock (gate)
            {
                if (cache.TryGetValue(year, out CircuitYear cached)) return cached;
            }

            CircuitYear built = Compute(year);
            lock (gate) cache[year] = built;
            return built;
        }

        CircuitYear Compute(int year)
        {
            // TODO(model): the full circuit (see the design notes); until then the industries column alone
            CircuitYear c = new CircuitYear { Year = year, Gdp = data.Gdp.GrowthAt(year), Notes = "industries only" };
            foreach (Tier t in data.Tiers)
            {
                double v = 0;
                foreach (Industry i in data.Industries)
                {
                    if (i.TierId == t.Id) v += i.ValueAdded.GrowthAt(year);
                }

                c.Nodes.Add(new CircuitNode
                {
                    Id = "tier:" + t.Id, Name = t.Name, Column = CircuitColumn.Industries, Index = c.Nodes.Count,
                    Value = v, Kind = "tier", Level = t.Id == "gov" ? "gov" : "capital"
                });
            }

            return c;
        }
    }
}
