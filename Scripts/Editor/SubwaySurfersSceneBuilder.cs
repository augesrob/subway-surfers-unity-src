// ============================================================
//  SubwaySurfersSceneBuilder.cs
//  Drop this into Assets/Scripts/Editor/
//  Then: Unity menu → Tools → Build Subway Surfers Scene
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-click scene builder for the Subway Surfers Clone.
/// Creates all GameObjects, components, tags, layers, and
/// a starter chunk prefab — ready to hit Play immediately.
/// </summary>
public static class SubwaySurfersSceneBuilder
{
    // ── layer / tag names ────────────────────────────────────
    private const string TAG_PLAYER      = "Player";
    private const string TAG_OBSTACLE    = "Obstacle";
    private const string TAG_LIGHTSIGNAL = "LightSignal";
    private const string LAYER_GROUND    = "Ground";
    private const string LAYER_OBSTACLE  = "Obstacle";

    // ── chunk geometry constants ─────────────────────────────
    private const float LANE_WIDTH   = 3f;
    private const float CHUNK_LENGTH = 54f;
    private const float OBSTACLE_H   = 2f;

    [MenuItem("Tools/Build Subway Surfers Scene #&B")]   // Shift+Alt+B
    public static void BuildScene()
    {
        // ------------------------------------------------------------------
        // 0. Pre-flight: packages check (just a warning, won't stop build)
        // ------------------------------------------------------------------
        Debug.Log("[SceneBuilder] Starting Subway Surfers scene build…");

        // ------------------------------------------------------------------
        // 1. Tags & Layers
        // ------------------------------------------------------------------
        EnsureTag(TAG_PLAYER);
        EnsureTag(TAG_OBSTACLE);
        EnsureTag(TAG_LIGHTSIGNAL);
        int groundLayer   = EnsureLayer(LAYER_GROUND);
        int obstacleLayer = EnsureLayer(LAYER_OBSTACLE);

        // ------------------------------------------------------------------
        // 2. Clear existing scene objects that conflict
        // ------------------------------------------------------------------
        foreach (var n in new[] { "GameManager", "ScoreManager", "PowerupManager",
                                   "Player", "Enemy", "ChunkSpawnerManager",
                                   "GameDifficultyManager", "GameCamera",
                                   "CameraPosIntro", "CameraFollow", "LookAtTarget",
                                   "StartingGround", "UI_Canvas", "[MANAGERS]",
                                   "[GAMEPLAY]", "[CAMERA]", "[UI]" })
        {
            var existing = GameObject.Find(n);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        // ------------------------------------------------------------------
        // 3. Directional Light (if none exists)
        // ------------------------------------------------------------------
        if (Object.FindFirstObjectByType<Light>() == null)
        {
            var lightGO = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(lightGO, "Create Light");
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.95f, 0.85f);
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // ------------------------------------------------------------------
        // 4. Managers group
        // ------------------------------------------------------------------
        var managersRoot = CreateGroup("[MANAGERS]");

        // -- GameManager --
        var gmGO = CreateChild("GameManager", managersRoot);
        var gm   = gmGO.AddComponent<GameManager>();
        gmGO.AddComponent<AudioSource>();

        // -- ScoreManager (will be wired to UI later) --
        var smGO = CreateChild("ScoreManager", managersRoot);
        var sm   = smGO.AddComponent<ScoreManager>();

        // -- PowerupManager (must be named exactly "PowerupManager") --
        var pmGO = new GameObject("PowerupManager");
        Undo.RegisterCreatedObjectUndo(pmGO, "PowerupManager");
        pmGO.transform.SetParent(managersRoot.transform);
        var pm   = pmGO.AddComponent<PowerupManager>();

        // ------------------------------------------------------------------
        // 5. Player
        // ------------------------------------------------------------------
        var gameplayRoot = CreateGroup("[GAMEPLAY]");

        var playerGO = CreateChild("Player", gameplayRoot);
        playerGO.tag = TAG_PLAYER;

        // Visual body (capsule)
        var bodyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyGO.name = "Body";
        bodyGO.transform.SetParent(playerGO.transform);
        bodyGO.transform.localPosition = new Vector3(0, 1f, 0);
        Undo.RegisterCreatedObjectUndo(bodyGO, "Player Body");
        // Paint it a nice blue
        SetColor(bodyGO, new Color(0.2f, 0.5f, 0.9f));

        // AudioSource (PlayerController calls _audioSource.PlayOneShot)
        playerGO.AddComponent<AudioSource>();

        // CharacterController — sits at foot level
        var cc = playerGO.AddComponent<CharacterController>();
        cc.height  = 2f;
        cc.radius  = 0.4f;
        cc.center  = new Vector3(0, 1f, 0);
        cc.skinWidth = 0.01f;

        // Ground check transform (small sphere at feet)
        var groundCheck = new GameObject("GroundCheck");
        Undo.RegisterCreatedObjectUndo(groundCheck, "GroundCheck");
        groundCheck.transform.SetParent(playerGO.transform);
        groundCheck.transform.localPosition = new Vector3(0, 0.1f, 0);

        // ForceReceiver
        var fr = playerGO.AddComponent<ForceReceiver>();
        SetPrivateField(fr, "_groundCheckTransform", groundCheck.transform);
        SetPrivateField(fr, "_checkRadius", 0.35f);
        SetPrivateField(fr, "_groundMask", (LayerMask)(1 << groundLayer));

        // InputReader
        playerGO.AddComponent<InputReader>();

        // PlayerController — auto-wires via GetComponent<> in Awake; just set tuning values
        var pc = playerGO.AddComponent<PlayerController>();
        SetPrivateField(pc, "_dashSpeed",       12f);
        SetPrivateField(pc, "_jumpForce",       14f);
        SetPrivateField(pc, "_rightSidelineX",  4.6f);
        SetPrivateField(pc, "_leftSidelineX",  -4.6f);
        SetPrivateField(pc, "_midSidelineX",    0f);

        playerGO.transform.position = new Vector3(0, 0, 0);

        // ------------------------------------------------------------------
        // 6. GameDifficultyManager
        // ------------------------------------------------------------------
        var gdmGO = CreateChild("GameDifficultyManager", gameplayRoot);
        var gdm   = gdmGO.AddComponent<GameDifficultyManager>();
        SetPrivateField(gdm, "_playerForceReceiver", fr);

        // ------------------------------------------------------------------
        // 7. Starting ground (static plane)
        // ------------------------------------------------------------------
        var startGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
        startGround.name = "StartingGround";
        Undo.RegisterCreatedObjectUndo(startGround, "StartingGround");
        startGround.transform.SetParent(gameplayRoot.transform);
        startGround.transform.position = new Vector3(0, 0, 20f);
        startGround.transform.localScale = new Vector3(1.5f, 1f, 12f); // 15m wide × 120m long
        startGround.layer = groundLayer;
        SetColor(startGround, new Color(0.3f, 0.3f, 0.35f));

        // Remove mesh collider on body (doesn't need it — ground check uses Physics.CheckSphere)
        // But startGround DOES need its collider → leave it.

        // ------------------------------------------------------------------
        // 8. Chunk Spawner + Chunk Prefab
        // ------------------------------------------------------------------
        string chunkPrefabPath = "Assets/Prefabs/Chunks/ChunkBasic.prefab";
        EnsureDirectory("Assets/Prefabs/Chunks");
        GameObject chunkPrefab = CreateOrUpdateChunkPrefab(chunkPrefabPath, groundLayer, obstacleLayer);

        var csmGO = CreateChild("ChunkSpawnerManager", gameplayRoot);
        var csm   = csmGO.AddComponent<ChunkSpawnerManager>();
        SetPrivateField(csm, "_chunks", new GameObject[] { chunkPrefab });
        SetPrivateField(csm, "_chunkLenght", CHUNK_LENGTH);
        SetPrivateField(csm, "_playerTransform", playerGO.transform);
        SetPrivateField(csm, "_spawnDistance", 50f);
        SetPrivateField(csm, "_size", 10);

        // ------------------------------------------------------------------
        // 9. Enemy (placeholder, no model)
        // ------------------------------------------------------------------
        var enemyGO = CreateChild("Enemy", gameplayRoot);
        var en      = enemyGO.AddComponent<EnemyController>();
        SetPrivateField(en, "_approachDistance", new Vector3(0, 0, 3f));
        SetPrivateField(en, "_speed", 2f);
        SetPrivateField(en, "_playerTransform", playerGO.transform);
        // _guardAnimator / _dogAnimator left null (no models yet)
        enemyGO.transform.position = new Vector3(0, 0, -8f);
        // EnemyController.Start() hides itself — that's fine.

        // ------------------------------------------------------------------
        // 10. Camera
        // ------------------------------------------------------------------
        var cameraRoot = CreateGroup("[CAMERA]");

        // Intro position marker
        var introPos = new GameObject("CameraPosIntro");
        Undo.RegisterCreatedObjectUndo(introPos, "CameraPosIntro");
        introPos.transform.SetParent(cameraRoot.transform);
        introPos.transform.position = new Vector3(0, 4f, -12f);
        introPos.transform.rotation = Quaternion.Euler(15f, 0, 0);

        // Follow position marker
        var followPos = new GameObject("CameraFollow");
        Undo.RegisterCreatedObjectUndo(followPos, "CameraFollow");
        followPos.transform.SetParent(cameraRoot.transform);
        followPos.transform.position = new Vector3(0, 6f, -8f);
        followPos.transform.rotation = Quaternion.Euler(12f, 0, 0);

        // Look-at target (sits slightly ahead of player)
        var lookAt = new GameObject("LookAtTarget");
        Undo.RegisterCreatedObjectUndo(lookAt, "LookAtTarget");
        lookAt.transform.SetParent(playerGO.transform);
        lookAt.transform.localPosition = new Vector3(0, 1.5f, 2f);

        // Main Camera
        Camera mainCam = Camera.main;
        GameObject cameraGO;
        if (mainCam == null)
        {
            cameraGO = new GameObject("GameCamera");
            Undo.RegisterCreatedObjectUndo(cameraGO, "GameCamera");
            mainCam = cameraGO.AddComponent<Camera>();
            cameraGO.AddComponent<AudioListener>();
        }
        else
        {
            cameraGO = mainCam.gameObject;
            cameraGO.name = "GameCamera";
        }
        cameraGO.transform.SetParent(cameraRoot.transform);
        cameraGO.transform.position = introPos.transform.position;
        cameraGO.transform.rotation = introPos.transform.rotation;

        var camCtrl = cameraGO.AddComponent<CameraController>();
        SetPrivateField(camCtrl, "_followLerpSpeed",  6f);
        SetPrivateField(camCtrl, "_lookLerpSpeed",    4f);
        SetPrivateField(camCtrl, "_xOffsetDamping",   3f);
        SetPrivateField(camCtrl, "_cameraChangeLimit",0.2f);
        SetPrivateField(camCtrl, "_lookAtTarget",     lookAt.transform);
        SetPrivateField(camCtrl, "_cameraPosAtIntro", introPos.transform);
        SetPrivateField(camCtrl, "_cameraPosAtFollow",followPos.transform);
        SetPrivateField(camCtrl, "_playerTransform",  playerGO.transform);
        SetPrivateField(camCtrl, "_playerForceReceiver", fr);
        SetPrivateField(camCtrl, "_trainHeight",      2.5f);

        // ------------------------------------------------------------------
        // 11. UI Canvas
        // ------------------------------------------------------------------
        var uiRoot = CreateGroup("[UI]");

        var canvasGO = CreateChild("UI_Canvas", uiRoot);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Score text (top-center)
        var scoreTMP = CreateTMP("ScoreText", canvasGO,
            new Vector2(0, -40), new Vector2(400, 60),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            "000000", 36, TextAlignmentOptions.Center);

        // Coin text (top-left)
        var coinTMP = CreateTMP("CoinText", canvasGO,
            new Vector2(100, -40), new Vector2(200, 60),
            new Vector2(0, 1), new Vector2(0, 1),
            "0", 30, TextAlignmentOptions.Left);

        // Wire ScoreManager → TMP
        SetPrivateField(sm, "_scoreText", scoreTMP);
        SetPrivateField(sm, "_coinText",  coinTMP);

        // ------------------------------------------------------------------
        // 12. Wire PowerupManager → ForceReceiver via Start() discovery
        //     (PowerupManager already uses FindObjectOfType<PlayerController>())
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // 13. Mark scene dirty and save
        // ------------------------------------------------------------------
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SceneBuilder] ✅ Scene built successfully! " +
                  "Hit Play to test. Press any key to start the intro sequence.");

