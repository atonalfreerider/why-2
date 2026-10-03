using System;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>A transparent, deterministic counterfactual on top of the observed-year census.
    /// Every player runs the same program. Units are group nominal $B, no assumed asset-price appreciation.
    /// Historical records are immutable; this ledger is reset when the historical year changes.
    ///
    /// The loop is closed through <see cref="ValueCapture"/>: each year's category spending is traced through the
    /// industries into wages and owners' surplus, and next year's wage and capital incomes move by the change in what
    /// households' own spending generated for each player (other income: transfers, exports, investment and government
    /// demand, stays at the census level). Seller pressure is a scenario control: it adds manufactured want (fantasy)
    /// and sold fear, which lowers saving and tilts budgets toward the categories whose owners' surplus Jev judged most
    /// persuaded. Borrowing beyond the census debt pays interest to the owners of banks and lenders.</summary>
    public sealed class PlayerMindProgram
    {
        public static PlayerMindProgram Active { get; set; }
        public int Revision { get; private set; }
        public void Changed() { Revision++; }
        public sealed class State
        {
            public string Key;
            public double Assets, Debt, Income, Taxes, Spending, Saving, AllowanceIn, AllowanceOut;
            /// <summary>Spending by the six categories; interest on scenario borrowing; this year's capture ledger.</summary>
            public readonly double[] Category = new double[6];
            public double Interest, CapturePaid, CaptureReceived, WagesFromSpending;
            public float Reason, Fear, Fantasy;
            public double Wealth => Assets - Debt;
            public double NetCapture => CaptureReceived - CapturePaid;
        }
        public readonly State[] Players;
        public int Year { get; private set; }
        public double LastResidual { get; private set; }
        public int Steps { get; private set; }
        /// <summary>Seller pressure, 0..1: advertising, status marketing and fear selling aimed at every player (scenario).</summary>
        public float Pressure { get; private set; }
        /// <summary>Interest rate on household debt: personal interest paid over the census's household debt.</summary>
        public readonly double InterestRate;
        /// <summary>The latest capture ledger (the baseline until the first step).</summary>
        public ValueCapture.Ledger Ledger { get; private set; }
        readonly LandSnapshot baseline;
        readonly ValueCapture capture;
        readonly ValueCapture.Ledger baseLedger;
        readonly int[] dependents;
        readonly double[] baseDebt, baseWages, baseOwnersIncome, baseFear, baseFantasy;
        readonly double[][] baseMix;
        readonly System.Collections.Generic.List<(int from,int to)> support = new System.Collections.Generic.List<(int,int)>();
        public PlayerMindProgram(LandSnapshot source)
        {
            baseline=source; Year=source.Year; var players=source.Players.Players; int n=players.Length;
            Players=new State[n]; dependents=new int[n];
            baseDebt=new double[n]; baseWages=new double[n]; baseOwnersIncome=new double[n]; baseFear=new double[n]; baseFantasy=new double[n]; baseMix=new double[n][];
            double debt=0;
            for(int i=0;i<n;i++)
            {
                Player p=players[i];
                Players[i]=new State { Key=p.Key, Assets=Math.Max(0,p.Wealth+p.Debt), Debt=Math.Max(0,p.Debt),
                    Income=p.Wages+p.Business+p.Capital+p.Transfers, Taxes=p.Taxes,
                    Spending=p.Spending, Saving=p.Saving, Reason=p.Reason,Fear=p.Fear,Fantasy=p.Fantasy };
                dependents[i]=p.Children.Length;
                baseDebt[i]=Math.Max(0,p.Debt); baseWages[i]=p.Wages; baseOwnersIncome[i]=p.Business+p.Capital;
                baseFear[i]=p.Fear; baseFantasy[i]=p.Fantasy; debt+=baseDebt[i];
                double total=0; for(int c=0;c<6;c++) total+=Math.Max(0,p.Category[c]);
                baseMix[i]=new double[6];
                for(int c=0;c<6;c++){ baseMix[i][c]=total>0?Math.Max(0,p.Category[c])/total:1/6.0; Players[i].Category[c]=Math.Max(0,p.Spending)*baseMix[i][c]; }
            }
            var circuit=LandService.Model?.Data.Circuit;
            double interest=(circuit?.PersonalOf("interestPaid")??0)+(circuit?.PersonalOf("mortgageInterestPaid")??0);
            InterestRate=debt>0&&interest>0?Math.Min(.2,interest/debt):.05;
            capture=ValueCapture.Get();
            if(capture!=null){baseLedger=capture.Account(source,this);Ledger=baseLedger;Book(baseLedger);}
            var lives=LandService.Model?.Lives;
            var sim=LandService.Population?.Sim;
            if(lives!=null&&sim!=null)foreach(var child in sim.People)
            {
                if(!lives.TryGet(child.Index,source.Year,out PersonYear record)||!record.FamilyDependent||record.SupportParent<0)continue;
                int from=source.Players.PlayerOfPerson[record.SupportParent],to=source.Players.PlayerOfPerson[child.Index];
                if(from>=0&&to>=0)support.Add((from,to));
            }
        }
        /// <summary>Sets the seller pressure (0..1) for the following years.</summary>
        public void SetPressure(float pressure){Pressure=Math.Max(0,Math.Min(1,pressure));Changed();}
        void Book(ValueCapture.Ledger l)
        {
            for(int i=0;i<Players.Length;i++){Players[i].CapturePaid=l.Paid[i];Players[i].CaptureReceived=l.Received[i];Players[i].WagesFromSpending=l.WagesIn[i];}
        }
        /// <summary>One annual step: income from last year's circuit, read memory, score competing motives, budget,
        /// transfer support, spend, pay interest, repay debt or save. Credit is capped; unfinanced desire is forgone.</summary>
        public void Step()
        {
            if(capture!=null&&Steps>0)
            {
                // Last year's spending generated this year's wages and owners' surplus; only the change from the census
                // year moves incomes. Interest paid on scenario borrowing reaches lenders' owners like other capital income.
                var l=capture.Account(baseline,this);Ledger=l;Book(l);
                double interest=0,capitalAll=0;foreach(var s in Players)interest+=s.Interest;
                foreach(var p in baseline.Players.Players)capitalAll+=Math.Max(0,p.Capital);
                for(int i=0;i<Players.Length;i++)
                {
                    Player p=baseline.Players.Players[i];
                    double wages=Math.Max(0,baseWages[i]+l.WagesIn[i]-baseLedger.WagesIn[i]);
                    double owners=Math.Max(0,baseOwnersIncome[i]+l.Received[i]-baseLedger.Received[i]+(capitalAll>0?interest*Math.Max(0,p.Capital)/capitalAll:0));
                    Players[i].Income=wages+owners+p.Transfers;
                }
            }
            double before=0,after=0,external=0;
            for(int i=0;i<Players.Length;i++)
            {
                State p=Players[i]; before+=p.Wealth; p.AllowanceIn=p.AllowanceOut=0;
                p.Interest=InterestRate*Math.Max(0,p.Debt-baseDebt[i]);
                double disposable=Math.Max(0,p.Income-p.Taxes-p.Interest);
                double buffer=p.Assets/Math.Max(.001,baseline.Players.Players[i].Spending);
                float scarcity=(float)Math.Max(0,1-buffer);
                float fear=Math.Max(0,Math.Min(1,p.Fear+scarcity*.12f+Pressure*.15f));
                float fantasy=Math.Max(0,Math.Min(1,p.Fantasy+Pressure*.30f));
                // Coefficients are scenario assumptions, not estimates of causal psychological effects.
                double rate=Clamp(.02+.22*p.Reason+.12*fear-.20*fantasy,-.15,.55);
                double desired=disposable*(1-rate);
                double creditRoom=Math.Max(0,disposable*.5-p.Debt);
                p.Spending=Math.Min(desired,disposable+p.Assets+creditRoom);
                Mix(i,p,fear,fantasy);
                p.Saving=disposable-p.Spending;
                if(p.Saving>=0)
                {double repay=Math.Min(p.Debt,p.Saving);p.Debt-=repay;p.Assets+=p.Saving-repay;}
                else
                {double draw=Math.Min(p.Assets,-p.Saving);p.Assets-=draw;p.Debt+=-p.Saving-draw;}
                // Within-player child support is an internal transfer; it must not create income in aggregate.
                double support=Math.Min(disposable*.1,dependents[i]*.2); // assumed $2,000 per representative child
                p.AllowanceOut=p.AllowanceIn=support;
                external+=p.Income-p.Taxes-p.Interest-p.Spending;
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
        /// <summary>The budget's categories: the census mix, tilted by the extra fantasy toward categories whose surplus
        /// is a manufactured want and by the extra fear toward those sold by fear (Jev's owners'-weighted levers).</summary>
        void Mix(int i,State p,float fear,float fantasy)
        {
            var w=new double[6];double total=0;
            double dFantasy=fantasy-baseFantasy[i],dFear=fear-baseFear[i];
            double meanMade=0,meanFear=0;
            if(capture!=null)for(int c=0;c<6;c++){meanMade+=baseMix[i][c]*capture.CategoryOwnerLevers[c][0];meanFear+=baseMix[i][c]*capture.CategoryOwnerLevers[c][1];}
            for(int c=0;c<6;c++)
            {
                double made=capture!=null?capture.CategoryOwnerLevers[c][0]-meanMade:0,sold=capture!=null?capture.CategoryOwnerLevers[c][1]-meanFear:0;
                w[c]=baseMix[i][c]*Math.Exp(Clamp(2.5*dFantasy*made+2.5*dFear*sold,-3,3));total+=w[c];
            }
            for(int c=0;c<6;c++)p.Category[c]=total>0?p.Spending*w[c]/total:p.Spending/6;
        }
        static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
    }
}
