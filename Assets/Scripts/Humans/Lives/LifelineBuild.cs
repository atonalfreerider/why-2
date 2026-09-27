using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Why.Humans.Lives
{
    /// <summary>
    /// Everything the lifelines layer prepares off the main thread: every stream's simulated lives, the coarse
    /// lifeline mesh, the population curves and the famous figures (the denser lifeline tiers follow in the
    /// background, see <see cref="LifelineMeshes.EmitDenseTiers"/>). Pure managed code; streams are simulated
    /// in parallel with fixed per-stream seeds, so the result never depends on thread timing.
    /// </summary>
    public sealed class LifelineBuild
    {
        const int Seed = 1931; // the Histomap's year

        public LifelineMeshes Meshes { get; private set; }
        public FigureLines Figures { get; private set; }

        /// <summary>People per coarse line, by <see cref="Civ.Index"/> (0 = the stream has no lines).</summary>
        public double[] PeoplePerLine { get; private set; }

        /// <summary>Stream names by <see cref="Civ.Index"/> (the HUD's "1 line = N people" context).</summary>
        public string[] Names { get; private set; }

        /// <summary>Streams with lines.</summary>
        public int Streams { get; private set; }

        /// <summary>Builds everything. Worker thread.</summary>
        /// <param name="world">the shared human model</param>
        /// <param name="figuresJson">Data/figures (may be null)</param>
        /// <param name="demographyJson">Data/demography, for per-civ war mortality (may be null)</param>
        /// <param name="addLabel">thread-safe label sink</param>
        public static LifelineBuild Run(HumanWorld world, string figuresJson, string demographyJson,
            Action<LabelSpec> addLabel)
        {
            Dictionary<string, Dictionary<string, double>> overrides = CivWars.ParseOverrides(demographyJson);
            int count = 0;
            foreach (Civ c in world.Civs) count = Math.Max(count, c.Index + 1);

            CivLives[] lives = new CivLives[count];
            Parallel.ForEach(world.Civs, civ =>
            {
                CivWars wars = CivWars.For(world, civ, overrides);
                lives[civ.Index] = CivLives.Build(world, civ, wars, Seed + 7919 * civ.Index);
            });

            LifelineBuild build = new LifelineBuild
            {
                PeoplePerLine = new double[count],
                Names = new string[count],
                Meshes = new LifelineMeshes(lives),
                Figures = new FigureLines(world, lives)
            };

            foreach (CivLives s in lives)
            {
                if (s == null) continue;
                build.Names[s.Civ.Index] = s.Civ.Id == CivLives.UsId
                    ? s.Civ.Name + " before " + CivLives.UsCutoffYear.ToString("0", CultureInfo.InvariantCulture)
                    : s.Civ.Name;
                if (s.People.Length == 0) continue;
                build.PeoplePerLine[s.Civ.Index] = s.PeoplePerLine;
                build.Streams++;
            }

            // the coarse tier on one thread; curves and figures share another (both sample the stream grids).
            // The denser tiers are only needed close up: the layer fills them in the background later
            // (LifelineMeshes.EmitDenseTiers).
            LifelineMeshes meshes = build.Meshes;
            Parallel.Invoke(
                () => meshes.EmitTier(0),
                () =>
                {
                    meshes.EmitCurves(world.NowYear);
                    build.Figures.Build(figuresJson, addLabel);
                });
            return build;
        }
    }
}
