using System;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>Landscape readout and reversible scenario controls ("What if?"), using the project's existing uGUI system: a
    /// chip under the HUD's title (measured, since the title's panel grows with its subtitle) that opens into the panel.
    /// It steps aside during the tour, whose cards carry their own key.</summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillControls : GraphModule
    {
        public static PlayerMindProgram Program => PlayerMindProgram.Active;
        /// <summary>The panel's box while it is shown (canvas units of any canvas made by UiFactory.CreateCanvas; empty otherwise).</summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;
        /// <summary>Closes the panel to its chip (the explorer opened: the two share the left column, and together they no
        /// longer fit beside the key).</summary>
        public static void Fold()=>foldRequested=true;
        static bool foldRequested;
        /// <summary>The closed chip's size and the open panel's (reference pixels).</summary>
        const float ChipWidth = 132, ChipHeight = 38, PanelWidth = 300, PanelHeight = 330;
        GraphRoot root;
        RectTransform panel, canvasRect;
        TextMeshProUGUI text;
        LandSnapshot snapshot;
        HudBlocks hud;
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
            Canvas canvas=UiFactory.CreateCanvas("Landscape program",43,transform);canvasRect=(RectTransform)canvas.transform;
            panel=UiFactory.Panel(canvas.transform,"Readout",new Color(.025f,.035f,.055f,.92f)).rectTransform;
            panel.anchorMin=panel.anchorMax=new Vector2(0,1); panel.pivot=new Vector2(0,1);
            panel.anchoredPosition=new Vector2(HudKit.Margin,-140); panel.sizeDelta=new Vector2(ChipWidth,ChipHeight);
            text=UiFactory.Text(panel,"Circuit","",12,GraphStyle.Text);
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(0,1);
            text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=new Vector2(12,-10);
            text.rectTransform.sizeDelta=new Vector2(276,232);
            Add("Next group",()=>Select(1)); Add("Its mind",()=>root.Focus("mind"));
            Add("Reason +",()=>Adjust(true)); Add("Fear +",()=>Adjust(false));
            Add("Next year",Step); Add("Play / pause",()=>playing=!playing); Add("Reset",Reset);
            Add("Sellers push +",Pressure);
            Add("Sources",()=>dataBasis=!dataBasis);
            toggle=UiFactory.Button(panel,"Inspect","What if? ►",12,()=>{expanded=!expanded;if(expanded)HillExplorer.Instance?.Fold();});
            var tr=(RectTransform)toggle.transform;tr.anchorMin=tr.anchorMax=new Vector2(1,1);tr.pivot=new Vector2(1,1);
            tr.anchoredPosition=new Vector2(-8,-8);tr.sizeDelta=new Vector2(116,23);
        }
        void Add(string label,Action action)
        {
            var b=UiFactory.Button(panel,label,label,11,action);int i=buttons.Count;buttons.Add(b);
            var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(10+(i%3)*94,-246-(i/3)*25);r.sizeDelta=new Vector2(90,23);
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
        /// <summary>Raises the scenario's seller pressure by a quarter (wrapping to zero): more manufactured want and sold fear.</summary>
        void Pressure(){if(Program!=null)Program.SetPressure(Program.Pressure>.99f?0:Program.Pressure+.25f);}
        void Update()
        {
            if(!root||!root.IsLoaded)return;
            if(snapshot!=LandService.Current)
            {
                bool yearChanged=snapshot==null||snapshot.Year!=LandService.Current.Year;
                snapshot=LandService.Current;if(yearChanged)Reset();
            }
            if(playing){clock+=GraphTime.Delta;if(clock>=2){clock=0;Step();}}
            // the tour's cards carry their own key and caveats; a scenario chip over them only competed for the eye
            bool show=!root.TourActive&&LandView.PresetId!="section";
            panel.gameObject.SetActive(show);
            if(!show){Occupied=UiBox.Empty;return;}
            if(Program==null||snapshot==null)return;
            if(foldRequested){foldRequested=false;expanded=false;}
            // a group picked on the land opens the scenario on it; one picked inside the explorer leaves the explorer open
            if(selected!=EconomyState.SelectedPlayer){selected=EconomyState.SelectedPlayer;if(selected>=0&&HillExplorer.Depth==0){expanded=true;HillExplorer.Instance?.Fold();}}
            int i=Mathf.Clamp(EconomyState.SelectedPlayer,0,Program.Players.Length-1);
            var p=Program.Players[i];
            string summary="<b>WHAT IF? · "+snapshot.Year+"</b>"+(LandService.Model.Data.IsEstimate(snapshot.Year)?" · an estimate":" · measured accounts")+
                "\nTry a change for one group of households; Reset undoes it.\n";
            var owner=HillLandscapeLayer.HoverOwner;
            int sector=HillLandscapeLayer.HoverIndustry;
            var ledger=HillCaptureLayer.Shown;var capture=ValueCapture.Get();
            if(dataBasis)summary+="\nAccounts: BEA, BLS, the Federal Reserve and the Census; who keeps each dollar follows the BEA's 2024 table of what industries buy from each other. 2026 is an estimate."+
                "\nHow purchases are sold: "+(capture!=null&&capture.Judged?"judged by an AI model (Jev, "+capture.Model+", "+capture.Judgments.Count+" items), not measured.":"fixed shares for each kind of spending.")+
                "\nAssumed by the model: heights, holdings, motives, seller pressure and allowances. Named owners: two SEC filings; company org charts are illustrative.";
            else if(owner!=null) summary+="\n<b>"+owner.name+" / "+owner.company+"</b>\n"+owner.note;
            else if(sector>=0)
            {
                SectorGeom s=snapshot.Land.Sectors[sector];
                summary+="\n<b>"+LandService.Model.Data.Industries[sector].Name+"</b>\nValue added "+LandFacts.Money(s.ValueAdded)+" · wages "+LandFacts.Money(s.Wages)+
                    "\nInvestment "+LandFacts.Money(snapshot.Money.InvestmentBySector[sector])+"\nOwners' share (operating surplus) "+LandFacts.Money(s.Owners);
                if(ledger!=null&&capture!=null)summary+="\nFrom household spending: wages "+LandFacts.Money(ledger.Wages[sector])+" · owners "+LandFacts.Money(ledger.Owners[sector])+
                    "\nSold by persuasion "+LandFacts.Percent(capture.IndustryPersuaded(sector))+" · to locked-in buyers "+LandFacts.Percent(capture.IndustryLockedIn(sector))+" (judged by Jev)";
            }
            else summary+="\n<b>"+HillLandscapeLayer.PlayerTitle(snapshot.Players.Players[i])+"</b> · the whole group\n"+(Program.Steps==0?"As measured":"Scenario, "+Program.Year)+
                ": assets "+LandFacts.Money(p.Assets)+" · debt "+LandFacts.Money(p.Debt)+
                "\nIncome "+LandFacts.Money(p.Income)+" · spending "+LandFacts.Money(p.Spending)+
                "\nReason "+p.Reason.ToString("P0")+" · fear in its spending "+p.Fear.ToString("P0")+" (model)"+
                "\nNet assets "+LandFacts.Money(p.Wealth)+" · allowance "+LandFacts.Money(p.AllowanceOut)+
                (ledger!=null&&i<ledger.Paid.Length?"\nPays owners "+LandFacts.Money(ledger.Paid[i])+" inside its spending ("+LandFacts.Percent(ledger.Paid[i]/Math.Max(1e-9,ledger.Spent[i]))+")"+
                    "\n  persuaded "+LandFacts.Percent(ledger.PaidPersuaded[i]/Math.Max(1e-9,ledger.Paid[i]))+" · locked in "+LandFacts.Percent(ledger.PaidLockedIn[i]/Math.Max(1e-9,ledger.Paid[i]))+" (judged by Jev)"+
                    "\nCollects "+LandFacts.Money(ledger.Received[i])+" · <b>net "+(ledger.Net(i)>=0?"+":"-")+LandFacts.Money(Math.Abs(ledger.Net(i)))+"</b>":"")+
                (Program.Pressure>0?"\nPressure from sellers "+LandFacts.Percent(Program.Pressure):"");
            text.text=expanded?summary:"";
            text.gameObject.SetActive(expanded);
            foreach(var button in buttons)button.gameObject.SetActive(expanded);
            toggle.GetComponentInChildren<TextMeshProUGUI>().text=expanded?"What if? ▼":"What if? ►";
            // Closed, the chip is just its button; open, the panel keeps proportional on portrait screens. It stands under the
            // HUD's title, whose panel grows with the view's subtitle.
            float width=expanded?Mathf.Min(PanelWidth,UiFactory.CanvasSize.x-2*HudKit.Margin):ChipWidth;panel.sizeDelta=new Vector2(width,expanded?PanelHeight:ChipHeight);
            hud??=new HudBlocks(root);
            HudFrame f=hud.Measure(canvasRect,UiFactory.CanvasSize);
            float top=Mathf.Round(EconomyUiLayout.TitleBottom(f)+f.Gap);
            if(Mathf.Abs(panel.anchoredPosition.y+top)>.5f||Mathf.Abs(panel.anchoredPosition.x-f.Margin)>.5f)panel.anchoredPosition=new Vector2(f.Margin,-top);
            Occupied=new UiBox(panel.anchoredPosition.x,-panel.anchoredPosition.y,width,panel.sizeDelta.y);
        }
        void OnDestroy(){PlayerMindProgram.Active=null;Occupied=UiBox.Empty;}
    }
}