        // Summary
        Debug.Log("[SceneBuilder] What to verify in Inspector:\n" +
                  "  • GameManager → assign AudioClips array (8 clips, or leave empty for now)\n" +
                  "  • EnemyController → assign guard/dog Animator (leave null if no model yet)\n" +
                  "  • ChunkSpawnerManager → ChunkBasic prefab is auto-assigned\n" +
                  "  • ForceReceiver → Ground layer mask is set to 'Ground'\n" +
                  "  • ScoreText / CoinText → auto-wired to ScoreManager\n" +
                  "  • PowerupManager → auto-discovers Player via FindObjectOfType");
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static GameObject CreateGroup(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateChild(string name, GameObject parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    private static GameObject CreateOrUpdateChunkPrefab(string path, int groundLayer, int obstacleLayer)
    {
        // Build the chunk as a scene GO, then save as prefab
        var chunkRoot = new GameObject("ChunkBasic");

        // Ground plane
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(chunkRoot.transform);
        ground.transform.localPosition = new Vector3(0, 0, CHUNK_LENGTH * 0.5f);
        ground.transform.localScale    = new Vector3(1.5f, 1f, CHUNK_LENGTH * 0.1f);
        ground.layer = groundLayer;
        SetColor(ground, new Color(0.3f, 0.3f, 0.35f));

        // 3 lane rails (visual)
        for (int i = -1; i <= 1; i++)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = $"Rail_{(i < 0 ? "L" : i > 0 ? "R" : "C")}";
            rail.transform.SetParent(chunkRoot.transform);
            rail.transform.localPosition = new Vector3(i * LANE_WIDTH, 0.05f, CHUNK_LENGTH * 0.5f);
            rail.transform.localScale    = new Vector3(0.2f, 0.1f, CHUNK_LENGTH);
            SetColor(rail, new Color(0.5f, 0.45f, 0.4f));
            DestroyCollider(rail);
        }

        // 2 random obstacle positions
        float[] zPositions = { 10f, 28f, 42f };
        int[] laneOffsets  = { -1, 0, 1 };
        int   obstacleCount = 0;
        foreach (float z in zPositions)
        {
            if (obstacleCount >= 2) break;
            int lane = laneOffsets[Random.Range(0, laneOffsets.Length)];
            var obs  = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obs.name = $"Obstacle_{obstacleCount}";
            obs.transform.SetParent(chunkRoot.transform);
            obs.transform.localPosition = new Vector3(lane * LANE_WIDTH, OBSTACLE_H * 0.5f, z);
            obs.transform.localScale    = new Vector3(2.5f, OBSTACLE_H, 2f);
            obs.layer = obstacleLayer;
            obs.tag   = TAG_OBSTACLE;
            SetColor(obs, new Color(0.6f, 0.2f, 0.1f));
            obstacleCount++;
        }

        // A coin row (5 coins in center lane)
        for (int c = 0; c < 5; c++)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = $"Coin_{c}";
            coin.transform.SetParent(chunkRoot.transform);
            coin.transform.localPosition = new Vector3(0, 0.8f, 15f + c * 1.5f);
            coin.transform.localScale    = Vector3.one * 0.4f;
            SetColor(coin, new Color(1f, 0.85f, 0.1f));
            coin.AddComponent<CoinController>();
            // Switch collider to trigger
            var col = coin.GetComponent<Collider>();
            col.isTrigger = true;
        }

        // Save as prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(chunkRoot, path);
        Object.DestroyImmediate(chunkRoot);
        Debug.Log($"[SceneBuilder] Chunk prefab saved → {path}");
        return prefab;
    }

