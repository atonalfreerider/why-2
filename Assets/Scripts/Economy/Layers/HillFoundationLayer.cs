using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Social infrastructure: the government bedrock beneath the whole wave, with one pillar per program sized by
    /// its FY2025 outlays (circuit.json government; CBO / Treasury, federal fiscal year; state and local purchases from
    /// NIPA). Pillars rise under whom they serve: Social Security to the retirees, health programs under the health
    /// hill, defense under manufacturing, schools / police / roads under the valley floor, other federal programs to the
    /// lowest rungs, and interest on the public debt up to the clouds of diversified wealth that hold the bonds.
    /// Taxes rain down from every household into the bedrock; benefits rise to every household that receives them.
    /// Detail grows with distance (level of detail) and in the foundation view. Other years scale FY2025 by GDP.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillFoundationLayer : GraphLayer
    {
        public override int Order => 58;
        enum Part { Bedrock, Pillars, Benefits, Taxes, Selected, Count }
        static readonly Color Steel = new Color(.50f, .62f, .76f), Ice = new Color(.55f, .82f, 1), Gold = new Color(1, .68f, .22f), Blue = new Color(.30f, .70f, 1);
        /// <summary>One public program: its outlays ($B) and where it rises.</summary>
        public sealed class Pillar { public string Name, Note; public double Dollars; public Vector3 Base, Top; public Color Color; public readonly List<(int player, double dollars)> Serves = new List<(int, double)>(); }
        public static IReadOnlyList<Pillar> Pillars => pillars;
        static readonly List<Pillar> pillars = new List<Pillar>();
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int lod)> labels = new List<(TextMeshPro, int)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content;
        int selection = -2, lod;

        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || selection != EconomyState.SelectedPlayer) Build();
            if (content == null) return;
            string view = LandView.PresetId; bool hidden = view == "mind" || view == "section";
            content.SetActive(!hidden); if (hidden) return;
            lod = view == "foundation" ? 2 : rig.Pose.Distance < 35 ? 2 : rig.Pose.Distance < 100 ? 1 : 0;
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            foreach (var e in labels)
            {
                bool show = view == "foundation" || lod >= e.lod && view != "society" && view != "betrayal" && view != "capture";
                e.text.gameObject.SetActive(show); if (show) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
        }

        float Emphasis(Part part, string view)
        {
            if (view == "foundation") return part == Part.Bedrock ? .55f : 1;
            if (view == "society" || view == "betrayal") return .02f;
            switch (part)
            {
                case Part.Bedrock: return view == "roots" ? .25f : .14f;
                case Part.Pillars: return view == "overview" || view == "landscape" || view == "companies" ? .10f : view == "capture" ? .2f : .35f;
                case Part.Benefits: return view == "people" ? .12f : lod >= 2 ? .5f : .06f;
                case Part.Taxes: return lod >= 2 ? .45f : view == "rivers" ? .3f : .05f;
                case Part.Selected: return 1;
            }
            return 1;
        }

        void Build()
        {
            Clear(); snapshot = LandService.Current; land = HillLandscapeLayer.Current; selection = EconomyState.SelectedPlayer;
            if (land == null || snapshot?.Players?.Players == null) return;
            content = new GameObject("Social infrastructure"); content.transform.SetParent(transform, false);
            var data = LandService.Model.Data; var gov = data.Circuit; var players = snapshot.Players.Players;
            double scale = gov != null && gov.IncomeOf("gdp") > 0 ? snapshot.Land.Gdp / gov.IncomeOf("gdp") : 1;
            pillars.Clear();
            Vector3 Under(params string[] ids)
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (string id in ids) { var ind = data.IndustryById(id); if (ind == null) continue; sum += land.Hills[ind.Index].Center; n++; }
                return n > 0 ? sum / n : Vector3.zero;
            }
            Vector3 Of(Func<Player, bool> who)
            {
                Vector3 sum = Vector3.zero; int n = 0;
                for (int p = 0; p < players.Length; p++) if (who(players[p])) { sum += land.People[p]; n++; }
                return n > 0 ? sum / n : Vector3.zero;
            }
            double G(string key) => (gov?.GovernmentOf(key) ?? 0) * scale;
            var ss = Add("SOCIAL SECURITY", "retirement and disability pensions, federal", G("socialSecurity"), Of(p => p.Group == Group.RetiredSocialSecurity || p.Group == Group.RetiredSavings), Ice);
            var health = Add("MEDICARE + MEDICAID", "federal health programs; hospitals and insurers are paid", G("health"), Under("health"), Ice);
            Add("DEFENSE", "military, federal", G("defense"), Under("manufacturing", "hardware"), Steel);
            var local = Add("SCHOOLS · POLICE · ROADS · COURTS", "state and local purchases", G("stateLocalPurchases"), Of(p => p.Rung <= 1 && p.Group != Group.Top1), Blue);
            var other = Add("OTHER FEDERAL", "agencies, food and income support, veterans", G("otherOutlays"), Of(p => p.Group == Group.OutOfWork || p.Group == Group.WorkingPoor), Steel);
            var bonds = Add("INTEREST TO BONDHOLDERS", "net interest on the federal debt: taxes paid to lenders", G("netInterest"), Of(p => p.Group == Group.Top1), Gold);
            if (bonds.Top != Vector3.zero) bonds.Top.y = land.CloudY - 4;
            // Who each pillar serves: Social Security by the players' Social Security, the health and safety-net pillars by
            // the rest of their benefits, the bondholders by capital income at the top (a proxy for Treasury holdings).
            double topCapital = 0; foreach (var p in players) if (p.Group == Group.Top1) topCapital += Math.Max(0, p.Capital);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i]; double otherBenefits = Math.Max(0, p.Transfers - p.SocialSecurity);
                if (p.SocialSecurity > .05) ss.Serves.Add((i, p.SocialSecurity));
                if (otherBenefits > .05) { health.Serves.Add((i, otherBenefits * .6)); other.Serves.Add((i, otherBenefits * .4)); }
                if (p.Rung <= 1 && p.Group != Group.Top1) local.Serves.Add((i, 0));
                if (p.Group == Group.Top1 && topCapital > 0) bonds.Serves.Add((i, bonds.Dollars * Math.Max(0, p.Capital) / topCapital));
            }
            Bedrock(gov, scale); Columns(); Benefits(); Taxes(); Selected();
        }

        Pillar Add(string name, string note, double dollars, Vector3 under, Color color)
        {
            int k = pillars.Count;
            // The pillars stand in a row in the bedrock just behind the crest and lean toward whom they serve.
            var pillar = new Pillar { Name = name, Note = note, Dollars = dollars, Color = color,
                Base = new Vector3(HillLandscape.FootX + 8 + k * land.CrestWidth / 6.5f, HillLandscape.Bedrock, land.CrestZ - 26 - (k % 2) * 12) };
            pillar.Top = under == Vector3.zero ? pillar.Base + Vector3.up * 14 : new Vector3(under.x, land.Ground(under.x, under.z) - 1.2f, under.z);
            pillars.Add(pillar); return pillar;
        }

        void Bedrock(Why.Economy.Data.CircuitFile gov, double scale)
        {
            var b = new LineMeshBuilder();
            // The bedrock runs under the whole wave, from the first year to the crest, as wide as the crest.
            float x0 = land.XMin, x1 = land.XMax, z0 = land.TailZ, z1 = land.CrestZ;
            for (float x = x0; x <= x1; x += 8) b.AddSegment(new Vector3(x, HillLandscape.Bedrock, z0), new Vector3(x, HillLandscape.Bedrock, z1), WithAlpha(Steel, .5f), .7f, 0, 0, 1);
            for (float z = z1; z >= z0; z -= 8) b.AddSegment(new Vector3(x0, HillLandscape.Bedrock, z), new Vector3(x1, HillLandscape.Bedrock, z), WithAlpha(Steel, .5f), .7f, 0, 0, 1);
            Lines(b, Part.Bedrock, false, false);
            double receipts = (gov?.GovernmentOf("receipts") ?? 0) * scale, deficit = (gov?.GovernmentOf("deficit") ?? 0) * scale;
            double corp = (gov?.GovernmentOf("corporateTax") ?? 0) * scale, dividends = (gov?.IncomeOf("dividends") ?? 0) * scale, buybacks = (gov?.IncomeOf("buybacks") ?? 0) * scale;
            string year = snapshot.Year == (gov?.Year ?? 2025) ? "FY" + snapshot.Year : snapshot.Year + " (FY2025 scaled by GDP)";
            Label("<b>SOCIAL INFRASTRUCTURE</b> · " + year + "\nfederal taxes in " + LandFacts.Money(receipts) + " · borrowed " + LandFacts.Money(deficit) +
                "\ncorporate tax " + LandFacts.Money(corp) + " vs dividends " + LandFacts.Money(dividends) + " + buybacks " + LandFacts.Money(buybacks),
                new Vector3(land.XMin + 10, HillLandscape.Bedrock + 2.5f, land.CrestZ + 6), .3f, new Color(.75f, .85f, 1), 0);
        }

        void Columns()
        {
            var b = new LineMeshBuilder();
            foreach (var p in pillars)
            {
                float r = .5f + Mathf.Sqrt((float)Math.Max(0, p.Dollars)) * .045f;
                Vector3 bend = new Vector3(p.Top.x, Mathf.Lerp(p.Base.y, p.Top.y, .55f), p.Top.z);
                var spine = new List<LinePoint>();
                for (int s = 0; s <= 30; s++)
                {
                    float t = s / 30f; Vector3 q = Vector3.Lerp(Vector3.Lerp(p.Base, bend, t), Vector3.Lerp(bend, p.Top, t), t);
                    spine.Add(new LinePoint(q, p.Color, 2.2f, 0, 2.4f));
                    if (s % 3 == 0) Circle(b, q, r * (1 - .35f * t), WithAlpha(p.Color, .75f), 1.2f, 1.8f);
                }
                b.AddFlowPath(spine, 0, .5f);
                Circle(b, p.Base, r * 1.4f, p.Color, 2, 2.6f);
                Label("<b>" + p.Name + "</b> " + LandFacts.Money(p.Dollars) + "\n" + p.Note, p.Base + Vector3.up * 2.4f + Vector3.back * (r + 1), .22f, Color.Lerp(p.Color, Color.white, .35f), 1);
            }
            var renderer = Lines(b, Part.Pillars, true, true); renderer.sharedMaterial.SetFloat("_FlowFreq", 1.2f); renderer.sharedMaterial.SetFloat("_FlowSpeed", .45f);
        }

        void Benefits()
        {
            var b = new LineMeshBuilder();
            foreach (var p in pillars)
                foreach (var serve in p.Serves)
                {
                    Vector3 to = land.People[serve.player] + Vector3.up * .3f;
                    float width = serve.dollars > 0 ? .5f + Mathf.Sqrt((float)serve.dollars) * .3f : .5f;
                    Arch(b, p.Top, to, WithAlpha(p.Color, .7f), width, 1.8f, 1 + Vector3.Distance(p.Top, to) * .05f);
                }
            Lines(b, Part.Benefits, true, true);
        }

        void Taxes()
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].Taxes <= .05) continue;
                Vector3 from = land.People[i]; var drop = new LinePoint[10];
                for (int s = 0; s < drop.Length; s++) drop[s] = new LinePoint(Vector3.Lerp(from, new Vector3(from.x, HillLandscape.Bedrock, from.z), s / 9f), WithAlpha(Steel, .55f), .4f + Mathf.Sqrt((float)players[i].Taxes) * .22f, 0, 1.4f);
                b.AddFlowPath(drop, 0, .4f, i * .17f);
            }
            Lines(b, Part.Taxes, true, true);
        }

        void Selected()
        {
            int i = EconomyState.SelectedPlayer; var players = snapshot.Players.Players;
            if (i < 0 || i >= players.Length) return;
            var b = new LineMeshBuilder(); Vector3 at = land.People[i];
            if (players[i].Taxes > .01) b.AddFlowPath(new[] { new LinePoint(at, Steel, 2.5f, 0, 3), new LinePoint(new Vector3(at.x, HillLandscape.Bedrock, at.z), Steel, 2.5f, 0, 3) }, 0, .4f);
            foreach (var p in pillars) foreach (var s in p.Serves) if (s.player == i && s.dollars > 0) Arch(b, p.Top, at + Vector3.up * .3f, p.Color, 2.5f, 4, 2);
            Lines(b, Part.Selected, true, true);
        }

        static void Arch(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[25];
            for (int s = 0; s < pts.Length; s++) { float t = s / 24f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .5f);
        }

        static void Circle(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[49];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 48; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Foundation " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3190, additive, 0, flow ? 1 : 0)); owned.Add(material);
            var r = AddMesh("Foundation " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Label(string text, Vector3 at, float size, Color color, int minLod)
        {
            var go = new GameObject("Foundation label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(40, 8);
            go.transform.localScale = Vector3.one * size; labels.Add((t, minLod));
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() { Clear(); pillars.Clear(); }
    }
}
