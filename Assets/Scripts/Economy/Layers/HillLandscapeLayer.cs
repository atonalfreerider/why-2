using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>Hills, crowns, portfolio clouds, household glyphs and directed money rivers.
    /// Uses the existing census, income-output accounts and social seasons without changing the original scene.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillLandscapeLayer : GraphLayer
    {
        public override int Order => 55;
        public override IEnumerable<string> RequiredTexts => new[] { "Data/economy/hill-owners" };
        public static HillLandscape Current { get; private set; }
        /// <summary>Where the separate 3D decision program stands: beside the road, ahead of the present.</summary>
        public static readonly Vector3 MindOrigin = new Vector3(-70, 4, 40);
        public static SocialSeasonResult Shown { get; private set; }
        public static int HoverIndustry { get; private set; } = -1;
        public static int HoverPlayer { get; private set; } = -1;
        public static OwnerRecord HoverOwner { get; private set; }
        public static OwnerRecord[] OwnerEvidence { get; private set; }=Array.Empty<OwnerRecord>();
        public sealed class OwnerRecord { public string name, company, industry, note, source; public int asOfYear; }
        sealed class OwnerFile { public OwnerRecord[] owners; }
        readonly List<(MeshRenderer renderer, LandGroup group)> renderers = new List<(MeshRenderer, LandGroup)>();
        readonly List<(TextMeshPro text, LandGroup group)> labels = new List<(TextMeshPro, LandGroup)>();
        readonly List<(Vector3 at, OwnerRecord owner)> crowns = new List<(Vector3, OwnerRecord)>();
        readonly List<Mesh> meshes = new List<Mesh>();
        OwnerRecord[] owners = Array.Empty<OwnerRecord>();
        LandSnapshot snapshot;
        GameObject content;
        int selected = -2, roundVersion = -1;
        int programVersion = -1, mindPerson=-2;
        float lastClick=-10;int lastPlayer=-1;
        Vector2 pressed;
        MeshRenderer socialRenderer;
        SocialSeasonResult userSeason;
        int hoverTie=-1;
        LandFrame frame;
        static readonly Color Gold = new Color(1, .73f, .22f), Blue = new Color(.36f, .72f, 1),
            Rose = new Color(1, .36f, .52f), Ice = new Color(.6f, .86f, 1), Steel = new Color(.36f, .44f, .52f);

        public override void Prepare(GraphContext ctx)
        {
            snapshot = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            var file = JsonConvert.DeserializeObject<OwnerFile>(ctx.Text("Data/economy/hill-owners") ?? "{}");
            owners = file?.owners ?? Array.Empty<OwnerRecord>();OwnerEvidence=owners;
        }
        public override void Upload(GraphContext ctx)
        {
            frame = EconomyStage.Land(); frame.Place(transform);
            Rebuild(snapshot);
            LandService.Changed += Rebuild;
        }
        void Rebuild(LandSnapshot next)
        {
            if(next!=snapshot)userSeason=null;
            Clear(); snapshot = next;
            if (next?.Players?.Players == null) return;
            Current = new HillLandscape(next);
            content = new GameObject("Hill landscape"); content.transform.SetParent(transform, false);
            Terrain(); Owners(); Inhabitants(); Money(); Mind(); Social();
            selected = EconomyState.SelectedPlayer;
            LandService.ReportReady(nameof(HillLandscapeLayer), next.Version);
            Debug.Log("[Why] Hills " + next.Year + ": " + Current.Hills.Length + " industries; " +
                Current.People.Length + " players; crest " + Current.Year + " at z " + Current.CrestZ.ToString("F1", LandFacts.Ci) +
                ", value width " + Current.CrestValueWidth.ToString("F1", LandFacts.Ci) + " (bands " + Current.CrestBands().ToString("F1", LandFacts.Ci) + "); named ownership rings " + crowns.Count + "; group portfolios and psychological motives are model assumptions.");
        }
        void Terrain()
        {
            // The wave's terrain (strata, ridges along time, the crest's cut face) is drawn by HillWaveLayer; this layer
            // names the hills at the crest and runs the measured input-output purchases underground.
            foreach(var h in Current.Hills)
            {
                var account=snapshot.Land.Sectors[h.Industry];
                if(h.Tier>0&&account.ValueAdded/snapshot.Land.Gdp>.035)
                    Label(LandService.Model.Data.Industries[h.Industry].Name,h.Summit+Vector3.up*.6f,LandGroup.Sectors,.23f,Color.white);
            }
            Strata();
            var roots=new LineMeshBuilder();var io=LandService.Model.Data.Circuit?.Io;
            if(io!=null)foreach(var edge in io.Flows())
            {
                var a=LandService.Model.Data.IndustryById(edge.from);var b=LandService.Model.Data.IndustryById(edge.to);
                if(a==null||b==null||a.Index==b.Index||edge.value<100)continue;
                Underground(roots,Current.Hills[a.Index].Foot,Current.Hills[b.Index].Foot,WithAlpha(Blue,.32f),.8f+(float)System.Math.Sqrt(edge.value)*.015f);
            }
            Lines(roots,LandGroup.Roots,true);
        }
        /// <summary>Class is altitude: a legend of the rungs up the flank of the most populated hill.</summary>
        void Strata()
        {
            var count=new int[Current.Hills.Length];foreach(int h in Current.IndustryOf)count[h]++;
            int best=0;for(int h=1;h<count.Length;h++)if(Current.Hills[h].Tier>0&&count[h]>count[best])best=h;
            var hill=Current.Hills[best];var b=new LineMeshBuilder();
            string[] names={"VALLEY FLOOR · out of work","LOWER VALLEY · working poor, students","FOOTHILLS · frontline, gig, Social Security retirees",
                "SLOPES · office, public servants, comfortable retirees","UPPER SLOPES · professional-managerial class","UNDER THE SUMMIT · business owners"};
            for(int r=0;r<names.Length;r++)
            {
                var ring=new Vector3[41];
                for(int j=0;j<ring.Length;j++)ring[j]=Current.At(hill,HillLandscape.Valley+Mathf.PI*.5f*j/40f,HillLandscape.RungRadius[r],.12f);
                b.AddPolyline(ring,WithAlpha(r>=4?Gold:Blue,.55f),1.1f,0,0,1.4f);
                Label(names[r],Current.At(hill,HillLandscape.Valley,HillLandscape.RungRadius[r],1.2f)+Vector3.left*3,LandGroup.Glyphs,.17f,r>=4?Gold:Ice);
            }
            Label("CLOUDS · the top 1%: claims on every hill",Current.CloudCenter+new Vector3(0,4,-6),LandGroup.Glyphs,.25f,Gold);
            Lines(b,LandGroup.Glyphs);
        }
        void Owners()
        {
            // Hilltop halos are drawn by HillCaptureLayer, sized by the owners' surplus households' spending generates.
            var architecture=new LineMeshBuilder();var glow=new LineMeshBuilder();
            int seat=0;
            foreach(OwnerRecord owner in owners)
            {
                if(snapshot.Year<owner.asOfYear)continue;
                var industry=LandService.Model.Data.IndustryById(owner.industry);if(industry==null)continue;
                Vector3 at=Current.Hills[industry.Index].Surface((seat++%2==0?0:Mathf.PI),.30f)+Vector3.up*.15f;
                crowns.Add((at,owner));
                // The crown is an atmospheric ring of concentrated claims, never a literal royal object.
                for(int band=0;band<3;band++)
                {
                    var ring=new Vector3[49];
                    for(int j=0;j<ring.Length;j++){float a=j*Mathf.PI/24;ring[j]=at+new Vector3(Mathf.Cos(a)*(.9f+band*.18f),.4f+band*.12f,Mathf.Sin(a)*(.9f+band*.18f));}
                    glow.AddPolyline(ring,WithAlpha(Gold,.9f-band*.25f),2.2f-band*.5f,0,0,4-band);
                }
                Label(owner.name+" · voting control of "+owner.company,at+Vector3.up*1.6f,LandGroup.Crown,.20f,Gold);
            }
            Lines(architecture,LandGroup.Towers);
            var light=Lines(glow,LandGroup.Crown,true);light.sharedMaterial.SetColor("_Color",new Color(3,2.6f,1.8f,1));
        }
        void Underground(LineMeshBuilder b,Vector3 a,Vector3 end,Color color,float width)
        {
            var points=new LinePoint[49];
            for(int k=0;k<points.Length;k++)
            {
                float t=k/48f;Vector3 v=Vector3.Lerp(a,end,t);
                v.y=Mathf.Min(v.y-8*Mathf.Sin(t*Mathf.PI),Current.Ground(v.x,v.z)-6);
                points[k]=new LinePoint(v,color,width);
            }
            b.AddFlowPath(points,0,1);
        }
        void GroundFlow(LineMeshBuilder builder,Vector3 a,Vector3 b,Color color,float width)
        {
            var points=new LinePoint[65];
            for(int k=0;k<points.Length;k++)
            {
                float t=k/64f;Vector3 p=Vector3.Lerp(a,b,t);
                p.x+=Mathf.Sin(t*Mathf.PI*2)*.5f*Mathf.Sin(t*Mathf.PI);
                p.y+=Mathf.Sin(t*Mathf.PI)*.45f;points[k]=new LinePoint(p,color,width);
            }
            builder.AddFlowPath(points,0,.3f);Arrow(builder,points[59].Data,points[61].Data,color);
        }
        void Inhabitants()
        {
            var people = new LineMeshBuilder(); var clouds = new LineMeshBuilder(); var kin = new LineMeshBuilder();
            var cloudFill = new SurfaceMeshBuilder();
            var players = snapshot.Players.Players;
            for (int i = 0; i < players.Length; i++)
            {
                Player p = players[i]; Vector3 at = Current.People[i];
                Color color = Current.Cloud[i] || p.Group == Group.Owners ? Gold : Blue;
                Ring(people, at, .17f + .025f * Mathf.Sqrt(p.Adults.Length), color,1.4f);
                people.AddSegment(at,at+Vector3.up*.65f,color,1.5f,0,0);
                Ring(people,at+Vector3.up*.78f,.12f,color,1.3f,true);
                for(int a=0;a<p.Adults.Length;a++)
                {
                    float phase=a*2.399963f,rad=.06f*Mathf.Sqrt(a+1);
                    Vector3 dot=at+new Vector3(Mathf.Cos(phase)*rad,.05f,Mathf.Sin(phase)*rad);
                    people.AddSegment(dot,dot+Vector3.up*.07f,WithAlpha(color,.75f),1.2f,0,0);
                }
                // Small satellites are individual representative lifelines, each standing for 100,000 people.
                for (int c = 0; c < p.Children.Length; c++)
                {
                    float a = c * 2.399963f; float r = .06f * Mathf.Sqrt(c + 1);
                    Vector3 child = at + new Vector3(Mathf.Cos(a)*r,.01f,Mathf.Sin(a)*r);
                    kin.AddSegment(at,child,WithAlpha(Blue,.45f),.65f,0,0);
                    Ring(kin,child,.035f,Blue,.8f);
                }
                if (Current.Cloud[i])
                {
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 center=at+new Vector3((c-1.5f)*.9f,-.10f,Mathf.Sin(c)*.6f);
                        var inner=new Vector3[49];var edge=new Vector3[49];
                        for(int j=0;j<=48;j++){float a=j*Mathf.PI/24;inner[j]=center;edge[j]=center+new Vector3(Mathf.Cos(a)*1.3f,0,Mathf.Sin(a)*.9f);}
                        cloudFill.AddBand(inner,edge,new[]{(Color32)WithAlpha(Gold,.22f)},new[]{(Color32)WithAlpha(Gold,0)},0,2);
                        if(c==1)clouds.AddPolyline(edge,WithAlpha(Gold,.32f),1.2f,0,0);
                    }
                    // All-sector ownership mix: the proxy comes from distributional capital-income weights.
                    double[][] mix = CapitalSources.Mix(LandService.Model.Data, snapshot.Year);
                    for (int h = 0; h < Current.Hills.Length; h++)
                        if (mix[3][h] > .07) Flow(clouds,Current.Hills[h].Summit,at,WithAlpha(Gold,.08f),.8f,2);
                }
            }
            Lines(clouds,LandGroup.Crown,true);
            Surface(cloudFill,LandGroup.Crown);
            FamilyFlows();
        }
        void FamilyFlows()
        {
            var links = new LineMeshBuilder(); var sim = LandService.Population?.Sim;
            if (sim == null) return;
            var lives = LandService.Model.Lives;
            foreach (var child in sim.People)
            {
                if (!lives.TryGet(child.Index,snapshot.Year,out PersonYear r)) continue;
                int target = snapshot.Players.PlayerOfPerson[child.Index];
                if (target < 0) continue;
                int parent = child.Mother >= 0 ? child.Mother : child.Father;
                if (parent < 0) continue;
                int source = snapshot.Players.PlayerOfPerson[parent];
                if (r.FamilyDependent && r.SupportParent >= 0) source=snapshot.Players.PlayerOfPerson[r.SupportParent];
                if (EconomyState.SelectedPlayer < 0 || source != EconomyState.SelectedPlayer && target != EconomyState.SelectedPlayer) continue;
                if (source >= 0 && source != target && r.FamilyDependent)
                    Flow(links,Current.People[source],Current.People[target],WithAlpha(Blue,.22f),.7f,.35f);
                if (r.Inherited > 0)
                {
                    // The lives record aggregates spouse and parent estates: don't invent a specific donor.
                    Vector3 a = Current.People[target]+Vector3.up*2;
                    Flow(links,a,Current.People[target],WithAlpha(Gold,.8f),1.2f,.4f);
                }
            }
            Lines(links,LandGroup.Dots,true);
        }
        void Money()
        {
            // Investment: the owners' pool in the clouds sends part of its surplus back down to the hills as new capital.
            // Household rivers (HillActivityLayer) and the split of value at each market (HillCaptureLayer) are drawn there.
            var capital = new LineMeshBuilder(); var pools = new LineMeshBuilder();
            Vector3 cloud = Current.CloudCenter;
            foreach (IndustryHill hill in Current.Hills)
            {
                double investment = snapshot.Money.InvestmentBySector[hill.Industry];
                if (investment <= 0 || hill.Tier == 0) continue;
                Vector3 halo = Current.Halo(hill.Industry);
                var points = new LinePoint[25];
                for (int k = 0; k < points.Length; k++)
                {
                    float t = k / 24f; Vector3 p = Vector3.Lerp(cloud, halo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 6;
                    points[k] = new LinePoint(p, WithAlpha(Gold, .5f), .8f + (float)Math.Sqrt(investment) * .06f, 0, 1.6f);
                }
                capital.AddFlowPath(points, 0, .4f);
            }
            Ring(pools, cloud, 6, WithAlpha(Gold, .35f), 1.4f);
            Label("INVESTMENT\nowners' saving sent back down as new capital", cloud + Vector3.up * 5, LandGroup.CapitalFlows, .25f, Gold);
            Lines(capital,LandGroup.CapitalFlows,true); Lines(pools,LandGroup.Pools);
        }
        void Mind()
        {
            var lines = new LineMeshBuilder();
            int index = Mathf.Clamp(EconomyState.SelectedPlayer,0,snapshot.Players.Players.Length-1);
            Player p = snapshot.Players.Players[index]; Vector3 origin = MindOrigin;
            var scenario=PlayerMindProgram.Active;
            var state=scenario!=null&&scenario.Players.Length>index?scenario.Players[index]:null;
            programVersion=scenario?.Revision??-1;mindPerson=EconomyState.Person;
            bool individual=EconomyState.Person>=0&&LandService.Model.Lives.TryGet(EconomyState.Person,snapshot.Year,out _);
            PersonYear person=default;
            if(individual){LandService.Model.Lives.TryGet(EconomyState.Person,snapshot.Year,out person);state=new PlayerMindProgram.State{Assets=Math.Max(0,person.Wealth+person.Debt)/1e9,Debt=person.Debt/1e9,Reason=person.Reason,Fear=person.FearShare,Fantasy=person.Fantasy};}
            Vector3[] nodes = { new Vector3(-2,-.5f,-3),new Vector3(-4,1,0),new Vector3(4,1,0),
                new Vector3(0,4,0),new Vector3(0,1,1),new Vector3(3,-1,3) };
            string[] names = { "MEMORY\nAssets · debt · inheritance", "DESIRE\nComfort · belonging", "FEAR\nLoss · insecurity",
                "DELIBERATION\nFuture " + (individual?person.Future:p.Future).ToString("P0") + " · reason " + (state?.Reason??p.Reason).ToString("P0"),
                "CHOICE\nSpend · save · borrow", "FEEDBACK\nNext year's resources" };
            Color[] colors = { Gold,Rose,Ice,Color.white,Blue,Gold };
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 at = origin+nodes[i];
                for (int j = 0; j < 3; j++) Ring(lines,at+Vector3.up*(j-1)*.16f,.47f,WithAlpha(colors[i],.7f),1.6f,j==1);
                Label(names[i],at+Vector3.up*.72f,LandGroup.Mirages,.09f,colors[i]);
            }
            int[,] edges = { {0,1},{0,2},{1,3},{2,3},{1,4},{2,4},{3,4},{4,5},{5,0} };
            for (int e = 0; e < edges.GetLength(0); e++)
            {
                int a=edges[e,0],b=edges[e,1];float fear=state?.Fear??p.Fear,reason=state?.Reason??p.Reason;
                float weight=a==1?1-fear:a==2?fear:a==3?reason:.5f;
                Flow(lines,origin+nodes[a],origin+nodes[b],colors[a],.6f+weight*3,.4f);
            }
            // A spatial envelope gives the program a volume, without claiming neuroanatomical accuracy.
            for (int j=0;j<9;j++) Ring(lines,origin+new Vector3(0,1.4f,(j-4)*.48f),3.6f*Mathf.Sqrt(1-Mathf.Pow((j-4)/5f,2)),WithAlpha(Steel,.23f),.65f,true);
            Label((individual?"Synthetic record #"+EconomyState.Person:PlayerTitle(p)) + " · " + snapshot.Year,origin+new Vector3(0,5.7f,0),LandGroup.Mirages,.13f,Color.white);
            Label("Behavioral assumptions · conceptual decision program",origin+new Vector3(0,-2,-4),LandGroup.Mirages,.08f,Steel);
            if(state!=null)Label("Fear "+state.Fear.ToString("P0")+" · assets "+(individual?"$"+Math.Max(0,person.Wealth+person.Debt).ToString("N0"):LandFacts.Money(state.Assets))+" · debt "+(individual?"$"+person.Debt.ToString("N0"):LandFacts.Money(state.Debt))+
                "\n"+(individual?"Historical simulated individual":scenario.Steps>0?"Scenario "+scenario.Year:"Baseline"),origin+new Vector3(0,-2.8f,-4),LandGroup.Mirages,.08f,Gold);
            Lines(lines,LandGroup.Mirages,true);
        }
        void Social()
        {
            if (socialRenderer)
            { Mesh m=socialRenderer.GetComponent<MeshFilter>().sharedMesh; meshes.Remove(m); Destroy(m); Destroy(socialRenderer.sharedMaterial); Destroy(socialRenderer.gameObject); }
            var lines = new LineMeshBuilder();
            var season = userSeason ?? (LandView.ShowBetrayal ? LandService.Betrayal(false) ?? snapshot.Society : snapshot.Society);
            Shown=season;
            if (season == null) return;
            int round = Mathf.Clamp(LandView.Round,0,season.Rounds);
            for (int e=0;e<season.PairA.Length;e++)
            {
                int a=season.PairA[e],b=season.PairB[e];
                if (EconomyState.SelectedPlayer >= 0 && a != EconomyState.SelectedPlayer && b != EconomyState.SelectedPlayer) continue;
                if (season.Exposure[round][e] < .08f) continue;
                float c=(season.CoopAB[round][e]+season.CoopBA[round][e])*.5f;
                Flow(lines,Current.People[a]+Vector3.up*.4f,Current.People[b]+Vector3.up*.4f,
                    WithAlpha(Color.Lerp(Rose,Blue,c),.4f),1.1f,.7f);
            }
            socialRenderer=Lines(lines,LandGroup.Ties,true,false);
            roundVersion=LandView.RoundVersion;
        }
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (Current == null || snapshot == null) return;
            if (mindPerson != EconomyState.Person || selected != EconomyState.SelectedPlayer || programVersion != (PlayerMindProgram.Active?.Revision??-1)) { Rebuild(snapshot); return; }
            if (roundVersion != LandView.RoundVersion) Social();
            int incident=EconomyState.TakeIncident();
            if(incident>=0&&incident<snapshot.Society.PairA.Length)
            {
                userSeason=SocialSeason.Run(LandService.Model.Data,snapshot.Land,snapshot.Players,EconomyState.Social,snapshot.Year,
                    new Incident{Round=Mathf.Min(snapshot.Society.Rounds,LandView.Round+1),From=snapshot.Society.PairA[incident],To=snapshot.Society.PairB[incident]});
                Social();
                if(LandView.Round>=snapshot.Society.Rounds)LandView.Replay();else LandView.Play(true);
            }
            bool mind=LandView.PresetId=="mind", social=LandView.PresetId=="society"||LandView.PresetId=="betrayal";
            foreach (var entry in renderers)
            {
                if (!entry.renderer) continue;
                float alpha=entry.group==LandGroup.Mirages ? (mind?1:0) : mind ? .035f : Emphasis(entry.group);
                GraphMaterials.SetAlpha(entry.renderer.sharedMaterial,alpha);
            }
            if (socialRenderer) GraphMaterials.SetAlpha(socialRenderer.sharedMaterial,social?1:0);
            foreach (var entry in labels)
            {
                string view=LandView.PresetId;
                bool show=entry.group==LandGroup.Mirages?mind:!mind && !social && (entry.group==LandGroup.Crown ? view=="capture"
                    : entry.group==LandGroup.Glyphs ? view=="people" : entry.group==LandGroup.CapitalFlows ? view=="rivers"
                    : entry.text.text=="GOVERNMENT" || view=="landscape" || view=="capture");
                entry.text.gameObject.SetActive(show);
                if (show) entry.text.transform.rotation=rig.Cam.transform.rotation;
            }
            Pick(rig.Cam,mind);
        }
        float Emphasis(LandGroup g)
        {
            string view=LandView.PresetId;
            if (g==LandGroup.Ties) return view=="society"||view=="betrayal"?1:0;
            if (g==LandGroup.Terraces) return .22f;
            if (g==LandGroup.Sectors) return .20f;
            if (g==LandGroup.Towers) return view=="capture"?.55f:.12f;
            if (g==LandGroup.Crown) return view=="capture"?1:view=="people"?.35f:.10f;
            if (g==LandGroup.Roots) return view=="roots"?.75f:.055f;
            if (g==LandGroup.Glyphs) return view=="people"?.9f:0;
            if (g==LandGroup.CapitalFlows||g==LandGroup.Pools) return view=="rivers"?.6f:view=="capture"?.2f:0;
            if (g==LandGroup.Rivers||g==LandGroup.Income) return view=="rivers"?.20f:0;
            return 1;
        }
        void Pick(Camera camera,bool mind)
        {
            HoverIndustry=HoverPlayer=hoverTie=-1; HoverOwner=null;
            var mouse=Mouse.current;
            if(mouse==null||mind||UI.HillExplorer.Depth>=3||EventSystem.current&&EventSystem.current.IsPointerOverGameObject())return;
            Vector2 pointer=mouse.position.ReadValue(); float best=18*Screen.height/1080f;
            var moving=HillActivityLayer.Positions;
            for(int i=0;i<(moving?.Length??Current.People.Length);i++)
            {
                Vector3 screen=camera.WorldToScreenPoint(frame.World((moving!=null?moving[i]:Current.People[i])+Vector3.up*.8f));
                float d=Vector2.Distance(pointer,screen);
                if(screen.z>0&&d<best){best=d;HoverPlayer=moving!=null?i/HillActivityLayer.Subgroups:i;}
            }
            if(HoverPlayer<0)foreach(var crown in crowns)
            {
                Vector3 screen=camera.WorldToScreenPoint(frame.World(crown.at+Vector3.up*.2f));
                if(screen.z>0&&Vector2.Distance(pointer,screen)<24)HoverOwner=crown.owner;
            }
            if(HoverPlayer<0&&HoverOwner==null)for(int i=0;i<Current.Hills.Length;i++)
            {
                Vector3 screen=camera.WorldToScreenPoint(frame.World(Current.Hills[i].Summit));
                float d=Vector2.Distance(pointer,screen);
                if(screen.z>0&&d<35){HoverIndustry=i;break;}
            }
            if(HoverPlayer<0&&(LandView.PresetId=="society"||LandView.PresetId=="betrayal"))
            {
                var season=userSeason??snapshot.Society;float closest=9;
                for(int e=0;e<season.PairA.Length;e++)
                {
                    int a=season.PairA[e],b=season.PairB[e];
                    if(EconomyState.SelectedPlayer>=0&&a!=EconomyState.SelectedPlayer&&b!=EconomyState.SelectedPlayer)continue;
                    for(int j=1;j<10;j++)
                    {
                        float t=j/10f;Vector3 point=Vector3.Lerp(Current.People[a],Current.People[b],t)+Vector3.up*(.4f+.7f*Mathf.Sin(t*Mathf.PI));
                        Vector3 screen=camera.WorldToScreenPoint(frame.World(point));float distance=Vector2.Distance(pointer,screen);
                        if(screen.z>0&&distance<closest){closest=distance;hoverTie=e;}
                    }
                }
            }
            if(mouse.leftButton.wasPressedThisFrame)pressed=pointer;
            if(mouse.leftButton.wasReleasedThisFrame&&Vector2.Distance(pressed,pointer)<5&&HoverPlayer>=0)
                {
                EconomyState.SetSelection(HoverPlayer,-1,-1);
                if(lastPlayer==HoverPlayer&&Time.unscaledTime-lastClick<.35f)UI.HillExplorer.Instance?.EnterGroup(HoverPlayer);
                lastPlayer=HoverPlayer;lastClick=Time.unscaledTime;
            }
            else if(mouse.leftButton.wasReleasedThisFrame&&Vector2.Distance(pressed,pointer)<5&&hoverTie>=0)
                EconomyState.SetSelection(EconomyState.SelectedPlayer,-1,hoverTie);
            else if(mouse.leftButton.wasReleasedThisFrame&&Vector2.Distance(pressed,pointer)<5&&HillWaveLayer.HoverCompany<0)
            {
                int firm=-1;float closest=22;
                for(int f=0;f<snapshot.Land.Towers.Length;f++){Vector3 screen=camera.WorldToScreenPoint(frame.World(HillActivityLayer.CompanyHalo(f)));float d=Vector2.Distance(pointer,screen);if(screen.z>0&&d<closest){closest=d;firm=f;}}
                if(firm>=0)UI.HillExplorer.Instance?.EnterCompany(firm);
                else if(HoverIndustry>=0)UI.HillExplorer.Instance?.EnterSector(HoverIndustry);
            }
        }
        MeshRenderer Lines(LineMeshBuilder b,LandGroup group,bool flow=false,bool track=true)
        {
            Mesh mesh=b.ToMesh("Hills "+group); meshes.Add(mesh);
            Material material=GraphMaterials.Raw(GraphMaterials.Line(Color.white,1,3205,false,0,flow?1:0));
            if(flow){material.SetFloat("_FlowFreq",2);material.SetFloat("_FlowSpeed",.5f);}
            var r=AddMesh("Hills "+group,mesh,material); r.transform.SetParent(content.transform,false);
            if(track)renderers.Add((r,group)); return r;
        }
        void Surface(SurfaceMeshBuilder b,LandGroup group)
        {
            Mesh mesh=b.ToMesh("Hill surfaces");meshes.Add(mesh);
            var r=AddMesh("Hill surfaces",mesh,GraphMaterials.Raw(GraphMaterials.Surface(Color.white,1,3195)));
            r.transform.SetParent(content.transform,false);renderers.Add((r,group));
        }
        void Label(string title,Vector3 at,LandGroup group,float size,Color color)
        {
            var go=new GameObject(title);go.transform.SetParent(content.transform,false);go.transform.localPosition=at;
            var text=go.AddComponent<TextMeshPro>();text.text=title;text.fontSize=24; text.alignment=TextAlignmentOptions.Center;
            text.color=color;text.textWrappingMode=TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta=new Vector2(22,4);go.transform.localScale=Vector3.one*size;labels.Add((text,group));
        }
        static Color WithAlpha(Color c,float a){c.a=a;return c;}
        public static string PlayerTitle(Player p)
        {
            string name=p.Group.ToString();
            var groups=LandService.Model?.Data.GroupsFile?.Groups;
            if(groups!=null&&(int)p.Group<groups.Count)name=groups[(int)p.Group].Name;
            string sector=p.Anchor>=0?LandService.Model.Data.Industries[p.Anchor].Name:"diversified / household income";
            return name+" / "+sector;
        }
        static void Ring(LineMeshBuilder b,Vector3 center,float radius,Color color,float width,bool vertical=false)
        {
            var points=new Vector3[33];for(int i=0;i<=32;i++)
            {float a=i*Mathf.PI/16;points[i]=center+(vertical?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0):new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            b.AddPolyline(points,color,width,0,0);
        }
        static void Flow(LineMeshBuilder b,Vector3 a,Vector3 z,Color color,float width,float lift)
        {
            var pts=new LinePoint[21];for(int i=0;i<pts.Length;i++)
            {float t=i/20f;pts[i]=new LinePoint(Vector3.Lerp(a,z,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*lift),color,width);}
            b.AddFlowPath(pts,0,2); Arrow(b,pts[17].Data,pts[19].Data,color);
        }
        static void Arrow(LineMeshBuilder b,Vector3 a,Vector3 z,Color color)
        {
            Vector3 d=(z-a).normalized,side=Vector3.Cross(d,Vector3.up).normalized;
            if(side.sqrMagnitude<.01f)side=Vector3.right;
            b.AddPolyline(new[]{z-d*.10f+side*.045f,z,z-d*.10f-side*.045f},color,1,0,0);
        }
        void Clear()
        {
            foreach(var e in renderers)if(e.renderer)Destroy(e.renderer.sharedMaterial);
            if(socialRenderer)Destroy(socialRenderer.sharedMaterial);
            foreach(var mesh in meshes)if(mesh)Destroy(mesh);
            if(content)Destroy(content);
            meshes.Clear();renderers.Clear();labels.Clear();crowns.Clear();socialRenderer=null;
        }
        void OnDestroy(){LandService.Changed-=Rebuild;Clear();Current=null;Shown=null;HoverIndustry=HoverPlayer=-1;HoverOwner=null;}
    }
}
