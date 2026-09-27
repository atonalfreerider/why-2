using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Why.Matter
{
    /// <summary>Contents of Data/matter.json: the bands of matter and the cosmic / geologic epochs.</summary>
    public sealed class MatterFile
    {
        /// <summary>Age of the universe in the data (years); the clock itself uses <see cref="DeepTime.AgeU"/>.</summary>
        [JsonProperty("ageOfUniverseYears")] public double AgeOfUniverseYears = 13.787e9;

        /// <summary>The bodies of matter (bands), from the whole universe to Earth.</summary>
        [JsonProperty("items")] public List<MatterItem> Items = new List<MatterItem>();

        /// <summary>Cosmic and geologic moments (ticks on the inner ring).</summary>
        [JsonProperty("epochs")] public List<MatterEpoch> Epochs = new List<MatterEpoch>();

        /// <summary>Parses matter.json; returns null (and reports why) when the text is missing or invalid.</summary>
        public static MatterFile Parse(string json, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "matter.json is missing";
                return null;
            }

            try
            {
                MatterFile file = JsonConvert.DeserializeObject<MatterFile>(json);
                if (file == null)
                {
                    error = "matter.json is empty";
                    return null;
                }

                file.Items ??= new List<MatterItem>();
                file.Epochs ??= new List<MatterEpoch>();
                file.Items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.Id) || i.StartYa <= 0);
                file.Epochs.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Id));
                return file;
            }
            catch (Exception e)
            {
                error = "could not parse matter.json: " + e.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// A body of matter with a lifetime, e.g. the Milky Way or the Sun. Items nest through
    /// <see cref="Parent"/>; the ones with <see cref="InPath"/> form our lineage (universe to Earth).
    /// </summary>
    public sealed class MatterItem
    {
        /// <summary>Stable id; anchors are "matter:&lt;id&gt;".</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Display name.</summary>
        [JsonProperty("name")] public string Name;

        /// <summary>When the item forms (years ago).</summary>
        [JsonProperty("startYa")] public double StartYa;

        /// <summary>When the item ends (years ago); 0 = still exists.</summary>
        [JsonProperty("endYa")] public double EndYa;

        /// <summary>Ordinary (baryonic) mass in kg; sets the band width on a log scale.</summary>
        [JsonProperty("massKg")] public double MassKg;

        /// <summary>Radial rank in the source data: 0 = innermost (Earth) ... 9 = the whole universe.</summary>
        [JsonProperty("rank")] public int Rank;

        /// <summary>Id of the item this one formed out of (null for the universe).</summary>
        [JsonProperty("parent")] public string Parent;

        /// <summary>True for our lineage (universe, Milky Way, solar nebula, Sun, Earth).</summary>
        [JsonProperty("inPath")] public bool InPath;

        /// <summary>One or two sentences for the tooltip and the director.</summary>
        [JsonProperty("blurb")] public string Blurb;

        /// <summary>Where the numbers come from.</summary>
        [JsonProperty("source")] public string Source;

        /// <summary>Diameter in metres (0 = unknown); with the mass it sets how dense, and so how bright, a body is.</summary>
        [JsonProperty("sizeM")] public double SizeM;

        /// <summary>False for structures gravity does not hold together (superclusters, the universe): they expand and thin out.</summary>
        [JsonProperty("bound")] public bool Bound = true;

        /// <summary>Icon shown beside the item's labels (an icon id from Why.Icons, e.g. "sun", "galaxy").</summary>
        [JsonProperty("icon")] public string Icon;

        /// <summary>The name, or the id when the name is missing.</summary>
        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
    }

    /// <summary>A moment of cosmic or geologic history shown as a tick on the inner ring.</summary>
    public sealed class MatterEpoch
    {
        /// <summary>Stable id; anchors are "epoch:&lt;id&gt;".</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Display name.</summary>
        [JsonProperty("name")] public string Name;

        /// <summary>When it happened (years ago).</summary>
        [JsonProperty("ya")] public double Ya;

        /// <summary>Time after the Big Bang (years), for the moments the clock cannot resolve.</summary>
        [JsonProperty("afterBigBangYears")] public double? AfterBigBangYears;

        /// <summary>Label level of detail: 1 = always, 2 = mid zoom, 3 = close.</summary>
        [JsonProperty("tier")] public int Tier = 2;

        /// <summary>One or two sentences for the tooltip and the director.</summary>
        [JsonProperty("blurb")] public string Blurb;

        /// <summary>Where the date comes from.</summary>
        [JsonProperty("source")] public string Source;

        /// <summary>The name, or the id when the name is missing.</summary>
        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
    }
}
