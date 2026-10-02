using System;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;

namespace Why.Economy.UI
{
    /// <summary>Landscape readout and reversible scenario controls, using the project's existing uGUI system.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillControls : GraphModule
    {
        public static PlayerMindProgram Program => PlayerMindProgram.Active;
        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI text;
        LandSnapshot snapshot;
        float clock;
        bool playing;
        bool expanded;
        bool dataBasis;
        int selected=-1;
        UnityEngine.UI.Button toggle;
        readonly System.Collections.Generic.List<UnityEngine.UI.Button> buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        public override void Init(GraphRoot graphRoot)
        {
            root=graphRoot;
            Canvas canvas=UiFactory.CreateCanvas("Landscape program",43,transform);
            panel=UiFactory.Panel(canvas.transform,"Readout",new Color(.025f,.035f,.055f,.92f)).rectTransform;
            panel.anchorMin=panel.anchorMax=new Vector2(0,1); panel.pivot=new Vector2(0,1);
            panel.anchoredPosition=new Vector2(18,-140); panel.sizeDelta=new Vector2(300,242);
            text=UiFactory.Text(panel,"Circuit","",12,GraphStyle.Text);
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(0,1);
            text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=new Vector2(12,-10);
            text.rectTransform.sizeDelta=new Vector2(276,180);
            Add("Next player",()=>Select(1)); Add("3D mind",()=>root.Focus("mind"));
            Add("Reason +",()=>Adjust(true)); Add("Fear +",()=>Adjust(false));
            Add("Step year",Step); Add("Play / pause",()=>playing=!playing); Add("Reset",Reset);
            Add("Data basis",()=>dataBasis=!dataBasis);
            toggle=UiFactory.Button(panel,"Inspect","Inspect / simulate",11,()=>expanded=!expanded);
            var tr=(RectTransform)toggle.transform;tr.anchorMin=tr.anchorMax=new Vector2(1,1);tr.pivot=new Vector2(1,1);
            tr.anchoredPosition=new Vector2(-8,-8);tr.sizeDelta=new Vector2(116,23);
        }
        void Add(string label,Action action)
        {
            var b=UiFactory.Button(panel,label,label,11,action);int i=buttons.Count;buttons.Add(b);
            var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(10+(i%3)*94,-194-(i/3)*25);r.sizeDelta=new Vector2(90,23);
        }
        public override void OnLoaded(GraphRoot graphRoot){snapshot=LandService.Current;Reset();}
        void Reset(){playing=false;clock=0;PlayerMindProgram.Active=snapshot==null?null:new PlayerMindProgram(snapshot);}
        void Select(int delta)
        {
            int n=snapshot?.Players.Players.Length??0;if(n==0)return;
            EconomyState.SetSelection((Math.Max(-1,EconomyState.SelectedPlayer)+delta+n)%n,-1,-1);
        }
        void Adjust(bool reason)
        {
            if(Program==null)return;
            int i=Mathf.Clamp(EconomyState.SelectedPlayer,0,Program.Players.Length-1);
            if(EconomyState.SelectedPlayer<0)EconomyState.SetSelection(i,-1,-1);
            var p=Program.Players[i];
            if(reason)p.Reason=p.Reason>.95f?0:Mathf.Min(1,p.Reason+.1f);
            else p.Fear=p.Fear>.95f?0:Mathf.Min(1,p.Fear+.1f);
            Program.Changed();
        }
        void Step(){if(Program!=null)Program.Step();}
        void Update()
        {
            if(!root||!root.IsLoaded)return;
            if(snapshot!=LandService.Current)
            {
                bool yearChanged=snapshot==null||snapshot.Year!=LandService.Current.Year;
                snapshot=LandService.Current;if(yearChanged)Reset();
            }
            if(playing){clock+=Time.unscaledDeltaTime;if(clock>=2){clock=0;Step();}}
            if(Program==null||snapshot==null)return;
            if(selected!=EconomyState.SelectedPlayer){selected=EconomyState.SelectedPlayer;if(selected>=0)expanded=true;}
            int i=Mathf.Clamp(EconomyState.SelectedPlayer,0,Program.Players.Length-1);
            var p=Program.Players[i];
            string summary="<b>LANDSCAPE / "+snapshot.Year+"</b>"+(LandService.Model.Data.IsEstimate(snapshot.Year)?" · estimate":" · stored vintage")+
                "\nFootprints: value added · height: hierarchy\nGold: capital · blue: labor · rose/ice: motives\n";
            var owner=HillLandscapeLayer.HoverOwner;
            int sector=HillLandscapeLayer.HoverIndustry;
            if(dataBasis)summary+="\nSaved BEA / BLS / Fed / Census accounts.\nThe September 30, 2026 BEA revision is not yet incorporated. 2026 is estimated.\n\nMountain elevation, portfolios, motives and allowance rates are model assumptions.\nOwnership rings: dated SEC filings; two examples, not a complete ownership census.";
            else if(owner!=null) summary+="\n<b>"+owner.name+" / "+owner.company+"</b>\n"+owner.note;
            else if(sector>=0)
            {
                SectorGeom s=snapshot.Land.Sectors[sector];
                summary+="\n<b>"+LandService.Model.Data.Industries[sector].Name+"</b>\nValue added "+LandFacts.Money(s.ValueAdded)+" · wages "+LandFacts.Money(s.Wages)+
                    "\nInvestment "+LandFacts.Money(snapshot.Money.InvestmentBySector[sector])+"\nOperating surplus "+LandFacts.Money(s.Owners);
            }
            else summary+="\n<b>"+HillLandscapeLayer.PlayerTitle(snapshot.Players.Players[i])+"</b> · group totals\n"+(Program.Steps==0?"Baseline":"Scenario "+Program.Year)+
                ": assets "+LandFacts.Money(p.Assets)+" · debt "+LandFacts.Money(p.Debt)+
                "\nIncome "+LandFacts.Money(p.Income)+" · spend "+LandFacts.Money(p.Spending)+
                "\nReason "+p.Reason.ToString("P0")+" · fear "+p.Fear.ToString("P0")+
                "\nAllowance (assumed) "+LandFacts.Money(p.AllowanceOut);
            text.text=expanded?summary:"<b>LANDSCAPE / "+snapshot.Year+"</b>";
            foreach(var button in buttons)button.gameObject.SetActive(expanded);
            toggle.GetComponentInChildren<TextMeshProUGUI>().text=expanded?"Collapse":"Inspect / simulate";
            panel.gameObject.SetActive(LandView.PresetId!="section");
            // Keep the readout proportional on portrait screens; existing right-hand inspectors remain available.
            float width=Mathf.Min(300,UiFactory.CanvasSize.x-36);panel.sizeDelta=new Vector2(width,expanded?280:40);
        }
        void OnDestroy(){PlayerMindProgram.Active=null;}
    }
}
