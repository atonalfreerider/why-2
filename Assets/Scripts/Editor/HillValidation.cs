using System;
using UnityEngine;
using Why.Economy;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.Humans.Smv;

namespace Why.EditorChecks
{
    /// <summary>Integration checks against the loaded economy, executable through Unity's eval command.
    /// Checks accounting identities, deterministic scenarios, scene isolation and geometry without changing the scene.</summary>
    public static class HillValidation
    {
        public static string Run()
        {
            var source=LandService.Current;
            Require(source!=null,"Economy must finish loading first");
            var land=new HillLandscape(source);
            Require(Math.Abs(land.FootprintArea()-HillLandscape.Area)<.001,"Footprint area must conserve the global GDP scale");
            Require(land.Hills.Length==LandService.Model.Data.Industries.Count,"Every industry needs a hill");
            Require(land.People.Length==source.Players.Players.Length,"Every group needs a place");
            foreach(var lower in land.Hills)foreach(var upper in land.Hills)
                if(upper.Tier>lower.Tier)Require(upper.Summit.y>lower.Summit.y,"Production hierarchy must rise through all five strata");
            int clouds=0;
            for(int i=0;i<land.People.Length;i++)
            {
                Vector3 p=land.People[i];Require(Finite(p.x)&&Finite(p.y)&&Finite(p.z),"Finite player coordinates");
                if(land.Cloud[i]){clouds++;Require(p.y>20,"Diversified wealth must be visibly above the terrain");}
                else Require(p.y>=land.Ground(p.x,p.z,land.Hills[land.IndustryOf[i]].Tier)-.05f,"Households must not be buried below the terrain");
            }
            var a=new PlayerMindProgram(source);var b=new PlayerMindProgram(source);double largest=0;
            for(int year=0;year<30;year++)
            {
                double before=Total(a);a.Step();b.Step();double flow=0;
                for(int i=0;i<a.Players.Length;i++)
                {
                    var state=a.Players[i];Require(state.Assets>=0&&state.Debt>=0,"Nonnegative gross assets and debt");
                    Require(state.Assets==b.Players[i].Assets&&state.Debt==b.Players[i].Debt,"Scenario must be deterministic");
                    flow+=state.Income-state.Taxes-state.Interest-state.Spending;
                }
                double residual=Math.Abs(Total(a)-before-flow);largest=Math.Max(largest,residual);
                Require(residual<1e-6,"Net-worth change must equal external net income less taxes, interest and consumption; allowances net to zero");
            }
            var prudent=new PlayerMindProgram(source);var impulsive=new PlayerMindProgram(source);
            for(int i=0;i<prudent.Players.Length;i++){prudent.Players[i].Reason=1;impulsive.Players[i].Reason=0;}
            prudent.Step();impulsive.Step();
            for(int i=0;i<prudent.Players.Length;i++)Require(prudent.Players[i].Spending<=impulsive.Players[i].Spending+1e-8,"Deliberation must affect the decision program");
            Require(GraphScene.Includes(typeof(HillLandscapeLayer)),"Hills must be active in Economy");
            Require(!GraphScene.Includes(typeof(LandscapeLayer)),"Bowl must remain disabled");
            var market=new CorporateCompetition(source);market.Update(a);
            double receipts=market.OutsideReceipts;foreach(double v in market.FirmReceipts){Require(v>=0,"Nonnegative firm allocation");receipts+=v;}
            Require(Math.Abs(receipts-market.TotalSpending)<1e-6,"Firm allocations plus outside option conserve player spending");
            double desired=0;foreach(var p in a.Players)desired+=p.Spending;
            Require(Math.Abs(desired-market.TotalSpending)<1e-6,"Company competition must not create additional expenditure");
            for(int p=0;p<land.People.Length;p++){var budget=market.Budget(p,a.Players[p]);double total=0;foreach(double v in budget)total+=v;Require(Math.Abs(total-a.Players[p].Spending)<1e-7,"Every player budget is allocated exactly once");}
            foreach(var hill in land.Hills)if(hill.Tier==0)Require(hill.Summit.y<0,"Government lies beneath the landscape");
            string capture=Capture(source,land);
            var pop=LandService.Population;var sim=pop.Sim;
            double checksum=Samples(sim);
            var geometry=new SmvGeometry(sim,pop.CivIndex){FamilyJunctions=true};
            geometry.BuildLifelines();geometry.BuildParentLinks();
            Require(geometry.LinkCount>0,"Family links must be present");
            Require(Samples(sim)==checksum,"Family rendering must not mutate population samples");
            var historical=LandService.BuildBlocking(1972);
            Require(historical.Land.Towers.Length==0,"No present-day firms in historical years");
            Require(Math.Abs(new HillLandscape(historical).FootprintArea()-HillLandscape.Area)<.001,"Historical geometry conserves area");
            string result="PASS: "+land.Hills.Length+" hills, "+land.People.Length+" players, "+clouds+
                " cloud groups; 30-year deterministic ledger; max residual "+largest.ToString("G4")+
                " $B; mind sensitivity; corporate budget conservation; government foundation; scene isolation; "+geometry.LinkCount+" family links; immutable population; 1972 geometry; "+capture;
            Debug.Log("[Why] "+result);return result;
        }
        /// <summary>The value-capture accounting: the Leontief inverse, every dollar's split, each market's balance, the
        /// owners' surplus fully attributed, wages fully paid, seller pressure raising persuaded capture, and class altitude.</summary>
        static string Capture(LandSnapshot source,HillLandscape land)
        {
            var vc=ValueCapture.Get();Require(vc!=null,"Value capture model must load");
            Require(vc.MaxInverseResidual<1e-8,"Leontief inverse must solve (I - A) L = I");
            for(int i=0;i<vc.N;i++)for(int j=0;j<vc.N;j++)Require(vc.Leontief[i,j]>=-1e-9&&vc.Direct[i,j]>=0,"Input requirements must be nonnegative");
            foreach(var item in vc.ItemParts){double sum=0;foreach(double v in item.Value){Require(v>=-1e-9,"Nonnegative parts of a dollar");sum+=v;}Require(Math.Abs(sum-1)<.005,"Every dollar of "+item.Key+" must split into wages, taxes, upkeep, owners and imports");}
            var baseline=new PlayerMindProgram(source);var led=vc.Account(source,baseline);
            double spent=0,parts=0,received=0,self=0,wagesIn=0;
            foreach(double v in led.Spent)spent+=v;foreach(double v in led.Total)parts+=v;
            for(int p=0;p<led.Received.Length;p++){received+=led.Received[p];self+=led.SelfOwned[p];wagesIn+=led.WagesIn[p];}
            double wages=0;foreach(double v in led.Wages)wages+=v;
            Require(Math.Abs(parts-spent)<spent*.005,"Household spending must equal its wages, taxes, upkeep, owners and imports");
            Require(Math.Abs(received+self+led.Abroad-led.OwnersTotal)<1e-6*Math.Max(1,led.OwnersTotal),"Owners' surplus must be fully attributed to holders, homeowners or abroad");
            Require(Math.Abs(wagesIn-wages)<1e-6*Math.Max(1,wages),"Wages generated must be fully paid to players");
            for(int j=0;j<vc.N;j++)
            {
                double inputs=0;for(int k=0;k<vc.N;k++)inputs+=led.Inputs[j,k];
                Require(Math.Abs(led.Output[j]-led.Sold[j]-inputs)<1e-6*Math.Max(1,led.Output[j]),"Each market's output must equal household sales plus sales to other industries");
            }
            var calm=new PlayerMindProgram(source);var pushed=new PlayerMindProgram(source);pushed.SetPressure(1);
            for(int y=0;y<3;y++){calm.Step();pushed.Step();}
            var lc=vc.Account(source,calm);var lp=vc.Account(source,pushed);double pc=0,pp=0;
            for(int p=0;p<lc.PaidPersuaded.Length;p++){pc+=lc.PaidPersuaded[p];pp+=lp.PaidPersuaded[p];}
            Require(pp>pc,"Seller pressure must raise the owners' surplus captured by persuasion");
            int checkedHills=0;
            for(int h=0;h<land.Hills.Length;h++)
            {
                if(land.Hills[h].Tier==0)continue;float low=float.MaxValue,high=float.MinValue;
                for(int p=0;p<land.People.Length;p++)
                {
                    if(land.Cloud[p]||land.IndustryOf[p]!=h)continue;int rung=HillLandscape.Altitude(source.Players.Players[p]);
                    if(rung<=1)low=Mathf.Min(low,land.People[p].y);if(rung>=4)high=Mathf.Max(high,land.People[p].y);
                }
                if(low<float.MaxValue&&high>float.MinValue){checkedHills++;Require(high>low,"Owners and managers must sit above the valley floor of their own hill");}
            }
            return "capture: Leontief residual "+vc.MaxInverseResidual.ToString("G3")+"; "+vc.ItemParts.Count+" item splits; "+
                (vc.Judged?vc.Judgments.Count+" Jev judgments":"no judgments")+"; owners "+LandFacts.Money(led.OwnersTotal)+" of "+LandFacts.Money(spent)+
                " attributed; persuaded capture "+LandFacts.Money(pc)+" -> "+LandFacts.Money(pp)+" under pressure; altitude on "+checkedHills+" hills.";
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static double Total(PlayerMindProgram p){double n=0;foreach(var s in p.Players)n+=s.Wealth;return n;}
        static double Samples(SmvSimulation sim){double n=0;for(int i=0;i<sim.SampleY.Length;i++)n+=sim.SampleY[i]+sim.SampleRho[i];return n;}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
