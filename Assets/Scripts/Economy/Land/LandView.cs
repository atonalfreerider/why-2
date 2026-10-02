using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// The land's view state, one per scene (main thread): the preset on screen, the transition's clock
    /// (<see cref="MorphTime"/>: 0 = folded into the cut, 2.4 s = the bowl) and the land's fade-in (<see cref="Reveal"/>),
    /// the eased emphasis of every land group (<see cref="Alpha"/>) and the label groups shown, and the season's
    /// playback (<see cref="Round"/>). Set only by the land's view layer (Order 44, before every land layer ticks) and by
    /// the social panel's controls; read by every land layer. No layer other than the section layer morphs: the others
    /// multiply their materials' alpha by <see cref="Alpha"/> of their group.
    /// </summary>
    public static class LandView
    {
        /// <summary>The preset on screen (EconomyViews id).</summary>
        public static string PresetId { get; private set; } = "overview";

        /// <summary>The transition's clock, 0..2.4 s (2.4 = unfolded).</summary>
        public static float MorphTime { get; private set; }

        /// <summary>The land layers' fade (0..1): in over the unfold's last 0.2 s, out over the fold's first 0.2 s.</summary>
        public static float Reveal { get; private set; }

        /// <summary>The season's round shown (0..96) and whether it plays.</summary>
        public static int Round { get; private set; } = LandStyle.SeasonRounds;

        public static bool Playing { get; private set; }

        /// <summary>The social layer shows the betrayal season (the betrayal view).</summary>
        public static bool ShowBetrayal { get; private set; }

        /// <summary>Incremented whenever any <see cref="LabelsShown"/> value changes (land layers then update their labels).</summary>
        public static int LabelsVersion { get; private set; }

        /// <summary>Incremented whenever <see cref="Round"/> changes (the social layer rebuilds its ties at the 4 Hz tick).</summary>
        public static int RoundVersion { get; private set; }

        static readonly float[] eased = new float[(int)LandGroup.Count];
        static readonly float[] target = new float[(int)LandGroup.Count];
        static ViewSpec spec;
        static bool folding, snapped;
        static float roundClock, holdClock;
        static int labelMask = -1;

        /// <summary>
        /// The eased emphasis of a group, times <see cref="Reveal"/> for the land's own groups (the road and the cut stay
        /// visible while the land is folded).
        /// </summary>
        public static float Alpha(LandGroup g)
        {
            int i = (int)g;
            if (i < 0 || i >= eased.Length) return 0;
            return g == LandGroup.Road || g == LandGroup.Cut ? eased[i] : eased[i] * Reveal;
        }

        /// <summary>Whether a group's labels show: the preset lists the group and its alpha is at least 0.3 (7.2).</summary>
        public static bool LabelsShown(LandGroup g) => spec != null && spec.ShowsLabels(g) && Alpha(g) >= 0.3f;

        /// <summary>Plays or pauses the season (the social panel).</summary>
        public static void Play(bool on)
        {
            if (on && !Playing)
            {
                roundClock = Round;
                holdClock = 0;
            }

            Playing = on;
        }

        /// <summary>One round forward, paused (after the last round: back to the first).</summary>
        public static void Step()
        {
            Playing = false;
            SetRound(Round >= LandStyle.SeasonRounds ? 0 : Round + 1);
        }

        /// <summary>Plays the season from round 0 (the betrayal view: from the start of its loop).</summary>
        public static void Replay()
        {
            SetRound(ShowBetrayal ? LandStyle.BetrayalLoop0 : 0);
            roundClock = Round;
            holdClock = 0;
            Playing = true;
        }

        // ------------------------------------------------------------------ driven by the view layer

        /// <summary>
        /// A preset was focused: its emphasis becomes the target (snapped the first time), the morph clock heads for its
        /// target, the season shows its round (<paramref name="heldRound"/> &gt;= 0 overrides the preset's).
        /// </summary>
        internal static void Apply(ViewSpec s, int heldRound)
        {
            spec = s;
            PresetId = s.Id;
            ShowBetrayal = s.Betrayal;
            for (int g = 0; g < target.Length; g++)
            {
                target[g] = s.Alpha[g];
                if (!snapped) eased[g] = s.Alpha[g];
            }

            snapped = true;
            folding = s.MorphTarget < MorphTime;
            int round = s.HoldRound >= 0 && heldRound >= 0 ? heldRound : s.HoldRound;
            if (round >= 0)
            {
                Playing = false;
                SetRound(Mathf.Clamp(round, 0, LandStyle.SeasonRounds));
            }

            UpdateLabels();
        }

        /// <summary>
        /// One frame: the morph clock moves toward the preset's target (forward 1x, backward 2x; frozen at
        /// <paramref name="frozenMorph"/> while unfolding when it is &gt;= 0), the reveal follows it, the emphasis eases,
        /// the season plays.
        /// </summary>
        internal static void Advance(float dt, float frozenMorph)
        {
            float goal = spec?.MorphTarget ?? LandStyle.UnfoldSeconds;
            if (MorphTime < goal)
            {
                folding = false;
                MorphTime = frozenMorph >= 0 ? Mathf.Min(goal, frozenMorph) : Mathf.Min(goal, MorphTime + dt);
            }
            else if (MorphTime > goal)
            {
                folding = true;
                MorphTime = Mathf.Max(goal, MorphTime - dt * LandStyle.UnfoldSeconds / LandStyle.FoldSeconds);
            }

            float end = LandStyle.UnfoldSeconds;
            Reveal = folding
                ? LandMath.Phase(MorphTime, end - LandStyle.FoldRevealSeconds * end / LandStyle.FoldSeconds, end)
                : LandMath.Phase(MorphTime, LandStyle.RevealStart, end);

            float step = LandStyle.EmphasisRate * dt;
            for (int g = 0; g < eased.Length; g++) eased[g] = Mathf.MoveTowards(eased[g], target[g], step);
            if (Playing) PlayStep(dt);
            UpdateLabels();
        }

        /// <summary>Back to the initial state (a new scene load): folded, nothing shown, the season at its end.</summary>
        internal static void ResetState()
        {
            spec = null;
            PresetId = "overview";
            MorphTime = 0;
            Reveal = 0;
            folding = snapped = false;
            Playing = ShowBetrayal = false;
            Round = LandStyle.SeasonRounds;
            roundClock = holdClock = 0;
            labelMask = -1;
            for (int g = 0; g < eased.Length; g++) eased[g] = target[g] = 0;
        }

        /// <summary>
        /// Playback at 4 rounds a second: the season runs 0..96, holds 2 s and loops; the betrayal view loops rounds
        /// 40..60 with the same hold.
        /// </summary>
        static void PlayStep(float dt)
        {
            int first = ShowBetrayal ? LandStyle.BetrayalLoop0 : 0;
            int last = ShowBetrayal ? LandStyle.BetrayalLoop1 : LandStyle.SeasonRounds;
            if (Round >= last)
            {
                holdClock += dt;
                if (holdClock < LandStyle.SeasonHoldSeconds) return;
                holdClock = 0;
                roundClock = first;
                SetRound(first);
                return;
            }

            roundClock += dt * LandStyle.RoundsPerSecond;
            SetRound(Mathf.Clamp(Mathf.FloorToInt(roundClock), first, last));
        }

        static void SetRound(int round)
        {
            if (round == Round) return;
            Round = round;
            RoundVersion++;
        }

        static void UpdateLabels()
        {
            int mask = 0;
            for (int g = 0; g < (int)LandGroup.Count; g++)
            {
                if (LabelsShown((LandGroup)g)) mask |= 1 << g;
            }

            if (mask == labelMask) return;
            labelMask = mask;
            LabelsVersion++;
        }
    }
}
