using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Why.EditorTools
{
    /// <summary>
    /// Fixed Game view sizes for recording tutorial videos: "Why/Game View/Portrait 1080x1920" and
    /// "Why/Game View/Landscape 1920x1080", and V in play mode (through <see cref="ScreenLayout.EditorGameView"/>):
    /// portrait remembers the size the Game view had, and V again goes back to it.
    ///
    /// Unity keeps the Game view's size list internal, so it is driven by reflection (GameViewSizes,
    /// GameViewSizeGroup, GameViewSize, GameView.SizeSelectionCallback, checked against 6000.6); a missing member
    /// logs a warning and leaves the Game view as it was. Sizes added here last for the Editor session (they are
    /// not saved to the preferences).
    /// </summary>
    [InitializeOnLoad]
    static class PortraitGameView
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                     BindingFlags.Static | BindingFlags.FlattenHierarchy;

        /// <summary>SessionState key of the size index to return to (survives the domain reload of entering play mode).</summary>
        const string RestoreKey = "Why.PortraitGameView.Restore";

        static PortraitGameView() => ScreenLayout.EditorGameView = SetPortrait;

        [MenuItem("Why/Game View/Portrait 1080x1920")]
        static void PortraitMenu()
        {
            if (Select(1080, 1920, "Why portrait", out int previous)) Remember(previous, 1080, 1920);
        }

        [MenuItem("Why/Game View/Landscape 1920x1080")]
        static void LandscapeMenu() => Select(1920, 1080, "Why landscape", out _);

        /// <summary>V in play mode: portrait at 1080x1920, or back to the size the Game view had (else 1920x1080).</summary>
        static bool SetPortrait(bool portrait)
        {
            if (portrait)
            {
                if (!Select(1080, 1920, "Why portrait", out int previous)) return false;
                Remember(previous, 1080, 1920);
                return true;
            }

            int restore = SessionState.GetInt(RestoreKey, -1);
            SessionState.EraseInt(RestoreKey);
            if (restore >= 0 && restore < Count() && SelectIndex(restore)) return true;
            return Select(1920, 1080, "Why landscape", out _);
        }

        /// <summary>Number of sizes in the current size group (0 when it cannot be read).</summary>
        static int Count()
        {
            try
            {
                if (!Sizes(out object group, out _, out _)) return 0;
                MethodInfo count = group.GetType().GetMethod("GetTotalCount", Members);
                return count != null ? (int)count.Invoke(group, null) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Remembers the size to return to, unless it was the portrait size itself.</summary>
        static void Remember(int previous, int width, int height)
        {
            if (previous >= 0 && !IsSize(previous, width, height)) SessionState.SetInt(RestoreKey, previous);
        }

        /// <summary>Selects (adding it first if needed) a fixed-resolution size in every open Game view.</summary>
        static bool Select(int width, int height, string label, out int previous)
        {
            previous = -1;
            try
            {
                if (!Sizes(out object group, out Type sizeType, out Type kindType)) return false;
                int index = Find(group, sizeType, width, height);
                if (index < 0)
                {
                    object fixedResolution = Enum.Parse(kindType, "FixedResolution");
                    ConstructorInfo ctor = sizeType.GetConstructor(Members, null,
                        new[] { kindType, typeof(int), typeof(int), typeof(string) }, null);
                    MethodInfo add = group.GetType().GetMethod("AddCustomSize", Members);
                    if (ctor == null || add == null) return Fail("GameViewSize constructor or AddCustomSize");
                    add.Invoke(group, new[] { ctor.Invoke(new[] { fixedResolution, width, height, label }) });
                    index = Find(group, sizeType, width, height);
                    if (index < 0) return Fail("the added size");
                }

                return SelectIndex(index, out previous);
            }
            catch (Exception e)
            {
                return Fail(e.GetBaseException().Message);
            }
        }

        static bool SelectIndex(int index) => SelectIndex(index, out _);

        /// <summary>Selects a size by its index in the current size group, in every open Game view.</summary>
        static bool SelectIndex(int index, out int previous)
        {
            previous = -1;
            try
            {
                Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (viewType == null) return Fail("UnityEditor.GameView");
                PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", Members);
                MethodInfo callback = viewType.GetMethod("SizeSelectionCallback", Members, null,
                    new[] { typeof(int), typeof(object) }, null);
                if (selected == null) return Fail("GameView.selectedSizeIndex");

                Object[] views = Resources.FindObjectsOfTypeAll(viewType);
                if (views.Length == 0) return Fail("an open Game view");
                foreach (Object view in views)
                {
                    if (previous < 0) previous = (int)selected.GetValue(view);
                    // the callback also refits the view's zoom; the property alone is the fallback
                    if (callback != null) callback.Invoke(view, new object[] { index, null });
                    else selected.SetValue(view, index);
                    ((EditorWindow)view).Repaint();
                }

                return true;
            }
            catch (Exception e)
            {
                return Fail(e.GetBaseException().Message);
            }
        }

        /// <summary>The current size group (e.g. Standalone) and the internal size types.</summary>
        static bool Sizes(out object group, out Type sizeType, out Type kindType)
        {
            group = null;
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            sizeType = editor.GetType("UnityEditor.GameViewSize");
            kindType = editor.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || kindType == null) return Fail("the Game view size types");

            PropertyInfo instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", Members);
            object sizes = instance?.GetValue(null);
            PropertyInfo current = sizesType.GetProperty("currentGroup", Members);
            group = sizes != null ? current?.GetValue(sizes) : null;
            return group != null || Fail("GameViewSizes.currentGroup");
        }

        /// <summary>Index of a fixed-resolution size in the group, or -1.</summary>
        static int Find(object group, Type sizeType, int width, int height)
        {
            Type groupType = group.GetType();
            MethodInfo count = groupType.GetMethod("GetTotalCount", Members);
            MethodInfo get = groupType.GetMethod("GetGameViewSize", Members);
            PropertyInfo w = sizeType.GetProperty("width", Members);
            PropertyInfo h = sizeType.GetProperty("height", Members);
            PropertyInfo kind = sizeType.GetProperty("sizeType", Members);
            if (count == null || get == null || w == null || h == null || kind == null) return -1;

            int total = (int)count.Invoke(group, null);
            object[] args = new object[1];
            for (int i = 0; i < total; i++)
            {
                args[0] = i;
                object size = get.Invoke(group, args);
                if (size != null && (int)w.GetValue(size) == width && (int)h.GetValue(size) == height &&
                    kind.GetValue(size).ToString() == "FixedResolution")
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>True when the size at this index of the current group is width x height.</summary>
        static bool IsSize(int index, int width, int height)
        {
            try
            {
                if (!Sizes(out object group, out Type sizeType, out _)) return false;
                object size = group.GetType().GetMethod("GetGameViewSize", Members)?.Invoke(group, new object[] { index });
                return size != null && (int)sizeType.GetProperty("width", Members).GetValue(size) == width &&
                       (int)sizeType.GetProperty("height", Members).GetValue(size) == height;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool Fail(string what)
        {
            Debug.LogWarning($"[Why] Game view size: could not reach {what} (Unity internals changed?); " +
                             "pick the size in the Game view's resolution menu instead.");
            return false;
        }
    }
}
