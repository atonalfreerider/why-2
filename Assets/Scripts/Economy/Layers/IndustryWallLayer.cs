using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The corporate layer: a wall of industries standing beneath the people from 1947 to now, a stacked area chart of
    /// value added in 2025 dollars. Its height follows the real size of the economy (the largest year reaches just
    /// under the people), so the wall grows elevenfold along the road. Floors from the ground up follow the user's
    /// notebook: government (the foundation), raw (oil, gas, metals in matter red; farms in life green), manufacturing
    /// and infrastructure, services, and tech nearest the people.
    ///
    /// Each industry's band is split by who receives its value: the dim lower part pays wages, a faint sliver pays
    /// production taxes and replaces worn-out capital, and the bright upper part is what owners keep (net operating
    /// surplus: profits, proprietors' income, rent and interest). Value created is the whole band; value captured is
    /// its glowing top. The split follows each industry's latest shares, with wages scaled over time by the economy's
    /// labor share.
    ///
    /// The wall stands on the population's center line (data rho), in the vertical plane of the road; everything is in
    /// data space, so the lens straightens it with the road.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class IndustryWallLayer : GraphLayer
    {
        /// <summary>Opacity and HDR intensity of the three parts of a band.</summary>
        const float WagesAlpha = 0.22f, UpkeepAlpha = 0.09f, OwnersAlpha = 0.5f;

        const float WagesIntensity = 0.55f, UpkeepIntensity = 0.45f, OwnersIntensity = 1.35f;

        /// <summary>Upkeep (taxes, depreciation) is drawn in a neutral grey, not the industry's hue.</summary>
        static readonly Color UpkeepColor = new Color(0.62f, 0.64f, 0.68f);

        /// <summary>Year the wages share is scaled to (the year of the industries' compShare).</summary>
        const double ShareYear = 2024;

        /// <summary>Industry labels are staggered across these years so they do not all crowd at the present end.</summary>
        const int LabelYearLatest = 2022, LabelYearStep = 7, LabelStagger = 5;

        public override int Order => 40;

        // built in Prepare
        SurfaceMeshBuilder fills;
        LineMeshBuilder lines;
        readonly List<LabelSpec> labels = new List<LabelSpec>();

        Material fillMat, lineMat;

        /// <summary>The wall at a sample year: per industry, the bottom of wages, upkeep, owners and the band's top.</summary>
        sealed class Column
        {
            public double Year;
            public float U, Rho;
            public float[] Lo, Upkeep, Owners, Hi;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            EconomyModel model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            if (model == null || model.Data.Industries.Count == 0)
            {
                Debug.LogWarning("[Why] IndustryWallLayer: no economy data, wall skipped");
                return;
            }

            EconomyData data = model.Data;
            SmvPopulation pop = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            List<Column> wall = BuildColumns(data, pop, ctx.NowYear);
            if (wall.Count < 2) return;

            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(wall.Count * (data.Industries.Count + 2));
            BuildBands(data, wall);
            BuildEdges(data, wall);
            Register(ctx, data, wall);
            Debug.Log($"[Why] IndustryWallLayer.Prepare {sw.ElapsedMilliseconds} ms: {data.Industries.Count} industries, " +
                      $"{wall.Count} years");
        }

        /// <summary>The wall sampled every year from the first year of data to now.</summary>
        static List<Column> BuildColumns(EconomyData data, SmvPopulation pop, double now)
        {
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count;
            List<double> years = new List<double>();
            for (int y = data.FirstYear; y < now; y++) years.Add(y);
            years.Add(now);

            // real value added (2025 dollars) per industry and year, and the largest total
            double[,] real = new double[years.Count, n];
            double max = 0;
            for (int j = 0; j < years.Count; j++)
            {
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Max(0, data.Real(inds[i].ValueAdded.GrowthAt(years[j]), years[j]));
                    real[j, i] = v;
                    total += v;
                }

                max = Math.Max(max, total);
            }

            if (max <= 0) return new List<Column>();
            float scale = (float)((EconomyStyle.WallTopY - EconomyStyle.GroundY) / max);
            double laborRef = data.LaborShare.IsEmpty ? 1 : data.LaborShare.At(ShareYear);

            List<Column> columns = new List<Column>(years.Count);
            for (int j = 0; j < years.Count; j++)
            {
                double year = years[j];
                double labor = data.LaborShare.IsEmpty || laborRef <= 0 ? 1 : data.LaborShare.At(year) / laborRef;
                Column c = new Column
                {
                    Year = year,
                    U = EconomyStage.U(year),
                    Rho = CenterAt(pop, year) + EconomyStyle.WallRhoOffset,
                    Lo = new float[n], Upkeep = new float[n], Owners = new float[n], Hi = new float[n]
                };

                float acc = EconomyStyle.GroundY;
                for (int i = 0; i < n; i++)
                {
                    Industry ind = inds[i];
                    float h = (float)real[j, i] * scale;
                    double upkeep = Math.Min(1, ind.TaxShare + ind.DepShare);
                    double wages = Math.Max(0, Math.Min(1 - upkeep, ind.CompShare * labor));
                    c.Lo[i] = acc;
                    c.Upkeep[i] = acc + (float)(h * wages);
                    c.Owners[i] = c.Upkeep[i] + (float)(h * upkeep);
                    c.Hi[i] = acc + h;
                    c.Owners[i] = Mathf.Min(c.Owners[i], c.Hi[i]);
                    acc += h;
                }

                columns.Add(c);
            }

            return columns;
        }

        /// <summary>The population's center line (data rho) at a year; the framing default before it exists.</summary>
        static float CenterAt(SmvPopulation pop, double year)
        {
            if (pop?.Sim == null || pop.Sim.StepCount == 0) return EconomyStyle.FramingRho;
            return pop.Sim.Center[pop.Sim.StepAt(year)];
        }

        void BuildBands(EconomyData data, List<Column> wall)
        {
            int m = wall.Count;
            List<Vector3> a = new List<Vector3>(m), b = new List<Vector3>(m);
            List<Color32> colors = new List<Color32>(m);
            for (int i = 0; i < data.Industries.Count; i++)
            {
                Industry ind = data.Industries[i];
                Color hue = EconomyStyle.Level(ind.Level);
                AddPart(wall, i, c => c.Lo[i], c => c.Upkeep[i], Tint(hue, WagesAlpha), WagesIntensity,
                    EconomyIds.IndustryPart(i, EconomyIds.PartWages), a, b, colors);
                AddPart(wall, i, c => c.Upkeep[i], c => c.Owners[i], Tint(UpkeepColor, UpkeepAlpha), UpkeepIntensity,
                    EconomyIds.IndustryPart(i, EconomyIds.PartUpkeep), a, b, colors);
                AddPart(wall, i, c => c.Owners[i], c => c.Hi[i], Tint(hue, OwnersAlpha), OwnersIntensity,
                    EconomyIds.IndustryPart(i, EconomyIds.PartOwners), a, b, colors);
            }
        }

        void AddPart(List<Column> wall, int industry, Func<Column, float> lo, Func<Column, float> hi, Color32 color,
            float intensity, int id, List<Vector3> a, List<Vector3> b, List<Color32> colors)
        {
            a.Clear();
            b.Clear();
            colors.Clear();
            foreach (Column c in wall)
            {
                a.Add(new Vector3(c.U, lo(c), c.Rho));
                b.Add(new Vector3(c.U, Mathf.Max(lo(c), hi(c)), c.Rho));
                colors.Add(color);
            }

            fills.AddBand(a, b, colors, id, intensity);
        }

        /// <summary>Thin lines between industries, brighter ones on top of each tier and a gold line along the top.</summary>
        void BuildEdges(EconomyData data, List<Column> wall)
        {
            IReadOnlyList<Industry> inds = data.Industries;
            List<LinePoint> pts = new List<LinePoint>(wall.Count);
            for (int i = 0; i < inds.Count; i++)
            {
                bool tierTop = i == inds.Count - 1 || inds[i + 1].TierIndex != inds[i].TierIndex;
                bool wallTop = i == inds.Count - 1;
                Color hue = wallTop ? EconomyStyle.Capital : EconomyStyle.Level(inds[i].Level);
                Color32 color = Tint(hue, wallTop ? 0.85f : tierTop ? 0.55f : 0.28f);
                float width = wallTop ? 1.8f : tierTop ? 1.3f : 0.7f;
                float intensity = wallTop ? 1.8f : tierTop ? 1.1f : 0.8f;
                pts.Clear();
                foreach (Column c in wall)
                {
                    pts.Add(new LinePoint(new Vector3(c.U, c.Hi[i], c.Rho), color, width, 0, intensity));
                }

                lines.AddPolyline(pts, wallTop ? EconomyIds.WallTop : EconomyIds.IndustryPart(i, EconomyIds.PartEdge));
            }

            // the ground line the wall stands on
            pts.Clear();
            Color32 ground = Tint(GraphStyle.AxisDim, 0.5f);
            foreach (Column c in wall) pts.Add(new LinePoint(new Vector3(c.U, EconomyStyle.GroundY, c.Rho), ground, 1f));
            lines.AddPolyline(pts, GraphIds.None);
        }

        // ------------------------------------------------------------------ labels and anchors

        void Register(GraphContext ctx, EconomyData data, List<Column> wall)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            IReadOnlyList<Industry> inds = data.Industries;
            Column now = wall[wall.Count - 1];
            int last = data.LastYear;
            double gdpLast = data.Gdp.GrowthAt(last);

            // industries: a label inside the band, staggered across recent decades within each tier
            int[] rankInTier = new int[inds.Count];
            for (int i = 0, r = 0; i < inds.Count; i++)
            {
                r = i > 0 && inds[i].TierIndex == inds[i - 1].TierIndex ? r + 1 : 0;
                rankInTier[i] = r;
            }

            for (int i = 0; i < inds.Count; i++)
            {
                Industry ind = inds[i];
                int labelYear = LabelYearLatest - LabelYearStep * (rankInTier[i] % LabelStagger);
                Column c = ColumnAt(wall, labelYear);
                double va = ind.ValueAdded.GrowthAt(last);
                double share = gdpLast > 0 ? va / gdpLast : 0;
                IdRange ids = EconomyIds.Industries(i, i);
                Anchors.Register(new Anchor
                {
                    Key = "industry:" + ind.Id,
                    Label = ind.Name,
                    Blurb = IndustryBlurb(data, ind, last, ci),
                    Level = GraphLevel.Humans,
                    YearsAgo = ctx.NowYear - labelYear,
                    EndYearsAgo = 0,
                    Y = 0.5f * (c.Lo[i] + c.Hi[i]),
                    Rho = c.Rho,
                    Ids = ids,
                    Tier = share > 0.04 ? 1 : 2
                });

                AddLabel(ctx, new LabelSpec
                {
                    Text = ind.Name,
                    Data = new Vector3(c.U, 0.5f * (c.Lo[i] + c.Hi[i]), c.Rho),
                    Priority = 6 + (float)(120 * share),
                    SizePx = share > 0.06 ? 12 : 11,
                    Color = GraphStyle.Text,
                    Align = TMPro.TextAlignmentOptions.Center,
                    AnchorKey = "industry:" + ind.Id,
                    Ids = ids
                });
            }

            // tiers: the notebook's words at the present end of the wall, beside their floor
            foreach (Tier t in data.Tiers)
            {
                int first = -1, lastIndex = -1;
                for (int i = 0; i < inds.Count; i++)
                {
                    if (inds[i].TierIndex != t.Index) continue;
                    if (first < 0) first = i;
                    lastIndex = i;
                }

                if (first < 0) continue;
                double va = 0;
                for (int i = first; i <= lastIndex; i++) va += inds[i].ValueAdded.GrowthAt(last);
                IdRange ids = EconomyIds.Industries(first, lastIndex);
                float mid = 0.5f * (now.Lo[first] + now.Hi[lastIndex]);
                Anchors.Register(new Anchor
                {
                    Key = "tier:" + t.Id,
                    Label = t.Name,
                    Blurb = (t.Blurb ?? "") + $" {last}: {Money(va, ci)} of value added, " +
                            $"{(gdpLast > 0 ? 100 * va / gdpLast : 0).ToString("0.0", ci)}% of GDP.",
                    Level = GraphLevel.Humans,
                    YearsAgo = 0.5,
                    EndYearsAgo = 0,
                    Y = mid,
                    Rho = now.Rho,
                    Ids = ids,
                    Tier = 1
                });

                AddLabel(ctx, new LabelSpec
                {
                    Text = (t.Name ?? t.Id).ToUpperInvariant() + "  <color=#" + UiHex(GraphStyle.TextDim) + ">" +
                           Money(va, ci) + "</color>",
                    Data = new Vector3(now.U, mid, now.Rho),
                    Priority = 40,
                    SizePx = 13,
                    Color = EconomyStyle.Level(t.Id == "gov" ? "gov" : "capital"),
                    PixelOffset = new Vector2(14, 0),
                    AnchorKey = "tier:" + t.Id,
                    Ids = ids
                });
            }

            // the whole wall now, and how small it was
            IdRange all = EconomyIds.Industries(0, inds.Count - 1);
            double real1950 = data.Real(data.Gdp.GrowthAt(1950), 1950), realLast = data.Real(gdpLast, last);
            Anchors.Register(new Anchor
            {
                Key = "wall:now",
                Label = "The economy, " + last.ToString(ci),
                Blurb = $"{Money(gdpLast, ci)} of value added in {last}: {(realLast / Math.Max(real1950, 1)).ToString("0.0", ci)} " +
                        $"times the {Money(real1950, ci)} of 1950 in today's dollars. The bright tops of the bands are what " +
                        "owners keep; the dim parts pay wages.",
                Level = GraphLevel.Humans,
                YearsAgo = 0.5,
                EndYearsAgo = 0,
                Y = now.Hi[inds.Count - 1],
                Rho = now.Rho,
                Ids = all,
                Tier = 1
            });

            Column c1950 = ColumnAt(wall, 1950);
            Anchors.Register(new Anchor
            {
                Key = "wall:1950",
                Label = "The economy, 1950",
                Blurb = $"{Money(data.Gdp.GrowthAt(1950), ci)} at the time ({Money(real1950, ci)} in {last} dollars). " +
                        $"Manufacturing was {Percent(data, "manufacturing", 1950, ci)} of it; in {last} it is " +
                        $"{Percent(data, "manufacturing", last, ci)}.",
                Level = GraphLevel.Humans,
                YearsAgo = ctx.NowYear - 1950,
                EndYearsAgo = ctx.NowYear - 1950,
                Y = c1950.Hi[inds.Count - 1],
                Rho = c1950.Rho,
                Ids = all,
                Tier = 2
            });
        }

        static Column ColumnAt(List<Column> wall, double year)
        {
            Column best = wall[0];
            foreach (Column c in wall)
            {
                if (Math.Abs(c.Year - year) < Math.Abs(best.Year - year)) best = c;
            }

            return best;
        }

        static string IndustryBlurb(EconomyData data, Industry ind, int year, CultureInfo ci)
        {
            double va = ind.ValueAdded.GrowthAt(year), gdp = data.Gdp.GrowthAt(year);
            StringBuilder s = new StringBuilder();
            if (!string.IsNullOrEmpty(ind.Blurb)) s.Append(ind.Blurb).Append(' ');
            s.Append(year.ToString(ci)).Append(": ").Append(Money(va, ci)).Append(" of value added (")
                .Append((gdp > 0 ? 100 * va / gdp : 0).ToString("0.0", ci)).Append("% of GDP; ")
                .Append(Percent(data, ind.Id, 1950, ci)).Append(" in 1950). Wages take ")
                .Append((100 * ind.CompShare).ToString("0", ci)).Append("%, owners keep ")
                .Append((100 * ind.OwnersShare).ToString("0", ci)).Append('%');
            if (ind.ProfitMargin > 0) s.Append("; typical net margin ").Append((100 * ind.ProfitMargin).ToString("0.#", ci)).Append('%');
            s.Append('.');
            if (ind.Companies != null && ind.Companies.Count > 0)
            {
                s.Append(" Largest: ");
                for (int k = 0; k < ind.Companies.Count && k < 3; k++)
                {
                    Company co = ind.Companies[k];
                    if (k > 0) s.Append(", ");
                    s.Append(co.Name);
                    if (co.MarketCap > 0) s.Append(" (").Append(Money(co.MarketCap, ci)).Append(')');
                }

                s.Append('.');
            }

            return s.ToString();
        }

        static string Percent(EconomyData data, string industryId, double year, CultureInfo ci)
        {
            Industry ind = data.IndustryById(industryId);
            double gdp = data.Gdp.GrowthAt(year);
            if (ind == null || gdp <= 0) return "?";
            return (100 * ind.ValueAdded.GrowthAt(year) / gdp).ToString("0.0", ci) + "%";
        }

        /// <summary>$B as "$480B" or "$2.9T".</summary>
        public static string Money(double billions, CultureInfo ci)
        {
            if (Math.Abs(billions) >= 1000) return "$" + (billions / 1000).ToString(billions >= 10000 ? "0.0" : "0.00", ci) + "T";
            if (Math.Abs(billions) >= 10) return "$" + billions.ToString("0", ci) + "B";
            return "$" + billions.ToString("0.0", ci) + "B";
        }

        static string UiHex(Color c) =>
            ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2") +
            ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2");

        void AddLabel(GraphContext ctx, LabelSpec spec)
        {
            labels.Add(spec);
            ctx.Labels.Add(spec);
        }

        static Color32 Tint(Color c, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255),
                (byte)(Mathf.Clamp01(alpha) * 255));

        // ------------------------------------------------------------------ upload

        public override void Upload(GraphContext ctx)
        {
            if (fills == null) return;
            fillMat = GraphMaterials.Surface(Color.white, 1f, EconomyStyle.QueueWall);
            fillMat.SetFloat("_EdgeSoft", 0f);
            lineMat = GraphMaterials.Line(Color.white, 1f, EconomyStyle.QueueWall + 1);
            AddMesh("IndustryWall", fills.ToMesh("IndustryWall"), fillMat);
            AddMesh("IndustryWallEdges", lines.ToMesh("IndustryWallEdges"), lineMat);
            fills = null;
            lines = null;
        }
    }
}