    private static TextMeshProUGUI CreateTMP(
        string name, GameObject parent,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent.transform);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin      = anchorMin;
        rt.anchorMax      = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta      = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = alignment;
        tmp.color     = Color.white;
        // Add a soft shadow so text is readable over any background
        var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.6f);
        shadow.effectDistance = new Vector2(1, -1);

        return tmp;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                               Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    private static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    // Reflection helper — set private/serialized fields without exposing them
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var type = target.GetType();
        while (type != null)
        {
            var fi = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                fi.SetValue(target, value);
                // Notify Unity serialization
                if (target is UnityEngine.Object uo)
                    EditorUtility.SetDirty(uo);
                return;
            }
            type = type.BaseType;
        }
        Debug.LogWarning($"[SceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
    }

    // ── Tag / Layer helpers ──────────────────────────────────────

    private static void EnsureTag(string tag)
    {
        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tm.ApplyModifiedProperties();
        Debug.Log($"[SceneBuilder] Added tag: {tag}");
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        // Unity user layers start at index 8
        for (int i = 8; i < layers.arraySize; i++)
        {
            var elem = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(elem.stringValue))
            {
                elem.stringValue = layerName;
                tm.ApplyModifiedProperties();
                Debug.Log($"[SceneBuilder] Added layer '{layerName}' at index {i}");
                return i;
            }
        }
        Debug.LogError($"[SceneBuilder] No free layer slot for '{layerName}'!");
        return 0;
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
