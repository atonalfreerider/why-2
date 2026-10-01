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
    /// The screen as the economy's own UI sees it: the canvas, its orientation and safe insets, the HUD's spacing, and the
    /// boxes of everything the economy's panels must keep clear of (the HUD's title, its tour and help buttons, the F3
    /// stats, the legend and lifeline readout, the preset bar, the games panel). Measured from the live UI each frame
    /// (see <see cref="HudBlocks"/>); hidden blocks are empty.
    /// </summary>
    public struct HudFrame
    {
        public Vector2 Canvas;
        public bool Portrait;
        public float SafeTop, SafeBottom;

        /// <summary>Distance of corner blocks from the screen edge and between neighbors (HudKit's, by orientation).</summary>
        public float Margin, Gap;

        public UiBox Title, TopButtons, DevStats, Legend, Readout, PresetBar, GamesPanel;

        /// <summary>True when nothing moved by half a canvas unit or more (no new layout needed).</summary>
        public bool Near(HudFrame o) =>
            Portrait == o.Portrait && Mathf.Abs(Canvas.x - o.Canvas.x) < 0.5f && Mathf.Abs(Canvas.y - o.Canvas.y) < 0.5f &&
            Mathf.Abs(SafeTop - o.SafeTop) < 0.5f && Mathf.Abs(SafeBottom - o.SafeBottom) < 0.5f &&
            Title.Near(o.Title) && TopButtons.Near(o.TopButtons) && DevStats.Near(o.DevStats) && Legend.Near(o.Legend) &&
            Readout.Near(o.Readout) && PresetBar.Near(o.PresetBar) && GamesPanel.Near(o.GamesPanel);
    }

    /// <summary>
    /// Where the economy's controls and the person inspector go, as pure functions of a <see cref="HudFrame"/> (tested
    /// headless in the harness; the modules only apply the boxes to their RectTransforms):
    /// <list type="bullet">
    /// <item>the year chip stands at the right edge under the HUD's tour and help buttons (and under the F3 stats while they
    /// show), in portrait in the column beside the title;</item>
    /// <item>the year's readout runs under the chip at the right edge; in portrait, under the title block (where the games
    /// panel's sheet docks in its views, so it steps aside there);</item>
    /// <item>the inspector hangs below them at the right edge (landscape), or spans the width under them (portrait, a top
    /// sheet), down to the first HUD block beneath it in its column (the preset bar, the legend, the readout) and scaled
    /// down when its content is taller than that room.</item>
    /// </list>
    /// <see cref="Check"/> lists any overlap with the HUD's blocks: the modules run it after every layout (a runtime
    /// self-check that logs once per layout).
    /// </summary>
    public static class EconomyUiLayout
    {
        /// <summary>Height of the HUD's tour and help buttons (Hud.TopButtonHeight), when they cannot be measured.</summary>
        public const float TopButtonHeight = 30f;

        /// <summary>Height of the HUD's title block (Hud.TitleHeight), when it cannot be measured.</summary>
        public const float TitleHeight = 90f;

        /// <summary>Gap between the year chip and the readout beneath it (they read as one block).</summary>
        public const float ChipGap = 6f;

        /// <summary>Smallest scale the inspector is drawn at to fit (below it the content would be unreadable; it then
        /// runs past its room instead, which <see cref="Check"/> reports).</summary>
        public const float MinScale = 0.6f;

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
        /// The year's readout: right-aligned under the chip; in portrait under the title block (beside the title there is
        /// only room for the chip). Empty in portrait while the games panel's sheet holds that place.
        /// </summary>
        public static UiBox Readout(HudFrame f, UiBox chip, Vector2 size)
        {
            if (!f.Portrait) return new UiBox(f.Canvas.x - f.Margin - size.x, chip.Bottom + ChipGap, size.x, size.y);
            if (!f.GamesPanel.IsEmpty) return UiBox.Empty;
            float top = Mathf.Max(TitleBottom(f), chip.Bottom) + f.Gap;
            return new UiBox(f.Canvas.x - f.Margin - size.x, top, size.x, size.y);
        }

        /// <summary>Bottom of the HUD's title block (measured, or its authored height).</summary>
        public static float TitleBottom(HudFrame f) =>
            f.Title.IsEmpty ? f.Margin + f.SafeTop + TitleHeight : f.Title.Bottom;

        /// <summary>
        /// The inspector below <paramref name="above"/> (the readout, or the chip when the readout is hidden): at the right
        /// edge <paramref name="width"/> wide in landscape, the full width in portrait (under the title too). Its content is
        /// <paramref name="height"/> tall at scale 1; <paramref name="scale"/> shrinks it into the room down to the first HUD
        /// block beneath it in its column. The box is the panel as drawn (scaled).
        /// </summary>
        public static UiBox Inspector(HudFrame f, UiBox above, float width, float height, out float scale)
        {
            float top = (above.IsEmpty ? ControlsTop(f) : above.Bottom) + f.Gap;
            if (f.Portrait) top = Mathf.Max(top, TitleBottom(f) + f.Gap);
            float w = f.Portrait ? Mathf.Max(1, f.Canvas.x - 2 * f.Margin) : width;
            float x0 = f.Canvas.x - f.Margin - w;
            float limit = RoomBelow(f, top, x0, x0 + w);
            scale = Mathf.Clamp(height > 0 ? (limit - top) / height : 1, MinScale, 1);
            return new UiBox(f.Canvas.x - f.Margin - w * scale, top, w * scale, height * scale);
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
        /// The runtime self-check: adds a line to <paramref name="problems"/> for every HUD block <paramref name="box"/>
        /// overlaps and for leaving the canvas (half a unit of tolerance). Returns the number of problems added.
        /// </summary>
        public static int Check(HudFrame f, string name, UiBox box, List<string> problems)
        {
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
            Against(name, box, "the games panel", f.GamesPanel, problems);
            return problems.Count - before;
        }

        static void Against(string name, UiBox box, string what, UiBox other, List<string> problems)
        {
            if (box.Overlaps(other)) problems.Add(name + " " + box + " overlaps " + what + " " + other);
        }
    }
}
