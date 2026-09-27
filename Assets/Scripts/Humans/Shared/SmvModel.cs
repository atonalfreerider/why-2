using UnityEngine;

namespace Why.Humans
{
    /// <summary>
    /// "Social market value" model, ported from the smv project (Person.SMV and the README storyline):
    ///
    /// Women - value from a bell curve (1..10); drops over the years on a log scale; drops 2 points once
    /// when having a child; a high partner count lowers value (and marriage probability).
    /// Men - value from a bell curve (height/charisma); increases with asset value (accumulates with age);
    /// partner count increases value; drops 1 point when having a child; starts to drop above 42.
    /// Women's attraction to men follows a log curve; pairing requires the woman's value to be equal or
    /// lower than the man's. Children (under 18) are outside the market.
    ///
    /// All values are 0..10. Parameters are in one place so the model stays transparent and tunable.
    /// </summary>
    public static class SmvModel
    {
        public const float AdultAge = 18f;

        // women
        public const float FemalePeakStart = 20f;
        public const float FemaleDeclineStart = 28f;
        public const float FemaleLogDecline = 2.2f;     // points per ln-unit of years past the decline start
        public const float FemaleDeclineScale = 4f;     // years per ln-unit
        public const float ChildPenaltyFemale = 2f;
        public const float PartnerPenaltyPerPartner = 0.15f;
        public const int PartnerPenaltyFree = 3;
        public const float PartnerPenaltyMax = 2f;

        // men
        public const float MalePeakAge = 24f;
        public const float AssetPoints = 3f;            // max points from accumulated assets
        public const float AssetsFullAge = 55f;
        public const float MaleDeclineStart = 42f;
        public const float ChildPenaltyMale = 1f;
        public const float PartnerBonusMax = 1.5f;

        /// <summary>Bell-curve base value (mean 5, sd 1.8, clamped 1..10) from a uniform RNG.</summary>
        public static float SampleBase(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = 1.0 - rng.NextDouble();
            double z = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);
            return Mathf.Clamp((float)(5.0 + 1.8 * z), 1f, 10f);
        }

        /// <param name="male">sex</param>
        /// <param name="baseValue">bell-curve base (1..10)</param>
        /// <param name="age">years</param>
        /// <param name="hasChildren">has had at least one child</param>
        /// <param name="partners">lifetime sexual partners so far</param>
        /// <param name="wealth">asset potential 0..1 (men); ignored for women</param>
        public static float Value(bool male, float baseValue, float age, bool hasChildren, int partners, float wealth)
        {
            if (age < AdultAge) return 0f;
            float v;
            if (male)
            {
                float maturity = SmoothStep(15f, MalePeakAge, age);
                float assets = AssetPoints * Mathf.Clamp01(wealth) * SmoothStep(20f, AssetsFullAge, age);
                float partnerBonus = Mathf.Min(PartnerBonusMax, 0.4f * Mathf.Log(1 + partners));
                float over = Mathf.Max(0, age - MaleDeclineStart);
                float decline = 0.1f * over + 0.002f * over * over;
                v = baseValue * maturity + assets + partnerBonus - decline - (hasChildren ? ChildPenaltyMale : 0);
            }
            else
            {
                float maturity = SmoothStep(14f, FemalePeakStart, age);
                float over = Mathf.Max(0, age - FemaleDeclineStart);
                float decline = FemaleLogDecline * Mathf.Log(1 + over / FemaleDeclineScale);
                float partnerPenalty = Mathf.Min(PartnerPenaltyMax,
                    PartnerPenaltyPerPartner * Mathf.Max(0, partners - PartnerPenaltyFree));
                v = baseValue * maturity - decline - (hasChildren ? ChildPenaltyFemale : 0) - partnerPenalty;
            }

            return Mathf.Clamp(v, 0f, 10f);
        }

        /// <summary>Women's attraction to a man follows a log curve of his value (0..1).</summary>
        public static float Attraction(float manValue) => Mathf.Log(1 + Mathf.Max(0, manValue)) / Mathf.Log(11f);

        /// <summary>Pairing rule from the storyline: the woman's value must be equal or lower than the man's.</summary>
        public static bool CanPair(float womanValue, float manValue) => womanValue <= manValue;

        /// <summary>Relative chance a woman with this partner count marries (high counts lower it).</summary>
        public static float MarriageLikelihood(int partners) => 1f / (1f + 0.08f * Mathf.Max(0, partners - PartnerPenaltyFree));

        /// <summary>Height of a lifeline point in data space for a value (adults) or age (children).</summary>
        public static float Height(float value, float age)
        {
            if (age < AdultAge) return GraphStyle.HumansY + GraphStyle.SmvHeight * 0.08f * Mathf.Clamp01(age / AdultAge);
            return GraphStyle.HumansY + GraphStyle.SmvHeight * Mathf.Clamp01(value / 10f);
        }

        /// <summary>
        /// Offset from the band center, as a fraction (0..1) of the population envelope, for the person at
        /// rank k of n (0 = highest value). High value clusters near the center; low value spreads outward
        /// (the smv project spread by count^1.5). Married people are drawn toward the middle.
        /// </summary>
        public static float RankOffset(int k, int n, bool married)
        {
            float f = n <= 1 ? 0.5f : (k + 0.5f) / n;
            float o = 0.08f + 0.92f * Mathf.Pow(f, 1.5f);
            return married ? 0.04f + o * 0.5f : o;
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
