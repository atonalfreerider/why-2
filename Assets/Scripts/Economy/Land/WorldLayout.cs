using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where M2, "One economy among many", stands in the stage plane (MODALITIES.md 6.1-6.6; stage coordinates: u to the
    /// right, v up, one unit one world unit), computed from the <see cref="WorldModel"/> on one basis (market exchange
    /// rates, or purchasing power for the PPP toggle and the history variant):
    /// <list type="bullet">
    /// <item>the row: one block per economy, west to east in the model's computed order, each as wide as its share of world
    /// output (<see cref="UnitsPerPercent"/>: 100% = 500 units), touching (the 0.6-unit separators are drawn over the
    /// joins), the rest of the world at the east end; at market rates the US is centred at u 0, and both bases share the
    /// row's west end, so the 0-100% rule and the history wall stay put when the blocks morph; each block's value-added
    /// strata, base, revenue line, military stratum, net IIP bar and Treasury threads;</item>
    /// <item>the lanes: one arch per pair above the row (apex 0.22 × span + 10), its two lanes concentric beside the arch's
    /// spine, the eastbound one outside; each as wide as the exporter's record at <see cref="LaneUnitsPerBillion"/>. Lanes
    /// land on ports along each block's top, ordered so arches nest (the nearest partners at the block's sides, the farthest
    /// in its middle), a pair wider than its block overhanging it toward the partner, never past the row's ends; one
    /// geometry for every lane of "All flows", so the default set, "All flows" and the conflict marks share their paths
    /// when the variant changes;</item>
    /// <item>the anchors of the conflict marks on the lanes of drawn events only: tariff walls at the importer's end
    /// (rate × 0.5 units), export-control valves at the exporter's end, sanction cuts mid-lane with the importer's record
    /// before the event as the ghost and in the base year at the break (the change the label prints), and the rerouted
    /// lanes; Germany's marks stand on the EU27's lanes;</item>
    /// <item>the keystones of the chokepoints layer over their main holders' blocks, and the history wall (u from the row's
    /// west end at 6.75 units a year from 1950, v −230 to −110 for 0-100%), bands stacked west to east from the bottom.</item>
    /// </list>
    /// The drawing's choices are named here (the gap between a pair's lanes, the ports' spacing, where a wall or a valve
    /// sits along its lane) and printed by the layers as "assumed". Pure and immutable: built on any thread with the model.
    /// </summary>
    public sealed class WorldLayout
    {
        /// <summary>Stage units per 1% of world output (spec 6.1): the row is a 100% bar of 500 units.</summary>
        public const float UnitsPerPercent = 5f;

        /// <summary>The row's width (units).</summary>
        public const float RowUnits = 100 * UnitsPerPercent;

        /// <summary>A block's height: 100% of the economy's value added (spec 6.2); the row stands on v 0.</summary>
        public const float BlockHeight = 32f;

        /// <summary>The dark lines over the joins between blocks (units).</summary>
        public const float SeparatorWidth = 0.6f;

        /// <summary>The base's depth below the block per 1% of GDP the state spends (spec 6.2: US 15.0, EU 19.6, Taiwan 6.3);
        /// the revenue line and the military stratum take the same scale.</summary>
        public const float BaseUnitsPerPercent = 0.4f;

        /// <summary>Lane width: 1 unit = $100B of goods a year (spec 6.3).</summary>
        public const float LaneUnitsPerBillion = 0.01f;

        /// <summary>An arch's apex above the row's top: 0.22 × its span + 10 units (spec 6.3).</summary>
        public const float ApexPerSpan = 0.22f, ApexBase = 10f;

        /// <summary>The gap between a pair's two lanes (units; a drawing choice).</summary>
        public const float LaneGap = 0.4f;

        /// <summary>Ports on a block's top: the gap between pairs (the widest and narrowest, units), the margin kept from the
        /// block's sides, and the share of the block's width the ports may spread over (drawing choices).</summary>
        public const float PortGapMax = 0.8f, PortGapMin = 0.15f, PortMargin = 0.15f, PortFill = 0.9f;

        /// <summary>A tariff wall's height: 1 unit per 2 percentage points of the average rate (spec 6.4).</summary>
        public const float WallUnitsPerPoint = 0.5f;

        /// <summary>A net IIP bar: 1 unit = $500B (spec 6.5).</summary>
        public const float NiipUnitsPerBillion = 1f / 500f;

        /// <summary>The history wall (spec 6.6): its first year, units per year, and its bottom and top (v) for 0 and 100%.</summary>
        public const int HistoryFirstYear = 1950;

        public const float HistoryUnitsPerYear = 6.75f, HistoryBottom = -230f, HistoryTop = -110f;

        /// <summary>The nominal camera at 1080p (spec 9.1): target v, pitch (degrees) and distance for the row, and the history
        /// wall's target v and pitch; the target's u is the row's middle. FOV 45° vertical.</summary>
        public const float CameraV = 40f, CameraPitch = 8f, CameraDistance = 435f, HistoryCameraV = -170f, HistoryCameraPitch = 0f;

        /// <summary>Points along each lane (equal steps of its length).</summary>
        public const int LaneSamples = 64;

        /// <summary>Where marks sit along a lane (shares of its length from the exporter): walls from
        /// <see cref="WallT"/> back toward the exporter in steps of <see cref="WallStep"/>; valves spread between
        /// <see cref="ValveFrom"/> and <see cref="ValveTo"/>; cuts and reroutes at the middle (drawing choices).</summary>
        public const float WallT = 0.92f, WallStep = 0.06f, ValveFrom = 0.12f, ValveTo = 0.4f, CutT = 0.5f;

        /// <summary>The spine's samples along an arch (for even steps along its length and the lanes beside it).</summary>
        const int Dense = 256;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Which shares size the blocks.</summary>
        public enum Basis
        {
            /// <summary>Market exchange rates (IMF WEO NGDPD): the default row; trade and debts are paid at these.</summary>
            Market,

            /// <summary>Purchasing power (IMF WEO PPPSH): the PPP toggle and the history variant.</summary>
            Ppp
        }

        /// <summary>One block of the row.</summary>
        public sealed class Block
        {
            public string Id;

            /// <summary>Its place west to east (0 = west).</summary>
            public int Index;

            public WorldModel.Economy Economy;

            /// <summary>Its share of world output on this basis (%).</summary>
            public double Share;

            /// <summary>Its west and east edges (u).</summary>
            public float U0, U1;

            public float Width => U1 - U0;
            public float Centre => 0.5f * (U0 + U1);

            /// <summary>The top of each value-added stratum (v), bottom to top: agriculture, industry excluding manufacturing,
            /// manufacturing, services (the last at <see cref="BlockHeight"/>); null when not measured (the rest of the world).</summary>
            public float[] StrataTops;

            /// <summary>Below the row (positive down from v 0): the base's depth (state spending, % of GDP × 0.4), the revenue
            /// line's and the military stratum's; NaN when not measured.</summary>
            public float BaseDepth = float.NaN, RevenueDepth = float.NaN, MilitaryDepth = float.NaN;

            /// <summary>The net international investment position (US$ bn, end-2024) and its bar (|NIIP| × 1/500 units): a
            /// creditor's above the block, a debtor's hatched below the base. NaN when not measured.</summary>
            public double Niip = double.NaN;

            public float NiipLength = float.NaN;
            public bool Creditor;

            /// <summary>US Treasuries held (US$ bn, end-Aug 2026, custodial) and the threads they make, round(holdings / 100);
            /// for the rest of the world, the TIC total less the 12 holders. -1 when none are drawn (the US issues them; an
            /// unverified holding is not drawn).</summary>
            public double TreasuriesBn = double.NaN;

            public int Threads = -1;
        }

        /// <summary>One pair's arch: the spine its two lanes run beside, from the west foot to the east foot.</summary>
        public sealed class Arch
        {
            /// <summary>The pair's economies, west and east.</summary>
            public readonly string West, East;

            /// <summary>The spine's feet (u) on the row's top (v <see cref="Top"/>), and its apex above the top.</summary>
            public readonly float WestFoot, EastFoot, Top, Apex;

            /// <summary>A short arch (apex above half the span) stands on vertical legs under a half circle; a longer one is a
            /// half ellipse. Both meet the row upright, so the lanes land side by side on the blocks' tops.</summary>
            public readonly bool Stadium;

            /// <summary>The spine's tightest radius of curvature: a lane further inside than this would fold.</summary>
            public readonly float MinRadius;

            readonly double half, legs, length;
            readonly double[] along;

            internal Arch(string west, string east, float westFoot, float eastFoot, float top)
            {
                West = west;
                East = east;
                WestFoot = westFoot;
                EastFoot = eastFoot;
                Top = top;
                half = Math.Max(1e-4, 0.5 * (eastFoot - westFoot));
                Apex = (float)(ApexPerSpan * (eastFoot - westFoot) + ApexBase);
                Stadium = Apex >= half;
                if (Stadium)
                {
                    legs = Apex - half;
                    length = 2 * legs + Math.PI * half;
                    MinRadius = (float)half;
                    return;
                }

                // a half ellipse: its length along θ, to step evenly along it
                along = new double[Dense + 1];
                double pu = WestFoot, pv = Top;
                for (int k = 1; k <= Dense; k++)
                {
                    double th = Math.PI * k / Dense;
                    double u = 0.5 * (WestFoot + EastFoot) - half * Math.Cos(th), v = Top + Apex * Math.Sin(th);
                    along[k] = along[k - 1] + Math.Sqrt((u - pu) * (u - pu) + (v - pv) * (v - pv));
                    pu = u;
                    pv = v;
                }

                length = along[Dense];
                MinRadius = (float)(Apex * (double)Apex / half);
            }

            /// <summary>The distance between the feet.</summary>
            public float Span => EastFoot - WestFoot;

            /// <summary>The spine's length.</summary>
            public float Length => (float)length;

            /// <summary>
            /// The spine at <paramref name="s"/> of its length from the west foot (0) to the east foot (1): the point and the
            /// unit normal pointing out of the arch (west at the west foot, up at the apex, east at the east foot).
            /// </summary>
            public void At(double s, out double u, out double v, out double nu, out double nv)
            {
                s = Math.Max(0, Math.Min(1, s));
                double mid = 0.5 * (WestFoot + EastFoot);
                if (Stadium)
                {
                    double d = s * length, round = Math.PI * half;
                    if (d <= legs)
                    {
                        u = WestFoot;
                        v = Top + d;
                        nu = -1;
                        nv = 0;
                        return;
                    }

                    if (d >= legs + round)
                    {
                        u = EastFoot;
                        v = Top + legs - (d - legs - round);
                        nu = 1;
                        nv = 0;
                        return;
                    }

                    double alpha = Math.PI - (d - legs) / half;
                    nu = Math.Cos(alpha);
                    nv = Math.Sin(alpha);
                    u = mid + half * nu;
                    v = Top + legs + half * nv;
                    return;
                }

                double target = s * length;
                int i = Array.BinarySearch(along, target);
                if (i < 0) i = ~i - 1;
                i = Math.Max(0, Math.Min(Dense - 1, i));
                double step = along[i + 1] - along[i];
                double th = Math.PI * (i + (step > 0 ? (target - along[i]) / step : 0)) / Dense;
                double c = Math.Cos(th), sn = Math.Sin(th);
                u = mid - half * c;
                v = Top + Apex * sn;
                double x = -Apex * c, y = half * sn, n = Math.Sqrt(x * x + y * y);
                nu = x / n;
                nv = y / n;
            }
        }

        /// <summary>One lane's path: the band a layer draws, <see cref="Points"/> ± <see cref="Width"/>/2 × <see cref="Normals"/>.</summary>
        public sealed class LanePath
        {
            /// <summary>The goods it carries (exporter, importer, both records, set and role).</summary>
            public WorldModel.Lane Trade;

            public Arch Arch;

            /// <summary>Eastbound lanes run outside the spine, westbound inside.</summary>
            public bool Eastbound;

            /// <summary>Its width (the exporter's record × <see cref="LaneUnitsPerBillion"/>) and its centreline's distance from
            /// the spine (outward positive).</summary>
            public float Width, Offset;

            /// <summary>The centreline from the exporter's foot to the importer's (<see cref="LaneSamples"/> + 1 points, equal
            /// steps of its length) and the arch's outward normal at each; the arrowhead goes at the last point, pointing down.</summary>
            public Vector2[] Points, Normals;

            /// <summary>Its centreline's length.</summary>
            public float Length;

            /// <summary>Whether the band's two edges keep moving forward with the centreline (false: it folds, so it is drawn
            /// narrower than its record somewhere).</summary>
            public bool Clean;

            public Vector2 From => Points[0];
            public Vector2 To => Points[Points.Length - 1];

            /// <summary>The point at <paramref name="t"/> of the lane's length (0 at the exporter, 1 at the importer).</summary>
            public Vector2 At(float t)
            {
                Locate(t, out int i, out float f);
                return Vector2.Lerp(Points[i], Points[i + 1], f);
            }

            /// <summary>The arch's outward normal at <paramref name="t"/>.</summary>
            public Vector2 NormalAt(float t)
            {
                Locate(t, out int i, out float f);
                return Vector2.Lerp(Normals[i], Normals[i + 1], f).normalized;
            }

            /// <summary>The direction of travel (toward the importer) at <paramref name="t"/>.</summary>
            public Vector2 TangentAt(float t)
            {
                Locate(t, out int i, out _);
                return (Points[i + 1] - Points[i]).normalized;
            }

            void Locate(float t, out int i, out float f)
            {
                float x = Math.Max(0f, Math.Min(1f, t)) * (Points.Length - 1);
                i = Math.Min(Points.Length - 2, (int)x);
                f = x - i;
            }
        }

        /// <summary>What a conflict mark on a lane is.</summary>
        public enum AnchorKind
        {
            /// <summary>A tariff wall across the lane at the importer's end.</summary>
            Wall,

            /// <summary>An export-control valve (a diamond) at the exporter's end.</summary>
            Valve,

            /// <summary>A sanction cut: the lane at its base-year width, its width before the event as a ghost, a break mark.</summary>
            Cut,

            /// <summary>A lane that grew as a cut economy's trade was rerouted to it (labelled before → after).</summary>
            Reroute
        }

        /// <summary>One conflict mark's place on a lane, with the numbers it encodes.</summary>
        public sealed class LaneAnchor
        {
            public AnchorKind Kind;

            /// <summary>The events that put it there (a cut lists every drawn event cutting its lane).</summary>
            public string[] Events = Array.Empty<string>();

            public LanePath Lane;

            /// <summary>Where it sits: a share of the lane's length from the exporter (0) to the importer (1), the point, the
            /// direction of travel there and the arch's outward normal.</summary>
            public float T;

            public Vector2 Point, Tangent, Normal;

            /// <summary>Its place among the marks of its kind on the same lane, and how many there are.</summary>
            public int Slot, Slots = 1;

            /// <summary>A wall: the average tariff on the lane's goods (%), its date and source, and its height (rate × 0.5
            /// units); the earlier rate (PIIE's January 2018) as a ghost. NaN when the data store no average for the lane (a
            /// duty on certain goods only: <see cref="Note"/> says so). <see cref="AllIn"/>: the rate includes MFN duties.</summary>
            public double Rate = double.NaN, EarlierRate = double.NaN;

            public string RateDate, EarlierDate, RateSource, Note;
            public float Height = float.NaN, EarlierHeight = float.NaN;
            public bool AllIn;

            /// <summary>A cut or a reroute: the importer's own record of its imports from the exporter in the year before the
            /// event and in the base year (US$ bn), which record that is, and both as widths on that one record (units): the
            /// mark draws its ghost at <see cref="BeforeWidth"/> and the lane at the break at <see cref="AfterWidth"/>, so it
            /// shows the change its label prints. The lane elsewhere keeps the exporter's record
            /// (<see cref="LanePath.Width"/>), which can differ (Russia's cells are built from partners' records). A valve:
            /// the controlled exports before and after (US chip-making tools to China, 2021 and 2025); its widths are NaN.</summary>
            public double Before = double.NaN, After = double.NaN;

            public int BeforeYear, AfterYear;
            public string Record;
            public float BeforeWidth = float.NaN, AfterWidth = float.NaN;

            /// <summary>The end of a suspension or truce (ISO, from the event) and whether it falls within 30 days of the
            /// as-of date (flagged on the strip).</summary>
            public string Expires;

            public bool ExpiresSoon;
        }

        /// <summary>A keystone's 100% bar over its main holder's block.</summary>
        public sealed class Keystone
        {
            public WorldModel.Chokepoint Chokepoint;

            /// <summary>The block the bar floats above: its largest holder's (a country outside the 13 is in the rest of the
            /// world); null when no holder has a block.</summary>
            public Block Over;

            /// <summary>Its place among the keystones over the same block (to stack them), and how many there are.</summary>
            public int Slot, Slots = 1;

            /// <summary>The holders along the bar, largest first, then the rest of the world's output ("others").</summary>
            public KeystoneSegment[] Segments = Array.Empty<KeystoneSegment>();
        }

        /// <summary>One holder's part of a keystone bar: from and to (% along the bar) and the block its leader spoke goes to.</summary>
        public sealed class KeystoneSegment
        {
            /// <summary>The holder's id as stored ("TWN", "COD", "EUROPE"), or "others" for the remainder.</summary>
            public string Holder;

            /// <summary>Its block (a country outside the 13: the rest of the world; Germany: the EU27); null for a region or the rest.</summary>
            public Block Block;

            public double From, To;
            public string Text, Note;
        }

        /// <summary>The history wall on one basis: each band's bottom and top per year (cumulative %), stacked in the row's order.</summary>
        public sealed class HistoryWall
        {
            public readonly WorldModel.History Series;

            /// <summary>The bands bottom to top: the row's order, west to east, the rest of the world on top.</summary>
            public readonly string[] Bands;

            /// <summary>Per band and year: the band's bottom and top (cumulative % of world output).</summary>
            public readonly double[][] Lo, Hi;

            /// <summary>The u of <see cref="HistoryFirstYear"/> (the row's west end).</summary>
            public readonly float Left;

            internal HistoryWall(WorldModel.History series, IReadOnlyList<string> order, float left)
            {
                Series = series;
                Left = left;
                Bands = order.ToArray();
                int n = series.Years.Length;
                Lo = new double[Bands.Length][];
                Hi = new double[Bands.Length][];
                double[] acc = new double[n];
                for (int b = 0; b < Bands.Length; b++)
                {
                    Lo[b] = new double[n];
                    Hi[b] = new double[n];
                    series.Shares.TryGetValue(Bands[b], out double[] s);
                    for (int i = 0; i < n; i++)
                    {
                        double v = s != null && i < s.Length && !double.IsNaN(s[i]) ? s[i] : 0;
                        Lo[b][i] = acc[i];
                        acc[i] += v;
                        Hi[b][i] = acc[i];
                    }
                }
            }

            public int[] Years => Series.Years;

            /// <summary>The u of a year (6.75 units a year from 1950 at the row's west end).</summary>
            public float U(double year) => Left + (float)((year - HistoryFirstYear) * HistoryUnitsPerYear);

            /// <summary>The v of a cumulative share (0% at <see cref="HistoryBottom"/>, 100% at <see cref="HistoryTop"/>).</summary>
            public static float V(double percent) => HistoryBottom + (float)(percent / 100 * (HistoryTop - HistoryBottom));
        }

        /// <summary>The basis the blocks are sized on.</summary>
        public readonly Basis Of;

        /// <summary>The row, west to east, the rest of the world last.</summary>
        public readonly IReadOnlyList<Block> Blocks;

        /// <summary>Germany's outline inside the EU27 block (its own share, centred in the EU's block; never a block of the row).</summary>
        public readonly Block Germany;

        /// <summary>The row's west and east ends (u).</summary>
        public readonly float Left, Right;

        /// <summary>One arch per pair, and the lanes beside them: every lane of "All flows" (largest pairs first) and the
        /// default set's.</summary>
        public readonly IReadOnlyList<Arch> Arches;

        public readonly IReadOnlyList<LanePath> Lanes, DefaultLanes;

        /// <summary>The blocks whose ports overlap because their "All flows" lanes are wider together than the block.</summary>
        public readonly IReadOnlyList<string> Crowded;

        /// <summary>The pairs whose two lanes are together wider than one of their blocks, so their feet overhang it toward
        /// the partner, onto the neighbouring block ("MEX-USA on MEX (8.87 units wide, the block 8.20: 0.68 onto USA)").</summary>
        public readonly IReadOnlyList<string> Overhanging;

        /// <summary>The conflict marks of drawn events, on the lanes they stand on.</summary>
        public readonly IReadOnlyList<LaneAnchor> Anchors;

        /// <summary>Drawn walls, valves and cuts (or parts of them) with no lane to stand on, and why ("drc_cobalt: ROW has no
        /// lanes"); and rated walls left out because PIIE's average on the lane already counts them.</summary>
        public readonly IReadOnlyList<string> Unanchored, Covered;

        public readonly IReadOnlyList<Keystone> Keystones;

        /// <summary>The history wall at purchasing power (1950-2024; the default basis) and at market rates (1980-2024).</summary>
        public readonly HistoryWall PppWall, MarketWall;

        readonly Dictionary<string, Block> blockById = new Dictionary<string, Block>(StringComparer.Ordinal);
        readonly Dictionary<string, LanePath> laneByKey = new Dictionary<string, LanePath>(StringComparer.Ordinal);
        readonly WorldModel model;

        // how far a pair wider than its block may overhang it ("MEX-USA@MEX"), for Problems()
        readonly Dictionary<string, float> overhang = new Dictionary<string, float>(StringComparer.Ordinal);

        // the TIC total less the 12 holders, when every one may be drawn (NaN otherwise): negative is a problem, not drawn
        double restTreasuries = double.NaN;

        /// <summary>The row's layout on one basis (the model builds both).</summary>
        public static WorldLayout Build(WorldModel m, Basis basis) => new WorldLayout(m, basis);

        /// <summary>A block by economy id (null when it has none: Germany, the world).</summary>
        public Block BlockOf(string id) => id != null && blockById.TryGetValue(id, out Block b) ? b : null;

        /// <summary>The lane from an exporter to an importer (null when there is none).</summary>
        public LanePath Lane(string exporter, string importer) =>
            laneByKey.TryGetValue(exporter + ">" + importer, out LanePath l) ? l : null;

        /// <summary>The nominal camera target for the row: the row's middle at v 40 (spec 9.1: (176, 40)).</summary>
        public Vector2 CameraTarget => new Vector2(0.5f * (Left + Right), CameraV);

        /// <summary>The nominal camera target for the history variant (spec 9.1: (176, −170)).</summary>
        public Vector2 HistoryCameraTarget => new Vector2(0.5f * (Left + Right), HistoryCameraV);

        /// <summary>Pixels per stage unit at a camera distance, for a frontal view (FOV 45° vertical, 1080 px tall unless
        /// given): 3.0 at the row's 435.</summary>
        public static float PixelsPerUnit(float distance, float screenHeight = 1080, float fovDegrees = 45) =>
            (float)(screenHeight / (2 * distance * Math.Tan(fovDegrees * Math.PI / 360)));

        WorldLayout(WorldModel m, Basis basis)
        {
            model = m;
            Of = basis;

            // the row: the US's west edge at market rates fixes the west end on both bases
            double west = 0;
            WorldModel.Economy us = m.ById(WorldModel.Us);
            foreach (WorldModel.Economy e in m.Row)
            {
                if (e.Id == WorldModel.Us) break;
                west += Num(e.Share.Value) * UnitsPerPercent;
            }

            Left = us != null ? (float)-(west + Num(us.Share.Value) * UnitsPerPercent / 2) : -RowUnits / 2;
            List<Block> blocks = new List<Block>();
            double at = Left;
            foreach (WorldModel.Economy e in m.Row)
            {
                double share = Num(basis == Basis.Market ? e.Share.Value : e.SharePpp.Value);
                Block b = new Block { Id = e.Id, Index = blocks.Count, Economy = e, Share = share, U0 = (float)at };
                at += share * UnitsPerPercent;
                b.U1 = (float)at;
                Fill(b);
                blocks.Add(b);
                blockById[b.Id] = b;
            }

            Blocks = blocks;
            Right = (float)at;
            Threads(m);
            Germany = InsideEu(m, basis);

            // the lanes, the marks on them, the keystones, the history wall
            List<string> crowded = new List<string>(), overhanging = new List<string>();
            List<Arch> arches = new List<Arch>();
            List<LanePath> lanes = new List<LanePath>();
            Route(m, arches, lanes, crowded, overhanging);
            Arches = arches;
            Lanes = lanes;
            foreach (LanePath l in lanes) laneByKey[l.Trade.Key] = l;
            DefaultLanes = lanes.Where(l => l.Trade.InDefault).ToList();
            Crowded = crowded;
            Overhanging = overhanging;
            List<string> unanchored = new List<string>(), covered = new List<string>();
            Anchors = Mark(m, unanchored, covered);
            Unanchored = unanchored;
            Covered = covered;
            Keystones = Keys(m);
            PppWall = new HistoryWall(m.HistoryPpp, m.Order, Left);
            MarketWall = new HistoryWall(m.HistoryMarket, m.Order, Left);
        }

        /// <summary>A block's strata, base, revenue line, military stratum and net IIP bar.</summary>
        static void Fill(Block b)
        {
            WorldModel.Economy e = b.Economy;
            if (e.Strata != null)
            {
                double sum = e.Strata.Sum();
                b.StrataTops = new float[e.Strata.Length];
                double acc = 0;
                for (int i = 0; i < e.Strata.Length; i++)
                {
                    acc += e.Strata[i];
                    b.StrataTops[i] = (float)(acc / sum * BlockHeight);
                }
            }

            if (e.GovSpending.Has) b.BaseDepth = (float)(e.GovSpending.Value * BaseUnitsPerPercent);
            if (e.GovRevenue.Has) b.RevenueDepth = (float)(e.GovRevenue.Value * BaseUnitsPerPercent);
            if (e.MilitaryPctGdp.Has) b.MilitaryDepth = (float)(e.MilitaryPctGdp.Value * BaseUnitsPerPercent);
            if (e.NiipUsd.Has)
            {
                b.Niip = e.NiipUsd.Value;
                b.NiipLength = (float)(Math.Abs(b.Niip) * NiipUnitsPerBillion);
                b.Creditor = b.Niip > 0;
            }
        }

        /// <summary>
        /// Treasury threads, one per $100B (spec 6.5), from every holder whose end-Aug 2026 holding may be drawn (verified on
        /// or after the as-of date, its gate item too); the rest of the world's are the TIC total less the 12 holders, only
        /// when every one of them may be drawn, and never when that is negative (a problem: the holders outrun the total).
        /// The US issues them: none.
        /// </summary>
        void Threads(WorldModel m)
        {
            double per = m.ThreadBillions > 0 ? m.ThreadBillions : 100;
            double held = 0;
            bool all = true;
            foreach (Block b in Blocks)
            {
                if (b.Id == WorldModel.Us || b.Economy.IsRest) continue;
                if (!m.Drawable(b.Economy.Treasuries))
                {
                    all = false;
                    continue;
                }

                b.TreasuriesBn = b.Economy.Treasuries.Value;
                b.Threads = (int)Math.Round(b.TreasuriesBn / per, MidpointRounding.AwayFromZero);
                held += b.TreasuriesBn;
            }

            Block rest = BlockOf(WorldModel.Rest);
            if (rest == null || !all || !m.Drawable(m.TreasuriesTotal)) return;
            restTreasuries = m.TreasuriesTotal.Value - held;
            if (restTreasuries < 0) return;
            rest.TreasuriesBn = restTreasuries;
            rest.Threads = (int)Math.Round(rest.TreasuriesBn / per, MidpointRounding.AwayFromZero);
        }

        /// <summary>Germany's outline: its own share's width, centred in the EU27's block (it is never added to the row).</summary>
        Block InsideEu(WorldModel m, Basis basis)
        {
            WorldModel.Economy de = m.GermanyEconomy;
            Block eu = BlockOf(de?.Inside ?? WorldModel.Eu);
            if (de == null || eu == null) return null;
            double share = Num(basis == Basis.Market ? de.Share.Value : de.SharePpp.Value);
            float half = (float)(share * UnitsPerPercent / 2);
            Block g = new Block { Id = de.Id, Index = -1, Economy = de, Share = share, U0 = eu.Centre - half, U1 = eu.Centre + half };
            Fill(g);
            return g;
        }

        // ------------------------------------------------------------------ lanes

        /// <summary>One pair of lanes: its two economies (west, east), the eastbound and westbound lanes and their widths.</summary>
        sealed class PairLanes
        {
            public string West, East;
            public WorldModel.Lane Eastbound, Westbound;
            public float EastWidth, WestWidth, WestFoot, EastFoot;
            public float Footprint => EastWidth + LaneGap + WestWidth;
        }

        /// <summary>
        /// Ports, arches and lanes. On each block's top the pairs it belongs to are laid side by side, west to east: the
        /// pairs to the west nearest first, then those to the east farthest first, so a farther arch lands outside a nearer
        /// one and they nest; each pair's footprint is its two lanes and the gap between them. They spread over
        /// <see cref="PortFill"/> of the block with gaps between <see cref="PortGapMin"/> and <see cref="PortGapMax"/>; when
        /// they are wider together than the block, they overlap evenly across it, so two neighbours' arches never cross over
        /// each other's feet. Every footprint then keeps <see cref="PortMargin"/> from the block's sides, or, when it is wider
        /// than the room between them, keeps to the block's edges; one wider than the block itself keeps to the block's side
        /// away from its partner and overhangs toward it, onto the neighbouring block, never past the row's ends
        /// (<see cref="Overhanging"/>; Mexico's block is narrower than its pair with the US).
        /// </summary>
        void Route(WorldModel m, List<Arch> arches, List<LanePath> lanes, List<string> crowded, List<string> overhanging)
        {
            Dictionary<string, PairLanes> pairs = new Dictionary<string, PairLanes>(StringComparer.Ordinal);
            foreach (WorldModel.Lane l in m.Lanes)
            {
                Block a = BlockOf(l.Exporter), b = BlockOf(l.Importer);
                if (a == null || b == null) continue;
                if (!pairs.TryGetValue(l.Pair, out PairLanes p))
                {
                    bool exporterWest = a.Centre < b.Centre;
                    p = new PairLanes { West = exporterWest ? a.Id : b.Id, East = exporterWest ? b.Id : a.Id };
                    pairs[l.Pair] = p;
                }

                float width = (float)(Num(l.Record) * LaneUnitsPerBillion);
                if (l.Exporter == p.West)
                {
                    p.Eastbound = l;
                    p.EastWidth = width;
                }
                else
                {
                    p.Westbound = l;
                    p.WestWidth = width;
                }
            }

            foreach (Block x in Blocks)
            {
                List<PairLanes> touching = pairs.Values.Where(p => p.West == x.Id || p.East == x.Id).ToList();
                if (touching.Count == 0) continue;
                float Distance(PairLanes p) => Math.Abs(BlockOf(p.West == x.Id ? p.East : p.West).Centre - x.Centre);
                int Partner(PairLanes p) => BlockOf(p.West == x.Id ? p.East : p.West).Index;
                List<PairLanes> seq = touching.Where(p => p.East == x.Id).OrderBy(Distance).ThenBy(Partner)
                    .Concat(touching.Where(p => p.West == x.Id).OrderByDescending(Distance).ThenBy(Partner)).ToList();
                int n = seq.Count;
                float total = seq.Sum(p => p.Footprint);
                float room = x.Width - 2 * PortMargin;
                float[] lefts = new float[n];
                if (n == 1)
                {
                    lefts[0] = x.Centre - total / 2;
                }
                else if (total + PortGapMin * (n - 1) <= room)
                {
                    float gap = Math.Max(PortGapMin, Math.Min(PortGapMax, (PortFill * x.Width - total) / (n - 1)));
                    float s = x.Centre - (total + gap * (n - 1)) / 2;
                    for (int k = 0; k < n; k++)
                    {
                        lefts[k] = s;
                        s += seq[k].Footprint + gap;
                    }
                }
                else
                {
                    // wider than the block: centres spread evenly (by half footprints) from the west margin to the east one
                    crowded.Add(x.Id);
                    float c0 = x.U0 + PortMargin + seq[0].Footprint / 2, c1 = x.U1 - PortMargin - seq[n - 1].Footprint / 2;
                    if (c1 < c0) c0 = c1 = x.Centre;
                    float[] cum = new float[n];
                    for (int k = 1; k < n; k++) cum[k] = cum[k - 1] + (seq[k - 1].Footprint + seq[k].Footprint) / 2;
                    for (int k = 0; k < n; k++) lefts[k] = c0 + (c1 - c0) * (cum[n - 1] > 0 ? cum[k] / cum[n - 1] : 0) - seq[k].Footprint / 2;
                }

                for (int k = 0; k < n; k++)
                {
                    // on the block: inside its margins, else inside its edges; wider than the block, from the side away from
                    // the partner toward it; and never past the row's ends
                    PairLanes p = seq[k];
                    float fp = p.Footprint;
                    bool partnerEast = p.West == x.Id;
                    if (fp <= room) lefts[k] = Clamp(lefts[k], x.U0 + PortMargin, x.U1 - PortMargin - fp);
                    else if (fp <= x.Width) lefts[k] = Clamp(lefts[k], x.U0, x.U1 - fp);
                    else
                    {
                        lefts[k] = partnerEast ? x.U0 : x.U1 - fp;
                        int next = x.Index + (partnerEast ? 1 : -1);
                        string onto = next >= 0 && next < Blocks.Count ? Blocks[next].Id : "nothing";
                        overhang[p.West + "-" + p.East + "@" + x.Id] = fp - x.Width;
                        overhanging.Add($"{p.West}-{p.East} on {x.Id} ({F2(fp)} units wide, the block {F2(x.Width)}: {F2(fp - x.Width)} onto {onto})");
                    }

                    lefts[k] = Clamp(lefts[k], Left, Right - fp);
                    // at its west foot a pair's eastbound lane is on the outside (west); at its east foot, on the east
                    if (p.West == x.Id) p.WestFoot = lefts[k] + p.EastWidth + LaneGap / 2;
                    else p.EastFoot = lefts[k] + p.WestWidth + LaneGap / 2;
                }
            }

            foreach (PairLanes p in pairs.Values.OrderByDescending(p => p.EastWidth + p.WestWidth).ThenBy(p => p.West + "-" + p.East, StringComparer.Ordinal))
            {
                Arch arch = new Arch(p.West, p.East, p.WestFoot, p.EastFoot, BlockHeight);
                arches.Add(arch);
                if (p.Eastbound != null) lanes.Add(Path(arch, p.Eastbound, true, p.EastWidth, LaneGap / 2 + p.EastWidth / 2));
                if (p.Westbound != null) lanes.Add(Path(arch, p.Westbound, false, p.WestWidth, -(LaneGap / 2 + p.WestWidth / 2)));
            }
        }

        /// <summary>A lane beside its arch's spine: dense samples offset along the normal, then even steps of its own length.</summary>
        static LanePath Path(Arch arch, WorldModel.Lane trade, bool eastbound, float width, float offset)
        {
            double[] pu = new double[Dense + 1], pv = new double[Dense + 1], qu = new double[Dense + 1], qv = new double[Dense + 1];
            for (int k = 0; k <= Dense; k++)
            {
                // exporter to importer: west to east for an eastbound lane
                double s = eastbound ? (double)k / Dense : 1 - (double)k / Dense;
                arch.At(s, out double u, out double v, out double nu, out double nv);
                pu[k] = u + offset * nu;
                pv[k] = v + offset * nv;
                qu[k] = nu;
                qv[k] = nv;
            }

            double[] along = new double[Dense + 1];
            for (int k = 1; k <= Dense; k++) along[k] = along[k - 1] + Math.Sqrt(Sq(pu[k] - pu[k - 1]) + Sq(pv[k] - pv[k - 1]));
            Vector2[] points = new Vector2[LaneSamples + 1], normals = new Vector2[LaneSamples + 1];
            int i = 0;
            for (int j = 0; j <= LaneSamples; j++)
            {
                double target = along[Dense] * j / LaneSamples;
                while (i < Dense - 1 && along[i + 1] < target) i++;
                double step = along[i + 1] - along[i], f = step > 0 ? Math.Max(0, Math.Min(1, (target - along[i]) / step)) : 0;
                points[j] = new Vector2((float)(pu[i] + (pu[i + 1] - pu[i]) * f), (float)(pv[i] + (pv[i + 1] - pv[i]) * f));
                normals[j] = new Vector2((float)(qu[i] + (qu[i + 1] - qu[i]) * f), (float)(qv[i] + (qv[i + 1] - qv[i]) * f)).normalized;
            }

            LanePath lane = new LanePath
            {
                Trade = trade, Arch = arch, Eastbound = eastbound, Width = width, Offset = offset, Points = points, Normals = normals,
                Length = (float)along[Dense]
            };
            lane.Clean = KeepsWidth(lane);
            return lane;
        }

        /// <summary>Whether both edges of the band move forward with its centreline everywhere (a fold runs backward).</summary>
        static bool KeepsWidth(LanePath l)
        {
            float h = l.Width / 2;
            for (int j = 0; j < l.Points.Length - 1; j++)
            {
                Vector2 d = l.Points[j + 1] - l.Points[j];
                Vector2 outer = l.Points[j + 1] + l.Normals[j + 1] * h - (l.Points[j] + l.Normals[j] * h);
                Vector2 inner = l.Points[j + 1] - l.Normals[j + 1] * h - (l.Points[j] - l.Normals[j] * h);
                if (Vector2.Dot(outer, d) <= 0 || Vector2.Dot(inner, d) <= 0) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ conflict marks

        /// <summary>
        /// The marks of every drawn wall, valve and cut on the lanes they stand on, by printed rules:
        /// <list type="bullet">
        /// <item>a wall: the first party imposes a duty on goods from each other party, so it stands on the lane from that
        /// party at the first party's end. PIIE's trade-weighted averages (an event linking tariffs.series) give the US-China
        /// lanes their rates, US on China and China on the US (its retaliation), the latest row that may be drawn, with
        /// January 2018 as the ghost; those averages include every other US duty on Chinese goods, so a rated wall of
        /// another event is not added there (<see cref="Covered"/>). Section 301's forced-labour order gives each partner's
        /// rate. Other walls store no average (a duty on certain goods), so their rate is NaN;</item>
        /// <item>a valve: the first party controls its exports to each other party, on the lane from it;</item>
        /// <item>a cut: the economy whose goods are refused is the one every linked bilateral series names (else the first
        /// party); the cut stands on its lane to each other party, with that importer's own record of the year before the
        /// event and of the base year; an importer in a linked series that is not a party took the rerouted goods.</item>
        /// </list>
        /// A party drawn inside another block (Germany, inside the EU27) stands on that block's lanes, one mark per lane and
        /// event, with the EU's records: Germany is never added to them. A party left with no lane is listed in
        /// <see cref="Unanchored"/>, never dropped without a word.
        /// </summary>
        List<LaneAnchor> Mark(WorldModel m, List<string> unanchored, List<string> covered)
        {
            List<LaneAnchor> list = new List<LaneAnchor>();
            WorldModel.TariffRow latest = m.TariffsLatest, first = m.TariffsFirst;
            List<WorldModel.Event> drawn = m.Events.Where(e => e.Drawn).ToList();
            bool piie = latest != null && drawn.Any(e => e.Form == "wall" && e.Linked.Contains("tariffs.series"));
            // PIIE's chart averages US duties on Chinese goods and China's on US goods: the two US-China lanes
            HashSet<string> averaged = new HashSet<string>(StringComparer.Ordinal);
            if (piie)
            {
                averaged.Add(WorldModel.China + ">" + WorldModel.Us);
                averaged.Add(WorldModel.Us + ">" + WorldModel.China);
            }

            foreach (WorldModel.Event e in drawn)
            {
                if (e.Parties.Length == 0) continue;
                switch (e.Form)
                {
                    case "wall":
                        Walls(m, e, piie, averaged, latest, first, list, unanchored, covered);
                        break;
                    case "valve":
                        string from = DrawnIn(m, e.Parties[0]);
                        foreach (string to in e.Parties.Skip(1).Select(p => DrawnIn(m, p)).Where(p => p != from).Distinct())
                        {
                            LanePath lane = Lane(from, to);
                            if (lane == null)
                            {
                                unanchored.Add($"{e.Id} ({e.Form}: {(BlockOf(from) == null || !m.IsLaneEconomy(from) ? from : to)} has no lanes)");
                                continue;
                            }

                            LaneAnchor a = new LaneAnchor { Kind = AnchorKind.Valve, Events = new[] { e.Id }, Lane = lane, Expires = e.Expires, ExpiresSoon = e.ExpiresSoon };
                            if (e.Linked.Contains("trade.chipTools") && m.ChipTools2021.Has && m.ChipTools2025.Has)
                            {
                                a.Before = m.ChipTools2021.Value;
                                a.After = m.ChipTools2025.Value;
                                a.BeforeYear = YearOf(m.ChipTools2021.Year);
                                a.AfterYear = YearOf(m.ChipTools2025.Year);
                                a.Record = m.ChipTools2021.Source;
                            }

                            list.Add(a);
                        }

                        break;
                    case "cut":
                        Cuts(m, e, list, unanchored);
                        break;
                }
            }

            // place each mark along its lane: walls back from the importer, valves out from the exporter, cuts mid-lane
            foreach (IGrouping<(LanePath, AnchorKind), LaneAnchor> g in list.GroupBy(a => (a.Lane, a.Kind)))
            {
                List<LaneAnchor> marks = g.Key.Item2 == AnchorKind.Wall
                    ? g.OrderBy(a => double.IsNaN(a.Rate) ? 1 : 0).ThenBy(a => m.EventById(a.Events[0])?.Start ?? "", StringComparer.Ordinal).ToList()
                    : g.ToList();
                for (int k = 0; k < marks.Count; k++)
                {
                    LaneAnchor a = marks[k];
                    a.Slot = k;
                    a.Slots = marks.Count;
                    a.T = a.Kind == AnchorKind.Wall ? Math.Max(0.5f, WallT - WallStep * k)
                        : a.Kind == AnchorKind.Valve ? ValveFrom + (ValveTo - ValveFrom) * (k + 0.5f) / marks.Count
                        : CutT;
                    a.Point = a.Lane.At(a.T);
                    a.Tangent = a.Lane.TangentAt(a.T);
                    a.Normal = a.Lane.NormalAt(a.T);
                }
            }

            return list;
        }

        void Walls(WorldModel m, WorldModel.Event e, bool piie, HashSet<string> averaged, WorldModel.TariffRow latest, WorldModel.TariffRow first,
            List<LaneAnchor> list, List<string> unanchored, List<string> covered)
        {
            string importer = DrawnIn(m, e.Parties[0]);
            bool series = piie && e.Linked.Contains("tariffs.series");
            bool s301 = e.Linked.Contains("tariffs.section301_2026");
            foreach (string exporter in e.Parties.Skip(1).Select(p => DrawnIn(m, p)).Where(p => p != importer).Distinct())
            {
                LanePath lane = Lane(exporter, importer);
                if (lane == null)
                {
                    unanchored.Add($"{e.Id} (wall: {exporter} → {importer} has no lane)");
                    continue;
                }

                LaneAnchor a = new LaneAnchor { Kind = AnchorKind.Wall, Events = new[] { e.Id }, Lane = lane, Expires = e.Expires, ExpiresSoon = e.ExpiresSoon };
                if (series && averaged.Contains(lane.Trade.Key))
                {
                    Rate(a, importer == WorldModel.Us ? latest.UsOnChina : latest.ChinaOnUs, latest.Date,
                        importer == WorldModel.Us ? first?.UsOnChina ?? double.NaN : first?.ChinaOnUs ?? double.NaN, first?.Date, m.TariffSource);
                }
                else if (averaged.Contains(lane.Trade.Key))
                {
                    covered.Add($"{e.Id} on {lane.Trade.Key}: inside PIIE's average");
                    continue;
                }
                else if (s301 && m.Section301Drawn && m.Section301Rates.TryGetValue(exporter, out double rate))
                {
                    Rate(a, rate, e.Start, double.NaN, null, "Federal Register 2026-15274 (Section 301, forced labour; tariffs.section301_2026)");
                    a.AllIn = m.Section301AllIn.Contains(exporter);
                }
                else
                {
                    a.Note = "no average rate stored: the duty covers certain goods";
                }

                list.Add(a);
            }

            // PIIE also gives the other side's average: China's retaliation on US goods stands on the reverse lane
            if (!series) return;
            foreach (string key in averaged)
            {
                if (list.Any(x => x.Kind == AnchorKind.Wall && x.Lane.Trade.Key == key)) continue;
                LanePath lane = laneByKey.TryGetValue(key, out LanePath l) ? l : null;
                if (lane == null) continue;
                LaneAnchor a = new LaneAnchor { Kind = AnchorKind.Wall, Events = new[] { e.Id }, Lane = lane };
                bool usImports = lane.Trade.Importer == WorldModel.Us;
                Rate(a, usImports ? latest.UsOnChina : latest.ChinaOnUs, latest.Date,
                    usImports ? first?.UsOnChina ?? double.NaN : first?.ChinaOnUs ?? double.NaN, first?.Date, m.TariffSource);
                list.Add(a);
            }
        }

        static void Rate(LaneAnchor a, double rate, string date, double earlier, string earlierDate, string source)
        {
            a.Rate = rate;
            a.RateDate = date;
            a.RateSource = source;
            a.Height = (float)(rate * WallUnitsPerPoint);
            a.EarlierRate = earlier;
            a.EarlierDate = double.IsNaN(earlier) ? null : earlierDate;
            a.EarlierHeight = (float)(earlier * WallUnitsPerPoint);
        }

        void Cuts(WorldModel m, WorldModel.Event e, List<LaneAnchor> list, List<string> unanchored)
        {
            const string prefix = "trade.bilateral.pairs.";
            List<string[]> linked = e.Linked.Where(x => x.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => x.Substring(prefix.Length).Split('-')).Where(x => x.Length == 2).ToList();
            string target = e.Parties[0];
            if (linked.Count > 0)
            {
                IEnumerable<string> common = linked[0];
                foreach (string[] k in linked.Skip(1)) common = common.Intersect(k);
                target = common.FirstOrDefault(id => e.Parties.Contains(id)) ?? target;
            }

            target = DrawnIn(m, target);
            int before = YearOf(e.Start) - 1, after = m.Year;
            void Add(string party, AnchorKind kind)
            {
                // a party drawn inside a block (Germany) is cut on that block's lane, merged with any mark already there
                string importer = DrawnIn(m, party);
                LanePath lane = Lane(target, importer);
                if (lane == null)
                {
                    unanchored.Add($"{e.Id} ({(kind == AnchorKind.Cut ? "cut" : "reroute")}: {target} → {importer} has no lane)");
                    return;
                }

                LaneAnchor a = list.FirstOrDefault(x => x.Kind == kind && x.Lane == lane);
                if (a != null)
                {
                    if (!a.Events.Contains(e.Id)) a.Events = a.Events.Concat(new[] { e.Id }).ToArray();
                    return;
                }

                // both widths on the importer's record, the one the label prints
                a = new LaneAnchor { Kind = kind, Events = new[] { e.Id }, Lane = lane, BeforeYear = before, AfterYear = after };
                a.Before = m.BilateralSeries.Imports(importer, target, before, out string record);
                a.After = m.BilateralSeries.Imports(importer, target, after, out _);
                a.Record = record;
                a.BeforeWidth = (float)(a.Before * LaneUnitsPerBillion);
                a.AfterWidth = (float)(a.After * LaneUnitsPerBillion);
                list.Add(a);
            }

            // the economies that refuse the goods, then those that took them instead
            foreach (string p in e.Parties.Where(p => DrawnIn(m, p) != target)) Add(p, AnchorKind.Cut);
            foreach (string[] k in linked)
            {
                string importer = k[0] == target ? k[1] : k[0];
                if (!e.Parties.Any(p => DrawnIn(m, p) == importer)) Add(importer, AnchorKind.Reroute);
            }
        }

        /// <summary>The block an economy's marks stand on: its own, or for Germany the EU27's (drawn inside it, never added
        /// to it: a mark naming Germany stands on the EU's lane, with the EU's records).</summary>
        string DrawnIn(WorldModel m, string id) => id == null || BlockOf(id) != null ? id : m.ById(id)?.Inside ?? id;

        // ------------------------------------------------------------------ keystones

        /// <summary>Each keystone over its largest holder's block, stacked when several share one; its holders as segments.</summary>
        List<Keystone> Keys(WorldModel m)
        {
            List<Keystone> keys = new List<Keystone>();
            foreach (WorldModel.Chokepoint c in m.Chokepoints)
            {
                List<WorldModel.Holder> holders = c.Holders.Where(h => !double.IsNaN(h.Value)).OrderByDescending(h => h.Value).ToList();
                Keystone k = new Keystone { Chokepoint = c, Over = holders.Select(h => BlockOfHolder(m, h.Id)).FirstOrDefault(b => b != null) };
                List<KeystoneSegment> segments = new List<KeystoneSegment>();
                double acc = 0;
                foreach (WorldModel.Holder h in holders)
                {
                    segments.Add(new KeystoneSegment { Holder = h.Id, Block = BlockOfHolder(m, h.Id), From = acc, To = acc + h.Value, Text = h.Text, Note = h.Note });
                    acc += h.Value;
                }

                if (acc < 100 - 1e-6) segments.Add(new KeystoneSegment { Holder = "others", From = acc, To = 100 });
                k.Segments = segments.ToArray();
                keys.Add(k);
            }

            foreach (IGrouping<Block, Keystone> g in keys.Where(k => k.Over != null).GroupBy(k => k.Over))
            {
                int slot = 0;
                foreach (Keystone k in g)
                {
                    k.Slot = slot++;
                    k.Slots = g.Count();
                }
            }

            return keys;
        }

        /// <summary>A holder's block: an economy of the row, Germany's EU27, any other country (three letters) the rest of the
        /// world; a region ("EUROPE") has none.</summary>
        Block BlockOfHolder(WorldModel m, string id)
        {
            if (id == null) return null;
            Block b = BlockOf(id);
            if (b != null) return b;
            if (id == WorldModel.Germany) return BlockOf(m.GermanyEconomy?.Inside ?? WorldModel.Eu);
            return id.Length == 3 && id.All(char.IsUpper) && id != WorldModel.World ? BlockOf(WorldModel.Rest) : null;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// The geometry's rules: the row is 500 units (blocks touching, in the model's order); at market rates the US is
        /// centred at u 0; every lane is its record × <see cref="LaneUnitsPerBillion"/> wide within 2%, keeps that width
        /// along its whole arch (no fold), spans a positive distance, and lands with its whole width on both blocks' tops (a
        /// pair wider than its block overhanging it no further than it must, <see cref="Overhanging"/>) and never past the
        /// row's ends; the rest of the world's Treasuries (the TIC total less the 12 holders) are not negative.
        /// </summary>
        public List<string> Problems()
        {
            List<string> list = new List<string>();
            string on = Of == Basis.Market ? "" : "at purchasing power, ";
            if (!(Math.Abs(Right - Left - RowUnits) <= 0.05)) list.Add($"{on}the row is {F2(Right - Left)} units, not {F1(RowUnits)}");
            for (int i = 1; i < Blocks.Count; i++)
            {
                if (Math.Abs(Blocks[i].U0 - Blocks[i - 1].U1) > 1e-3) list.Add($"{on}{Blocks[i - 1].Id} and {Blocks[i].Id} do not touch");
            }

            Block us = BlockOf(WorldModel.Us);
            if (Of == Basis.Market && (us == null || Math.Abs(us.Centre) > 1e-3)) list.Add($"the US block is not centred at u 0 (at {F2(us?.Centre ?? float.NaN)})");
            if (!model.Order.SequenceEqual(Blocks.Select(b => b.Id))) list.Add($"{on}the blocks are not in the model's west-to-east order");
            foreach (LanePath l in Lanes)
            {
                double want = l.Trade.Record * LaneUnitsPerBillion;
                if (!(Math.Abs(l.Width - want) <= WorldModel.WidthTolerance * want + 1e-6)) list.Add($"{on}lane {l.Trade.Key} is {F2(l.Width)} units wide, not its record × scale {F2(want)}");
                if (!l.Clean) list.Add($"{on}lane {l.Trade.Key} folds on its arch ({F2(Math.Abs(l.Offset) + l.Width / 2)} units inside a spine whose tightest radius is {F2(l.Arch.MinRadius)}): drawn narrower than its record");
                if (!(l.Arch.Span > 0)) list.Add($"{on}the arch of {l.Arch.West}-{l.Arch.East} spans {F2(l.Arch.Span)} units");
                if (Math.Abs(l.From.y - BlockHeight) > 1e-3 || Math.Abs(l.To.y - BlockHeight) > 1e-3) list.Add($"{on}lane {l.Trade.Key} does not land on the blocks' tops");
                Foot(l, l.Trade.Exporter, l.From.x, on, list);
                Foot(l, l.Trade.Importer, l.To.x, on, list);
            }

            // basis-free, so the market and purchasing-power layouts make one problem
            if (restTreasuries < 0)
            {
                int holders = Blocks.Count(b => b.Id != WorldModel.Us && !b.Economy.IsRest);
                list.Add($"the TIC total ${F1(model.TreasuriesTotal.Value)}B is less than the {holders} holders' ${F1(model.TreasuriesTotal.Value - restTreasuries)}B: " +
                         "the rest of the world's Treasuries would be negative, and are not drawn");
            }

            return list;
        }

        /// <summary>A lane's foot: the band across it (the lane meets the row upright) on its block's top, overhanging it only
        /// as far as its pair is wider than the block, and never past the row's ends.</summary>
        void Foot(LanePath l, string id, float u, string on, List<string> list)
        {
            float lo = u - l.Width / 2, hi = u + l.Width / 2;
            if (lo < Left - 1e-3f) list.Add($"{on}lane {l.Trade.Key} hangs {F2(Left - lo)} units off the row's west end at {id}");
            if (hi > Right + 1e-3f) list.Add($"{on}lane {l.Trade.Key} hangs {F2(hi - Right)} units off the row's east end at {id}");
            Block b = BlockOf(id);
            if (b == null) return;
            float off = Math.Max(0, b.U0 - lo) + Math.Max(0, hi - b.U1);
            float allowed = overhang.TryGetValue(l.Arch.West + "-" + l.Arch.East + "@" + id, out float a) ? a : 0;
            if (off > allowed + 1e-3f)
            {
                list.Add($"{on}lane {l.Trade.Key} lands {F2(off)} units off {id}'s top" + (allowed > 0 ? $" (its pair is wider than the block by {F2(allowed)})" : ""));
            }
        }

        static int YearOf(string date) => date != null && date.Length >= 4 && int.TryParse(date.Substring(0, 4), NumberStyles.Integer, Ci, out int y) ? y : 0;

        static double Num(double v) => double.IsNaN(v) ? 0 : v;

        static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));

        static double Sq(double v) => v * v;

        static string F1(double v) => v.ToString("0.0", Ci);
        static string F2(double v) => v.ToString("0.00", Ci);
    }
}
