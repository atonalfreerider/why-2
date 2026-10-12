using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>What a mind diagram shows: the ten drives (each pole sums to 1), the motive's fear share, reason and
    /// future orientation, the choice (spend / save / borrow) and the memory (balance sheet) it feeds back into.</summary>
    public sealed class MindState
    {
        public string Title = "", Note = "";
        public readonly float[] Desire = new float[5], Fear = new float[5];
        public float FearShare = .5f, Reason = .5f, Future = .5f, Fantasy;
        /// <summary>Shares of the year's disposable income spent, saved and borrowed (0..1), and their readouts.</summary>
        public float Spend = .9f, Save = .1f, Borrow;
        public string ChoiceText = "", MemoryText = "";
        /// <summary>Net worth and debt in dollars (the memory rings, logarithmic).</summary>
        public double Wealth, Debt;
        public bool InControl;

        /// <summary>A simulated person's mind in a year: <see cref="EconomicLives.DriveWeights"/> and the PersonYear.
        /// Null when the person was not simulated then.</summary>
        public static MindState Of(EconomicLives lives, int person, int year)
        {
            if (lives == null || !lives.TryGet(person, year, out PersonYear r)) return null;
            var m = new MindState();
            if (!lives.DriveWeights(person, year, m.Desire, m.Fear)) for (int k = 0; k < 5; k++) m.Desire[k] = m.Fear[k] = .2f;
            m.FearShare = r.FearShare; m.Reason = r.Reason; m.Future = r.Future; m.Fantasy = r.Fantasy; m.InControl = r.InControl;
            double income = Math.Max(1, r.Disposable);
            m.Spend = Mathf.Clamp01((float)(r.Spending / income));
            m.Save = Mathf.Clamp01((float)(Math.Max(0, r.Saving) / income));
            m.Borrow = Mathf.Clamp01((float)(Math.Max(0, -r.Saving) / income));
            m.Wealth = r.Wealth; m.Debt = r.Debt;
            m.ChoiceText = "spend " + PersonLife.Money(r.Spending) + (r.Saving >= 0 ? " · save " + PersonLife.Pct(r.Saving / income) : " · borrow " + PersonLife.Money(-r.Saving));
            m.MemoryText = "net worth " + PersonLife.Money(r.Wealth) + " · debt " + PersonLife.Money(r.Debt) + " · runway " + Runway(r.Runway);
            return m;
        }

        /// <summary>A cohort's mind: its adults' mean drives (up to 48 sampled in index order), with the scenario ledger's
        /// fear, reason and balance sheet when one runs (group $B).</summary>
        public static MindState Of(EconomicLives lives, Player p, int year, PlayerMindProgram.State state)
        {
            var m = new MindState(); int n = 0;
            float[] d = new float[5], f = new float[5];
            if (lives != null && p?.Adults != null)
                for (int a = 0; a < p.Adults.Length && n < 48; a++)
                {
                    if (!lives.DriveWeights(p.Adults[a], year, d, f)) continue;
                    for (int k = 0; k < 5; k++) { m.Desire[k] += d[k]; m.Fear[k] += f[k]; }
                    n++;
                }
            for (int k = 0; k < 5; k++) { m.Desire[k] = n > 0 ? m.Desire[k] / n : .2f; m.Fear[k] = n > 0 ? m.Fear[k] / n : .2f; }
            if (p == null) return m;
            m.FearShare = state?.Fear ?? p.Fear; m.Reason = state?.Reason ?? p.Reason; m.Future = p.Future; m.Fantasy = state?.Fantasy ?? p.Fantasy;
            double spend = state?.Spending ?? p.Spending, saving = state?.Saving ?? p.Saving, income = Math.Max(1e-6, spend + saving);
            m.Spend = Mathf.Clamp01((float)(spend / income)); m.Save = Mathf.Clamp01((float)(Math.Max(0, saving) / income)); m.Borrow = Mathf.Clamp01((float)(Math.Max(0, -saving) / income));
            m.Wealth = (state?.Wealth ?? p.Wealth) * 1e9 / Math.Max(1, p.Adults.Length * 1e5); m.Debt = (state?.Debt ?? p.Debt) * 1e9 / Math.Max(1, p.Adults.Length * 1e5);
            m.ChoiceText = "spend " + LandFacts.Money(spend) + (saving >= 0 ? " · save " + LandFacts.Money(saving) : " · borrow " + LandFacts.Money(-saving));
            m.MemoryText = "assets " + LandFacts.Money(state?.Assets ?? Math.Max(0, p.Wealth + p.Debt)) + " · debt " + LandFacts.Money(state?.Debt ?? p.Debt) + " (group)";
            return m;
        }

        static string Runway(float years) => years >= 1 ? years.ToString("0.0", LandFacts.Ci) + " years" : Mathf.RoundToInt(years * 12) + " months";
    }

    /// <summary>
    /// The atomistic mind, drawn in 3D so it reads from any side (a headset can walk around it): five desire drives on
    /// the rose side and five fear drives on the ice side of a ring, each a small sphere of rings sized by its share of the
    /// motive (drive weight x the pole's share: desire 1 - fear share, fear the fear share). Every drive feeds a thread
    /// into the deliberation gate at the center, whose aperture is reason; out of the gate the choice splits into spend
    /// (colored by the motive), save (gold) and borrow (rose), and the choice feeds the memory below (the balance
    /// sheet: gold ring net worth, rose ring debt), which closes the loop back into the drives (a thin buffer raises the
    /// fears of ruin and exposure next year). The drives and their weights are the lives model's; the drawing is a
    /// conceptual decision program, not neuroanatomy.
    /// </summary>
    public static class MindDiagram
    {
        public static readonly Color Gold = new Color(1, .70f, .20f), Steel = new Color(.40f, .50f, .60f), White = new Color(1, .96f, .9f);
        /// <summary>Drive ids in the lives model's order.</summary>
        public static string[] DesireIds => LivesInputs.DesireIds;
        public static string[] FearIds => LivesInputs.FearIds;

        /// <summary>The rose spectrum of desire and the ice spectrum of fear (deep to pale across the five drives).</summary>
        public static Color DesireColor(int k) => Color.Lerp(new Color(1, .20f, .46f), new Color(1, .58f, .80f), k / 4f);
        public static Color FearColor(int k) => Color.Lerp(new Color(.30f, .62f, 1), new Color(.62f, .92f, 1), k / 4f);

        /// <summary>A drive's display name (psyche.json), else its id capitalized.</summary>
        public static string Name(bool fear, int k)
        {
            string id = (fear ? FearIds : DesireIds)[k];
            var list = fear ? LandService.Model?.Data?.Psyche?.Fears : LandService.Model?.Data?.Psyche?.Desires;
            if (list != null) foreach (var d in list) if (d != null && d.Id == id && !string.IsNullOrEmpty(d.Name)) return d.Name;
            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }

        /// <summary>A drive node relative to the mind's center (scale 1): desires around -x, fears around +x.</summary>
        public static Vector3 Node(bool fear, int k, float scale)
        {
            float a = (fear ? 0 : Mathf.PI) + (k - 2) * .44f * (fear ? 1 : -1);
            return new Vector3(Mathf.Cos(a) * 3.3f, .9f + .25f * Mathf.Cos((k - 2) * 1.1f), Mathf.Sin(a) * 3.3f) * scale;
        }

        /// <summary>A drive's share of the whole motive (weight x its pole).</summary>
        public static float Share(MindState m, bool fear, int k) => fear ? m.Fear[k] * m.FearShare : m.Desire[k] * (1 - m.FearShare);

        public static Vector3 Gate(float s) => new Vector3(0, 1.0f, 0) * s;
        public static Vector3 Memory(float s) => new Vector3(0, -2.3f, 0) * s;

        /// <summary>Draws the mind at <paramref name="o"/>; labels go through <paramref name="label"/> (text, point, size, color).</summary>
        public static void Build(LineMeshBuilder b, Vector3 o, MindState m, Action<string, Vector3, float, Color> label, float s = 1)
        {
            if (m == null) return;
            Vector3 gate = o + Gate(s), memory = o + Memory(s);
            Vector3 spend = o + new Vector3(0, -.85f, 0) * s, save = o + new Vector3(.2f, -.95f, 1.25f) * s, borrow = o + new Vector3(-.2f, -.95f, -1.25f) * s;
            for (int pole = 0; pole < 2; pole++)
                for (int k = 0; k < 5; k++)
                {
                    bool fear = pole == 1; float w = Share(m, fear, k);
                    Color c = fear ? FearColor(k) : DesireColor(k); Vector3 at = o + Node(fear, k, s);
                    float r = s * (.14f + .62f * Mathf.Sqrt(Mathf.Max(0, w)));
                    Sphere(b, at, r, c, 1.6f, 2.4f + 3 * w);
                    Thread(b, at, gate, c, .6f + 9 * w, 1.6f + 3 * w, .5f * s);
                    label((fear ? "FEAR · " : "DESIRE · ") + Name(fear, k).ToUpperInvariant() + "\n" + PersonLife.Pct(w) + " of the motive", at + Vector3.up * (r + .45f * s), .085f * s, Color.Lerp(c, Color.white, .25f));
                }
            // The deliberation gate: a gimbal whose inner aperture is reason (the higher OS), with the future's weight.
            Sphere(b, gate, .62f * s, White, 1.4f, 2.2f);
            Ring(b, gate, (.12f + .45f * m.Reason) * s, Gold, 2.6f, 3.6f, false);
            Ring(b, gate, (.12f + .45f * m.Reason) * s, Gold, 2.6f, 3.6f, true);
            label("DELIBERATION\nreason " + PersonLife.Pct(m.Reason) + " · future " + PersonLife.Pct(m.Future) + " · fantasy " + PersonLife.Pct(m.Fantasy), gate + Vector3.up * .95f * s, .09f * s, White);
            // The choice: spend (the motive's color), save (gold), borrow (rose); widths are shares of the year's income.
            Color motive = Color.Lerp(DesireColor(0), FearColor(0), m.FearShare);
            Thread(b, gate, spend, motive, 1 + 8 * m.Spend, 2.6f, .1f * s); Sphere(b, spend, (.16f + .3f * m.Spend) * s, motive, 1.5f, 2.6f);
            if (m.Save > .005f) { Thread(b, gate, save, Gold, 1 + 14 * m.Save, 3, .3f * s); Sphere(b, save, (.12f + .5f * Mathf.Sqrt(m.Save)) * s, Gold, 1.5f, 3); }
            if (m.Borrow > .005f) { Thread(b, gate, borrow, DesireColor(0), 1 + 14 * m.Borrow, 3, .3f * s); Sphere(b, borrow, (.12f + .5f * Mathf.Sqrt(m.Borrow)) * s, DesireColor(0), 1.5f, 3); }
            label("CHOICE\n" + m.ChoiceText, spend + new Vector3(0, -.15f, 0) * s + Vector3.down * .35f * s, .085f * s, Color.Lerp(motive, Color.white, .4f));
            // Memory: the balance sheet the choice leaves for next year (logarithmic rings), and the feedback into the drives.
            float wr = (.35f + Mathf.Log10(1 + (float)Math.Max(0, m.Wealth)) * .14f) * s, dr = (.3f + Mathf.Log10(1 + (float)Math.Max(0, m.Debt)) * .12f) * s;
            Ring(b, memory, wr, Gold, 2.2f, m.InControl ? 4 : 2.6f, false); Ring(b, memory + Vector3.up * .08f * s, wr * 1.06f, Gold, 1, 1.6f, false);
            if (m.Debt > 0) Ring(b, memory + Vector3.up * .16f * s, dr, DesireColor(0), 2, 2.4f, false);
            Thread(b, spend, memory, motive, 1.2f, 1.8f, -.2f * s); if (m.Save > .005f) Thread(b, save, memory, Gold, 1.4f, 2.2f, -.2f * s);
            if (m.Borrow > .005f) Thread(b, borrow, memory, DesireColor(0), 1.4f, 2.2f, -.2f * s);
            for (int k = 0; k < 5; k += 2) Thread(b, memory, o + Node(true, k, s), WithAlpha(FearColor(4), .35f), .8f, 1.2f, -1.2f * s);
            label("MEMORY\n" + m.MemoryText, memory + Vector3.down * .55f * s, .085f * s, Gold);
            // The skull: a faint envelope gives the program a volume without claiming neuroanatomy.
            for (int j = 0; j < 7; j++)
            {
                float t = (j - 3) / 3.6f, y = t * 3.2f * s;
                Ring(b, o + new Vector3(0, -.5f * s + y, 0), 4.3f * s * Mathf.Sqrt(Mathf.Max(0, 1 - t * t)), WithAlpha(Steel, .18f), .6f, 1, false);
            }
            if (!string.IsNullOrEmpty(m.Title)) label(m.Title, o + Vector3.up * 4.3f * s, .13f * s, Color.white);
            if (!string.IsNullOrEmpty(m.Note)) label(m.Note, o + Vector3.up * 3.75f * s, .075f * s, Steel * 1.6f);
        }

        /// <summary>Three orthogonal rings: a sphere that reads from every side.</summary>
        public static void Sphere(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            Ring(b, c, r, color, width, intensity, false); Ring(b, c, r, color, width * .8f, intensity * .8f, true);
            var pts = new Vector3[33]; for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI / 16; pts[i] = c + new Vector3(0, Mathf.Sin(a) * r, Mathf.Cos(a) * r); }
            b.AddPolyline(pts, WithAlpha(color, color.a * .8f), width * .8f, 0, 0, intensity * .8f);
        }

        public static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity, bool vertical)
        {
            var pts = new Vector3[41];
            for (int i = 0; i <= 40; i++) { float a = i * Mathf.PI / 20; pts[i] = c + (vertical ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) : new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        /// <summary>A flowing thread from a to z, bowed upward by <paramref name="lift"/> (negative bows down), with a world width so it keeps a body up close.</summary>
        public static void Thread(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[17];
            for (int i = 0; i < pts.Length; i++) { float t = i / 16f; pts[i] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, .012f * width, intensity); }
            b.AddFlowPath(pts, 0, 1.5f);
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
