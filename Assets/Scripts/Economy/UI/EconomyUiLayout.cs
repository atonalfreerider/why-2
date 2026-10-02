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
        /// The bowl's box in the view's pose (the preset's destination, not the camera mid-flight, so panels do not move
        /// during a flight): the panels never cover it (7.2). Empty while the land is not in view.
        /// </summary>
        public UiBox Bowl;

        /// <summary>True when nothing moved by half a canvas unit or more (no new layout needed).</summary>
        public bool Near(HudFrame o) =>
            Portrait == o.Portrait && Mathf.Abs(Canvas.x - o.Canvas.x) < 0.5f && Mathf.Abs(Canvas.y - o.Canvas.y) < 0.5f &&
            Mathf.Abs(SafeTop - o.SafeTop) < 0.5f && Mathf.Abs(SafeBottom - o.SafeBottom) < 0.5f &&
            Title.Near(o.Title) && TopButtons.Near(o.TopButtons) && DevStats.Near(o.DevStats) && Legend.Near(o.Legend) &&
            Readout.Near(o.Readout) && PresetBar.Near(o.PresetBar) && SocialPanel.Near(o.SocialPanel) &&
            LandLegend.Near(o.LandLegend) && Bowl.Near(o.Bowl);
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
    /// when the bowl reaches into the column. On a portrait screen with the land in view they are bottom sheets
    /// (<see cref="SheetBox"/>: the lower part of the frame, at most <see cref="SheetShare"/> of the height, never over the
    /// bowl; it covers the preset bar and the HUD's legend while it is up);
    /// on the road they are a top sheet under the title taking at most <see cref="PortraitShare"/> of the room, so a strip
    /// of the graph stays visible beneath it (<see cref="PersonRegion"/>: where the N key frames a person);</item>
    /// <item>the social panel stands at the bottom-left above the land legend (landscape) or is a bottom sheet (portrait);
    /// the land legend stands at the bottom-left above the HUD's legend and lifeline readout.</item>
    /// </list>
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
            return box.Overlaps(f.Bowl) ? UiBox.Empty : box;
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
                float right = f.Canvas.x - f.Margin - LandscapeInspectorWidth - f.Gap;
                return new UiBox(0, 0, Mathf.Max(1, right), f.Canvas.y);
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
            if (!f.Bowl.IsEmpty) top = Mathf.Max(top, f.Bowl.Bottom + f.Gap);
            return top;
        }

        /// <summary>The width a sheet's content is laid out at for a scale (it spans the screen between the margins).</summary>
        public static float SheetLayoutWidth(HudFrame f, float scale) => Mathf.Max(1, f.Canvas.x - 2 * f.Margin) / Mathf.Max(1e-3f, scale);

        /// <summary>The scale a sheet's content <paramref name="height"/> tall (at scale 1) is drawn at to fit its room (not below <see cref="MinScale"/>).</summary>
        public static float SheetScale(HudFrame f, UiBox above, float height)
        {
            float room = SheetBottom(f) - SheetTopLimit(f, above);
            return height <= 0 ? 1 : Mathf.Clamp(room / height, MinScale, 1);
        }

        /// <summary>A bottom sheet: content laid out <paramref name="width"/> × <paramref name="height"/> drawn at <paramref name="scale"/>, its bottom on <see cref="SheetBottom"/>.</summary>
        public static UiBox SheetBox(HudFrame f, float width, float height, float scale)
        {
            float bottom = SheetBottom(f);
            return new UiBox(f.Margin, bottom - height * scale, width * scale, height * scale);
        }

        /// <summary>
        /// The landscape inspector's scale for content <paramref name="height"/> tall: <see cref="InspectorScale"/>, and
        /// narrower when the bowl reaches into the right column (its left edge then stays a gap right of the bowl).
        /// </summary>
        public static float ColumnScale(HudFrame f, UiBox above, float height)
        {
            float scale = InspectorScale(f, above, height);
            if (f.Portrait || f.Bowl.IsEmpty) return scale;
            float top = InspectorTop(f, above), bottom = top + height * scale;
            if (f.Bowl.Y >= bottom || f.Bowl.Bottom <= top) return scale;
            float room = f.Canvas.x - f.Margin - (f.Bowl.Right + f.Gap);
            return Mathf.Clamp(Mathf.Min(scale, room / LandscapeInspectorWidth), MinScale, 1);
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
        /// The land legend: bottom-left, above the HUD's legend and lifeline readout (both orientations), content
        /// <paramref name="size"/> drawn at <paramref name="scale"/>. When the bowl reaches down into its column the legend
        /// shrinks into the room under the bowl (not below <see cref="MinScale"/>); with less room than that it steps aside
        /// (empty box): the bowl is never covered.
        /// </summary>
        public static UiBox LandLegendBox(HudFrame f, Vector2 size, out float scale)
        {
            float bottom = LeftBottom(f, size.x);
            scale = 1;
            if (!f.Bowl.IsEmpty && f.Bowl.OverlapsX(f.Margin, f.Margin + size.x) && f.Bowl.Bottom + f.Gap > bottom - size.y)
            {
                scale = (bottom - (f.Bowl.Bottom + f.Gap)) / Mathf.Max(1, size.y);
                if (scale < MinScale) return UiBox.Empty;
            }

            return new UiBox(f.Margin, bottom - size.y * scale, size.x * scale, size.y * scale);
        }

        /// <summary>
        /// The social panel in landscape: bottom-left above the land legend (or the HUD's legend), content
        /// <paramref name="width"/> × <paramref name="height"/> scaled to fit below the title (not below <see cref="MinScale"/>).
        /// </summary>
        public static UiBox SocialBox(HudFrame f, float width, float height, out float scale)
        {
            float bottom = LeftBottom(f, width);
            if (!f.LandLegend.IsEmpty && f.LandLegend.OverlapsX(f.Margin, f.Margin + width)) bottom = Mathf.Min(bottom, f.LandLegend.Y - f.Gap);
            float top = TitleBottom(f) + f.Gap;
            scale = height <= 0 ? 1 : Mathf.Clamp((bottom - top) / height, MinScale, 1);
            return new UiBox(f.Margin, bottom - height * scale, width * scale, height * scale);
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
            Against(name, box, "the bowl", f.Bowl, problems);
            return problems.Count - before;
        }

        static void Against(string name, UiBox box, string what, UiBox other, List<string> problems)
        {
            if (box.Overlaps(other)) problems.Add(name + " " + box + " overlaps " + what + " " + other);
        }
    }
}
