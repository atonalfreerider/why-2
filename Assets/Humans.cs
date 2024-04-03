using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shapes.Lines;
using UnityEngine;

public class Humans : MonoBehaviour
{
    //         YA              Name          W-E  Power 
    Dictionary<int, Dictionary<string, Tuple<int, int>>> powerByYearsAgo;

    void Awake()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("powerByYearsAgo");

        powerByYearsAgo =
            JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, Tuple<int, int>>>>(textAsset.text);
    }

    void Start()
    {
        GameObject humans = new GameObject("Humans");
        humans.transform.SetParent(transform, false);

        for (int i = 1; i <= 4; i++)
        {
            GameObject tick = Main.NewTick(Color.white, .25f, i + " kYA");
            tick.transform.SetParent(humans.transform, false);
            tick.transform.Rotate(Vector3.up, 180);
            tick.transform.Translate(Vector3.forward * (4 - i) * .5f);
        }

        Dictionary<string, Tuple<Vector3, Vector3>> previousSlice = new();
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
                float lhsX = power.Item1 * popMillions * popScale;
                float rhsX = (power.Item1 + power.Item2) * popMillions * popScale;
                Vector3 lhsStart = new Vector3(lhsX, 0, zPos);
                Vector3 rhsStart = new Vector3(rhsX, 0, zPos);

                if (previousSlice.TryGetValue(name, out Tuple<Vector3, Vector3> previousPower))
                {
                    StaticLink lhs = Instantiate(StaticLink.prototypeStaticLink, humans.transform);
                    lhs.gameObject.SetActive(true);
                    lhs.LW = 0.01f;
                    lhs.DrawFromTo(previousPower.Item1, lhsStart);
                    lhs.SetColor(Color.blue);

                    StaticLink rhs = Instantiate(StaticLink.prototypeStaticLink, humans.transform);
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

        float angle = Main.Instance.ToArbitrary(4000) * Mathf.PI * 2;
        humans.transform.localPosition = new Vector3(2 * Mathf.Sin(angle), 0, -2 * Mathf.Cos(angle));
        humans.transform.Rotate(Vector3.up, -(angle * 180 / Mathf.PI - 90));
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