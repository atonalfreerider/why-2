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
                    flow+=state.Income-state.Taxes-state.Spending;
                }
                double residual=Math.Abs(Total(a)-before-flow);largest=Math.Max(largest,residual);
                Require(residual<1e-6,"Net-worth change must equal external net income less consumption; allowances net to zero");
            }
            var prudent=new PlayerMindProgram(source);var impulsive=new PlayerMindProgram(source);
            for(int i=0;i<prudent.Players.Length;i++){prudent.Players[i].Reason=1;impulsive.Players[i].Reason=0;}
            prudent.Step();impulsive.Step();
            for(int i=0;i<prudent.Players.Length;i++)Require(prudent.Players[i].Spending<=impulsive.Players[i].Spending+1e-8,"Deliberation must affect the decision program");
            Require(GraphScene.Includes(typeof(HillLandscapeLayer)),"Hills must be active in Economy");
            Require(!GraphScene.Includes(typeof(LandscapeLayer)),"Bowl must remain disabled");
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
                " $B; mind sensitivity; scene isolation; "+geometry.LinkCount+" family links; immutable population; 1972 geometry.";
            Debug.Log("[Why] "+result);return result;
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static double Total(PlayerMindProgram p){double n=0;foreach(var s in p.Players)n+=s.Wealth;return n;}
        static double Samples(SmvSimulation sim){double n=0;for(int i=0;i<sim.SampleY.Length;i++)n+=sim.SampleY[i]+sim.SampleRho[i];return n;}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
