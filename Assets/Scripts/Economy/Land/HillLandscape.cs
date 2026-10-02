using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>An industry's mountain mass. Footprint area tracks value added; elevation expresses the
    /// climb from labor to capital claims within that industry, not a ranking of industries' social worth.</summary>
    public sealed class IndustryHill
    {
        public int Industry, Tier;
        public Vector3 Center;
        public float Width, Depth, Height;
        public Vector3 Summit => Surface(0,0);
        public Vector3 Foot => Surface(-Mathf.PI*.5f,1);
        public Vector3 Surface(float angle,float radius)
        {
            float r=Mathf.Clamp01(radius),x=Center.x+Mathf.Cos(angle)*Width*r,z=Center.z+Mathf.Sin(angle)*Depth*r;
            float ridge=1+.22f*r*Mathf.Sin(angle*3+Industry)+.14f*r*Mathf.Cos(angle*7+r*9);
            float elevation=Height*Mathf.Pow(1-r*r,2)*ridge;
            return new Vector3(x,(Tier==0?-18:HillLandscape.Lowland(x,z))+elevation,z);
        }
        public float Elevation(float x,float z)
        {
            float dx=(x-Center.x)/Width,dz=(z-Center.z)/Depth,r=Mathf.Sqrt(dx*dx+dz*dz);
            return r>=1?0:Surface(Mathf.Atan2(dz,dx),r).y-HillLandscape.Lowland(x,z);
        }
    }

    /// <summary>A connected, deterministic terrain projection of the existing census and accounts.
    /// Horizontal topology groups related production; role and capital claims establish vertical hierarchy.</summary>
    public sealed class HillLandscape
    {
        public readonly LandSnapshot Source;
        public readonly IndustryHill[] Hills;
        public readonly Vector3[] People;
        public readonly int[] IndustryOf;
        public readonly bool[] Cloud;
        public const float CloudY=82;
        public const float CrownY=74;
        public const float Area=18000;
        // Stable topology, intentionally not geographical. Historical years retain the same centers.
        public HillLandscape(LandSnapshot source)
        {
            Source=source; Hills=new IndustryHill[source.Land.Sectors.Length];
            for(int i=0;i<Hills.Length;i++)
            {
                SectorGeom s=source.Land.Sectors[i];
                float radius=Mathf.Sqrt((float)(Area*s.ValueAdded/source.Land.Gdp/Math.PI));
                int rank=0,count=0;foreach(var other in source.Land.Sectors)if(other.Tier==s.Tier){if(other.Industry<i)rank++;count++;}
                float spacing=count>5?14:count==2?55:26;
                Vector2 center=new Vector2((rank-(count-1)*.5f)*spacing,24*(int)s.Tier-52+Mathf.Sin(i*2.4f)*2);
                float share=(float)(s.Owners/Math.Max(1,s.ValueAdded));
                Hills[i]=new IndustryHill{Industry=i,Tier=(int)s.Tier,Center=new Vector3(center.x,0,center.y),
                    Width=radius*1.18f,Depth=radius/1.18f,
                    Height=s.Tier==Tier.Gov?1.1f:4+8*share+2.5f*Mathf.Sqrt((float)(s.ValueAdded/source.Land.Gdp))};
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
                    People[i]=new Vector3(3+Mathf.Cos(a)*32,CloudY+Mathf.Sin(a*2)*2,9+Mathf.Sin(a)*28);
                }
                else seats[h].Add(i);
            }
            for(int h=0;h<seats.Length;h++)
            {
                var row=seats[h];row.Sort((a,b)=>MeanWealth(players[a]).CompareTo(MeanWealth(players[b])));
                for(int k=0;k<row.Count;k++)
                {
                    int i=row[k];Player p=players[i];IndustryHill hill=Hills[h];
                    float a=-Mathf.PI*.5f+(k%5-2)*.43f;
                    float rank=row.Count>1?k/(float)(row.Count-1):.5f;
                    if(p.Group==Group.Owners){var at=hill.Surface(a,.40f+.25f*(1-rank));at.y=Ground(at.x,at.z)+.08f;People[i]=at;}
                    else
                    {
                        float r=1.1f+(1-rank)*.5f+(k/5)*.13f;
                        float x=hill.Center.x+Mathf.Cos(a)*hill.Width*r,z=hill.Center.z+Mathf.Sin(a)*hill.Depth*r;
                        People[i]=new Vector3(x,Ground(x,z,hill.Tier)+.10f,z);
                    }
                }
            }
        }
        public static float Lowland(float x,float z)
        {
            float climb=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-70,48,z));
            float rear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(57,115,z));
            float side=Mathf.Exp(-Mathf.Pow(x/105,6));
            return side*rear*(64*climb+1.4f*Mathf.Sin(x*.055f+z*.045f)*Mathf.Sin(z*.09f));
        }
        public float Ground(float x,float z,int tier=0)
        {
            float mass=0;foreach(var hill in Hills)if(hill.Tier>0)mass=Mathf.Max(mass,hill.Elevation(x,z));
            return Lowland(x,z)+mass;
        }
        static double MeanWealth(Player p)=>p.Wealth/Math.Max(1,p.Adults.Length);
        public double FootprintArea(){double total=0;foreach(var h in Hills)total+=Math.PI*h.Width*h.Depth;return total;}
    }
}
