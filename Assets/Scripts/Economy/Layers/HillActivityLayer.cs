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
        public static Vector3[] Positions { get; private set; }
        public static CorporateCompetition Competition { get; private set; }
        public static Vector3 Position(int i)=>Positions!=null&&i*Subgroups<Positions.Length?Positions[i*Subgroups]:HillLandscapeLayer.Current.People[i];
        static readonly Color Desire=new Color(1,.23f,.48f),Fear=new Color(.25f,.65f,1),Gold=new Color(1,.66f,.14f);
        readonly List<MeshRenderer> actors=new List<MeshRenderer>(), firms=new List<MeshRenderer>(), controls=new List<MeshRenderer>(), orgs=new List<MeshRenderer>();
        float[] haloScale;
        readonly List<Mesh> owned=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly List<TextMeshPro> labels=new List<TextMeshPro>(),badges=new List<TextMeshPro>();
        Vector3[] home, destinations, firmAt;
        int[] recipient;
        double[] riverAmount;
        LineMeshBuilder pulseGeometry;
        MeshRenderer pulses, rivers, riverFill, focus, structure, rivalry;
        LandSnapshot snapshot;
        HillLandscape land;
        GameObject content;
        float animation, refresh;
        int version=-1, selection=-2, lod=-1;
        string view;
        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx){active=this;EconomyStage.Land().Place(transform);}
        public override void Tick(GraphContext ctx,CameraRig rig)
        {
            if(HillLandscapeLayer.Current==null)return;
            if(snapshot!=LandService.Current){Build();}
            if(land==null)return;
            int nextLod=rig.Pose.Distance<35?2:rig.Pose.Distance<100?1:0;
            if(nextLod!=lod){lod=nextLod;UpdateBalances();}
            var program=PlayerMindProgram.Active;
            if(version!=(program?.Revision??-1)||selection!=EconomyState.SelectedPlayer)
            {
                version=program?.Revision??-1;selection=EconomyState.SelectedPlayer;
                Competition.Update(program);PlanTrips();UpdateBalances();refresh=1;
            }
            animation+=Time.deltaTime;
            bool hidden=LandView.PresetId=="mind"||LandView.PresetId=="section";
            content.SetActive(!hidden);if(hidden)return;
            int focusPlayer=selection>=0?selection:HillLandscapeLayer.HoverPlayer;
            int focusIndustry=focusPlayer>=0?land.IndustryOf[focusPlayer]:HillExplorer.Industry>=0?HillExplorer.Industry:HillLandscapeLayer.HoverIndustry;
            for(int p=0;p<actors.Count;p++)
            {
                float progress=(1-Mathf.Cos(animation*(.055f+.015f*(1-FearOf(p)))+p*2.399963f))*.5f;
                Vector3 at=Vector3.Lerp(home[p],destinations[p],progress);
                if(!land.Cloud[p/Subgroups])at.y=land.Ground(at.x,at.z)+.3f;
                if(HillExplorer.Depth>=3&&p/Subgroups==HillExplorer.Group)at=HillExplorer.GroupCenter;
                Positions[p]=at;actors[p].transform.localPosition=at;
                bool memberView=HillExplorer.Depth>=3&&p/Subgroups==HillExplorer.Group;
                bool nearby=lod==0||focusIndustry<0||land.IndustryOf[p/Subgroups]==focusIndustry||Vector3.Distance(rig.Pose.Target,EconomyStage.Land().World(at))<35;
                actors[p].gameObject.SetActive(!memberView&&nearby);
                Color color=Color.Lerp(Desire,Fear,FearOf(p));
                float intensity=p/Subgroups==focusPlayer?3.5f:focusPlayer>=0?.16f:p%145==0?1.4f:.60f;
                actors[p].sharedMaterial.SetColor("_Color",Color.white*intensity);
            }
            for(int f=0;f<firms.Count;f++)
            {
                bool related=snapshot.Land.Towers[f].Industry==focusIndustry,chosen=HillExplorer.Company==f;
                firms[f].transform.localScale=Vector3.one*haloScale[f];
                float glow=chosen?5:related?3.6f:Competition.FirmReceipts[f]>Competition.TotalSpending*.03?2.2f:LandView.PresetId=="capture"?1.6f:.9f;
                firms[f].sharedMaterial.SetColor("_Color",Color.white*(glow*(.9f+.1f*Mathf.Sin(animation*2+f))));
                // Corporate infrastructure by distance: beams and halos always; revenue, margin and value readouts
                // nearer or for the focused sector; the organization inside the firm only close up.
                bool near=Vector3.Distance(rig.Pose.Target,EconomyStage.Land().World(firmAt[f]))<60;
                GraphMaterials.SetAlpha(controls[f].sharedMaterial,chosen||related&&lod>=1||lod>=1&&near?.95f:LandView.PresetId=="capture"?.55f:.22f);
                GraphMaterials.SetAlpha(orgs[f].sharedMaterial,HillExplorer.EvidenceOnly?0:chosen||lod>=2&&near?.9f:related&&lod>=1?.3f:0);
                labels[f].gameObject.SetActive(chosen||(related&&lod>=1)||lod>=1&&near||LandView.PresetId=="capture");
                labels[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].color=related?new Color(1,.78f,.30f):new Color(.40f,.55f,.66f);
                badges[f].gameObject.SetActive(lod>=1||related||snapshot.Land.Towers[f].MarketCap>2000);
                labels[f].text=FirmText(f);
            }
            GraphMaterials.SetAlpha(structure.sharedMaterial,HillExplorer.EvidenceOnly?0:LandView.PresetId=="roots"?.35f:.018f);
            GraphMaterials.SetAlpha(rivalry.sharedMaterial,HillExplorer.EvidenceOnly?0:LandView.PresetId=="capture"?.5f:.07f);
            if(view!=LandView.PresetId){view=LandView.PresetId;refresh=1;}
            refresh+=Time.deltaTime;
            if(refresh>=.2f){refresh=0;DrawRivers(focusPlayer);DrawFocus(focusIndustry);}
        }
        float FearOf(int p)=>PlayerMindProgram.Active?.Players[p/Subgroups].Fear??snapshot.Players.Players[p/Subgroups].Fear;
        void Build()
        {
            Clear();snapshot=LandService.Current;land=HillLandscapeLayer.Current;
            content=new GameObject("Live players and corporate interior");content.transform.SetParent(transform,false);
            home=new Vector3[land.People.Length*Subgroups];
            for(int p=0;p<home.Length;p++){float a=p%Subgroups*Mathf.PI*2/Subgroups;home[p]=land.People[p/Subgroups]+new Vector3(Mathf.Cos(a)*1.1f,0,Mathf.Sin(a)*1.1f);}
            Positions=(Vector3[])home.Clone();
            destinations=new Vector3[home.Length];recipient=new int[home.Length];riverAmount=new double[home.Length];
            Competition=new CorporateCompetition(snapshot);Competition.Update(PlayerMindProgram.Active);
            for(int p=0;p<home.Length;p++){var actor=Render(new LineMeshBuilder(),"Player "+p+" / group "+p/Subgroups);actor.transform.localScale=Vector3.one*.5f;actors.Add(actor);}
            BuildCorporations();
            rivers=Render(new LineMeshBuilder(),"Desire fear capital rivers",false);
            rivers.sharedMaterial.SetFloat("_Flow",1);rivers.sharedMaterial.SetFloat("_FlowFreq",3);rivers.sharedMaterial.SetFloat("_FlowSpeed",.6f);
            var surface=new SurfaceMeshBuilder().ToMesh("River ribbons");owned.Add(surface);
            var surfaceMat=GraphMaterials.Raw(GraphMaterials.Surface(Color.white,1,3208,false));materials.Add(surfaceMat);
            riverFill=AddMesh("Split fear and desire water",surface,surfaceMat);riverFill.transform.SetParent(content.transform,false);
            focus=Render(new LineMeshBuilder(),"Focused summit",true);
            pulses=Render(new LineMeshBuilder(),"Moving HDR river highlights",true);pulses.sharedMaterial.SetFloat("_Flow",0);
            PlanTrips();UpdateBalances();selection=-2;version=-1;
        }
        void PlanTrips()
        {
            for(int p=0;p<home.Length;p++)
            {
                var budget=Competition.Budget(p/Subgroups,PlayerMindProgram.Active?.Players[p/Subgroups]);int best=0;
                double total=0;foreach(double amount in budget)total+=amount;
                double sample=((p*.61803398875+.21)%1)*total,cumulative=0;
                for(int h=0;h<budget.Length;h++){cumulative+=budget[h];if(cumulative>=sample){best=h;break;}}
                recipient[p]=best;riverAmount[p]=budget[best]/Subgroups;
                // A bounded visit toward the budget-weighted spending destination, keeping cohort settlements legible.
                Vector3 target=land.Market(best);
                destinations[p]=Vector3.Lerp(home[p],target,.18f+.20f*(1-FearOf(p)));
                if(land.Cloud[p/Subgroups])destinations[p].y=home[p].y;
            }
        }
        void UpdateBalances()
        {
            for(int p=0;p<actors.Count;p++)
            {
                var state=PlayerMindProgram.Active?.Players[p/Subgroups];var cohort=snapshot.Players.Players[p/Subgroups];
                double assets=state?.Assets??Math.Max(0,cohort.Wealth+cohort.Debt),debt=state?.Debt??cohort.Debt;
                assets/=Subgroups;debt/=Subgroups;
                var b=new LineMeshBuilder();Color motive=Color.Lerp(Desire,Fear,FearOf(p));
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
            var total=new int[land.Hills.Length];foreach(var t in towers)total[t.Industry]++;
            var capture=LandService.Model.Data.Circuit?.Capture;
            for(int f=0;f<towers.Length;f++)
            {
                var tower=towers[f];var hill=land.Hills[tower.Industry];int seat=count[hill.Industry]++;
                // Firms stand on the hill's far slopes, above and behind the households on its valley face.
                float angle=Mathf.PI*.5f+(seat-(total[hill.Industry]-1)*.5f)*.62f;
                Vector3 at=land.At(hill,angle,.42f+.14f*(seat%2));firmAt[f]=at;
                float beam=Beam(tower),revenue=.45f+Mathf.Sqrt((float)Math.Max(0,tower.Revenue))*.07f;
                haloScale[f]=.45f+Mathf.Sqrt((float)Math.Max(0,tower.NetIncome))*.09f;
                // Far: the halo of profit at the top of a beam as tall as the market's price of future profits.
                var node=new LineMeshBuilder();
                Ring(node,Vector3.zero,1,Gold,2.6f,false,4);Ring(node,Vector3.up*.05f,1.18f,Gold,1.2f,false,2.2f);Ring(node,Vector3.zero,.72f,new Color(1,.95f,.85f),1.1f,false,2.6f);
                var renderer=Render(node,tower.Name,true);renderer.transform.localPosition=at+Vector3.up*beam;firms.Add(renderer);
                labels.Add(Label(tower.Name,at+Vector3.up*(beam+2.8f)));
                string ticker=string.IsNullOrWhiteSpace(tower.Ticker)?tower.Name.Substring(0,Math.Min(3,tower.Name.Length)).ToUpperInvariant():tower.Ticker;
                var badge=Label("["+ticker+"]",at+Vector3.up*(beam+1.3f));badge.text="<b>["+ticker+"]</b>";badge.transform.localScale=Vector3.one*.55f;badges.Add(badge);
                // Mid: revenue at the foot, its net margin as a gold arc, the beam marked every $1T of market value.
                var mid=new LineMeshBuilder();
                mid.AddSegment(at,at+Vector3.up*beam,Gold,1.4f,0,0,2.2f);
                Ring(mid,at+Vector3.up*.08f,revenue,new Color(.62f,.78f,.95f,.8f),1.2f,false,1.2f);
                float margin=Mathf.Clamp01(tower.Margin>0?tower.Margin:(float)(tower.NetIncome/Math.Max(1e-6,tower.Revenue)));
                if(margin>0)ArcAt(mid,at+Vector3.up*.12f,revenue,-Mathf.PI*.5f,-Mathf.PI*.5f+margin*Mathf.PI*2,Gold,3,4);
                for(double v=1000;v<tower.MarketCap;v+=1000){Vector3 tick=at+Vector3.up*(beam*(float)(v/tower.MarketCap));mid.AddSegment(tick-Vector3.right*.25f,tick+Vector3.right*.25f,Gold,1,0,0,1.8f);}
                controls.Add(Render(mid,"Revenue margin value / "+tower.Name,true));
                // Near: the organization (schematic): the workforce at the base, then managers, executives and the board
                // narrowing up the beam to the owners' halo. Headcount sets the base; reporting lines are illustrative.
                double staff=capture!=null&&tower.Company<capture.Count?capture[tower.Company].UsEmployees:0;
                orgs.Add(Render(Organization(at,beam,revenue,staff),"Organization / "+tower.Name,true));
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
            rivalry=Render(competition,"Same-sector rivalry (scenario)",true);
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
                    b.AddSegment(p,p+Vector3.up*.18f,colors[level],1.6f,0,0,level==3?3:1.8f);
                    Ring(b,p+Vector3.up*.26f,.06f,colors[level],1,true,level==3?3:1.6f);
                }
                if(level>0)for(int k=0;k<previous.Count;k++)
                {
                    Vector3 lower=previous[k];int up=k*current.Count/previous.Count;
                    b.AddSegment(lower+Vector3.up*.3f,current[up],WithAlpha(colors[level],.45f),.7f,0,0,1.2f);
                }
                previous.Clear();previous.AddRange(current);
            }
            foreach(var seat in previous)b.AddSegment(seat+Vector3.up*.3f,at+Vector3.up*beam,WithAlpha(Gold,.6f),.8f,0,0,2.4f);
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
            for(int p=0;p<home.Length;p++)
            {
                if(riverAmount[p]<=0)continue;
                if(p/Subgroups!=focused && p%15!=0)continue;
                var hill=land.Hills[recipient[p]];bool active=p/Subgroups==focused;
                float alpha=active?.90f:focused>=0?.006f:LandView.PresetId=="rivers"?.18f:.035f;
                Color color=Color.Lerp(Desire,Fear,FearOf(p));color.a=alpha;
                Vector3 end=land.Market(recipient[p]);
                var path=new LinePoint[33];
                for(int k=0;k<path.Length;k++)
                {
                    float t=k/32f;Vector3 at=Vector3.Lerp(Positions[p],end,t);
                    float ground=land.Ground(at.x,at.z)+.25f;
                    at.y=land.Cloud[p/Subgroups]?Mathf.Lerp(Positions[p].y,ground,Mathf.SmoothStep(0,1,t)):ground;
                    path[k]=new LinePoint(at,color,(active?2:1)+(float)Math.Log10(1+riverAmount[p])*.3f,0,active?2.3f:1.0f);
                }
                Water(fill,b,path,.12f+(float)Math.Sqrt(riverAmount[p])*.055f,FearOf(p),active?.8f:focused>=0?.008f:.035f,p,active);
            }
            // Aggregate all accounts into wide industry trunks; only sparse tributaries are shown at overview scale.
            for(int h=0;h<land.Hills.Length;h++)
            {
                if(land.Hills[h].Tier==0)continue;
                double amount=0,af=0;
                for(int p=0;p<home.Length;p++)if(recipient[p]==h){amount+=riverAmount[p];af+=riverAmount[p]*FearOf(p);}
                if(amount<=0)continue;
                var path=new LinePoint[41];
                for(int k=0;k<path.Length;k++)
                {
                    // The household river runs in from the valley and ends at the hill's market, where its value splits
                    // (HillCaptureLayer): the owners' share climbs on as gold.
                    float t=k/40f;Vector3 point=land.At(land.Hills[h],-Mathf.PI*.5f+Mathf.Sin(t*6)*.12f*(1-t),Mathf.Lerp(1.55f,.86f,t),.3f);
                    path[k]=new LinePoint(point,Color.white,1);
                }
                bool active=false;if(focused>=0)for(int p=focused*Subgroups;p<(focused+1)*Subgroups;p++)if(recipient[p]==h)active=true;
                Water(fill,b,path,.25f+(float)Math.Sqrt(amount)*.085f,(float)(af/amount),active?.55f:focused>=0?.015f:LandView.PresetId=="rivers"?.55f:.20f,home.Length+h,active);
            }
            Replace(rivers,b);Replace(pulses,pulseGeometry);
            var filter=riverFill.GetComponent<MeshFilter>();owned.Remove(filter.sharedMesh);Destroy(filter.sharedMesh);
            filter.sharedMesh=fill.ToMesh("Fear desire ribbons");owned.Add(filter.sharedMesh);
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
                Color hue=lane==0?new Color(.06f,.78f,1):new Color(1,.10f,.36f);hue.a=alpha;
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
                        Color light=hue;light.a=active?.95f:.65f;
                        pulseGeometry.AddSegment(tail,head,light,active?3.5f:2.5f,0,0,active?7:4.5f);
                        pulseGeometry.AddSegment(head-Vector3.up*.13f,head+Vector3.up*.13f,light,3,0,0,active?8:5);
                    }
                }
            }
        }
        void DrawFocus(int industry)
        {
            var b=new LineMeshBuilder();
            bool selectedIndustry=industry>=0;
            if(industry<0&&Competition!=null){industry=0;for(int h=1;h<Competition.IndustrySpending.Length;h++)if(Competition.IndustrySpending[h]>Competition.IndustrySpending[industry])industry=h;}
            if(industry>=0)
            {
                var h=land.Hills[industry];var points=new Vector3[65];
                for(int j=0;j<points.Length;j++){points[j]=h.Surface(j*Mathf.PI/32,.30f);points[j].y=h.Summit.y-h.Height*.15f;}
                b.AddPolyline(points,Gold,selectedIndustry?2.8f:2,0,0,selectedIndustry?3.5f:2.3f);
            }
            Replace(focus,b);
        }
        void Tunnel(LineMeshBuilder b,Vector3 a,Vector3 end,Color color,float width)
        {
            var points=new LinePoint[25];color.a=.35f;
            for(int k=0;k<points.Length;k++){float t=k/24f;Vector3 p=Vector3.Lerp(a,end,t);p.y=Mathf.Min(p.y-7*Mathf.Sin(t*Mathf.PI),land.Ground(p.x,p.z)-5);points[k]=new LinePoint(p,color,width);}
            b.AddFlowPath(points,0,1);
        }
        MeshRenderer Render(LineMeshBuilder b,string name,bool glow=false)
        {
            Mesh m=b.ToMesh(name);owned.Add(m);
            Material mat=GraphMaterials.Raw(GraphMaterials.Line(Color.white,1,3210,glow,0,glow?1:0));
            mat.SetFloat("_FlowFreq",2);mat.SetFloat("_FlowSpeed",.3f);materials.Add(mat);
            var r=AddMesh(name,m,mat);r.transform.SetParent(content.transform,false);return r;
        }
        void Replace(MeshRenderer r,LineMeshBuilder b)
        {
            var filter=r.GetComponent<MeshFilter>();Mesh old=filter.sharedMesh;owned.Remove(old);Destroy(old);
            filter.sharedMesh=b.ToMesh(r.name);owned.Add(filter.sharedMesh);
        }
        TextMeshPro Label(string name,Vector3 at)
        {
            var go=new GameObject(name);go.transform.SetParent(content.transform,false);go.transform.localPosition=at;
            go.transform.localScale=Vector3.one*.15f;var text=go.AddComponent<TextMeshPro>();text.fontSize=24;text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.rectTransform.sizeDelta=new Vector2(30,5);text.color=new Color(.6f,.7f,.8f);return text;
        }
        static void Ring(LineMeshBuilder b,Vector3 at,float radius,Color c,float width,bool vertical=false,float intensity=1)
        {
            var points=new Vector3[25];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI/12;points[i]=at+(vertical?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0):new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            b.AddPolyline(points,c,width,0,0,intensity);
        }
        void Clear(){foreach(var m in owned)if(m)Destroy(m);foreach(var m in materials)if(m)Destroy(m);if(content)Destroy(content);owned.Clear();materials.Clear();actors.Clear();firms.Clear();controls.Clear();orgs.Clear();labels.Clear();badges.Clear();}
        void OnDestroy(){Clear();Positions=null;Competition=null;active=null;}
    }
}
