using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Model;
using Why.Humans.Smv;

namespace Why.Economy.Land
{
    /// <summary>What a pick in the land found (7.5).</summary>
    public enum PickKind : byte
    {
        None = 0,

        /// <summary>A member's dot over its player's disc (a lifeline): <see cref="LandHit.Person"/>, <see cref="LandHit.Index"/> its player.</summary>
        Dot,

        /// <summary>A player's head or disc: <see cref="LandHit.Index"/> the player.</summary>
        Player,

        /// <summary>A company's tower: <see cref="LandHit.Index"/> its capture index.</summary>
        Tower,

        /// <summary>A tie of the season: <see cref="LandHit.Index"/> its pair.</summary>
        Tie,

        /// <summary>A river's fall over the lip: <see cref="LandHit.Index"/> its lane (0-5 categories, 6 taxes, 7 abroad).</summary>
        Fall,

        /// <summary>A river, distributary, the tax river or the abroad pour: <see cref="LandHit.Index"/> its lane.</summary>
        River,

        /// <summary>A lane of the lip canal: <see cref="LandHit.Index"/> the lane.</summary>
        Canal,

        /// <summary>The crown.</summary>
        Crown,

        /// <summary>A sector's tread: <see cref="LandHit.Index"/> the industry, <see cref="LandHit.Part"/> the strip.</summary>
        Sector,

        /// <summary>A sector's pool: <see cref="LandHit.Index"/> the industry.</summary>
        Pool,

        /// <summary>A lifeline's dot in the cut: <see cref="LandHit.Person"/>.</summary>
        CutDot,

        /// <summary>An industry's bar in the cut's card: <see cref="LandHit.Index"/> the industry, <see cref="LandHit.Part"/> the strip.</summary>
        CutBar
    }

    /// <summary>One pick's result: what, which, how far from the pointer (px), where (world).</summary>
    public struct LandHit
    {
        public PickKind Kind;

        /// <summary>The player, company, pair, lane or industry (see <see cref="PickKind"/>); -1 for none.</summary>
        public int Index;

        /// <summary>The person of a dot (index into the population's people); -1 otherwise.</summary>
        public int Person;

        /// <summary>
        /// A sector's or bar's strip (<see cref="EconomyIds.SectorWages"/>, <see cref="EconomyIds.SectorUpkeep"/>,
        /// <see cref="EconomyIds.SectorOwners"/>; a pool hit carries <see cref="EconomyIds.SectorPool"/>); -1 otherwise.
        /// </summary>
        public int Part;

        /// <summary>Screen distance from the pointer to the thing (0 inside an area); the ray's length for a sector.</summary>
        public float DistancePx;

        /// <summary>The world point the pick refers to (the head, the dot, the tower top, the tread point ...).</summary>
        public Vector3 World;

        public bool IsNone => Kind == PickKind.None;

        public static LandHit None => new LandHit { Kind = PickKind.None, Index = -1, Person = -1, Part = -1, DistancePx = float.PositiveInfinity };

        public override string ToString() =>
            Kind + " " + Index.ToString(LandFacts.Ci) + (Person >= 0 ? " person " + Person.ToString(LandFacts.Ci) : "") +
            (Part >= 0 ? " part " + Part.ToString(LandFacts.Ci) : "") + " at " + DistancePx.ToString("0.0", LandFacts.Ci) + " px";
    }

