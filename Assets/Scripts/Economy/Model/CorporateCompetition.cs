using System;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>Illustrative competition for a player's fixed spending budget, not measured market shares.
    /// The sample firms share half of each sector's budget; the other half is an explicit outside option.
    /// Synthetic appeal and reliability scores permit counterfactual preferences without inventing evidence.</summary>
    public sealed class CorporateCompetition
    {
        public readonly double[] IndustrySpending, FirmReceipts;
        public double OutsideReceipts, TotalSpending;
        readonly LandSnapshot source;
        public CorporateCompetition(LandSnapshot source)
        {
            this.source=source;
            IndustrySpending=new double[source.Land.Sectors.Length];
            FirmReceipts=new double[source.Land.Towers.Length];
        }
        public static double Score(int firm, float fear)
        {
            double appeal=.25+((firm*37+11)%101)/101.0;
            double reliability=.25+((firm*61+43)%101)/101.0;
            return Math.Exp(2*((1-fear)*appeal+fear*reliability));
        }
        public double[] Budget(int player, PlayerMindProgram.State state)
        {
            var p=source.Players.Players[player];var result=new double[IndustrySpending.Length];double total=0;
            for(int h=0;h<result.Length;h++)for(int c=0;c<6;c++)
            {
                double weight=Math.Max(0,state!=null?state.Category[c]:p.Category[c])*LandService.Model.Data.Categories[c].IndustryShare(LandService.Model.Data.Industries[h].Id);
                result[h]+=weight;total+=weight;
            }
            double spending=Math.Max(0,state?.Spending??p.Spending);
            if(total<=0){result[Math.Max(0,p.Anchor) % result.Length]=spending;return result;}
            for(int h=0;h<result.Length;h++)result[h]*=spending/total;
            return result;
        }
        public void Update(PlayerMindProgram program)
        {
            Array.Clear(IndustrySpending,0,IndustrySpending.Length);Array.Clear(FirmReceipts,0,FirmReceipts.Length);
            TotalSpending=OutsideReceipts=0;
            // A scenario program built for another year's cohorts does not apply to this year's.
            if(program!=null&&program.Players.Length!=source.Players.Players.Length)program=null;
            for(int p=0;p<source.Players.Players.Length;p++)
            {
                var state=program?.Players[p];float fear=state?.Fear??source.Players.Players[p].Fear;
                var budget=Budget(p,state);
                for(int h=0;h<budget.Length;h++)
                {
                    IndustrySpending[h]+=budget[h];TotalSpending+=budget[h];double weights=0;
                    for(int f=0;f<FirmReceipts.Length;f++)if(source.Land.Towers[f].Industry==h)weights+=Score(f,fear);
                    if(weights==0){OutsideReceipts+=budget[h];continue;}
                    OutsideReceipts+=budget[h]*.5;
                    for(int f=0;f<FirmReceipts.Length;f++)if(source.Land.Towers[f].Industry==h)
                        FirmReceipts[f]+=budget[h]*.5*Score(f,fear)/weights;
                }
            }
        }
    }
}
