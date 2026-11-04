using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;

namespace Why.Economy.Layers
{
    /// <summary>Moving cohort markers, balance-sheet rings, live demand rivers and an illustrative corporate interior.
    /// Animation is a repeated visual itinerary: only PlayerMindProgram.Step books annual transactions.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillActivityLayer : GraphLayer
    {
        public override int Order=>56;
        public const int Subgroups=5;
        static HillActivityLayer active;
        public static Vector3 CompanyPosition(int index)=>active!=null&&active.firmAt!=null&&index>=0&&index<active.firmAt.Length?active.firmAt[index]:Vector3.zero;
        /// <summary>The firm's profit halo at the top of its market-value beam.</summary>
        public static Vector3 CompanyHalo(int index)=>active!=null&&active.firms.Count>index&&index>=0?active.firms[index].transform.localPosition:CompanyPosition(index);
        /// <summary>A stored firm's (tower's) gold inner circle of owners: its center and radius, land-local; false when
        /// the shown year has no tower for it. Matched by ticker, else name (CompanyLives.Matches).</summary>
        public static bool OwnerCircle(CompanyLives.Life life,out Vector3 center,out float radius)
        {
            center=default;radius=0;
            if(active?.circleAt==null||life==null)return false;
            var towers=active.snapshot.Land.Towers;
            for(int f=0;f<towers.Length&&f<active.circleAt.Length;f++)
                if(CompanyLives.Matches(towers[f].Ticker,towers[f].Name,life)){center=active.circleAt[f];radius=active.circleRadius[f];return true;}
            return false;
        }
        public static Vector3[] Positions { get; private set; }
        public static CorporateCompetition Competition { get; private set; }
        public static Vector3 Position(int i)=>Positions!=null&&i*Subgroups<Positions.Length?Positions[i*Subgroups]:HillLandscapeLayer.Current.People[i];
        static readonly Color Desire=new Color(1,.23f,.48f),Fear=new Color(.25f,.65f,1),Gold=new Color(1,.66f,.14f);
        /// <summary>The rivers' lanes: rose for what is bought from desire, ice for what is bought from fear.</summary>
        static readonly Color DesireLane=new Color(1,.10f,.36f),FearLane=new Color(.06f,.78f,1);
        /// <summary>The width (units) a trunk would have for the whole year's household spending: trunks are linear in dollars.</summary>
        const float TrunkWidth=40;
        /// <summary>The camera distance (the rivers view's) at which the caption has its drawn size; it keeps that size on screen.</summary>
        const float CaptionDistance=176;
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom. The hovered or
        /// selected cohort's rivers bloom by their own intensity and highlights (Water).</summary>
        const float Pulse=.2f;
        readonly List<MeshRenderer> actors=new List<MeshRenderer>(), firms=new List<MeshRenderer>(), controls=new List<MeshRenderer>(), orgs=new List<MeshRenderer>();
        float[] haloScale;
        readonly List<Mesh> owned=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly List<TextMeshPro> labels=new List<TextMeshPro>(),badges=new List<TextMeshPro>();
        Vector3[] home, destinations, firmAt, circleAt;
        float[] circleRadius;
        int[] recipient;
        double[] riverAmount;
        /// <summary>Per subgroup: the fear share of what its river buys at its market (ValueCapture.SoldFear), which
        /// splits its lanes; the spender's own fear colors the marker.</summary>
        float[] riverFear;
        /// <summary>Per hill: the fear share of all households' spending at its market (<see cref="Trunks"/>), which splits its trunk.</summary>
        double[] trunkFear;
        /// <summary>The trunks' names (one per hill, empty when unnamed), their plates, and the rivers view's caption over its
        /// rose / ice bar.</summary>
        readonly List<TextMeshPro> trunkLabels=new List<TextMeshPro>();
        readonly List<Transform> trunkPlates=new List<Transform>();
        Transform caption;TextMeshPro captionText,captionNote;MeshRenderer captionBar;
        /// <summary>The font's material drawn after the hills (queue 3252), for the names read over the rivers and the wake,
        /// which the font's own queue would let them draw over.</summary>
        Material raisedText;
        /// <summary>The dark plate behind the trunks' names (a unit square scaled to each name, and its material drawn just
        /// before the raised text, queue 3251), kept across rebuilds.</summary>
        Mesh plateQuad;Material plateMaterial;
        LineMeshBuilder pulseGeometry;
        MeshRenderer pulses, rivers, riverFill, focus, structure, rivalry, circles, circleThreads;
        double[] eliteClaim;
        double eliteShare;
        LandSnapshot snapshot;
        HillLandscape land;
        GameObject content;
        float animation, refresh;
        int version=-1, selection=-2, lod=-1;
        /// <summary>The scenario program the trips and balances were drawn for (a new year brings a new one).</summary>
        PlayerMindProgram programSeen;
        /// <summary>1 while a rebuild has made its first half and waits for a frame for its second (see Begin).</summary>
        int stage;
        /// <summary>The markers and firms drawn for the previous land, on screen until the rebuild's second half swaps.</summary>
        GameObject retired;
        readonly List<UnityEngine.Object> retiredObjects=new List<UnityEngine.Object>(), kept=new List<UnityEngine.Object>();
        GameObject riversRoot;
        string view;
        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx){active=this;EconomyStage.Land().Place(transform);}
        public override void Tick(GraphContext ctx,CameraRig rig)
        {
            if(HillLandscapeLayer.Current==null)return;
            if(snapshot!=LandService.Current){if(!HillRebuilds.Take())return;Begin();}
            // the second half of a rebuild takes the next free frame (the same one for a land published blocking)
            if(stage==1){if(!HillRebuilds.Take())return;Finish();}
            if(land==null)return;
            int nextLod=rig.Pose.Distance<35?2:rig.Pose.Distance<100?1:0;
            if(nextLod!=lod){lod=nextLod;UpdateBalances();}
            var program=Program;
            if(program!=programSeen||version!=(program?.Revision??-1)||selection!=EconomyState.SelectedPlayer)
            {
                if(!HillRebuilds.Take())return;
                programSeen=program;version=program?.Revision??-1;selection=EconomyState.SelectedPlayer;
                Competition.Update(program);PlanTrips();UpdateBalances();refresh=1;
            }
            animation+=Time.deltaTime;
            bool hidden=LandView.PresetId=="mind"||LandView.PresetId=="section";
            // The money chart's series wear the rivers' rose and ice and the halos' gold (the mortgage, the Treasury, the bank's
            // rate): seen edge-on behind its panel, the crest's trunks, halos and owner rings added up to the series' own
            // brightness and read as more of them, so the chart's view draws none of them (the firms' glow, below).
            bool chart=LandView.PresetId=="money";
            // The regimes view draws its own summits (HillRegimeLayer: steel where the state owns them, gold where it does
            // not), so it draws none of the firms' halos, beams, organizations and owner rings, nor the ring of the summit most
            // is spent at (DrawFocus): under the Soviet state, whose every summit is its own, their gold read as private ones.
            bool ownSummits=LandView.PresetId==HillRegimeLayer.View;
            content.SetActive(!hidden);riversRoot.SetActive(!hidden&&!chart);if(hidden)return;
            int focusPlayer=selection>=0?selection:HillLandscapeLayer.HoverPlayer;
            int focusIndustry=focusPlayer>=0?land.IndustryOf[focusPlayer]:HillExplorer.Industry>=0?HillExplorer.Industry:HillLandscapeLayer.HoverIndustry;
            for(int p=0;p<actors.Count;p++)
            {
                float progress=(1-Mathf.Cos(animation*(.055f+.015f*(1-FearOf(p)))+p*2.399963f))*.5f;
                Vector3 at=Vector3.Lerp(home[p],destinations[p],progress);
                if(!land.Cloud[p/Subgroups])at.y=Mathf.Lerp(land.Footing(land.IndustryOf[p/Subgroups],at.x,at.z),land.Footing(recipient[p],at.x,at.z),progress)+.3f;
                if(HillExplorer.Depth>=3&&p/Subgroups==HillExplorer.Group)at=HillExplorer.GroupCenter;
                Positions[p]=at;actors[p].transform.localPosition=at;
                bool memberView=HillExplorer.Depth>=3&&p/Subgroups==HillExplorer.Group;
                bool nearby=lod==0||focusIndustry<0||land.IndustryOf[p/Subgroups]==focusIndustry||Vector3.Distance(rig.Pose.Target,EconomyStage.Land().World(at))<35;
                actors[p].gameObject.SetActive(!memberView&&nearby);
                Color color=Color.Lerp(Desire,Fear,FearOf(p));
                float intensity=(p/Subgroups==focusPlayer?3.5f:focusPlayer>=0?.16f:p%145==0?1:.60f)*(PersonFocus.IsView(LandView.PresetId)?.25f:EconomyViews.Focused(LandView.PresetId)?.3f:1);
                actors[p].sharedMaterial.SetColor("_Color",Color.white*intensity);
            }
            string v=LandView.PresetId;bool person=PersonFocus.IsView(v),focusedView=EconomyViews.Focused(v);
            // The person views and the focused views keep the firms' readouts, beams and organizations out of their story
            // (the strategy view keeps the beams its streams leave from, and draws its own readouts); a person view keeps
            // the organization of the employer the person's income comes from.
            bool quietFirm=focusedView||person,quietBeams=quietFirm&&v!="strategy";int employer=person?Employer():-1;
            for(int f=0;f<firms.Count;f++)
            {
                bool related=snapshot.Land.Towers[f].Industry==focusIndustry,chosen=HillExplorer.Company==f;
                firms[f].transform.localScale=Vector3.one*haloScale[f];
                // HDR only for the chosen firm and the hovered or selected sector's; who keeps the money draws the halos of
                // the industries' owners instead of the firms' (the firms' inner circles of owners stay)
                float glow=chosen?5:related?3.6f:v=="capture"?0:Competition.FirmReceipts[f]>Competition.TotalSpending*.03?1:.9f;
                // in a focused view other than the companies' own the firms' halos step back to landmarks, in a person's further;
                // the money chart's and the regimes' views draw none (above)
                if(chart||ownSummits)glow=0;else if(focusedView&&v!="strategy")glow*=.25f;else if(person)glow*=.15f;
                // blended, not added (decision 36): up to 1 the glow is the halo's opacity and its brightness, so one shows
                // as it did added (the glow squared) and the halos the tech hills stack on screen keep their gold; past 1 the
                // halo is opaque and its brightness alone carries the glow, as bright as added, into bloom
                firms[f].sharedMaterial.SetColor("_Color",glow<=1?Color.white*glow:new Color(glow*glow,glow*glow,glow*glow,1));
                // Corporate infrastructure by distance: beams and halos always; revenue, margin and value readouts nearer or
                // for the focused sector; the organization inside the firm only close up, or the chosen firm's. The money
                // chart's and the regimes' views draw none of it, the chosen firm's organization neither: without its beam and
                // halo it floated over its seat, its gold board's lines meeting at nothing.
                bool near=Vector3.Distance(rig.Pose.Target,EconomyStage.Land().World(firmAt[f]))<60;
                GraphMaterials.SetAlpha(controls[f].sharedMaterial,chart||ownSummits?0:chosen||!quietBeams&&(related&&lod>=1||lod>=1&&near)?.95f:quietBeams?.05f:v=="capture"?.55f:.22f);
                GraphMaterials.SetAlpha(orgs[f].sharedMaterial,HillExplorer.EvidenceOnly||chart||ownSummits?0:chosen||f==employer||!quietFirm&&lod>=2&&near?.9f:!quietFirm&&related&&lod>=1?.3f:0);
                // the readouts are world-sized: from the capture view's 150 units their lines were 4-5 px, so there too they
                // show only for the chosen firm, the focused sector's or from mid distance
                labels[f].gameObject.SetActive(!quietFirm&&(chosen||(related&&lod>=1)||lod>=1&&near));
                labels[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].color=related?new Color(1,.78f,.30f):new Color(.40f,.55f,.66f);
                badges[f].gameObject.SetActive(!quietFirm&&(lod>=1||related||snapshot.Land.Towers[f].MarketCap>2000));
                labels[f].text=FirmText(f);
            }
            GraphMaterials.SetAlpha(structure.sharedMaterial,HillExplorer.EvidenceOnly?0:LandView.PresetId=="roots"?.35f:.018f);
            GraphMaterials.SetAlpha(rivalry.sharedMaterial,HillExplorer.EvidenceOnly?0:LandView.PresetId=="capture"?.5f:.07f);
            // The owners' inner circles: every firm sits inside a ring of its elite owners; threads rise to the clouds (not in
            // who keeps the money, where the owners' surplus already climbs from the halos to its holders). Neither in the
            // money chart's and the regimes' views (above).
            bool own=v=="capture"||v=="companies"||v=="strategy";bool quiet=PersonFocus.IsView(v)||EconomyViews.Focused(v)&&!own;
            GraphMaterials.SetAlpha(circles.sharedMaterial,chart||ownSummits?0:own?1:quiet?.15f:v=="overview"||v=="landscape"||v=="people"?.75f:.45f);
            GraphMaterials.SetAlpha(circleThreads.sharedMaterial,v=="capture"||chart||ownSummits?0:own?.6f:v=="people"?.3f:quiet?.02f:.08f);
            // The rivers view names its trunks (not while a cohort's own rivers dim them) and the year's split of household
            // spending (both written by Trunks).
            bool riversView=v=="rivers";
            foreach(var t in trunkLabels)
            {
                bool show=riversView&&focusPlayer<0&&!string.IsNullOrEmpty(t.text);
                t.gameObject.SetActive(show);if(show)t.transform.rotation=rig.Cam.transform.rotation;
            }
            caption.gameObject.SetActive(riversView);
            if(riversView){caption.rotation=rig.Cam.transform.rotation;caption.localScale=Vector3.one*Mathf.Max(.5f,Vector3.Distance(rig.Cam.transform.position,caption.position)/CaptionDistance);}
            if(view!=LandView.PresetId){view=LandView.PresetId;refresh=1;}
            refresh+=Time.deltaTime;
            // redrawn five times a second, never in a frame another layer rebuilds in (after a swap: the next free frame), and
            // not while the money chart's view hides them
            if(refresh>=.2f&&!chart&&HillRebuilds.Take()){refresh=0;DrawRivers(focusPlayer);DrawFocus(focusIndustry);}
        }
        /// <summary>The followed person's employer (tower index), as the person layer draws it: the firm nearest the person on
        /// their household's hill, when they earn; -1 otherwise.</summary>
        int Employer()
        {
            var life=PersonFocus.Life;if(life==null||firmAt==null||life.Now.Earned<=100)return -1;
            int industry=life.IndustryAt(life.Records.Length-1);if(industry<0)industry=life.Now.Industry;
            int best=-1;float closest=float.MaxValue;var towers=snapshot.Land.Towers;
            for(int f=0;f<towers.Length&&f<firmAt.Length;f++){if(towers[f].Industry!=industry)continue;float d=Vector3.Distance(firmAt[f],PersonFocus.Place);if(d<closest){closest=d;best=f;}}
            return best;
        }
        /// <summary>The running scenario, when it was built for the shown year's cohorts (else the census stands).</summary>
        PlayerMindProgram Program=>PlayerMindProgram.Active!=null&&snapshot!=null&&PlayerMindProgram.Active.Players.Length==snapshot.Players.Players.Length?PlayerMindProgram.Active:null;
        float FearOf(int p)=>Program?.Players[p/Subgroups].Fear??snapshot.Players.Players[p/Subgroups].Fear;
        /// <summary>
        /// A rebuild's first half: the new year's competition and corporate interior, into new content kept hidden while the
        /// markers and firms drawn for the previous land stay on screen; <see cref="Finish"/> makes the markers, plans their
        /// trips and draws their balance sheets in the next free frame and swaps. Half a rebuild a frame (HillRebuilds).
        /// </summary>
        void Begin()
        {
            if(stage==1)Clear();else Retire();   // a half-made rebuild is dropped when the land changed again before its second half
            snapshot=LandService.Current;land=HillLandscapeLayer.Current;
            content=new GameObject("Live players and corporate interior");content.transform.SetParent(transform,false);content.SetActive(false);
            KeepRivers();
            home=new Vector3[land.People.Length*Subgroups];
            for(int p=0;p<home.Length;p++){float a=p%Subgroups*Mathf.PI*2/Subgroups;home[p]=land.People[p/Subgroups]+new Vector3(Mathf.Cos(a)*1.1f,0,Mathf.Sin(a)*1.1f);}
            destinations=new Vector3[home.Length];recipient=new int[home.Length];riverAmount=new double[home.Length];riverFear=new float[home.Length];
            Competition=new CorporateCompetition(snapshot);Competition.Update(Program);
            BuildCorporations();
            stage=1;
        }
        /// <summary>A rebuild's second half: the markers, their trips and balance sheets, then the swap.</summary>
        void Finish()
        {
            var empty=new LineMeshBuilder(1);
            for(int p=0;p<home.Length;p++){var actor=Render(empty,"Player "+p+" / group "+p/Subgroups);actor.transform.localScale=Vector3.one*.5f;actors.Add(actor);}
            Positions=(Vector3[])home.Clone();
            // the trunks' names are made under the layer (active), so the text wakes with the font's material to raise, then
            // join the hidden content; each stands on a plate (sized by Trunks), read over the pale lanes at the markets
            for(int h=0;h<land.Hills.Length;h++)
            {
                var name=Raised(Label("Trunk "+h,land.Market(h),transform));name.transform.SetParent(content.transform,false);
                name.transform.localScale=Vector3.one*.7f;name.color=new Color(.9f,.93f,.97f);trunkLabels.Add(name);trunkPlates.Add(Plate(name));
            }
            PlanTrips();UpdateBalances();
            // built for the program and selection it saw: the next Tick redoes the trips and balances only if they change
            programSeen=Program;version=programSeen?.Revision??-1;selection=EconomyState.SelectedPlayer;
            DropRetired();content.SetActive(true);stage=0;
            refresh=1;   // the rivers follow in the next free frame
        }
        void PlanTrips()
        {
            for(int p=0;p<home.Length;p++)
            {
                var budget=Competition.Budget(p/Subgroups,Program?.Players[p/Subgroups]);int best=0;
                double total=0;foreach(double amount in budget)total+=amount;
                double sample=((p*.61803398875+.21)%1)*total,cumulative=0;
                for(int h=0;h<budget.Length;h++){cumulative+=budget[h];if(cumulative>=sample){best=h;break;}}
                recipient[p]=best;riverAmount[p]=budget[best]/Subgroups;
                var capture=ValueCapture.Get();var spend=(IReadOnlyList<double>)Program?.Players[p/Subgroups].Category??snapshot.Players.Players[p/Subgroups].Category;
                riverFear[p]=capture!=null?(float)capture.FearOfPurchase(spend,best,LandService.Model.Data):FearOf(p);
                // A bounded visit toward the budget-weighted spending destination, keeping cohort settlements legible.
                Vector3 target=land.Market(best);
                destinations[p]=Vector3.Lerp(home[p],target,.18f+.20f*(1-FearOf(p)));
                if(land.Cloud[p/Subgroups])destinations[p].y=home[p].y;
            }
            Trunks();
        }
        /// <summary>
        /// The trunks' split and names: per hill, the fear share of all households' spending at its market (each cohort's
        /// budget there times <see cref="ValueCapture.FearOfPurchase"/>, as its trips are split); the trunks carrying at
        /// least 3% of the year's spending named with their desire and fear dollars; and the caption's split of the whole.
        /// </summary>
        void Trunks()
        {
            var capture=ValueCapture.Get();var data=LandService.Model.Data;var players=snapshot.Players.Players;int n=land.Hills.Length;
            var spent=new double[n];var feared=new double[n];
            for(int i=0;i<players.Length;i++)
            {
                var state=Program?.Players[i];var budget=Competition.Budget(i,state);
                var spend=(IReadOnlyList<double>)state?.Category??players[i].Category;
                for(int h=0;h<n;h++)if(budget[h]>0){spent[h]+=budget[h];feared[h]+=budget[h]*(capture!=null?capture.FearOfPurchase(spend,h,data):state?.Fear??players[i].Fear);}
            }
            double total=0,fear=0;trunkFear=new double[n];var named=new List<int>();
            for(int h=0;h<n;h++)
            {
                total+=spent[h];fear+=feared[h];trunkFear[h]=spent[h]>0?feared[h]/spent[h]:.5;
                trunkLabels[h].text="";
            }
            for(int h=0;h<n;h++)if(land.Hills[h].Tier>0&&total>0&&spent[h]>=.03*total)named.Add(h);
            // neighbors along the crest stand at three heights, so their names do not print over each other
            named.Sort((a,c)=>land.Market(a).x.CompareTo(land.Market(c).x));
            for(int k=0;k<named.Count;k++)
            {
                int h=named[k];
                // the name as large as its figures: from the rivers view's distance a smaller line is too faint to read
                trunkLabels[h].text=data.Industries[h].Name+"\n<color=#FF5C8A>"+LandFacts.Money(spent[h]-feared[h])+" desire</color> · <color=#4FB8FF>"+LandFacts.Money(feared[h])+" fear</color>";
                trunkLabels[h].transform.localPosition=land.Market(h)+Vector3.up*(3.5f+3.2f*(k%3));trunkPlates[h].localScale=PlateSize(trunkLabels[h]);
            }
            // The caption stands over the wake on the valley side, clear of the rivers in the rivers view's frame.
            int desire=total>0?(int)Math.Round(100*(1-fear/total)):50;
            captionText.text="<color=#FF5C8A>desire "+desire+"%</color> · <color=#4FB8FF>fear "+(100-desire)+"%</color>";
            captionNote.text="of household spending in "+snapshot.Year+", "+LandFacts.Money(total)+" a year\n"+
                (capture!=null&&capture.Judged?"each purchase judged by the AI model Jev":"by spending category, assumed")+" · widths at the markets: dollars a year";
            var bar=new LineMeshBuilder(4);float half=15,split=-half+2*half*desire/100f;
            bar.AddSegment(new Vector3(-half,0,0),new Vector3(split-.2f,0,0),DesireLane,12,0,0);
            bar.AddSegment(new Vector3(split+.2f,0,0),new Vector3(half,0,0),FearLane,12,0,0);
            Redraw(captionBar,bar.ToMesh("Rivers caption bar"));
            caption.localPosition=new Vector3(60,40,land.CrestZ-60);
        }
        void UpdateBalances()
        {
            var b=new LineMeshBuilder(64);
            for(int p=0;p<actors.Count;p++)
            {
                var state=Program?.Players[p/Subgroups];var cohort=snapshot.Players.Players[p/Subgroups];
                double assets=state?.Assets??Math.Max(0,cohort.Wealth+cohort.Debt),debt=state?.Debt??cohort.Debt;
                assets/=Subgroups;debt/=Subgroups;
                b.Clear();Color motive=Color.Lerp(Desire,Fear,FearOf(p));
                b.AddSegment(Vector3.zero,Vector3.up*1.2f,motive,2.2f,0,0);
                Ring(b,Vector3.up*1.5f,.30f,motive,1.8f,true);
                // Gross assets and liabilities get separate persistent rings; logarithmic radius handles group scale.
                if(lod>=1||p/Subgroups==EconomyState.SelectedPlayer)Ring(b,Vector3.zero,.35f+(float)Math.Log10(1+assets)*.20f,Gold,1.4f);
                if(debt>0&&(lod>=1||p/Subgroups==EconomyState.SelectedPlayer))Ring(b,Vector3.up*.12f,.28f+(float)Math.Log10(1+debt)*.18f,Desire,1.4f);
                for(int child=0;lod>=2&&child<Math.Min(4,(cohort.Children.Length+p%Subgroups)/Subgroups);child++)
                {float a=child*2.4f;Vector3 c=new Vector3(Mathf.Cos(a)*.9f,0,Mathf.Sin(a)*.9f);b.AddSegment(c,c+Vector3.up*.5f,Color.white,1,0,0);}
                Replace(actors[p],b);
            }
        }
        void BuildCorporations()
        {
            var branches=new LineMeshBuilder();var competition=new LineMeshBuilder();
            var towers=snapshot.Land.Towers;firmAt=new Vector3[towers.Length];haloScale=new float[towers.Length];var count=new int[land.Hills.Length];
            circleAt=new Vector3[towers.Length];circleRadius=new float[towers.Length];
            var total=new int[land.Hills.Length];foreach(var t in towers)total[t.Industry]++;
            var capture=LandService.Model.Data.Circuit?.Capture;
            var ring=new LineMeshBuilder();var rise=new LineMeshBuilder();
            // The top 1%'s share of corporate equity (DFA, the shown year) of the domestic part of each firm's market value:
            // the size of its inner circle of elite owners (a portfolio proxy, not the firm's register).
            var circuit=LandService.Model.Data.Circuit;
            eliteShare=Math.Max(0,Math.Min(1,Data.EconomyData.Keyed(circuit?.GroupEquityHistory,"top1").At(snapshot.Year)));
            double domestic=1-Math.Max(0,Math.Min(1,circuit?.ForeignEquityShare??0));eliteClaim=new double[towers.Length];
            for(int f=0;f<towers.Length;f++)
            {
                var tower=towers[f];var hill=land.Hills[tower.Industry];int seat=count[hill.Industry]++;
                // Firms stand on the hill's crest side, where their lifelines (HillWaveLayer) arrive from the wake.
                Vector3 at=land.FirmSeat(hill.Industry,seat,total[hill.Industry]);firmAt[f]=at;
                float beam=Beam(tower),revenue=.45f+Mathf.Sqrt((float)Math.Max(0,tower.Revenue))*.07f;
                haloScale[f]=.45f+Mathf.Sqrt((float)Math.Max(0,tower.NetIncome))*.09f;
                // Far: the halo of profit at the top of a beam as tall as the market's price of future profits. Drawn in its
                // hue (intensity 1), blended; the material's glow lifts the chosen and the hovered sector's firms into bloom
                // (Tick).
                var node=new LineMeshBuilder();
                Ring(node,Vector3.zero,1,Gold,2.6f);Ring(node,Vector3.up*.05f,1.18f,Gold,1.2f);Ring(node,Vector3.zero,.72f,new Color(1,.95f,.85f),1.1f);
                var renderer=Render(node,tower.Name,true,false);renderer.transform.localPosition=at+Vector3.up*beam;firms.Add(renderer);
                labels.Add(Label(tower.Name,at+Vector3.up*(beam+2.8f)));
                string ticker=string.IsNullOrWhiteSpace(tower.Ticker)?tower.Name.Substring(0,Math.Min(3,tower.Name.Length)).ToUpperInvariant():tower.Ticker;
                var badge=Label("["+ticker+"]",at+Vector3.up*(beam+1.3f));badge.text="<b>["+ticker+"]</b>";badge.transform.localScale=Vector3.one*.55f;badges.Add(badge);
                // Mid: revenue at the foot, its net margin as a gold arc, the beam marked every $1T of market value.
                var mid=new LineMeshBuilder();
                mid.AddSegment(at,at+Vector3.up*beam,Gold,1.4f,0,0);
                Ring(mid,at+Vector3.up*.08f,revenue,new Color(.62f,.78f,.95f,.8f),1.2f);
                float margin=Mathf.Clamp01(tower.Margin>0?tower.Margin:(float)(tower.NetIncome/Math.Max(1e-6,tower.Revenue)));
                if(margin>0)ArcAt(mid,at+Vector3.up*.12f,revenue,-Mathf.PI*.5f,-Mathf.PI*.5f+margin*Mathf.PI*2,Gold,3,1);
                for(double v=1000;v<tower.MarketCap;v+=1000){Vector3 tick=at+Vector3.up*(beam*(float)(v/tower.MarketCap));mid.AddSegment(tick-Vector3.right*.25f,tick+Vector3.right*.25f,Gold,1,0,0);}
                controls.Add(Render(mid,"Revenue margin value / "+tower.Name,true));
                // Near: the organization (schematic): the workforce at the base, then managers, executives and the board
                // narrowing up the beam to the owners' halo. Headcount sets the base; reporting lines are illustrative.
                double staff=capture!=null&&tower.Company<capture.Count?capture[tower.Company].UsEmployees:0;
                orgs.Add(Render(Organization(at,beam,revenue,staff),"Organization / "+tower.Name,true));
                eliteClaim[f]=Math.Max(0,tower.MarketCap)*domestic*eliteShare;
                circleAt[f]=at+Vector3.up*(beam-.35f);circleRadius[f]=1.18f*haloScale[f]+.55f+.03f*Mathf.Sqrt((float)eliteClaim[f]);
                InnerCircle(ring,rise,circleAt[f],circleRadius[f],eliteClaim[f]);
                for(int prior=0;prior<f;prior++)if(towers[prior].Industry==hill.Industry)
                    competition.AddPolyline(new[]{firmAt[prior]+Vector3.up*.2f,(firmAt[prior]+at)*.5f+Vector3.up*1.2f,at+Vector3.up*.2f},Desire,1,0,0,1,1);
            }
            // Sector IO is evidence; distributing its endpoint across sampled firms is illustrative.
            var io=LandService.Model.Data.Circuit?.Io;
            if(io!=null)foreach(var edge in io.Flows())
            {
                var a=LandService.Model.Data.IndustryById(edge.from);var b=LandService.Model.Data.IndustryById(edge.to);
                if(a==null||b==null||a.Index==b.Index||edge.value<100)continue;
                for(int f=0;f<towers.Length;f++)if(towers[f].Industry==a.Index)
                    Tunnel(branches,firmAt[f],land.Hills[b.Index].Foot,Fear,.6f);
            }
            structure=Render(branches,"Inferred supply allocations");
            // blended, not added, as the halos (decision 36): the tech hills' circles stack on screen, and their gold summed
            // to a flat yellow-white
            circles=Render(ring,"Owners' inner circles",true,false);
            circleThreads=Render(rise,"Inner circles to the clouds",true);
            rivalry=Render(competition,"Same-sector rivalry (scenario)",true);
        }
        /// <summary>A gold ring of owner figures around a firm's profit halo, as many as its elite claim warrants
        /// (one per ~$9B squared-root scaled, 5..22), with a thin gold thread up to the 1% cloud.</summary>
        void InnerCircle(LineMeshBuilder ring,LineMeshBuilder rise,Vector3 c,float r,double claim)
        {
            int n=Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt((float)claim)/3f),5,22);
            Ring(ring,c,r,Gold,2.2f);Ring(ring,c+Vector3.up*.08f,r*1.05f,WithAlpha(Gold,.5f),1);
            for(int k=0;k<n;k++)
            {
                float a=k*Mathf.PI*2/n;Vector3 p=c+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                ring.AddSegment(p,p+Vector3.up*.32f,Gold,1.8f,.025f,0);Ring(ring,p+Vector3.up*.42f,.08f,Gold,1.2f,true);
            }
            var cloud=land.CloudCenter;var pts=new LinePoint[21];
            for(int k=0;k<pts.Length;k++){float t=k/20f;pts[k]=new LinePoint(Vector3.Lerp(c+Vector3.up*.4f,cloud,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*4,WithAlpha(Gold,.55f),.7f+Mathf.Sqrt((float)claim)*.02f);}
            rise.AddFlowPath(pts,0,.3f);
        }
        static float Beam(TowerGeom t)=>2+Mathf.Log10(1+(float)Math.Max(0,t.MarketCap))*2.4f;
        LineMeshBuilder Organization(Vector3 at,float beam,float radius,double staffThousands)
        {
            var b=new LineMeshBuilder();
            int workers=Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(1+(float)Math.Max(1,staffThousands),2)*3.2f),8,40);
            int managers=Mathf.Max(3,workers/5),executives=Mathf.Min(6,Mathf.Max(3,managers/2));
            float[] heights={.25f,beam*.30f,beam*.58f,beam*.80f};float[] radii={radius*.95f,radius*.62f,radius*.36f,radius*.18f};
            int[] counts={workers,managers,executives,7};Color[] colors={Fear,new Color(.55f,.80f,1),new Color(1,.95f,.88f),Gold};
            var previous=new List<Vector3>();var current=new List<Vector3>();
            for(int level=0;level<4;level++)
            {
                current.Clear();
                for(int k=0;k<counts[level];k++)
                {
                    float a=k*Mathf.PI*2/counts[level]+level*.3f;
                    Vector3 p=at+new Vector3(Mathf.Cos(a)*radii[level],heights[level],Mathf.Sin(a)*radii[level]);current.Add(p);
                    b.AddSegment(p,p+Vector3.up*.18f,colors[level],1.6f,0,0);
                    Ring(b,p+Vector3.up*.26f,.06f,colors[level],1,true);
                }
                if(level>0)for(int k=0;k<previous.Count;k++)
                {
                    Vector3 lower=previous[k];int up=k*current.Count/previous.Count;
                    b.AddSegment(lower+Vector3.up*.3f,current[up],WithAlpha(colors[level],.45f),.7f,0,0);
                }
                previous.Clear();previous.AddRange(current);
            }
            foreach(var seat in previous)b.AddSegment(seat+Vector3.up*.3f,at+Vector3.up*beam,WithAlpha(Gold,.6f),.8f,0,0);
            return b;
        }
        string FirmText(int f)
        {
            var t=snapshot.Land.Towers[f];
            if(HillExplorer.EvidenceOnly)return t.Name+"\nStored revenue "+LandFacts.Money(t.Revenue);
            string s="<b>"+t.Name+"</b>";
            if(t.Revenue>0)s+="\nrevenue "+LandFacts.Money(t.Revenue)+" · profit "+LandFacts.Money(t.NetIncome)+" ("+LandFacts.Percent(t.NetIncome/t.Revenue)+")";
            if(t.NetIncome>0)s+="\nmarket value "+LandFacts.Money(t.MarketCap)+" = "+(t.MarketCap/t.NetIncome).ToString("F0",LandFacts.Ci)+" years of today's profit";
            else s+="\nmarket value "+LandFacts.Money(t.MarketCap);
            if(eliteClaim!=null&&f<eliteClaim.Length&&eliteClaim[f]>0)s+="\ninner circle: the top 1% hold ≈ "+LandFacts.Money(eliteClaim[f])+" ("+LandFacts.Percent(eliteShare)+" of US-held equity)";
            return s+"\nscenario household demand "+LandFacts.Money(Competition.FirmReceipts[f]);
        }
        static Color WithAlpha(Color c,float a){c.a=a;return c;}
        static void ArcAt(LineMeshBuilder b,Vector3 c,float r,float a0,float a1,Color color,float width,float intensity)
        {
            int n=Mathf.Max(2,Mathf.CeilToInt((a1-a0)/(Mathf.PI*2)*64));var pts=new Vector3[n+1];
            for(int k=0;k<=n;k++){float a=Mathf.Lerp(a0,a1,k/(float)n);pts[k]=c+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
            b.AddPolyline(pts,color,width,0,0,intensity);
        }
        void DrawRivers(int focused)
        {
            var b=new LineMeshBuilder();var fill=new SurfaceMeshBuilder();pulseGeometry=new LineMeshBuilder();
            // the rivers' own view draws every third subgroup's trip at half strength and the trunks at full; elsewhere one
            // trip in fifteen and the trunks are a trace under the view's own story
            bool own=LandView.PresetId=="rivers";
            for(int p=0;p<home.Length;p++)
            {
                if(riverAmount[p]<=0)continue;
                if(p/Subgroups!=focused && p%(own?3:15)!=0)continue;
                var hill=land.Hills[recipient[p]];bool active=p/Subgroups==focused;
                float alpha=active?.90f:focused>=0?.006f:own?.18f:.035f;
                Color color=Color.Lerp(Desire,Fear,riverFear[p]);color.a=alpha;
                Vector3 end=land.Market(recipient[p]);
                var path=new LinePoint[33];
                for(int k=0;k<path.Length;k++)
                {
                    float t=k/32f;Vector3 at=Vector3.Lerp(Positions[p],end,t);
                    float ground=land.Ground(at.x,at.z)+.25f;
                    at.y=land.Cloud[p/Subgroups]?Mathf.Lerp(Positions[p].y,ground,Mathf.SmoothStep(0,1,t)):ground;
                    path[k]=new LinePoint(at,color,(active?2:1)+(float)Math.Log10(1+riverAmount[p])*.3f,0,active?2.3f:1.0f);
                }
                Water(fill,b,path,.12f+(float)Math.Sqrt(riverAmount[p])*.055f,riverFear[p],active?.8f:focused>=0?.008f:own?.5f:.035f,p,active);
            }
            // Each industry's trunk carries all households' spending at its market, as wide as its share of the year's
            // dollars (linear, unlike the trips), split into the fear and desire lanes of what is bought there (Trunks).
            double total=Competition.TotalSpending;
            for(int h=0;h<land.Hills.Length;h++)
            {
                if(land.Hills[h].Tier==0)continue;
                double amount=Competition.IndustrySpending[h];
                if(amount<=0||total<=0)continue;
                var path=new LinePoint[41];
                for(int k=0;k<path.Length;k++)
                {
                    // The household river runs in from the valley and ends at the hill's market, where its value splits
                    // (HillCaptureLayer): the owners' share climbs on as gold.
                    float t=k/40f;Vector3 point=land.At(land.Hills[h],HillLandscape.Valley+Mathf.Sin(t*6)*.12f*(1-t),Mathf.Lerp(1.55f,.86f,t),.3f);
                    path[k]=new LinePoint(point,Color.white,1);
                }
                bool active=false;if(focused>=0)for(int p=focused*Subgroups;p<(focused+1)*Subgroups;p++)if(recipient[p]==h)active=true;
                Water(fill,b,path,.25f+TrunkWidth*(float)(amount/total),(float)trunkFear[h],active?.55f:focused>=0?.015f:own?.8f:.20f,home.Length+h,active);
            }
            Redraw(rivers,b.ToMesh(rivers.name));Redraw(pulses,pulseGeometry.ToMesh(pulses.name));Redraw(riverFill,fill.ToMesh("Fear desire ribbons"));
        }
        // Adapted from FlowsLayer.Ribbons: nonadditive split lanes plus moving centers.
        void Water(SurfaceMeshBuilder fill,LineMeshBuilder b,LinePoint[] path,float width,float fear,float alpha,int id,bool active)
        {
            float split=-width*.5f+width*fear;
            for(int lane=0;lane<2;lane++)
            {
                float e0=lane==0?-width*.5f:split+.018f,e1=lane==0?split-.018f:width*.5f;
                if(e1<=e0)continue;
                var left=new Vector3[path.Length];var right=new Vector3[path.Length];var middle=new LinePoint[path.Length];
                Color hue=lane==0?FearLane:DesireLane;hue.a=alpha;
                for(int k=0;k<path.Length;k++)
                {
                    Vector3 delta=path[Math.Min(k+1,path.Length-1)].Data-path[Math.Max(0,k-1)].Data;
                    Vector3 normal=new Vector3(-delta.z,0,delta.x).normalized;
                    left[k]=path[k].Data+normal*e0;right[k]=path[k].Data+normal*e1;
                    middle[k]=new LinePoint((left[k]+right[k])*.5f+Vector3.up*.02f,hue,active?1.4f:.65f,0,active?2:1);
                }
                fill.AddBand(left,right,new[]{(Color32)hue},id,.9f);b.AddFlowPath(middle,id,1.6f);
                if(alpha>.05f&&(active||id>=home.Length))
                {
                    int count=active?3:1;
                    for(int spark=0;spark<count;spark++)
                    {
                        float t=Mathf.Repeat(animation*.16f+id*.381966f+lane*.27f+spark/(float)count,1);
                        float index=t*(middle.Length-1);int k=Mathf.Min(middle.Length-2,(int)index);
                        Vector3 head=Vector3.Lerp(middle[k].Data,middle[k+1].Data,index-k)+Vector3.up*.09f;
                        Vector3 tail=middle[Mathf.Max(0,k-2)].Data+Vector3.up*.09f;
                        // HDR only on the hovered or selected cohort's rivers: the trunks' pulses stay in their hue
                        Color light=hue;light.a=active?.95f:.65f;
                        pulseGeometry.AddSegment(tail,head,light,active?3.5f:2.5f,0,0,active?7:1);
                        pulseGeometry.AddSegment(head-Vector3.up*.13f,head+Vector3.up*.13f,light,3,0,0,active?8:1);
                    }
                }
            }
        }
        /// <summary>The focused summit's ring: the selected or hovered group's industry, else the explored or hovered one, lit
        /// into bloom; with none, the summit most is spent at, in its hue. The regimes view draws its own summits, where gold is
        /// the private owners' (HillRegimeLayer): it rings only a focused summit, in white (a gold ring on one the Soviet
        /// state owns read as private).</summary>
        void DrawFocus(int industry)
        {
            var b=new LineMeshBuilder();
            bool selectedIndustry=industry>=0,ownSummits=LandView.PresetId==HillRegimeLayer.View;
            if(industry<0&&Competition!=null&&!ownSummits){industry=0;for(int h=1;h<Competition.IndustrySpending.Length;h++)if(Competition.IndustrySpending[h]>Competition.IndustrySpending[industry])industry=h;}
            if(industry>=0)
            {
                var h=land.Hills[industry];var points=new Vector3[65];
                for(int j=0;j<points.Length;j++){points[j]=h.Surface(j*Mathf.PI/32,.30f);points[j].y=h.Summit.y-h.Height*.15f;}
                b.AddPolyline(points,ownSummits?Color.white:Gold,selectedIndustry?2.8f:2,0,0,selectedIndustry?3.5f:1);
            }
            Redraw(focus,b.ToMesh(focus.name));
        }
        void Tunnel(LineMeshBuilder b,Vector3 a,Vector3 end,Color color,float width)
        {
            var points=new LinePoint[25];color.a=.35f;
            for(int k=0;k<points.Length;k++){float t=k/24f;Vector3 p=Vector3.Lerp(a,end,t);p.y=Mathf.Min(p.y-7*Mathf.Sin(t*Mathf.PI),land.Ground(p.x,p.z)-5);points[k]=new LinePoint(p,color,width);}
            b.AddFlowPath(points,0,1);
        }
        /// <summary>A line mesh in the content at the firms' queue. A glowing one carries the flow's pulse and is added, or
        /// blended when not <paramref name="added"/>: marks that stack past their hue on screen (decision 36).</summary>
        MeshRenderer Render(LineMeshBuilder b,string name,bool glow=false,bool added=true)
        {
            Mesh m=b.ToMesh(name);owned.Add(m);
            Material mat=GraphMaterials.Raw(GraphMaterials.Line(Color.white,1,3210,glow&&added,0,glow?Pulse:0));
            mat.SetFloat("_FlowFreq",2);mat.SetFloat("_FlowSpeed",.3f);materials.Add(mat);
            var r=AddMesh(name,m,mat);r.transform.SetParent(content.transform,false);return r;
        }
        void Replace(MeshRenderer r,LineMeshBuilder b)
        {
            var filter=r.GetComponent<MeshFilter>();Mesh old=filter.sharedMesh;owned.Remove(old);Destroy(old);
            filter.sharedMesh=b.ToMesh(r.name);owned.Add(filter.sharedMesh);
        }
        TextMeshPro Label(string name,Vector3 at,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent!=null?parent:content.transform,false);go.transform.localPosition=at;
            go.transform.localScale=Vector3.one*.15f;var text=go.AddComponent<TextMeshPro>();text.fontSize=24;text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.rectTransform.sizeDelta=new Vector2(30,5);text.color=new Color(.6f,.7f,.8f);return text;
        }
        /// <summary>Gives a label the raised font material. The label must have woken (made under an active parent): before,
        /// it has no material to copy, and its first activation would put the font's own back.</summary>
        TextMeshPro Raised(TextMeshPro t)
        {
            if(raisedText==null&&t.fontSharedMaterial!=null){raisedText=new Material(t.fontSharedMaterial){renderQueue=3252};kept.Add(raisedText);}
            if(raisedText!=null)t.fontSharedMaterial=raisedText;return t;
        }
        /// <summary>A dark plate behind a raised label (it turns and hides with the label): the lanes and halos behind it step
        /// back, so the label reads over them. Sized to the text by <see cref="PlateSize"/> whenever the text changes.</summary>
        Transform Plate(TextMeshPro t)
        {
            if(plateQuad==null)
            {
                var b=new SurfaceMeshBuilder();
                b.AddBand(new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0)},new[]{new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)},new[]{(Color32)WithAlpha(GraphStyle.Background,.75f)},0,1);
                plateQuad=b.ToMesh("Name plate");kept.Add(plateQuad);
                plateMaterial=GraphMaterials.Raw(GraphMaterials.Surface(Color.white,1,3251));plateMaterial.SetFloat("_EdgeSoft",0);kept.Add(plateMaterial);
            }
            var r=AddMesh("Name plate",plateQuad,plateMaterial);r.transform.SetParent(t.transform,false);return r.transform;
        }
        /// <summary>A label's plate in the label's own units: its text with the label system's margin, .35 em sideways and .12 em
        /// above and below (TextMeshPro sets 2.4 local units per em at its font size 24).</summary>
        static Vector3 PlateSize(TextMeshPro t){Vector2 s=t.GetPreferredValues(t.text)+new Vector2(.7f,.24f)*2.4f;return new Vector3(s.x,s.y,1);}
        static void Ring(LineMeshBuilder b,Vector3 at,float radius,Color c,float width,bool vertical=false,float intensity=1)
        {
            var points=new Vector3[25];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI/12;points[i]=at+(vertical?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0):new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            b.AddPolyline(points,c,width,0,0,intensity);
        }
        void Clear(){foreach(var m in owned)if(m)Destroy(m);foreach(var m in materials)if(m)Destroy(m);if(content)Destroy(content);owned.Clear();materials.Clear();actors.Clear();firms.Clear();controls.Clear();orgs.Clear();labels.Clear();badges.Clear();trunkLabels.Clear();trunkPlates.Clear();}
        /// <summary>Sets what is on screen aside (still drawn) for a rebuild: it goes when the new one is complete.</summary>
        void Retire()
        {
            DropRetired();
            retired=content;retiredObjects.AddRange(owned);retiredObjects.AddRange(materials);content=null;
            owned.Clear();materials.Clear();actors.Clear();firms.Clear();controls.Clear();orgs.Clear();labels.Clear();badges.Clear();trunkLabels.Clear();trunkPlates.Clear();
        }
        void DropRetired(){foreach(var o in retiredObjects)if(o)Destroy(o);retiredObjects.Clear();if(retired)Destroy(retired);retired=null;}
        /// <summary>The rivers, their highlights and fill and the focused summit, made once and redrawn in place (DrawRivers,
        /// DrawFocus): a rebuild keeps showing the last ones until the next redraw instead of none.</summary>
        void KeepRivers()
        {
            if(riversRoot)return;
            riversRoot=new GameObject("Rivers (kept across rebuilds)");riversRoot.transform.SetParent(transform,false);
            var empty=new LineMeshBuilder(1);
            rivers=Kept(empty,"Desire fear capital rivers",false);
            rivers.sharedMaterial.SetFloat("_Flow",Pulse);rivers.sharedMaterial.SetFloat("_FlowFreq",3);rivers.sharedMaterial.SetFloat("_FlowSpeed",.6f);
            var surface=new SurfaceMeshBuilder().ToMesh("River ribbons");kept.Add(surface);
            var surfaceMat=GraphMaterials.Raw(GraphMaterials.Surface(Color.white,1,3208,false));kept.Add(surfaceMat);
            riverFill=AddMesh("Split fear and desire water",surface,surfaceMat);riverFill.transform.SetParent(riversRoot.transform,false);
            focus=Kept(empty,"Focused summit",true);
            pulses=Kept(empty,"Moving HDR river highlights",true);pulses.sharedMaterial.SetFloat("_Flow",0);
            // The rivers view's caption: the year's split of household spending in words over a rose / ice bar, turned to
            // the camera with its bar (written by Trunks). Hidden only once its words are raised: they wake while it is active.
            caption=new GameObject("Rivers caption").transform;caption.SetParent(riversRoot.transform,false);
            captionText=Raised(Label("Rivers caption",Vector3.up*2.6f,caption));captionText.transform.localScale=Vector3.one*.9f;captionText.color=new Color(.92f,.95f,1);
            captionNote=Raised(Label("Rivers caption note",Vector3.down*2.6f,caption));captionNote.transform.localScale=Vector3.one*.6f;captionNote.color=new Color(.78f,.83f,.9f);
            captionBar=Kept(empty,"Rivers caption bar",false);captionBar.transform.SetParent(caption,false);captionBar.sharedMaterial.renderQueue=3252;
            caption.gameObject.SetActive(false);
        }
        MeshRenderer Kept(LineMeshBuilder b,string name,bool glow)
        {
            Mesh m=b.ToMesh(name);kept.Add(m);
            Material mat=GraphMaterials.Raw(GraphMaterials.Line(Color.white,1,3210,glow,0,glow?Pulse:0));
            mat.SetFloat("_FlowFreq",2);mat.SetFloat("_FlowSpeed",.3f);kept.Add(mat);
            var r=AddMesh(name,m,mat);r.transform.SetParent(riversRoot.transform,false);return r;
        }
        void Redraw(MeshRenderer r,Mesh mesh){var filter=r.GetComponent<MeshFilter>();kept.Remove(filter.sharedMesh);Destroy(filter.sharedMesh);filter.sharedMesh=mesh;kept.Add(mesh);}
        void OnDestroy(){Clear();DropRetired();foreach(var o in kept)if(o)Destroy(o);kept.Clear();if(riversRoot)Destroy(riversRoot);Positions=null;Competition=null;active=null;}
    }
}