    /// <summary>
    /// A pinhole camera as Unity's (vertical field of view, the camera looking along its rotation's +z), reduced to what
    /// picking needs: projecting world points to screen pixels (origin bottom-left, like the mouse) and the ray through a
    /// pixel. Built from a live camera's transform, or from a view preset's <see cref="CameraPose"/> (as
    /// <c>CameraRig</c> applies it), so the harness and the layout can project without a camera object. Pure value.
    /// </summary>
    public readonly struct LandProjector
    {
        public readonly Vector3 Eye;
        public readonly Quaternion Rotation;
        public readonly float TanHalf, Aspect;

        /// <summary>The screen (pixels).</summary>
        public readonly Vector2 Screen;

        public readonly Quaternion Inverse;

        public LandProjector(Vector3 eye, Quaternion rotation, float fovDeg, Vector2 screen)
        {
            Eye = eye;
            Rotation = rotation;
            Inverse = Quaternion.Inverse(rotation);
            TanHalf = Mathf.Tan(Mathf.Clamp(fovDeg, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            Screen = new Vector2(Mathf.Max(1, screen.x), Mathf.Max(1, screen.y));
            Aspect = Screen.x / Screen.y;
        }

        /// <summary>The camera a pose puts in place (CameraRig.Apply: rotation Euler(pitch, yaw, 0), backed off the target).</summary>
        public static LandProjector FromPose(CameraPose pose, float fovDeg, Vector2 screen)
        {
            Quaternion rot = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            return new LandProjector(pose.Target - rot * Vector3.forward * pose.Distance, rot, fovDeg, screen);
        }

        /// <summary>A world point's screen pixel (origin bottom-left) and its depth along the view; false behind the camera.</summary>
        public bool Project(Vector3 world, out Vector2 px, out float depth)
        {
            Vector3 v = Inverse * (world - Eye);
            depth = v.z;
            if (v.z <= 1e-4f)
            {
                px = default;
                return false;
            }

            float x = v.x / (v.z * TanHalf * Aspect), y = v.y / (v.z * TanHalf);
            px = new Vector2((x + 1) * 0.5f * Screen.x, (y + 1) * 0.5f * Screen.y);
            return true;
        }

        public bool Project(Vector3 world, out Vector2 px) => Project(world, out px, out _);

        /// <summary>The ray from the eye through a screen pixel (origin bottom-left).</summary>
        public Ray RayThrough(Vector2 px)
        {
            float x = (2 * px.x / Screen.x - 1) * TanHalf * Aspect, y = (2 * px.y / Screen.y - 1) * TanHalf;
            return new Ray(Eye, (Rotation * new Vector3(x, y, 1)).normalized);
        }
    }

    /// <summary>
    /// What may be picked now and within how many pixels (7.5): heads and discs 14 px, dots 6 px (only where a player's
    /// disc is large enough on screen for its dots to be told apart), ties 6 px, falls, rivers and the canal 8 px, the
    /// crown 8 px; a group is pickable while it is drawn at <see cref="MinAlpha"/> or more. <see cref="FromView"/> reads
    /// the land's view state; the harness builds its own.
    /// </summary>
    public struct PickOptions
    {
        public bool Dots, Players, Towers, Crown, Ties, Waters, Sectors, Pools, Cut;
        public float DotPx, HeadPx, TiePx, WaterPx, CrownPx, TowerPx;

        /// <summary>A player's disc must be at least this many pixels across (radius) for its dots to be picked.</summary>
        public float DotDiscPx;

        /// <summary>Emphasis tiers per kind (2 the view's subject, 1 shown, 0 faint): the subject wins a tie of kinds.</summary>
        public byte PlayersTier, TowersTier, CrownTier, TiesTier, WatersTier;

        /// <summary>A group drawn fainter than this cannot be picked.</summary>
        public const float MinAlpha = 0.25f;

        /// <summary>The thresholds at a UI scale (1080p pixels × scale).</summary>
        public static PickOptions Radii(float uiScale)
        {
            float s = Mathf.Max(0.25f, uiScale);
            return new PickOptions
            {
                DotPx = 6 * s, HeadPx = 14 * s, TiePx = 6 * s, WaterPx = 8 * s, CrownPx = 8 * s, TowerPx = 6 * s, DotDiscPx = 9 * s
            };
        }

        /// <summary>Everything pickable (tests): every group at full emphasis.</summary>
        public static PickOptions All(float uiScale)
        {
            PickOptions o = Radii(uiScale);
            o.Dots = o.Players = o.Towers = o.Crown = o.Ties = o.Waters = o.Sectors = o.Pools = o.Cut = true;
            o.PlayersTier = o.TowersTier = o.CrownTier = o.TiesTier = o.WatersTier = 1;
            return o;
        }

        /// <summary>What the view on screen draws (<see cref="LandView"/>): the land's groups only once it is revealed.</summary>
        public static PickOptions FromView(float uiScale)
        {
            PickOptions o = Radii(uiScale);
            bool land = LandView.Reveal >= 0.5f;
            o.Dots = land && LandView.Alpha(LandGroup.Dots) >= MinAlpha;
            o.Players = land && LandView.Alpha(LandGroup.Glyphs) >= MinAlpha;
            o.Towers = land && LandView.Alpha(LandGroup.Towers) >= MinAlpha;
            o.Crown = land && LandView.Alpha(LandGroup.Crown) >= MinAlpha;
            o.Ties = land && LandView.Alpha(LandGroup.Ties) >= MinAlpha;
            o.Waters = land && LandView.Alpha(LandGroup.Rivers) >= MinAlpha;
            o.Sectors = land && LandView.Alpha(LandGroup.Sectors) >= MinAlpha;
            o.Pools = land && LandView.Alpha(LandGroup.Pools) >= 0.5f;
            o.Cut = LandView.Alpha(LandGroup.Cut) >= MinAlpha;
            o.PlayersTier = Tier(LandGroup.Glyphs);
            o.TowersTier = Tier(LandGroup.Towers);
            o.CrownTier = Tier(LandGroup.Crown);
            o.TiesTier = Tier(LandGroup.Ties);
            o.WatersTier = Tier(LandGroup.Rivers);
            return o;
        }

        static byte Tier(LandGroup g)
        {
            float a = LandView.Alpha(g);
            return (byte)(a >= 0.9f ? 2 : a >= 0.45f ? 1 : 0);
        }
    }

    /// <summary>
    /// Everything of one snapshot a pick tests, in land-local coordinates, prepared once per snapshot (main thread): every
    /// adult member's dot (its sunflower slot at its reason, as the inhabitants draw it), every player's disc center, radius
    /// and head, the rivers' polylines by lane (falls apart), the towers. Ties are read from the season at pick time.
    /// </summary>
    public sealed class LandPickScene
    {
        public LandSnapshot Snapshot;

        /// <summary>Dots: land-local position, person, player.</summary>
        public Vector3[] DotAt = Array.Empty<Vector3>();

        public int[] DotPerson = Array.Empty<int>(), DotPlayer = Array.Empty<int>();

        /// <summary>Per player: the first dot's index in the dot arrays (dots are grouped by player).</summary>
        public int[] FirstDot = Array.Empty<int>();

        /// <summary>The water: polylines (land-local), their lane, and whether each is a fall.</summary>
        public readonly List<Vector3[]> Water = new List<Vector3[]>();

        public readonly List<int> WaterLane = new List<int>();
        public readonly List<bool> WaterFall = new List<bool>();

        /// <summary>
        /// The scene on screen for one camera (<see cref="See"/>): every player's disc center, head and disc radius, every
        /// water polyline's points, and the box around every tie's arc (its quadratic curve's three control points, which
        /// bound it). Made by the first pick of a camera pose and reused by the picks that follow until the camera, the
        /// land's frame or the screen changes: hovering with a still camera projects nothing again, and a pick far from a
        /// tie skips its arc.
        /// </summary>
        public Vector2[] CenterPx = Array.Empty<Vector2>(), HeadPx = Array.Empty<Vector2>();

        /// <summary>Per player: the disc's radius on screen (px) and whether its center and head are in front of the camera.</summary>
        public float[] DiscRadiusPx = Array.Empty<float>();

        public bool[] PlayerOnScreen = Array.Empty<bool>();

        /// <summary>The water polylines' points on screen, flattened (<see cref="WaterFirst"/>), and whether each is in front.</summary>
        public Vector2[] WaterPx = Array.Empty<Vector2>();

        public bool[] WaterPxOk = Array.Empty<bool>();
        public int[] WaterFirst = Array.Empty<int>();

        /// <summary>Per pair of the season: the box of its arc on screen (x0, y0, x1, y1); NaN x0 when it cannot be bounded.</summary>
        public Vector4[] TieBox = Array.Empty<Vector4>();

        LandProjector seenCam;
        LandFrame seenFrame;
        SocialSeasonResult seenSeason;
        bool seen;

        /// <summary>
        /// Projects the scene for a camera unless it already is (the same eye, rotation, field of view, screen and frame):
        /// players, waters and the ties' boxes (see <see cref="CenterPx"/>). Main thread; allocates only when the snapshot's
        /// sizes change.
        /// </summary>
        public void See(LandFrame frame, LandProjector cam)
        {
            SocialSeasonResult season = Snapshot?.Society;
            if (seen && season == seenSeason && SameCamera(cam, seenCam) && frame.Origin == seenFrame.Origin && frame.Rotation == seenFrame.Rotation) return;
            seen = true;
            seenCam = cam;
            seenFrame = frame;
            seenSeason = season;

            Player[] ps = Snapshot?.Players?.Players ?? Array.Empty<Player>();
            if (CenterPx.Length != ps.Length)
            {
                CenterPx = new Vector2[ps.Length];
                HeadPx = new Vector2[ps.Length];
                DiscRadiusPx = new float[ps.Length];
                PlayerOnScreen = new bool[ps.Length];
            }

            for (int pi = 0; pi < ps.Length; pi++)
            {
                Player p = ps[pi];
                Vector3 c = Figure.Center(p);
                bool ok = cam.Project(frame.World(c), out CenterPx[pi]) & cam.Project(frame.World(Figure.Head(p)), out HeadPx[pi]);
                PlayerOnScreen[pi] = ok;
                DiscRadiusPx[pi] = ok ? LandPick.DiscPx(p, c, frame, cam, CenterPx[pi]) : 0;
            }

            int points = 0;
            foreach (Vector3[] w in Water) points += w.Length;
            if (WaterPx.Length != points)
            {
                WaterPx = new Vector2[points];
                WaterPxOk = new bool[points];
            }

            if (WaterFirst.Length != Water.Count + 1) WaterFirst = new int[Water.Count + 1];
            int k = 0;
            for (int w = 0; w < Water.Count; w++)
            {
                WaterFirst[w] = k;
                foreach (Vector3 q in Water[w])
                {
                    WaterPxOk[k] = cam.Project(frame.World(q), out WaterPx[k]);
                    k++;
                }
            }

            WaterFirst[Water.Count] = k;

            int pairs = season?.PairA?.Length ?? 0;
            if (TieBox.Length != pairs) TieBox = new Vector4[pairs];
            for (int t = 0; t < pairs; t++)
            {
                int a = season.PairA[t], b = season.PairB[t];
                TieBox[t] = new Vector4(float.NaN, 0, 0, 0);
                if (a < 0 || b < 0 || a >= ps.Length || b >= ps.Length || !PlayerOnScreen[a] || !PlayerOnScreen[b]) continue;
                // the arc is the quadratic curve through the heads with its middle control point at twice its rise: the
                // three control points' projections bound the arc on screen (all three in front of the camera)
                Vector3 ha = Figure.Head(ps[a]), hb = Figure.Head(ps[b]);
                Vector3 mid = 0.5f * (ha + hb);
                mid.y += 2 * (LandPick.TiePoint(ha, hb, 0.5f).y - mid.y);
                if (!cam.Project(frame.World(mid), out Vector2 m)) continue;
                Vector2 pa = HeadPx[a], pb = HeadPx[b];
                TieBox[t] = new Vector4(Mathf.Min(pa.x, Mathf.Min(pb.x, m.x)), Mathf.Min(pa.y, Mathf.Min(pb.y, m.y)),
                    Mathf.Max(pa.x, Mathf.Max(pb.x, m.x)), Mathf.Max(pa.y, Mathf.Max(pb.y, m.y)));
            }
        }

        static bool SameCamera(LandProjector a, LandProjector b) =>
            a.Eye == b.Eye && a.Rotation == b.Rotation && a.TanHalf == b.TanHalf && a.Screen == b.Screen;

        /// <summary>The snapshot's dots and waters (members' reason from the lives; 0.4 where a member has no year, as drawn).</summary>
        public static LandPickScene Build(LandSnapshot s, EconomicLives lives)
        {
            LandPickScene c = new LandPickScene { Snapshot = s };
            Player[] ps = s?.Players?.Players;
            if (ps != null)
            {
                int n = 0;
                foreach (Player p in ps) n += p.Adults?.Length ?? 0;
                c.DotAt = new Vector3[n];
                c.DotPerson = new int[n];
                c.DotPlayer = new int[n];
                c.FirstDot = new int[ps.Length + 1];
                int k = 0;
                for (int pi = 0; pi < ps.Length; pi++)
                {
                    Player p = ps[pi];
                    c.FirstDot[pi] = k;
                    int m = p.Adults?.Length ?? 0;
                    for (int j = 0; j < m; j++)
                    {
                        float reason = 0.4f;
                        if (lives != null && lives.TryGet(p.Adults[j], s.Year, out PersonYear r)) reason = r.Reason;
                        c.DotAt[k] = Figure.DotSlot(p, j, m, reason);
                        c.DotPerson[k] = p.Adults[j];
                        c.DotPlayer[k] = pi;
                        k++;
                    }
                }

                c.FirstDot[ps.Length] = k;
            }

            if (s?.Money != null)
            {
                foreach (FlowPath f in s.Money.Paths)
                {
                    if (f.Points == null || f.Points.Length < 2 || f.Lane < 0) continue;
                    bool fall = f.Kind == FlowKind.Waterfall || f.Kind == FlowKind.Taxes && f.Points[0].y >= LandStyle.CanalY - 1e-3f &&
                        f.Points[f.Points.Length - 1].y < LandStyle.CanalY - 0.2f && f.Points.Length <= LandStyle.WaterfallPoints + 2;
                    if (f.Kind < FlowKind.River) continue;
                    c.Water.Add(f.Points);
                    c.WaterLane.Add(f.Lane);
                    c.WaterFall.Add(fall);
                }
            }

            return c;
        }
    }

    /// <summary>
    /// The cut's pickable geometry for a year (1.1), in world space under the timeline's lens: each industry's card bar
    /// (0.6 wide across the road, centered on the wall's plane, its wages / upkeep / owners heights from the wall's band
    /// at Y + 0.5) and every lifeline's dot alive at Y.
    /// </summary>
    public sealed class CutPickScene
    {
        public int Year;

        /// <summary>Per industry: the bar's four strip boundaries (world) bottom to top, at the bar's two sides.</summary>
        public Vector3[][] BarLeft = Array.Empty<Vector3[]>(), BarRight = Array.Empty<Vector3[]>();

        public Vector3[] DotAt = Array.Empty<Vector3>();
        public int[] DotPerson = Array.Empty<int>();

        public static CutPickScene Build(SmvPopulation pop, WallGeometry wall, int year, WarpState warp)
        {
            CutPickScene c = new CutPickScene { Year = year };
            double t = year + 0.5;
            float u = EconomyStage.U(t);
            if (wall != null && wall.IsValid)
            {
                int n = wall.IndustryCount;
                c.BarLeft = new Vector3[n][];
                c.BarRight = new Vector3[n][];
                Vector3 across = GraphWarp.NormalAt(u, warp);
                across.y = 0;
                across = across.sqrMagnitude > 1e-8f ? across.normalized * (0.5f * LandStyle.CardWidth) : Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    WallBand b = wall.BandAt(i, t);
                    float[] ys = { b.WagesLo, b.UpkeepLo, b.OwnersLo, b.Hi };
                    c.BarLeft[i] = new Vector3[4];
                    c.BarRight[i] = new Vector3[4];
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 w = GraphWarp.ToWorld(u, ys[k], b.Rho, warp);
                        c.BarLeft[i][k] = w - across;
                        c.BarRight[i][k] = w + across;
                    }
                }
            }

            if (pop?.Sim != null)
            {
                int step = pop.Sim.StepAt(t);
                List<Vector3> at = new List<Vector3>(4096);
                List<int> who = new List<int>(4096);
                IReadOnlyList<SmvPerson> people = pop.Sim.People;
                for (int i = 0; i < people.Count; i++)
                {
                    if (!pop.PointAt(people[i], step, out Vector3 d)) continue;
                    at.Add(GraphWarp.ToWorld(d.x, d.y, d.z, warp));
                    who.Add(i);
                }

                c.DotAt = at.ToArray();
                c.DotPerson = who.ToArray();
            }

            return c;
        }
    }

