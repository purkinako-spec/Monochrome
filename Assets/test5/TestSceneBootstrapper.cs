using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using Monochrome.Core.Character;
using Monochrome.Core.Input;
using Monochrome.Core.Transition;
using Monochrome.Core.Dialogue;
using Monochrome.Core.Save;
using InteractionSystem2D;
using Runtime.Dialogue.Logic;

namespace Monochrome.Test
{
    /// <summary>
    /// Test5シーン用ブートストラッパー。
    /// シーン起動時にプレイヤー、NPC、SpawnPoint、AreaDoor 等すべてのテスト用
    /// GameObjectをプログラムで生成・構成し、デバッグUIで状態を可視化します。
    ///
    /// テスト対象:
    ///   Phase 1: InputSystem / ActionBlocker
    ///   Phase 2: CharacterMotor2D / PlayerController2D
    ///   Phase 3: CharacterNavigator2D (Wander / Patrol) / CharacterVisual2D / EmotionBalloon
    ///   Phase 4: AreaTransitionManager / ScreenFader / SpawnPoint / AreaDoor2D (IntraScene)
    ///   Phase 5: (Minigame — 構造テストのみ、ミニゲームシーンは別途必要)
    ///   Phase 6: DialogueSelector / GameSaveManager / FlagManager
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TestSceneBootstrapper : MonoBehaviour
    {
        // ========== Runtime References ==========
        private PlayerController2D playerCtrl;
        private CharacterNavigator2D npcElderNav;
        private CharacterNavigator2D npcGuardNav;
        private Transform cameraTarget;

        private int interactableLayer;

        // ========== Debug UI ==========
        private readonly List<string> eventLog = new List<string>();
        private const int MAX_LOG = 20;
        private Vector2 debugScroll;
        private Texture2D bgTex;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            Debug.Log("[Test5] ===== TestSceneBootstrapper Awake =====");

            interactableLayer = ResolveLayer("Interactable", 8);

            SetupCamera();
            CreateGround();

            var player = CreatePlayer();
            cameraTarget = player.transform;

            CreateNPC_Elder();
            CreateNPC_Guard();
            CreateSpawnPoints();
            CreateAreaDoors();

            Log("All GameObjects created");
            Debug.Log("[Test5] ===== Setup Complete =====");
        }

        private IEnumerator Start()
        {
            // 全 Awake 完了後に実行
            yield return null;

            SetupFlags();

            // 初期フェードイン
            var fader = ScreenFader.Instance;
            if (fader != null && fader.IsFadedOut)
            {
                _ = fader.FadeInAsync(0.5f);
                Log("Fade-in triggered");
            }

            Log("Systems ready — move with WASD / Gamepad");
        }

        private void LateUpdate()
        {
            // 簡易カメラ追従
            if (cameraTarget != null && Camera.main != null)
            {
                var p = cameraTarget.position;
                var cam = Camera.main.transform;
                cam.position = Vector3.Lerp(cam.position,
                    new Vector3(p.x, p.y, -10f), Time.deltaTime * 8f);
            }
        }

