using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TFY.CatAdventure.Domain;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TFY.CatAdventure.EditorTools
{
    /// <summary>
    /// One-click project setup (menu TFY):
    ///   1. Setup Project  — URP assets for LOW/MEDIUM/HIGH/ULTRA (+SSAO on HIGH/ULTRA), four Quality levels, Android player
    ///                       settings (IL2CPP, ARM64, Vulkan+GLES3, ASTC, portrait, Linear), shaders the code creates at runtime,
    ///                       and the Boot scene. Settings are looked up by name and skipped if a URP version renamed them.
    ///   2. Enable Firebase — adds the TFY_FIREBASE define once the Firebase Unity SDK is imported.
    ///   Check Levels       — runs the level planner on every level and prints a fairness report.
    ///   Build Android APK  — development-free release build to Builds/Android.
    /// </summary>
    public static class TFYSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string ScenePath = "Assets/Scenes/Boot.unity";
        private static readonly string[] Tiers = { "Low", "Medium", "High", "Ultra" };

        [MenuItem("TFY/1. Setup Project (URP, Quality, Android, Scene)", priority = 1)]
        public static void SetupProject()
        {
            Directory.CreateDirectory(SettingsDir);
            var assets = new UniversalRenderPipelineAsset[4];
            for (int i = 0; i < 4; i++) assets[i] = CreateUrpAsset(Tiers[i], i);
            SetupQualityLevels(assets);
            GraphicsSettings.defaultRenderPipeline = assets[1];
            AddAlwaysIncludedShaders("Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit", "Skybox/Procedural");
            SetupPlayer();
            SetupBranding();
            CreateBootScene();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("TFY Cat Adventure", "Project set up.\n\nPress Play in the Boot scene. Without Firebase the game runs fully offline.\n\nFor Firebase: import the Firebase Unity SDK (Auth, Firestore, Functions, Remote Config, Analytics, Crashlytics), add google-services.json, then TFY ▸ 2. Enable Firebase.", "OK");
        }

        private static UniversalRenderPipelineAsset CreateUrpAsset(string tier, int level)
        {
            string rendererPath = $"{SettingsDir}/TFY_{tier}_Renderer.asset", assetPath = $"{SettingsDir}/TFY_{tier}_URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                if (renderer.postProcessData == null)
                {
                    var get = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (get != null) renderer.postProcessData = (PostProcessData)get.Invoke(null, null);
                }
                if (level >= 2) AddFeature(renderer, "ScreenSpaceAmbientOcclusion");
                EditorUtility.SetDirty(renderer);
            }
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (!asset) { asset = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(asset, assetPath); }

            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", level >= 1);
            Set(so, "m_MSAA", level == 3 ? 4 : level == 2 ? 2 : 1);
            Set(so, "m_RenderScale", level == 0 ? 0.8f : level == 1 ? 0.9f : 1f);
            Set(so, "m_ShadowDistance", new[] { 0f, 35f, 55f, 80f }[level]);
            Set(so, "m_ShadowCascadeCount", level >= 3 ? 2 : 1);
            Set(so, "m_MainLightShadowmapResolution", new[] { 512, 1024, 2048, 2048 }[level]);
            Set(so, "m_MainLightShadowsSupported", level >= 1);
            Set(so, "m_SoftShadowsSupported", level >= 2);
            Set(so, "m_AdditionalLightsPerObjectLimit", new[] { 0, 2, 4, 6 }[level]);
            Set(so, "m_AdditionalLightsRenderingMode", level == 0 ? 0 : 1); // 0 disabled, 1 per pixel
            Set(so, "m_SupportsCameraDepthTexture", level >= 2);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_SupportsDynamicBatching", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AddFeature(UniversalRendererData renderer, string typeName)
        {
            var type = typeof(UniversalRendererData).Assembly.GetTypes().FirstOrDefault(t => t.Name == typeName && typeof(ScriptableRendererFeature).IsAssignableFrom(t));
            if (type == null) { Debug.LogWarning($"[TFY] Renderer feature {typeName} not found in this URP version — skipped."); return; }
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = typeName;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            var so = new SerializedObject(renderer);
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id) ? id : 0; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetupQualityLevels(UniversalRenderPipelineAsset[] assets)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (!qs) { Debug.LogWarning("[TFY] QualitySettings.asset not found."); return; }
            var so = new SerializedObject(qs);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var l = levels.GetArrayElementAtIndex(i);
                SetRel(l, "name", Tiers[i]);
                var rp = l.FindPropertyRelative("customRenderPipeline"); if (rp != null) rp.objectReferenceValue = assets[i];
                SetRel(l, "lodBias", new[] { 0.7f, 1f, 1.5f, 2f }[i]);
                SetRel(l, "particleRaycastBudget", new[] { 16, 64, 256, 1024 }[i]);
                SetRel(l, "vSyncCount", 0);
                SetRel(l, "anisotropicTextures", i >= 2 ? 2 : 1);
                SetRel(l, "realtimeReflectionProbes", i >= 2);
                SetRel(l, "softParticles", i >= 2);
                SetRel(l, "skinWeights", i >= 2 ? 4 : 2);
                SetRel(l, "globalTextureMipmapLimit", i == 0 ? 1 : 0);
            }
            var cur = so.FindProperty("m_CurrentQuality"); if (cur != null) cur.intValue = 1;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var e = perPlatform.GetArrayElementAtIndex(i);
                    var first = e.FindPropertyRelative("first"); var second = e.FindPropertyRelative("second");
                    if (first != null && second != null) second.intValue = first.stringValue == "Standalone" ? 3 : 1;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddAlwaysIncludedShaders(params string[] names)
        {
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (!gs) return;
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders"); if (list == null) return;
            foreach (var n in names)
            {
                var shader = Shader.Find(n); if (!shader) { Debug.LogWarning($"[TFY] Shader {n} not found."); continue; }
                bool present = false; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupPlayer()
        {
            PlayerSettings.companyName = "TFY";
            PlayerSettings.productName = "TFY Cat Adventure";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tfy.catadventure");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.tfy.catadventure");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.gpuSkinning = true;
            // Input System + legacy (both) so editor tools keep working.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps) { var so = new SerializedObject(ps); var p = so.FindProperty("activeInputHandler"); if (p != null) { p.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); } }
        }

        /// <summary>Logo as a UI sprite, and the app icon (Android + iOS) from Assets/Branding/app_icon.png.</summary>
        private static void SetupBranding()
        {
            const string logoPath = "Assets/Resources/Branding/tfy_logo.png", iconPath = "Assets/Branding/app_icon.png";
            if (AssetImporter.GetAtPath(logoPath) is TextureImporter logo)
            { logo.textureType = TextureImporterType.Sprite; logo.alphaIsTransparency = true; logo.mipmapEnabled = false; logo.maxTextureSize = 1024; logo.SaveAndReimport(); }
            if (AssetImporter.GetAtPath(iconPath) is TextureImporter icon) { icon.textureType = TextureImporterType.Default; icon.mipmapEnabled = false; icon.SaveAndReimport(); }
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (iconTex) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Application);
            else Debug.LogWarning("[TFY] App icon not found at " + iconPath);
        }

        private static void CreateBootScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("TFY Cat Adventure").AddComponent<App>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------ Firebase define
        [MenuItem("TFY/2. Enable Firebase (after importing the SDK)", priority = 2)]
        public static void EnableFirebase()
        {
            bool sdk = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("Firebase.FirebaseApp") != null);
            if (!sdk) { EditorUtility.DisplayDialog("Firebase", "Import the Firebase Unity SDK packages first:\nFirebaseAuth, FirebaseFirestore, FirebaseFunctions, FirebaseRemoteConfig, FirebaseAnalytics, FirebaseCrashlytics.\n\nThen add Assets/google-services.json (Android) and GoogleService-Info.plist (iOS).", "OK"); return; }
            if (!File.Exists("Assets/google-services.json")) Debug.LogWarning("[TFY] Assets/google-services.json is missing — download it from the Firebase console (Project settings ▸ Your apps ▸ Android).");
            SetDefine("TFY_FIREBASE", true);
        }

        [MenuItem("TFY/Disable Firebase (offline build)", priority = 3)]
        public static void DisableFirebase() => SetDefine("TFY_FIREBASE", false);

        private static void SetDefine(string define, bool on)
        {
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(d => d.Length > 0 && d != define).ToList();
                if (on) defs.Add(define);
                PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defs));
            }
            Debug.Log($"[TFY] {define} {(on ? "enabled" : "disabled")}.");
        }

        // ------------------------------------------------------------------ level report
        [MenuItem("TFY/Check Levels (fairness report)", priority = 20)]
        public static void CheckLevels()
        {
            var json = Resources.Load<TextAsset>("default_content");
            var content = JsonConvert.DeserializeObject<GameContent>(json.text, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            foreach (var l in content.levels)
            {
                var plan = LevelPlanner.Plan(l, content);
                var t = l.Totals();
                int rows = plan.rows.Count(r => r.obstacleId != null);
                bool exact = plan.Count(PlacementKind.Coin) == t.coins && plan.Count(PlacementKind.StarToken) == t.starTokens && plan.Count(PlacementKind.Gem) == t.gems;
                Debug.Log($"Level {l.number} {l.name} [{l.world}/{l.difficulty}]: {t.length} m, {rows} obstacle rows, {t.coins} coins, {t.fish} fish, {t.gems} gems, {plan.checkpoints.Count} checkpoints — budgets {(exact ? "OK" : "MISMATCH")}");
            }
        }

        private const string UnlockMenu = "TFY/Testing/Unlock All Levels";

        [MenuItem(UnlockMenu, priority = 30)]
        public static void ToggleUnlockAll()
        {
            bool on = PlayerPrefs.GetInt(Data.SaveService.UnlockAllKey, 0) != 1;
            PlayerPrefs.SetInt(Data.SaveService.UnlockAllKey, on ? 1 : 0); PlayerPrefs.Save();
            Menu.SetChecked(UnlockMenu, on);
            if (Application.isPlaying && App.I) { App.I.Save.Recompute(); App.I.UI.Show<UI.HomeScreen>(); }
            Debug.Log($"[TFY] Unlock all levels (testing): {(on ? "ON" : "OFF")}");
        }

        [MenuItem(UnlockMenu, true)]
        private static bool ToggleUnlockAllValidate() { Menu.SetChecked(UnlockMenu, PlayerPrefs.GetInt(Data.SaveService.UnlockAllKey, 0) == 1); return true; }

        [MenuItem("TFY/Build Android APK", priority = 40)]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/TFYCatAdventure.apk", target = BuildTarget.Android, options = BuildOptions.None,
            });
            Debug.Log($"[TFY] Build {report.summary.result}: {report.summary.outputPath} ({report.summary.totalSize / 1048576f:0.0} MB)");
        }

        // ------------------------------------------------------------------ helpers
        private static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.Log($"[TFY] URP setting {name} not present in this version — skipped."); return; }
            Assign(p, value);
        }

        private static void SetRel(SerializedProperty parent, string name, object value) { var p = parent.FindPropertyRelative(name); if (p != null) Assign(p, value); }

        private static void Assign(SerializedProperty p, object value)
        {
            switch (value)
            {
                case bool b: if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = b; else p.intValue = b ? 1 : 0; break;
                case int i: if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i; else p.intValue = i; break;
                case float f: if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum) p.intValue = (int)f; else p.floatValue = f; break;
                case string s: p.stringValue = s; break;
            }
        }
    }
}
