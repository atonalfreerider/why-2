using System;
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
        }

        public override void OnFocus(ViewPreset preset)
        {
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
            float road = LandView.RoadAlpha;
            Axis.TimeAxisLayer.SceneAlpha = road;
            Humans.Smv.SmvLayer.SceneAlpha = road;
        }

        /// <summary>The causality scene's road is never dimmed: the hooks go back to 1 with the economy scene.</summary>
        void OnDestroy()
        {
            Axis.TimeAxisLayer.SceneAlpha = 1f;
            Humans.Smv.SmvLayer.SceneAlpha = 1f;
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
