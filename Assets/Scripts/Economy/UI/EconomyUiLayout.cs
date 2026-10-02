using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Why.Economy.UI
{
    /// <summary>
    /// A rectangle of a canvas in canvas units, measured from the canvas's top-left corner (x to the right, y down), the
    /// way the HUD lays out its blocks. Empty when it has no area (a hidden block).
    /// </summary>
    public struct UiBox
    {
        public float X, Y, W, H;

        public UiBox(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public static readonly UiBox Empty = new UiBox(0, 0, 0, 0);

        public float Right => X + W;
        public float Bottom => Y + H;
        public bool IsEmpty => W <= 0 || H <= 0;

        /// <summary>True when the two boxes share area (touching edges do not count).</summary>
        public bool Overlaps(UiBox o) => !IsEmpty && !o.IsEmpty && X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

        /// <summary>The smallest box around both (an empty box adds nothing).</summary>
        public static UiBox Union(UiBox a, UiBox b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;
            float x = Mathf.Min(a.X, b.X), y = Mathf.Min(a.Y, b.Y);
            return new UiBox(x, y, Mathf.Max(a.Right, b.Right) - x, Mathf.Max(a.Bottom, b.Bottom) - y);
        }

        /// <summary>True when the two boxes share a stretch of x.</summary>
        public bool OverlapsX(float x0, float x1) => !IsEmpty && X < x1 && x0 < Right;

        /// <summary>Same box within half a canvas unit (layouts are redone only when something really moved).</summary>
        public bool Near(UiBox o) =>
            Mathf.Abs(X - o.X) < 0.5f && Mathf.Abs(Y - o.Y) < 0.5f && Mathf.Abs(W - o.W) < 0.5f && Mathf.Abs(H - o.H) < 0.5f;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0}, {1:0}, {2:0} x {3:0})", X, Y, W, H);
    }

    /// <summary>
    /// The land's silhouette on screen (canvas units, from the canvas's top-left corner): a convex polygon, the hull of the
    /// bowl's wall, the players standing on its rim, the crown and the towers' tops (<see cref="Land.LandPick.BowlOutline"/>).
    /// The panels test against it rather than against its box, so a panel may stand in a corner the bowl's ellipse leaves
    /// free. Immutable: a new one is made when the view's pose, the screen or the land changes, so frames compare it by
    /// reference.
    /// </summary>
    public sealed class BowlShape
    {
        readonly Vector2[] points;

        /// <summary>+1 when the points run counter-clockwise in the canvas's axes (y down), -1 otherwise.</summary>
        readonly float winding;

        /// <summary>The box around the whole polygon (not clipped to the canvas).</summary>
        public readonly UiBox Bounds;

        public int Count => points.Length;
        public Vector2 this[int i] => points[i];

        /// <summary>The hull from points in fractions of the screen (top-left origin, in order around it), scaled to a canvas.</summary>
        public BowlShape(IReadOnlyList<Vector2> fractions, Vector2 canvas)
        {
            points = new Vector2[fractions.Count];
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity, area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = new Vector2(fractions[i].x * canvas.x, fractions[i].y * canvas.y);
                points[i] = p;
                x0 = Mathf.Min(x0, p.x);
                x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.y);
                y1 = Mathf.Max(y1, p.y);
            }

            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++) area += points[j].x * points[i].y - points[i].x * points[j].y;
            winding = area >= 0 ? 1 : -1;
            Bounds = points.Length > 0 ? new UiBox(x0, y0, x1 - x0, y1 - y0) : UiBox.Empty;
        }

        /// <summary>
        /// True when the box and the polygon share area (separating axes: the box's two and every edge's normal; touching
        /// does not count).
        /// </summary>
        public bool Overlaps(UiBox b)
        {
            if (points.Length < 3 || b.IsEmpty || !(b.X < Bounds.Right && Bounds.X < b.Right && b.Y < Bounds.Bottom && Bounds.Y < b.Bottom)) return false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                // the edge's outward normal: the polygon lies on its inner side, so the box is clear when its nearest corner is outside
                Vector2 e = points[i] - points[j];
                Vector2 n = winding > 0 ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
                float edge = Vector2.Dot(points[j], n);
                float nearest = (n.x >= 0 ? b.X : b.Right) * n.x + (n.y >= 0 ? b.Y : b.Bottom) * n.y;
                if (nearest >= edge) return false;
            }

            return true;
        }

        /// <summary>The lowest point (largest y) of the polygon within the column x0..x1; negative infinity when it misses the column.</summary>
        public float LowestIn(float x0, float x1)
        {
            float low = float.NegativeInfinity;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j], c = points[i];
                if (c.x >= x0 && c.x <= x1) low = Mathf.Max(low, c.y);
                Crossing(a, c, x0, ref low);
                Crossing(a, c, x1, ref low);
            }

            return low;
        }

        /// <summary>The highest point (smallest y) of the polygon within the column x0..x1; positive infinity when it misses the column.</summary>
        public float HighestIn(float x0, float x1)
        {
            float high = float.NegativeInfinity;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j], c = points[i];
                if (c.x >= x0 && c.x <= x1) high = Mathf.Max(high, -c.y);
                Crossing(new Vector2(a.x, -a.y), new Vector2(c.x, -c.y), x0, ref high);
                Crossing(new Vector2(a.x, -a.y), new Vector2(c.x, -c.y), x1, ref high);
            }

            return -high;
        }

        static void Crossing(Vector2 a, Vector2 c, float x, ref float low)
        {
            if ((a.x - x) * (c.x - x) > 0 || Mathf.Abs(c.x - a.x) < 1e-6f) return;
            low = Mathf.Max(low, Mathf.Lerp(a.y, c.y, (x - a.x) / (c.x - a.x)));
        }
    }

    /// <summary>
    /// The screen as the economy's own UI sees it: the canvas, its orientation and safe insets, the HUD's spacing, the
    /// boxes of everything the economy's panels must keep clear of (the HUD's title, its tour and help buttons, the F3
    /// stats, the legend and lifeline readout, the preset bar; the social panel and the land legend), and the bowl's box
    /// on screen in the view being shown (empty when the land is not in view). Measured from the live UI each frame (see
    /// <see cref="HudBlocks"/>); hidden blocks are empty.
    /// </summary>
    public struct HudFrame
    {
        public Vector2 Canvas;
        public bool Portrait;
        public float SafeTop, SafeBottom;

        /// <summary>Distance of corner blocks from the screen edge and between neighbors (HudKit's, by orientation).</summary>
        public float Margin, Gap;

        public UiBox Title, TopButtons, DevStats, Legend, Readout, PresetBar, SocialPanel, LandLegend;

        /// <summary>
        /// A portrait sheet that is up (the social panel's or an inspector's; empty in landscape): the land legend keeps
        /// clear of it (at the top, or it steps aside while the sheet is up).
        /// </summary>
        public UiBox Sheet;

        /// <summary>
        /// The bowl's box in the view's pose (the preset's destination, not the camera mid-flight, so panels do not move
        /// during a flight), clipped to the canvas. Empty while the land is not in view.
        /// </summary>
        public UiBox Bowl;

        /// <summary>
        /// The land's silhouette in the view's pose (<see cref="BowlShape"/>): the panels never cover it (7.2). Null while the
        /// land is not in view, or where only the box is known (then the box stands for it).
        /// </summary>
        public BowlShape Shape;

        /// <summary>True when nothing moved by half a canvas unit or more (no new layout needed).</summary>
        public bool Near(HudFrame o) =>
            ReferenceEquals(Shape, o.Shape) &&
            Portrait == o.Portrait && Mathf.Abs(Canvas.x - o.Canvas.x) < 0.5f && Mathf.Abs(Canvas.y - o.Canvas.y) < 0.5f &&
            Mathf.Abs(SafeTop - o.SafeTop) < 0.5f && Mathf.Abs(SafeBottom - o.SafeBottom) < 0.5f &&
            Title.Near(o.Title) && TopButtons.Near(o.TopButtons) && DevStats.Near(o.DevStats) && Legend.Near(o.Legend) &&
            Readout.Near(o.Readout) && PresetBar.Near(o.PresetBar) && SocialPanel.Near(o.SocialPanel) &&
            LandLegend.Near(o.LandLegend) && Sheet.Near(o.Sheet) && Bowl.Near(o.Bowl);
    }

    /// <summary>
    /// Where the economy's controls and panels go, as pure functions of a <see cref="HudFrame"/> (tested headless in the
    /// harness; the modules only apply the boxes to their RectTransforms):
    /// <list type="bullet">
    /// <item>the year chip stands at the right edge under the HUD's tour and help buttons (and under the F3 stats while they
    /// show), in portrait in the column beside the title;</item>
    /// <item>the year's readout runs under the chip at the right edge; in portrait, under the title block;</item>
    /// <item>the inspectors (the person's and the player's, one at a time) hang below them at the right edge (landscape),
    /// down to the first HUD block beneath it in its column, scaled down when their content is taller than that room or
    /// when the bowl reaches into the column (<see cref="ColumnBox"/>: where the right column is too narrow beside the bowl
    /// they dock at the left under the title, and as a last resort shrink below <see cref="MinScale"/>, never over the
    /// bowl). On a portrait screen with the land in view they are bottom sheets
    /// (<see cref="SheetBox"/>: the lower part of the frame, at most <see cref="SheetShare"/> of the height, never over the
    /// bowl; it covers the preset bar and the HUD's legend while it is up);
    /// on the road they are a top sheet under the title taking at most <see cref="PortraitShare"/> of the room, so a strip
    /// of the graph stays visible beneath it (<see cref="PersonRegion"/>: where the N key frames a person);</item>
    /// <item>the social panel stands at the bottom-left above the land legend (landscape) or is a bottom sheet (portrait);
    /// the land legend stands at the bottom-left above the HUD's legend and lifeline readout, or where the bowl takes that
    /// place at the top-left under the title, with its notes collapsed or without its swatches where it must (<see cref="LandLegendPlace"/>).</item>
    /// </list>
    /// Everything keeps clear of the land's silhouette (<see cref="BowlShape"/>, a gap from it), not of its box.
    /// <see cref="Check"/> lists any overlap with the HUD's blocks, the other panels and the bowl: the modules run it after
    /// every layout (a runtime self-check that logs once per layout), and the harness runs it for every land view at both
    /// aspects.
    /// </summary>
    public static class EconomyUiLayout
    {
        /// <summary>Height of the HUD's tour and help buttons (Hud.TopButtonHeight), when they cannot be measured.</summary>
        public const float TopButtonHeight = 30f;

        /// <summary>Height of the HUD's title block (Hud.TitleHeight), when it cannot be measured.</summary>
        public const float TitleHeight = 90f;

        /// <summary>Gap between the year chip and the readout beneath it (they read as one block).</summary>
        public const float ChipGap = 6f;

        /// <summary>Width of the inspector's landscape column (reference pixels; <see cref="PersonPanel"/> lays out for it).</summary>
        public const float LandscapeInspectorWidth = 360f;

        /// <summary>Smallest scale the inspector is drawn at to fit (below it the content would be unreadable; it then
        /// runs past its room instead, which <see cref="Check"/> reports).</summary>
        public const float MinScale = 0.6f;

        /// <summary>
        /// The last resort below <see cref="MinScale"/>: a panel shrinks this far rather than cover the bowl (text ~8 px at
        /// 1080p, ~13 px on a 1080x1920 phone), where neither side of the bowl has room at <see cref="MinScale"/>.
        /// </summary>
        public const float FloorScale = 0.45f;

        /// <summary>
        /// Share of the room between the year controls and the HUD's bottom blocks the portrait sheet may take. The
        /// people's band sits in the middle of a portrait screen: a sheet down to the legend (the whole room, ~470 of 889
        /// canvas units at 1080x1920) hid it, and with it the line the viewer had just clicked or N had just framed. A
        /// third of the room (~150 units, 330 px) stays for the graph.
        /// </summary>
        public const float PortraitShare = 0.66f;

        /// <summary>
        /// Smallest scale of the portrait sheet within its share: 13-unit text stays ~22 px on a 1080x1920 frame (the
        /// HUD's is 28). Content taller than that runs further down (the self-check does not object: it is still
        /// inside the room).
        /// </summary>
        public const float PortraitMinScale = 0.8f;

        /// <summary>
        /// How far the N key moves a person's point from the middle of the free region toward the future, as a share of
        /// the region's half extent: more of the life that led to the year shows, and less of the void past the present
        /// (0.55 puts it ~62% across a 1080p landscape screen, beside the inspector, and ~78% across a portrait one).
        /// </summary>
        public const float FutureShift = 0.55f;

        /// <summary>
        /// The portrait bottom sheets (the social panel; the inspectors while the land is in view) take at most this share of
        /// the screen's height (7.2: at most 38%).
        /// </summary>
        public const float SheetShare = 0.38f;

        /// <summary>Width of the social panel and of the land legend in landscape (canvas units; the bottom-left column).</summary>
        public const float LeftColumnWidth = 380f;

        /// <summary>Top of the controls column: below the tour and help buttons, and below the F3 stats while they show.</summary>
        public static float ControlsTop(HudFrame f)
        {
            float top = f.TopButtons.IsEmpty ? f.Margin + f.SafeTop + TopButtonHeight : f.TopButtons.Bottom;
            top += f.Gap;
            if (!f.DevStats.IsEmpty) top = Mathf.Max(top, f.DevStats.Bottom + f.Gap);
            return top;
        }

        /// <summary>The year chip (size in canvas units), right-aligned at the margin under the buttons.</summary>
        public static UiBox Chip(HudFrame f, Vector2 size) =>
            new UiBox(f.Canvas.x - f.Margin - size.x, ControlsTop(f), size.x, size.y);

        /// <summary>
        /// The chip's width: <paramref name="preferred"/>, or in portrait at most the width of the column beside the title
        /// (the tour and help buttons' width), so it never reaches under the title.
        /// </summary>
        public static float ChipWidth(HudFrame f, float preferred) =>
            f.Portrait && !f.TopButtons.IsEmpty ? Mathf.Min(preferred, Mathf.Max(110f, f.TopButtons.W)) : preferred;

        /// <summary>The widest the year's readout runs before it wraps (canvas units): a short column under the chip.</summary>
        public static float ReadoutMaxWidth(HudFrame f) => f.Portrait ? f.Canvas.x - 2 * f.Margin : 470f;

        /// <summary>
        /// The year's readout: right-aligned under the chip; in portrait under the title block (beside the title there is
        /// only room for the chip). Empty (the land legend shows it instead) on a portrait screen with the land in view
        /// (<see cref="ReadoutInLegend"/>) and wherever the view's bowl reaches its place.
        /// </summary>
        public static UiBox Readout(HudFrame f, UiBox chip, Vector2 size)
        {
            if (ReadoutInLegend(f)) return UiBox.Empty;
            UiBox box = f.Portrait
                ? new UiBox(f.Canvas.x - f.Margin - size.x, Mathf.Max(TitleBottom(f), chip.Bottom) + f.Gap, size.x, size.y)
                : new UiBox(f.Canvas.x - f.Margin - size.x, chip.Bottom + ChipGap, size.x, size.y);

            // never over the bowl: where the view's bowl reaches the readout's place it moves into the land legend
            return OnBowl(f, box, f.Gap) ? UiBox.Empty : box;
        }

        /// <summary>
        /// On a portrait screen with the land in view the bowl fills the band under the title: the year's readout moves into
        /// the land legend at the bottom (<see cref="LandLegend"/>), and the chip keeps only its year row (dragging across it
        /// scrubs).
        /// </summary>
        public static bool ReadoutInLegend(HudFrame f) => f.Portrait && !f.Bowl.IsEmpty;

        /// <summary>Bottom of the HUD's title block (measured, or its authored height).</summary>
        public static float TitleBottom(HudFrame f) =>
            f.Title.IsEmpty ? f.Margin + f.SafeTop + TitleHeight : f.Title.Bottom;

        /// <summary>Width of the inspector as drawn: the landscape column, or the portrait sheet across the screen.</summary>
        public static float InspectorWidth(HudFrame f) =>
            f.Portrait ? Mathf.Max(1, f.Canvas.x - 2 * f.Margin) : LandscapeInspectorWidth;

        /// <summary>
        /// Top of the inspector: below <paramref name="above"/> (the year controls: the readout, or the chip when the
        /// readout is hidden), and in portrait below the HUD's title too.
        /// </summary>
        public static float InspectorTop(HudFrame f, UiBox above)
        {
            float top = (above.IsEmpty ? ControlsTop(f) : above.Bottom) + f.Gap;
            return f.Portrait ? Mathf.Max(top, TitleBottom(f) + f.Gap) : top;
        }

        /// <summary>The y where the inspector's room ends: the first HUD block beneath it in its column.</summary>
        public static float InspectorLimit(HudFrame f, UiBox above)
        {
            float w = InspectorWidth(f), x0 = f.Canvas.x - f.Margin - w;
            return RoomBelow(f, InspectorTop(f, above), x0, x0 + w);
        }

        /// <summary>
        /// The scale the inspector is drawn at for content <paramref name="height"/> tall at scale 1 (laid out at
        /// <see cref="InspectorWidth"/>): landscape shrinks it into its room (not below <see cref="MinScale"/>); the
        /// portrait sheet into <see cref="PortraitShare"/> of the room (not below <see cref="PortraitMinScale"/>), and
        /// into the whole room (not below <see cref="MinScale"/>) when even that is too tall.
        /// </summary>
        public static float InspectorScale(HudFrame f, UiBox above, float height)
        {
            if (height <= 0) return 1;
            float room = InspectorLimit(f, above) - InspectorTop(f, above);
            if (!f.Portrait) return Mathf.Clamp(room / height, MinScale, 1);
            float scale = Mathf.Min(1, Mathf.Max(PortraitMinScale, room * PortraitShare / height));
            return height * scale <= room ? scale : Mathf.Clamp(room / height, MinScale, 1);
        }

        /// <summary>
        /// The inspector as drawn: content laid out <paramref name="width"/> wide and <paramref name="height"/> tall,
        /// drawn at <paramref name="scale"/>, its right edge at the margin. (The portrait sheet lays its content out at
        /// <see cref="InspectorWidth"/> / scale, so it still spans the screen when scaled.)
        /// </summary>
        public static UiBox InspectorBox(HudFrame f, UiBox above, float width, float height, float scale) =>
            new UiBox(f.Canvas.x - f.Margin - width * scale, InspectorTop(f, above), width * scale, height * scale);

        /// <summary>
        /// The part of the canvas the inspector leaves to the graph, where the N key frames a person: left of the
        /// landscape column (the full height), or in portrait the strip between the sheet and the HUD's bottom blocks.
        /// The sheet is <paramref name="sheet"/> while it is shown (the next person's is about as tall), else taken at its
        /// largest share of the room (<see cref="PortraitShare"/>; a sheet at <see cref="PortraitMinScale"/> may run a
        /// little further).
        /// </summary>
        public static UiBox PersonRegion(HudFrame f, UiBox above, UiBox sheet)
        {
            if (!f.Portrait)
            {
                // beside the column: right of it when the inspector docked at the left (the bowl took the right column)
                if (!sheet.IsEmpty && sheet.X + 0.5f * sheet.W < 0.5f * f.Canvas.x)
                {
                    float left = sheet.Right + f.Gap;
                    return new UiBox(left, 0, Mathf.Max(1, f.Canvas.x - left), f.Canvas.y);
                }

                float right = f.Canvas.x - f.Margin - LandscapeInspectorWidth - f.Gap;
                return new UiBox(0, 0, Mathf.Max(1, right), f.Canvas.y);
            }

            if (BottomSheets(f))
            {
                // the land in view: the inspector is a bottom sheet, the strip between the controls and its top is free (or,
                // under a top sheet, the strip between it and the HUD's bottom blocks)
                if (IsTopSheet(f, sheet))
                {
                    float below = sheet.Bottom + f.Gap, end = RoomBelow(f, below, 0, f.Canvas.x);
                    return new UiBox(0, below, f.Canvas.x, Mathf.Max(1, end - below));
                }

                float from = InspectorTop(f, above);
                float to = (sheet.IsEmpty ? SheetTopLimit(f, above) : sheet.Y) - f.Gap;
                return new UiBox(0, from, f.Canvas.x, Mathf.Max(1, to - from));
            }

            float top = InspectorTop(f, above), limit = InspectorLimit(f, above);
            float sheetBottom = top + (limit - top) * PortraitShare;
            if (!sheet.IsEmpty) sheetBottom = Mathf.Max(sheetBottom, sheet.Bottom);
            sheetBottom = Mathf.Min(sheetBottom + f.Gap, limit - 1);
            return new UiBox(0, sheetBottom, f.Canvas.x, limit - sheetBottom);
        }

        /// <summary>
        /// Where (canvas units) the N key puts a person's point: the middle of <see cref="PersonRegion"/>, moved toward
        /// the future by <see cref="FutureShift"/> of its half extent. <paramref name="past"/> is the direction of the
        /// past on screen (unit; x right, y down).
        /// </summary>
        public static Vector2 PersonFocus(HudFrame f, UiBox above, UiBox sheet, Vector2 past)
        {
            UiBox r = PersonRegion(f, above, sheet);
            return new Vector2(r.X + r.W * 0.5f * (1 - FutureShift * past.x), r.Y + r.H * 0.5f * (1 - FutureShift * past.y));
        }

        // ------------------------------------------------------------------ the land: the bowl, sheets, the left column

        /// <summary>The bowl's box in canvas units, from its box as fractions of the screen (top-left origin).</summary>
        public static UiBox BowlBox(HudFrame f, Rect fraction) =>
            fraction.width <= 0 || fraction.height <= 0
                ? UiBox.Empty
                : new UiBox(fraction.x * f.Canvas.x, fraction.y * f.Canvas.y, fraction.width * f.Canvas.x, fraction.height * f.Canvas.y);

        /// <summary>The bowl's box on the canvas from its silhouette: its bounds clipped to the canvas (empty when off it).</summary>
        public static UiBox BowlBox(HudFrame f, BowlShape shape)
        {
            if (shape == null || shape.Count < 3) return UiBox.Empty;
            UiBox b = shape.Bounds;
            float x0 = Mathf.Max(0, b.X), y0 = Mathf.Max(0, b.Y), x1 = Mathf.Min(f.Canvas.x, b.Right), y1 = Mathf.Min(f.Canvas.y, b.Bottom);
            return x1 > x0 && y1 > y0 ? new UiBox(x0, y0, x1 - x0, y1 - y0) : UiBox.Empty;
        }

        /// <summary>Whether the inspectors are bottom sheets: a portrait screen with the land in view.</summary>
        public static bool BottomSheets(HudFrame f) => f.Portrait && !f.Bowl.IsEmpty;

        /// <summary>
        /// The bottom of a portrait sheet: the screen's bottom margin (above the safe area). A sheet is the lower part of the
        /// tall frame while the bowl keeps the upper part (7.2): it covers the HUD's preset bar, legend and lifeline readout
        /// while it is up (× or Esc gives them back).
        /// </summary>
        public static float SheetBottom(HudFrame f) => f.Canvas.y - f.Margin - f.SafeBottom;

        /// <summary>
        /// The highest a portrait sheet may reach: <see cref="SheetShare"/> of the height above its bottom, below the title
        /// and the year controls (<paramref name="above"/>), and below the bowl.
        /// </summary>
        public static float SheetTopLimit(HudFrame f, UiBox above)
        {
            float top = Mathf.Max(SheetBottom(f) - SheetShare * f.Canvas.y, TitleBottom(f) + f.Gap);
            if (!above.IsEmpty) top = Mathf.Max(top, above.Bottom + f.Gap);
            if (!f.Bowl.IsEmpty)
            {
                // the bowl's lowest point across the sheet's width (its box's bottom where only the box is known)
                float low = f.Shape != null ? f.Shape.LowestIn(f.Margin, f.Canvas.x - f.Margin) : f.Bowl.Bottom;
                if (!float.IsNegativeInfinity(low)) top = Mathf.Max(top, Mathf.Min(low, f.Bowl.Bottom) + f.Gap);
            }

            return top;
        }

        /// <summary>The width a sheet's content is laid out at for a scale (it spans the screen between the margins).</summary>
        public static float SheetLayoutWidth(HudFrame f, float scale) => Mathf.Max(1, f.Canvas.x - 2 * f.Margin) / Mathf.Max(1e-3f, scale);

        /// <summary>
        /// The scale a sheet's content <paramref name="height"/> tall (at scale 1) is drawn at to fit its room under the
        /// bowl: below <see cref="MinScale"/> down to <see cref="FloorScale"/> rather than reach the bowl (a scaled sheet is
        /// laid out wider by 1 / scale, so it runs shorter still).
        /// </summary>
        public static float SheetScale(HudFrame f, UiBox above, float height)
        {
            float room = SheetBottom(f) - SheetTopLimit(f, above);
            return height <= 0 ? 1 : Mathf.Clamp(room / height, FloorScale, 1);
        }

        /// <summary>
        /// The largest scale a sheet is drawn at whose content, laid out at <see cref="SheetLayoutWidth"/> for that scale
        /// (wider as it shrinks, so its text wraps less), fits the sheet's room under the bowl: <paramref name="heightAt"/>
        /// (layout width) is its height at scale 1; drawn height = height × scale grows with the scale, so a bisection finds
        /// it. <see cref="FloorScale"/> where even that does not fit.
        /// </summary>
        public static float SheetFit(HudFrame f, UiBox above, Func<float, float> heightAt) =>
            FitRoom(f, SheetBottom(f) - SheetTopLimit(f, above), heightAt);

        /// <summary>The largest scale (down to <see cref="FloorScale"/>) at which a sheet laid out for it fits a room's height.</summary>
        static float FitRoom(HudFrame f, float room, Func<float, float> heightAt)
        {
            if (heightAt(SheetLayoutWidth(f, 1)) <= room) return 1;
            float lo = FloorScale, hi = 1;
            if (heightAt(SheetLayoutWidth(f, lo)) * lo > room) return lo;
            for (int i = 0; i < 8; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (heightAt(SheetLayoutWidth(f, mid)) * mid <= room) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// A portrait sheet for content whose height at a layout width is <paramref name="heightAt"/> (scale 1): the bottom
        /// sheet under the bowl (<see cref="SheetFit"/>, <see cref="SheetBox"/>); where that is drawn below
        /// <see cref="MinScale"/> (the bowl reaches low), a top sheet under the title and the year controls, above the bowl's
        /// highest point and at most <see cref="SheetShare"/> of the height, when it is drawn larger there.
        /// </summary>
        public static Placement SheetPlace(HudFrame f, UiBox above, Func<float, float> heightAt)
        {
            float s = SheetFit(f, above, heightAt), lw = SheetLayoutWidth(f, s);
            Placement bottom = new Placement { Box = SheetBox(f, lw, heightAt(lw), s), Scale = s, LayoutWidth = lw, Compact = true };
            if (s >= MinScale || f.Bowl.IsEmpty) return bottom;

            float top = TitleBottom(f) + f.Gap;
            if (!above.IsEmpty) top = Mathf.Max(top, above.Bottom + f.Gap);
            float high = f.Shape != null ? f.Shape.HighestIn(f.Margin, f.Canvas.x - f.Margin) : f.Bowl.Y;
            if (float.IsPositiveInfinity(high)) high = f.Bowl.Y;
            float room = Mathf.Min(high - f.Gap, top + SheetShare * f.Canvas.y) - top;
            if (room <= 0) return bottom;
            float t = FitRoom(f, room, heightAt);
            if (t <= s || heightAt(SheetLayoutWidth(f, t)) * t > room) return bottom;
            lw = SheetLayoutWidth(f, t);
            return new Placement { Box = new UiBox(f.Margin, top, lw * t, heightAt(lw) * t), Scale = t, LayoutWidth = lw, Compact = true };
        }

        /// <summary>Whether a sheet stands at the top of the screen (<see cref="SheetPlace"/>'s fallback), not at the bottom.</summary>
        public static bool IsTopSheet(HudFrame f, UiBox sheet) => !sheet.IsEmpty && sheet.Y + 0.5f * sheet.H < 0.5f * f.Canvas.y;

        /// <summary>A bottom sheet: content laid out <paramref name="width"/> × <paramref name="height"/> drawn at <paramref name="scale"/>, its bottom on <see cref="SheetBottom"/>.</summary>
        public static UiBox SheetBox(HudFrame f, float width, float height, float scale)
        {
            float bottom = SheetBottom(f);
            return new UiBox(f.Margin, bottom - height * scale, width * scale, height * scale);
        }

        /// <summary>
        /// The landscape inspector, never over the bowl's silhouette (7.2), for content that can be laid out at any width,
        /// full or compact (the portrait sheets' two-column layout): <paramref name="heightAt"/>(layout width, compact) is
        /// its height at scale 1. The first of these that keeps a gap from the bowl (and clear of the land legend and the
        /// social panel):
        /// <list type="number">
        /// <item>the full content in the right column under the year controls (<paramref name="width"/> wide), at the
        /// largest scale its room allows (<see cref="InspectorScale"/>) down to <see cref="MinScale"/>;</item>
        /// <item>the same in the left column under the title (above the social panel and the land legend);</item>
        /// <item>the compact content in the right column, then in the left one, likewise;</item>
        /// <item>the compact content as a band under the year controls, wider than the column and laid out wider by
        /// 1 / scale (the bowl's top leaves a band above it: a close, low view);</item>
        /// <item>the compact content below <see cref="MinScale"/>, down to <see cref="FloorScale"/>: in the columns, then as a band.</item>
        /// </list>
        /// Where nothing keeps clear of the bowl it stays in the right column at <see cref="MinScale"/>, full (and
        /// <see cref="Check"/> reports it).
        /// </summary>
        public static Placement ColumnPlace(HudFrame f, UiBox above, float width, Func<float, bool, float> heightAt)
        {
            float top = InspectorTop(f, above), right = f.Canvas.x - f.Margin;
            float full = Mathf.Max(1, heightAt(width, false));
            float hi = InspectorScale(f, above, full);
            Placement fallback = new Placement
            {
                Box = Anchored(right, top, true, false, width * hi, full * hi), Scale = hi, LayoutWidth = width, Compact = false
            };
            if (f.Bowl.IsEmpty) return fallback;

            if (InColumns(f, above, width, full, false, MinScale, out Placement p)) return p;
            float compact = Mathf.Max(1, heightAt(width, true));
            if (compact < full && InColumns(f, above, width, compact, true, MinScale, out p)) return p;
            if (InBand(f, above, heightAt, MinScale, out p)) return p;
            if (InColumns(f, above, width, Mathf.Min(compact, full), compact < full, FloorScale, out p)) return p;
            if (InBand(f, above, heightAt, FloorScale, out p)) return p;
            fallback.Scale = MinScale;
            fallback.Box = Anchored(right, top, true, false, width * MinScale, full * MinScale);
            return fallback;
        }

        /// <summary>Where and how an inspector is drawn (<see cref="ColumnPlace"/>): its box, scale, the width its content is laid out at, compact or full.</summary>
        public struct Placement
        {
            public UiBox Box;
            public float Scale, LayoutWidth;
            public bool Compact;
        }

        /// <summary>
        /// Content <paramref name="width"/> × <paramref name="height"/> in the right column, else the left, at the largest
        /// scale from the room's down to <paramref name="lo"/> (from <see cref="MinScale"/> down to <paramref name="lo"/> when
        /// <paramref name="lo"/> is below it) that keeps clear.
        /// </summary>
        static bool InColumns(HudFrame f, UiBox above, float width, float height, bool compact, float lo, out Placement p)
        {
            float top = InspectorTop(f, above), right = f.Canvas.x - f.Margin;
            float hi = lo < MinScale ? MinScale : InspectorScale(f, above, height);
            float s = FitScale(f, right, top, true, false, width, height, lo, hi);
            if (s > 0)
            {
                p = new Placement { Box = Anchored(right, top, true, false, width * s, height * s), Scale = s, LayoutWidth = width, Compact = compact };
                return true;
            }

            float leftTop = LeftTop(f, width);
            float leftHi = lo < MinScale ? MinScale : Mathf.Clamp((LeftLimit(f, width) - leftTop) / height, MinScale, 1);
            s = FitScale(f, f.Margin, leftTop, false, false, width, height, lo, leftHi);
            if (s > 0)
            {
                p = new Placement { Box = Anchored(f.Margin, leftTop, false, false, width * s, height * s), Scale = s, LayoutWidth = width, Compact = compact };
                return true;
            }

            p = default;
            return false;
        }

        /// <summary>The band's widths tried, as shares of the canvas between the margins (narrowest first: it leaves the most of the view).</summary>
        static readonly float[] BandShares = { 0.45f, 0.6f, 0.8f };

        /// <summary>The scales tried for the band, largest first (each ≥ the pass's lower bound).</summary>
        static readonly float[] BandScales = { 1f, 0.85f, 0.7f, MinScale, 0.52f, FloorScale };

        /// <summary>
        /// The compact content as a band under the year controls at the right margin: the largest scale (not below
        /// <paramref name="lo"/>, nor below <see cref="MinScale"/> unless <paramref name="lo"/> is) and, at it, the narrowest
        /// width that keeps clear of the bowl and within the room above the HUD's bottom blocks.
        /// </summary>
        static bool InBand(HudFrame f, UiBox above, Func<float, bool, float> heightAt, float lo, out Placement p)
        {
            float top = InspectorTop(f, above), right = f.Canvas.x - f.Margin, span = f.Canvas.x - 2 * f.Margin;
            foreach (float s in BandScales)
            {
                if (s < lo - 1e-4f || lo < MinScale && s > MinScale + 1e-4f) continue;
                foreach (float share in BandShares)
                {
                    float w = span * share, layout = w / s, h = Mathf.Max(1, heightAt(layout, true)) * s;
                    UiBox box = Anchored(right, top, true, false, w, h);
                    if (box.Bottom > RoomBelow(f, top, box.X, box.Right) || !Clear(f, box)) continue;
                    p = new Placement { Box = box, Scale = s, LayoutWidth = layout, Compact = true };
                    return true;
                }
            }

            p = default;
            return false;
        }

        /// <summary>
        /// True when a box keeps a gap from the bowl and clear of the land legend, the social panel and a portrait sheet that
        /// is up (the panels laid out before it).
        /// </summary>
        public static bool Clear(HudFrame f, UiBox box) =>
            !OnBowl(f, box, f.Gap) && !box.Overlaps(f.LandLegend) && !box.Overlaps(f.SocialPanel) && !box.Overlaps(f.Sheet);

        /// <summary>
        /// The y where the room of a landscape inspector at <paramref name="box"/> ends: its column's first block beneath
        /// it (the right column's, or the left column's when it docked there).
        /// </summary>
        public static float ColumnLimit(HudFrame f, UiBox above, UiBox box) =>
            !f.Portrait && !box.IsEmpty && box.X + 0.5f * box.W < 0.5f * f.Canvas.x ? LeftLimit(f, box.W) : InspectorLimit(f, above);

        // ------------------------------------------------------------------ fitting beside the bowl

        /// <summary>
        /// True when a box comes within <paramref name="margin"/> of the bowl: its silhouette (<see cref="HudFrame.Shape"/>), or
        /// its box where only that is known.
        /// </summary>
        public static bool OnBowl(HudFrame f, UiBox box, float margin)
        {
            if (box.IsEmpty || f.Bowl.IsEmpty) return false;
            UiBox grown = new UiBox(box.X - margin, box.Y - margin, box.W + 2 * margin, box.H + 2 * margin);
            return f.Shape != null ? f.Shape.Overlaps(grown) : grown.Overlaps(f.Bowl);
        }

        /// <summary>A box w × h with one corner at (x, y): its right edge there when <paramref name="right"/>, its bottom when <paramref name="bottom"/>.</summary>
        public static UiBox Anchored(float x, float y, bool right, bool bottom, float w, float h) =>
            new UiBox(right ? x - w : x, bottom ? y - h : y, w, h);

        /// <summary>
        /// The largest scale in [<paramref name="lo"/>, <paramref name="hi"/>] at which content w × h, anchored by a corner at
        /// (x, y), stays <see cref="Clear"/> (the boxes grow from the corner, so they nest: a bisection finds it); -1 when
        /// even <paramref name="lo"/> does not.
        /// </summary>
        public static float FitScale(HudFrame f, float x, float y, bool right, bool bottom, float w, float h, float lo, float hi)
        {
            hi = Mathf.Max(lo, hi);
            if (Clear(f, Anchored(x, y, right, bottom, w * hi, h * hi))) return hi;
            if (!Clear(f, Anchored(x, y, right, bottom, w * lo, h * lo))) return -1;
            for (int i = 0; i < 14; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (!Clear(f, Anchored(x, y, right, bottom, w * mid, h * mid))) hi = mid;
                else lo = mid;
            }

            return lo;
        }

        /// <summary>Whether the land legend stands at the top of the left column (the bowl took its place at the bottom).</summary>
        public static bool LegendOnTop(HudFrame f) => !f.LandLegend.IsEmpty && f.LandLegend.Y + 0.5f * f.LandLegend.H < 0.5f * f.Canvas.y;

        /// <summary>The top of the left column (landscape): under the title, and under the land legend while it stands at the top.</summary>
        static float LeftTop(HudFrame f, float width)
        {
            float top = TitleBottom(f) + f.Gap;
            if (LegendOnTop(f) && f.LandLegend.OverlapsX(f.Margin, f.Margin + width)) top = Mathf.Max(top, f.LandLegend.Bottom + f.Gap);
            return top;
        }

        /// <summary>The y where the left column's room ends for a panel at its top: the social panel, the land legend at the bottom, the HUD's blocks.</summary>
        static float LeftLimit(HudFrame f, float width)
        {
            float limit = LeftBottom(f, width);
            Limit(f.SocialPanel, 0, f.Margin, f.Margin + width, f.Gap, ref limit);
            if (!LegendOnTop(f)) Limit(f.LandLegend, 0, f.Margin, f.Margin + width, f.Gap, ref limit);
            return limit;
        }

        /// <summary>The bottom of the left column (landscape): above the HUD's legend and lifeline readout, or the margin.</summary>
        static float LeftBottom(HudFrame f, float width)
        {
            float bottom = f.Canvas.y - f.Margin - f.SafeBottom;
            Limit(f.Legend, 0, f.Margin, f.Margin + width, f.Gap, ref bottom);
            Limit(f.Readout, 0, f.Margin, f.Margin + width, f.Gap, ref bottom);
            Limit(f.PresetBar, 0, f.Margin, f.Margin + width, f.Gap, ref bottom);
            return bottom;
        }

        /// <summary>
        /// The land legend for its content variants (<paramref name="sizes"/>, the first <paramref name="count"/>: the full
        /// legend, then shorter ones: its notes collapsed, then compact), never over the bowl's silhouette (nor over the
        /// HUD's blocks): at the bottom-left above the HUD's legend and lifeline readout (above a portrait bottom sheet while
        /// one is up, <see cref="HudFrame.Sheet"/>), or at the top-left under the title
        /// (and under the year controls, <paramref name="above"/>, where they share its column). Tried in passes: each variant (longest first) at the bottom, then at
        /// the top, at <paramref name="comfort"/> scale or more; then the same down to <see cref="MinScale"/>; then the last
        /// variant down to <see cref="FloorScale"/>. Empty (the legend steps aside) only where none of that keeps clear.
        /// </summary>
        public static UiBox LandLegendPlace(HudFrame f, UiBox above, Vector2[] sizes, int count, float comfort, out int variant, out float scale)
        {
            float topY = TitleBottom(f) + f.Gap;
            for (int pass = 0; pass < 3; pass++)
            {
                float lo = pass == 0 ? Mathf.Max(MinScale, comfort) : pass == 1 ? MinScale : FloorScale;
                if (pass == 1 && lo >= comfort - 1e-4f) continue;
                for (int v = pass == 2 ? count - 1 : 0; v < count; v++)
                {
                    Vector2 size = sizes[v];
                    float bottom = LeftBottom(f, size.x);
                    // above a bottom sheet that is up (portrait): between it and the bowl
                    if (!f.Sheet.IsEmpty && f.Sheet.OverlapsX(f.Margin, f.Margin + size.x) && !IsTopSheet(f, f.Sheet)) bottom = Mathf.Min(bottom, f.Sheet.Y - f.Gap);
                    float hi = Mathf.Clamp((bottom - topY) / Mathf.Max(1, size.y), lo, 1);
                    if (pass == 2) hi = Mathf.Min(hi, MinScale);
                    scale = FitScale(f, f.Margin, bottom, false, true, size.x, size.y, lo, hi);
                    if (scale > 0)
                    {
                        variant = v;
                        return Anchored(f.Margin, bottom, false, true, size.x * scale, size.y * scale);
                    }

                    // the top: under the title (and under the year controls where they share its column: portrait)
                    float y = above.OverlapsX(f.Margin, f.Margin + size.x) ? Mathf.Max(topY, above.Bottom + f.Gap) : topY;
                    hi = Mathf.Clamp((bottom - y) / Mathf.Max(1, size.y), lo, 1);
                    if (pass == 2) hi = Mathf.Min(hi, MinScale);
                    scale = FitScale(f, f.Margin, y, false, false, size.x, size.y, lo, hi);
                    if (scale > 0)
                    {
                        variant = v;
                        return Anchored(f.Margin, y, false, false, size.x * scale, size.y * scale);
                    }
                }
            }

            variant = Mathf.Max(0, count - 1);
            scale = 1;
            return UiBox.Empty;
        }

        /// <summary>The legend's comfortable scale: below it a shorter variant at this scale or more reads better.</summary>
        public const float LegendComfort = 0.85f;

        /// <summary>
        /// The social panel in landscape: bottom-left above the land legend (or the HUD's legend when the legend stands at
        /// the top), content <paramref name="width"/> × <paramref name="height"/> scaled to fit below the title (and below the
        /// legend at the top) and to keep a gap from the bowl: not below <see cref="MinScale"/>, unless only a smaller
        /// panel (down to <see cref="FloorScale"/>) keeps clear of it.
        /// </summary>
        public static UiBox SocialBox(HudFrame f, float width, float height, out float scale)
        {
            float bottom = LeftBottom(f, width);
            if (!LegendOnTop(f) && !f.LandLegend.IsEmpty && f.LandLegend.OverlapsX(f.Margin, f.Margin + width)) bottom = Mathf.Min(bottom, f.LandLegend.Y - f.Gap);
            float top = LeftTop(f, width);
            float hi = height <= 0 ? 1 : Mathf.Clamp((bottom - top) / height, MinScale, 1);
            scale = FitScale(f, f.Margin, bottom, false, true, width, height, MinScale, hi);
            if (scale <= 0) scale = FitScale(f, f.Margin, bottom, false, true, width, height, FloorScale, MinScale);
            if (scale <= 0) scale = hi;
            return Anchored(f.Margin, bottom, false, true, width * scale, height * scale);
        }

        /// <summary>
        /// The y (canvas units from the top) where the room below <paramref name="top"/> in the column [x0, x1] ends: the
        /// first of the HUD's bottom blocks (preset bar, legend, readout) that shares the column, a gap above it; else the
        /// bottom margin (above the safe area).
        /// </summary>
        public static float RoomBelow(HudFrame f, float top, float x0, float x1)
        {
            float limit = f.Canvas.y - f.Margin - f.SafeBottom;
            Limit(f.PresetBar, top, x0, x1, f.Gap, ref limit);
            Limit(f.Legend, top, x0, x1, f.Gap, ref limit);
            Limit(f.Readout, top, x0, x1, f.Gap, ref limit);
            return limit;
        }

        static void Limit(UiBox b, float top, float x0, float x1, float gap, ref float limit)
        {
            if (b.OverlapsX(x0, x1) && b.Bottom > top) limit = Mathf.Min(limit, b.Y - gap);
        }

        /// <summary>
        /// The runtime self-check: adds a line to <paramref name="problems"/> for every HUD block, panel or the bowl
        /// <paramref name="box"/> overlaps and for leaving the canvas (half a unit of tolerance); a portrait
        /// <paramref name="sheet"/> may cover the HUD's preset bar, legend, lifeline readout and the land legend. A module clears its own
        /// box from the frame before checking itself. Returns the number of problems added.
        /// </summary>
        public static int Check(HudFrame f, string name, UiBox box, List<string> problems, bool sheet = false)
        {
            // a portrait sheet stands over the preset bar and covers the HUD's legend and lifeline readout while it is up
            if (sheet)
            {
                f.Sheet = UiBox.Empty;
                f.Legend = UiBox.Empty;
                f.Readout = UiBox.Empty;
                f.PresetBar = UiBox.Empty;
                f.LandLegend = UiBox.Empty;
            }

            if (box.IsEmpty) return 0;
            int before = problems.Count;
            if (box.X < -0.5f || box.Y < -0.5f || box.Right > f.Canvas.x + 0.5f || box.Bottom > f.Canvas.y + 0.5f)
            {
                problems.Add(name + " " + box + " leaves the canvas " + f.Canvas.x.ToString("0", CultureInfo.InvariantCulture) +
                             " x " + f.Canvas.y.ToString("0", CultureInfo.InvariantCulture));
            }

            Against(name, box, "the HUD title", f.Title, problems);
            Against(name, box, "the tour and help buttons", f.TopButtons, problems);
            Against(name, box, "the F3 stats", f.DevStats, problems);
            Against(name, box, "the legend", f.Legend, problems);
            Against(name, box, "the lifeline readout", f.Readout, problems);
            Against(name, box, "the preset bar", f.PresetBar, problems);
            Against(name, box, "the social panel", f.SocialPanel, problems);
            Against(name, box, "the land legend", f.LandLegend, problems);
            Against(name, box, "a portrait sheet", f.Sheet, problems);
            if (OnBowl(f, box, 0)) problems.Add(name + " " + box + " overlaps the bowl " + (f.Shape != null ? f.Shape.Bounds : f.Bowl));
            return problems.Count - before;
        }

        static void Against(string name, UiBox box, string what, UiBox other, List<string> problems)
        {
            if (box.Overlaps(other)) problems.Add(name + " " + box + " overlaps " + what + " " + other);
        }
    }
}
