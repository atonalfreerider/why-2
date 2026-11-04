using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>
    /// The world around the United States for M2, "One economy among many" (MODALITIES.md section 6): global.json
    /// (Tools/global/build.py; Docs/data/global.md) read into the 13 economies, Germany inside the EU27 (never added to it),
    /// the rest of the world and the world; the goods lanes between the 13 (one record per lane, the exporter's, with the
    /// importer's as its mirror); the dated events and 2026 values with the 2026 gate's verdicts; PIIE's tariff averages,
    /// the bilateral series behind the sanction cuts, Treasuries, keystones, straits and the shares of world output since
    /// 1950 on both bases. On top of the data the model decides:
    /// <list type="bullet">
    /// <item>the west-to-east order, recomputed from the population-weighted centroid longitudes (the file's order is
    /// checked against it), the rest of the world at the east end;</item>
    /// <item>the lane sets: the default (every US pair and the <see cref="DefaultOtherPairs"/> largest other pairs) and "All
    /// flows", each with the share of world goods exports outside intra-EU trade it carries;</item>
    /// <item>what may be drawn: an item the build drew (fail closed: never one it did not), verified, re-read on or after
    /// the as-of date when it is dated 2026 (by its own dates, whatever a flag says), with its gate item passing too, and
    /// for an event, not past its stated end or expiry date without a verified outcome; a copy dated 2026 that carries no
    /// verdict is never drawn. Every date compared is the data's own (<see cref="AsOf"/>, the date of the gate's last re-reading): the
    /// model never reads the clock, so a player run after an expiry shows the world as of its data, stamped "as of",
    /// until global.json is rebuilt (the build then drops what expired without an outcome).</item>
    /// </list>
    /// The geometry in the stage plane is <see cref="WorldLayout"/>'s (<see cref="Layout"/> at market rates,
    /// <see cref="PppLayout"/> at purchasing power). <see cref="Problems"/> lists what breaks the spec's rules (section 6.7);
    /// the model publishes its numbers under <c>world.*</c> (<see cref="ModelFacts"/>). Pure and immutable once built:
    /// parses on any thread, no Unity objects; <see cref="Provide"/> builds the shared one once per run.
    /// </summary>
    public sealed class WorldModel
    {
        /// <summary>The data file (Resources path).</summary>
        public const string Path = "Data/economy/global";

        /// <summary>The 13 economies' and the rest of the world's shares of world output sum to 100 within this (points).</summary>
        public const double ShareTolerance = 0.1;

        /// <summary>The history wall's bands sum to 100 within this every year (points), the build's own check.</summary>
        public const double HistoryTolerance = 0.5;

        /// <summary>A lane is as wide as its record times the scale within this share.</summary>
        public const double WidthTolerance = 0.02;

        /// <summary>The default lane set: every US pair and this many of the largest other pairs (two-way, exporters' records).</summary>
        public const int DefaultOtherPairs = 6;

        /// <summary>An event whose expiry falls within this many days of the as-of date is flagged on the event strip.</summary>
        public const int ExpiryFlagDays = 30;

        /// <summary>A cell whose two records differ by more than this share of the exporter's is flagged (the build's rule).</summary>
        public const double MirrorGapShare = 0.15;

        /// <summary>The gate's year (spec 6.8): an item dated in it or later is re-read on the as-of date before it is drawn,
        /// whatever its copy's flag says, and a copy dated so without a verdict is never drawn.</summary>
        public const int GateYear = 2026;

        /// <summary>Ids: the United States, China (PIIE's chart is of the US-China lanes), the EU27, Germany (drawn inside
        /// the EU27, never added), Russia (partner-built cells), the rest of the world and the world.</summary>
        public const string Us = "USA", China = "CHN", Eu = "EU27", Germany = "DEU", Russia = "RUS", Rest = "ROW", World = "WLD";

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ what the file holds

        /// <summary>The 2026 gate's verdict on one item as global.json stores it (Tools/global/verify.py).</summary>
        public sealed class Verdict
        {
            /// <summary>The gate item's id ("tic_CHN", "china_rare_earths.expires"), its kind (value, event, evidence,
            /// outcome) and, for a further source or an outcome, its event.</summary>
            public string Id, Kind, Parent;

            /// <summary>"verified" or "unverified"; the date the source was re-read for the verdict; why it failed.</summary>
            public string Status, VerifiedOn, Reason;

            /// <summary>The date the item is about (ISO; a value's date, an event's start).</summary>
            public string Date;

            /// <summary>Jev's support for the claim (0-1, model evidence; NaN when not asked).</summary>
            public double SupportedJ = double.NaN;

            /// <summary>Whether the item is dated 2026 or later (then it must be re-read on or after the as-of date): its own
            /// dates, its copy's flag or its gate item's make it so, and a flag only ever widens it. And whether the build drew it.</summary>
            public bool Dated2026, Drawn;

            public bool Verified => Status == "verified";

            /// <summary>Verified, and re-read on or after <paramref name="asOf"/> when dated 2026.</summary>
            public bool Passes(string asOf) => Verified && (!Dated2026 || OnOrAfter(VerifiedOn, asOf));
        }

        /// <summary>One stored value: its number (NaN when null), year or month, unit, source, URL, API field (or the
        /// arithmetic of a computed value) and note.</summary>
        public sealed class Measure
        {
            public double Value = double.NaN;
            public string Year, Unit, Source, Url, Field, Note;

            /// <summary>The gate's verdict for a value dated 2026 (TIC holdings, COFER), else null (and then a value dated 2026
            /// is not drawn: <see cref="Drawable"/>).</summary>
            public Verdict Gate;

            /// <summary>The value a year before (TIC: end-Aug 2025), NaN when not stored.</summary>
            public double YearBefore = double.NaN;

            public bool Has => !double.IsNaN(Value);
        }

        /// <summary>One economy, Germany, the rest of the world or the world, with its year-2024 measures.</summary>
        public sealed class Economy
        {
            public string Id, Name, Short, Label;

            /// <summary>Its place in the file's west-to-east row (0 = west, the rest of the world last; -1 for Germany and the world).</summary>
            public int FileOrder = -1;

            /// <summary>The block it is drawn inside (Germany: EU27), else null.</summary>
            public string Inside;

            /// <summary>Size (IMF WEO NGDPD, PPPGDP, PPPSH), people (WDI), the state (IMF Fiscal Monitor, % of GDP), trade
            /// (WDI, % of GDP), energy (net imports, % of use; negative = net exporter), crude (% of world output), military
            /// (SIPRI), the current account (WEO), the net international investment position (IMF IIP), V-Dem's electoral
            /// democracy index, the centroid longitude (degrees east) and US Treasuries held (TIC, end-Aug 2026, gated).</summary>
            public Measure Gdp, GdpPpp, Share, SharePpp, Population, SharePeople, LabourForce, GovSpending, GovRevenue, ExportsPct,
                ImportsPct, EnergyNetImports, CrudeShare, MilitaryUsd, MilitaryPctGdp, MilitaryShare, CurrentAccountUsd,
                CurrentAccountPct, NiipUsd, NiipPct, Edi, Centroid, Treasuries;

            /// <summary>Shares of value added at basic prices normalised to 100, bottom to top: agriculture, industry excluding
            /// manufacturing, manufacturing, services (null when not measured: the rest of the world).</summary>
            public double[] Strata;

            /// <summary>The strata's note and the value-added basis (OECD for the US, DGBAS for Taiwan, Canada 2022).</summary>
            public string StrataNote, ValueAddedBasis;

            /// <summary>Regimes of the World (0 closed autocracy - 3 liberal democracy; NaN for the EU27) and its label.</summary>
            public double Regime = double.NaN;

            public string RegimeLabel;

            /// <summary>The EU27's population shares by regime class (%), empty elsewhere.</summary>
            public IReadOnlyDictionary<string, double> RegimeShares = new Dictionary<string, double>();

            /// <summary>The rest of the world: how many economies it gathers (economies.ROW.economies, counted by the build from
            /// IMF WEO NGDPD) and its label (economies.ROW.note: "the rest of the world · N economies together, not one").</summary>
            public double Economies = double.NaN;

            public string Note;

            public bool IsRest => Id == Rest;
        }

        /// <summary>One goods lane of 2024: a year's goods from the exporter to the importer.</summary>
        public sealed class Lane
        {
            public string Exporter, Importer;

            /// <summary>The file's key for the pair, both directions ("USA-CHN", trade.lanes).</summary>
            public string Pair;

            /// <summary>The exporter's record (drawn: one record per lane) and the importer's (the mirror, in the label and on
            /// hover), US$ bn; the fields they were read from.</summary>
            public double Record, Mirror;

            public string RecordField, MirrorField;

            /// <summary>The exporter's record is Russia's, built by the IMF from partners' records (drawn in the ghost style,
            /// "partner records"); likewise the mirror when the importer is Russia.</summary>
            public bool PartnerBuilt, MirrorPartnerBuilt;

            /// <summary>The two records differ by more than <see cref="MirrorGapShare"/> of the exporter's.</summary>
            public bool GapFlagged;

            /// <summary>Both directions of the pair together (exporters' records, US$ bn): how pairs are ranked.</summary>
            public double TwoWay;

            /// <summary>In the default set (every lane is in "All flows"), and its emphasis there: "subject" (US pairs),
            /// "support" (the other default pairs) or "context".</summary>
            public bool InDefault;

            public string Role;

            /// <summary>"CHN>USA": exporter, then importer.</summary>
            public string Key => Exporter + ">" + Importer;

            /// <summary>"CHN-USA", for fact keys.</summary>
            public string FactKey => Exporter + "-" + Importer;
        }

        /// <summary>A further source an event cites (a start date, a scope, an expiry, a later status), gated like the event.</summary>
        public sealed class Evidence
        {
            public string Role, Url, Quote, Claim, Note;
            public Verdict Verdict;
        }

        /// <summary>The recorded outcome of an event past its stated end or expiry date, gated like the event itself.</summary>
        public sealed class Outcome
        {
            public string Text, Url, Quote, Claim;

            /// <summary>Whether the build recorded it (it passed the gate).</summary>
            public bool Recorded;

            public Verdict Verdict;
        }

        /// <summary>One dated event (tariffs, sanctions, export controls, wars, chokepoints, agreements, blocs).</summary>
        public sealed class Event
        {
            public string Id, Name, Kind, Url, Quote, Claim, Magnitude, Note, StartNote, ExpiresNote;

            /// <summary>How it may be drawn: wall, cut, valve, war, strait, tick (the event strip only) or none (listed only).</summary>
            public string Form;

            /// <summary>The economies it involves, in the file's order (the first is the one that acts: imposes the tariff,
            /// controls the export).</summary>
            public string[] Parties = Array.Empty<string>();

            /// <summary>Where its magnitudes are stored ("tariffs.series", "trade.bilateral.pairs.USA-RUS", "trade.chipTools").</summary>
            public string[] Linked = Array.Empty<string>();

            /// <summary>ISO dates (null when none): the start, the stated end, and the end of a suspension or truce.</summary>
            public string Start, End, Expires;

            public string EndText;

            /// <summary>Whether a verified quote states the expiry date in full (else the date is the research's, ≈).</summary>
            public bool ExpiresStated;

            public Verdict Verdict;
            public Evidence[] Evidence = Array.Empty<Evidence>();

            /// <summary>The recorded outcome, or null.</summary>
            public Outcome Outcome;

            /// <summary>The build's flags: listed (in the panel and strip) and drawn (Verdict.Drawn).</summary>
            public bool BuildListed;

            // the model's reading, as of the data's as-of date

            /// <summary>The stated end date, or the expiry, is before the as-of date.</summary>
            public bool PastEnd, PastExpiry;

            /// <summary>A verified outcome is recorded (re-read on or after the as-of date when dated 2026).</summary>
            public bool OutcomeRecorded;

            /// <summary>The expiry falls within <see cref="ExpiryFlagDays"/> days of the as-of date (flagged on the strip).</summary>
            public bool ExpiresSoon;

            /// <summary>Listed: verified with every further source, and not past an end or expiry without an outcome (and
            /// listed by the build). Drawn: listed, with a mark (form other than none) and drawn by the build.</summary>
            public bool Listed, Drawn;

            /// <summary>Why it is not listed (empty when it is).</summary>
            public string Why = "";

            /// <summary>A mark on the stage (not only a tick on the strip).</summary>
            public bool Marked => Drawn && Form != "tick";
        }

        /// <summary>A gated 2026 value (OHCHR, Euroclear, Brent, the Gulf shut-ins, NATO, TIC, PIIE's 2026 rows, COFER).</summary>
        public sealed class GatedValue
        {
            public string Id, Kind, Group, Label, Date, Claim, Url, Quote;

            /// <summary>The value when it is one number (NaN otherwise), and its parts when it is several ("killed", "injured").</summary>
            public double Number = double.NaN;

            public IReadOnlyDictionary<string, double> Parts = new Dictionary<string, double>();
            public Verdict Verdict;

            /// <summary>Drawn by the build and passing the gate as of the as-of date, its gate item too.</summary>
            public bool Drawn;
        }

        /// <summary>One row of PIIE's US-China tariff chart: an action and the four trade-weighted averages after it (%).</summary>
        public sealed class TariffRow
        {
            public string Date, Action, GateId;
            public double ChinaOnRow = double.NaN, ChinaOnUs = double.NaN, UsOnChina = double.NaN, UsOnRow = double.NaN;

            /// <summary>The row's gate verdict (2026 rows only; null before).</summary>
            public Verdict Verdict;

            /// <summary>Drawable: a row before 2026 that carries no verdict, or a row the build drew that passes the gate with
            /// its gate item (a row dated 2026 without a verdict is not).</summary>
            public bool Drawn;
        }

        /// <summary>One keystone of the chokepoints layer: a 100% bar of who holds a critical link.</summary>
        public sealed class Chokepoint
        {
            public string Id, Keystone, Scope, Source, Url, Note, Rule, Field;
            public int Year;
            public Holder[] Holders = Array.Empty<Holder>();
        }

        /// <summary>A holder's share of a keystone (%); its id is an economy, another country (in the rest of the world) or a
        /// region ("EUROPE").</summary>
        public sealed class Holder
        {
            public string Id, Text, Note, Computed;
            public double Value = double.NaN;
        }

        /// <summary>A strait's measured flow (EIA, UNCTAD) with its quote.</summary>
        public sealed class Strait
        {
            public string Id, Name, Measure, Unit, Source, Url, Quote;
            public double Value = double.NaN, Before = double.NaN;
            public int Year;
        }

        /// <summary>A bilateral series pair ("USA-RUS"): its series by name, one value per <see cref="Bilateral.Years"/>.</summary>
        public sealed class BilateralPair
        {
            public string Key, Unit, Source, Url, Field;
            public IReadOnlyDictionary<string, double[]> Series = new Dictionary<string, double[]>();
        }

        /// <summary>The annual bilateral series 2015-2025 (Census US pairs, Eurostat EU pairs, IMTS for Russia's rerouting).</summary>
        public sealed class Bilateral
        {
            public int[] Years = Array.Empty<int>();
            public IReadOnlyDictionary<string, BilateralPair> Pairs = new Dictionary<string, BilateralPair>();

            /// <summary>
            /// The importer's own record of its imports from <paramref name="from"/> in a year (US$ bn; NaN when not stored),
            /// from the pair "importer-from": the US's general imports (Census), the EU27's imports converted to dollars
            /// (Eurostat), or an importer's c.i.f. record of imports from Russia (IMTS). <paramref name="record"/> names it.
            /// </summary>
            public double Imports(string importer, string from, int year, out string record)
            {
                record = null;
                int i = Array.IndexOf(Years, year);
                if (i < 0 || !Pairs.TryGetValue(importer + "-" + from, out BilateralPair p)) return double.NaN;
                foreach (string name in new[] { "usImports", "importsUsd", "importsFromRussia" })
                {
                    if (name == "usImports" && importer != Us) continue;
                    if (name == "importsFromRussia" && from != Russia) continue;
                    if (!p.Series.TryGetValue(name, out double[] s) || i >= s.Length || double.IsNaN(s[i])) continue;
                    record = p.Source + " (" + name + ")";
                    return s[i];
                }

                return double.NaN;
            }
        }

        /// <summary>US customs imports by partner, 2024 and 2025 (Census), with the exporter's 2025 record.</summary>
        public sealed class Overlay
        {
            public string Partner, Url, Field;
            public double Imports2024 = double.NaN, Imports2025 = double.NaN, ChangePct = double.NaN, ExporterRecord2025 = double.NaN;
        }

        /// <summary>The shares of world output since 1950 on one basis (purchasing power or market rates).</summary>
        public sealed class History
        {
            /// <summary>"ppp" or "market".</summary>
            public string Basis, Unit, Source, Note, Before1980;

            public int[] Years = Array.Empty<int>();

            /// <summary>Each band's share per year (%), by economy id (the 13 and the rest of the world).</summary>
            public IReadOnlyDictionary<string, double[]> Shares = new Dictionary<string, double[]>();

            /// <summary>Where the source changes (Maddison to the IMF, 1980; 0 when it does not), its label, note, and each
            /// band's step there (before, after).</summary>
            public int SpliceYear;

            public string SpliceLabel, SpliceNote;
            public IReadOnlyDictionary<string, double[]> SpliceJumps = new Dictionary<string, double[]>();

            /// <summary>The band that is the USSR before 1992 (Russia) and its label.</summary>
            public string SeamBand, SeamLabel;

            /// <summary>The largest distance of a year's bands from 100 (points).</summary>
            public double WorstSum;

            public int First => Years.Length > 0 ? Years[0] : 0;
            public int Last => Years.Length > 0 ? Years[Years.Length - 1] : 0;

            /// <summary>A band's share in a year (NaN when the year or band is not stored).</summary>
            public double At(string band, int year)
            {
                int i = Array.IndexOf(Years, year);
                return i >= 0 && Shares.TryGetValue(band, out double[] s) && i < s.Length ? s[i] : double.NaN;
            }
        }

        /// <summary>An item the gate lists as pending verification (WorldPanel's list).</summary>
        public sealed class Pending
        {
            public string Id, Label, Reason;
        }

        /// <summary>A check the build printed, as stored.</summary>
        public sealed class BuildCheck
        {
            public string Name, Detail;
            public bool Ok;
        }

        // ------------------------------------------------------------------ the model

        /// <summary>The as-of date of the data (ISO): the day the gate last re-read every 2026 source. Every "past its end",
        /// "verified on or after" and "expires within 30 days" is measured from it, never from the clock.</summary>
        public readonly string AsOf;

        /// <summary>The base year (2024).</summary>
        public readonly int Year;

        /// <summary>The file's note.</summary>
        public readonly string Note;

        /// <summary>The row, west to east, the rest of the world last: the computed order (<see cref="Order"/>).</summary>
        public readonly IReadOnlyList<Economy> Row;

        /// <summary>The 13 economies' ids, west to east as computed from their centroid longitudes, then "ROW".</summary>
        public readonly IReadOnlyList<string> Order;

        /// <summary>The order global.json stores, and its printed rule ("west → east by where people live").</summary>
        public readonly IReadOnlyList<string> FileOrder;

        public readonly string OrderRule;

        /// <summary>Germany (inside the EU27) and the world.</summary>
        public readonly Economy GermanyEconomy, WorldEconomy;

        /// <summary>Every lane of "All flows" (13 × 12), by the pair's two-way size, largest first; the default set's lanes.</summary>
        public readonly IReadOnlyList<Lane> Lanes, DefaultLanes;

        /// <summary>World goods exports less intra-EU trade (US$ bn): the base of the lane shares.</summary>
        public readonly double WorldExIntraEu;

        /// <summary>World goods exports (WDI) and intra-EU trade (IMTS; a label on the EU block, never an arc), US$ bn.</summary>
        public readonly Measure WorldGoodsExports, IntraEu;

        /// <summary>The exporters' records the default set and "All flows" carry (US$ bn) and their shares of
        /// <see cref="WorldExIntraEu"/> (%).</summary>
        public readonly double DefaultCarried, AllCarried, DefaultShare, AllShare;

        /// <summary>What the trade block prints: its basis, the one-record rule, its notes.</summary>
        public readonly string TradeBasis, TradeRule;

        public readonly IReadOnlyList<string> TradeNotes;

        /// <summary>Taiwan's exports to Hong Kong (US$ bn; the "via Hong Kong" stub on Taiwan → China) and its field.</summary>
        public readonly double TaiwanViaHongKong;

        public readonly string TaiwanViaHongKongField;

        /// <summary>Each exporter's goods to the rest of the world and in all (US$ bn; the EU27's extra-EU).</summary>
        public readonly IReadOnlyDictionary<string, double> ToRest, ExportsTotal;

        /// <summary>US services trade by partner, 2024 (BEA; the hollow "+ services" lanes): exports and imports, US$ bn.</summary>
        public readonly IReadOnlyDictionary<string, (double exports, double imports)> UsServices;

        public readonly string UsServicesSource;

        /// <summary>US customs imports by partner, 2024 → 2025 (the 2025 variant).</summary>
        public readonly IReadOnlyDictionary<string, Overlay> Overlay2025;

        public readonly string Overlay2025Note;

        public readonly Bilateral BilateralSeries;

        /// <summary>US exports to China of chip-making machines (HS 8486; UN Comtrade), 2021 and 2025.</summary>
        public readonly Measure ChipTools2021, ChipTools2025;

        /// <summary>PIIE's chart rows by date, its caveat ("statutory rates on fixed 2017 trade, not duties collected") and source.</summary>
        public readonly IReadOnlyList<TariffRow> Tariffs;

        public readonly string TariffCaveat, TariffSource;

        /// <summary>The first row (January 2018: the earlier rates) and the latest drawable one (the walls' rates).</summary>
        public readonly TariffRow TariffsFirst, TariffsLatest;

        /// <summary>The Section 301 forced-labour rate by partner (%; Federal Register 2026-15274), the partners whose rate is
        /// all-in (MFN included), its event, and whether it may be drawn.</summary>
        public readonly IReadOnlyDictionary<string, double> Section301Rates;

        public readonly IReadOnlyCollection<string> Section301AllIn;
        public readonly string Section301Event;
        public readonly bool Section301Drawn;

        /// <summary>Every dated event, in the file's order.</summary>
        public readonly IReadOnlyList<Event> Events;

        /// <summary>Every gated 2026 value.</summary>
        public readonly IReadOnlyList<GatedValue> Values2026;

        public readonly IReadOnlyList<Chokepoint> Chokepoints;
        public readonly IReadOnlyList<Strait> Straits;

        /// <summary>US Treasuries held by all foreign residents and by foreign officials (TIC, end-Aug 2026, gated), COFER's
        /// dollar share (2026-Q2, gated; and 2024-Q4) and the thread size (US$ bn per thread).</summary>
        public readonly Measure TreasuriesTotal, TreasuriesOfficial, CoferLatest, Cofer2024;

        public readonly double ThreadBillions;
        public readonly string FinanceNote;

        /// <summary>The history wall's two bases.</summary>
        public readonly History HistoryPpp, HistoryMarket;

        /// <summary>The gate: its rule, threshold, counts, the pending list and every item's verdict.</summary>
        public readonly string GateRule, JevModel;

        public readonly double GateThreshold;
        public readonly int GateVerified, GateUnverified;
        public readonly IReadOnlyList<Pending> PendingItems;
        public readonly IReadOnlyList<Verdict> GateItems;

        /// <summary>The totals line and its parts.</summary>
        public readonly string TotalsLine;

        public readonly double TotalsWorldGdpT, TotalsThe13Gdp, TotalsThe13ShareGdp, TotalsThe13SharePeople, TotalsUsShareGdp,
            TotalsUsSharePeople, TotalsUsShareMilitary;

        /// <summary>The build's printed checks, its assumptions (≈) and fallbacks.</summary>
        public readonly IReadOnlyList<BuildCheck> BuildChecks;

        public readonly IReadOnlyList<string> Assumptions, Fallbacks;

        /// <summary>The geometry at market exchange rates (the default row) and at purchasing power (the PPP toggle and the
        /// history variant).</summary>
        public readonly WorldLayout Layout, PppLayout;

        /// <summary>The "[Why] World ..." lines (without the prefix), for the log.</summary>
        public readonly IReadOnlyList<string> Summary;

        /// <summary>The build's time on its thread (ms).</summary>
        public readonly double BuildMs;

        readonly Dictionary<string, Economy> byId = new Dictionary<string, Economy>(StringComparer.Ordinal);
        readonly Dictionary<string, Lane> laneByKey = new Dictionary<string, Lane>(StringComparer.Ordinal);
        readonly Dictionary<string, Event> eventById = new Dictionary<string, Event>(StringComparer.Ordinal);
        readonly Dictionary<string, Verdict> gateById = new Dictionary<string, Verdict>(StringComparer.Ordinal);
        readonly List<string> problems = new List<string>();

        // what the file says it computed, kept to check against
        readonly List<string> fileDefaultPairs = new List<string>();
        readonly Dictionary<string, (string set, string role, double twoWay)> fileLanes = new Dictionary<string, (string, string, double)>();
        readonly HashSet<string> fileGaps = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> tradeOrder = new List<string>();
        readonly List<(string id, double partners, double toRest, double total)> rowTotals = new List<(string, double, double, double)>();
        double fileDefaultShare = double.NaN, fileAllShare = double.NaN, fileDefaultTwoWay = double.NaN;
        string gateAsOf;
        readonly List<string> historyBands = new List<string>();
        readonly List<(string where, Verdict inline)> inlineVerdicts = new List<(string, Verdict)>();

        // copies dated 2026 or later that carry no gate verdict (not drawn; each a problem)
        readonly List<string> ungated = new List<string>();

        // ------------------------------------------------------------------ the shared model

        // only Provide takes the lock, to publish once; Get never waits for a build
        static readonly object SharedGate = new object();
        static volatile WorldModel shared;

        /// <summary>The shared model, or null until <see cref="Provide"/> has built it; never blocks, so a layer may poll it
        /// every frame while the loader's worker builds.</summary>
        public static WorldModel Get() => shared;

        /// <summary>
        /// Builds the shared model from global.json's text on the calling thread (a worker: never the main thread, where the
        /// parse would stall the frame), publishes its facts and shares it; later calls return the shared model whatever
        /// text they pass. The parse runs outside the lock, as <see cref="MoneyCircuit.Build"/> computes: two calls at once
        /// both parse, and both get the model published first, whose facts are published before <see cref="Get"/> sees it.
        /// Throws when the text is not the world data, so the caller can log why.
        /// </summary>
        public static WorldModel Provide(string json)
        {
            WorldModel first = shared;
            if (first != null) return first;
            WorldModel built = Parse(json);
            lock (SharedGate)
            {
                if (shared != null) return shared;
                built.PublishFacts();
                shared = built;
            }

            return built;
        }

        /// <summary>A model built from global.json's text without sharing or publishing it (tools and tests).</summary>
        public static WorldModel Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("global.json is empty or missing");
            // dates stay text: a reader that turns "2026-11-03" into a DateTime would print it in the machine's format
            JObject root;
            using (JsonTextReader reader = new JsonTextReader(new StringReader(json.TrimStart('\uFEFF'))) { DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader);
            }

            return new WorldModel(root);
        }

        /// <summary>An economy, Germany, the rest of the world or the world by id (null when unknown).</summary>
        public Economy ById(string id) => id != null && byId.TryGetValue(id, out Economy e) ? e : null;

        /// <summary>The lane from an exporter to an importer (null when there is none: the same economy, Germany, the rest).</summary>
        public Lane LaneOf(string exporter, string importer) =>
            laneByKey.TryGetValue(exporter + ">" + importer, out Lane l) ? l : null;

        /// <summary>An event by id (null when unknown).</summary>
        public Event EventById(string id) => id != null && eventById.TryGetValue(id, out Event e) ? e : null;

        /// <summary>A gate item's verdict by id (null when unknown).</summary>
        public Verdict GateItem(string id) => id != null && gateById.TryGetValue(id, out Verdict v) ? v : null;

        /// <summary>Whether an id is one of the 13 economies (a lane end; not Germany, the rest or the world).</summary>
        public bool IsLaneEconomy(string id) => id != null && tradeOrder.Contains(id);

        /// <summary>The share of world goods exports outside intra-EU trade that a set of lanes carries (%; exporters' records).</summary>
        public double ShareOf(IEnumerable<Lane> lanes) => WorldExIntraEu > 0 ? lanes.Sum(l => l.Record) / WorldExIntraEu * 100 : double.NaN;

        /// <summary>
        /// What breaks the spec's rules (section 6.7 and WP11): the shares do not sum to 100 ± 0.1; Germany is added to the
        /// EU27 or the row; a drawn item is unverified, or a drawn 2026 item was verified before the as-of date; a copy
        /// disagrees with its gate item, names none, or is dated 2026 without a verdict; a drawn event is past its stated
        /// end or expiry date without a verified outcome; a lane is not as wide as its record times the scale (within 2%),
        /// folds, or lands off its block's top or the row (<see cref="WorldLayout.Problems"/>); the rest of the world's
        /// Treasuries would be negative; the order, lane sets, shares, totals, history sums or gate counts disagree with
        /// what the file says it computed; a build check failed. Empty when all is well; computed once.
        /// </summary>
        public IReadOnlyList<string> Problems() => problems;

        WorldModel(JObject root)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            AsOf = Str(root["asOf"]);
            Year = (int)Num(root["baseYear"], 0);
            Note = Str(root["note"]);
            if (!IsDate(AsOf)) problems.Add($"global.json has no as-of date (asOf '{AsOf}'): nothing dated 2026 can be checked");

            // ---- the gate's items first: the inline copies are checked against them
            JObject gate = root["gate"] as JObject ?? new JObject();
            gateAsOf = Str(gate["asOf"]);
            GateRule = Str(gate["rule"]);
            GateThreshold = Num(gate["threshold"]);
            JevModel = Str(Child(gate["jev"], "model"));
            GateVerified = (int)Num(gate["verified"], -1);
            GateUnverified = (int)Num(gate["unverified"], -1);
            List<Verdict> items = new List<Verdict>();
            foreach (JObject o in Objects(gate["items"]))
            {
                Verdict v = ReadVerdict(o, Str(o["id"]), Str(o["kind"]), false);
                v.Parent = Str(o["parent"]);
                items.Add(v);
                if (v.Id != null) gateById[v.Id] = v;
            }

            GateItems = items;
            PendingItems = Objects(gate["pending"]).Select(o => new Pending { Id = Str(o["id"]), Label = Str(o["label"]), Reason = Str(o["reason"]) }).ToList();

            // ---- economies and the order
            JObject economies = root["economies"] as JObject ?? new JObject();
            foreach (KeyValuePair<string, JToken> kv in economies)
            {
                if (kv.Value is JObject o) byId[kv.Key] = ReadEconomy(kv.Key, o);
            }

            JObject order = root["order"] as JObject ?? new JObject();
            FileOrder = Strings(order["westToEast"]);
            OrderRule = Str(order["rule"]);
            List<string> row13 = Strings(root["row13"]).ToList();
            if (row13.Count == 0) row13 = Strings(Child(root["trade"], "order")).ToList();
            Dictionary<string, double> lon = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string id in row13)
            {
                double l = ById(id)?.Centroid?.Value ?? double.NaN;
                if (double.IsNaN(l)) l = Num(Child(order["lon"], id));
                if (double.IsNaN(l)) problems.Add($"{id} has no centroid longitude: its place in the west-to-east row cannot be computed");
                lon[id] = double.IsNaN(l) ? double.MaxValue : l;
            }

            List<string> computed = row13.OrderBy(id => lon[id]).ThenBy(id => id, StringComparer.Ordinal).ToList();
            computed.Add(Rest);
            Order = computed;
            List<Economy> rowList = new List<Economy>();
            foreach (string id in Order)
            {
                Economy e = ById(id);
                if (e == null) problems.Add($"global.json has no economy '{id}'");
                else rowList.Add(e);
            }

            Row = rowList;
            GermanyEconomy = ById(Germany);
            WorldEconomy = ById(World);
            if (GermanyEconomy == null) problems.Add("global.json has no Germany (DEU): it cannot be drawn inside the EU27");
            if (WorldEconomy == null) problems.Add("global.json has no world (WLD)");

            // ---- totals, checks, assumptions
            JObject totals = root["totals"] as JObject ?? new JObject();
            TotalsLine = Str(totals["line"]);
            TotalsWorldGdpT = Num(totals["worldGdpT"]);
            TotalsThe13Gdp = Num(totals["the13Gdp"]);
            TotalsThe13ShareGdp = Num(totals["the13ShareGdp"]);
            TotalsThe13SharePeople = Num(totals["the13SharePeople"]);
            TotalsUsShareGdp = Num(totals["usShareGdp"]);
            TotalsUsSharePeople = Num(totals["usSharePeople"]);
            TotalsUsShareMilitary = Num(totals["usShareMilitary"]);
            BuildChecks = Objects(root["checks"]).Select(o => new BuildCheck { Name = Str(o["check"]), Ok = Bool(o["ok"]), Detail = Str(o["detail"]) }).ToList();
            Assumptions = Strings(root["assumptions"]);
            Fallbacks = Strings(root["fallbacks"]);

            // ---- trade
            JObject trade = root["trade"] as JObject ?? new JObject();
            ReadTrade(trade, out List<Lane> lanes, out WorldGoodsExports, out IntraEu, out TaiwanViaHongKong, out TaiwanViaHongKongField,
                out Dictionary<string, double> toRest, out Dictionary<string, double> total);
            TradeBasis = Str(trade["basis"]);
            TradeRule = Str(trade["rule"]);
            TradeNotes = Strings(trade["notes"]);
            ToRest = toRest;
            ExportsTotal = total;
            WorldExIntraEu = WorldGoodsExports.Value - IntraEu.Value;

            // the default set: every US pair, then the largest other pairs by two-way trade (exporters' records)
            List<string> otherPairs = lanes.Where(l => l.Exporter != Us && l.Importer != Us).Select(l => l.Pair).Distinct()
                .OrderByDescending(p => lanes.First(l => l.Pair == p).TwoWay).ThenBy(p => p, StringComparer.Ordinal)
                .Take(DefaultOtherPairs).ToList();
            foreach (Lane l in lanes)
            {
                bool us = l.Exporter == Us || l.Importer == Us;
                l.InDefault = us || otherPairs.Contains(l.Pair);
                l.Role = us ? "subject" : l.InDefault ? "support" : "context";
            }

            Lanes = lanes.OrderByDescending(l => l.TwoWay).ThenBy(l => l.Pair, StringComparer.Ordinal).ThenByDescending(l => l.Record).ToList();
            foreach (Lane l in Lanes) laneByKey[l.Key] = l;
            DefaultLanes = Lanes.Where(l => l.InDefault).ToList();
            DefaultCarried = DefaultLanes.Sum(l => l.Record);
            AllCarried = Lanes.Sum(l => l.Record);
            DefaultShare = ShareOf(DefaultLanes);
            AllShare = ShareOf(Lanes);

            JObject services = trade["usServices"] as JObject ?? new JObject();
            UsServicesSource = Str(services["source"]);
            Dictionary<string, (double, double)> svc = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> kv in services["partners"] as JObject ?? new JObject())
            {
                if (kv.Value is JObject p) svc[kv.Key] = (Num(p["exports"]), Num(p["imports"]));
            }

            UsServices = svc;
            JObject overlay = trade["overlay2025"] as JObject ?? new JObject();
            Overlay2025Note = Str(overlay["note"]);
            Dictionary<string, Overlay> over = new Dictionary<string, Overlay>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> kv in overlay["partners"] as JObject ?? new JObject())
            {
                if (!(kv.Value is JObject p)) continue;
                over[kv.Key] = new Overlay
                {
                    Partner = kv.Key, Url = Str(p["url"]), Field = Str(p["field"]),
                    Imports2024 = Num(p["imports2024"]), Imports2025 = Num(p["imports2025"]),
                    ChangePct = Num(p["changePct"]), ExporterRecord2025 = Num(p["exporterRecord2025"])
                };
            }

            Overlay2025 = over;
            BilateralSeries = ReadBilateral(trade["bilateral"] as JObject);
            ChipTools2021 = ReadMeasure(Child(trade["chipTools"], "2021"));
            ChipTools2025 = ReadMeasure(Child(trade["chipTools"], "2025"));

            // ---- tariffs
            JObject tariffs = root["tariffs"] as JObject ?? new JObject();
            TariffCaveat = Str(tariffs["caveat"]);
            TariffSource = Str(tariffs["source"]);
            List<TariffRow> rows = new List<TariffRow>();
            foreach (JObject o in Objects(tariffs["series"]))
            {
                TariffRow r = new TariffRow
                {
                    Date = Str(o["date"]), Action = Str(o["action"]), GateId = Str(o["gateId"]),
                    ChinaOnRow = Num(o["chinaOnRow"]), ChinaOnUs = Num(o["chinaOnUs"]), UsOnChina = Num(o["usOnChina"]), UsOnRow = Num(o["usOnRow"])
                };
                if (o["status"] != null || r.GateId != null)
                {
                    r.Verdict = ReadVerdict(o, r.GateId, "value", false, r.Date);
                    inlineVerdicts.Add(("tariffs.series " + r.Date, r.Verdict));
                }
                else if (InGateYear(r.Date)) ungated.Add(Ungated("tariffs.series " + r.Date, r.Date));

                rows.Add(r);
            }

            Tariffs = rows;
            JObject s301 = tariffs["section301_2026"] as JObject ?? new JObject();
            Dictionary<string, double> rates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> kv in s301["rates"] as JObject ?? new JObject()) rates[kv.Key] = Num(kv.Value);
            Section301Rates = rates;
            Section301AllIn = new HashSet<string>(Strings(s301["allIn"]), StringComparer.Ordinal);
            Section301Event = Str(s301["gateId"]);
            Verdict s301Verdict = s301.Count > 0 ? ReadVerdict(s301, Section301Event, "event", false) : null;
            if (s301Verdict != null) inlineVerdicts.Add(("tariffs.section301_2026", s301Verdict));

            // ---- events and 2026 values
            List<Event> events = new List<Event>();
            foreach (JObject o in Objects(root["events"])) events.Add(ReadEvent(o));
            Events = events;
            foreach (Event e in events)
            {
                if (e.Id != null) eventById[e.Id] = e;
            }

            List<GatedValue> values = new List<GatedValue>();
            foreach (JObject o in Objects(root["values2026"]))
            {
                GatedValue v = new GatedValue
                {
                    Id = Str(o["id"]), Kind = Str(o["kind"]), Group = Str(o["group"]), Label = Str(o["label"]), Date = Str(o["date"]),
                    Claim = Str(o["claim"]), Url = Str(o["url"]), Quote = Str(o["quote"])
                };
                v.Verdict = ReadVerdict(o, v.Id, "value", false, v.Date);
                inlineVerdicts.Add(("values2026 " + v.Id, v.Verdict));
                if (o["value"] is JObject parts)
                {
                    Dictionary<string, double> p = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, JToken> kv in parts) p[kv.Key] = Num(kv.Value);
                    v.Parts = p;
                }
                else v.Number = Num(o["value"]);

                values.Add(v);
            }

            Values2026 = values;

            // ---- chokepoints, straits, finance, history
            Chokepoints = Objects(root["chokepoints"]).Select(o => new Chokepoint
            {
                Id = Str(o["id"]), Keystone = Str(o["keystone"]), Scope = Str(o["scope"]), Year = (int)Num(o["year"], 0),
                Source = Str(o["source"]), Url = Str(o["url"]), Note = Str(o["note"]), Rule = Str(o["rule"]), Field = Str(o["field"]),
                Holders = Objects(o["holders"]).Select(h => new Holder
                {
                    Id = Str(h["id"]), Value = Num(h["value"]), Text = Str(h["valueText"]), Note = Str(h["note"]), Computed = Str(h["computed"])
                }).ToArray()
            }).ToList();
            Straits = Objects(root["straits"]).Select(o => new Strait
            {
                Id = Str(o["id"]), Name = Str(o["strait"]), Measure = Str(o["measure"]), Unit = Str(o["unit"]), Source = Str(o["source"]),
                Url = Str(o["url"]), Quote = Str(o["quote"]), Value = Num(o["value"]), Before = Num(o["before"]), Year = (int)Num(o["year"], 0)
            }).ToList();
            JObject finance = root["finance"] as JObject ?? new JObject();
            TreasuriesTotal = ReadMeasure(finance["treasuriesTotal"], "finance.treasuriesTotal");
            TreasuriesOfficial = ReadMeasure(finance["treasuriesOfficial"], "finance.treasuriesOfficial");
            CoferLatest = ReadMeasure(Child(finance["cofer"], "usdShareLatest"), "finance.cofer.usdShareLatest");
            Cofer2024 = ReadMeasure(Child(finance["cofer"], "usdShare2024Q4"));
            ThreadBillions = Num(finance["threadsPer"], 100);
            FinanceNote = Str(finance["note"]);
            JObject history = root["history"] as JObject ?? new JObject();
            historyBands.AddRange(Strings(history["bands"]));
            HistoryPpp = ReadHistory(history["ppp"] as JObject, "ppp");
            HistoryMarket = ReadHistory(history["market"] as JObject, "market");

            // ---- the model's readings, as of the data's date
            Read2026();
            Section301Drawn = s301Verdict != null && CopyDrawn(s301Verdict) && (EventById(Section301Event)?.Drawn ?? false);
            TariffsFirst = Tariffs.FirstOrDefault();
            TariffsLatest = Tariffs.Where(r => r.Drawn && IsDate(r.Date) && !Before(AsOf, r.Date))
                .OrderBy(r => r.Date, StringComparer.Ordinal).LastOrDefault();

            // ---- geometry, then the checks
            Layout = WorldLayout.Build(this, WorldLayout.Basis.Market);
            PppLayout = WorldLayout.Build(this, WorldLayout.Basis.Ppp);
            Check();
            BuildMs = sw.Elapsed.TotalMilliseconds;
            Summary = Describe();
        }

        // ------------------------------------------------------------------ reading

        Economy ReadEconomy(string id, JObject o)
        {
            Economy e = new Economy
            {
                Id = id, Name = Str(o["name"]), Short = Str(o["short"]), Label = Str(o["label"]), FileOrder = (int)Num(o["order"], -1),
                Inside = Str(o["inside"]), Note = Str(o["note"]),
                Gdp = ReadMeasure(o["gdp"]), GdpPpp = ReadMeasure(o["gdpPpp"]), Share = ReadMeasure(o["shareWorld"]),
                SharePpp = ReadMeasure(o["sharePpp"]), Population = ReadMeasure(o["population"]), SharePeople = ReadMeasure(o["sharePeople"]),
                LabourForce = ReadMeasure(o["labourForce"]), GovSpending = ReadMeasure(o["govSpending"]), GovRevenue = ReadMeasure(o["govRevenue"]),
                ExportsPct = ReadMeasure(o["exportsPct"]), ImportsPct = ReadMeasure(o["importsPct"]),
                EnergyNetImports = ReadMeasure(o["energyNetImports"]), CrudeShare = ReadMeasure(o["crudeShare"]),
                MilitaryUsd = ReadMeasure(Child(o["military"], "usd")), MilitaryPctGdp = ReadMeasure(Child(o["military"], "pctGdp")),
                MilitaryShare = ReadMeasure(Child(o["military"], "shareWorld")), CurrentAccountUsd = ReadMeasure(Child(o["currentAccount"], "usd")),
                CurrentAccountPct = ReadMeasure(Child(o["currentAccount"], "pctGdp")), NiipUsd = ReadMeasure(Child(o["niip"], "usd")),
                NiipPct = ReadMeasure(Child(o["niip"], "pctGdp")), Edi = ReadMeasure(Child(o["democracy"], "edi")), Centroid = ReadMeasure(o["centroid"]),
                Treasuries = ReadMeasure(o["treasuries"], "economies." + id + ".treasuries"),
                Economies = o["economies"] is JObject n ? Num(n["value"]) : double.NaN
            };
            if (o["valueAdded"] is JObject va)
            {
                e.ValueAddedBasis = Str(va["basis"]);
                if (va["strata"] is JObject strata)
                {
                    double[] shares = (strata["shares"] as JArray)?.Select(x => Num(x)).ToArray();
                    e.Strata = shares != null && shares.Length == 4 && shares.All(s => !double.IsNaN(s)) ? shares : null;
                    e.StrataNote = Str(strata["note"]);
                }
            }

            if (Child(o["democracy"], "regime") is JObject regime)
            {
                e.Regime = Num(regime["value"]);
                e.RegimeLabel = Str(regime["label"]);
                Dictionary<string, double> shares = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, JToken> kv in regime["populationShares"] as JObject ?? new JObject()) shares[kv.Key] = Num(kv.Value);
                e.RegimeShares = shares;
            }

            return e;
        }

        /// <summary>
        /// A stored value; a gated one (a gateId) keeps its verdict, checked against the gate's item. One dated 2026 or later
        /// ("2026-08", "2026-Q2") without a verdict is listed as ungated: <see cref="Drawable"/> refuses it, Check reports it.
        /// </summary>
        Measure ReadMeasure(JToken t, string where = null)
        {
            Measure m = new Measure();
            if (!(t is JObject o)) return m;
            m.Value = Num(o["value"]);
            m.Year = Str(o["year"]);
            m.Unit = Str(o["unit"]);
            m.Source = Str(o["source"]);
            m.Url = Str(o["url"]);
            m.Field = Str(o["field"]);
            m.Note = Str(o["note"]);
            m.YearBefore = Num(o["aug2025"]);
            if (o["gateId"] != null || o["status"] != null)
            {
                // a copy dated by its month or quarter is a 2026 item even where the copy carries no flag
                m.Gate = ReadVerdict(o, Str(o["gateId"]), "value", false, m.Year);
                inlineVerdicts.Add((where ?? o.Path, m.Gate));
            }
            else if (m.Has && InGateYear(m.Year)) ungated.Add(Ungated(where ?? o.Path, m.Year));

            return m;
        }

        /// <summary>
        /// The verdict fields of an item, an event or an inline copy. A copy that names its gate item (PIIE's 2026 rows, the
        /// Section 301 rates, TIC holdings) states its status and whether it is drawn; what it leaves out (the re-read date,
        /// Jev's support, the date) is its gate item's. It is dated 2026 when its own flag, its gate item's, its date or any of
        /// <paramref name="dates"/> (an event's start, end and expiry; a row's date; a value's month) says so, or
        /// <paramref name="dated"/> (a further source or an outcome of a 2026 event): a flag only ever widens it, never
        /// narrows it, and a copy dated 2026 marks its gate item so too (fail closed).
        /// </summary>
        Verdict ReadVerdict(JObject o, string id, string kind, bool dated, params string[] dates)
        {
            Verdict v = new Verdict
            {
                Id = id, Kind = kind, Status = Str(o["status"]), VerifiedOn = Str(o["verifiedOn"]), Reason = Str(o["reason"]),
                SupportedJ = Num(o["supportedJ"]), Dated2026 = dated || Bool(o["dated2026"]), Drawn = Bool(o["drawn"]), Date = Str(o["date"])
            };
            Verdict item = GateItem(id);
            if (item != null)
            {
                if (o["verifiedOn"] == null) v.VerifiedOn = item.VerifiedOn;
                if (o["supportedJ"] == null) v.SupportedJ = item.SupportedJ;
                v.Date ??= item.Date;
                v.Dated2026 |= item.Dated2026;
            }

            v.Dated2026 |= InGateYear(v.Date) || dates.Any(InGateYear);
            if (item != null && v.Dated2026) item.Dated2026 = true;
            return v;
        }

        Event ReadEvent(JObject o)
        {
            Event e = new Event
            {
                Id = Str(o["id"]), Name = Str(o["name"]), Kind = Str(o["kind"]), Form = Str(o["form"]) ?? "none", Url = Str(o["url"]),
                Quote = Str(o["quote"]), Claim = Str(o["claim"]), Magnitude = Str(o["magnitude"]), Note = Str(o["note"]),
                StartNote = Str(o["startNote"]), ExpiresNote = Str(o["expiresNote"]), Parties = Strings(o["parties"]).ToArray(),
                Linked = Strings(o["linked"]).ToArray(), Start = Str(o["start"]), End = Str(o["end"]), EndText = Str(o["endText"]),
                Expires = Str(o["expires"]), ExpiresStated = Bool(o["expiresStated"]), BuildListed = Bool(o["listed"])
            };
            // dated 2026 when its start, stated end or expiry is, whatever its flag says
            e.Verdict = ReadVerdict(o, e.Id, "event", false, e.Start, e.End, e.Expires);
            e.Verdict.Date ??= e.Start;
            List<Evidence> evidence = new List<Evidence>();
            foreach (JObject x in Objects(o["evidence"]))
            {
                // a further source of a 2026 event is re-read with it
                string role = Str(x["role"]);
                Verdict v = ReadVerdict(x, e.Id + "." + role, "evidence", e.Verdict.Dated2026);
                v.Parent = e.Id;
                evidence.Add(new Evidence { Role = role, Url = Str(x["url"]), Quote = Str(x["quote"]), Claim = Str(x["claim"]), Note = Str(x["note"]), Verdict = v });
            }

            e.Evidence = evidence.ToArray();
            if (o["outcome"] is JObject oc)
            {
                Verdict v = ReadVerdict(oc, e.Id + ".outcome", "outcome", e.Verdict.Dated2026);
                v.Parent = e.Id;
                e.Outcome = new Outcome
                {
                    Text = Str(oc["text"]), Url = Str(oc["url"]), Quote = Str(oc["quote"]), Claim = Str(oc["claim"]),
                    Recorded = Bool(oc["recorded"]), Verdict = v
                };
            }

            return e;
        }

        void ReadTrade(JObject trade, out List<Lane> lanes, out Measure worldExports, out Measure intraEu, out double viaHk, out string viaHkField,
            out Dictionary<string, double> toRest, out Dictionary<string, double> total)
        {
            lanes = new List<Lane>();
            tradeOrder.AddRange(Strings(trade["order"]));
            worldExports = ReadMeasure(trade["worldGoodsExports"]);
            intraEu = ReadMeasure(trade["intraEU"]);
            viaHk = Num(Child(trade["taiwanViaHongKong"], "exports2024"));
            viaHkField = Str(Child(trade["taiwanViaHongKong"], "field"));
            toRest = new Dictionary<string, double>(StringComparer.Ordinal);
            total = new Dictionary<string, double>(StringComparer.Ordinal);
            int n = tradeOrder.Count;
            JArray matrix = trade["matrix"] as JArray ?? new JArray(), mirror = trade["mirror"] as JArray ?? new JArray(), cells = trade["cells"] as JArray ?? new JArray();
            JArray rest = trade["toROW"] as JArray ?? new JArray(), totals = trade["total"] as JArray ?? new JArray();
            foreach (JObject l in Objects(trade["lanes"]))
            {
                string pair = Str(l["pair"]);
                if (pair != null) fileLanes[pair] = (Str(l["set"]), Str(l["role"]), Num(l["twoWay"]));
            }

            JObject def = trade["defaultSet"] as JObject ?? new JObject();
            fileDefaultPairs.AddRange(Strings(def["usPairs"]));
            fileDefaultPairs.AddRange(Strings(def["otherPairs"]));
            fileDefaultShare = Num(def["shareWorldExIntraEU"]);
            fileDefaultTwoWay = Num(def["twoWayTotal"]);
            fileAllShare = Num(trade["arcsShareWorldExIntraEU"]);
            foreach (string flag in Strings(trade["mirrorGapsFlagged"]))
            {
                int space = flag.IndexOf(' ');
                fileGaps.Add(space > 0 ? flag.Substring(0, space) : flag);
            }

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    string a = tradeOrder[i], b = tradeOrder[j];
                    double rec = Num(Cell(matrix, i, j)), mir = Num(Cell(mirror, i, j));
                    JObject cell = Cell(cells, i, j) as JObject;
                    // the file's key for the pair, either way round; else the trade order's
                    string pair = fileLanes.ContainsKey(a + "-" + b) ? a + "-" + b : fileLanes.ContainsKey(b + "-" + a) ? b + "-" + a : (i < j ? a + "-" + b : b + "-" + a);
                    lanes.Add(new Lane
                    {
                        Exporter = a, Importer = b, Pair = pair, Record = rec, Mirror = mir,
                        RecordField = Str(cell?["record"]), MirrorField = Str(cell?["mirror"]),
                        PartnerBuilt = a == Russia, MirrorPartnerBuilt = b == Russia,
                        GapFlagged = rec > 0 && mir > 0 && Math.Abs(mir - rec) > MirrorGapShare * rec
                    });
                    if (!double.IsNaN(rec)) sum += rec;
                }

                double r = Num(rest.ElementAtOrDefault(i)), t = Num(totals.ElementAtOrDefault(i));
                toRest[tradeOrder[i]] = r;
                total[tradeOrder[i]] = t;
                rowTotals.Add((tradeOrder[i], sum, r, t));
            }

            Dictionary<string, double> twoWay = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (Lane l in lanes) twoWay[l.Pair] = (twoWay.TryGetValue(l.Pair, out double s) ? s : 0) + (double.IsNaN(l.Record) ? 0 : l.Record);
            foreach (Lane l in lanes) l.TwoWay = twoWay[l.Pair];
        }

        static Bilateral ReadBilateral(JObject o)
        {
            Bilateral b = new Bilateral();
            if (o == null) return b;
            b.Years = (o["years"] as JArray)?.Select(y => (int)Num(y, 0)).ToArray() ?? Array.Empty<int>();
            Dictionary<string, BilateralPair> pairs = new Dictionary<string, BilateralPair>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> kv in o["pairs"] as JObject ?? new JObject())
            {
                if (!(kv.Value is JObject p)) continue;
                Dictionary<string, double[]> series = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, JToken> s in p)
                {
                    if (s.Value is JArray a) series[s.Key] = a.Select(x => Num(x)).ToArray();
                }

                pairs[kv.Key] = new BilateralPair
                {
                    Key = kv.Key, Unit = Str(p["unit"]) ?? Str(o["unit"]), Source = Str(p["source"]), Url = Str(p["url"]), Field = Str(p["field"]), Series = series
                };
            }

            b.Pairs = pairs;
            return b;
        }

        static History ReadHistory(JObject o, string basis)
        {
            History h = new History { Basis = basis };
            if (o == null) return h;
            h.Unit = Str(o["unit"]);
            h.Source = Str(o["source"]) ?? string.Join("; ", Objects(o["sources"]).Select(s => Str(s["years"]) + ": " + Str(s["source"])));
            h.Note = Str(o["note"]) ?? Str(o["euNote"]);
            h.Before1980 = Str(o["before1980"]);
            h.Years = (o["years"] as JArray)?.Select(y => (int)Num(y, 0)).ToArray() ?? Array.Empty<int>();
            Dictionary<string, double[]> shares = new Dictionary<string, double[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> kv in o["shares"] as JObject ?? new JObject())
            {
                if (kv.Value is JArray a) shares[kv.Key] = a.Select(x => Num(x)).ToArray();
            }

            h.Shares = shares;
            if (o["splice"] is JObject splice)
            {
                h.SpliceYear = (int)Num(splice["year"], 0);
                h.SpliceLabel = Str(splice["label"]);
                h.SpliceNote = Str(splice["note"]);
                Dictionary<string, double[]> jumps = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, JToken> kv in splice["jump"] as JObject ?? new JObject())
                {
                    if (kv.Value is JArray a) jumps[kv.Key] = a.Select(x => Num(x)).ToArray();
                }

                h.SpliceJumps = jumps;
            }

            if (o["ussrSeam"] is JObject seam)
            {
                h.SeamBand = Str(seam["band"]);
                h.SeamLabel = Str(seam["label"]);
            }

            for (int i = 0; i < h.Years.Length; i++)
            {
                double sum = 0;
                foreach (double[] s in shares.Values) sum += i < s.Length && !double.IsNaN(s[i]) ? s[i] : double.NaN;
                h.WorstSum = Math.Max(h.WorstSum, double.IsNaN(sum) ? double.PositiveInfinity : Math.Abs(sum - 100));
            }

            return h;
        }

        // ------------------------------------------------------------------ what may be drawn, as of the data's date

        /// <summary>
        /// The model's own reading of every gated item, as of <see cref="AsOf"/>, the build's flags only ever narrowing it
        /// (fail closed). A copy passes when it passes the gate and so does its gate item (<see cref="CopyPasses"/>: never
        /// when the gate holds no item). An event is listed when it, and every further source it cites, passes, and it is
        /// not past its stated end or expiry date without a verified outcome that passes; it is drawn when it is listed,
        /// has a mark (form other than none) and the build drew it and its gate item (<see cref="CopyDrawn"/>). A 2026 value
        /// or PIIE row is drawn on the same terms; a row dated 2026 without a verdict never is. The build's own rule is the
        /// same (Tools/global/build.py), so the two agree unless the data was edited by hand; <see cref="Check"/> reports
        /// any disagreement.
        /// </summary>
        void Read2026()
        {
            foreach (Event e in Events)
            {
                List<string> why = new List<string>();
                string failing = Failing(e.Verdict, "it");
                if (failing.Length > 0) why.Add(failing);
                foreach (Evidence x in e.Evidence)
                {
                    failing = Failing(x.Verdict, "its " + x.Role + " source");
                    if (failing.Length > 0) why.Add(failing);
                }

                e.OutcomeRecorded = e.Outcome != null && e.Outcome.Recorded && CopyPasses(e.Outcome.Verdict);
                e.PastEnd = IsDate(e.End) && Before(e.End, AsOf);
                e.PastExpiry = IsDate(e.Expires) && Before(e.Expires, AsOf);
                e.ExpiresSoon = IsDate(e.Expires) && IsDate(AsOf) && !Before(e.Expires, AsOf) && Days(AsOf, e.Expires) <= ExpiryFlagDays;
                if (e.PastEnd && !e.OutcomeRecorded) why.Add($"its stated end date {e.End} is before the as-of date {AsOf} and no verified outcome is recorded");
                if (e.PastExpiry && !e.OutcomeRecorded && !(e.PastEnd && e.End == e.Expires))
                {
                    why.Add($"its suspension or truce expired on {e.Expires}, before the as-of date {AsOf}, and no verified outcome is recorded");
                }
                e.Why = string.Join("; ", why);
                e.Listed = why.Count == 0 && e.BuildListed;
                e.Drawn = e.Listed && e.Form != "none" && CopyDrawn(e.Verdict);
                if (why.Count == 0 && !e.BuildListed) e.Why = e.Verdict.Reason ?? "not listed by the build";
            }

            foreach (GatedValue v in Values2026) v.Drawn = CopyDrawn(v.Verdict);
            foreach (TariffRow r in Tariffs) r.Drawn = r.Verdict == null ? !InGateYear(r.Date) : CopyDrawn(r.Verdict);
        }

        /// <summary>The gate's own item passes (verified, re-read on or after the as-of date when dated 2026): an inline copy
        /// never outruns its item, and a copy whose item the gate does not hold, or that names none, fails (fail closed).</summary>
        bool GatePasses(string id) => id != null && gateById.TryGetValue(id, out Verdict v) && v.Passes(AsOf);

        /// <summary>The gate's own item passes and the build drew it (an event's item is drawn with its event).</summary>
        bool GateDraws(string id) => GatePasses(id) && gateById[id].Drawn;

        /// <summary>A copy passes the gate as of <see cref="AsOf"/>, and so does its gate item (whether or not anything is drawn).</summary>
        bool CopyPasses(Verdict copy) => copy.Passes(AsOf) && GatePasses(copy.Id);

        /// <summary>A copy may be drawn: the build drew it and its gate item, and both pass.</summary>
        bool CopyDrawn(Verdict copy) => copy.Drawn && copy.Passes(AsOf) && GateDraws(copy.Id);

        /// <summary>
        /// Whether a stored value may be drawn: one with a verdict (Treasuries, COFER) when the build drew it and its gate
        /// item and both pass; one without when it is not dated 2026 or later (a 2026 value that lost its verdict is never
        /// drawn). Allocation-free, for any thread.
        /// </summary>
        public bool Drawable(Measure m) => m != null && m.Has && (m.Gate == null ? !InGateYear(m.Year) : CopyDrawn(m.Gate));

        string NotPassing(Verdict v, string what)
        {
            if (!v.Verified) return $"{what} is unverified" + (string.IsNullOrEmpty(v.Reason) ? "" : $" ({v.Reason})");
            return $"{what} was re-read on {v.VerifiedOn ?? "no date"}, before the as-of date {AsOf}";
        }

        /// <summary>Why a copy fails <see cref="CopyPasses"/>: its own verdict, or its gate item's (empty when both pass).</summary>
        string Failing(Verdict copy, string what)
        {
            if (!copy.Passes(AsOf)) return NotPassing(copy, what);
            if (copy.Id == null) return $"{what} names no gate item";
            Verdict item = GateItem(copy.Id);
            if (item == null) return $"{what} names gate item {copy.Id}, which the gate does not hold";
            return item.Passes(AsOf) ? "" : NotPassing(item, $"{(what == "it" ? "its" : what + "'s")} gate item {item.Id}");
        }

        /// <summary>Why a copy the build drew fails <see cref="CopyDrawn"/> (empty when it may be drawn).</summary>
        string NotDrawn(Verdict copy)
        {
            string failing = Failing(copy, "it");
            return failing.Length > 0 || GateDraws(copy.Id) ? failing : $"the build did not draw its gate item {copy.Id}";
        }

        /// <summary>The problem a copy dated 2026 or later without a verdict makes.</summary>
        static string Ungated(string where, string date) => $"{where} is dated {date} but carries no gate verdict: it is not drawn";

        /// <summary>Whether a gate item is drawn by the model's reading: a value or an event as above; a further source or an
        /// outcome with its event.</summary>
        public bool ItemDrawn(Verdict item)
        {
            if (item == null) return false;
            switch (item.Kind)
            {
                case "event":
                    return EventById(item.Id)?.Drawn ?? false;
                case "evidence":
                case "outcome":
                    return (EventById(item.Parent)?.Drawn ?? false) && GatePasses(item.Id);
                default:
                    GatedValue v = Values2026.FirstOrDefault(x => x.Id == item.Id);
                    return v != null ? v.Drawn : GateDraws(item.Id);
            }
        }

        // ------------------------------------------------------------------ checks

        void Check()
        {
            // the row: shares of world output, at market rates and at purchasing power
            double sum = Row.Sum(e => e.Share.Value), sumPpp = Row.Sum(e => e.SharePpp.Value);
            if (!(Math.Abs(sum - 100) <= ShareTolerance)) problems.Add($"the 13 economies and the rest of the world hold {F2(sum)}% of world output, not 100 ± {ShareTolerance}");
            if (!(Math.Abs(sumPpp - 100) <= ShareTolerance)) problems.Add($"the shares at purchasing power sum to {F2(sumPpp)}%, not 100 ± {ShareTolerance}");
            double world = WorldEconomy?.Gdp.Value ?? double.NaN;
            foreach (Economy e in Row)
            {
                if (!(e.Share.Value >= 0) || !(e.SharePpp.Value >= 0)) problems.Add($"{e.Id} has no share of world output on both bases");
                if (e.IsRest) continue;
                double own = e.Gdp.Value / world * 100;
                if (!(Math.Abs(own - e.Share.Value) <= 0.01)) problems.Add($"{e.Id}'s share {F2(e.Share.Value)}% is not its GDP over the world's ({F2(own)}%): one GDP source per economy");
            }

            Economy rest = ById(Rest);
            double the13 = Row.Where(e => !e.IsRest).Sum(e => e.Gdp.Value);
            if (rest != null && !(Math.Abs(world - the13 - rest.Gdp.Value) <= 0.5)) problems.Add($"the rest of the world's GDP {F1(rest.Gdp.Value)} is not the world's less the 13's ({F1(world - the13)})");

            // Germany: inside the EU27, never added (not a block, not a lane end, not a band, not in the totals)
            if (GermanyEconomy != null && GermanyEconomy.Inside != Eu) problems.Add($"Germany is not marked inside the EU27 (inside '{GermanyEconomy.Inside}')");
            if (Order.Contains(Germany) || FileOrder.Contains(Germany) || tradeOrder.Contains(Germany)) problems.Add("Germany is a block of the row or a lane end: it is added to the EU27");
            if (historyBands.Contains(Germany) || HistoryPpp.Shares.ContainsKey(Germany) || HistoryMarket.Shares.ContainsKey(Germany)) problems.Add("Germany is a band of the history wall: it is added to the EU27");
            if (!(Math.Abs(TotalsThe13Gdp - the13) <= 0.5)) problems.Add($"the totals' GDP of the 13 ({F1(TotalsThe13Gdp)}) is not the sum of the 13 ({F1(the13)}): Germany added, or a block missing");
            if (!(Math.Abs(TotalsUsShareGdp - (ById(Us)?.Share.Value ?? double.NaN)) <= 0.01)) problems.Add("the totals line's US share is not the US block's share");
            if (!(Math.Abs(TotalsWorldGdpT - Math.Round(world / 1000, 1, MidpointRounding.AwayFromZero)) <= 0.051)) problems.Add($"the totals' world GDP ${F1(TotalsWorldGdpT)}T is not the world's ({F1(world / 1000)})");

            // the order: computed from the centroid longitudes, the rest of the world at the east end
            if (!FileOrder.SequenceEqual(Order)) problems.Add($"global.json's west-to-east order ({string.Join(" ", FileOrder)}) is not the one its centroid longitudes give ({string.Join(" ", Order)})");
            for (int i = 0; i < Row.Count; i++)
            {
                if (Row[i].FileOrder != i) problems.Add($"{Row[i].Id}'s stored place {Row[i].FileOrder} is not its computed place {i}");
            }

            // the lanes: one per ordered pair of the 13, each with its record; the sets and their shares as the file computed them
            int n13 = tradeOrder.Count;
            if (Lanes.Count != n13 * (n13 - 1)) problems.Add($"{Lanes.Count} lanes, not {n13 * (n13 - 1)} (13 × 12)");
            foreach (Lane l in Lanes)
            {
                if (!(l.Record >= 0)) problems.Add($"lane {l.Key} has no exporter's record");
                if (!(l.Mirror >= 0)) problems.Add($"lane {l.Key} has no importer's record (its mirror)");
            }

            HashSet<string> defaultPairs = new HashSet<string>(DefaultLanes.Select(l => l.Pair), StringComparer.Ordinal);
            if (!defaultPairs.SetEquals(fileDefaultPairs)) problems.Add($"the default set computed ({string.Join(", ", defaultPairs.OrderBy(p => p, StringComparer.Ordinal))}) is not global.json's ({string.Join(", ", fileDefaultPairs.OrderBy(p => p, StringComparer.Ordinal))})");
            if (!(Math.Abs(DefaultShare - fileDefaultShare) <= 0.05)) problems.Add($"the default lanes carry {F2(DefaultShare)}% of world goods exports outside intra-EU trade, global.json says {F2(fileDefaultShare)}%");
            if (!(Math.Abs(AllShare - fileAllShare) <= 0.05)) problems.Add($"all lanes carry {F2(AllShare)}%, global.json says {F2(fileAllShare)}%");
            if (!(Math.Abs(DefaultCarried - fileDefaultTwoWay) <= 0.5)) problems.Add($"the default lanes carry ${F1(DefaultCarried)}B, global.json says ${F1(fileDefaultTwoWay)}B");
            foreach (Lane l in Lanes)
            {
                if (!fileLanes.TryGetValue(l.Pair, out (string set, string role, double twoWay) f)) { problems.Add($"pair {l.Pair} is not among global.json's lanes"); continue; }
                if (f.role != l.Role || (f.set == "default") != l.InDefault) problems.Add($"lane {l.Key} is {l.Role} here, {f.role} ({f.set}) in global.json");
                if (!(Math.Abs(f.twoWay - l.TwoWay) <= 0.05)) problems.Add($"pair {l.Pair} carries {F2(l.TwoWay)} both ways here, {F2(f.twoWay)} in global.json");
            }

            HashSet<string> gaps = new HashSet<string>(Lanes.Where(l => l.GapFlagged).Select(l => l.Key), StringComparer.Ordinal);
            if (!gaps.SetEquals(fileGaps)) problems.Add($"{gaps.Count} lanes have mirror gaps over 15% here, {fileGaps.Count} are flagged in global.json");
            foreach ((string id, double partners, double toRest, double total) r in rowTotals)
            {
                if (!(Math.Abs(r.partners + r.toRest - r.total) <= 0.02)) problems.Add($"{r.id}'s partners + the rest ({F2(r.partners + r.toRest)}) are not its total exports ({F2(r.total)})");
            }

            if (!(WorldExIntraEu > 0)) problems.Add("world goods exports less intra-EU trade is not positive: no lane share can be computed");

            // the 2026 gate: nothing drawn unverified; every drawn 2026 item re-read on or after the as-of date
            if (gateAsOf != AsOf) problems.Add($"the gate's as-of date {gateAsOf} is not the data's {AsOf}");
            foreach (Verdict v in GateItems)
            {
                if (v.Drawn && !v.Passes(AsOf)) problems.Add($"global.json draws {v.Kind} {v.Id}, but {NotPassing(v, "it")}");
            }

            // every copy (PIIE's rows, Section 301, Treasuries, COFER, the 2026 values) against its gate item; and no copy
            // dated 2026 without a verdict
            foreach ((string place, Verdict inline) in inlineVerdicts)
            {
                if (inline.Drawn && !inline.Passes(AsOf)) problems.Add($"global.json draws {place}, but {NotPassing(inline, "it")}");
                if (inline.Id == null) { problems.Add($"{place} carries a verdict but names no gate item: it is not drawn"); continue; }
                if (!gateById.TryGetValue(inline.Id, out Verdict item)) problems.Add($"{place} names gate item {inline.Id}, which the gate does not hold");
                else if (item.Status != inline.Status || (inline.Drawn && !item.Drawn)) problems.Add($"{place} says {inline.Status}{(inline.Drawn ? ", drawn" : "")}; its gate item {item.Id} says {item.Status}{(item.Drawn ? ", drawn" : "")}");
            }

            problems.AddRange(ungated);

            int verified = GateItems.Count(v => v.Verified);
            if (verified != GateVerified || GateItems.Count - verified != GateUnverified) problems.Add($"the gate counts {GateVerified} verified and {GateUnverified} unverified; its items say {verified} and {GateItems.Count - verified}");
            HashSet<string> pending = new HashSet<string>(PendingItems.Select(p => p.Id), StringComparer.Ordinal);
            HashSet<string> unverified = new HashSet<string>(GateItems.Where(v => !v.Verified && v.Parent == null).Select(v => v.Id), StringComparer.Ordinal);
            if (!pending.SetEquals(unverified)) problems.Add($"the pending list ({string.Join(", ", pending)}) is not the unverified items ({string.Join(", ", unverified)})");

            // events: no drawn event past its stated end or expiry date without a verified outcome (as of the data's date); each
            // copy agrees with its gate items
            foreach (Event e in Events)
            {
                // (a drawn copy whose gate item the build did not draw is CheckEventCopies' disagreement)
                if (e.Verdict.Drawn && !e.Listed) problems.Add($"global.json draws event {e.Id}, but {e.Why}");
                else if (e.Verdict.Drawn && e.Form == "none") problems.Add($"global.json draws event {e.Id}, but it has no mark (form none)");
                else if (e.BuildListed && !e.Listed) problems.Add($"global.json lists event {e.Id}, but {e.Why}");
                CheckEventCopies(e);
            }

            foreach (GatedValue v in Values2026)
            {
                if (v.Verdict.Drawn && !v.Drawn) problems.Add($"global.json draws value {v.Id}, but {NotDrawn(v.Verdict)}");
            }

            if (TariffsLatest == null) problems.Add("no row of PIIE's tariff chart may be drawn: the US-China walls have no rate");

            // history: both bases sum to 100 within 0.5 every year, 14 bands, one per year
            foreach (History h in new[] { HistoryPpp, HistoryMarket })
            {
                if (h.Years.Length == 0) { problems.Add($"the {h.Basis} history has no years"); continue; }
                if (!(h.WorstSum <= HistoryTolerance)) problems.Add($"the {h.Basis} history's bands sum to 100 only within {F2(h.WorstSum)} points (allowed {HistoryTolerance})");
                if (h.Shares.Count != Order.Count || Order.Any(id => !h.Shares.ContainsKey(id))) problems.Add($"the {h.Basis} history's bands ({string.Join(" ", h.Shares.Keys)}) are not the row's");
                for (int i = 1; i < h.Years.Length; i++)
                {
                    if (h.Years[i] != h.Years[i - 1] + 1) { problems.Add($"the {h.Basis} history skips from {h.Years[i - 1]} to {h.Years[i]}"); break; }
                }

                foreach (KeyValuePair<string, double[]> kv in h.Shares)
                {
                    if (kv.Value.Length != h.Years.Length) problems.Add($"the {h.Basis} history's {kv.Key} band has {kv.Value.Length} values for {h.Years.Length} years");
                }
            }

            if (HistoryPpp.First != WorldLayout.HistoryFirstYear) problems.Add($"the purchasing-power history starts in {HistoryPpp.First}, not {WorldLayout.HistoryFirstYear}");

            // what the build printed
            foreach (BuildCheck c in BuildChecks)
            {
                if (!c.Ok) problems.Add($"global.json's build check '{c.Name}' failed: {c.Detail}");
            }

            // the geometry: 500 units, the US at u 0, every lane its record times the scale
            problems.AddRange(Layout.Problems());
            problems.AddRange(PppLayout.Problems());

            // an item seen twice (its own entry and the gate's) is one problem
            List<string> once = problems.Distinct().ToList();
            problems.Clear();
            problems.AddRange(once);
        }

        /// <summary>
        /// An event's copies against their gate items, by the build's rule: the event's copy is verified when its own item and
        /// every further source's item are (a source that fails makes it unverified even when its own quote passed); each
        /// further source's copy and the outcome's state their items' verdicts.
        /// </summary>
        void CheckEventCopies(Event e)
        {
            Verdict item = GateItem(e.Id);
            if (item == null)
            {
                problems.Add($"event {e.Id} is not among the gate's items");
                return;
            }

            List<string> unverified = new List<string>();
            if (!item.Verified) unverified.Add($"its gate item {item.Id} says {item.Status ?? "nothing"}");
            foreach (Evidence x in e.Evidence)
            {
                Verdict xi = GateItem(x.Verdict.Id);
                if (xi == null) problems.Add($"event {e.Id}'s {x.Role} source is not among the gate's items ({x.Verdict.Id})");
                else if (xi.Status != x.Verdict.Status) problems.Add($"event {e.Id}'s {x.Role} source says {x.Verdict.Status}; its gate item {xi.Id} says {xi.Status}");
                if (xi == null || !xi.Verified) unverified.Add($"its {x.Role} source's gate item {x.Verdict.Id} says {xi?.Status ?? "nothing"}");
            }

            if (e.Verdict.Verified && unverified.Count > 0) problems.Add($"event {e.Id} says verified, but {string.Join("; ", unverified)}");
            else if (!e.Verdict.Verified && unverified.Count == 0)
            {
                problems.Add($"event {e.Id} says {e.Verdict.Status ?? "nothing"}, but its gate item{(e.Evidence.Length > 0 ? " and its further sources' are" : " is")} verified");
            }

            // the build draws an event and its item together
            if (e.Verdict.Drawn != item.Drawn) problems.Add($"event {e.Id} says {(e.Verdict.Drawn ? "drawn" : "not drawn")}; its gate item says {(item.Drawn ? "drawn" : "not drawn")}");

            if (e.Outcome == null) return;
            Verdict oi = GateItem(e.Outcome.Verdict.Id);
            if (oi == null) problems.Add($"event {e.Id}'s outcome is not among the gate's items ({e.Outcome.Verdict.Id})");
            else if (oi.Status != e.Outcome.Verdict.Status || (e.Outcome.Recorded && !oi.Verified))
            {
                problems.Add($"event {e.Id}'s outcome says {e.Outcome.Verdict.Status}{(e.Outcome.Recorded ? ", recorded" : "")}; its gate item {oi.Id} says {oi.Status}");
            }
        }

        // ------------------------------------------------------------------ the log

        List<string> Describe()
        {
            WorldLayout g = Layout;
            List<string> lines = new List<string>
            {
                $"{Year}: {Row.Count} blocks, shares {F1(Row.Sum(e => e.Share.Value))}, row {F1(g.Right - g.Left)} units, order {string.Join(" ", Order)}",
                "row (units, west to east): " + string.Join(", ", g.Blocks.Select(b => $"{b.Id} {F1(b.Width)}")) +
                $"; u {F1(g.Left)}…{F1(g.Right)}, the US at u {F1(g.BlockOf(Us)?.Centre ?? float.NaN)} ({F1(g.BlockOf(Us)?.U0 ?? float.NaN)}…{F1(g.BlockOf(Us)?.U1 ?? float.NaN)}); " +
                $"camera ({F1(g.CameraTarget.x)}, {F1(g.CameraTarget.y)}) pitch {F1(WorldLayout.CameraPitch)}° at {F1(WorldLayout.CameraDistance)} = {F2(WorldLayout.PixelsPerUnit(WorldLayout.CameraDistance))} px/unit at 1080p; " +
                $"at purchasing power: " + string.Join(", ", PppLayout.Blocks.Select(b => $"{b.Id} {F1(b.Width)}")),
                $"lanes: default {DefaultLanes.Count} ({DefaultLanes.Count(l => l.Role == "subject")} US + {DefaultLanes.Count(l => l.Role != "subject")} others) carry {F1(DefaultShare)}% " +
                $"of world goods exports outside intra-EU trade (${F2(DefaultCarried / 1000)}T of ${F2(WorldExIntraEu / 1000)}T); All flows {Lanes.Count} lanes carry {F1(AllShare)}% " +
                $"(${F2(AllCarried / 1000)}T); intra-EU ${F2(IntraEu.Value / 1000)}T is a label, never an arc; widest {Widest(DefaultLanes)}; ports overlap on " +
                (g.Crowded.Count > 0 ? string.Join(" ", g.Crowded) + " (All flows wider than the block)" : "no block") +
                "; pairs wider than their block, overhanging toward the partner: " + (g.Overhanging.Count > 0 ? string.Join(", ", g.Overhanging) : "none") +
                "; at purchasing power: " + (PppLayout.Overhanging.Count > 0 ? string.Join(", ", PppLayout.Overhanging) : "none"),
                $"anchors: {g.Anchors.Count(a => a.Kind == WorldLayout.AnchorKind.Wall)} walls ({g.Anchors.Count(a => a.Kind == WorldLayout.AnchorKind.Wall && !double.IsNaN(a.Rate))} with an average rate), " +
                $"{g.Anchors.Count(a => a.Kind == WorldLayout.AnchorKind.Valve)} valves, {g.Anchors.Count(a => a.Kind == WorldLayout.AnchorKind.Cut)} cuts, " +
                $"{g.Anchors.Count(a => a.Kind == WorldLayout.AnchorKind.Reroute)} reroutes; " +
                (g.Unanchored.Count > 0 ? "not on a lane: " + string.Join(", ", g.Unanchored) : "every drawn wall, valve and cut is on a lane") +
                (TariffsLatest != null ? $"; US-China walls at PIIE's {TariffsLatest.Date} averages (US on China {F2(TariffsLatest.UsOnChina)}%, China on the US {F2(TariffsLatest.ChinaOnUs)}%)" : "")
            };

            // every 2026 item, drawn or excluded
            List<Verdict> dated = GateItems.Where(v => v.Dated2026).ToList();
            List<Verdict> drawn = dated.Where(ItemDrawn).ToList(), excluded = dated.Where(v => !ItemDrawn(v)).ToList();
            lines.Add($"2026 gate (as of {AsOf}, Jev {JevModel}, threshold {F2(GateThreshold)}): {dated.Count} items dated 2026, {drawn.Count} drawn, {excluded.Count} excluded; " +
                      $"{GateItems.Count(v => v.Verified)} of {GateItems.Count} gated items verified");
            foreach (IGrouping<string, Verdict> kind in drawn.GroupBy(v => v.Kind).OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                lines.Add($"2026 drawn {kind.Key} ({kind.Count()}): " + string.Join(", ", kind.Select(v => $"{v.Id} {v.VerifiedOn}")));
            }

            foreach (Verdict v in excluded) lines.Add($"2026 excluded {v.Kind} {v.Id}: {WhyNotDrawn(v)}");

            // every event, and what expires soon
            List<Event> marked = Events.Where(e => e.Drawn).ToList();
            lines.Add($"events: {Events.Count} dated; {marked.Count} drawn (" +
                      string.Join(", ", marked.GroupBy(e => e.Form).OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Count()}")) +
                      $"; 'tick' is the event strip only), {Events.Count(e => e.Listed && !e.Drawn)} listed without a mark, {Events.Count(e => !e.Listed)} not drawn: " +
                      string.Join("; ", Events.Where(e => !e.Listed).Select(e => $"{e.Id} ({e.Why})")));
            List<Event> soon = Events.Where(e => e.ExpiresSoon).ToList();
            lines.Add(soon.Count == 0
                ? $"expiring within {ExpiryFlagDays} days of {AsOf}: none"
                : $"expiring within {ExpiryFlagDays} days of {AsOf}: " + string.Join(", ", soon.Select(e => $"{e.Id} {e.Expires} ({(e.Drawn ? "drawn" : "not drawn")}{(e.ExpiresStated ? "" : ", ≈ date")})")) +
                  " (drawn as of the data's date; a rebuild after an expiry drops the event unless a verified outcome is recorded)");
            lines.Add($"history: purchasing power {HistoryPpp.First}-{HistoryPpp.Last} ({HistoryPpp.Shares.Count} bands, sums within {F2(HistoryPpp.WorstSum)} of 100, " +
                      $"splice {HistoryPpp.SpliceYear}), market rates {HistoryMarket.First}-{HistoryMarket.Last} (sums within {F2(HistoryMarket.WorstSum)}); stacked {string.Join(" ", Order)} from the bottom");
            lines.Add($"built in {F1(BuildMs)} ms; Problems(): " + (problems.Count == 0 ? "none" : problems.Count + " (logged as errors)"));
            return lines;
        }

        /// <summary>Why a gate item is not drawn: its event's reason, its own verdict, its event not drawn, or the build's choice.</summary>
        string WhyNotDrawn(Verdict v)
        {
            Event e = v.Kind == "event" ? EventById(v.Id) : null;
            if (e != null) return e.Why.Length > 0 ? e.Why : e.Listed ? $"listed only (form {e.Form})" : "not drawn by the build";
            if (!v.Passes(AsOf)) return NotPassing(v, "it");
            if (v.Parent != null && !(EventById(v.Parent)?.Drawn ?? false)) return $"its event {v.Parent} is not drawn";
            return "not drawn by the build" + (string.IsNullOrEmpty(v.Reason) ? "" : $" ({v.Reason})");
        }

        static string Widest(IEnumerable<Lane> lanes)
        {
            Lane w = lanes.OrderByDescending(l => l.Record).FirstOrDefault();
            return w == null ? "none" : $"{w.Key} {F2(w.Record * WorldLayout.LaneUnitsPerBillion)} units (${F1(w.Record)}B)";
        }

        // ------------------------------------------------------------------ facts

        /// <summary>
        /// Publishes the numbers a card, the narration or a panel quotes under <c>world.*</c>, each rounded as it is printed
        /// (shares to 0.1 point, keystone shares included: a share the source gives in whole percent, "69%", is stored whole
        /// and stays so; $T to 0.1, lanes to whole $B or 0.1 below $10B, cuts to 0.1), with its global.json field and basis.
        /// A 2026 value is published only when it may be drawn, with "verified on" in its basis.
        /// </summary>
        void PublishFacts()
        {
            const string measured = "measured", computed = "computed from measured values";
            string asOf = $"as of {AsOf} (global.json)";
            ModelFacts.Set("world.asOf", AsOf ?? "", "", "global.json asOf: the 2026 gate's last re-reading", "the data's date; never the clock");
            ModelFacts.Set("world.year", Year, "", "global.json baseYear", measured);
            Economy w = WorldEconomy;
            if (w != null) ModelFacts.Set("world.gdp", R(w.Gdp.Value / 1000, 1), "$T", "global.json economies.WLD.gdp (IMF WEO Oct 2026, NGDPD 2024)", measured + ", market exchange rates");
            ModelFacts.Set("world.blocks", Row.Count, "blocks", "WorldModel: the 13 economies and the rest of the world", computed);
            ModelFacts.Set("world.rowUnits", R(Layout.Right - Layout.Left, 1), "units", "WorldLayout: 5 units per 1% of world output", computed);
            ModelFacts.Set("world.order", string.Join(" ", Order), "", "WorldModel: centroid longitudes (global.json economies.*.centroid, Natural Earth pop_max)", computed + "; rule: west → east by where people live");
            double the13 = Row.Where(e => !e.IsRest).Sum(e => e.Gdp.Value), people13 = Row.Where(e => !e.IsRest).Sum(e => e.Population.Value);
            if (w != null)
            {
                ModelFacts.Set("world.the13.share", R(the13 / w.Gdp.Value * 100, 1), "%", "global.json economies.*.gdp (IMF WEO), the 13 over WLD", computed);
                ModelFacts.Set("world.the13.people", R(people13 / w.Population.Value * 100, 1), "%", "global.json economies.*.population (WDI; Taiwan IMF), the 13 over WLD", computed);
            }

            foreach (Economy e in Row)
            {
                string src = $"global.json economies.{e.Id}";
                ModelFacts.Set($"world.share.{e.Id}", R(e.Share.Value, 1), "%", src + ".shareWorld (IMF WEO NGDPD 2024)", e.IsRest ? computed + ": the world less the 13" : computed);
                ModelFacts.Set($"world.sharePpp.{e.Id}", R(e.SharePpp.Value, 1), "%", src + ".sharePpp (IMF WEO PPPSH 2024)", e.IsRest ? computed + ": 100 less the 13" : measured);
                ModelFacts.Set($"world.gdp.{e.Id}", R(e.Gdp.Value / 1000, e.Gdp.Value < 1000 ? 2 : 1), "$T", src + ".gdp (IMF WEO NGDPD 2024)", e.IsRest ? computed + ": the world less the 13" : measured);
                if (w != null) ModelFacts.Set($"world.people.{e.Id}", R(e.Population.Value / w.Population.Value * 100, 1), "%", src + ".population over WLD", computed);
                if (e.IsRest) ModelFacts.Set("world.rest.economies", e.Economies, "economies", src + ".economies (IMF WEO NGDPD 2024)", computed);
            }

            Economy us = ById(Us);
            if (us != null)
            {
                ModelFacts.Set("world.military.USA", R(us.MilitaryShare.Value, 1), "%", "global.json economies.USA.military.shareWorld (SIPRI v1.2, 2024)", computed);
                ModelFacts.Set("world.military.USA.T", R(us.MilitaryUsd.Value / 1000, 1), "$T", "global.json economies.USA.military.usd (SIPRI v1.2, 2024)", measured);
                ModelFacts.Set("world.military.USA.pctGdp", R(us.MilitaryPctGdp.Value, 1), "%", "global.json economies.USA.military.pctGdp (SIPRI v1.2, 2024)", measured);
                ModelFacts.Set("world.niip.USA", R(us.NiipUsd.Value / 1000, 1), "$T", "global.json economies.USA.niip.usd (IMF IIP, end-2024)", measured);
                ModelFacts.Set("world.currentAccount.USA", R(us.CurrentAccountUsd.Value / 1000, 1), "$T", "global.json economies.USA.currentAccount.usd (IMF WEO BCA 2024)", measured);
            }

            // lanes: the sets and their shares, every default lane both records
            ModelFacts.Set("world.lanes.default", DefaultLanes.Count, "lanes", "WorldModel: every US pair both ways and the six largest other pairs", computed);
            ModelFacts.Set("world.lanes.default.us", DefaultLanes.Count(l => l.Role == "subject"), "lanes", "WorldModel default set", computed);
            ModelFacts.Set("world.lanes.default.others", DefaultLanes.Count(l => l.Role != "subject"), "lanes", "WorldModel default set", computed);
            ModelFacts.Set("world.lanes.default.share", R(DefaultShare, 1), "%", "WorldModel: global.json trade.matrix over trade.worldGoodsExports less trade.intraEU", computed + ", exporters' records, goods 2024");
            ModelFacts.Set("world.lanes.all", Lanes.Count, "lanes", "WorldModel: every pair of the 13 both ways", computed);
            ModelFacts.Set("world.lanes.all.share", R(AllShare, 1), "%", "WorldModel: global.json trade.matrix over trade.worldGoodsExports less trade.intraEU", computed + ", exporters' records, goods 2024");
            ModelFacts.Set("world.trade.intraEU", R(IntraEu.Value / 1000, 2), "$T", "global.json trade.intraEU (IMF IMTS 2024)", computed + "; a label on the EU block, never an arc");
            foreach (Lane l in DefaultLanes)
            {
                ModelFacts.Set($"world.trade.{l.FactKey}", Bn(l.Record), "$B", $"global.json trade.matrix {l.Key} ({l.RecordField})",
                    (l.PartnerBuilt ? "measured by partners (Russia's cells are partner records)" : measured) + ", the exporter's record, goods 2024");
                ModelFacts.Set($"world.trade.{l.FactKey}.mirror", Bn(l.Mirror), "$B", $"global.json trade.mirror {l.Key} ({l.MirrorField})",
                    (l.MirrorPartnerBuilt ? "measured by partners" : measured) + ", the importer's record, goods 2024");
            }

            foreach (Overlay o in Overlay2025.Values)
            {
                ModelFacts.Set($"world.us2025.{o.Partner}", Bn(o.Imports2025), "$B", $"global.json trade.overlay2025.{o.Partner} ({o.Field})", measured + ", US customs records 2025");
                ModelFacts.Set($"world.us2025.{o.Partner}.change", R(o.ChangePct, 1), "%", $"global.json trade.overlay2025.{o.Partner}.changePct", computed + ", US customs records 2024 → 2025");
            }

            // chokepoints: every holder's share of every keystone, to 0.1 point (gallium 98.9, cobalt 74.8, crude 16.2 as spec
            // 6.5 prints them; "69%" as the source gives it)
            foreach (Chokepoint c in Chokepoints)
            {
                foreach (Holder h in c.Holders)
                {
                    ModelFacts.Set($"world.keystone.{c.Id}.{h.Id}", R(h.Value, 1), "%", $"global.json chokepoints.{c.Id} ({c.Source}, {c.Year})",
                        h.Computed != null ? computed + ": " + h.Computed : measured);
                }
            }

            // conflict: PIIE's averages (the US-China walls), the Russia cuts and reroutes, chip tools
            if (TariffsFirst != null)
            {
                ModelFacts.Set("world.tariff.usOnChina.2018", R(TariffsFirst.UsOnChina, 1), "%", $"global.json tariffs.series {TariffsFirst.Date} (PIIE)", measured + "; " + TariffCaveat);
                ModelFacts.Set("world.tariff.chinaOnUs.2018", R(TariffsFirst.ChinaOnUs, 1), "%", $"global.json tariffs.series {TariffsFirst.Date} (PIIE)", measured + "; " + TariffCaveat);
            }

            if (TariffsLatest != null)
            {
                string basis = measured + "; " + TariffCaveat + (TariffsLatest.Verdict != null ? $"; verified on {TariffsLatest.Verdict.VerifiedOn}, {asOf}" : "");
                ModelFacts.Set("world.tariff.usOnChina", R(TariffsLatest.UsOnChina, 1), "%", $"global.json tariffs.series {TariffsLatest.Date} (PIIE)", basis);
                ModelFacts.Set("world.tariff.chinaOnUs", R(TariffsLatest.ChinaOnUs, 1), "%", $"global.json tariffs.series {TariffsLatest.Date} (PIIE)", basis);
                ModelFacts.Set("world.tariff.date", TariffsLatest.Date, "", "global.json tariffs.series: the latest row that may be drawn", basis);
            }

            foreach (WorldLayout.LaneAnchor a in Layout.Anchors.Where(a => a.Kind == WorldLayout.AnchorKind.Cut || a.Kind == WorldLayout.AnchorKind.Reroute))
            {
                // to 0.1 either side, as the cut is read ("$29.6B in 2021 to $3.0B")
                string key = $"world.{(a.Kind == WorldLayout.AnchorKind.Cut ? "cut" : "reroute")}.{a.Lane.Trade.FactKey}";
                ModelFacts.Set($"{key}.{a.BeforeYear}", R(a.Before, 1), "$B", $"global.json trade.bilateral {a.Lane.Trade.Importer}-{a.Lane.Trade.Exporter}: {a.Record}", measured + ", the importer's record");
                ModelFacts.Set($"{key}.{a.AfterYear}", R(a.After, 1), "$B", $"global.json trade.bilateral {a.Lane.Trade.Importer}-{a.Lane.Trade.Exporter}: {a.Record}", measured + ", the importer's record");
            }

            if (ChipTools2021.Has) ModelFacts.Set("world.chipTools.2021", R(ChipTools2021.Value, 2), "$B", "global.json trade.chipTools.2021 (UN Comtrade, HS 8486)", measured);
            if (ChipTools2025.Has) ModelFacts.Set("world.chipTools.2025", R(ChipTools2025.Value, 2), "$B", "global.json trade.chipTools.2025 (UN Comtrade, HS 8486)", measured);

            // money and debt: Treasuries (2026, gated) and the threads they make
            foreach (WorldLayout.Block b in Layout.Blocks)
            {
                if (b.Threads < 0) continue;
                Measure m = b.Economy.Treasuries;
                bool rest = b.Economy.IsRest;
                string src = rest ? "WorldModel: global.json finance.treasuriesTotal less the 12 holders (TIC Table 3, end-Aug 2026)" : $"global.json economies.{b.Id}.treasuries (TIC Table 3, end-Aug 2026)";
                string basis = (rest ? computed : measured) + $"; custodial: TIC records the custodian's country; verified on {(rest ? TreasuriesTotal.Gate?.VerifiedOn : m.Gate?.VerifiedOn)}, {asOf}";
                ModelFacts.Set($"world.treasuries.{b.Id}", R(b.TreasuriesBn, 0), "$B", src, basis);
                ModelFacts.Set($"world.threads.{b.Id}", b.Threads, "threads", src + $"; 1 thread = ${F0(ThreadBillions)}B", basis);
            }

            // history: each band in the first and last years on both bases, and the junction caption
            foreach (History h in new[] { HistoryPpp, HistoryMarket })
            {
                foreach (string id in Order)
                {
                    foreach (int y in new[] { h.First, h.Last })
                    {
                        double v = h.At(id, y);
                        if (!double.IsNaN(v)) ModelFacts.Set($"world.history.{h.Basis}.{id}.{y}", R(v, 1), "%", $"global.json history.{h.Basis} ({h.Source})", h.Basis == "ppp" ? measured + " at purchasing power" : measured + " at market rates");
                    }
                }
            }

            // the gate
            ModelFacts.Set("world.gate.verified", GateItems.Count(v => v.Verified), "items", "global.json gate.items", "the 2026 gate's verdicts, " + asOf);
            ModelFacts.Set("world.gate.unverified", GateItems.Count(v => !v.Verified), "items", "global.json gate.items (pending verification: not drawn, not even dashed)", "the 2026 gate's verdicts, " + asOf);
            ModelFacts.Set("world.gate.drawn2026", GateItems.Count(v => v.Dated2026 && ItemDrawn(v)), "items", "WorldModel: global.json gate.items dated 2026", computed + ", " + asOf);
            ModelFacts.Set("world.events.drawn", Events.Count(e => e.Drawn), "events", "WorldModel: global.json events", computed + ", " + asOf);
            foreach (Event e in Events.Where(e => e.ExpiresSoon))
            {
                ModelFacts.Set($"world.expires.{e.Id}", e.Expires, "", $"global.json events.{e.Id}.expires", (e.ExpiresStated ? "stated in a verified quote" : "≈ the research's date") + ", " + asOf);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// A number rounded half away from zero as its stored decimals read (78.35 → 78.4): rounded in decimal, so the
        /// binary 78.3499... does not round down, and Mono and .NET agree.
        /// </summary>
        static double R(double v, int digits) =>
            double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e15 ? v : (double)Math.Round((decimal)v, digits, MidpointRounding.AwayFromZero);

        /// <summary>US$ bn as printed: whole above $10B, else to 0.1.</summary>
        static double Bn(double v) => R(v, Math.Abs(v) < 10 ? 1 : 0);

        static string F0(double v) => v.ToString("0", Ci);
        static string F1(double v) => v.ToString("0.0", Ci);
        static string F2(double v) => v.ToString("0.00", Ci);

        static IEnumerable<JObject> Objects(JToken t) => t is JArray a ? a.OfType<JObject>() : Enumerable.Empty<JObject>();

        /// <summary>A member of an object (null when the token is missing, null or not an object: indexing a JSON null throws).</summary>
        static JToken Child(JToken t, string key) => t is JObject o ? o[key] : null;

        /// <summary>Row i, column j of an array of arrays (null when either is missing).</summary>
        static JToken Cell(JArray rows, int i, int j) => i < rows.Count && rows[i] is JArray r && j < r.Count ? r[j] : null;

        static IReadOnlyList<string> Strings(JToken t) => t is JArray a ? a.Select(Str).Where(s => s != null).ToList() : new List<string>();

        static string Str(JToken t) => t is JValue v && v.Value != null ? Convert.ToString(v.Value, Ci) : null;

        static bool Bool(JToken t) => t is JValue v && v.Type == JTokenType.Boolean && (bool)v.Value;

        static double Num(JToken t) => Num(t, double.NaN);

        static double Num(JToken t, double fallback)
        {
            if (!(t is JValue v) || v.Value == null) return fallback;
            switch (v.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return Convert.ToDouble(v.Value, Ci);
                case JTokenType.String:
                    return double.TryParse((string)v.Value, NumberStyles.Float, Ci, out double d) ? d : fallback;
                default:
                    return fallback;
            }
        }

        /// <summary>A full ISO date (YYYY-MM-DD).</summary>
        public static bool IsDate(string s) =>
            s != null && s.Length == 10 && DateTime.TryParseExact(s, "yyyy-MM-dd", Ci, DateTimeStyles.None, out _);

        /// <summary>Whether a date ("2026-08-22", "2026-08", "2026-Q2") falls in <see cref="GateYear"/> or later (read in place:
        /// <see cref="Drawable"/> allocates nothing).</summary>
        public static bool InGateYear(string date)
        {
            if (date == null || date.Length < 4) return false;
            int year = 0;
            for (int i = 0; i < 4; i++)
            {
                char c = date[i];
                if (c < '0' || c > '9') return false;
                year = year * 10 + (c - '0');
            }

            return year >= GateYear;
        }

        /// <summary>Whether ISO date <paramref name="a"/> is strictly before <paramref name="b"/>.</summary>
        public static bool Before(string a, string b) => a != null && b != null && string.CompareOrdinal(a, b) < 0;

        /// <summary>Whether ISO date <paramref name="a"/> is on or after <paramref name="b"/> (false when either is missing).</summary>
        public static bool OnOrAfter(string a, string b) => a != null && b != null && string.CompareOrdinal(a, b) >= 0;

        /// <summary>Days from ISO date <paramref name="from"/> to <paramref name="to"/> (both read from the data).</summary>
        static double Days(string from, string to) =>
            (DateTime.ParseExact(to, "yyyy-MM-dd", Ci) - DateTime.ParseExact(from, "yyyy-MM-dd", Ci)).TotalDays;
    }
}
