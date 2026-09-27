using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Why.Matter
{
    /// <summary>Contents of Data/matter.json: the bands of matter and the cosmic / geologic epochs.</summary>
    public sealed class MatterFile
    {
        [JsonProperty("ageOfUniverseYears")] public double AgeOfUniverseYears = 13.787e9;
        [JsonProperty("items")] public List<MatterItem> Items = new List<MatterItem>();
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
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;

        /// <summary>When the item forms (years ago).</summary>
        [JsonProperty("startYa")] public double StartYa;

        /// <summary>When the item ends (years ago); 0 = still exists.</summary>
        [JsonProperty("endYa")] public double EndYa;

        [JsonProperty("massKg")] public double MassKg;

        /// <summary>Radial rank in the source data: 0 = innermost (Earth) ... 9 = the whole universe.</summary>
        [JsonProperty("rank")] public int Rank;

        [JsonProperty("parent")] public string Parent;
        [JsonProperty("inPath")] public bool InPath;
        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("source")] public string Source;

        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
    }

    /// <summary>A moment of cosmic or geologic history shown as a tick on the inner ring.</summary>
    public sealed class MatterEpoch
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;

        /// <summary>When it happened (years ago).</summary>
        [JsonProperty("ya")] public double Ya;

        /// <summary>Time after the Big Bang (years), for the moments the clock cannot resolve.</summary>
        [JsonProperty("afterBigBangYears")] public double? AfterBigBangYears;

        [JsonProperty("tier")] public int Tier = 2;
        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("source")] public string Source;

        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
    }
}
