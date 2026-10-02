using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>Moving cohort markers, balance-sheet rings, live demand rivers and an illustrative corporate interior.
    /// Animation is a repeated visual itinerary: only PlayerMindProgram.Step books annual transactions.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillActivityLayer : GraphLayer
    {
        public override int Order=>56;
        public const int Subgroups=5;
        public static Vector3[] Positions { get; private set; }
        public static CorporateCompetition Competition { get; private set; }
        public static Vector3 Position(int i)=>Positions!=null&&i*Subgroups<Positions.Length?Positions[i*Subgroups]:HillLandscapeLayer.Current.People[i];
        static readonly Color Desire=new Color(1,.23f,.48f),Fear=new Color(.25f,.65f,1),Gold=new Color(1,.66f,.14f);
        readonly List<MeshRenderer> actors=new List<MeshRenderer>(), firms=new List<MeshRenderer>(), controls=new List<MeshRenderer>();
        readonly List<Mesh> owned=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly List<TextMeshPro> labels=new List<TextMeshPro>(),badges=new List<TextMeshPro>();
        Vector3[] home, destinations, firmAt;
        int[] recipient;
        double[] riverAmount;
        LineMeshBuilder pulseGeometry;
        MeshRenderer pulses, rivers, riverFill, focus, structure, rivalry, foundation;
        LandSnapshot snapshot;
        HillLandscape land;
        GameObject content;
        float animation, refresh;
        int version=-1, selection=-2;
        string view;
        public override void Prepare(GraphContext ctx) { }
        public override void Upload(GraphContext ctx){EconomyStage.Land().Place(transform);}
        public override void Tick(GraphContext ctx,CameraRig rig)
        {
            if(HillLandscapeLayer.Current==null)return;
            if(snapshot!=LandService.Current){Build();}
            if(land==null)return;
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
            int focusIndustry=focusPlayer>=0?land.IndustryOf[focusPlayer]:HillLandscapeLayer.HoverIndustry;
            for(int p=0;p<actors.Count;p++)
            {
                float progress=(1-Mathf.Cos(animation*(.055f+.015f*(1-FearOf(p)))+p*2.399963f))*.5f;
                Vector3 at=Vector3.Lerp(home[p],destinations[p],progress);
                if(!land.Cloud[p/Subgroups])at.y=land.Ground(at.x,at.z)+.3f;
                Positions[p]=at;actors[p].transform.localPosition=at;
                Color color=Color.Lerp(Desire,Fear,FearOf(p));
                float intensity=p/Subgroups==focusPlayer?3.5f:focusPlayer>=0?.16f:p%145==0?1.4f:.60f;
                actors[p].sharedMaterial.SetColor("_Color",Color.white*intensity);
            }
            for(int f=0;f<firms.Count;f++)
            {
                bool related=snapshot.Land.Towers[f].Industry==focusIndustry;
                float receipts=(float)Math.Log10(1+Competition.FirmReceipts[f]);
                firms[f].transform.localScale=Vector3.one*(.5f+receipts*.18f);
                float glow=related?4:Competition.FirmReceipts[f]>Competition.TotalSpending*.03?2.2f:LandView.PresetId=="capture"?1.4f:.35f;
                firms[f].sharedMaterial.SetColor("_Color",Gold*(glow*(.9f+.1f*Mathf.Sin(animation*2+f))));
                GraphMaterials.SetAlpha(controls[f].sharedMaterial,related?.55f:LandView.PresetId=="capture"?.18f:.035f);
                labels[f].gameObject.SetActive(related||LandView.PresetId=="capture");
                labels[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].transform.rotation=rig.Cam.transform.rotation;
                badges[f].color=related?new Color(1,.78f,.30f):new Color(.40f,.55f,.66f);
                badges[f].gameObject.SetActive(true);
                labels[f].text=snapshot.Land.Towers[f].Name+"\nScenario demand "+LandFacts.Money(Competition.FirmReceipts[f]);
            }
            GraphMaterials.SetAlpha(structure.sharedMaterial,LandView.PresetId=="roots"?.35f:.018f);
            GraphMaterials.SetAlpha(rivalry.sharedMaterial,LandView.PresetId=="capture"?.5f:.07f);
            GraphMaterials.SetAlpha(foundation.sharedMaterial,LandView.PresetId=="roots"?.3f:.022f);
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
            BuildCorporations();BuildGovernment();
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
                Vector3 target=land.Hills[best].Foot;
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
                Ring(b,Vector3.zero,.35f+(float)Math.Log10(1+assets)*.20f,Gold,1.4f);
                if(debt>0)Ring(b,Vector3.up*.12f,.28f+(float)Math.Log10(1+debt)*.18f,Desire,1.4f);
                for(int child=0;child<Math.Min(4,(cohort.Children.Length+p%Subgroups)/Subgroups);child++)
                {float a=child*2.4f;Vector3 c=new Vector3(Mathf.Cos(a)*.9f,0,Mathf.Sin(a)*.9f);b.AddSegment(c,c+Vector3.up*.5f,Color.white,1,0,0);}
                Replace(actors[p],b);
            }
        }
        void BuildCorporations()
        {
            var branches=new LineMeshBuilder();var competition=new LineMeshBuilder();
            var towers=snapshot.Land.Towers;firmAt=new Vector3[towers.Length];var count=new int[land.Hills.Length];
            for(int f=0;f<towers.Length;f++)
            {
                var hill=land.Hills[towers[f].Industry];int seat=count[hill.Industry]++;
                float angle=seat*2.4f;Vector3 at=hill.Surface(angle,.30f+.12f*(seat%3));
                at.y=land.Ground(at.x,at.z)-3;firmAt[f]=at;
                var node=new LineMeshBuilder();Ring(node,Vector3.zero,1,Color.white,1.8f);Ring(node,Vector3.zero,.7f,Color.white,1.4f,true);
                var renderer=Render(node,towers[f].Name,true);renderer.transform.localPosition=at;firms.Add(renderer);
                labels.Add(Label(towers[f].Name,at+Vector3.up*3.2f));
                string ticker=string.IsNullOrWhiteSpace(towers[f].Ticker)?towers[f].Name.Substring(0,Math.Min(3,towers[f].Name.Length)).ToUpperInvariant():towers[f].Ticker;
                var badge=Label("["+ticker+"]",at+Vector3.up*1.4f);badge.text="<b>["+ticker+"]</b>";badge.transform.localScale=Vector3.one*.55f;badges.Add(badge);
                var tree=new LineMeshBuilder();
                tree.AddSegment(at,hill.Summit,Gold,1,0,0);
                for(int team=0;team<3;team++)
                {
                    Vector3 manager=at+new Vector3(Mathf.Cos(team*2.1f)*2,-2,Mathf.Sin(team*2.1f)*2);
                    manager.y=Mathf.Min(manager.y,land.Ground(manager.x,manager.z)-4);
                    tree.AddSegment(manager,at,Fear,.8f,0,0);Ring(tree,manager,.22f,Fear,1);
                    for(int worker=0;worker<3;worker++)
                    {Vector3 employee=manager+new Vector3(worker-1,-1.5f,1.5f);tree.AddSegment(employee,manager,Fear,.7f,0,0);Ring(tree,employee,.12f,Fear,.8f,true);}
                }
                controls.Add(Render(tree,"Control structure / "+towers[f].Name));
                for(int prior=0;prior<f;prior++)if(towers[prior].Industry==hill.Industry)
                    competition.AddPolyline(new[]{firmAt[prior],(firmAt[prior]+at)*.5f-Vector3.up*2,at},Desire,1,0,0,1,1);
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
            structure=Render(branches,"Employee management ownership and inferred supply allocations");
            rivalry=Render(competition,"Same-sector rivalry (scenario)",true);
        }
        void BuildGovernment()
        {
            var b=new LineMeshBuilder();
            foreach(var h in land.Hills)if(h.Tier>0)
            {
                Vector3 below=new Vector3(h.Center.x,-18,h.Center.z);
                b.AddSegment(below,new Vector3(h.Center.x,HillLandscape.Lowland(h.Center.x,h.Center.z)-3,h.Center.z),Fear,.65f,0,0);
                foreach(var gov in land.Hills)if(gov.Tier==0)b.AddSegment(gov.Summit,below,Fear,.7f,0,0);
            }
            foundation=Render(b,"Government foundation (conceptual support)");
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
                Vector3 end=hill.Foot;
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
                    float t=k/40f;Vector3 point=land.Hills[h].Surface(-Mathf.PI*.5f+Mathf.Sin(t*6)*.12f,1-t);
                    point.y=land.Ground(point.x,point.z)+.3f;path[k]=new LinePoint(point,Color.white,1);
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
        static void Ring(LineMeshBuilder b,Vector3 at,float radius,Color c,float width,bool vertical=false)
        {
            var points=new Vector3[25];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI/12;points[i]=at+(vertical?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0):new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            b.AddPolyline(points,c,width,0,0);
        }
        void Clear(){foreach(var m in owned)if(m)Destroy(m);foreach(var m in materials)if(m)Destroy(m);if(content)Destroy(content);owned.Clear();materials.Clear();actors.Clear();firms.Clear();controls.Clear();labels.Clear();badges.Clear();}
        void OnDestroy(){Clear();Positions=null;Competition=null;}
    }
}
