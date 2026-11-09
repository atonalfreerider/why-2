using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>Semantic drill-down, with explicit evidence boundaries between sectors, firms and synthetic people. It waits as
    /// a chip ("Explore the hill") and opens on a click or when the viewer clicks into the land; it is shown only where the
    /// land is the subject (a sector list does nothing over a chart, a mind or a ladder) and folds back to its chip while
    /// another panel takes the eye. Its top level is the hill's own order: five tiers, tech on top, each opening to its
    /// industries. A group's people (depth 3 and below) are shown only while the panel is open: folded or hidden, it steps
    /// back to the page that lists the group, so the land answers clicks again (folded in the mind, which takes no clicks on
    /// the land, it keeps its path back). A click on the land steps in only where the land is the story (<see cref="Steps"/>).</summary>
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
        /// <summary>The panel's box while it is shown, or its chip's (canvas units of any canvas made by UiFactory.CreateCanvas; empty otherwise).</summary>
        public static UiBox Occupied { get; private set; }=UiBox.Empty;
        /// <summary>The views whose subject is the land itself, where flying to an industry, a firm or a group of households makes sense.</summary>
        static readonly HashSet<string> LandViews=new HashSet<string>{"overview","landscape","capture","people","rivers","roots","companies"};
        /// <summary>Rows of a page below the top level, the most rows a page shows (the top level: five tiers and the
        /// services' eleven industries), the panel's width and the chip's size.</summary>
        const int PageRows=10,MaxRows=16;
        const float Width=354,ChipWidth=170,ChipHeight=30,RowsTop=125,RowStep=23;
        GraphRoot root;
        RectTransform panel,chip,canvasRect;
        /// <summary>The HUD's blocks, measured each frame: the panel stands above the HUD's legend and lifeline readout
        /// in the bottom-left column instead of over them.</summary>
        HudBlocks hud;
        TextMeshProUGUI title, details, pagination;
        UnityEngine.UI.Button evidence,dependency,previous,next;
        readonly List<(string label,Action open)> items=new List<(string,Action)>();
        readonly List<UnityEngine.UI.Button> rows=new List<UnityEngine.UI.Button>();
        readonly List<int> members=new List<int>();
        readonly List<Vector3> memberAt=new List<Vector3>();
        readonly List<int> memberIds=new List<int>();
        int page,year=-1,tier=-1;
        bool dependencies,expanded;
        /// <summary>The view on screen at the last frame, whether the panel or its chip stood over it, and whether the tour ran.</summary>
        string lastView;
        bool shown,touring;
        Mesh mesh;
        Material material;
        GameObject dots;
        Vector2 press;
        public override void Init(GraphRoot graphRoot)
        {
            root=graphRoot;Instance=this;Depth=0;EvidenceOnly=false;Industry=Company=Group=-1;
            var canvas=UiFactory.CreateCanvas("Explore economic hierarchy",46,transform);canvasRect=(RectTransform)canvas.transform;
            chip=(RectTransform)UiFactory.Button(canvas.transform,"Explore the hill","Explore the hill ►",12,()=>{Expand();Refresh();}).transform;
            chip.anchorMin=chip.anchorMax=chip.pivot=Vector2.zero;chip.anchoredPosition=new Vector2(HudKit.Margin,HudKit.Margin);chip.sizeDelta=new Vector2(ChipWidth,ChipHeight);
            panel=UiFactory.Panel(canvas.transform,"Explore",new Color(.018f,.027f,.04f,.94f)).rectTransform;
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.zero;panel.anchoredPosition=new Vector2(HudKit.Margin,HudKit.Margin);panel.sizeDelta=new Vector2(Width,356);
            title=Text("Path",14,10,10,268,25);
            Button("Hide ▼",282,8,62,()=>expanded=false);
            details=Text("Evidence",11,10,37,334,56);details.textWrappingMode=TextWrappingModes.Normal;
            Button("Up",10,97,70,()=>Up());Button("Go into",84,97,112,()=>DiveSelected());
            dependency=Button("Trades with",200,97,144,()=>{dependencies=!dependencies;page=0;Refresh();});
            for(int i=0;i<MaxRows;i++){int row=i;rows.Add(Button("",10,RowsTop+i*RowStep,334,()=>{int n=First+row;if(n<items.Count)items[n].open();}));}
            evidence=Button("Show only measured links",10,0,334,()=>{EvidenceOnly=!EvidenceOnly;Label(evidence,EvidenceOnly?"Show all links (some illustrative)":"Show only measured links");});
            previous=Button("Previous",10,0,83,()=>{page=Math.Max(0,page-1);RefreshRows();});
            pagination=Text("Page",11,105,0,140,22);
            next=Button("Next",262,0,82,()=>{page=Math.Min(Pages-1,page+1);RefreshRows();});
        }
        TextMeshProUGUI Text(string name,int size,float x,float y,float w,float h)
        {
            var t=UiFactory.Text(panel,name,"",size,GraphStyle.Text);t.overflowMode=TextOverflowModes.Truncate;Place(t.rectTransform,x,y,w,h);return t;
        }
        UnityEngine.UI.Button Button(string name,float x,float y,float width,Action action)
        {
            var b=UiFactory.Button(panel,name,name,11,action);b.GetComponentInChildren<TextMeshProUGUI>().overflowMode=TextOverflowModes.Truncate;Place((RectTransform)b.transform,x,y,width,21);return b;
        }
        static void Label(UnityEngine.UI.Button b,string text)=>b.GetComponentInChildren<TextMeshProUGUI>().text=text;
        static void Place(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
        /// <summary>The first item on screen: the top level shows all of its rows, deeper lists a page at a time.</summary>
        int First=>Depth==0?0:page*PageRows;
        int Pages=>Math.Max(1,(items.Count+PageRows-1)/PageRows);
        public override void OnLoaded(GraphRoot graphRoot){year=EconomyState.Year;Refresh();}
        public void EnterSector(int h)
        {
            if(h<0||h>=HillLandscapeLayer.Current.Hills.Length||!Steps(false))return;
            Depth=1;Industry=h;Company=Group=-1;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);
            page=0;dependencies=false;Refresh();var hill=HillLandscapeLayer.Current.Hills[h];
            Fly(hill.Summit-Vector3.up*2,Mathf.Max(28,Mathf.Max(hill.Width,hill.Depth)*3));Expand();
        }
        public void EnterCompany(int f)
        {
            var firms=LandService.Current.Land.Towers;if(f<0||f>=firms.Length||!Steps(true))return;
            Depth=2;Company=f;Industry=firms[f].Industry;Group=-1;
            EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,firms[f].Company,-1);page=0;dependencies=false;Refresh();
            Fly(Vector3.Lerp(HillActivityLayer.CompanyPosition(f),HillActivityLayer.CompanyHalo(f),.45f),24);Expand();
        }
        public void EnterGroup(int p)
        {
            if(p<0||p>=LandService.Current.Players.Players.Length||!Steps(false))return;
            Industry=HillLandscapeLayer.Current.IndustryOf[p];if(Company>=0&&LandService.Current.Land.Towers[Company].Industry!=Industry)Company=-1;Group=p;Depth=3;GroupCenter=HillActivityLayer.Position(p);
            EconomyState.SetSelection(p,Company>=0?LandService.Current.Land.Towers[Company].Company:-1,-1);EconomyState.SetPerson(-1);
            page=0;dependencies=false;Refresh();Fly(GroupCenter+Vector3.up,22);Expand();
        }
        public void EnterPerson(int person)
        {
            if(Group<0||!members.Contains(person))return;
            Depth=4;EconomyState.SetPerson(person);page=0;Refresh();
            int index=memberIds.IndexOf(person);Fly(index>=0?memberAt[index]:GroupCenter,8);Expand();
        }
        public void EnterMind(){if(EconomyState.Person<0)return;Depth=5;Refresh();root.Focus("mind");}
        public void Up()
        {
            if(Depth==5){EnterPerson(EconomyState.Person);return;}
            if(Depth==4){EnterGroup(Group);return;}
            if(Depth==3){if(Company>=0)EnterCompany(Company);else EnterSector(Industry);return;}
            if(Depth==2){EnterSector(Industry);return;}
            UpToOverview();
        }
        void DiveSelected()
        {
            if(EconomyState.SelectedPlayer>=0)EnterGroup(EconomyState.SelectedPlayer);
            else if(HillLandscapeLayer.HoverIndustry>=0)EnterSector(HillLandscapeLayer.HoverIndustry);
        }
        void Fly(Vector3 local,float distance)
        {
            // the land comes back first, under its own title and emphasis, for the explorer's own step out of a view that is not
            // the land's (the mind of the person it walked to) and for a hill clicked behind the families, whose view hides the
            // wave's names; a click on the land in the other views that take one (1972, the encounters, the bedrock, a firm's
            // ring in a company's spending: see Steps) flies there without leaving the view
            string view=LandView.PresetId??"";
            if(!IsLandView(view)&&(shown||view=="section"))root.Focus("landscape",0);
            var pose=root.Rig.Pose;pose.Target=EconomyStage.Land().World(local);pose.Distance=distance;pose.Pitch=25;root.Rig.FlyTo(pose,1.1f);
        }
        /// <summary>The tiers from the top of the hill down (tech first, as the hill stands).</summary>
        static List<Data.Tier> TiersTopDown(Data.EconomyData data)
        {
            var tiers=new List<Data.Tier>(data.Tiers);tiers.Sort((a,b)=>b.Order.CompareTo(a.Order));return tiers;
        }
        static string TierName(Data.Tier t)=>t.Id=="tech"?"Tech":t.Id=="services"?"Services":t.Id=="make"?"Manufacturing & infrastructure":t.Id=="raw"?"Raw materials":t.Id=="gov"?"Government":t.Name;
        void Refresh()
        {
            if(LandService.Current==null)return;
            dependency.interactable=Depth==1||Depth==2;
            items.Clear();var source=LandService.Current;var data=LandService.Model.Data;
            string sector=Industry>=0?data.Industries[Industry].Name:"";
            title.text=Depth==0?"EXPLORE THE HILL":Depth==1?"INDUSTRY · "+sector.ToUpperInvariant():Depth==2?"COMPANY · "+source.Land.Towers[Company].Name.ToUpperInvariant():
                Depth==3?"A GROUP OF HOUSEHOLDS":Depth==4?"ONE SIMULATED PERSON":"INSIDE ONE MIND";
            details.text="Click an industry to fly to it. Heights show what depends on what, not measured value. Double-click a group of people to see who is in it.";
            if(Depth==0)
            {
                // the hill's own order: five tiers, each as wide as its measured share of value added, opening to its industries
                double all=0;foreach(var s in source.Land.Sectors)all+=Math.Max(0,s.ValueAdded);
                foreach(var t in TiersTopDown(data))
                {
                    int index=t.Index;bool open=tier==index;double share=0;
                    for(int h=0;h<source.Land.Sectors.Length;h++)if(data.Industries[h].TierIndex==index)share+=Math.Max(0,source.Land.Sectors[h].ValueAdded);
                    items.Add((TierName(t).ToUpperInvariant()+"  ·  "+LandFacts.Percent(share/Math.Max(1e-9,all))+" of the economy  "+(open?"▼":"►"),()=>{tier=tier==index?-1:index;Refresh();}));
                    if(open)for(int h=0;h<source.Land.Sectors.Length;h++)if(data.Industries[h].TierIndex==index){int ind=h;items.Add((data.Industries[h].Name,()=>EnterSector(ind)));}
                }
            }
            else if(Depth<=2)
            {
                var s=source.Land.Sectors[Industry];
                details.text=sector+" · "+source.Year+"\nValue added "+LandFacts.Money(s.ValueAdded)+", of which wages "+LandFacts.Money(s.Wages)+".\nLinks between industries are measured; company suppliers and org charts are illustrative.";
                if(Depth==2)
                {
                    var f=source.Land.Towers[Company];var record=data.Circuit.Capture[f.Company];
                    details.text=f.Name+" · its FY"+record.Year+" worldwide accounts\nRevenue "+LandFacts.Money(f.Revenue)+"; profit "+LandFacts.Money(f.NetIncome)+".\nThe groups below work in its industry, not necessarily at this company.";
                }
                if(dependencies)
                {
                    var connections=new List<(double value,int h,string label)>();
                    foreach(var edge in data.Circuit.Io.Flows())
                    {
                        var a=data.IndustryById(edge.from);var b=data.IndustryById(edge.to);if(a==null||b==null||a.Index==b.Index)continue;
                        if(a.Index==Industry)connections.Add((edge.value,b.Index,"Sells to "+b.Name+" · "+LandFacts.Money(edge.value)));
                        if(b.Index==Industry)connections.Add((edge.value,a.Index,"Buys from "+a.Name+" · "+LandFacts.Money(edge.value)));
                    }
                    connections.Sort((a,b)=>b.value.CompareTo(a.value));foreach(var c in connections){int h=c.h;items.Add((c.label,()=>EnterSector(h)));}
                }
                else
                {
                    if(Depth==1)for(int f=0;f<source.Land.Towers.Length;f++)if(source.Land.Towers[f].Industry==Industry){int index=f;items.Add(("Company: "+source.Land.Towers[f].Name,()=>EnterCompany(index)));}
                    for(int p=0;p<source.Players.Players.Length;p++)if(HillLandscapeLayer.Current.IndustryOf[p]==Industry){int index=p;items.Add(("Group: "+data.GroupsFile.Groups[(int)source.Players.Players[p].Group].Name,()=>EnterGroup(index)));}
                    if(Depth==2)foreach(var owner in HillLandscapeLayer.OwnerEvidence)if(source.Land.Towers[Company].Name.StartsWith(owner.company,StringComparison.OrdinalIgnoreCase))
                    {var evidence=owner;items.Insert(0,("Owner's filing: "+owner.name,()=>{details.text=evidence.name+" · "+evidence.asOfYear+"\n"+evidence.note;Application.OpenURL(evidence.source);}));}
                }
            }
            else if(Depth==3)
            {
                var p=source.Players.Players[Group];members.Clear();members.AddRange(p.Adults);members.AddRange(p.Children);
                details.text=HillLandscapeLayer.PlayerTitle(p)+"\n"+members.Count+" simulated people, each standing for 100,000 Americans.\nTheir money and families are simulated, not real people.";
                foreach(int id in members){int person=id;string label="Person #"+id;if(LandService.Model.Lives.TryGet(id,source.Year,out PersonYear r))label+=" · age "+r.Age.ToString("F0")+" · net worth $"+r.Wealth.ToString("N0");items.Add((label,()=>EnterPerson(person)));}
            }
            else
            {
                int person=EconomyState.Person;
                details.text="Simulated person #"+person+" · "+source.Year+"\nBalances come from the simulation; motives are assumed.\nA what-if for the group does not change this person's past.";
                items.Add(("See inside their mind",EnterMind));
                var p=LandService.Population.Sim.People[person];
                if(p.Mother>=0){int id=p.Mother;items.Add(("Mother: person #"+id,()=>VisitFamily(id)));}
                if(p.Father>=0){int id=p.Father;items.Add(("Father: person #"+id,()=>VisitFamily(id)));}
                if(LandService.Model.Lives.TryGet(person,source.Year,out PersonYear r)&&r.FamilyDependent&&r.SupportParent>=0){int id=r.SupportParent;items.Add(("Supported by: person #"+id,()=>VisitFamily(id)));}
            }
            page=0;RefreshRows();
        }
        void VisitFamily(int id)
        {
            int group=LandService.Current.Players.PlayerOfPerson[id];
            if(group<0){details.text="Person #"+id+" is not alive in this year; pick another year to find them.";return;}
            EnterGroup(group);EnterPerson(id);
        }
        /// <summary>The rows of the page on screen, then the evidence switch and the pages under them; the panel grows with them.</summary>
        void RefreshRows()
        {
            int first=First,count=Math.Max(0,Math.Min(items.Count-first,Depth==0?MaxRows:PageRows));
            for(int row=0;row<rows.Count;row++){bool on=row<count;rows[row].gameObject.SetActive(on);if(on)Label(rows[row],items[first+row].label);}
            float y=RowsTop+count*RowStep+4;
            Place((RectTransform)evidence.transform,10,y,334,21);y+=27;
            bool paged=Depth>0&&items.Count>PageRows;
            previous.gameObject.SetActive(paged);next.gameObject.SetActive(paged);pagination.gameObject.SetActive(paged);
            if(paged)
            {
                Place((RectTransform)previous.transform,10,y,83,21);Place((RectTransform)next.transform,262,y,82,21);Place(pagination.rectTransform,105,y+3,140,22);
                pagination.text=(page+1)+" / "+Pages+"  ·  "+items.Count+" entries";y+=27;
            }
            panel.sizeDelta=new Vector2(Width,y+4);
            DrawMembers();
        }
        void DrawMembers()
        {
            if(dots)Destroy(dots);if(mesh)Destroy(mesh);if(material)Destroy(material);memberAt.Clear();memberIds.Clear();
            if(Depth<3||Depth==5||Group<0)return;
            var b=new LineMeshBuilder();int start=Depth==4?Math.Max(0,members.IndexOf(EconomyState.Person))/64*64:(page*PageRows/64)*64;
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
        /// <summary>Closes the panel to its chip (the scenario panel opened in the same column); from a group's people it then
        /// steps back to the group's page (<see cref="LeaveMembers"/>), except in the mind, which keeps the path back.</summary>
        public void Fold()=>expanded=false;
        /// <summary>Opens the panel, and closes the scenario panel that shares its column, where the panel stands over the view
        /// on screen; a step taken where it does not (1972's view, the encounters, a company's spending) leaves both as they
        /// are. A step calls it after its flight, which may have brought the land's own view.</summary>
        void Expand(){if(!Shows(LandView.PresetId??""))return;expanded=true;HillControls.Fold();}
        void Update()
        {
            if(!root||!root.IsLoaded)return;
            string view=LandView.PresetId??"";
            bool newView=view!=lastView;lastView=view;
            // the tour narrates the land with nothing chosen (LandViewLayer clears the selection as it starts): a path explored
            // before it would keep an industry's firms lit through its stops
            bool tour=root.TourActive;if(tour&&!touring&&Depth>0)ClearPath();touring=tour;
            if(year!=EconomyState.Year)
            {
                // A new year has other cohorts: drop the stale path and selection. Only a viewer inside the explorer, on a view
                // it stands over, is flown home; otherwise the camera stays (a view that brought the year, 1972's or the way
                // back from it; the tour's; or the slider moving the wave's crest).
                year=EconomyState.Year;
                if(Depth>0&&shown&&!newView&&!tour)UpToOverview();else ClearPath();
            }
            // the people of a group are shown only while the panel is open (their marks, the group's markers gathered and
            // the land's clicks held for them), and in the mind, which has none of these: a viewer who walked into it keeps
            // the path back (the chip, then Up) with the panel folded; a mind is entered only in the mind's view
            if(Depth>=3&&!(Shows(view)&&(expanded||view=="mind")))LeaveMembers();
            else if(Depth==5&&view!="mind"){Depth=4;page=0;Refresh();}
            if(Depth>=4&&EconomyState.Person<0){EnterGroup(Group);view=LandView.PresetId??"";}
            bool show=Shows(view);
            // an inspector, the encounters or the replay takes the eye: an explorer not in use folds to its chip
            bool sidePanel=!PlayerPanel.Occupied.IsEmpty||!PersonInspector.Shown.IsEmpty||!SocialPanel.Occupied.IsEmpty||!ReplayPanel.Occupied.IsEmpty;
            bool open=show&&expanded&&!(sidePanel&&Depth==0);
            shown=show;
            panel.gameObject.SetActive(open);chip.gameObject.SetActive(show&&!open);
            if(show)Dock(open?panel:chip);else Occupied=UiBox.Empty;
            if(dots)dots.SetActive(open&&view!="mind");
            if(!open||Depth<3||Depth==5)return;
            if(!PointerInput.Available||EventSystem.current&&EventSystem.current.IsPointerOverGameObject())return;
            Vector2 pointer=PointerInput.Position;if(PointerInput.PressedThisFrame)press=pointer;
            if(!PointerInput.ReleasedThisFrame||Vector2.Distance(press,pointer)>5)return;
            int hit=-1;float closest=14;
            for(int i=0;i<memberAt.Count;i++){Vector3 screen=root.Rig.Cam.WorldToScreenPoint(EconomyStage.Land().World(memberAt[i]+Vector3.up*.4f));float d=Vector2.Distance(pointer,screen);if(screen.z>0&&d<closest){closest=d;hit=memberIds[i];}}
            if(hit>=0)EnterPerson(hit);
        }
        /// <summary>Stands the panel (or its chip) in the bottom-left column above the HUD's legend and lifeline readout (they
        /// are measured live, so a key that grows with the view or a moved legend is respected), never above the canvas's top.</summary>
        void Dock(RectTransform target)
        {
            hud??=new HudBlocks(root);
            HudFrame f=hud.Measure(canvasRect,UiFactory.CanvasSize);
            float column=f.Margin+target.sizeDelta.x,bottom=f.Margin;
            foreach(var box in new[]{f.Legend,f.Readout})
                if(!box.IsEmpty&&box.X<column)bottom=Mathf.Max(bottom,f.Canvas.y-box.Y+f.Gap);
            bottom=Mathf.Min(bottom,Mathf.Max(f.Margin,f.Canvas.y-target.sizeDelta.y-f.Margin));
            if(Mathf.Abs(target.anchoredPosition.y-bottom)>.5f||Mathf.Abs(target.anchoredPosition.x-f.Margin)>.5f)target.anchoredPosition=new Vector2(f.Margin,bottom);
            Occupied=new UiBox(f.Margin,f.Canvas.y-bottom-target.sizeDelta.y,target.sizeDelta.x,target.sizeDelta.y);
        }
        /// <summary>Whether the explorer (its panel or chip) stands over a view: a desktop navigator of the land, so on a
        /// portrait (phone-video) screen the preset bar and the HUD's blocks fill the room it needs; the mind keeps it only for
        /// a viewer who walked there through it (its Up leads back).</summary>
        bool Shows(string view)=>!root.TourActive&&!ScreenLayout.IsPortrait&&(IsLandView(view)||Depth>=4&&view=="mind");
        /// <summary>A view whose subject is the land: the Wave view is not while it shows the sweep through time (HillSweepLayer).</summary>
        static bool IsLandView(string view)=>LandViews.Contains(view)&&!(view=="overview"&&HillSweepLayer.On);
        /// <summary>Whether a click on the land (a hilltop, a firm's gold ring, a group) takes the explorer into it in the view
        /// on screen; the explorer's own rows are shown only where it does. Not during the tour, whose stop holds the camera
        /// while its card and voice describe it, nor where the land is only the faint ground of another story (the focused
        /// views and the person views): the flight would end in near-invisible hills under that view's title, and the step
        /// would drop the person or the group the view follows. A company's spending view still takes its firm from a click
        /// on the firm's gold ring (<see cref="Company"/>).</summary>
        bool Steps(bool firm)
        {
            string view=LandView.PresetId??"";
            return !root.TourActive&&!PersonFocus.IsView(view)&&(!EconomyViews.Focused(view)||firm&&view=="strategy");
        }
        /// <summary>Back from a group's people to the page that lists the group (its company's or its industry's), without
        /// flying. The selected group stays, and so does a person chosen there (the person views follow them).</summary>
        void LeaveMembers(){Depth=Company>=0?2:1;Group=-1;page=0;dependencies=false;Refresh();}
        /// <summary>Back to the top of the hill with nothing chosen, the camera where it is.</summary>
        void ClearPath(){Depth=0;Industry=Company=Group=-1;page=0;dependencies=false;EconomyState.SetPerson(-1);EconomyState.SetSelection(-1,-1,-1);Refresh();}
        void UpToOverview(){ClearPath();root.Focus("overview");}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);Instance=null;Depth=0;Industry=Company=Group=-1;Occupied=UiBox.Empty;}
    }
}
