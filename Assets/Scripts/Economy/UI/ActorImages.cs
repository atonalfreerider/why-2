using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Why.Economy.UI
{
    /// <summary>
    /// The actors' images: portraits, logos, flags and arms packed by Tools/images/fetch.py into atlas pages under
    /// Resources/Data/economy/images (actors-N.png, index.json, CREDITS.md). Every image is free-licensed (public domain,
    /// CC0, CC BY or CC BY-SA) and carries its credit line; a real person is packed only with the user's approval recorded
    /// in its item (for the history stories: 7 Nov 2026, Docs/research/advantage/brief-v4.md).
    /// </summary>
    public static class ActorImages
    {
        public const string Folder = "Data/economy/images/";

        public sealed class Cell
        {
            public string Id, Label, Shape, Credit, Attribution, Kind;
            public Texture2D Page;
            public Rect Uv;
        }

        static Dictionary<string, Cell> cells;

        /// <summary>The image of an actor or person by its id, or false when it has none (a monogram then stands for it).</summary>
        public static bool TryGet(string id, out Cell cell)
        {
            cell = null;
            if (string.IsNullOrEmpty(id)) return false;
            Load();
            return cells.TryGetValue(id, out cell) && cell.Page != null;
        }

        static void Load()
        {
            if (cells != null) return;
            cells = new Dictionary<string, Cell>();
            var asset = Resources.Load<TextAsset>(Folder + "index");
            if (asset == null) return;
            JObject root;
            try { root = JObject.Parse(asset.text); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Why] images: could not parse the atlas index: " + e.Message);
                return;
            }

            var pages = new List<Texture2D>();
            if (root["pages"] is JArray pa)
                foreach (var p in pa)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension((string)p);
                    pages.Add(Resources.Load<Texture2D>(Folder + name));
                }

            if (!(root["cells"] is JObject cs)) return;
            foreach (var kv in cs)
            {
                if (!(kv.Value is JObject c)) continue;
                int page = (int?)c["page"] ?? 0;
                var uv = c["uv"] as JArray;
                if (uv == null || uv.Count < 4 || page < 0 || page >= pages.Count) continue;
                float u0 = (float)uv[0], v0 = (float)uv[1], u1 = (float)uv[2], v1 = (float)uv[3];
                cells[kv.Key] = new Cell
                {
                    Id = kv.Key, Label = (string)c["label"], Shape = (string)c["shape"], Credit = (string)c["credit_short"],
                    Attribution = (string)c["attribution"], Kind = (string)c["kind"], Page = pages[page], Uv = Rect.MinMaxRect(u0, v0, u1, v1),
                };
            }
        }
    }
}
