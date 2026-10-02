using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    // The contract types of the land: one year of the economy cut out of the road and opened into a stepped bowl
    // (Docs/ECONOMY.md, "The landscape"). Builders (LandLayout, PlayerCensus, MoneyRouting, SocialSeason) fill them on a
    // worker from the year's data; LandService publishes them as a LandSnapshot; the land layers only read them.
    // Dollars are $B nominal of the snapshot's year unless a member says otherwise; angles are degrees counter-clockwise
    // seen from above, 270 facing the road (LandFrame); positions are land-local (LandFrame).

    /// <summary>The five strata of the bowl, from the floor up: government, raw, make, services, tech.</summary>
    public enum Tier : byte { Gov = 0, Raw = 1, Make = 2, Services = 3, Tech = 4 }

    /// <summary>The twelve 2026 socioeconomic groups, in groups.json order.</summary>
    public enum Group : byte
    {
        Top1 = 0, Owners = 1, Gig = 2, Pmc = 3, Public = 4, Office = 5, Frontline = 6, WorkingPoor = 7,
        RetiredSavings = 8, RetiredSocialSecurity = 9, Students = 10, OutOfWork = 11
    }

    /// <summary>Where a player stands: on the rim, on top of a corporation's tower (controllers), on the crown (rentiers).</summary>
    public enum Place : byte { Rim = 0, Tower = 1, Crown = 2 }

    /// <summary>
    /// What a drawn flow carries. Income and capital move in the air (arcs); spending, taxes and imports run on the
    /// ground (rivulets, the canal, rivers); purchases between industries are the roots (RootsModel, not a FlowPath).
    /// </summary>
    public enum FlowKind : byte
    {
        Wages = 0, WagesCompany = 1, Business = 2, Capital = 3, Transfers = 4,                      // income, in the air
        Payout = 5, PayoutAbroad = 6, Saving = 7, Investment = 8, Credit = 9, Borrowing = 10,      // capital, in the air
        Rivulet = 11, River = 12, Distributary = 13, Waterfall = 14, Taxes = 15, Abroad = 16       // on the ground
    }

    /// <summary>Emphasis groups: rows of the EconomyViews table (7.2). Every land renderer belongs to one.</summary>
    public enum LandGroup : byte
    {
        Road = 0, Cut, Terraces, Sectors, Pools, Roots, Towers, Crown, Overlays, Glyphs, Dots, Mirages,
        Income, CapitalFlows, Rivers, Glitter, Taxes, Ties, Coalitions, Count
    }

    /// <summary>
    /// One industry's sector of its ring in a year: an arc of the ring (a wedge of the floor disc for government) whose
    /// area is the industry's value added at <see cref="LandStyle.AreaGdp"/>, split radially into wages (inner), upkeep
    /// and the owners' share (outer) at area-true radii.
    /// </summary>
    public sealed class SectorGeom
    {
        /// <summary>Industry index (EconomyData.Industries) and its tier (ring).</summary>
        public int Industry; public Tier Tier;

        /// <summary>The sector's angular extent.</summary>
        public float Theta0, Theta1;             // degrees, counter-clockwise, Theta1 > Theta0 (may exceed 360)

        /// <summary>Inner and outer radius of the ring, and the height of its tread.</summary>
        public float R0, R1, Y;                  // radii (gov wedges: R0 = 0) and tread height

        /// <summary>Outer radius of the wages strip and of the upkeep strip (the owners' strip runs from RUpkeep to R1).</summary>
        public float RWages, RUpkeep;            // area-true strip boundaries

        /// <summary>
        /// The angular extent of the sector's tower run when its towers need more arc than the sector has (it includes
        /// the sector's own arc; the plinth is drawn where it lies beyond the sector); NaN when the towers fit.
        /// </summary>
        public float Plinth0 = float.NaN, Plinth1 = float.NaN;   // tower plinth arc beyond the sector (degrees)

        /// <summary>Value added and its split (WallGeometry.Split), the year's.</summary>
        public double ValueAdded, Wages, Upkeep, Owners;         // $B nominal, the year's

        /// <summary>Center angle (degrees).</summary>
        public float Mid => 0.5f * (Theta0 + Theta1);
    }

    /// <summary>
    /// One of the 25 most valuable companies (circuit.json capture) as a gold tower on its sector's owners' strip:
    /// height = market value, square cross-section = net income, foot ring = revenue (2.6). Only for years from 2024.
    /// </summary>
    public sealed class TowerGeom
    {
        /// <summary>Index in EconomyData.Circuit.Capture (also the tower's index in LandGeometry.Towers) and its industry.</summary>
        public int Company, Industry;            // index in EconomyData capture list; industry index

        /// <summary>Ticker (empty for a private company) and name.</summary>
        public string Ticker, Name;

        /// <summary>
        /// Foot center (polar: degrees, radius, tread height), height above the tread, side of the square, radius of the
        /// foot ring, and the net margin (net income / revenue).
        /// </summary>
        public float Theta, R, BaseY, Height, Side, FootR, Margin;

        /// <summary>Stands on its sector's plinth (the sector's towers need more arc than it has); private (no accounts).</summary>
        public bool OnPlinth, Private;           // Private: no revenue or net income (SpaceX)

        /// <summary>The company's figures (worldwide, the data's year).</summary>
        public double MarketCap, Revenue, NetIncome;    // $B

        /// <summary>Height of the tower's top square.</summary>
        public float TopY => BaseY + Height;
    }

    /// <summary>The year's bowl: sectors, towers, ring fills and the fixed ring order (LandLayout.Build).</summary>
    public sealed class LandGeometry
    {
        /// <summary>Calendar year and its GDP (the sum of the industries' value added, so the sectors fill 24 u² exactly).</summary>
        public int Year; public double Gdp;                             // $B nominal

        /// <summary>The year's scales: area, flow width, root width and height per $B (0.1's constants over Gdp).</summary>
        public float AreaPerB, WidthPerB, RootWidthPerB, HeightPerB;    // world units per $B, the year's

        /// <summary>One sector per industry.</summary>
        public SectorGeom[] Sectors;                                    // 25, EconomyData.Industries order

        /// <summary>Towers by capture index.</summary>
        public TowerGeom[] Towers;                                      // empty when Year < 2024

        /// <summary>Share of each ring's circumference its sectors fill (gaps excluded): ≤ 1.</summary>
        public float[] RingFill = new float[5];

        /// <summary>The run center of each ring.</summary>
        public float[] RingOffset = new float[5];                       // run centers, degrees (fixed for all years)

        /// <summary>Each ring's industries in the order they are laid.</summary>
        public int[][] RingOrder;                                       // industries by increasing angle (fixed)

        /// <summary>The 2024 layout cost ($B × radians), the body of the "[Why] Land" log line, Σ sector mid angles.</summary>
        public double LayoutCost; public string Log; public double Checksum;
    }

    /// <summary>
    /// A player: the people of one cell of group × anchor × party (3.1), with their money, their mind and their place.
    /// </summary>
    public sealed class Player
    {
        /// <summary>Index in PlayerSet.Players (sorted by group, anchor, tribe) and the stable key.</summary>
        public int Index; public string Key;                            // "frontline|trade|R"; stable across years

        /// <summary>The socioeconomic group and its class rung (0..5).</summary>
        public Group Group; public int Rung;

        /// <summary>The industry that pays the player.</summary>
        public int Anchor = -1;                                         // industry index, -1 for pseudo-anchors

        /// <summary>The anchor's id.</summary>
        public string AnchorId;                                         // industry id, "tier:<id>", "rest", "capital", "pensions", ...

        /// <summary>The sector whose center angle the player's place starts from.</summary>
        public int AngleIndustry = -1;                                  // the sector whose angle the player takes

        /// <summary>Party label and the members' party shares.</summary>
        public char Tribe;                                              // 'D', 'R', 'I', 'M'

        /// <summary>The adults' party shares: Democrat, Republican, independent.</summary>
        public readonly float[] TribeShares = new float[3];             // D, R, I

        /// <summary>The adults' generation shares.</summary>
        public readonly float[] Generations = new float[5];             // Silent .. Gen Z

        /// <summary>The member lines (each 100,000 people).</summary>
        public int[] Adults, Children;                                  // person indices, ascending

        /// <summary>The members' money in the year.</summary>
        public double Wages, Business, Capital, Transfers, SocialSecurity, Taxes, Spending, Saving, Wealth, Debt;   // $B

        /// <summary>Wages and business income by the industry that pays them.</summary>
        public readonly double[] WagesBy = new double[25], BusinessBy = new double[25];

        /// <summary>Spending by the six categories, and its fear and fantasy dollars.</summary>
        public readonly double[] Category = new double[6];             // necessities, escapism, jeopardy, status, growth, collective

        /// <summary>Of each category's dollars, those spent moving away from a fear, and those that buy a fantasy ($B).</summary>
        public readonly double[] CategoryFear = new double[6], CategoryFantasy = new double[6];

        /// <summary>The adults' means: the mind (6), in-control share, married and with-children shares, age.</summary>
        public float Fear, Fantasy, Reason, HigherOs, Future, Agency, InControl, Married, WithKids, Age;

        /// <summary>The members' strategy mix (inspector only; every player plays tit for tat).</summary>
        public readonly float[] Strategy = new float[7];                // members' PdStrategy shares

        /// <summary>Where the player stands, and the tower when it stands on one.</summary>
        public Place Place; public int Tower = -1;                      // capture index when Place == Tower

        /// <summary>The disc's center (land polar: degrees, radius, height) and its rim row.</summary>
        public float Theta, R, Y; public int Row = -1;                  // disc center (land polar); rim row 0..3

        /// <summary>The disc's radius (Figure.DiscRadius) and whether it stands on a gold plinth.</summary>
        public float Radius; public bool Plinth;

        /// <summary>People the player stands for (adults and children).</summary>
        public double People => (Adults.Length + Children.Length) * 1e5;
    }

    /// <summary>The year's players (PlayerCensus.Build).</summary>
    public sealed class PlayerSet
    {
        /// <summary>Year, the players by index, and each person's player.</summary>
        public int Year; public Player[] Players; public int[] PlayerOfPerson;   // person index -> player, -1

        /// <summary>
        /// The body of the "[Why] Players" log line (8.8) and Σ adults × index. After "self-employed in government n" the
        /// body carries each group's share of the year's adults (adult lines, %, one decimal, in group order with the
        /// line's short names): "; shares 1% 1.0, owners 4.1, gig 2.8, PMC 19.7, public 4.3, office 6.9, frontline 21.6,
        /// poor 6.5, comf.ret 11.0, ss.ret 11.4, students 3.7, out 7.1; checks 3/3 PASS; checksum ..." (landcheck compares
        /// them with 3.2 within 0.5 pt).
        /// </summary>
        public string Log; public double Checksum;
    }

    /// <summary>One drawn flow, land-local, with its dollars split by motive (spending) or plain (the rest).</summary>
    public sealed class FlowPath
    {
        /// <summary>What it carries, and its canal lane.</summary>
        public FlowKind Kind; public int Lane = -1;                     // 0-5 categories, 6 taxes, 7 abroad; -1 otherwise

        /// <summary>Where it starts and ends.</summary>
        public int From = -1, To = -1;                                  // player, industry or company; -2 crown, -3 abroad, -4 floor

        /// <summary>The path, land-local.</summary>
        public Vector3[] Points;

        /// <summary>Its dollars, and for spending the fear, desire (= dollars − fear) and fantasy parts.</summary>
        public double Dollars, Fear, Desire, Fantasy;                   // $B

        /// <summary>Drawn dashed (borrowing).</summary>
        public bool Dashed;
    }

    /// <summary>One lane of the lip canal, sampled at every whole degree.</summary>
    public sealed class CanalLane
    {
        /// <summary>Lane index (0-5 categories, 6 taxes, 7 abroad) and the angle where it leaves the canal.</summary>
        public int Lane; public float Exit;                              // degrees

        /// <summary>Inner and outer radius at each degree.</summary>
        public readonly float[] R0 = new float[360], R1 = new float[360];

        /// <summary>The lane's flow at each degree, by motive (spending) or plain (taxes, abroad).</summary>
        public readonly double[] Fear = new double[360], Desire = new double[360], Fantasy = new double[360], Plain = new double[360];

        /// <summary>Which way the water runs at each degree.</summary>
        public readonly sbyte[] Direction = new sbyte[360];             // +1 counter-clockwise, -1 clockwise, 0 divide
    }

    /// <summary>A player's wage patch on a sector's wage strip (or a tower's share when Company ≥ 0).</summary>
    public struct Patch { public int Industry, Player, Company; public float Theta0, Theta1, R0, R1; public double Dollars; }

    /// <summary>The year's money on the land (MoneyRouting.Build): every drawn flow, the canal, pools and the crown.</summary>
    public sealed class MoneyFlows
    {
        /// <summary>The calendar year the flows are of.</summary>
        public int Year;

        /// <summary>Every drawn flow.</summary>
        public readonly List<FlowPath> Paths = new List<FlowPath>();

        /// <summary>The lip canal's eight lanes.</summary>
        public CanalLane[] Canal = new CanalLane[8];

        /// <summary>The wage patches.</summary>
        public Patch[] Patches;

        /// <summary>Per industry: household spending it is paid directly (the first recipient), and its fear and fantasy parts.</summary>
        public readonly double[] PoolInflow = new double[25], PoolFear = new double[25], PoolFantasy = new double[25];

        /// <summary>Per industry: payouts into the crown, and investment out of it.</summary>
        public readonly double[] PayoutBySector = new double[25], InvestmentBySector = new double[25];

        /// <summary>The crown's other accounts, and household spending on imports.</summary>
        public double PayoutAbroad, SavingIn, Credit, Investment, Imports;

        /// <summary>Exit angle of each canal lane.</summary>
        public readonly float[] Fall = new float[8];                     // exit angles, degrees

        /// <summary>Share of each category's domestic spending first paid to each industry.</summary>
        public double[,] CategoryToSeller;                               // [6, 25], row-normalized (RAS)

        /// <summary>The body of the "[Why] Money" log line and Σ dollars of the paths.</summary>
        public string Log; public double Checksum;
    }

    /// <summary>The season's settings (the social panel; EconomyState.Social).</summary>
    public struct SocialSettings
    {
        /// <summary>Mistakes ε, the shadow of the future w, the polarization factor, partner choice, and who forgives.</summary>
        public float Noise, Continuation, Polarization; public bool Rewire; public byte Forgive;   // 0 by the OS, 1 everyone, 2 nobody

        /// <summary>The defaults of 5.2 (games.json social).</summary>
        public static SocialSettings Default => new SocialSettings
            { Noise = 0.02f, Continuation = 0.95f, Polarization = 1f, Rewire = true, Forgive = 0 };
    }

    /// <summary>One betrayal: in round Round, player From defects on player To.</summary>
    public struct Incident { public int Round, From, To; }              // From betrays To (player indices)

    /// <summary>A season of tit for tat between the players (SocialSeason.Run).</summary>
    public sealed class SocialSeasonResult
    {
        /// <summary>Year, rounds, the settings it ran with and its incident.</summary>
        public int Year, Rounds = 96; public SocialSettings Settings; public Incident? Incident;

        /// <summary>The pairs.</summary>
        public int[] PairA, PairB;                                       // undirected pairs, A < B, index order

        /// <summary>Cooperation each way, every round.</summary>
        public float[][] CoopAB, CoopBA;                                 // [round 0..96][pair]

        /// <summary>Dealings between the pair, every round.</summary>
        public float[][] Exposure;                                       // [round][pair]: Ec(A,B) + Ec(B,A)

        /// <summary>The openings.</summary>
        public float[][] Opening;                                        // [2][pair]: c(A,B), c(B,A)

        /// <summary>Coalition of every player at each detection.</summary>
        public int[][] CoalitionAt;                                      // [detection 0..4][player], rounds 0,24,48,72,96; -1 none

        /// <summary>Standing at the last round.</summary>
        public byte[] Standing;                                          // [player]: 0 beta, 1 alpha, 2 omega, 3 anti-alpha

        /// <summary>Signals: pain, relief, satisfaction.</summary>
        public readonly List<(int round, int pair, byte signal)> Events = new List<(int, int, byte)>();   // 0 pain 1 relief 2 satisfaction

        /// <summary>The season's readouts per round (5.3).</summary>
        public float[] Cooperation, SameGroup, OtherGroup, CoPartisan, CrossPartisan;   // [round]

        /// <summary>A betrayal season's outcome measured against the season without the incident (5.4).</summary>
        public int Hit = -1, CalmAfter = -1;                            // betrayal outcome against the baseline

        /// <summary>The body of the "[Why] Society" log line and Σ cooperation.</summary>
        public string Log; public double Checksum;
    }

    /// <summary>Everything the land shows for one year, built together and published by LandService.</summary>
    public sealed class LandSnapshot
    {
        /// <summary>Year, the publication's version (LandService.Version) and whether it was built blocking (by a preset).</summary>
        public int Year, Version; public bool Blocking;

        /// <summary>The bowl's geometry, the players and the money of the year.</summary>
        public LandGeometry Land; public PlayerSet Players; public MoneyFlows Money;

        /// <summary>The year's season of tit for tat, and the same season with the round-48 betrayal (computed lazily by
        /// LandService.Betrayal; null until then).</summary>
        public SocialSeasonResult Society, Betrayal;

        /// <summary>The year's national accounts (MoneyCircuit.Build(Year)), for totals and labels.</summary>
        public CircuitYear Circuit;

        /// <summary>The checks of every builder on one line (8.8).</summary>
        public string ChecksLine;
    }
}
