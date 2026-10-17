#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Tools/Stage — одноразовая настройка проекта после обновления скриптов.
/// Порядок: «0. Run All» делает всё сразу; остальные пункты можно запускать по отдельности.
/// Скрипт нигде не ссылается на удаляемые типы по имени класса (только по строке), поэтому
/// его можно оставить в проекте после удаления старых скриптов.
/// </summary>
public static class StageSetupTools
{
    private const string MazeFolder = "Assets/Prefabs/Maze";
    private const string MaterialsFolder = "Assets/Prefabs/Maze/Materials";

    [MenuItem("Tools/Stage/0. Run All Setup")]
    public static void RunAll()
    {
        FixBuildSettings();
        CreateMazeItemPrefabs();
        AssignPrefabsToMazeGenerator();
        RemoveLegacyComponents();
        Debug.Log("[Stage] Настройка завершена. Сохраните сцену MazeScene (Ctrl+S) и проверьте Console.");
    }

    // ------------------------------------------------------------------ Build Settings

    [MenuItem("Tools/Stage/1. Fix Build Settings")]
    public static void FixBuildSettings()
    {
        string[] names = { "MainMenu", "Stage", "MazeScene" };
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/Scenes/" + names[i] + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning("[Stage] Не найдена сцена " + path);
                continue;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[Stage] Build Settings: " + string.Join(" → ", names) + " (MainMenu — стартовая).");
    }

    // ------------------------------------------------------------------ Префабы предметов

    [MenuItem("Tools/Stage/2. Create Maze Item Prefabs")]
    public static void CreateMazeItemPrefabs()
    {
        EnsureFolder("Assets/Prefabs", "Maze");
        EnsureFolder(MazeFolder, "Materials");

        SavePrefab(MazeItemFactory.CreateLetter(), MazeFolder + "/Letter.prefab");
        SavePrefab(MazeItemFactory.CreatePictureFrame(), MazeFolder + "/PictureFrame.prefab");
        SavePrefab(MazeItemFactory.CreateExitButton(), MazeFolder + "/ExitButton.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Stage] Префабы Letter / PictureFrame / ExitButton созданы в " + MazeFolder);
    }

