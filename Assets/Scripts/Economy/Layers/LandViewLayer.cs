using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Why.Economy.Land;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The land's view (SPEC 7.2, 8.3): on focus it applies the preset's <see cref="ViewSpec"/> (the transition's target,
    /// a temporary year built blocking, the held round, the betrayal season, whether the road's labels step aside, the
    /// emphasis targets); every frame it runs the transition's clock, eases the emphasis, plays the season, publishes
    /// finished land builds (<see cref="LandService.Tick"/>) and follows the viewer's year and season settings. It owns
    /// the harness's view overrides (unset in Unity): WHY_ECON_MORPH (seconds: the transition frozen there while it
    /// unfolds), WHY_ECON_ROUND (the held round), WHY_ECON_SELECT (player:&lt;key&gt;, tower:&lt;ticker&gt;, pair:&lt;k&gt;).
    /// It also dims the road while the land is open (1.1, 7.2): the shared time axis and lifelines take
    /// <see cref="LandView.RoadAlpha"/> through their <c>SceneAlpha</c> hooks (reset to 1 when the scene unloads); the
    /// industry wall reads it itself. Order 44: before every land layer, so they read this frame's <see cref="LandView"/>.
    /// Draws nothing.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class LandViewLayer : GraphLayer
    {
        public override int Order => 44;

        /// <summary>Frames longer than this (a hitch) advance the clocks by this much.</summary>
        const float MaxStep = 0.1f;

        LabelSystem labels;
        float frozenMorph = -1;
        int heldRound = -1;
        string select;

        /// <summary>The viewer's year while a preset shows a temporary one (-1: none stored).</summary>
        int storedYear = -1;

        int seenState = -1, requestedYear = -1;
        SocialSettings requestedSocial;

        /// <summary>
        /// What the preset catalog was last refreshed for (<see cref="EconomyPresets"/>): the cut's year (the section's
        /// title and pose) and the land on screen (generated subtitles). When either changes the catalog is refreshed in place.
        /// </summary>
        int catalogYear = -1, catalogShown = -1;

        /// <summary>The section view follows a new year over this time (s).</summary>
        const float SectionRefocusSeconds = 0.4f;

        GraphRoot root;

        public override void Prepare(GraphContext ctx)
        {
            frozenMorph = EnvFloat("WHY_ECON_MORPH", -1);
            heldRound = (int)EnvFloat("WHY_ECON_ROUND", -1);
            select = Environment.GetEnvironmentVariable("WHY_ECON_SELECT")?.Trim();
        }

        static float EnvFloat(string name, float fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrWhiteSpace(v) && float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                ? f
                : fallback;
        }

        public override void Upload(GraphContext ctx)
        {
            labels = ctx.Labels;
            LandView.ResetState();
            Axis.TimeAxisLayer.SceneAlpha = Humans.Smv.SmvLayer.SceneAlpha = 1f;
            storedYear = -1;
            requestedYear = LandService.Current?.Year ?? EconomyState.Year;
            requestedSocial = LandService.Current?.Society?.Settings ?? EconomyState.Social;

            // the catalog was first built before the land existed: refresh it (in place) with the land's numbers
            EconomyPresets.Refresh(ViewPresets.All);
            catalogYear = EconomyState.CutYear;
            catalogShown = LandService.ShownYear;
            root = GraphRoot.Instance;
            if (root != null) root.OrientationChanged += OnOrientation;
        }

        /// <summary>
        /// The screen turned: the catalog frames its views for the new shape (portrait aims lower), so it is refreshed in
        /// place and the camera, which the root has just sent to the old pose, flies to the new one.
        /// </summary>
        void OnOrientation()
        {
            EconomyPresets.Refresh(ViewPresets.All);
            ViewPreset now = root != null ? root.CurrentPreset : null;
            if (now == null) return;
            Freshen(now);
            if (now.FixedTarget.HasValue) root.Rig.FlyTo(now.Pose(), GraphRoot.ReframeSeconds);
        }

        /// <summary>
        /// Follows the cut's year and the land on screen: a new land refreshes the whole catalog in place (the generated
        /// subtitles); a new cut year (every step of a scrubber drag) only the section preset, and the section view
        /// follows the cut to its new year over <paramref name="seconds"/>.
        /// </summary>
        void FollowCatalog(float seconds)
        {
            int year = EconomyState.CutYear, shown = LandService.ShownYear;
            if (year == catalogYear && shown == catalogShown) return;
            bool moved = year != catalogYear;
            ViewPreset section = CatalogPreset("section");
            if (shown != catalogShown) EconomyPresets.Refresh(ViewPresets.All);
            else EconomyPresets.RefreshSection(section);
            catalogYear = year;
            catalogShown = shown;
            if (!moved || root == null || root.Rig == null || LandView.PresetId != "section") return;
            ViewPreset now = root.CurrentPreset ?? section;
            Freshen(now);
            if (now.FixedTarget.HasValue) root.Rig.FlyTo(now.Pose(), seconds);
        }

        /// <summary>The catalog's preset with this id, or null (<see cref="ViewPresets.Get"/> falls back to the first).</summary>
        static ViewPreset CatalogPreset(string id)
        {
            if (id == null) return null;
            IReadOnlyList<ViewPreset> all = ViewPresets.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == id) return all[i];
            }

            return null;
        }

        /// <summary>
        /// A preset object that is not the catalog's own (a copy the tour fitted to an anchor) takes the catalog's
        /// current pose and text. True when its pose changed.
        /// </summary>
        static bool Freshen(ViewPreset preset) => preset != null && EconomyPresets.CopyPose(CatalogPreset(preset.Id), preset);

        /// <summary>The root's default flight to a focused preset (s), for the backstop below.</summary>
        const float FocusSeconds = 2.2f;

        public override void OnFocus(ViewPreset preset)
        {
            // backstop: a preset object held since an older catalog (a copy) flies to the current pose instead
            if (Freshen(preset) && preset.FixedTarget.HasValue && root != null && root.Rig != null)
            {
                root.Rig.FlyTo(preset.Pose(), FocusSeconds);
            }

            ViewSpec spec = EconomyViews.Get(preset?.Id);
            if (spec.Year > 0)
            {
                if (storedYear < 0) storedYear = EconomyState.Year;
                ShowYear(spec.Year);
            }
            else if (storedYear >= 0)
            {
                int back = storedYear;
                storedYear = -1;
                ShowYear(back);
            }

            LandView.Apply(spec, heldRound);

            // the section focused from a view of another year (1972): the root flies to the cut of that year, the year
            // has just gone back, so the flight goes on to the restored year's cut at the focus's pace
            FollowCatalog(FocusSeconds);

            if (spec.Betrayal) LandService.Betrayal(true);
            if (labels != null) labels.DataLabelsHidden = spec.HideRoadLabels;
            ApplySelection();
        }

        /// <summary>A year set by a preset: the state follows and the land builds blocking (deterministic presets).</summary>
        void ShowYear(int year)
        {
            EconomyState.SetYear(year);
            requestedYear = EconomyState.Year;
            LandService.Request(requestedYear, true);
            seenState = EconomyState.Version;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            LandView.Advance(Mathf.Min(MaxStep, Time.unscaledDeltaTime), frozenMorph);
            if (EconomyState.Version != seenState)
            {
                // the viewer changed the year (keys, the chip, a click) or the season's settings: build on a worker
                seenState = EconomyState.Version;
                if (EconomyState.Year != requestedYear)
                {
                    requestedYear = EconomyState.Year;
                    LandService.Request(requestedYear, false);
                }

                if (!Same(EconomyState.Social, requestedSocial))
                {
                    requestedSocial = EconomyState.Social;
                    LandService.RequestSociety(requestedSocial, false);
                }
            }

            LandService.Tick();
            FollowCatalog(SectionRefocusSeconds);
            float road = LandView.RoadAlpha;
            Axis.TimeAxisLayer.SceneAlpha = road;
            Humans.Smv.SmvLayer.SceneAlpha = road;
        }

        /// <summary>The causality scene's road is never dimmed: the hooks go back to 1 with the economy scene.</summary>
        void OnDestroy()
        {
            if (root != null) root.OrientationChanged -= OnOrientation;
            Axis.TimeAxisLayer.SceneAlpha = 1f;
            Humans.Smv.SmvLayer.SceneAlpha = 1f;
        }

        /// <summary>Land labels kept inside the frame stay this far from its edges (px).</summary>
        const float FrameMarginPx = 6f;

        /// <summary>
        /// Shifts a fixed land label sideways just enough to stand wholly inside the frame (a label at the bowl's edge on a
        /// narrow portrait screen), as wide as the frame at most; its own offset is <paramref name="baseX"/> (px before the
        /// UI scale). Shared by the land layers whose labels a pose alone cannot keep in frame (the players', the rivers').
        /// True when the label's offset changed (the caller marks the label system dirty).
        /// </summary>
        internal static bool KeepInFrame(Camera cam, LabelSpec spec, float baseX, float padding)
        {
            if (cam == null || spec == null || spec.Hidden || !spec.Fixed) return false;
            Vector3 sp = cam.WorldToScreenPoint(spec.Data);
            float nx = baseX;
            if (sp.z > cam.nearClipPlane)
            {
                float scale = LabelSystem.UiScale, w = spec.Width * scale + 2 * padding;
                float x = sp.x + baseX * scale;
                float x0 = spec.Align == TMPro.TextAlignmentOptions.Right ? x - w
                    : spec.Align == TMPro.TextAlignmentOptions.Center ? x - 0.5f * w : x;
                float lo = FrameMarginPx, hi = Screen.width - FrameMarginPx;
                bool visible = x0 + w > 0 && x0 < Screen.width && sp.y > 0 && sp.y < Screen.height;
                if (visible && w <= hi - lo)
                {
                    if (x0 < lo) nx += (lo - x0) / scale;
                    else if (x0 + w > hi) nx += (hi - x0 - w) / scale;
                }
            }

            if (Mathf.Abs(spec.PixelOffset.x - nx) < 0.01f) return false;
            spec.PixelOffset = new Vector2(nx, spec.PixelOffset.y);
            return true;
        }

        static bool Same(SocialSettings a, SocialSettings b) =>
            a.Noise == b.Noise && a.Continuation == b.Continuation && a.Polarization == b.Polarization && a.Rewire == b.Rewire &&
            a.Forgive == b.Forgive;

        /// <summary>WHY_ECON_SELECT: selects a player by key, a tower by ticker (or name) or a season pair by index.</summary>
        void ApplySelection()
        {
            if (string.IsNullOrEmpty(select)) return;
            LandSnapshot s = LandService.Current;
            int colon = select.IndexOf(':');
            if (s == null || colon <= 0) return;
            string kind = select.Substring(0, colon), what = select.Substring(colon + 1);
            int player = -1, tower = -1, tie = -1;
            switch (kind)
            {
                case "player":
                    foreach (Player p in s.Players?.Players ?? Array.Empty<Player>()) player = p.Key == what ? p.Index : player;
                    break;
                case "tower":
                    foreach (TowerGeom t in s.Land?.Towers ?? Array.Empty<TowerGeom>())
                    {
                        if (t.Ticker == what || t.Name == what) tower = t.Company;
                    }

                    break;
                case "pair":
                    if (!int.TryParse(what, NumberStyles.Integer, CultureInfo.InvariantCulture, out tie)) tie = -1;
                    break;
            }

            if (player < 0 && tower < 0 && tie < 0)
            {
                Debug.LogWarning("[Why] WHY_ECON_SELECT: nothing matches '" + select + "' in " + s.Year.ToString(CultureInfo.InvariantCulture));
                return;
            }

            EconomyState.SetSelection(player, tower, tie);
        }
    }
}
