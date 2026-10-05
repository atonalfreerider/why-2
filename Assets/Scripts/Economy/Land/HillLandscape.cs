using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>An industry's ridge where it meets the crest of the wave (the shown year). Its band's width across the
    /// wave tracks value added; its relief expresses the climb from labor to capital claims within the industry, not a
    /// ranking of industries' social worth. Behind the crest the same ridge runs back through time (<see cref="HillLandscape"/>).</summary>
    public sealed class IndustryHill
    {
        public int Industry, Tier;
        /// <summary>The mound just behind the crest: center, half width across the wave (x), depth along time (z), peak.</summary>
        public Vector3 Center;
        public float Width, Depth, Height;
        internal HillLandscape Land;
        public Vector3 Summit => Surface(0,0);
        /// <summary>The foot on the valley (downhill) side, where the hill meets the industries beneath it.</summary>
        public Vector3 Foot => Surface(HillLandscape.Valley,1);
        public Vector3 Surface(float angle,float radius)
        {
            float r=Mathf.Clamp01(radius),x=Center.x+Mathf.Cos(angle)*Width*r,z=Mathf.Min(Center.z+Mathf.Sin(angle)*Depth*r,Land.CrestZ-.4f);
            if(Tier==0)return new Vector3(x,HillLandscape.Bedrock+Height*(1-r*r),z);
            return new Vector3(x,Land.Rest(Industry,x,z),z);
        }
        /// <summary>The ridge's own relief above the strata at a point (0 for government, which lies in the bedrock).</summary>
        public float Elevation(float x,float z)=>Tier==0?0:Land.Bump(Industry,x,z);
    }

    /// <summary>
    /// The economy as a wave moving forward through time: the expanded cross-section of the end of the causality graph.
    /// Land-local z is time, read off the road itself (<see cref="Z"/>), so the population's lifelines and the
    /// economy share one axis; x runs across the wave from the road (x = 0) up the hillside; y is influence.
    /// <para>Each year's cross-section is a hillside of stacked strata: raw materials at its foot, then manufacturing
    /// and infrastructure, services and tech on top, each standing on the layers beneath it, with the government bedrock
    /// under all of them. A tier's band across the wave is its share of the year's (non-government) value added and each
    /// industry is a ridge within its tier's band; the whole cross-section widens with real value added (the wave
    /// flares as the economy grows). The tier heights are a conceptual production hierarchy, not measured influence.</para>
    /// <para>The crest is the shown year: the present-day hills stand just behind it, the past trails behind as the
    /// wake, and the front falls away into the future. Stable positions permit historical comparison: a year's
    /// cross-section is the same whichever crest is shown.</para>
    /// </summary>
    public sealed class HillLandscape
    {
        public readonly LandSnapshot Source;
        public readonly IndustryHill[] Hills;
        public readonly Vector3[] People;
        public readonly int[] IndustryOf;
        public readonly bool[] Cloud;
        /// <summary>Depth of the government bedrock (social infrastructure) under the whole wave.</summary>
        public const float Bedrock=-18;
        /// <summary>Hill angles: the valley (downhill, toward the road) and the front (toward the crest and the future).</summary>
        public const float Valley=Mathf.PI, Front=Mathf.PI*.5f;
        /// <summary>The hillside's foot beside the road, its width and stack height at the latest data year, the depth
        /// (along time) of the present-day mounds behind the crest (the terrain holds the crest's own cross-section over
        /// it, about three years, so the present stands on the shown year's geometry), and the front's fall into the future.</summary>
        public const float FootX=18, WidthNow=190, HeightNow=58, CrestDepth=6, FrontRun=7;
        /// <summary>Each tier's thickness in the stack (gov is the bedrock; raw, make, services, tech).</summary>
        static readonly float[] TierThickness={0,.16f,.24f,.28f,.32f};
        const float Riser=4;
        /// <summary>The crest's year and z, the wake's first year and z.</summary>
        public readonly int Year, FirstYear;
        public readonly float CrestZ, TailZ;
        /// <summary>The pool of diversified wealth above the summit, and where foreign holders' claims leave.</summary>
        public readonly Vector3 CloudCenter, Abroad;
        public float CloudY=>CloudCenter.y;
        /// <summary>The government programs' footprint: under the hillside, just behind the crest.</summary>
        public float XMin=>-24;
        public float XMax=>Crest.T1[4]+30;
        readonly Slice[] slices;
        readonly int[] tierOf;
        Slice Crest=>slices[slices.Length-1];

        /// <summary>One year's cross-section: total width and stack height, tier bands, each industry's band center,
        /// band half width (value added), ridge half width (legibility floor) and peak.</summary>
        sealed class Slice
        {
            public float W,H,Z;
            public readonly float[] T0=new float[5],T1=new float[5],Step=new float[5];
            public float[] Center,Band,Half,Peak;
            /// <summary>Five-year centered averages of the centers, peaks and steps: the paths lifelines and ridge
            /// lines follow, free of the yearly noise in the accounts (the ribs keep each year's own cross-section).</summary>
            public float[] SmoothCenter,SmoothPeak;public readonly float[] SmoothStep=new float[5];
        }

        public HillLandscape(LandSnapshot source)
        {
            Source=source;Year=source.Year;
            var data=LandService.Model.Data;var industries=data.Industries;int n=industries.Count;
            tierOf=new int[n];for(int i=0;i<n;i++)tierOf[i]=industries[i].TierIndex;
            int last=0;foreach(var ind in industries)last=Math.Max(last,(int)ind.ValueAdded.LastYear);
            FirstYear=1947;int crest=Mathf.Clamp(Year,FirstYear,Math.Max(FirstYear,last));
            double reference=RealValueAdded(data,last);
            slices=new Slice[crest-FirstYear+1];
            for(int y=FirstYear;y<=crest;y++)slices[y-FirstYear]=Build(data,y,reference);
            for(int a=0;a<slices.Length;a++)
            {
                var s=slices[a];s.SmoothCenter=new float[n];s.SmoothPeak=new float[n];int from=Math.Max(0,a-2),to=Math.Min(slices.Length-1,a+2);
                for(int b=from;b<=to;b++)
                {
                    for(int i=0;i<n;i++){s.SmoothCenter[i]+=slices[b].Center[i];s.SmoothPeak[i]+=slices[b].Peak[i];}
                    for(int k=1;k<=4;k++)s.SmoothStep[k]+=slices[b].Step[k];
                }
                float w=1f/(to-from+1);for(int i=0;i<n;i++){s.SmoothCenter[i]*=w;s.SmoothPeak[i]*=w;}for(int k=1;k<=4;k++)s.SmoothStep[k]*=w;
            }
            CrestZ=Z(Year);TailZ=Z(FirstYear);
            var c=Crest;
            CloudCenter=new Vector3(FootX+c.W*.62f,c.H+26,CrestZ-30);
            Abroad=new Vector3(FootX+c.W+42,c.H+30,CrestZ-16);
            Hills=new IndustryHill[n];int govRank=0;
            for(int i=0;i<n;i++)
            {
                var hill=new IndustryHill{Industry=i,Tier=tierOf[i],Land=this,Depth=CrestDepth};
                if(tierOf[i]==0)
                {
                    hill.Center=new Vector3(FootX+c.W*(.30f+.40f*govRank++),Bedrock,CrestZ-CrestDepth);
                    hill.Width=10;hill.Height=1.1f;
                }
                else
                {
                    hill.Center=new Vector3(c.Center[i],0,CrestZ-CrestDepth);hill.Center.y=Ground(hill.Center.x,hill.Center.z);
                    hill.Width=c.Half[i];hill.Height=c.Peak[i];
                }
                Hills[i]=hill;
            }
            var players=source.Players.Players;People=new Vector3[players.Length];IndustryOf=new int[players.Length];Cloud=new bool[players.Length];
            var seats=new List<int>[Hills.Length];for(int i=0;i<seats.Length;i++)seats[i]=new List<int>();
            int cloud=0;
            for(int i=0;i<players.Length;i++)
            {
                Player p=players[i];int h=p.Anchor>=0?p.Anchor:p.AngleIndustry;if(h<0||h>=Hills.Length)h=i%Hills.Length;
                IndustryOf[i]=h;Cloud[i]=p.Group==Group.Top1;
                if(Cloud[i])
                {
                    float a=cloud++*2.399963f;
                    People[i]=CloudCenter+new Vector3(Mathf.Cos(a)*30,Mathf.Sin(a*2)*2,Mathf.Sin(a)*20);
                }
                else seats[h].Add(i);
            }
            // Class is altitude: each rung sits on its own contour of the hill that pays it, from the valley floor
            // (no wage, or the poorest) up the slope to the owners just under the summit. Rungs open toward the valley
            // and the wake; the crest side is left to the firms whose lifelines arrive there.
            for(int h=0;h<seats.Length;h++)
            {
                var row=seats[h];
                row.Sort((a,b)=>{int r=Altitude(players[a]).CompareTo(Altitude(players[b]));return r!=0?r:MeanWealth(players[a]).CompareTo(MeanWealth(players[b]));});
                var perRung=new Dictionary<int,int>();
                for(int k=0;k<row.Count;k++)
                {
                    int i=row[k];int rung=Altitude(players[i]);perRung.TryGetValue(rung,out int slot);perRung[rung]=slot+1;
                    // Rungs run downhill (toward the road); cohorts of one rung step back into the recent past.
                    float a=Valley+Mathf.Min(.25f*slot,1.2f)*(slot%2==0?1:-1)+(h%3-1)*.04f;
                    float wealth=row.Count>1?k/(float)(row.Count-1):.5f;
                    People[i]=OnHill(Hills[h],a,RungRadius[rung]-.05f*wealth,.10f);
                }
            }
        }

        static double RealValueAdded(Data.EconomyData data,int year)
        {
            double sum=0;foreach(var ind in data.Industries)if(ind.TierIndex>0)sum+=data.Real(ind.ValueAdded.GrowthAt(year),year);
            return sum;
        }

        Slice Build(Data.EconomyData data,int year,double reference)
        {
            var industries=data.Industries;int n=industries.Count;
            var s=new Slice{Center=new float[n],Band=new float[n],Half=new float[n],Peak=new float[n],Z=Z(year)};
            float ratio=(float)Math.Max(.01,RealValueAdded(data,year)/Math.Max(1e-9,reference));
            s.W=WidthNow*Mathf.Sqrt(ratio);s.H=HeightNow*Mathf.Pow(ratio,.3f);
            double total=0;foreach(var ind in industries)if(ind.TierIndex>0)total+=Math.Max(0,ind.ValueAdded.GrowthAt(year));
            float cursor=FootX,floor=3.6f*Mathf.Sqrt(s.W/WidthNow);
            for(int k=1;k<=4;k++)
            {
                // Each tier's riser stands in its own gap before the tier's band, so no industry straddles a step.
                cursor+=2*Riser;s.T0[k]=cursor;s.Step[k]=s.H*TierThickness[k];
                for(int i=0;i<n;i++)
                {
                    if(industries[i].TierIndex!=k)continue;
                    double share=total>0?Math.Max(0,industries[i].ValueAdded.GrowthAt(year))/total:0;
                    float band=(float)(.5*share*s.W);
                    s.Band[i]=band;s.Center[i]=cursor+band;s.Half[i]=Mathf.Max(band*1.1f,floor);cursor+=2*band;
                    var ind=industries[i];float owners=Mathf.Clamp((float)(1-ind.CompShare-ind.TaxShare-ind.DepShare),0,.9f);
                    s.Peak[i]=s.H/HeightNow*(3+9*owners+10*Mathf.Sqrt((float)share));
                }
                s.T1[k]=cursor;
            }
            return s;
        }

        // ------------------------------------------------------------------ time

        static float[] zOfYear;
        const int TableFirst=1940,TableLast=2032;

        /// <summary>Land-local z of a calendar year: the road's own position for that year, projected on the land's axis.</summary>
        public static float Z(double year)
        {
            if(zOfYear==null)
            {
                var frame=EconomyStage.Land();var table=new float[TableLast-TableFirst+1];
                for(int y=TableFirst;y<=TableLast;y++)table[y-TableFirst]=frame.Local(EconomyStage.OnRoad(y,0,EconomyStyle.FramingRho)).z;
                zOfYear=table;
            }
            double f=Math.Max(TableFirst,Math.Min(TableLast,year))-TableFirst;int a=Math.Min((int)f,zOfYear.Length-2);
            return Mathf.Lerp(zOfYear[a],zOfYear[a+1],(float)(f-a));
        }

        /// <summary>The calendar year at a land-local z (the inverse of <see cref="Z"/>, clamped to the table).</summary>
        public static double YearAt(float z)
        {
            Z(TableFirst);
            for(int a=0;a<zOfYear.Length-1;a++)
            {
                float z0=zOfYear[a],z1=zOfYear[a+1];
                if(z<=z1||a==zOfYear.Length-2)return TableFirst+a+(Mathf.Abs(z1-z0)<1e-6f?0:Mathf.Clamp01((z-z0)/(z1-z0)));
            }
            return TableLast;
        }

        // ------------------------------------------------------------------ the surface

        static float Smooth(float v){v=Mathf.Clamp01(v);return v*v*(3-2*v);}

        /// <summary>Terrain height at a land-local point: the strata of the year at z, plus every industry's ridge, with
        /// the front falling into the future beyond the crest and the tail rising out of the road before the first year.</summary>
        public float Ground(float x,float z,int tier=0)
        {
            Interp(z,out Slice a,out Slice b,out float f);
            float y=Profile(a,x);if(b!=a)y=Mathf.Lerp(y,Profile(b,x),f);
            return y*Taper(z);
        }

        /// <summary>The strata alone (no ridges) at a point: the hillside the industries stand on.</summary>
        public float Strata(float x,float z)
        {
            Interp(z,out Slice a,out Slice b,out float f);
            float y=Steps(a,x);if(b!=a)y=Mathf.Lerp(y,Steps(b,x),f);
            return y*Taper(z);
        }

        /// <summary>The ridge of one industry above the strata at a point.</summary>
        public float Bump(int industry,float x,float z)
        {
            if(tierOf[industry]==0)return 0;
            Interp(z,out Slice a,out Slice b,out float f);
            float y=Ridge(a,industry,x);if(b!=a)y=Mathf.Lerp(y,Ridge(b,industry,x),f);
            return y*Taper(z);
        }

        float Taper(float z)=>(1-Smooth((z-CrestZ)/FrontRun))*Smooth((z-(TailZ-14))/14);

        void Interp(float z,out Slice a,out Slice b,out float f)
        {
            double year=YearAt(z)-FirstYear;
            if(z>=CrestZ-CrestDepth){a=b=slices[slices.Length-1];f=0;return;}
            if(year<=0){a=b=slices[0];f=0;return;}
            if(year>=slices.Length-1){a=b=slices[slices.Length-1];f=0;return;}
            int i=(int)year;a=slices[i];b=slices[i+1];f=(float)(year-i);
        }

        static float Steps(Slice s,float x)
        {
            float y=0;
            for(int k=1;k<=4;k++)y+=s.Step[k]*Smooth((x-s.T0[k]+2*Riser)/(2*Riser));
            return y*Back(s,x);
        }

        /// <summary>The far side of the summit: the hillside falls away beyond the top tier's band.</summary>
        static float Back(Slice s,float x)=>1-Smooth((x-s.T1[4]-Riser)/16);

        float Ridge(Slice s,int i,float x)
        {
            float u=(x-s.Center[i])/s.Half[i];if(u<=-1||u>=1)return 0;
            float v=1-u*u;return s.Peak[i]*v*v;
        }

        float Profile(Slice s,float x)
        {
            // Ridges merge by their highest (narrow neighbors overlap): a massif, not a stack of bumps.
            float ridge=0;
            for(int i=0;i<tierOf.Length;i++)if(tierOf[i]>0)ridge=Mathf.Max(ridge,Ridge(s,i,x));
            return Steps(s,x)+ridge;
        }

        // ------------------------------------------------------------------ the wake, for drawing

        /// <summary>A year's cross-section (wake rib) at land-local x: the strata plus ridges, without the front's fall.</summary>
        public float SliceHeight(int year,float x)=>Profile(SliceOf(year),x);
        /// <summary>A year's strata alone (the cut face of the wave at that year).</summary>
        public float SliceStrata(int year,float x)=>Steps(SliceOf(year),x);
        /// <summary>A year's span across the wave (foot to summit edge, risers included), the top of each tier's layer
        /// (tiers 1-4), and a tier's band [x0, x1].</summary>
        public float SliceWidth(int year){var s=SliceOf(year);return s.T1[4]-FootX;}
        public float LayerTop(int year,int tier){var s=SliceOf(year);float y=0;for(int k=1;k<=tier;k++)y+=s.Step[k];return y;}
        public Vector2 TierBand(int year,int tier){var s=SliceOf(year);return new Vector2(s.T0[tier],s.T1[tier]);}
        /// <summary>An industry's band (true value-added width) in a year: center and half width.</summary>
        public Vector2 IndustryBand(int year,int industry){var s=SliceOf(year);return new Vector2(s.Center[industry],s.Band[industry]);}
        /// <summary>An industry's ridge half width and peak in a year.</summary>
        public Vector2 IndustryRidge(int year,int industry){var s=SliceOf(year);return new Vector2(s.Half[industry],s.Peak[industry]);}
        /// <summary>The top of a year's layer stack up to a tier (0 = the valley floor) at x: the cut face of the wave.</summary>
        public float LayerAt(int year,int tier,float x)
        {
            var s=SliceOf(year);float y=0;
            for(int k=1;k<=tier;k++)y+=s.Step[k]*Smooth((x-s.T0[k]+2*Riser)/(2*Riser));
            return y*Back(s,x);
        }
        /// <summary>An industry's ridge at a fractional year: x of its band's (five-year smoothed) center and the ridge's half width.</summary>
        public Vector2 RidgeAt(double year,int industry)
        {
            double f=Math.Max(0,Math.Min(slices.Length-1,year-FirstYear));int a=Math.Min((int)f,slices.Length-1),b=Math.Min(a+1,slices.Length-1);
            float t=(float)(f-a);
            return new Vector2(Mathf.Lerp(slices[a].SmoothCenter[industry],slices[b].SmoothCenter[industry],t),Mathf.Lerp(slices[a].Half[industry],slices[b].Half[industry],t));
        }
        /// <summary>The top of an industry's ridge at a fractional year: its tier's layer top plus its own peak. Smooth in
        /// time (no risers), so lifelines and ridge lines ride it without the year-to-year jitter of the steps.</summary>
        public float RidgeTopAt(double year,int industry)
        {
            double f=Math.Max(0,Math.Min(slices.Length-1,year-FirstYear));int a=Math.Min((int)f,slices.Length-1),b=Math.Min(a+1,slices.Length-1);
            float t=(float)(f-a),top=0;int tier=tierOf[industry];
            for(int k=1;k<=tier;k++)top+=Mathf.Lerp(slices[a].SmoothStep[k],slices[b].SmoothStep[k],t);
            return top+Mathf.Lerp(slices[a].SmoothPeak[industry],slices[b].SmoothPeak[industry],t);
        }
        Slice SliceOf(int year)=>slices[Mathf.Clamp(year-FirstYear,0,slices.Length-1)];

        // ------------------------------------------------------------------ places on the crest

        /// <summary>Radius on the hill (1 = its foot) of each altitude rung: valley floor .. just under the summit.</summary>
        public static readonly float[] RungRadius={1.62f,1.34f,1.06f,.82f,.62f,.42f};
        /// <summary>A player's altitude rung: the group's class rung (groups.json), with business owners above the
        /// professional-managerial class because they hold the claims on the hill itself.</summary>
        public static int Altitude(Player p)=>p.Group==Group.Owners?5:Mathf.Clamp(p.Rung,0,4);
        static double MeanWealth(Player p)=>p.Wealth/Math.Max(1,p.Adults.Length);
        /// <summary>The surface of one industry's own hill: the strata plus its ridge alone. People, markets and firms
        /// stand on their own hill's slope, so class altitude is measured on the hill that pays them even where a taller
        /// neighbor's ridge overlaps it in the cross-section.</summary>
        public float Rest(int industry,float x,float z)=>tierOf[industry]==0?Bedrock:Strata(x,z)+Bump(industry,x,z);
        /// <summary>A point on a hill's own slope at an angle and radius (radius 1 = its foot), never past the crest.</summary>
        public Vector3 OnHill(IndustryHill h,float angle,float radius,float lift=0)
        {
            float x=h.Center.x+Mathf.Cos(angle)*h.Width*radius,z=Mathf.Min(h.Center.z+Mathf.Sin(angle)*h.Depth*radius,CrestZ-.4f);
            return new Vector3(x,Rest(h.Industry,x,z)+lift,z);
        }
        /// <summary>A point on the terrain at an angle and radius of a hill (radius 1 = its foot; beyond it the valley),
        /// never past the crest.</summary>
        public Vector3 At(IndustryHill h,float angle,float radius,float lift=0)
        {
            float x=h.Center.x+Mathf.Cos(angle)*h.Width*radius,z=Mathf.Min(h.Center.z+Mathf.Sin(angle)*h.Depth*radius,CrestZ-.4f);
            return new Vector3(x,Ground(x,z)+lift,z);
        }
        /// <summary>The hill's market: where households' rivers arrive on its valley-side slope and its value splits.</summary>
        public Vector3 Market(int industry)=>OnHill(Hills[industry],Valley,.86f,.3f);
        /// <summary>The gold halo above the summit, where the owners' surplus gathers before it rises to its owners.</summary>
        public Vector3 Halo(int industry)=>Hills[industry].Summit+Vector3.up*(1.6f+Hills[industry].Height*.10f);
        /// <summary>Where a named firm stands: on its hill's crest side, where its lifeline arrives from the wake.</summary>
        public Vector3 FirmSeat(int industry,int seat,int count)=>OnHill(Hills[industry],Front+(seat-(count-1)*.5f)*.62f,.42f+.14f*(seat%2));
        /// <summary>The crest's span (foot to summit edge), its value-added width (the wave's size in the shown year) and the
        /// sum of its industries' bands (equal to the value-added width: value added is conserved).</summary>
        public float CrestWidth=>Crest.T1[4]-FootX;
        public float CrestValueWidth=>Crest.W;
        public float CrestBands(){float sum=0;foreach(float b in Crest.Band)sum+=2*b;return sum;}
    }
}
