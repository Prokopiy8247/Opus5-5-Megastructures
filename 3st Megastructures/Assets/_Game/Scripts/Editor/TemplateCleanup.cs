// TemplateCleanup: removes the URP template leftovers once our own settings are in place (MASTER_PROMPT 7.2):
// SampleScene, TutorialInfo, Readme, the Mobile pipeline asset and renderer, and the sample volume profile.
// A candidate is removed only when nothing that stays references it (dependencies of the scene, settings, quality
// levels and the default pipeline). DefaultVolumeProfile and UniversalRenderPipelineGlobalSettings are kept.
// Assets go to the OS trash (recoverable) when possible.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.EditorTools
{
    public static class TemplateCleanup
    {
        public static readonly string[] Candidates =
        {
            "Assets/Readme.asset",
            "Assets/TutorialInfo",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Settings/SampleSceneProfile.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };
        const string ScenesFolder = "Assets/Scenes";

        public static void Run(List<string> problems)
        {
            int removed = 0, kept = 0;
            foreach (var path in Candidates)
            {
                if (!Exists(path)) continue;
                var users = ReferencedBy(path);   // recomputed after each removal (the Mobile renderer frees up after its asset)
                if (users != null)
                {
                    ThreadLog.Warn($"TemplateCleanup: kept {path}: still referenced by {users}");
                    kept++;
                    continue;
                }
                if (Remove(path)) removed++;
                else ThreadLog.Warn($"TemplateCleanup: could not remove {path}");
            }
            if (AssetDatabase.IsValidFolder(ScenesFolder) && AssetDatabase.FindAssets("", new[] { ScenesFolder }).Length == 0)
                if (Remove(ScenesFolder)) removed++;
            ThreadLog.Info($"TemplateCleanup: {removed} removed, {kept} kept");
        }

        static bool Exists(string path) => AssetDatabase.IsValidFolder(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        static bool Remove(string path)
        {
            bool ok = AssetDatabase.MoveAssetToTrash(path);
            if (!ok) ok = AssetDatabase.DeleteAsset(path);
            if (ok) ThreadLog.Info("TemplateCleanup: removed " + path);
            return ok;
        }

        /// <summary>The assets that stay; everything they depend on must not be removed.</summary>
        static List<string> KeptRoots()
        {
            var roots = new List<string>
            {
                ThreadPaths.MainScene, ThreadPaths.LibraryAsset, ThreadPaths.PaletteAsset, ThreadPaths.RpAsset,
                ThreadPaths.RendererAsset, ThreadPaths.UrpGlobalSettings, ThreadPaths.DefaultVolumeProfile, ThreadPaths.InputActions,
            };
            var def = GraphicsSettings.defaultRenderPipeline;
            if (def != null) roots.Add(AssetDatabase.GetAssetPath(def));
            for (int i = 0; i < QualitySettings.count; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                if (rp != null) roots.Add(AssetDatabase.GetAssetPath(rp));
            }
            foreach (var s in EditorBuildSettings.scenes) if (s != null && s.enabled) roots.Add(s.path);
            var existing = new List<string>();
            foreach (var r in roots)
                if (!string.IsNullOrEmpty(r) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(r, AssetPathToGUIDOptions.OnlyExistingAssets))) existing.Add(r);
            return existing;
        }

        /// <summary>Null when unreferenced, else a short description of who references the candidate.</summary>
        static string ReferencedBy(string candidate)
        {
            var targets = new HashSet<string>();
            if (AssetDatabase.IsValidFolder(candidate))
            {
                foreach (var guid in AssetDatabase.FindAssets("", new[] { candidate })) targets.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else targets.Add(candidate);

            foreach (var root in KeptRoots())
            {
                if (targets.Contains(root)) return "build settings / quality / default pipeline (" + root + ")";
                foreach (var dep in AssetDatabase.GetDependencies(root, true))
                    if (dep != root && targets.Contains(dep)) return root;
            }
            return null;
        }
    }
}
