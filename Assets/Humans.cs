using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shapes.Lines;
using UnityEngine;

public class Humans : MonoBehaviour
{
    void Start()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("powerByYearsAgo");

        //         YA              Name          W-E  Power 
        Dictionary<int, Dictionary<string, Tuple<int, int>>> powerByYearsAgo =
            JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, Tuple<int, int>>>>(textAsset.text);

        Dictionary<string, Tuple<Vector3, Vector3>> previousSlice = new();
        const float R = 2f;
        const float yearScale = .0005f;
        const float popScale = .000001f;
        foreach ((int yearsAgo, Dictionary<string, Tuple<int, int>> slice) in powerByYearsAgo)
        {
            float popMillions = yearsAgo > 330 
                ? PreIndustrial(yearsAgo) 
                : PostIndustrial(yearsAgo);
            
            float zPos = (4000 - yearsAgo) * -yearScale;
            Dictionary<string, Tuple<Vector3, Vector3>> thisSlice = new();
            foreach ((string name, Tuple<int, int> power) in slice)
            {
                float lhsX = R + power.Item1 * popMillions * popScale;
                float rhsX = R + (power.Item1 + power.Item2) * popMillions * popScale;
                Vector3 lhsStart = new Vector3(lhsX, 0, zPos);
                Vector3 rhsStart = new Vector3(rhsX, 0, zPos);

                if (previousSlice.TryGetValue(name, out Tuple<Vector3, Vector3> previousPower))
                {
                    StaticLink lhs = Instantiate(StaticLink.prototypeStaticLink, transform);
                    lhs.gameObject.SetActive(true);
                    lhs.LW = 0.01f;
                    lhs.DrawFromTo(previousPower.Item1, lhsStart);
                    lhs.SetColor(Color.blue);

                    StaticLink rhs = Instantiate(StaticLink.prototypeStaticLink, transform);
                    rhs.gameObject.SetActive(true);
                    rhs.LW = 0.01f;
                    rhs.DrawFromTo(previousPower.Item2, rhsStart);
                    rhs.SetColor(Color.blue);

                    previousSlice[name] = new Tuple<Vector3, Vector3>(lhsStart, rhsStart);
                }

                thisSlice.Add(name, new Tuple<Vector3, Vector3>(lhsStart, rhsStart));
            }

            previousSlice = thisSlice;
        }
    }

    static float PreIndustrial(int yearsAgo)
    {
        return 601 * Mathf.Exp(-.000688f * yearsAgo);
    }

    static float PostIndustrial(int yearsAgo)
    {
        return 12188 + -2093 * Mathf.Log(yearsAgo) + 
            yearsAgo - 50; // artifical correction
    }
}