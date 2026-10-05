using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;

namespace Why.Economy.UI
{
    /// <summary>Semantic drill-down, with explicit evidence boundaries between sectors, firms and synthetic people.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillExplorer : GraphModule
    {
        public static HillExplorer Instance { get; private set; }
        public static int Depth { get; private set; }
        public static bool EvidenceOnly { get; private set; }
        public static int Industry { get; private set; }=-1;
        public static int Company { get; private set; }=-1;
        public static int Group { get; private set; }=-1;
        public static Vector3 GroupCenter { get; private set; }
        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI title, details, pagination;
        UnityEngine.UI.Button evidence,dependency;
        readonly List<(string label,Action open)> items=new List<(string,Action)>();
        readonly List<UnityEngine.UI.Button> rows=new List<UnityEngine.UI.Button>();
        readonly List<int> members=new List<int>();
        readonly List<Vector3> memberAt=new List<Vector3>();
        readonly List<int> memberIds=new List<int>();
        int page,year=-1;
        bool dependencies;
        Mesh mesh;
        Material material;
        GameObject dots;
        Vector2 press;
        public override void Init(GraphRoot graphRoot)
        {
            root=graphRoot;Instance=this;Depth=0;EvidenceOnly=false;Industry=Company=Group=-1;
            var canvas=UiFactory.CreateCanvas("Explore economic hierarchy",46,transform);
            panel=UiFactory.Panel(canvas.transform,"Explore",new Color(.018f,.027f,.04f,.94f)).rectTransform;
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.zero;panel.anchoredPosition=new Vector2(18,22);panel.sizeDelta=new Vector2(354,356);
            title=Text("Path",14,10,10,332,25);
            details=Text("Evidence",11,10,39,332,78);details.textWrappingMode=TextWrappingModes.Normal;
            Button("Up",10,119,70,()=>Up());Button("Dive selected",84,119,112,()=>DiveSelected());
            dependency=Button("Dependencies",200,119,144,()=>{dependencies=!dependencies;page=0;Refresh();});
            for(int i=0;i<6;i++){int row=i;rows.Add(Button("",10,147+i*23,334,()=>{int n=page*6+row;if(n<items.Count)items[n].open();}));}
            evidence=Button("Links: sourced + schematic",10,291,334,()=>{EvidenceOnly=!EvidenceOnly;evidence.GetComponentInChildren<TextMeshProUGUI>().text=EvidenceOnly?"Links: sourced sector IO only":"Links: sourced + schematic";});
            Button("Previous",10,321,83,()=>{page=Math.Max(0,page-1);RefreshRows();});
            pagination=Text("Page",11,105,324,140,22);
            Button("Next",262,321,82,()=>{page=Math.Min(Math.Max(0,(items.Count-1)/6),page+1);RefreshRows();});
        }
        TextMeshProUGUI Text(string name,int size,float x,float y,float w,float h)
        {
            var t=UiFactory.Text(panel,name,"",size,GraphStyle.Text);t.overflowMode=TextOverflowModes.Truncate;Place(t.rectTransform,x,y,w,h);return t;
        }
        UnityEngine.UI.Button Button(string name,float x,float y,float width,Action action)
        {
            var b=UiFactory.Button(panel,name,name,11,action);b.GetComponentInChildren<TextMeshProUGUI>().overflowMode=TextOverflowModes.Truncate;Place((RectTransform)b.transform,x,y,width,21);return b;
        }
        static void Place(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
        public override void OnLoaded(GraphRoot graphRoot){year=EconomyState.Year;Refresh();}
        public void EnterSector(int h)
        {
            if(h<0||h>=HillLandscapeLayer.Current.Hills.Length)return;
            Depth=1;Industry=h;Company=Group=-1;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);
            page=0;dependencies=false;Refresh();var hill=HillLandscapeLayer.Current.Hills[h];
            Fly(hill.Summit-Vector3.up*2,Mathf.Max(28,Mathf.Max(hill.Width,hill.Depth)*3));
        }
        public void EnterCompany(int f)
        {
            var firms=LandService.Current.Land.Towers;if(f<0||f>=firms.Length)return;
            Depth=2;Company=f;Industry=firms[f].Industry;Group=-1;
            EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,firms[f].Company,-1);page=0;dependencies=false;Refresh();
            Fly(Vector3.Lerp(HillActivityLayer.CompanyPosition(f),HillActivityLayer.CompanyHalo(f),.45f),24);
        }
        public void EnterGroup(int p)
        {
            if(p<0||p>=LandService.Current.Players.Players.Length)return;
            Industry=HillLandscapeLayer.Current.IndustryOf[p];if(Company>=0&&LandService.Current.Land.Towers[Company].Industry!=Industry)Company=-1;Group=p;Depth=3;GroupCenter=HillActivityLayer.Position(p);
            EconomyState.SetSelection(p,Company>=0?LandService.Current.Land.Towers[Company].Company:-1,-1);EconomyState.SetPerson(-1);
            page=0;dependencies=false;Refresh();Fly(GroupCenter+Vector3.up,22);
        }
        public void EnterPerson(int person)
        {
            if(Group<0||!members.Contains(person))return;
            Depth=4;EconomyState.SetPerson(person);page=0;Refresh();
            int index=memberIds.IndexOf(person);Fly(index>=0?memberAt[index]:GroupCenter,8);
        }
        public void EnterMind(){if(EconomyState.Person<0)return;Depth=5;Refresh();root.Focus("mind");}
        public void Up()
        {
            if(Depth==5){EnterPerson(EconomyState.Person);return;}
            if(Depth==4){EnterGroup(Group);return;}
            if(Depth==3){if(Company>=0)EnterCompany(Company);else EnterSector(Industry);return;}
            if(Depth==2){EnterSector(Industry);return;}
            Depth=0;Industry=Company=Group=-1;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);page=0;dependencies=false;Refresh();root.Focus("overview");
        }
        void DiveSelected()
        {
            if(EconomyState.SelectedPlayer>=0)EnterGroup(EconomyState.SelectedPlayer);
            else if(HillLandscapeLayer.HoverIndustry>=0)EnterSector(HillLandscapeLayer.HoverIndustry);
        }
        void Fly(Vector3 local,float distance)
        {
            if(LandView.PresetId=="mind"||LandView.PresetId=="section")root.Focus("overview",0);
            var pose=root.Rig.Pose;pose.Target=EconomyStage.Land().World(local);pose.Distance=distance;pose.Pitch=25;root.Rig.FlyTo(pose,1.1f);
        }
        void Refresh()
        {
            if(LandService.Current==null)return;
            dependency.interactable=Depth==1||Depth==2;
            items.Clear();var source=LandService.Current;var data=LandService.Model.Data;
            string sector=Industry>=0?data.Industries[Industry].Name:"Economy";
            title.text=Depth==0?"EXPLORE / ECONOMY":Depth==1?"ECONOMY / SECTOR":Depth==2?"SECTOR / COMPANY":Depth==3?"COHORT / INDIVIDUALS":Depth==4?"INDIVIDUAL / MIND":"INDIVIDUAL / MIND PROGRAM";
            details.text="Zoom reveals detail. Select a sector below.\nStored economic accounts; terrain hierarchy is conceptual.\nDouble-click a moving cohort to enter its population records.";
            if(Depth==0)
            {
                for(int h=0;h<source.Land.Sectors.Length;h++){int index=h;items.Add((data.Industries[h].Name,()=>EnterSector(index)));}
            }
            else if(Depth<=2)
            {
                var s=source.Land.Sectors[Industry];
                details.text=sector+" / "+source.Year+"\nStored value added "+LandFacts.Money(s.ValueAdded)+"; wages "+LandFacts.Money(s.Wages)+".\nIO links: sector accounts. Firm suppliers and reporting trees: illustrative.";
                if(Depth==2)
                {
                    var f=source.Land.Towers[Company];var record=data.Circuit.Capture[f.Company];
                    details.text=f.Name+" / stored FY"+record.Year+" worldwide accounts\nRevenue "+LandFacts.Money(f.Revenue)+"; net income "+LandFacts.Money(f.NetIncome)+".\nCohorts below share its sector; specific employment is NOT established.";
                }
                if(dependencies)
                {
                    var connections=new List<(double value,int h,string label)>();
                    foreach(var edge in data.Circuit.Io.Flows())
                    {
                        var a=data.IndustryById(edge.from);var b=data.IndustryById(edge.to);if(a==null||b==null||a.Index==b.Index)continue;
                        if(a.Index==Industry)connections.Add((edge.value,b.Index,"Supplies "+b.Name+" / "+LandFacts.Money(edge.value)));
                        if(b.Index==Industry)connections.Add((edge.value,a.Index,"Buys from "+a.Name+" / "+LandFacts.Money(edge.value)));
                    }
                    connections.Sort((a,b)=>b.value.CompareTo(a.value));foreach(var c in connections){int h=c.h;items.Add((c.label,()=>EnterSector(h)));}
                }
                else
                {
                    if(Depth==1)for(int f=0;f<source.Land.Towers.Length;f++)if(source.Land.Towers[f].Industry==Industry){int index=f;items.Add(("Company / "+source.Land.Towers[f].Name,()=>EnterCompany(index)));}
                    for(int p=0;p<source.Players.Players.Length;p++)if(HillLandscapeLayer.Current.IndustryOf[p]==Industry){int index=p;items.Add(("Cohort / "+data.GroupsFile.Groups[(int)source.Players.Players[p].Group].Name,()=>EnterGroup(index)));}
                    if(Depth==2)foreach(var owner in HillLandscapeLayer.OwnerEvidence)if(source.Land.Towers[Company].Name.StartsWith(owner.company,StringComparison.OrdinalIgnoreCase))
                    {var evidence=owner;items.Insert(0,("Control filing / "+owner.name,()=>{details.text=evidence.name+" / "+evidence.asOfYear+"\n"+evidence.note;Application.OpenURL(evidence.source);}));}
                }
            }
            else if(Depth==3)
            {
                var p=source.Players.Players[Group];members.Clear();members.AddRange(p.Adults);members.AddRange(p.Children);
                details.text=HillLandscapeLayer.PlayerTitle(p)+"\n"+members.Count+" synthetic population records; each represents 100,000 people.\nFinancial history and family ties are simulated, not identifiable people.";
                foreach(int id in members){int person=id;string label="Record #"+id;if(LandService.Model.Lives.TryGet(id,source.Year,out PersonYear r))label+=" / age "+r.Age.ToString("F0")+" / net $"+r.Wealth.ToString("N0");items.Add((label,()=>EnterPerson(person)));}
            }
            else
            {
                int person=EconomyState.Person;
                details.text="Synthetic record #"+person+" / "+source.Year+"\nPerson-level historical balances and assumed motives.\nGroup counterfactual controls do not alter this historical individual.";
                items.Add(("Enter this individual's mind",EnterMind));
                var p=LandService.Population.Sim.People[person];
                if(p.Mother>=0){int id=p.Mother;items.Add(("Mother / record #"+id,()=>VisitFamily(id)));}
                if(p.Father>=0){int id=p.Father;items.Add(("Father / record #"+id,()=>VisitFamily(id)));}
                if(LandService.Model.Lives.TryGet(person,source.Year,out PersonYear r)&&r.FamilyDependent&&r.SupportParent>=0){int id=r.SupportParent;items.Add(("Supported by / record #"+id,()=>VisitFamily(id)));}
            }
            page=0;RefreshRows();
        }
        void VisitFamily(int id)
        {
            int group=LandService.Current.Players.PlayerOfPerson[id];
            if(group<0){details.text="Relative #"+id+" is outside the current year's living census. Open the population timeline to inspect another year.";return;}
            EnterGroup(group);EnterPerson(id);
        }
        void RefreshRows()
        {
            for(int row=0;row<rows.Count;row++){int i=page*6+row;rows[row].gameObject.SetActive(i<items.Count);if(i<items.Count)rows[row].GetComponentInChildren<TextMeshProUGUI>().text=items[i].label;}
            pagination.text=(page+1)+" / "+Math.Max(1,(items.Count+5)/6)+"  ·  "+items.Count+" entries";
            DrawMembers();
        }
        void DrawMembers()
        {
            if(dots)Destroy(dots);if(mesh)Destroy(mesh);if(material)Destroy(material);memberAt.Clear();memberIds.Clear();
            if(Depth<3||Depth==5||Group<0)return;
            var b=new LineMeshBuilder();int start=Depth==4?Math.Max(0,members.IndexOf(EconomyState.Person))/64*64:(page*6/64)*64;
            for(int n=start;n<Math.Min(members.Count,start+64);n++)
            {
                int slot=n-start;Vector3 at=GroupCenter+new Vector3((slot%8-3.5f)*.75f,.8f,(slot/8-3.5f)*.75f);
                memberAt.Add(at);memberIds.Add(members[n]);Color color=members[n]==EconomyState.Person?new Color(1,.7f,.2f):new Color(.3f,.7f,1);
                b.AddSegment(at,at+Vector3.up*.45f,color,2,0,0,2);
                b.AddPolyline(new[]{at+new Vector3(-.10f,.55f,0),at+new Vector3(0,.68f,0),at+new Vector3(.10f,.55f,0),at+new Vector3(-.10f,.55f,0)},color,1.5f,0,0,2);
            }
            mesh=b.ToMesh("Individual population records");material=GraphMaterials.Raw(GraphMaterials.Line(Color.white,1,3250));
            dots=new GameObject("Individual records at cohort");dots.transform.SetParent(transform,false);EconomyStage.Land().Place(dots.transform);
            dots.AddComponent<MeshFilter>().sharedMesh=mesh;dots.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void Update()
        {
            if(!root||!root.IsLoaded)return;
            if(year!=EconomyState.Year)
            {
                // A new year has other cohorts: drop the stale path and selection. Only a viewer inside the explorer is
                // flown home; otherwise the camera stays (a preset's year, or the slider moving the wave's crest).
                year=EconomyState.Year;
                if(Depth>0)UpToOverview();else{Industry=Company=Group=-1;page=0;dependencies=false;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);Refresh();}
            }
            if(Depth>=4&&EconomyState.Person<0)EnterGroup(Group);
            bool show=!root.TourActive&&LandView.PresetId!="section"&&LandView.PresetId!="society"&&LandView.PresetId!="betrayal";panel.gameObject.SetActive(show);
            if(dots)dots.SetActive(show&&LandView.PresetId!="mind");
            if(!show||Depth<3||Depth==5)return;
            var mouse=Mouse.current;if(mouse==null||EventSystem.current&&EventSystem.current.IsPointerOverGameObject())return;
            Vector2 pointer=mouse.position.ReadValue();if(mouse.leftButton.wasPressedThisFrame)press=pointer;
            if(!mouse.leftButton.wasReleasedThisFrame||Vector2.Distance(press,pointer)>5)return;
            int hit=-1;float closest=14;
            for(int i=0;i<memberAt.Count;i++){Vector3 screen=root.Rig.Cam.WorldToScreenPoint(EconomyStage.Land().World(memberAt[i]+Vector3.up*.4f));float d=Vector2.Distance(pointer,screen);if(screen.z>0&&d<closest){closest=d;hit=memberIds[i];}}
            if(hit>=0)EnterPerson(hit);
        }
        void UpToOverview(){Depth=0;Industry=Company=Group=-1;page=0;dependencies=false;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);Refresh();root.Focus("overview");}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);Instance=null;Depth=0;Industry=Company=Group=-1;}
    }
}