    /// <summary>
    /// Picking in the land (7.5): pure functions of a snapshot, a camera (<see cref="LandProjector"/>) and a screen point,
    /// so the land's picker module and the harness share them. Screen distances to heads and discs, dots (close views),
    /// towers (their projected top squares, or their axis), ties (the arc polyline), falls, rivers and the crown; the
    /// canal, the pools and the sectors by a polar hit test of the pointer's ray on the tread planes; the cut's bars and
    /// dots. <see cref="Pick"/> returns the best: a dot first, then the view's subject, then by distance; areas last.
    /// Also: what hovering a hit lights (<see cref="Ranges"/>, a sector with its roots in and out), the year a click on the
    /// wall means (<see cref="WallYear"/>) and the land's silhouette on screen (<see cref="BowlOutline"/>), for the panels' layout.
    /// </summary>
    public static class LandPick
    {
        /// <summary>Points sampled around the rim for the bowl's outline (each of its floor and its rim).</summary>
        const int BowlSamples = 48;

        /// <summary>
        /// Among things the pointer is on (0 px: a disc, a tower's top square), the one whose head or top center is nearer
        /// wins: this share of that distance is added.
        /// </summary>
        const float HeadTieBreak = 0.01f;

        /// <summary>The share of a tie's arc at each end left out of its pick (there it overlaps its players' heads).</summary>
        const float TieEnd = 0.15f;