    private static void SavePrefab(GameObject instance, string path)
    {
        // Материалы, созданные в памяти, нужно превратить в ассеты — иначе в префабе останутся пустые ссылки.
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] mats = renderers[r].sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null || EditorUtility.IsPersistent(mats[m])) continue;
                string matPath = AssetDatabase.GenerateUniqueAssetPath(MaterialsFolder + "/" + mats[m].name + ".mat");
                AssetDatabase.CreateAsset(mats[m], matPath);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    // ------------------------------------------------------------------ Подключение к генератору

    [MenuItem("Tools/Stage/3. Assign Prefabs To MazeRoomGenerator (open scene)")]
    public static void AssignPrefabsToMazeGenerator()
    {
        MazeRoomGenerator generator = Object.FindFirstObjectByType<MazeRoomGenerator>();
        if (generator == null)
        {
            Debug.LogWarning("[Stage] Откройте сцену MazeScene — в ней находится MazeRoomGenerator (объект RoomManager).");
            return;
        }

        SerializedObject so = new SerializedObject(generator);
        SetPrefab(so, "floorPrefab", "Assets/Prefabs/Floor.prefab");
        SetPrefab(so, "wallPrefab", "Assets/Prefabs/Wall.prefab");
        SetPrefab(so, "ceilingPrefab", "Assets/Prefabs/Ceiling.prefab");
        SetPrefab(so, "tablePrefab", "Assets/Prefabs/Table.prefab");
        SetPrefab(so, "torchPrefab", "Assets/Prefabs/Torch.prefab");
        SetPrefab(so, "letterPrefab", MazeFolder + "/Letter.prefab");
        SetPrefab(so, "picturePrefab", MazeFolder + "/PictureFrame.prefab");
        SetPrefab(so, "exitButtonPrefab", MazeFolder + "/ExitButton.prefab");

        SerializedProperty decor = so.FindProperty("decorPrefabs");
        if (decor != null)
        {
            string[] decorPaths = { "Assets/Prefabs/Chest.prefab", "Assets/Prefabs/Barrel.prefab" };
            decor.arraySize = decorPaths.Length;
            for (int i = 0; i < decorPaths.Length; i++)
                decor.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(decorPaths[i]);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Debug.Log("[Stage] Префабы назначены в MazeRoomGenerator. Сохраните сцену (Ctrl+S).");
    }

    private static void SetPrefab(SerializedObject so, string field, string path)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning("[Stage] В MazeRoomGenerator нет поля " + field);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogWarning("[Stage] Префаб не найден: " + path + " (будет использована заглушка)");
        p.objectReferenceValue = prefab;
    }

    // ------------------------------------------------------------------ Чистка устаревших компонентов

    private static readonly string[] LegacyTypeNames =
    {
        "UIEventFixer", "UIClickableFixer", "NPCRotation", "AudioSyncController"
    };

    [MenuItem("Tools/Stage/4. Remove Legacy Components (UI fixers, NPCRotation)")]
    public static void RemoveLegacyComponents()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int removed = 0;

        for (int g = 0; g < guids.Length; g++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[g]);
            if (!path.StartsWith("Assets/")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int local = 0;

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null) continue; // «Missing Script» пропускаем
                if (System.Array.IndexOf(LegacyTypeNames, mb.GetType().Name) < 0) continue;

                try
                {
                    Object.DestroyImmediate(mb, true);
                    local++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Stage] Не удалось удалить " + mb.GetType().Name + " с " + path + " автоматически — удалите вручную. " + e.Message);
                }
            }

            if (local > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                removed += local;
                Debug.Log("[Stage] " + path + ": удалено компонентов — " + local);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Объекты в открытых сценах (AudioSyncController висит на Speakers/MusicPlayer в Stage).
        MonoBehaviour[] sceneObjects = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            MonoBehaviour mb = sceneObjects[i];
            if (mb == null || EditorUtility.IsPersistent(mb)) continue;
            if (System.Array.IndexOf(LegacyTypeNames, mb.GetType().Name) < 0) continue;

            UnityEngine.SceneManagement.Scene scene = mb.gameObject.scene;
            Object.DestroyImmediate(mb);
            EditorSceneManager.MarkSceneDirty(scene);
            removed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Stage] Удалено устаревших компонентов: " + removed +
                  ". Если сцена Stage открыта — сохраните её (Ctrl+S). Теперь можно удалить файлы " +
                  "UIEventFixer.cs, UIClickableFixer.cs, NPCRotation.cs и AudioSyncController.cs.");
    }
    // ------------------------------------------------------------------ Телефоны

    private const string TouchMenuPath = "Tools/Stage/Mobile/Touch Controls In Editor";

    [MenuItem(TouchMenuPath)]
    public static void ToggleTouchControlsInEditor()
    {
        bool enabled = !EditorPrefs.GetBool(PlatformUtil.EditorTouchKey, false);
        EditorPrefs.SetBool(PlatformUtil.EditorTouchKey, enabled);
        PlatformUtil.Refresh();
        Debug.Log("[Stage] Экранное управление в редакторе: " + (enabled ? "ВКЛЮЧЕНО (запустите игру: джойстик и кнопки работают мышью)" : "выключено"));
    }

    [MenuItem(TouchMenuPath, true)]
    private static bool ToggleTouchControlsValidate()
    {
        Menu.SetChecked(TouchMenuPath, EditorPrefs.GetBool(PlatformUtil.EditorTouchKey, false));
        return true;
    }

    [MenuItem("Tools/Stage/Mobile/Apply Recommended Player Settings")]
    public static void ApplyMobilePlayerSettings()
    {
        // Только ландшафт.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // Android: IL2CPP и 64-бит (Google Play принимает только такие сборки).
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        Debug.Log("[Stage] Мобильные настройки применены: ландшафт, Android — IL2CPP + ARM64. " +
                  "Остальное (Active Input Handling, Graphics API, URP-ассет для телефона) — по README_STAGE_FIXES.md.");
    }

    [MenuItem("Tools/Stage/Mobile/Limit Texture Size For Phones (1024)")]
    public static void LimitTexturesForPhones()
    {
        const int limit = 1024;
        string[] platforms = { "Android", "iPhone" };
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                if (path.StartsWith("Assets/TextMesh Pro") || path.StartsWith("Assets/Samples")) continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.maxTextureSize <= limit) continue;

                for (int p = 0; p < platforms.Length; p++)
                {
                    TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings(platforms[p]);
                    ps.overridden = true;
                    ps.maxTextureSize = limit;
                    ps.textureCompression = TextureImporterCompression.Compressed;   // ASTC выбирается автоматически
                    importer.SetPlatformTextureSettings(ps);
                }
                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Stage] Для Android и iOS максимальный размер текстур ограничен " + limit + " px у " + changed +
                  " текстур. На ПК они не изменились.");
    }
    // ------------------------------------------------------------------ Уборка проекта

    // Не подключены нигде — можно удалять в любой момент.
    private static readonly string[] DeadScripts =
    {
        "AnimatedButton", "ButtonSounds", "CursorManager", "OutlineWhenNear", "PauseMenu", "UnityHelper"
    };

    // Раньше были подключены в префабах/сцене: удаляются, только если на них больше нигде нет ссылок.
    private static readonly string[] ReplacedScripts =
    {
        "UIClickableFixer", "UIEventFixer", "NPCRotation", "AudioSyncController"
    };

    [MenuItem("Tools/Stage/5. Delete Unused Scripts")]
    public static void DeleteUnusedScripts()
    {
        List<string> toDelete = new List<string>();
        List<string> skipped = new List<string>();
        CollectDeletable(DeadScripts, toDelete, skipped);
        CollectDeletable(ReplacedScripts, toDelete, skipped);

        if (toDelete.Count == 0 && skipped.Count == 0)
        {
            Debug.Log("[Stage] Неиспользуемых скриптов не найдено — всё уже чисто.");
            return;
        }

        string message = toDelete.Count > 0
            ? "Будут удалены:\n" + string.Join("\n", toDelete)
            : "Удалять нечего.";
        if (skipped.Count > 0)
            message += "\n\nПропущены — на них ещё есть ссылки (сначала запустите «4. Remove Legacy Components» с открытой сценой Stage):\n" + string.Join("\n", skipped);

        if (toDelete.Count == 0)
        {
            Debug.LogWarning("[Stage] " + message);
            return;
        }

        if (!EditorUtility.DisplayDialog("Удалить неиспользуемые скрипты?", message, "Удалить", "Отмена")) return;

        for (int i = 0; i < toDelete.Count; i++) AssetDatabase.DeleteAsset(toDelete[i]);
        AssetDatabase.Refresh();
        Debug.Log("[Stage] Удалено скриптов: " + toDelete.Count);
    }

    private static void CollectDeletable(string[] names, List<string> toDelete, List<string> skipped)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/Scripts/" + names[i] + ".cs";
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) == null) continue;

            string user = FindReferencer(path);
            if (user == null) toDelete.Add(path);
            else skipped.Add(names[i] + "  ←  " + user);
        }
    }

    // Сцена или префаб, который использует скрипт (или null).
    private static string FindReferencer(string scriptPath)
    {
        string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        for (int pass = 0; pass < 2; pass++)
        {
            string[] guids = pass == 0 ? scenes : prefabs;
            for (int g = 0; g < guids.Length; g++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[g]);
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                if (System.Array.IndexOf(dependencies, scriptPath) >= 0) return assetPath;
            }
        }
        return null;
    }

    [MenuItem("Tools/Stage/6. Check Legacy Input Usage")]
    public static void CheckLegacyInputUsage()
    {
        System.Text.RegularExpressions.Regex legacy = new System.Text.RegularExpressions.Regex(
            @"\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw|GetButton|GetButtonDown|GetButtonUp|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp|mousePosition|mouseScrollDelta|touchCount|GetTouch|touches|anyKey|anyKeyDown|acceleration)\b");

        string[] files = System.IO.Directory.GetFiles("Assets", "*.cs", System.IO.SearchOption.AllDirectories);
        int hits = 0;
        for (int f = 0; f < files.Length; f++)
        {
            string path = files[f].Replace('\\', '/');
            if (path.Contains("/TextMesh Pro/")) continue;

            string[] lines = System.IO.File.ReadAllLines(files[f]);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith("//") || !legacy.IsMatch(line)) continue;
                hits++;
                Debug.LogWarning("[Stage] Старый Input: " + path + ":" + (i + 1) + "   " + line);
            }
        }

        if (hits == 0)
            Debug.Log("[Stage] Скриптов со старым Input в Assets не найдено — можно ставить Active Input Handling = Input System Package (New). " +
                      "(Пакеты из папки Packages эта проверка не смотрит.)");
        else
            Debug.LogWarning("[Stage] Найдено мест со старым Input: " + hits + ". Удалите/перепишите эти скрипты перед переключением на Input System Package (New) — иначе при запуске будет InvalidOperationException.");
    }
}
#endif
