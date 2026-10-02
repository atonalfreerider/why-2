using System;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>A transparent, deterministic counterfactual on top of the observed-year census.
    /// Every player runs the same program. Units are group nominal $B, no assumed asset-price appreciation.
    /// Historical records are immutable; this ledger is reset when the historical year changes.</summary>
    public sealed class PlayerMindProgram
    {
        public static PlayerMindProgram Active { get; set; }
        public int Revision { get; private set; }
        public void Changed() { Revision++; }
        public sealed class State
        {
            public string Key;
            public double Assets, Debt, Income, Taxes, Spending, Saving, AllowanceIn, AllowanceOut;
            public float Reason, Fear, Fantasy;
            public double Wealth => Assets - Debt;
        }
        public readonly State[] Players;
        public int Year { get; private set; }
        public double LastResidual { get; private set; }
        public int Steps { get; private set; }
        readonly LandSnapshot baseline;
        readonly int[] dependents;
        readonly System.Collections.Generic.List<(int from,int to)> support = new System.Collections.Generic.List<(int,int)>();
        public PlayerMindProgram(LandSnapshot source)
        {
            baseline=source; Year=source.Year; var players=source.Players.Players;
            Players=new State[players.Length]; dependents=new int[players.Length];
            for(int i=0;i<players.Length;i++)
            {
                Player p=players[i];
                Players[i]=new State { Key=p.Key, Assets=Math.Max(0,p.Wealth+p.Debt), Debt=Math.Max(0,p.Debt),
                    Income=p.Wages+p.Business+p.Capital+p.Transfers, Taxes=p.Taxes,
                    Spending=p.Spending, Saving=p.Saving, Reason=p.Reason,Fear=p.Fear,Fantasy=p.Fantasy };
                dependents[i]=p.Children.Length;
            }
            var lives=LandService.Model?.Lives;
            var sim=LandService.Population?.Sim;
            if(lives!=null&&sim!=null)foreach(var child in sim.People)
            {
                if(!lives.TryGet(child.Index,source.Year,out PersonYear record)||!record.FamilyDependent||record.SupportParent<0)continue;
                int from=source.Players.PlayerOfPerson[record.SupportParent],to=source.Players.PlayerOfPerson[child.Index];
                if(from>=0&&to>=0)support.Add((from,to));
            }
        }
        /// <summary>One annual step: read memory, score competing motives, budget, transfer support,
        /// spend, repay debt or save. Credit is capped; unfinanced desired consumption is forgone.</summary>
        public void Step()
        {
            double before=0,after=0,external=0;
            for(int i=0;i<Players.Length;i++)
            {
                State p=Players[i]; before+=p.Wealth; p.AllowanceIn=p.AllowanceOut=0;
                double disposable=Math.Max(0,p.Income-p.Taxes);
                double buffer=p.Assets/Math.Max(.001,baseline.Players.Players[i].Spending);
                float scarcity=(float)Math.Max(0,1-buffer);
                float fear=Math.Max(0,Math.Min(1,p.Fear+scarcity*.12f));
                // Coefficients are scenario assumptions, not estimates of causal psychological effects.
                double rate=Clamp(.02+.22*p.Reason+.12*fear-.20*p.Fantasy,-.15,.55);
                double desired=disposable*(1-rate);
                double creditRoom=Math.Max(0,disposable*.5-p.Debt);
                p.Spending=Math.Min(desired,disposable+p.Assets+creditRoom);
                p.Saving=disposable-p.Spending;
                if(p.Saving>=0)
                {double repay=Math.Min(p.Debt,p.Saving);p.Debt-=repay;p.Assets+=p.Saving-repay;}
                else
                {double draw=Math.Min(p.Assets,-p.Saving);p.Assets-=draw;p.Debt+=-p.Saving-draw;}
                // Within-player child support is an internal transfer; it must not create income in aggregate.
                double support=Math.Min(disposable*.1,dependents[i]*.2); // assumed $2,000 per representative child
                p.AllowanceOut=p.AllowanceIn=support;
                external+=p.Income-p.Taxes-p.Spending;
                after+=p.Wealth;
            }
            foreach(var transfer in support)
            {
                State from=Players[transfer.from],to=Players[transfer.to];
                // Assumed $2,000 per supported representative adult, capped by the giver's available assets.
                double amount=Math.Min(.2,from.Assets);
                from.Assets-=amount;to.Assets+=amount;from.AllowanceOut+=amount;to.AllowanceIn+=amount;
            }
            LastResidual=(after-before)-external; Year++; Steps++; Changed();
        }
        static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
    }
}
