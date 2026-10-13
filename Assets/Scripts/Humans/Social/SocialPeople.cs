using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Why.Humans.Social
{
    /// <summary>
    /// The popular people of the social graph (Data/social/people.json, built by Tools/social): accounts resolved to
    /// people and groups with their lifespans, region, category cloud, follow ties, a force-directed layout per
    /// region, government offices and the economy companies they run. Parsed once (any thread) and shared by both
    /// scenes: the causality graph draws their lifelines and a cloud per region at the present; the economy draws the
    /// United States cloud above the land, with capital holders at their companies and officeholders at the government.
    /// </summary>
    public sealed class SocialPeople
    {
        public const string Path = "Data/social/people";
        public const string SharedKey = "social.people";

        /// <summary>The white of popular people: cooler and brighter than the gold of capital, so their bloom reads apart.</summary>
        public static readonly Color White = new Color(.90f, .96f, 1f);

        public sealed class Region { public string id, name; public string[] civs = Array.Empty<string>(); public int count; }
        public sealed class Cloud { public string id, label, color; }

        /// <summary>One government office: [office, from, to, e|l, n|s] in the file.</summary>
        public struct Office
        {
            public string Name; public int From, To; public bool Executive, National;
            public bool Holds(double year) => year >= From && year <= To;
        }

        public sealed class Person
        {
            public string name, handle, kind, region, regionBasis, g, pol, fig, q, desc;
            public int? born, died;
            public bool est, lead;
            public double fol;
            public int cloud = -1;
            public JArray gov;
            public string[] firms = Array.Empty<string>();
            public float[] pos = { 0, 0, 0 };

            [JsonIgnore] public int Index, RegionIndex;
            [JsonIgnore] public Office[] Offices = Array.Empty<Office>();
            [JsonIgnore] public Vector3 Layout => new Vector3(pos[0], pos[1], pos[2]);
            [JsonIgnore] public bool Female => g == "f";
            [JsonIgnore] public bool Group => kind == "g";

            /// <summary>Fame 0..1 from followers: 100k is 0, a billion is 1 (log scale).</summary>
            [JsonIgnore] public float Fame => Mathf.Clamp01((Mathf.Log10((float)Math.Max(1, fol)) - 5f) / 4f);

            public bool Alive(double year) => born.HasValue && born.Value <= year && (!died.HasValue || died.Value >= year);

            /// <summary>The government office held in a year (national before state, executive before legislative), if any.</summary>
            public bool InOffice(double year, out Office office)
            {
                office = default; bool found = false;
                foreach (var o in Offices)
                {
                    if (!o.Holds(year)) continue;
                    if (!found || (o.National && !office.National) || (o.National == office.National && o.Executive && !office.Executive))
                    { office = o; found = true; }
                }
                return found;
            }
        }

        public Region[] regions = Array.Empty<Region>();
        public Cloud[] clouds = Array.Empty<Cloud>();
        public Person[] people = Array.Empty<Person>();
        public int[][] edges = Array.Empty<int[]>();
        public string note, built;

        /// <summary>People and groups of each region, in file order.</summary>
        [JsonIgnore] public List<Person>[] ByRegion = Array.Empty<List<Person>>();

        public static SocialPeople Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            SocialPeople s;
            try { s = JsonConvert.DeserializeObject<SocialPeople>(json); }
            catch (Exception e) { Debug.LogWarning("[Why] Social people unreadable: " + e.Message); return null; }
            if (s?.people == null) return null;
            s.ByRegion = new List<Person>[s.regions.Length];
            for (int r = 0; r < s.regions.Length; r++) s.ByRegion[r] = new List<Person>();
            for (int i = 0; i < s.people.Length; i++)
            {
                var p = s.people[i]; p.Index = i;
                p.RegionIndex = Math.Max(0, Array.FindIndex(s.regions, r => r.id == p.region));
                s.ByRegion[p.RegionIndex].Add(p);
                if (p.pos == null || p.pos.Length < 3) p.pos = new float[] { 0, 0, 0 };
                if (p.firms == null) p.firms = Array.Empty<string>();
                var offices = new List<Office>();
                if (p.gov != null)
                    foreach (var token in p.gov)
                    {
                        if (!(token is JArray a) || a.Count < 5) continue;
                        bool hasFrom = a[1].Type == JTokenType.Integer, hasTo = a[2].Type == JTokenType.Integer;
                        if (!hasFrom && !hasTo) continue; // undated: no year to seat them in
                        int to = hasTo ? (int)a[2] : 9999, from = hasFrom ? (int)a[1] : to - 4;
                        offices.Add(new Office { Name = (string)a[0], From = from, To = to, Executive = (string)a[3] == "e", National = (string)a[4] == "n" });
                    }
                p.Offices = offices.ToArray();
            }
            return s;
        }

        public int RegionIndex(string id) => Array.FindIndex(regions, r => r.id == id);

        /// <summary>A category cloud's color (the social graph's hex), or the people white when it has none.</summary>
        public Color CloudColor(int cloud)
        {
            if (cloud < 0 || cloud >= clouds.Length || !ColorUtility.TryParseHtmlString(clouds[cloud].color, out Color c)) return White;
            return c;
        }

        public string CloudLabel(int cloud) => cloud >= 0 && cloud < clouds.Length ? clouds[cloud].label.Replace("\n", " ") : "";
    }
}
