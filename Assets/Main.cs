#nullable enable
using System;
using System.Collections.Generic;
using Shapes.Lines;
using UnityEngine;

public class Main : MonoBehaviour
{
    public static Main Instance;

    public static float AgeU = 1.38f * Mathf.Pow(10, 10); // age of Universe in years
    public static double MassU = 1.50f * Mathf.Pow(10, 53); // Baryonic matter in kg
    public static float moment = 4.50f * Mathf.Pow(10, -10);
    public const int NumDiv = 1000;

    readonly Dictionary<float, float> adjustedTimeMap = new();

    void Awake()
    {
        Instance = this;

        // map the adjusted time scale analytically, so that adjusted values can be looked up in reverse
        for (int i = NumDiv; i > 0; i--)
        {
            float val = (float)i / NumDiv;
            val = Mathf.Round(val * 1000) / 1000f;
            adjustedTimeMap.Add(val, Ft(val));
        }
    }

    void Start()
    {
        const int numTicks = 30;
        for (int i = 10; i < numTicks; i++)
        {
            float val = (float)i / numTicks;
            float adj = Ft(val) * AgeU;

            string label = adj switch
            {
                > 1000000000 => (adj / Math.Pow(10, 9)).ToString("0.00") + " BYA",
                > 1000000 => (adj / Math.Pow(10, 6)).ToString("0.00") + " MYA",
                > 1000 => (adj / Math.Pow(10, 3)).ToString("0.00") + " kYA",
                > 1 => adj.ToString("0.00") + " YA",
                _ => ""
            };

            float weight = adj switch
            {
                > 1000000000 => 1,
                > 1000000 => .5f,
                > 1000 => .25f,
                > 1 => .25f,
                _ => .25f
            };

            GameObject tick = NewTick(Color.white, weight, label);
            tick.transform.SetParent(transform, false);
            tick.transform.Rotate(Vector3.up, -360 * (float)i / numTicks - 90);
            tick.transform.Translate(Vector3.left * 2);
        }
    }

    /// <summary>
    /// Searches through the arbitrary time mapping to find the value from 0 to 1.
    /// The arbitrary time is defined by Ft(t) = t^(t^(-1.4 - 2.39t))
    /// </summary>
    /// <param name="yearsAgo"></param>
    /// <returns>scaled time from 1 (beginning of time) to 0 (the present)</returns>
    public float ToArbitrary(float yearsAgo)
    {
        if (yearsAgo <= float.Epsilon) return 0;

        float prct = yearsAgo / AgeU;
        float closestVal = float.MaxValue;
        float closestKey = 0;
        foreach (KeyValuePair<float, float> keyValuePair in adjustedTimeMap)
        {
            if (Math.Abs(keyValuePair.Value - prct) < closestVal)
            {
                closestVal = Math.Abs(keyValuePair.Value - prct);
                closestKey = keyValuePair.Key;
            }
        }

        float closest = adjustedTimeMap[closestKey];
        if (closest > prct)
        {
            float prev = closestKey - 1f / NumDiv;
            prev = RoundToThree(prev);
            return Mathf.Lerp(prev, closestKey,
                (prct - adjustedTimeMap[prev]) / (adjustedTimeMap[closestKey] - adjustedTimeMap[prev]));
        }

        if (closest < prct)
        {
            float next = closestKey + 1f / NumDiv;
            next = RoundToThree(next);
            return Mathf.Lerp(closestKey, next,
                (prct - adjustedTimeMap[closestKey]) / (adjustedTimeMap[next] - adjustedTimeMap[closestKey]));
        }

        return closestKey;
    }

    public float FromArbitrary(float arbitrary)
    {
        if (adjustedTimeMap.ContainsKey(arbitrary))
        {
            return AgeU * adjustedTimeMap[arbitrary];
        }

        foreach (KeyValuePair<float, float> keyValuePair in adjustedTimeMap)
        {
            if (Mathf.Abs(arbitrary - keyValuePair.Key) < .0001f)
                return AgeU * keyValuePair.Value;
        }

        return 0;
    }

    /// <summary>
    /// An arbitrary mapping Ft(t) = t^(t^(-1.4 - 2.39t)). This scales Energy, Life, and Human history in even thirds
    /// </summary>
    public static float Ft(float t)
    {
        return Mathf.Pow(t, Mathf.Pow(t, -1.4f - 2.39f * t));
    }

    public static GameObject NewTick(Color shade, float weight, string tLabel)
    {
        GameObject newTick = new GameObject(tLabel);

        float length = .1f * weight;

        TextBox textBox = TextBox.Create(tLabel);
        textBox.transform.SetParent(newTick.transform, false);
        textBox.transform.Rotate(Vector3.right, 90);
        textBox.transform.Translate(Vector3.right * (length + .02f));
        textBox.gameObject.SetActive(true);
        textBox.Size = weight;
        textBox.Color = shade;

        StaticLink tick = Instantiate(StaticLink.prototypeStaticLink, newTick.transform);
        tick.gameObject.SetActive(true);
        tick.LW = weight * .01f;
        tick.DrawFromTo(Vector3.zero, new Vector3(length, 0, 0));
        tick.SetColor(shade);

        return newTick;
    }

    public static float RoundToThree(float val)
    {
        return Mathf.Round(val * 1000) / 1000f;
    }
}