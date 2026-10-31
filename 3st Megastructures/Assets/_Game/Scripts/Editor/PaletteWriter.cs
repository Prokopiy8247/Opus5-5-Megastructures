// PaletteWriter: writes Assets/_Game/Settings/ThreadPalette.asset from ThreadPalette.Table (MASTER_PROMPT 14.7.8 a).
// The asset keeps its GUID across rebuilds (GameBootstrap references it from Main.unity).
using UnityEditor;
using UnityEngine;

namespace Megastructure.EditorTools
{
    public static class PaletteWriter
    {
        public static ThreadPalette Write()
        {
            EditorUtil.EnsureFolder(ThreadPaths.SettingsDir);
            var pal = AssetDatabase.LoadAssetAtPath<ThreadPalette>(ThreadPaths.PaletteAsset);
            bool created = pal == null;
            if (created)
            {
                pal = ScriptableObject.CreateInstance<ThreadPalette>();
                pal.name = "ThreadPalette";
                pal.FillFromTable();
                AssetDatabase.CreateAsset(pal, ThreadPaths.PaletteAsset);
            }
            else
            {
                pal.FillFromTable();
                EditorUtility.SetDirty(pal);
            }
            AssetDatabase.SaveAssetIfDirty(pal);
            ThreadLog.Info($"PaletteWriter: {(created ? "created" : "updated")} {ThreadPaths.PaletteAsset} ({pal.tokens.Count} tokens)");
            return pal;
        }
    }
}
