using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shapes;
using UnityEngine;

public class Energy : MonoBehaviour
{
    public Material DistanceOpacityMat;
    
    List<EnergyItem> energyItems;
    
    Dictionary<string, List<Vector2>> innerWedgesByName;
    Dictionary<string, List<Vector2>> outerWedgesByName;
    
    void Awake()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("energy");
        energyItems = JsonConvert.DeserializeObject<List<EnergyItem>>(textAsset.text);

        innerWedgesByName = energyItems.ToDictionary(
            energyItem => energyItem.name,
            _ => new List<Vector2>());

        outerWedgesByName = energyItems.ToDictionary(
            energyItem => energyItem.name,
            _ => new List<Vector2>());

        // loop through 1 to .5 in .01 increments and draw the energy items. make sure they are stacked from lowest
        // mass on the inside to highest mass on the outside
        for (float arb = 1; arb > .5f; arb -= .01f)
        {
            Dictionary<double, EnergyItem> currentSliceStack = new();
            foreach (EnergyItem item in energyItems)
            {
                float start = item.start;
                float end = item.end;
                float arbStart = Main.Instance.ToArbitrary(start);
                float arbEnd = Main.Instance.ToArbitrary(end);
                if (arbStart >= arb && arbEnd <= arb)
                {
                    double mass = double.Parse(item.mass);
                    currentSliceStack.TryAdd(mass, item);
                }
            }

            Vector2 inner = InnerEnvelope(arb);
            Vector2 lastOuter = Vector2.zero;
            foreach ((double mass, EnergyItem item) in currentSliceStack.OrderBy(i => i.Key))
            {
                double massEnv = MassEnvelope(arb);
                float massPrct = (float)(mass / Math.Pow(10, massEnv));
                if (massPrct > 1)
                {
                    massPrct = 1;
                }

                if (massPrct < .01f)
                {
                    massPrct = 0;
                }

                List<Vector2> innerWedge = innerWedgesByName[item.name];

                Vector2 innerPt = inner;
                if (lastOuter.magnitude > float.Epsilon)
                {
                    innerPt = Vector2.LerpUnclamped(inner, lastOuter, 1.1f);
                }

                innerWedge.Add(innerPt);

                List<Vector2> outerWedge = outerWedgesByName[item.name];

                Vector2 visibility = VisibiityEnvelope(1 - arb);
                Vector2 outer = Vector2.Lerp(
                    new Vector2(inner.x, inner.y),
                    new Vector2(visibility.x, visibility.y),
                    massPrct);
                lastOuter = outer;
                outerWedge.Add(new Vector2(outer.x, outer.y));
            }
        }
    }

    void Start()
    {
        GameObject itemGO = new GameObject("Energy");
        itemGO.transform.SetParent(transform, false);
        
        foreach (EnergyItem energyItem in energyItems)
        {
            List<Vector2> innerWedge = innerWedgesByName[energyItem.name];
            List<Vector2> outerWedge = outerWedgesByName[energyItem.name];

            List<int> indices = new();
            List<Vector3> verts = new();
            for (int i = 0; i < innerWedge.Count - 1; i++)
            {
                Vector3 from = new Vector3(innerWedge[i].x, 0, innerWedge[i].y);
                Vector3 to = new Vector3(outerWedge[i].x, 0, outerWedge[i].y);

                Vector3 nextFrom = new Vector3(innerWedge[i + 1].x, 0, innerWedge[i + 1].y);
                Vector3 nextTo = new Vector3(outerWedge[i + 1].x, 0, outerWedge[i + 1].y);

                verts.Add(from);
                verts.Add(to);

                verts.Add(nextFrom);
                verts.Add(nextTo);

                indices.Add(i * 4);
                indices.Add(i * 4 + 1);
                indices.Add(i * 4 + 2);

                indices.Add(i * 4);
                indices.Add(i * 4 + 2);
                indices.Add(i * 4 + 1);

                indices.Add(i * 4 + 1);
                indices.Add(i * 4 + 2);
                indices.Add(i * 4 + 3);

                indices.Add(i * 4 + 1);
                indices.Add(i * 4 + 3);
                indices.Add(i * 4 + 2);
            }

            Polygon wedge = PolygonFactory.NewPoly(DistanceOpacityMat);
            wedge.transform.SetParent(itemGO.transform);
            wedge.name = energyItem.name;
            wedge.Draw3DPoly(verts.ToArray(), indices.ToArray());

            wedge.SetColor(Color.red);
        }
    }

    /// <summary>
    /// The inner edge of the curve that follows a circle around the origin at radius = 2
    /// </summary>
    /// <param name="prct">angle from beginning of time at 0</param>
    /// <returns>Vector2 coordinates of circle at radius = 2</returns>
    static Vector2 InnerEnvelope(float prct)
    {
        float x = 2 * Mathf.Sin(prct * Mathf.PI * 2);
        float y = -2 * Mathf.Cos(prct * Mathf.PI * 2);
        return new Vector2(x, y);
    }

    /// <summary>
    /// The outer edge of a logarithmic spiral
    /// </summary>
    /// <param name="prct">angle from beginning of time at 0</param>
    /// <returns>Vector2 coordinates of logarithmic spiral</returns>
    static Vector2 VisibiityEnvelope(float prct)
    {
        const float startR = 2;
        const float spiralFactor = 2;
        double x = -startR * Math.Exp(spiralFactor * prct * Mathf.PI * 2) * Mathf.Sin(prct * Mathf.PI * 2);
        double y = -startR * Math.Exp(spiralFactor * prct * Mathf.PI * 2) * Mathf.Cos(prct * Mathf.PI * 2);
        return new Vector2((float)x, (float)y);
    }

    static double MassEnvelopeLinear(float prct)
    {
        return 62 * prct - 9;
    }

    static double MassEnvelope(float prct)
    {
        if (prct > .5f)
        {
            long[] polyCoeff =
            {
                -8863,
                +70641,
                -227641,
                +379172,
                -342823,
                +158710,
                -29144
            };

            double outVal = polyCoeff[0];
            for (int i = 1; i < polyCoeff.Length; i++)
            {
                outVal += polyCoeff[i] * Math.Pow(prct, i);
            }

            return outVal;
        }

        return 0;
    }

    [Serializable]
    public class EnergyItem
    {
        public string name;
        public float start;
        public float end;
        public string mass;
    }
}