        /// <summary>A player within this share of the head radius is picked before towers, ties and waters.</summary>
        const float OnGlyph = 0.35f;

        /// <summary>Root runs this close (roots between them) merge before any run is left unlit.</summary>
        const int MergeGap = 2;

        /// <summary>A canal hit between lanes or beside a thin one takes the nearest lane within this (world units).</summary>
        const float CanalSlack = 0.012f;

        /// <summary>The ids the highlighter can light at once (Highlighter.MaxRanges).</summary>
        public const int MaxRanges = 8;

        // ------------------------------------------------------------------ the whole pick

        /// <summary>
        /// The thing under a screen point (pixels, origin bottom-left): a dot within <see cref="PickOptions.DotPx"/> wins;
        /// then the nearest of players, towers, the crown, ties and waters relative to its own radius, the view's subject
        /// first (<see cref="PickOptions.PlayersTier"/> ...); then the canal, a pool or a sector under the ray; then the cut.
        /// </summary>
        public static LandHit Pick(LandPickScene scene, CutPickScene cut, LandFrame frame, LandProjector cam, Vector2 point,
            PickOptions o, int round)
        {
            LandSnapshot s = scene?.Snapshot;
            if (s != null)
            {
                scene.See(frame, cam);
                LandHit player = o.Players ? Players(scene, frame, point, o.HeadPx) : LandHit.None;
                if (o.Dots)
                {
                    // a dot first, unless another player's head is nearer the pointer than the dot
                    LandHit dot = Dots(scene, frame, cam, point, o.DotPx, o.DotDiscPx);
                    if (!dot.IsNone && (player.IsNone || player.Index == dot.Index || player.DistancePx >= dot.DistancePx)) return dot;
                }

                // the pointer on a glyph (its disc, stalk or head) picks the player over the arcs that cross it, unless a
                // tower's top square under the pointer stands in front of it
                LandHit tower = o.Towers ? Towers(s, frame, cam, point, o.TowerPx) : LandHit.None;
                if (!player.IsNone && player.DistancePx <= OnGlyph * o.HeadPx)
                {
                    bool towerInFront = !tower.IsNone && tower.DistancePx < 1f &&
                                        Depth(cam, tower.World) < Depth(cam, player.World);
                    return towerInFront ? tower : player;
                }

                LandHit best = LandHit.None;
                int bestTier = -1;
                float bestScore = float.PositiveInfinity;
                Consider(player, o.HeadPx, o.PlayersTier, ref best, ref bestTier, ref bestScore);
                Consider(tower, o.TowerPx, o.TowersTier, ref best, ref bestTier, ref bestScore);
                if (o.Crown) Consider(Crown(frame, cam, point, o.CrownPx), o.CrownPx, o.CrownTier, ref best, ref bestTier, ref bestScore);
                if (o.Ties) Consider(Ties(scene, frame, cam, point, o.TiePx), o.TiePx, o.TiesTier, ref best, ref bestTier, ref bestScore);
                if (o.Waters) Consider(Waters(scene, frame, point, o.WaterPx), o.WaterPx, o.WatersTier, ref best, ref bestTier, ref bestScore);
                if (!best.IsNone) return best;

                Ray ray = cam.RayThrough(point);
                if (o.Waters)
                {
                    LandHit canal = Canal(s, frame, ray);
                    if (!canal.IsNone) return canal;
                }

                if (o.Sectors)
                {
                    LandHit sector = Sector(s, frame, ray, o.Pools);
                    if (!sector.IsNone) return sector;
                }
            }

            if (o.Cut && cut != null)
            {
                LandHit c = CutDots(cut, cam, point, o.DotPx);
                if (!c.IsNone) return c;
                return CutBars(cut, cam, point);
            }

            return LandHit.None;
        }

        /// <summary>A world point's depth along the view (infinity behind the camera).</summary>
        static float Depth(LandProjector cam, Vector3 world) => cam.Project(world, out _, out float d) ? d : float.PositiveInfinity;

        /// <summary>Keeps a candidate when its tier is higher, or the same with a smaller distance relative to its radius.</summary>
        static void Consider(LandHit h, float radius, byte tier, ref LandHit best, ref int bestTier, ref float bestScore)
        {
            if (h.IsNone) return;
            float score = h.DistancePx / Mathf.Max(1e-3f, radius);
            if (tier > bestTier || tier == bestTier && score < bestScore)
            {
                best = h;
                bestTier = tier;
                bestScore = score;
            }
        }

        // ------------------------------------------------------------------ people

        /// <summary>
        /// The nearest member's dot within <paramref name="radiusPx"/>, among players whose disc is at least
        /// <paramref name="discPx"/> in radius on screen (in far views the dots of a disc are a pixel apart: the disc is
        /// picked instead).
        /// </summary>
        public static LandHit Dots(LandPickScene scene, LandFrame frame, LandProjector cam, Vector2 point, float radiusPx, float discPx)
        {
            LandHit best = LandHit.None;
            Player[] ps = scene.Snapshot?.Players?.Players;
            if (ps == null) return best;
            scene.See(frame, cam);
            float r2 = radiusPx * radiusPx;
            for (int pi = 0; pi < ps.Length && pi + 1 < scene.FirstDot.Length; pi++)
            {
                Player p = ps[pi];
                if (!scene.PlayerOnScreen[pi]) continue;
                Vector2 cp = scene.CenterPx[pi];
                float rPx = scene.DiscRadiusPx[pi];
                if (rPx < discPx) continue;
                // the pointer must be near the disc (dots float at most 0.28 above it)
                float reach = rPx + radiusPx + 0.3f * rPx / Mathf.Max(1e-3f, p.Radius);
                if ((cp - point).sqrMagnitude > reach * reach) continue;
                for (int k = scene.FirstDot[pi]; k < scene.FirstDot[pi + 1]; k++)
                {
                    if (!cam.Project(frame.World(scene.DotAt[k]), out Vector2 d)) continue;
                    float dd = (d - point).sqrMagnitude;
                    if (dd > r2 || dd >= best.DistancePx * best.DistancePx) continue;
                    best = new LandHit
                    {
                        Kind = PickKind.Dot, Index = pi, Person = scene.DotPerson[k], Part = -1, DistancePx = Mathf.Sqrt(dd),
                        World = frame.World(scene.DotAt[k])
                    };
                }
            }

            return best;
        }

        /// <summary>A disc's radius on screen (px): the largest of four rim points' distances from its projected center.</summary>
        internal static float DiscPx(Player p, Vector3 center, LandFrame frame, LandProjector cam, Vector2 cp)
        {
            float r = 0;
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f;
                if (cam.Project(frame.World(center + LandFrame.Polar(p.Radius, a, 0)), out Vector2 e)) r = Mathf.Max(r, (e - cp).magnitude);
            }

            return r;
        }

