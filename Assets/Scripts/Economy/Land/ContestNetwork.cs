using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// The corporate biosphere's layout (design Docs/hills-design/ADVANTAGE.md, sections 3-7), in the present's land-local
    /// frame past the crest of 2026 (+z along time, +x across, +y up). Pure: vectors only, no Unity objects.
    /// <list type="bullet">
    /// <item><b>Bodies.</b> Each company is one rigid organism as wide as its shell (market value: radius
    /// <see cref="KR"/> ∛$bn), held at its altitude (market position, 10..150) by a strong spring, pulled along its
    /// dependencies (flows, stakes) by springs, pushed off every other organism (repulsion, a hard collision at the
    /// shells), drawn weakly to its supply-chain layer's depth (energy and tools by the crest, buyers toward the future) and
    /// to the middle. Owners float in the ownership class's stratum above, near the weighted centre of what they hold;
    /// buyers, the states and the people stand on the low ground beneath.</item>
    /// <item><b>Parts.</b> An organism's balance sheet, placed by rule around its core (book equity): what it owns in the
    /// upper hemisphere, what it owes hanging below, promises outside the shell beneath.</item>
    /// <item><b>The landscape.</b> A height field under the network, each point the kernel-weighted mean of the nearby
    /// companies' ground (their altitude less their shell), falling to the low ground far from them; drawn as contours.</item>
    /// </list>
    /// Deterministic: seeded positions, fixed iteration order. Rebuilt per land; retargeted (not rebuilt) when the world
    /// or the year changes, and then iterated live so the population visibly rearranges.
    /// </summary>
    public sealed class ContestNetwork
    {
        /// <summary>The middle across the land; the depth band past the crest; the volume scale; the strata's altitudes.</summary>
        public const float X0 = 290, ZFrom = 120, ZTo = 290, KR = 2.4f, MinR = 2.4f, MinAlt = 14, MaxAlt = 150,
            OwnerY = 150, WealthY = 185, BuyerY = -6, StateY = -30;

        /// <summary>The supply chain in strata under the hill (the user's fifth direction, Docs/research/advantage/brief-v3.md):
        /// consumer-facing products on top, the infrastructure beneath, raw materials, government the floor. The stack stands
        /// under the 2026 crest (x across the hill, centred on the crest's cut face), from StackTop down; each stratum's height
        /// leaves room for its largest body and the next one's (Gap between).</summary>
        public const float StackTop = -60, StackX = 130, StackDz = 20, Gap = 24;

        public static readonly string[] StratumOrder = { "consumer", "models", "clouds", "chips", "fabs", "tools", "energy", "materials", "state" };

        /// <summary>Each stratum's plane (land-local y), from the 2026 sizes (fixed, so every world reads on one stack).</summary>
        public readonly float[] StratumY = new float[StratumOrder.Length];

        public float StackZ => CrestZ + StackDz;

        /// <summary>Where the extinct fall: below the government floor.</summary>
        public float GraveY => StratumY[StratumOrder.Length - 1] - 70;

        public enum Kind { Company, Owner, Wealth, Buyer, State, Satellite }

        public enum PartKind { Core, Cash, Built, Bought, Stakes, Debt, Leases, Promises }

        /// <summary>The reference P/E at which a company's profits "justify" a value (assumed: about the US market's long-run
        /// trailing multiple). The shell between this earnings value and the market value is speculation.</summary>
        public const double ReferencePe = 15;

        public sealed class Body
        {
            public float EarningsR;              // the earnings shell's radius (0: no profits)
            public float BodyR;                  // the visible body's radius (its balance sheet), set when its organism is built
            public Vector3 BodyMin, BodyMax;     // the visible body's bounds from its centre, set with BodyR
            public int Stratum = -1;             // its stratum (StratumOrder), -1 above the stack
            public Body Host;                    // a satellite's company: its presence in another stratum, in the company's column
            public Advantage.Presence Presence;  // what a satellite (or a company in its own stratum) does there
            public float Territory;              // an advantage boundary's radius on its stratum (0: none); rivals stand outside
            public float Reach;                  // how far its drawn body reaches sideways (its balance sheet can outgrow its value's R)
            public List<Part> CustomParts;       // a body that is not a company's balance sheet (history's states, armies, faiths)

            /// <summary>Its radius for keeping apart: the larger of its value's shell and its drawn body's reach.</summary>
            public float Room => Mathf.Max(R, Reach);
            public double EarningsValue = double.NaN;
            public float Y2026;                  // the altitude it held in 2026 (where a simulated year's move is measured from)
            public string Id, Name, Layer;
            public Kind Kind;
            public Advantage.Actor Actor;
            public Advantage.Owner Owner;
            public Advantage.SimState State;
            public Vector3 Pos, Vel;
            public float R, TargetY, LayerZ;
            public double Value;                 // market value (companies) or holdings (owners), $bn
            public int Index;
            public bool Alive = true;
            public string AbsorbedBy;
            public bool Moves => Kind == Kind.Company || Kind == Kind.Owner || Kind == Kind.Wealth;

            /// <summary>Stands in a stratum and is pushed by the bodies there (a company, or a satellite whose company moves).</summary>
            public bool InStratum => (Kind == Kind.Company || Kind == Kind.Satellite || Kind == Kind.State) && Stratum >= 0;
        }

        public sealed class Link
        {
            public Body A, B;
            public string Kind;                  // flow, stake, owns
            public double UsdBn;
            public Advantage.Flow Flow;
            public Advantage.Stake Stake;
            public float Rest, K;
        }

        public readonly struct Part
        {
            public readonly PartKind Kind;
            public readonly Vector3 Offset;      // from the organism's centre
            public readonly float Radius;
            public readonly double UsdBn;        // its amount (dollars for a balance sheet; people, troops or ships for history's actors)
            public readonly Color Tint;          // its colour when it is not a balance sheet's (alpha 0: the part kind's own)
            public readonly float Solid;         // how tangible it reads (0: the part kind's own)

            public Part(PartKind kind, Vector3 offset, float radius, double usd)
            {
                Kind = kind; Offset = offset; Radius = radius; UsdBn = usd; Tint = default; Solid = 0;
            }

            public Part(PartKind kind, Vector3 offset, float radius, double usd, Color tint, float solid)
            {
                Kind = kind; Offset = offset; Radius = radius; UsdBn = usd; Tint = tint; Solid = solid;
            }
        }

        public readonly Advantage Model;
        public readonly float CrestZ;
        public readonly List<Body> Bodies = new List<Body>();
        public readonly List<Link> Links = new List<Link>();
        readonly Dictionary<string, Body> byId = new Dictionary<string, Body>();
        public string World { get; private set; } = Advantage.Now;
        public int Year { get; private set; } = 2026;

        /// <summary>The companies' total value in the world and year shown, against now.</summary>
        public double MarketRatio { get; private set; } = 1;

        /// <summary>The kinetic energy of the last step (the layout has settled below about 0.05).</summary>
        public float Energy { get; private set; }

        Vector3[] force;

        static readonly string[] LayerOrder = { "energy", "tools", "fabs", "chips", "clouds", "models", "apps", "buyers" };

        public Body Get(string id) => id != null && byId.TryGetValue(id, out Body b) ? b : null;

        public static float Radius(double usdBn) => double.IsNaN(usdBn) || usdBn <= 0 ? MinR : Mathf.Max(MinR, KR * Mathf.Pow((float)usdBn, 1f / 3));

        /// <summary>A company's judged advantage in a world's year: 2026's (A) moving to the world's judged value over three years,
        /// as the simulation ramps it (simulate.py, rampYears: none of the change in 2026, all of it from 2029).</summary>
        public static double AdvantageAt(Advantage.Actor a, string world, int year)
        {
            double a0 = a.AdvantageIn(Advantage.Now), a1 = a.AdvantageIn(world);
            if (double.IsNaN(a0) || double.IsNaN(a1) || year <= 2026) return a0;
            return a0 + (a1 - a0) * Mathf.Clamp01((year - 2026) / 3f);
        }

        /// <summary>Market position as altitude (design section 6): 150 (0.55 A/7 + 0.45 log10(V/10)/log10(500)), 10..150.</summary>
        public static float Altitude(double a, double valueBn)
        {
            float adv = double.IsNaN(a) ? 2.5f : (float)a;
            float cap = double.IsNaN(valueBn) || valueBn <= 0 ? 0 : Mathf.Clamp01(Mathf.Log10((float)valueBn / 10f) / Mathf.Log10(500f));
            return Mathf.Clamp(MaxAlt * (.55f * adv / 7f + .45f * cap), MinAlt, MaxAlt);
        }

        /// <summary>2026's lowest and highest raw altitude: every world and year is stretched by this one map, so the
        /// population spans the low ground to the high ground and a change in any world reads against the same scale.</summary>
        float rawLo = MinAlt, rawHi = MaxAlt;

        float Stretch(float raw) => Mathf.Clamp(MinAlt + (raw - rawLo) / Mathf.Max(1, rawHi - rawLo) * (MaxAlt - MinAlt), MinAlt - 6, MaxAlt + 6);

        public ContestNetwork(Advantage model, float crestZ, string world, int year)
        {
            Model = model;
            CrestZ = crestZ;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var a in model.Actors)
            {
                if (a.Flank == "state" || a.Band == "buyers" || !a.Sized) continue;
                float raw = Altitude(a.A, a.SizeUsdBn);
                lo = Mathf.Min(lo, raw);
                hi = Mathf.Max(hi, raw);
            }

            if (hi > lo) { rawLo = lo; rawHi = hi; }
            foreach (var a in model.Actors)
            {
                Kind kind = a.Flank == "state" ? Kind.State : a.Band == "buyers" ? Kind.Buyer : Kind.Company;
                int layer = Array.IndexOf(LayerOrder, a.Band ?? "");
                var b = new Body
                {
                    Id = a.Id, Name = a.Name, Kind = kind, Actor = a, Layer = a.Band,
                    LayerZ = crestZ + ZFrom + (layer < 0 ? 4.5f : layer) / (LayerOrder.Length - 1) * (ZTo - ZFrom),
                    Stratum = kind == Kind.Buyer ? -1 : Array.IndexOf(StratumOrder, kind == Kind.State ? "state" : a.StratumId ?? "consumer"),
                };
                b.Presence = a.Presence.FirstOrDefault(x => x.Stratum == a.StratumId);
                Add(b);
            }

            // vertical integration: a company's presence in another stratum is a satellite in its column (same x and z); so is an
            // actor's advantage boundary in a stratum it has no body in (China's hold on rare earths, under the state's floor)
            foreach (var host in Bodies.Where(x => x.Kind == Kind.Company || x.Kind == Kind.State).ToList())
            {
                foreach (var pr in host.Actor.Presence)
                {
                    int si = Array.IndexOf(StratumOrder, pr.Stratum);
                    if (si < 0 || si == host.Stratum || byId.ContainsKey(host.Id + "@" + pr.Stratum)) continue;
                    Add(new Body { Id = host.Id + "@" + pr.Stratum, Name = host.Name, Kind = Kind.Satellite, Actor = host.Actor, Host = host, Stratum = si, Presence = pr });
                }

                foreach (var m in model.Moats.Where(m => m.Actor == host.Id))
                {
                    int si = Array.IndexOf(StratumOrder, m.Stratum);
                    if (si < 0 || si == host.Stratum || byId.ContainsKey(host.Id + "@" + m.Stratum)) continue;
                    Add(new Body { Id = host.Id + "@" + m.Stratum, Name = host.Name, Kind = Kind.Satellite, Actor = host.Actor, Host = host, Stratum = si,
                                   Presence = new Advantage.Presence { Stratum = m.Stratum, What = m.Market, Facts = m.Facts } });
                }
            }

            // the strata's planes: each as far below the last as their largest bodies (at 2026's sizes) need
            var maxR = new float[StratumOrder.Length];
            foreach (var b in Bodies)
            {
                if (b.Stratum < 0) continue;
                float r = b.Kind == Kind.State ? 20 : b.Kind == Kind.Satellite ? SatelliteR(b.Host.Actor.Sized ? Radius(b.Host.Actor.SizeUsdBn) : MinR)
                    : b.Actor.Sized ? Radius(b.Actor.SizeUsdBn) : MinR;
                maxR[b.Stratum] = Mathf.Max(maxR[b.Stratum], r);
            }

            for (int i = 0; i < StratumOrder.Length; i++)
                StratumY[i] = i == 0 ? StackTop - maxR[0] : StratumY[i - 1] - (maxR[i - 1] + maxR[i] + Gap);

            foreach (var o in model.Owners)
            {
                if (byId.ContainsKey(o.Id)) continue;     // a company that owns another: the stake joins two organisms
                bool wealth = o.Kind == "households" || o.Kind == "person" || o.Kind == "abroad";
                Add(new Body { Id = o.Id, Name = o.Name, Kind = wealth ? Kind.Wealth : Kind.Owner, Owner = o, Value = o.HoldingsUsdBn, LayerZ = crestZ + (ZFrom + ZTo) * .5f });
            }

            foreach (var f in model.Flows)
            {
                Body a = Get(f.From), b = Get(f.To);
                if (a == null || b == null || a == b) continue;
                Links.Add(new Link { A = a, B = b, Kind = "flow", UsdBn = f.UsdBn, Flow = f });
            }

            foreach (var s in model.Stakes)
            {
                Body a = Get(s.Owner), b = Get(s.Company);
                if (a == null || b == null || a == b) continue;
                Links.Add(new Link { A = a, B = b, Kind = a.Kind == Kind.Company ? "stake" : "owns", UsdBn = s.UsdBn, Stake = s });
            }

            Retarget(world, year);
            Seed();
            for (int i = 0; i < 700; i++) Step(1);
        }

        /// <summary>The advantage boundary a body holds in a stratum (its actor's moat there), as a radius; 0 without one.</summary>
        float TerritoryIn(Body b, string stratum)
        {
            var moat = MoatOf(b.Actor?.Id ?? b.Id, stratum);
            return moat != null ? TerritoryOf(b.Room, moat.Share) : 0;
        }

        public Advantage.Moat MoatOf(string actor, string stratum) => Model.Moats.FirstOrDefault(m => m.Actor == actor && m.Stratum == stratum);

        /// <summary>A satellite's radius: a share of its company's (its presence there is a part of the company).</summary>
        public static float SatelliteR(float hostR) => Mathf.Max(3.5f, hostR * .38f);

        /// <summary>An advantage boundary's radius around its holder: its shell plus room for its share of the market (a share
        /// given in percent; otherwise a fixed margin).</summary>
        public static float TerritoryOf(float r, string share)
        {
            var m = share != null ? System.Text.RegularExpressions.Regex.Match(share, @"(\d+(?:\.\d+)?)\s*%") : null;
            double pct = m != null && m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
            return r + 8 + (double.IsNaN(pct) ? 12 : (float)Math.Min(100, pct) * .32f);
        }

        void Add(Body b)
        {
            if (byId.ContainsKey(b.Id)) return;
            byId[b.Id] = b;
            b.Index = Bodies.Count;
            Bodies.Add(b);
        }

        /// <summary>Takes a world's year: each company's size, altitude and fate; the owners' holdings follow their stakes'
        /// values. Positions are kept, so <see cref="Step"/> moves the population to its new places.</summary>
        public void Retarget(string world, int year)
        {
            World = string.IsNullOrEmpty(world) ? Advantage.Now : world;
            Year = year;
            var w = Model.WorldById(World) ?? Model.WorldById(Advantage.Now);
            foreach (var b in Bodies)
            {
                if (b.Kind == Kind.Company)
                {
                    var a = b.Actor;
                    // 2026 is the measured year in every world: its numbers are the verified facts, never the simulation's; a
                    // company the simulation could not run (all its inputs assumed) stays at its verified 2026 values
                    var st = World == Advantage.Now || year <= 2026 || Model.NotSimulated.Contains(a.Id) ? null : w?.At(a.Id, year);
                    b.State = st;
                    b.Value = st != null && !double.IsNaN(st.V) ? st.V : a.Sized ? a.SizeUsdBn : double.NaN;
                    b.Alive = st == null || st.Alive;
                    b.AbsorbedBy = st?.By;
                    b.R = Radius(b.Value);
                    // what its profits justify at the reference multiple: net income (the world's, else value / P/E) x 15
                    double ni = st != null && !double.IsNaN(st.Ni) ? st.Ni : a.Pe > 0 && !double.IsNaN(b.Value) ? b.Value / a.Pe : double.NaN;
                    b.EarningsValue = double.IsNaN(ni) ? double.NaN : Math.Max(0, ni) * ReferencePe;
                    b.EarningsR = double.IsNaN(b.EarningsValue) || b.EarningsValue <= 0 ? 0 : Mathf.Min(b.R, Radius(b.EarningsValue));
                    // its height is its stratum: the supply chain stands in layers, consumer products on top
                    b.TargetY = b.Stratum >= 0 ? StratumY[b.Stratum] : StackTop;
                    b.Y2026 = b.TargetY;
                    if (!b.Alive && st != null && st.Status == "extinct") b.TargetY = GraveY;
                    if (b.Alive && b.BodyR <= 0)
                    {
                        // bounds only, the first time: the coarse sphere is within a few percent, and the drawn organism refines
                        // them whenever it is rebuilt (a world's or year's change rebuilds it)
                        var shape = new Organism(b, null, 1);
                        b.BodyR = shape.MaxRadius;
                        b.BodyMin = shape.Min;
                        b.BodyMax = shape.Max;
                        b.Reach = Mathf.Max(Mathf.Max(-shape.Min.x, shape.Max.x), Mathf.Max(-shape.Min.z, shape.Max.z));
                    }
                    else if (!b.Alive) b.Reach = 0;
                    else b.Reach = Mathf.Max(Mathf.Max(-b.BodyMin.x, b.BodyMax.x), Mathf.Max(-b.BodyMin.z, b.BodyMax.z));

                    b.Territory = b.Alive ? TerritoryIn(b, a.StratumId) : 0;
                }
                else if (b.Kind == Kind.Buyer) { b.R = 16; b.TargetY = StackTop + 34; }
                else if (b.Kind == Kind.State)
                {
                    b.R = b.Id == "us-gov" ? 20 : 11;
                    b.TargetY = StratumY[StratumOrder.Length - 1];
                    b.Territory = TerritoryIn(b, "state");
                }
            }

            foreach (var b in Bodies)
            {
                if (b.Kind != Kind.Satellite) continue;
                b.Alive = b.Host.Alive;
                b.R = SatelliteR(b.Host.R);
                b.Value = double.NaN;
                b.TargetY = StratumY[b.Stratum];
                b.Territory = b.Alive ? TerritoryIn(b, StratumOrder[b.Stratum]) : 0;
            }

            // the whole: the companies' value in this world and year against now (the wealth groups own the market)
            double total = 0, total0 = 0;
            foreach (var b in Bodies)
                if (b.Kind == Kind.Company)
                {
                    if (!double.IsNaN(b.Value) && b.Alive) total += b.Value;
                    if (b.Actor.Sized) total0 += b.Actor.SizeUsdBn;
                }

            MarketRatio = total0 > 0 ? total / total0 : 1;

            // owners: holdings at this world's values (a stake's percentage of the company's value, else its dollars)
            foreach (var b in Bodies)
            {
                if (b.Kind != Kind.Owner && b.Kind != Kind.Wealth) continue;
                double held = 0;
                foreach (var s in b.Owner.Stakes)
                {
                    var c = Get(s.Company);
                    if (c != null && c.Kind == Kind.Company && !c.Alive) continue;    // an absorbed or extinct company's stake is gone
                    if (c != null && c.Kind == Kind.Company && !double.IsNaN(s.Pct) && !double.IsNaN(c.Value)) held += c.Value * s.Pct / 100;
                    else if (!double.IsNaN(s.UsdBn)) held += s.UsdBn;
                }

                b.Value = b.Owner.Kind == "households" || b.Owner.Kind == "abroad" ? b.Owner.HoldingsUsdBn * MarketRatio
                    : b.Owner.Stakes.Count > 0 ? held : b.Owner.HoldingsUsdBn;
                b.R = Mathf.Max(3f, .22f * Mathf.Sqrt((float)Math.Max(1, b.Value)));
                b.TargetY = b.Kind == Kind.Wealth ? WealthY : OwnerY;
            }

            foreach (var b in Bodies)
                if (b.Kind == Kind.Company && !b.Alive && b.AbsorbedBy != null && Get(b.AbsorbedBy) is Body host)
                    b.TargetY = host.TargetY;

            foreach (var l in Links)
            {
                l.Rest = l.A.R + l.B.R + (l.Kind == "flow" ? 8 : 5);
                l.K = l.Kind == "owns" || l.A.Kind == Kind.Owner || l.A.Kind == Kind.Wealth || l.B.Kind == Kind.Owner || l.B.Kind == Kind.Wealth ? 0
                    : .012f * Mathf.Clamp(Mathf.Log10(2 + (float)Math.Max(0, double.IsNaN(l.UsdBn) ? 5 : l.UsdBn)), .4f, 3f);
            }
        }

        /// <summary>Deterministic starting places: each company at its layer's depth, spread across by a hash of its id, at
        /// its altitude; owners above the middle; buyers and states on the low ground in rows.</summary>
        void Seed()
        {
            int buyers = 0, states = 0;
            foreach (var b in Bodies)
            {
                float h = Hash01(b.Id), h2 = Hash01(b.Id + "#");
                switch (b.Kind)
                {
                    case Kind.Company:
                        b.Pos = new Vector3(StackX + (h - .5f) * 300, b.TargetY, StackZ + (h2 - .5f) * 140);
                        break;
                    case Kind.Satellite:
                        b.Pos = new Vector3(b.Host.Pos.x, b.TargetY, b.Host.Pos.z);
                        break;
                    case Kind.Buyer:
                        b.Pos = new Vector3(StackX - 60 + buyers++ * 60, b.TargetY, StackZ + 70);
                        break;
                    case Kind.State:
                        b.Pos = new Vector3(StackX + (states % 2 == 0 ? 1 : -1) * (states + 1) / 2 * 40, b.TargetY, StackZ + 20 * ((states % 3) - 1));
                        states++;
                        break;
                    default:
                        b.Pos = new Vector3(StackX + (h - .5f) * 260, b.TargetY, StackZ + (h2 - .5f) * 160);
                        break;
                }

                b.Vel = Vector3.zero;
            }
        }

        static float Hash01(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>One or more steps of the forces (see the class summary). Companies and owners move; buyers and states
        /// stay where they stand.</summary>
        public void Step(int steps)
        {
            if (force == null || force.Length != Bodies.Count) force = new Vector3[Bodies.Count];
            for (int s = 0; s < steps; s++)
            {
                Array.Clear(force, 0, force.Length);
                for (int i = 0; i < Bodies.Count; i++)
                {
                    var a = Bodies[i];
                    if (!a.Moves && a.Kind != Kind.Satellite) continue;
                    // an absorbed company drifts into its absorber's side and stays there
                    if (a.Kind == Kind.Company && !a.Alive && a.AbsorbedBy != null && Get(a.AbsorbedBy) is Body host)
                    {
                        Vector3 side = a.Pos - host.Pos;
                        side.y = 0;
                        side = side.sqrMagnitude > 1e-3f ? side.normalized : Vector3.right;
                        Vector3 goal = host.Pos + side * (host.R * .9f);
                        force[i] += (goal - a.Pos) * .06f;
                        continue;
                    }

                    // the stack's middle, under the crest: strong enough that each stratum packs close
                    if (a.Kind == Kind.Company)
                    {
                        // wide, shallow bands: each stratum spreads across the screen and stays thin in depth
                        force[i].x += .004f * (StackX - a.Pos.x);
                        force[i].z += .03f * (StackZ - a.Pos.z);
                    }
                    else if (a.Kind != Kind.Satellite)
                    {
                        force[i].x += .006f * (StackX - a.Pos.x);
                        force[i].z += .006f * (StackZ - a.Pos.z);
                    }

                    for (int j = i + 1; j < Bodies.Count; j++)
                    {
                        var b = Bodies[j];
                        if (a.InStratum && b.InStratum)
                        {
                            // bodies of one stratum push each other apart; a satellite's push moves its whole column
                            if (a.Stratum != b.Stratum || !a.Alive || !b.Alive) continue;
                            int ia = a.Kind == Kind.Satellite ? a.Host.Index : i, ib = b.Kind == Kind.Satellite ? b.Host.Index : j;
                            if (ia == ib) continue;
                            Vector3 d = b.Pos - a.Pos;
                            d.y = 0;
                            float dist = Mathf.Max(.5f, d.magnitude);
                            // an advantage boundary keeps rivals outside it; bodies keep apart by what is drawn, not only their value
                            float ra = a.Room, rb = b.Room;
                            float min = Mathf.Max(ra + rb, Mathf.Max(a.Territory + rb, b.Territory + ra)) + 3;
                            float push = .6f * (ra + rb) * (ra + rb) / (dist * dist + 40);
                            if (dist < min) push += .45f * (min - dist);
                            Vector3 f = d / dist * push;
                            force[ia] -= f;
                            force[ib] += f;
                        }
                        else if (!a.InStratum && !b.InStratum && a.Moves && b.Moves && a.Kind != Kind.Company && b.Kind != Kind.Company)
                        {
                            // the golden circles keep apart in their sky
                            Vector3 d = b.Pos - a.Pos;
                            d.y = 0;
                            float dist = Mathf.Max(.5f, d.magnitude), min = a.R + b.R + 4;
                            float push = 2.2f * (a.R + b.R) * (a.R + b.R) / (dist * dist + 40);
                            if (dist < min) push += .45f * (min - dist);
                            Vector3 f = d / dist * push;
                            force[i] -= f;
                            force[j] += f;
                        }
                    }
                }

                // links: dependencies and stakes pull; an owner drifts toward what it holds
                foreach (var l in Links)
                {
                    int ia = l.A.Index, ib = l.B.Index;
                    if (l.Kind == "owns")
                    {
                        if (!l.A.Moves) continue;
                        Vector3 d = l.B.Pos - l.A.Pos;
                        d.y = 0;
                        float w = .0012f * Mathf.Clamp(Mathf.Log10(2 + (float)Math.Max(0, double.IsNaN(l.UsdBn) ? 20 : l.UsdBn)), .5f, 3f);
                        force[ia] += d * w;
                        continue;
                    }

                    // between strata a river pulls across, never up or down: the strata keep their heights
                    Vector3 dd = l.B.Pos - l.A.Pos;
                    dd.y = 0;
                    float dist = Mathf.Max(.5f, dd.magnitude), rest = l.A.Stratum == l.B.Stratum ? l.Rest : 0;
                    Vector3 f = dd / dist * (l.K * Mathf.Max(0, dist - rest) * .5f);
                    if (l.A.Moves) force[ia] += f;
                    if (l.B.Moves) force[ib] -= f;
                }

                float energy = 0;
                for (int i = 0; i < Bodies.Count; i++)
                {
                    var b = Bodies[i];
                    if (!b.Moves) continue;
                    Vector3 f = force[i];
                    float cap = 6;
                    if (f.sqrMagnitude > cap * cap) f = f.normalized * cap;
                    f.y = 0;
                    b.Vel = (b.Vel + f) * .82f;
                    b.Vel.y = 0;
                    b.Pos += b.Vel;
                    // a stratum's height is exact at rest, eased while it moves
                    float dy = b.TargetY - b.Pos.y;
                    b.Pos.y = b.Kind == Kind.Company ? b.Pos.y + dy * .12f : b.TargetY;
                    energy += b.Vel.sqrMagnitude + dy * dy * .0144f;
                }

                // satellites stand in their company's column
                foreach (var b in Bodies)
                    if (b.Kind == Kind.Satellite)
                        b.Pos = new Vector3(b.Host.Pos.x, b.Pos.y + (b.TargetY - b.Pos.y) * .12f, b.Host.Pos.z);
                Energy = energy / Math.Max(1, Bodies.Count);
            }
        }

        /// <summary>The worst interpenetration of two organisms' shells (land units; 0 when none) and their names.</summary>
        public float Overlap(out string pair)
        {
            float worst = 0;
            pair = "";
            for (int i = 0; i < Bodies.Count; i++)
                for (int j = i + 1; j < Bodies.Count; j++)
                {
                    Body a = Bodies[i], b = Bodies[j];
                    if (a.Kind != Kind.Company || b.Kind != Kind.Company || !a.Alive || !b.Alive || a.Stratum != b.Stratum) continue;
                    float o = a.Room + b.Room - new Vector2(a.Pos.x - b.Pos.x, a.Pos.z - b.Pos.z).magnitude;
                    if (o > worst) { worst = o; pair = a.Name + "/" + b.Name; }
                }

            return worst;
        }

        /// <summary>The largest gap between a company's altitude and its target.</summary>
        public float AltitudeError()
        {
            float worst = 0;
            foreach (var b in Bodies) if (b.Kind == Kind.Company && b.Alive) worst = Mathf.Max(worst, Mathf.Abs(b.Pos.y - b.TargetY));
            return worst;
        }

        // ------------------------------------------------------------------ the organism

        /// <summary>A company's balance sheet as parts around its core (offsets from the organism's centre): cash, what it
        /// built, what it bought and its stakes in the upper hemisphere; debt and leases hanging below; commitments (promises
        /// not yet on the balance sheet) beneath the shell. A part's radius is <see cref="KR"/> ∛$bn. Parts are scaled with
        /// the world's book equity against today's, so a simulated year shrinks or grows the whole organism.</summary>
        public List<Part> Parts(Body b) => PartsOf(b);

        public static List<Part> PartsOf(Body b)
        {
            if (b.CustomParts != null) return b.Alive ? b.CustomParts : new List<Part>();
            var parts = new List<Part>(9);
            var sheet = b.Actor?.Sheet;
            if (sheet == null || !b.Alive) return parts;
            double scale = 1;
            if (b.State != null && !double.IsNaN(b.State.Eq) && !double.IsNaN(sheet.Equity) && sheet.Equity > 0) scale = Math.Max(.05, b.State.Eq / sheet.Equity);
            double equity = b.State != null && !double.IsNaN(b.State.Eq) ? b.State.Eq : sheet.Equity;
            float core = double.IsNaN(equity) ? 0 : Radius(Math.Abs(equity));
            parts.Add(new Part(PartKind.Core, Vector3.zero, core, equity));
            var above = new List<(PartKind kind, double usd)>();
            double cash = b.State != null && !double.IsNaN(b.State.Cash) ? b.State.Cash : sheet.Cash;
            if (cash > 0) above.Add((PartKind.Cash, cash));
            if (sheet.Ppe > 0) above.Add((PartKind.Built, sheet.Ppe * scale));
            double bought = ((double.IsNaN(sheet.Goodwill) ? 0 : sheet.Goodwill) + (double.IsNaN(sheet.Intangibles) ? 0 : sheet.Intangibles)) * scale;
            if (bought > 0) above.Add((PartKind.Bought, bought));
            if (sheet.Investments > 0) above.Add((PartKind.Stakes, sheet.Investments * scale));
            float twist = Hash01(b.Id) * Mathf.PI * 2;
            for (int i = 0; i < above.Count; i++)
            {
                float r = Radius(above[i].usd), ang = twist + i * Mathf.PI * 2 / Math.Max(1, above.Count);
                Vector3 dir = new Vector3(Mathf.Cos(ang) * .74f, .67f, Mathf.Sin(ang) * .74f).normalized;
                parts.Add(new Part(above[i].kind, dir * (core + r + .5f), r, above[i].usd));
            }

            double debt = b.State != null && !double.IsNaN(b.State.Debt) ? b.State.Debt : sheet.Debt;
            if (debt > 0)
            {
                float r = Radius(debt);
                parts.Add(new Part(PartKind.Debt, Vector3.down * (core + r + .5f), r, debt));
            }

            if (sheet.Leases > 0)
            {
                float r = Radius(sheet.Leases * scale);
                Vector3 dir = new Vector3(Mathf.Cos(twist + 2.2f) * .7f, -.7f, Mathf.Sin(twist + 2.2f) * .7f).normalized;
                parts.Add(new Part(PartKind.Leases, dir * (core + r + .5f), r, sheet.Leases * scale));
            }

            if (sheet.Commitments > 0)
            {
                float r = Radius(sheet.Commitments * scale);
                parts.Add(new Part(PartKind.Promises, Vector3.down * (b.R + r + 2), r, sheet.Commitments * scale));
            }

            return parts;
        }

        // ------------------------------------------------------------------ the landscape

        /// <summary>The fitness landscape's height at a point: one smooth field, the mean of the living companies' altitudes
        /// weighted by their size (market value to the 2/3, kernels 40 units wide), falling to the low ground at the edges (a
        /// baseline a tenth of the largest weight), drawn 30 units under the bodies so it never passes through them.</summary>
        public float Height(float x, float z)
        {
            float sw = baseline, sh = 0;
            foreach (var b in Bodies)
            {
                if (b.Kind != Kind.Company || !b.Alive) continue;
                float dx = x - b.Pos.x, dz = z - b.Pos.z, d2 = dx * dx + dz * dz;
                if (d2 > 14400) continue;   // beyond three kernel widths a body adds nothing visible
                float w = b.R * b.R * Mathf.Exp(-d2 / (2 * 40f * 40f));
                sw += w;
                sh += w * b.Pos.y;
            }

            return Mathf.Max(0, sh / sw - 30);
        }

        float baseline = 1;

        /// <summary>The landscape's contours: segments at each level (every <paramref name="step"/> units) by marching squares
        /// over a grid of <paramref name="cell"/> units covering the population.</summary>
        public List<(Vector3 a, Vector3 b, float level)> Contours(float cell, float step)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var b in Bodies)
            {
                if (b.Kind != Kind.Company) continue;
                x0 = Mathf.Min(x0, b.Pos.x - b.R - 40); x1 = Mathf.Max(x1, b.Pos.x + b.R + 40);
                z0 = Mathf.Min(z0, b.Pos.z - b.R - 40); z1 = Mathf.Max(z1, b.Pos.z + b.R + 40);
            }

            var segs = new List<(Vector3, Vector3, float)>();
            if (x0 >= x1) return segs;
            float wmax = 1;
            foreach (var b in Bodies) if (b.Kind == Kind.Company && b.Alive) wmax = Mathf.Max(wmax, b.R * b.R);
            baseline = .1f * wmax;
            int nx = Mathf.Min(90, Mathf.CeilToInt((x1 - x0) / cell)), nz = Mathf.Min(90, Mathf.CeilToInt((z1 - z0) / cell));
            var h = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                    h[i, k] = Height(x0 + i * cell, z0 + k * cell);
            for (float level = step; level < MaxAlt; level += step)
                for (int i = 0; i < nx; i++)
                    for (int k = 0; k < nz; k++)
                    {
                        float a = h[i, k], b = h[i + 1, k], c = h[i + 1, k + 1], d = h[i, k + 1];
                        int code = (a > level ? 1 : 0) | (b > level ? 2 : 0) | (c > level ? 4 : 0) | (d > level ? 8 : 0);
                        if (code == 0 || code == 15) continue;
                        float px = x0 + i * cell, pz = z0 + k * cell;
                        Vector3 E(float v0, float v1, Vector3 p0, Vector3 p1) => Vector3.Lerp(p0, p1, Mathf.Clamp01((level - v0) / (v1 - v0 + 1e-6f)));
                        Vector3 A = new Vector3(px, level, pz), B = new Vector3(px + cell, level, pz), C = new Vector3(px + cell, level, pz + cell), D = new Vector3(px, level, pz + cell);
                        Vector3 ab = E(a, b, A, B), bc = E(b, c, B, C), cd = E(c, d, C, D), da = E(d, a, D, A);
                        switch (code)
                        {
                            case 1: case 14: segs.Add((da, ab, level)); break;
                            case 2: case 13: segs.Add((ab, bc, level)); break;
                            case 3: case 12: segs.Add((da, bc, level)); break;
                            case 4: case 11: segs.Add((bc, cd, level)); break;
                            case 6: case 9: segs.Add((ab, cd, level)); break;
                            case 7: case 8: segs.Add((cd, da, level)); break;
                            case 5: segs.Add((da, ab, level)); segs.Add((bc, cd, level)); break;
                            case 10: segs.Add((ab, bc, level)); segs.Add((cd, da, level)); break;
                        }
                    }

            return segs;
        }
    }
}