        // =====================================================================
        //  Camera & Ground
        // =====================================================================

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.13f);
            cam.transform.position = new Vector3(0, 0, -10);
        }

        private void CreateGround()
        {
            var ground = new GameObject("Ground");
            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSprite(new Color(0.18f, 0.20f, 0.22f), 4);
            sr.sortingOrder = -10;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(40, 40);
            ground.transform.position = Vector3.zero;

            // グリッド目安線
            for (int i = -10; i <= 10; i++)
            {
                MakeLine($"H{i}", new Vector3(0, i, 0), new Vector3(30, 0.015f, 1));
                MakeLine($"V{i}", new Vector3(i, 0, 0), new Vector3(0.015f, 30, 1));
            }
        }

        private void MakeLine(string n, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject($"Grid_{n}");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSprite(new Color(0.28f, 0.30f, 0.33f, 0.25f), 1);
            sr.sortingOrder = -9;
        }

        // =====================================================================
        //  Player
        // =====================================================================

        private GameObject CreatePlayer()
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            go.transform.position = new Vector3(0, -2, 0);

            // Visual
            AddVisualChild(go, "Visual", MakeCharSprite(new Color(0.25f, 0.6f, 1f)), 10);

            // Physics
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var physCol = go.AddComponent<BoxCollider2D>();
            physCol.size = new Vector2(0.7f, 0.7f);
            physCol.offset = Vector2.zero;

            // Motor & Controller
            go.AddComponent<CharacterMotor2D>();
            playerCtrl = go.AddComponent<PlayerController2D>();

            SetupPlayerInput(playerCtrl);

            // Detection zone (child)
            var dz = new GameObject("DetectionZone");
            dz.transform.SetParent(go.transform);
            dz.transform.localPosition = Vector3.zero;
            var detCol = dz.AddComponent<CircleCollider2D>();
            detCol.isTrigger = true;
            detCol.radius = 1.5f;
            var interactor = dz.AddComponent<PlayerInteractor2D>();
            SetupInteractor(interactor, detCol);

            // ラベル
            MakeLabel(go.transform, "Player", new Vector3(0, 0.75f, 0),
                new Color(0.4f, 0.75f, 1f));

            Debug.Log("[Test5] Player created");
            return go;
        }

        private void SetupPlayerInput(PlayerController2D ctrl)
        {
            try
            {
                var asset = FindInputAsset();
                if (asset == null) { Debug.LogWarning("[Test5] InputActionAsset not found"); return; }

                var map = asset.FindActionMap("Player");
                if (map == null) { Debug.LogWarning("[Test5] Player ActionMap not found"); return; }

                SetRef(ctrl, "moveAction",   map.FindAction("Move"));
                SetRef(ctrl, "sprintAction", map.FindAction("Sprint"));
                map.Enable();
                Debug.Log("[Test5] Player input bound (Move + Sprint)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Test5] Input setup failed: {e.Message}");
            }
        }

        private void SetupInteractor(PlayerInteractor2D interactor, Collider2D detCol)
        {
            try
            {
                SetField(interactor, "detectionCollider", detCol);
                SetField(interactor, "interactableLayerMask", (LayerMask)(1 << interactableLayer));
                SetField(interactor, "useTriggerCollider", true);

                var asset = FindInputAsset();
                if (asset == null) return;

                var interactAction = asset.FindActionMap("Player")?.FindAction("Interact");
                if (interactAction == null) return;
                var interactRef = InputActionReference.Create(interactAction);

                // ActionKeyMapping はネスト型 — リフレクションで生成
                var mappingType = typeof(PlayerInteractor2D)
                    .GetNestedType("ActionKeyMapping",
                        BindingFlags.Public | BindingFlags.NonPublic);

                if (mappingType != null)
                {
                    var mapping = Activator.CreateInstance(mappingType);
                    SetFieldOn(mapping, mappingType, "actionReference", interactRef);
                    SetFieldOn(mapping, mappingType, "logicalKey", "Interact");

                    var listType = typeof(List<>).MakeGenericType(mappingType);
                    var list = Activator.CreateInstance(listType);
                    listType.GetMethod("Add").Invoke(list, new[] { mapping });

                    SetField(interactor, "actionKeyMappings", list);
                    Debug.Log("[Test5] Interactor: Interact key mapped");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Test5] Interactor setup failed: {e.Message}");
            }
        }

        // =====================================================================
        //  NPC — Elder (Wander)
        // =====================================================================


        private Runtime.Dialogue.Core.DialogueContainer CreateTestDialogue(string speaker, string[] messages)
        {
            var container = ScriptableObject.CreateInstance<Runtime.Dialogue.Core.DialogueContainer>();
            container.name = $"Dialogue_{speaker}";
            container.startNodeID = "Node_0";

            for (int i = 0; i < messages.Length; i++)
            {
                var node = new Runtime.Dialogue.Core.DialogueNode
                {
                    nodeID = $"Node_{i}",
                    speakerName = speaker,
                    dialogueText = messages[i],
                    nextNodeID = i < messages.Length - 1 ? $"Node_{i + 1}" : ""
                };
                container.allNodes.Add(node);
            }
            return container;
        }

        private void CreateNPC_Elder()
        {
            var go = MakeNPCBase("NPC_Elder", new Vector3(3, 2, 0),
                new Color(1f, 0.4f, 0.3f), "Elder");

            var nav = go.AddComponent<CharacterNavigator2D>();
            npcElderNav = nav;
            SetField(nav, "defaultMode", NavigationMode.Wander);
            SetField(nav, "wanderRadius", 2.5f);
            SetField(nav, "minWanderWait", 1.5f);
            SetField(nav, "maxWanderWait", 4.0f);

            go.AddComponent<CharacterVisual2D>();
            go.AddComponent<EmotionBalloonController>();

            var sel = go.AddComponent<DialogueSelector>();
            SetField(sel, "npcId", "npc_test_elder");
            SetField(sel, "requiredKey", "Interact");
            SetField(sel, "talkActionDescription", "話す");
            sel.SetDefaultDirectContainer(CreateTestDialogue("長老", new[] {
                "おお、旅の人か。よくぞ訪ねてくれたのう。",
                "この村は今、静かな時間を過ごしておるよ。",
                "またいつでも話しかけておくれ。"
            }));

            Debug.Log("[Test5] NPC_Elder created (Wander, DialogueSelector)");
        }

        // =====================================================================
        //  NPC — Guard (Patrol)
        // =====================================================================

        private void CreateNPC_Guard()
        {
            var go = MakeNPCBase("NPC_Guard", new Vector3(-3, 0, 0),
                new Color(0.85f, 0.65f, 0.2f), "Guard");

            // パトロール用ウェイポイント
            var wps = new List<Transform>();
            Vector3[] positions =
            {
                new(-5, 0, 0), new(-5, 4, 0),
                new(-1, 4, 0), new(-1, 0, 0)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var wp = new GameObject($"PatrolWP_{i}");
                wp.transform.position = positions[i];
                wps.Add(wp.transform);
                // ウェイポイント可視化
                AddVisualChild(wp, "Dot",
                    MakeSprite(new Color(0.85f, 0.65f, 0.2f, 0.25f), 4), 1,
                    new Vector3(0.3f, 0.3f, 1));
            }

            var nav = go.AddComponent<CharacterNavigator2D>();
            npcGuardNav = nav;
            SetField(nav, "defaultMode", NavigationMode.Patrol);
            SetField(nav, "patrolWaypoints", wps);
            SetField(nav, "waypointWaitDuration", 1.5f);
            SetField(nav, "patrolStyle", MovementStyle.Walk);

            go.AddComponent<CharacterVisual2D>();
            go.AddComponent<EmotionBalloonController>();

            var sel = go.AddComponent<DialogueSelector>();
            SetField(sel, "npcId", "npc_test_guard");
            SetField(sel, "requiredKey", "Interact");
            SetField(sel, "talkActionDescription", "話す");
            sel.SetDefaultDirectContainer(CreateTestDialogue("警備兵", new[] {
                "異常なし！村の周囲を巡回警備中だ。",
                "怪しい気配を感じたらすぐに知らせてくれ！"
            }));

            Debug.Log("[Test5] NPC_Guard created (Patrol 4WP, DialogueSelector)");
        }

        /// <summary>NPC 共通ベースを作成</summary>
        private GameObject MakeNPCBase(string name, Vector3 pos, Color color, string label)
        {
            var go = new GameObject(name);
            go.layer = interactableLayer;
            go.transform.position = pos;

            AddVisualChild(go, "Visual", MakeCharSprite(color), 10);
            MakeLabel(go.transform, label, new Vector3(0, 0.75f, 0), color);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.0f, 1.0f);

            go.AddComponent<CharacterMotor2D>();
            return go;
        }

        // =====================================================================
        //  Spawn Points
        // =====================================================================

        private void CreateSpawnPoints()
        {
            MakeSpawn("Default",     new Vector3( 0, -2, 0), Vector2.up);
            MakeSpawn("WarpTarget_A", new Vector3(-6,  5, 0), Vector2.right);
            MakeSpawn("WarpTarget_B", new Vector3( 6, -4, 0), Vector2.left);

            Debug.Log("[Test5] 3 SpawnPoints created");
        }

        private void MakeSpawn(string id, Vector3 pos, Vector2 facing)
        {
            var go = new GameObject($"Spawn_{id}");
            go.transform.position = pos;
            var sp = go.AddComponent<SpawnPoint>();
            SetField(sp, "spawnId", id);
            SetField(sp, "facingDirection", facing);

            AddVisualChild(go, "Marker",
                MakeSprite(new Color(1f, 1f, 0.3f, 0.35f), 8), 1,
                new Vector3(0.5f, 0.5f, 1));
            MakeLabel(go.transform, $"SP:{id}",
                new Vector3(0, -0.5f, 0), new Color(1f, 1f, 0.4f));
        }

        // =====================================================================
        //  Area Doors (IntraScene)
        // =====================================================================

        private void CreateAreaDoors()
        {
            // 右端 → WarpA (InteractOnly)
            MakeDoor("Door_ToA", new Vector3(6, 0, 0),
                "WarpTarget_A", DoorActivationMode.InteractOnly, "[F] WarpA");
            // 左端 → WarpB (TouchOnly — 触れるだけでワープ)
            MakeDoor("Door_ToB", new Vector3(-6, -2, 0),
                "WarpTarget_B", DoorActivationMode.TouchOnly, "[Touch] WarpB");
            // WarpA 地点からの帰還
            MakeDoor("Door_RetA", new Vector3(-7, 5, 0),
                "Default", DoorActivationMode.InteractOnly, "[F] Return");
            // WarpB 地点からの帰還
            MakeDoor("Door_RetB", new Vector3(7, -4, 0),
                "Default", DoorActivationMode.InteractOnly, "[F] Return");

            Debug.Log("[Test5] 4 AreaDoors created (2 InteractOnly, 1 TouchOnly, 1 Return)");
        }

        private void MakeDoor(string name, Vector3 pos, string spawnId,
            DoorActivationMode mode, string label)
        {
            var go = new GameObject(name);
            go.layer = interactableLayer;
            go.transform.position = pos;

            AddVisualChild(go, "Visual",
                MakeSprite(new Color(0.3f, 0.9f, 0.4f), 8), 5,
                new Vector3(0.6f, 0.9f, 1));
            MakeLabel(go.transform, label, new Vector3(0, 0.8f, 0),
                new Color(0.4f, 1f, 0.5f));

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 1.2f);

            var door = go.AddComponent<AreaDoor2D>();
            SetField(door, "triggerType", DoorTriggerType.IntraScene);
            SetField(door, "spawnPointId", spawnId);
            SetField(door, "activationMode", mode);
            SetField(door, "requiredKey", "Interact");
            SetField(door, "fadeDuration", 0.3f);
        }

        // =====================================================================
        //  Flag Manager
        // =====================================================================

        private void SetupFlags()
        {
            try
            {
                // FlagManager が存在しない場合は生成
                var fm = UnityEngine.Object.FindFirstObjectByType<FlagManager>();
                if (fm == null)
                {
                    var fmGo = new GameObject("FlagManager");
                    fm = fmGo.AddComponent<FlagManager>();
                    Debug.Log("[Test5] FlagManager created");
                }

                fm.SetFlag("has_fishing_rod", 1);
                fm.SetFlag("test_flag_example", 0);
                fm.SetBool("quest_started", false);

                fm.OnFlagChanged += (key, val) => Log($"Flag: {key} = {val}");
                Debug.Log("[Test5] Flags initialized");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Test5] FlagManager setup error: {e.Message}");
            }
        }

        // =====================================================================
        //  Visual Helpers
        // =====================================================================

        private static Sprite MakeSprite(Color c, int size)
        {
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeCharSprite(Color c)
        {
            const int S = 32;
            var tex = new Texture2D(S, S);
            tex.filterMode = FilterMode.Point;
            var px = new Color[S * S];

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 15.5f, dy = y - 15.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * S + x] = d < 12 ? c : (d < 13 ? c * 0.55f : Color.clear);
                    px[y * S + x].a = d < 13 ? 1f : 0f;
                }
            // 目
            px[20 * S + 12] = px[20 * S + 13] = Color.white;
            px[20 * S + 19] = px[20 * S + 20] = Color.white;

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S),
                new Vector2(0.5f, 0.5f), 32);
        }

        private static void AddVisualChild(GameObject parent, string name,
            Sprite sprite, int order, Vector3? scale = null)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = scale ?? Vector3.one;
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
        }

        private static void MakeLabel(Transform parent, string text,
            Vector3 offset, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 28;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sortingOrder = 100;
        }

        // =====================================================================
        //  Reflection & Input Helpers
        // =====================================================================

        private InputActionAsset _cachedAsset;

        private InputActionAsset FindInputAsset()
        {
            if (_cachedAsset != null) return _cachedAsset;
            foreach (var a in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                if (a.name.Contains("InputSystem"))
                {
                    _cachedAsset = a;
                    return a;
                }
            }
            return null;
        }

        private static void SetRef(object target, string field, InputAction action)
        {
            if (action == null) return;
            SetField(target, field, InputActionReference.Create(action));
        }

        private static void SetField(object target, string name, object value)
        {
            var fi = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fi != null) fi.SetValue(target, value);
            else Debug.LogWarning($"[Test5] Field '{name}' not found on {target.GetType().Name}");
        }

        private static void SetFieldOn(object target, Type type, string name, object value)
        {
            var fi = type.GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            fi?.SetValue(target, value);
        }

        private static int ResolveLayer(string layerName, int fallback)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0) return l;
            Debug.LogWarning($"[Test5] Layer '{layerName}' not found, using {fallback}");
            return fallback;
        }

        // =====================================================================
        //  Debug UI (OnGUI)
        // =====================================================================

        private void Log(string msg)
        {
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (eventLog.Count > MAX_LOG) eventLog.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (bgTex == null)
            {
                bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
                bgTex.Apply();
            }

            var box = new GUIStyle(GUI.skin.box) { normal = { background = bgTex } };
            var rich = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            small.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            float w = 310, h = Screen.height - 20;
            GUILayout.BeginArea(new Rect(10, 10, w, h), box);

            GUILayout.Label("<b><color=#88ccff>Test5 Debug Panel</color></b>", rich);
            GUILayout.Space(4);

            // --- Player ---
            GUILayout.Label("<color=#ffcc44>── Player ──</color>", rich);
            if (playerCtrl != null)
            {
                var m = playerCtrl.Motor;
                var bs = playerCtrl.BlockState;
                GUILayout.Label($" Pos: {playerCtrl.transform.position:F1}", small);
                if (m != null)
                {
                    GUILayout.Label($" Facing: {m.FacingDirection}  Moving: {m.IsMoving}", small);
                    GUILayout.Label($" Style: {m.CurrentStyle}  Vel: {m.Velocity:F2}", small);
                }
                string bc = bs.IsBlocked ? "#ff6666" : "#66ff66";
                GUILayout.Label($" <color={bc}>Blocked: {bs.IsBlocked} ({bs.CurrentBlockers})</color>", rich);
            }
            GUILayout.Space(3);

            // --- NPC ---
            GUILayout.Label("<color=#ffcc44>── NPC ──</color>", rich);
            if (npcElderNav != null)
                GUILayout.Label($" Elder: {npcElderNav.CurrentMode}  pos={npcElderNav.transform.position:F1}", small);
            if (npcGuardNav != null)
                GUILayout.Label($" Guard: {npcGuardNav.CurrentMode}  pos={npcGuardNav.transform.position:F1}", small);
            GUILayout.Space(3);

            // --- Flags ---
            GUILayout.Label("<color=#ffcc44>── Flags ──</color>", rich);
            var fm = UnityEngine.Object.FindFirstObjectByType<FlagManager>();
            if (fm != null)
            {
                GUILayout.Label($" has_fishing_rod = {fm.GetFlag("has_fishing_rod")}", small);
                GUILayout.Label($" quest_started   = {fm.GetBool("quest_started")}", small);
                GUILayout.Label($" talk_elder       = {fm.GetFlag("talk_count_npc_test_elder")}", small);
                GUILayout.Label($" talk_guard       = {fm.GetFlag("talk_count_npc_test_guard")}", small);
            }
            GUILayout.Space(3);

            // --- Systems ---
            GUILayout.Label("<color=#ffcc44>── Systems ──</color>", rich);
            GUILayout.Label($" Transitioning: {AreaTransitionManager.Instance?.IsTransitioning}", small);
            GUILayout.Label($" Faded: {ScreenFader.Instance?.IsFadedOut}", small);
            GUILayout.Label($" Device: {InputDeviceManager.Instance?.CurrentDeviceType}", small);
            GUILayout.Space(3);

            // --- Buttons ---
            GUILayout.Label("<color=#ffcc44>── Actions ──</color>", rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save(0)"))
            {
                bool ok = GameSaveManager.Instance?.SaveGame(0) ?? false;
                Log($"Save → {ok}");
            }
            if (GUILayout.Button("Load(0)"))
            {
                _ = GameSaveManager.Instance?.LoadGame(0);
                Log("Load requested");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle quest"))
                fm?.Toggle("quest_started");
            if (GUILayout.Button("Fade test"))
                StartCoroutine(CoTestFade());
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // --- Log ---
            GUILayout.Label("<color=#ffcc44>── Log ──</color>", rich);
            debugScroll = GUILayout.BeginScrollView(debugScroll, GUILayout.Height(140));
            for (int i = eventLog.Count - 1; i >= 0; i--)
                GUILayout.Label($" <color=#aaa>{eventLog[i]}</color>", rich);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private IEnumerator CoTestFade()
        {
            Log("Fade test start");
            var f = ScreenFader.Instance;
            if (f == null) yield break;
            var t = f.FadeOutAsync(0.4f);
            while (!t.IsCompleted) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            t = f.FadeInAsync(0.4f);
            while (!t.IsCompleted) yield return null;
            Log("Fade test done");
        }
    }
}