        /// <summary>The nearest player within <paramref name="radiusPx"/> of its head, or with the pointer on its disc (0 px).</summary>
        /// <remarks>Reads the scene's projection for the camera (<see cref="LandPickScene.See"/>, made by the caller).</remarks>
        public static LandHit Players(LandPickScene scene, LandFrame frame, Vector2 point, float radiusPx)
        {
            LandHit best = LandHit.None;
            Player[] ps = scene.Snapshot?.Players?.Players;
            if (ps == null || scene.PlayerOnScreen.Length != ps.Length) return best;
            for (int pi = 0; pi < ps.Length; pi++)
            {
                if (!scene.PlayerOnScreen[pi]) continue;
                Vector2 cp = scene.CenterPx[pi], hp = scene.HeadPx[pi];
                // on the disc or the stalk to the head counts as 0; among those the nearest head wins
                float head = (point - hp).magnitude;
                float d = Mathf.Max(0, Mathf.Min(SegmentDistance(point, cp, hp), (point - cp).magnitude - scene.DiscRadiusPx[pi]));
                if (d > radiusPx) continue;
                d += HeadTieBreak * head;
                if (d >= best.DistancePx) continue;
                best = new LandHit
                {
                    Kind = PickKind.Player, Index = pi, Person = -1, Part = -1, DistancePx = d, World = frame.World(Figure.Head(ps[pi]))
                };
            }

            return best;
        }

        // ------------------------------------------------------------------ capital

        /// <summary>
        /// A tower whose projected top square holds the pointer (0 px), else the nearest tower axis (foot to top) within
        /// <paramref name="radiusPx"/>.
        /// </summary>
        public static LandHit Towers(LandSnapshot s, LandFrame frame, LandProjector cam, Vector2 point, float radiusPx)
        {
            LandHit best = LandHit.None;
            TowerGeom[] ts = s.Land?.Towers;
            if (ts == null) return best;
            foreach (TowerGeom t in ts)
            {
                Vector3 top = LandFrame.Polar(t.R, t.Theta, t.TopY), foot = LandFrame.Polar(t.R, t.Theta, t.BaseY);
                if (!cam.Project(frame.World(top), out Vector2 tp) || !cam.Project(frame.World(foot), out Vector2 fp)) continue;
                Vector3 u = LandFrame.Radial(t.Theta), v = LandFrame.Tangent(t.Theta);
                float h = 0.5f * Mathf.Max(t.Side, LandStyle.PrivateTowerSide);
                bool all = cam.Project(frame.World(top + h * (-u - v)), out Vector2 q0) & cam.Project(frame.World(top + h * (u - v)), out Vector2 q1) &
                           cam.Project(frame.World(top + h * (u + v)), out Vector2 q2) & cam.Project(frame.World(top + h * (-u + v)), out Vector2 q3);
                // inside a top square counts as 0 (the nearest top center breaks ties between neighbors)
                float d = all && InQuad(point, q0, q1, q2, q3) ? HeadTieBreak * (point - tp).magnitude : SegmentDistance(point, fp, tp);
                if (d > radiusPx || d >= best.DistancePx) continue;
                best = new LandHit { Kind = PickKind.Tower, Index = t.Company, Person = -1, Part = -1, DistancePx = d, World = frame.World(top) };
            }

            return best;
        }

        /// <summary>The crown's ring within <paramref name="radiusPx"/>.</summary>
        public static LandHit Crown(LandFrame frame, LandProjector cam, Vector2 point, float radiusPx)
        {
            float d = float.PositiveInfinity;
            Vector2 prev = default;
            bool hasPrev = false;
            for (int k = 0; k <= LandStyle.CrownSegments; k++)
            {
                Vector3 w = frame.World(LandFrame.Polar(LandStyle.CrownR, 360f * k / LandStyle.CrownSegments, LandStyle.CrownY));
                if (!cam.Project(w, out Vector2 p))
                {
                    hasPrev = false;
                    continue;
                }

                if (hasPrev) d = Mathf.Min(d, SegmentDistance(point, prev, p));
                prev = p;
                hasPrev = true;
            }

            if (d > radiusPx) return LandHit.None;
            return new LandHit
            {
                Kind = PickKind.Crown, Index = 0, Person = -1, Part = -1, DistancePx = d, World = frame.World(new Vector3(0, LandStyle.CrownY, 0))
            };
        }

        // ------------------------------------------------------------------ society

        /// <summary>A point of a tie's arc (SocialLayer's: the chord between the heads lifted by a parabola to its apex).</summary>
        public static Vector3 TiePoint(Vector3 a, Vector3 b, float t)
        {
            float chord = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
            float apex = Mathf.Max(a.y, b.y) + LandStyle.TieLift + LandStyle.TieSlope * chord;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += 4 * t * (1 - t) * (apex - 0.5f * (a.y + b.y));
            return p;
        }

        /// <summary>
        /// The nearest tie of the season (its arc polyline, <see cref="LandStyle.TiePoints"/> segments) within the radius;
        /// a tie whose arc's box on screen (<see cref="LandPickScene.TieBox"/>) is further than the radius is skipped.
        /// </summary>
        public static LandHit Ties(LandPickScene scene, LandFrame frame, LandProjector cam, Vector2 point, float radiusPx)
        {
            LandHit best = LandHit.None;
            LandSnapshot s = scene.Snapshot;
            SocialSeasonResult season = s?.Society;
            Player[] ps = s?.Players?.Players;
            if (season?.PairA == null || ps == null) return best;
            scene.See(frame, cam);
            int n = LandStyle.TiePoints;
            for (int k = 0; k < season.PairA.Length; k++)
            {
                int a = season.PairA[k], b = season.PairB[k];
                if (a < 0 || b < 0 || a >= ps.Length || b >= ps.Length) continue;
                if (k < scene.TieBox.Length)
                {
                    Vector4 box = scene.TieBox[k];
                    if (!float.IsNaN(box.x) && (point.x < box.x - radiusPx || point.x > box.z + radiusPx || point.y < box.y - radiusPx ||
                                                point.y > box.w + radiusPx)) continue;
                }

                Vector3 ha = Figure.Head(ps[a]), hb = Figure.Head(ps[b]);
                Vector2 prev = default;
                bool hasPrev = false;
                float d = float.PositiveInfinity;
                for (int i = 0; i <= n; i++)
                {
                    // the arc's ends stand on the players' heads: a tie is picked along its span
                    float t = Mathf.Lerp(TieEnd, 1 - TieEnd, i / (float)n);
                    if (!cam.Project(frame.World(TiePoint(ha, hb, t)), out Vector2 p))
                    {
                        hasPrev = false;
                        continue;
                    }

                    if (hasPrev) d = Mathf.Min(d, SegmentDistance(point, prev, p));
                    prev = p;
                    hasPrev = true;
                }

                if (d > radiusPx || d >= best.DistancePx) continue;
                best = new LandHit
                {
                    Kind = PickKind.Tie, Index = k, Person = -1, Part = -1, DistancePx = d, World = frame.World(TiePoint(ha, hb, 0.5f))
                };
            }

            return best;
        }

        // ------------------------------------------------------------------ water

