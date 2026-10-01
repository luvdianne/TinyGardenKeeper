using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TinyGardenKeeper.EditorTools
{
    public static class GameViewSizeHelper
    {
        private static object s_GameViewSizesInstance;
        private static MethodInfo s_GetGroupMethod;

        [MenuItem("TinyGarden/Set Resolution to iPhone 15")]
        public static void SetToiPhone15()
        {
            // 1. Configure PlayerSettings for Portrait Phone AR
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // 2. Add iPhone 15 (1179 x 2556) custom resolution
            try
            {
                AddCustomSize(1179, 2556, "iPhone 15 (1179x2556)");
                Debug.Log("[GameViewSizeHelper] Successfully added and configured iPhone 15 (1179x2556) resolution preset!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameViewSizeHelper] Could not register GameView size: " + ex.Message);
            }
        }

        public static void AddCustomSize(int width, int height, string baseText)
        {
            var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            if (sizesType == null) return;

            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instanceProp = singleType.GetProperty("instance");
            if (instanceProp == null) return;

            s_GameViewSizesInstance = instanceProp.GetValue(null, null);
            s_GetGroupMethod = sizesType.GetMethod("GetGroup");
            if (s_GetGroupMethod == null) return;

            // Add for Standalone (0), iOS (1), and Android (2)
            AddCustomSizeForGroup(GameViewSizeGroupType.Standalone, width, height, baseText);
            AddCustomSizeForGroup(GameViewSizeGroupType.iOS, width, height, baseText);
            AddCustomSizeForGroup(GameViewSizeGroupType.Android, width, height, baseText);

            // Select this size in the GameView window
            SelectSize(GameViewSizeGroupType.Standalone, baseText);
        }

        private static void AddCustomSizeForGroup(GameViewSizeGroupType groupType, int width, int height, string baseText)
        {
            if (s_GameViewSizesInstance == null || s_GetGroupMethod == null) return;

            var group = s_GetGroupMethod.Invoke(s_GameViewSizesInstance, new object[] { (int)groupType });
            if (group == null) return;

            var groupTypeObj = group.GetType();
            var getCustomCount = groupTypeObj.GetMethod("GetCustomCount");
            var getCustomSize = groupTypeObj.GetMethod("GetCustomSize");
            if (getCustomCount == null || getCustomSize == null) return;

            int customCount = (int)getCustomCount.Invoke(group, null);

            var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var getBaseText = gameViewSizeType?.GetMethod("get_baseText");

            for (int i = 0; i < customCount; i++)
            {
                var size = getCustomSize.Invoke(group, new object[] { i });
                string text = (string)getBaseText?.Invoke(size, null);
                if (text == baseText)
                    return; // Already exists
            }

            var addCustomSizeMethod = groupTypeObj.GetMethod("AddCustomSize");
            var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var ctor = gameViewSizeType?.GetConstructor(new Type[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            if (ctor != null && addCustomSizeMethod != null)
            {
                // 1 = FixedResolution
                var newSize = ctor.Invoke(new object[] { 1, width, height, baseText });
                addCustomSizeMethod.Invoke(group, new object[] { newSize });
            }
        }

        private static void SelectSize(GameViewSizeGroupType groupType, string baseText)
        {
            try
            {
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) return;

                var gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView == null) return;

                var group = s_GetGroupMethod.Invoke(s_GameViewSizesInstance, new object[] { (int)groupType });
                if (group == null) return;

                var groupTypeObj = group.GetType();
                var getDisplayTexts = groupTypeObj.GetMethod("GetDisplayTexts");
                if (getDisplayTexts == null) return;

                string[] texts = (string[])getDisplayTexts.Invoke(group, null);
                if (texts == null) return;

                int index = -1;
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].Contains(baseText) || texts[i].StartsWith(baseText))
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    var selectedSizeIndexProp = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (selectedSizeIndexProp != null)
                    {
                        selectedSizeIndexProp.SetValue(gameView, index, null);
                        gameView.Repaint();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameViewSizeHelper] Could not auto-select GameView size: " + ex.Message);
            }
        }
    }
}
