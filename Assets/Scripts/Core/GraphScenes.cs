using System;
using System.Reflection;

namespace Why
{
    /// <summary>
    /// The scenes a <see cref="GraphLayer"/> or <see cref="GraphModule"/> belongs to. <see cref="GraphRoot"/>
    /// discovers layers and modules by reflection; it only creates the ones that belong to its scene. A type
    /// without this attribute belongs to the causality graph ("why") only, so the original scene is unchanged
    /// by layers written for other scenes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GraphScenesAttribute : Attribute
    {
        public readonly string[] Scenes;

        public GraphScenesAttribute(params string[] scenes) => Scenes = scenes ?? Array.Empty<string>();
    }

    /// <summary>
    /// Which graph the running <see cref="GraphRoot"/> builds. Set once in <c>GraphRoot.Awake</c> from the
    /// root's serialized scene id (the Unity scene file decides): "why" is the causality graph from the Big
    /// Bang to now, "economy" the United States economy 1950 - 2026 built on the same population.
    /// </summary>
    public static class GraphScene
    {
        public const string Why = "why";
        public const string Economy = "economy";

        public static string Current { get; private set; } = Why;

        public static bool IsEconomy => Current == Economy;

        public static void Set(string id) => Current = string.IsNullOrWhiteSpace(id) ? Why : id.Trim();

        /// <summary>Whether a layer or module type belongs to the current scene.</summary>
        public static bool Includes(Type type)
        {
            GraphScenesAttribute a = type.GetCustomAttribute<GraphScenesAttribute>(false);
            if (a == null) return Current == Why;
            foreach (string s in a.Scenes)
            {
                if (string.Equals(s, Current, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