        /// <summary>
        /// The nearest river, distributary, tax river, abroad pour or fall (a fall within the radius wins), from the scene's
        /// projection for the camera (<see cref="LandPickScene.See"/>, made by the caller).
        /// </summary>
        public static LandHit Waters(LandPickScene scene, LandFrame frame, Vector2 point, float radiusPx)
        {
            LandHit best = LandHit.None;
            if (scene.WaterFirst.Length != scene.Water.Count + 1) return best;
            for (int w = 0; w < scene.Water.Count; w++)
            {
                Vector3[] pts = scene.Water[w];
                int first = scene.WaterFirst[w];
                bool hasPrev = false;
                float d = float.PositiveInfinity;
                Vector3 at = pts[0];
                for (int i = 0; i < pts.Length; i++)
                {
                    if (!scene.WaterPxOk[first + i])
                    {
                        hasPrev = false;
                        continue;
                    }

                    if (hasPrev)
                    {
                        float di = SegmentDistance(point, scene.WaterPx[first + i - 1], scene.WaterPx[first + i]);
                        if (di < d)
                        {
                            d = di;
                            at = pts[i];
                        }
                    }

                    hasPrev = true;
                }

                if (d > radiusPx) continue;
                bool fall = scene.WaterFall[w];
                // a fall beats a river within the radius (it names the river); otherwise the nearest
                bool better = best.IsNone || fall && best.Kind != PickKind.Fall || fall == (best.Kind == PickKind.Fall) && d < best.DistancePx;
                if (!better) continue;
                best = new LandHit
                {
                    Kind = fall ? PickKind.Fall : PickKind.River, Index = scene.WaterLane[w], Person = -1, Part = -1, DistancePx = d,
                    World = frame.World(at)
                };
            }

            return best;
        }

        /// <summary>The lane of the lip canal under the ray (its plane at <see cref="LandStyle.CanalY"/>), by the degree's lane radii.</summary>
        public static LandHit Canal(LandSnapshot s, LandFrame frame, Ray ray)
        {
            CanalLane[] lanes = s.Money?.Canal;
            if (lanes == null || !OnPlane(frame, ray, LandStyle.CanalY, out Vector3 local, out float dist)) return LandHit.None;
            float r = LandFrame.RadiusOf(local);
            if (r < LandStyle.CanalR0 - 0.02f || r > LandStyle.CanalR1 + 0.05f) return LandHit.None;
            int deg = Mathf.Clamp(Mathf.FloorToInt(LandFrame.ThetaOf(local)), 0, 359);
            int lane = -1;
            float nearest = CanalSlack;
            foreach (CanalLane l in lanes)
            {
                if (l == null || l.R1[deg] <= l.R0[deg]) continue;
                // inside the lane, else the nearest lane edge within the slack (thin lanes, the gaps between them)
                float d = r < l.R0[deg] ? l.R0[deg] - r : r > l.R1[deg] ? r - l.R1[deg] : -1;
                if (d < nearest)
                {
                    nearest = d;
                    lane = l.Lane;
                }
            }

            if (lane < 0) return LandHit.None;
            return new LandHit { Kind = PickKind.Canal, Index = lane, Person = -1, Part = -1, DistancePx = 0, World = frame.World(local) };
        }

        // ------------------------------------------------------------------ the land

        /// <summary>
        /// The sector (or its pool, when <paramref name="pools"/>) under the ray: the ray meets each tread's plane, the polar
        /// test keeps the hits inside a sector's radii and span, and the nearest to the eye wins (the stepped bowl's treads
        /// do not hide one another's insides from above).
        /// </summary>
        public static LandHit Sector(LandSnapshot s, LandFrame frame, Ray ray, bool pools)
        {
            SectorGeom[] sectors = s.Land?.Sectors;
            if (sectors == null) return LandHit.None;
            LandHit best = LandHit.None;
            for (int t = 0; t < 5; t++)
            {
                if (!OnPlane(frame, ray, LandStyle.TerraceY[t], out Vector3 local, out float dist) || dist >= best.DistancePx) continue;
                float r = LandFrame.RadiusOf(local), theta = LandFrame.ThetaOf(local);
                for (int i = 0; i < sectors.Length; i++)
                {
                    SectorGeom g = sectors[i];
                    if ((int)g.Tier != t || r < g.R0 || r > g.R1 || !LandFrame.InSpan(theta, g.Theta0, g.Theta1)) continue;
                    int part = r < g.RWages ? EconomyIds.SectorWages : r < g.RUpkeep ? EconomyIds.SectorUpkeep : EconomyIds.SectorOwners;
                    PickKind kind = PickKind.Sector;
                    if (pools && s.Money != null && g.Industry >= 0 && g.Industry < s.Money.PoolInflow.Length)
                    {
                        CanalRouting.PoolBand(g, s.Money.PoolInflow[g.Industry], out float p0, out float p1);
                        if (p1 > p0 && r >= p0)
                        {
                            kind = PickKind.Pool;
                            part = EconomyIds.SectorPool;
                        }
                    }

                    best = new LandHit { Kind = kind, Index = g.Industry, Person = -1, Part = part, DistancePx = dist, World = frame.World(local) };
                }
            }

            return best;
        }

        /// <summary>Where a ray meets the land's horizontal plane at a height (land-local point and distance along the ray).</summary>
        public static bool OnPlane(LandFrame frame, Ray ray, float y, out Vector3 local, out float distance)
        {
            Quaternion inv = Quaternion.Inverse(frame.Rotation);
            Vector3 o = frame.Local(ray.origin), d = inv * ray.direction;
            local = default;
            distance = float.PositiveInfinity;
            if (Mathf.Abs(d.y) < 1e-6f) return false;
            float t = (y - o.y) / d.y;
            if (t <= 0) return false;
            local = o + d * t;
            distance = t;
            return true;
        }

        // ------------------------------------------------------------------ the cut and the wall

        /// <summary>The nearest lifeline dot of the cut within the radius.</summary>
        public static LandHit CutDots(CutPickScene cut, LandProjector cam, Vector2 point, float radiusPx)
        {
            LandHit best = LandHit.None;
            float r2 = radiusPx * radiusPx;
            for (int k = 0; k < cut.DotAt.Length; k++)
            {
                if (!cam.Project(cut.DotAt[k], out Vector2 p)) continue;
                float dd = (p - point).sqrMagnitude;
                if (dd > r2 || dd >= best.DistancePx * best.DistancePx) continue;
                best = new LandHit
                {
                    Kind = PickKind.CutDot, Index = -1, Person = cut.DotPerson[k], Part = -1, DistancePx = Mathf.Sqrt(dd), World = cut.DotAt[k]
                };
            }

            return best;
        }

        /// <summary>The cut's bar (and its strip) whose projected quad holds the pointer.</summary>
        public static LandHit CutBars(CutPickScene cut, LandProjector cam, Vector2 point)
        {
            for (int i = 0; i < cut.BarLeft.Length; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    if (!cam.Project(cut.BarLeft[i][k], out Vector2 q0) || !cam.Project(cut.BarRight[i][k], out Vector2 q1) ||
                        !cam.Project(cut.BarRight[i][k + 1], out Vector2 q2) || !cam.Project(cut.BarLeft[i][k + 1], out Vector2 q3)) continue;
                    if (!InQuad(point, q0, q1, q2, q3)) continue;
                    return new LandHit
                    {
                        Kind = PickKind.CutBar, Index = i, Person = -1, Part = k, DistancePx = 0,
                        World = 0.5f * (cut.BarLeft[i][k] + cut.BarRight[i][k + 1])
                    };
                }
            }

