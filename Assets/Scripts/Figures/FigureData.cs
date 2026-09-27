using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Figures
{
    /// <summary>One famous figure from Data/figures.json, with resolved influence links.</summary>
    public sealed class Figure
    {
        public string id, name, gender, civ, role, blurb;
        public double born;
        public double? died;
        public float prominence;
        public int tier = 3;
        public string[] influencedBy = Array.Empty<string>();
        public Dictionary<string, string> influenceNote = new Dictionary<string, string>();

        /// <summary>Figures this one influenced (reverse of <see cref="influencedBy"/>).</summary>
        [JsonIgnore] public readonly List<Figure> Influenced = new List<Figure>();

        /// <summary>Resolved influencers.</summary>
        [JsonIgnore] public readonly List<Figure> Influencers = new List<Figure>();

        public string AnchorKey => "figure:" + id;

        public double End(double nowYear) => died ?? nowYear;

        public bool AliveIn(double year, double nowYear) => year >= born && year <= End(nowYear);

        public string Years
        {
            get
            {
                string b = born < 0 ? (-born).ToString("0") + " BCE" : born.ToString("0");
                if (!died.HasValue) return "born " + b;
                string d = died.Value < 0 ? (-died.Value).ToString("0") + " BCE" : died.Value.ToString("0");
                if (born < 0 && died.Value >= 0) return b + " - " + d + " CE";
                return b + " - " + d;
            }
        }

        public string Note(Figure other) =>
            influenceNote != null && influenceNote.TryGetValue(other.id, out string n) ? n : null;
    }

    /// <summary>Parses figures.json and links influences both ways. Thread safe (pure managed code).</summary>
    public static class FigureData
    {
        public const string Path = "Data/figures";

        sealed class FigureFile
        {
            public List<Figure> figures = new List<Figure>();
        }

        public static List<Figure> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<Figure>();
            List<Figure> list;
            try
            {
                list = JsonConvert.DeserializeObject<FigureFile>(json)?.figures ?? new List<Figure>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] could not parse figures: {e.Message}");
                return new List<Figure>();
            }

            Dictionary<string, Figure> byId = new Dictionary<string, Figure>();
            foreach (Figure f in list)
            {
                if (f?.id != null) byId[f.id] = f;
            }

            foreach (Figure f in list)
            {
                if (f?.influencedBy == null) continue;
                foreach (string src in f.influencedBy)
                {
                    if (src == null || src == f.id || !byId.TryGetValue(src, out Figure from)) continue;
                    f.Influencers.Add(from);
                    from.Influenced.Add(f);
                }
            }

            list.RemoveAll(f => f?.id == null);
            return list;
        }
    }
}