            return LandHit.None;
        }

        /// <summary>
        /// The year a point on the industry wall stands for (the road's click in the overview and section views, 1.3): the
        /// wall's columns projected (foot to top at their year), the point inside the quad between two of them, the year
        /// interpolated along it. False when the point is off the wall.
        /// </summary>
        public static bool WallYear(WallGeometry wall, WarpState warp, LandProjector cam, Vector2 point, out double year)
        {
            year = 0;
            if (wall == null || !wall.IsValid) return false;
            IReadOnlyList<WallColumn> cols = wall.Columns;
            int last = wall.IndustryCount - 1;
            bool hasPrev = false;
            Vector2 pb = default, pt = default;
            for (int j = 0; j < cols.Count; j++)
            {
                WallColumn c = cols[j];
                bool ok = cam.Project(GraphWarp.ToWorld(c.U, c.Lo[0], c.Rho, warp), out Vector2 b) &
                          cam.Project(GraphWarp.ToWorld(c.U, c.Hi[last], c.Rho, warp), out Vector2 t);
                if (ok && hasPrev)
                {
                    if (InQuad(point, pb, b, t, pt))
                    {
                        // the share of the way from the previous column, along the quad's middle
                        Vector2 m0 = 0.5f * (pb + pt), m1 = 0.5f * (b + t), d = m1 - m0;
                        float f = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - m0, d) / d.sqrMagnitude) : 0;
                        year = cols[j - 1].Year + f * (c.Year - cols[j - 1].Year);
                        return true;
                    }
                }

                hasPrev = ok;
                pb = b;
                pt = t;
            }

            return false;
        }

        // ------------------------------------------------------------------ what a hit lights

        /// <summary>
        /// The id ranges hovering or selecting a hit lights (at most <see cref="MaxRanges"/>): a player its glyph; a dot or a
        /// cut dot its lifeline (the line on the road, the dot in the cut and in its player); a tower; a tie and its two
        /// players; a river's lane; the crown and its capital flows; a sector (or pool, or the cut's bar) the sector, the
        /// wall's band and the cut's bar, and its roots in and out (<see cref="RootRuns"/>).
        /// <paramref name="roots"/> is the sector's root indices (RootsModel.RootsOf), <paramref name="lineId"/> the dot's
        /// lifeline id, <paramref name="rootDollars"/> (optional) a root's dollars, to keep the largest when not all fit.
        /// </summary>
        public static void Ranges(LandHit h, LandSnapshot s, IReadOnlyList<int> roots, int lineId, List<IdRange> into,
            Func<int, double> rootDollars = null)
        {
            into.Clear();
            switch (h.Kind)
            {
                case PickKind.Player:
                    into.Add(EconomyIds.LandPlayers(h.Index, h.Index));
                    break;
                case PickKind.Dot:
                case PickKind.CutDot:
                    if (lineId >= 0) into.Add(IdRange.Single(lineId));
                    break;
                case PickKind.Tower:
                    into.Add(IdRange.Single(EconomyIds.LandTower(h.Index)));
                    break;
                case PickKind.Tie:
                    into.Add(IdRange.Single(EconomyIds.LandTie(h.Index)));
                    if (s?.Society?.PairA != null && h.Index < s.Society.PairA.Length)
                    {
                        int a = s.Society.PairA[h.Index], b = s.Society.PairB[h.Index];
                        into.Add(EconomyIds.LandPlayers(a, a));
                        into.Add(EconomyIds.LandPlayers(b, b));
                    }

                    break;
                case PickKind.Fall:
                case PickKind.River:
                case PickKind.Canal:
                    into.Add(IdRange.Single(EconomyIds.LandRiver(h.Index)));
                    break;
                case PickKind.Crown:
                    into.Add(IdRange.Single(EconomyIds.LandCrown));
                    into.Add(new IdRange(EconomyIds.LandCapital(EconomyIds.CapitalPayouts), EconomyIds.LandCapital(EconomyIds.CapitalAbroad)));
                    break;
                case PickKind.Sector:
                case PickKind.Pool:
                case PickKind.CutBar:
                    into.Add(EconomyIds.LandSectors(h.Index, h.Index));
                    into.Add(EconomyIds.Industries(h.Index, h.Index));
                    RootRuns(roots, MaxRanges - into.Count, into, rootDollars);
                    break;
            }
        }

        /// <summary>
        /// A sector's roots as id ranges within <paramref name="slots"/>: consecutive root indices form one range (the roots
        /// into a buyer are consecutive: one range). When the runs are more than the slots, runs at most
        /// <see cref="MergeGap"/> apart merge first (lighting at most that many foreign roots between them), then the runs
        /// carrying the fewest dollars (<paramref name="dollars"/> of a root index; null: the shortest runs) are left
        /// unlit. Returns the number of roots lit that are not the sector's (from merges).
        /// </summary>
        public static int RootRuns(IReadOnlyList<int> roots, int slots, List<IdRange> into, Func<int, double> dollars = null)
        {
            if (roots == null || roots.Count == 0 || slots <= 0) return 0;
            List<(int lo, int hi, double v)> runs = new List<(int, int, double)>();
            double Weight(int k) => dollars != null ? dollars(k) : 1;
            int lo = roots[0], hi = roots[0];
            double v = Weight(roots[0]);
            for (int i = 1; i < roots.Count; i++)
            {
                if (roots[i] == hi + 1)
                {
                    hi = roots[i];
                    v += Weight(roots[i]);
                    continue;
                }

                runs.Add((lo, hi, v));
                lo = hi = roots[i];
                v = Weight(roots[i]);
            }

            runs.Add((lo, hi, v));
            int foreign = 0;
            while (runs.Count > slots)
            {
                int at = -1, gap = int.MaxValue;
                for (int i = 0; i + 1 < runs.Count; i++)
                {
                    int g = runs[i + 1].lo - runs[i].hi - 1;
                    if (g < gap)
                    {
                        gap = g;
                        at = i;
                    }
                }

                if (at < 0 || gap > MergeGap) break;
                runs[at] = (runs[at].lo, runs[at + 1].hi, runs[at].v + runs[at + 1].v);
                runs.RemoveAt(at + 1);
                foreign += gap;
            }

            while (runs.Count > slots)
            {
                int weakest = 0;
                for (int i = 1; i < runs.Count; i++)
                {
                    if (runs[i].v < runs[weakest].v) weakest = i;
                }

                runs.RemoveAt(weakest);
            }

            foreach ((int a, int b, double _) in runs) into.Add(new IdRange(EconomyIds.LandRoot(a), EconomyIds.LandRoot(b)));
            return foreign;
        }

        // ------------------------------------------------------------------ the bowl on screen

        /// <summary>
        /// The bowl's box on screen as fractions of the screen from its top-left corner (x right, y down): the box around
        /// <see cref="BowlOutline"/>, clipped to the screen; empty (w or h 0) when none of it is in front of the camera.
        /// </summary>
        public static Rect BowlBox(LandFrame frame, LandProjector cam)
        {
            List<Vector2> hull = new List<Vector2>(2 * BowlSamples + 64);
            BowlOutline(frame, cam, null, hull, new List<Vector2>(2 * BowlSamples + 64));
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
            foreach (Vector2 p in hull)
            {
                x0 = Mathf.Min(x0, p.x);
                x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.y);
                y1 = Mathf.Max(y1, p.y);
            }

            if (x1 < x0) return new Rect(0, 0, 0, 0);
            x0 = Mathf.Clamp01(x0);
            x1 = Mathf.Clamp01(x1);
            y0 = Mathf.Clamp01(y0);
            y1 = Mathf.Clamp01(y1);
            return x1 > x0 && y1 > y0 ? new Rect(x0, y0, x1 - x0, y1 - y0) : new Rect(0, 0, 0, 0);
        }

        /// <summary>
        /// The land's silhouette on screen, the panels keep clear of (7.2, WP5): the convex hull of the bowl's outer wall
        /// (its floor and its rim, radius <see cref="LandStyle.RimR"/>, <see cref="BowlSamples"/> points each), raised by a
        /// player's height at the rim (the glyphs that stand on it), the crown with its players' heads, and the towers' tops
        /// (<paramref name="towers"/>, optional). Points in fractions of the screen from its top-left corner (x right, y
        /// down), in order around the hull, into <paramref name="hull"/> (cleared); <paramref name="scratch"/> holds the
        /// projected points. Points behind the camera are left out; empty when none is in front. Not clipped to the screen.
        /// </summary>
        public static void BowlOutline(LandFrame frame, LandProjector cam, TowerGeom[] towers, List<Vector2> hull, List<Vector2> scratch)
        {
            hull.Clear();
            scratch.Clear();
            float rimTop = LandStyle.RimY + LandStyle.HeadY + 0.1f, crownTop = LandStyle.CrownY + LandStyle.HeadY + 0.1f;
            for (int k = 0; k < BowlSamples; k++)
            {
                float a = 360f * k / BowlSamples;
                Outline(frame, cam, LandFrame.Polar(LandStyle.RimR, a, 0), scratch);
                Outline(frame, cam, LandFrame.Polar(LandStyle.RimR, a, rimTop), scratch);
                if (k % 4 == 0) Outline(frame, cam, LandFrame.Polar(LandStyle.CrownR, a, crownTop), scratch);
            }

            if (towers != null)
            {
                foreach (TowerGeom t in towers) Outline(frame, cam, LandFrame.Polar(t.R, t.Theta, t.TopY), scratch);
            }

            ConvexHull(scratch, hull);
        }

        static void Outline(LandFrame frame, LandProjector cam, Vector3 local, List<Vector2> into)
        {
            if (cam.Project(frame.World(local), out Vector2 p)) into.Add(new Vector2(p.x / cam.Screen.x, 1 - p.y / cam.Screen.y));
        }

        /// <summary>Orders points by x, then y (the monotone chain's sweep).</summary>
        static readonly Comparison<Vector2> ByXThenY = (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);

        /// <summary>
        /// The convex hull of <paramref name="points"/> (sorted in place) into <paramref name="hull"/> (cleared), in order
        /// around it (Andrew's monotone chain; collinear points dropped).
        /// </summary>
        public static void ConvexHull(List<Vector2> points, List<Vector2> hull)
        {
            hull.Clear();
            int n = points.Count;
            if (n < 3)
            {
                hull.AddRange(points);
                return;
            }

            points.Sort(ByXThenY);
            for (int i = 0; i < n; i++)
            {
                while (hull.Count >= 2 && Turn(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            int lower = hull.Count + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (hull.Count >= lower && Turn(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            hull.RemoveAt(hull.Count - 1);
        }

        static float Turn(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        // ------------------------------------------------------------------ geometry

        /// <summary>Distance from a point to a segment (px).</summary>
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0;
            return (p - (a + t * ab)).magnitude;
        }

        /// <summary>Whether a point lies inside a polygon (corners in order, either winding; convex or not).</summary>
        public static bool InQuad(Vector2 p, Vector2[] q)
        {
            bool inside = false;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++) Cross(p, q[i], q[j], ref inside);
            return inside;
        }

        /// <summary>Whether a point lies inside a quadrilateral of four corners in order (either winding; convex or not).</summary>
        public static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            bool inside = false;
            Cross(p, a, d, ref inside);
            Cross(p, b, a, ref inside);
            Cross(p, c, b, ref inside);
            Cross(p, d, c, ref inside);
            return inside;
        }

        /// <summary>The even-odd rule's step: flips <paramref name="inside"/> when the edge a-b crosses the ray right of p.</summary>
        static void Cross(Vector2 p, Vector2 a, Vector2 b, ref bool inside)
        {
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-12f) + a.x) inside = !inside;
        }

        /// <summary>A hit in words, for logs and the harness ("player frontline|trade|R", "sector trade (owners)").</summary>
        public static string Describe(LandHit h, LandSnapshot s, Data.EconomyData data)
        {
            string Industry(int i) => data != null && i >= 0 && i < data.Industries.Count ? data.Industries[i].Id : i.ToString(LandFacts.Ci);
            string Part(int part) => part == EconomyIds.SectorWages ? "wages" : part == EconomyIds.SectorUpkeep ? "upkeep"
                : part == EconomyIds.SectorOwners ? "owners" : part == EconomyIds.SectorPool ? "pool" : "";
            Player[] ps = s?.Players?.Players;
            switch (h.Kind)
            {
                case PickKind.None: return "nothing";
                case PickKind.Dot:
                    return "dot person " + h.Person.ToString(LandFacts.Ci) + " of player " + (ps != null && h.Index < ps.Length ? ps[h.Index].Key : "?");
                case PickKind.Player: return "player " + (ps != null && h.Index < ps.Length ? ps[h.Index].Key : "?");
                case PickKind.Tower:
                    foreach (TowerGeom t in s?.Land?.Towers ?? Array.Empty<TowerGeom>())
                    {
                        if (t.Company == h.Index) return "tower " + (string.IsNullOrEmpty(t.Ticker) ? t.Name : t.Ticker);
                    }

                    return "tower " + h.Index.ToString(LandFacts.Ci);
                case PickKind.Tie:
                    if (s?.Society?.PairA != null && ps != null && h.Index < s.Society.PairA.Length)
                    {
                        return "tie " + h.Index.ToString(LandFacts.Ci) + " " + ps[s.Society.PairA[h.Index]].Key + " ~ " + ps[s.Society.PairB[h.Index]].Key;
                    }

                    return "tie " + h.Index.ToString(LandFacts.Ci);
                case PickKind.Fall: return "fall " + LaneId(h.Index);
                case PickKind.River: return "river " + LaneId(h.Index);
                case PickKind.Canal: return "canal lane " + LaneId(h.Index);
                case PickKind.Crown: return "crown";
                case PickKind.Sector: return "sector " + Industry(h.Index) + " (" + Part(h.Part) + ")";
                case PickKind.Pool: return "pool " + Industry(h.Index);
                case PickKind.CutDot: return "cut dot person " + h.Person.ToString(LandFacts.Ci);
                case PickKind.CutBar: return "cut bar " + Industry(h.Index) + " (" + (h.Part == 0 ? "wages" : h.Part == 1 ? "upkeep" : "owners") + ")";
            }

            return h.Kind.ToString();
        }

        /// <summary>A lane's id: the category ("jeopardy"), "taxes" or "abroad".</summary>
        public static string LaneId(int lane) =>
            lane >= 0 && lane < 6 ? Data.EconomyData.CategoryIds[lane] : lane == LandStyle.LaneTaxes ? "taxes" : lane == LandStyle.LaneAbroad ? "abroad" : "?";
    }
}
