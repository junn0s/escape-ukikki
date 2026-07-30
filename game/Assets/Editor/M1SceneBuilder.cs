using System.Collections.Generic;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Players;
using MonkeyLab.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// M1 2D 그레이박스 씬을 코드로 구성한다.
    /// 방 3개 + 연결 복도, 플레이어, 괴물 1마리, 퓨즈 미션 스테이션.
    ///
    /// 맵 수치는 docs/map-level-design.md §3 그레이박스 시작값을 따른다.
    /// 길찾기 셀 0.5m의 정수배로 맞춘다.
    /// </summary>
    public static class M1SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/91_GameplaySandbox.unity";
        private const string BalancePath = "Assets/_Project/Data/Balance/SO_GameBalance_Default.asset";

        private const float CellSize = 0.5f;
        private const float WallThickness = 0.5f;
        private const float CorridorWidth = 3f;
        private const float DoorWidth = 2f;

        // 방 3개: 전력 복구실(미션) — 복도 — 실험실 — 복도 — 백신실
        private static readonly RoomSpec[] Rooms =
        {
            new("Room_PowerRestore", new Vector2(-14f, 0f), new Vector2(8f, 10f)),
            new("Room_LabA",         new Vector2(0f, 0f),   new Vector2(10f, 12f)),
            new("Room_VaccineA",     new Vector2(14f, 0f),  new Vector2(8f, 10f))
        };

        public static void Run()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SO_GameBalance balance = AssetDatabase.LoadAssetAtPath<SO_GameBalance>(BalancePath);
            if (balance == null)
            {
                Debug.LogError($"[M1] Balance 에셋을 찾을 수 없다: {BalancePath}");
                return;
            }

            EnsureWallLayerExists();

            CreateLighting();
            GameObject environment = CreateEnvironment();
            NoiseService noiseService = CreateNoiseService(balance);

            GameObject player = CreatePlayer(balance);
            Camera camera = CreateCamera(player.transform);

            var motor = player.GetComponent<PlayerMotor>();
            SetPrivateField(motor, "_viewCamera", camera);

            FuseMissionStation station = CreateMissionStation(noiseService);
            MonsterBrain monster = CreateMonster(balance, noiseService, player.transform);

            CreateDebugHud(player, monster);
            CreateGridBaker(monster, environment);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[M1] 2D 씬 구성 완료: {ScenePath}");
            Debug.Log($"[M1] 미션 스테이션: {station.name} / 괴물: {monster.name}");
        }

        /// <summary>
        /// 벽 레이어가 없으면 경고한다. 시야 차단과 그리드 굽기가 이 레이어에 의존한다.
        /// </summary>
        private static void EnsureWallLayerExists()
        {
            if (LayerMask.NameToLayer("Walls") < 0 || LayerMask.NameToLayer("Interactable") < 0)
            {
                Debug.LogError(
                    "[M1] 'Walls' 또는 'Interactable' 레이어가 없다. " +
                    "ProjectSettings/TagManager.asset을 확인하라. 시야 차단과 상호작용 감지가 오작동한다.");
            }
        }

        private static int WallLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer("Walls");
                return layer >= 0 ? layer : 0;
            }
        }

        private static int InteractableLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer("Interactable");
                return layer >= 0 ? layer : 0;
            }
        }

        private static void CreateLighting()
        {
            // 2D는 Global Light 2D를 쓰지만, 그레이박스에서는 Sprite Renderer의
            // 기본(Unlit 유사) 표시로 충분하다. 조명 연출은 M6에서 다룬다.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
        }

        private static GameObject CreateEnvironment()
        {
            var root = new GameObject("Environment");

            foreach (RoomSpec room in Rooms)
            {
                var roomRoot = new GameObject(room.Name);
                roomRoot.transform.SetParent(root.transform);
                roomRoot.transform.position = room.Center;

                CreateFloor(roomRoot.transform, Vector2.zero, room.Size, $"Floor_{room.Name}");
                CreateRoomWalls(roomRoot.transform, room);
            }

            CreateCorridors(root.transform);
            return root;
        }

        private static void CreateFloor(Transform parent, Vector2 localPos, Vector2 size, string name)
        {
            GameObject floor = CreateQuad(name, new Color(0.16f, 0.19f, 0.22f), sortingOrder: -10);
            floor.transform.SetParent(parent);
            floor.transform.localPosition = localPos;
            floor.transform.localScale = size;
        }

        /// <summary>
        /// 방의 네 벽을 만들되, 좌우 벽에는 복도로 통하는 문을 뚫는다.
        /// 문 폭 2m는 그리드 4셀이다 (map-level-design.md §3).
        /// </summary>
        private static void CreateRoomWalls(Transform parent, in RoomSpec room)
        {
            float halfX = room.Size.x * 0.5f;
            float halfZ = room.Size.y * 0.5f;

            // 위아래 벽 (막힘). 코너를 덮도록 두께만큼 늘린다.
            CreateWall(parent, new Vector2(0f, halfZ),
                new Vector2(room.Size.x + WallThickness, WallThickness), "Wall_North");
            CreateWall(parent, new Vector2(0f, -halfZ),
                new Vector2(room.Size.x + WallThickness, WallThickness), "Wall_South");

            // 좌우 벽 (문 있음): 문 위아래로 나눠 만든다.
            float segment = (room.Size.y - DoorWidth) * 0.5f;
            float offset = (DoorWidth + segment) * 0.5f;

            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "West" : "East";

                CreateWall(parent, new Vector2(sign * halfX, offset),
                    new Vector2(WallThickness, segment), $"Wall_{side}_A");
                CreateWall(parent, new Vector2(sign * halfX, -offset),
                    new Vector2(WallThickness, segment), $"Wall_{side}_B");
            }
        }

        private static void CreateCorridors(Transform parent)
        {
            var corridorRoot = new GameObject("Corridors");
            corridorRoot.transform.SetParent(parent);

            for (int i = 0; i < Rooms.Length - 1; i++)
            {
                RoomSpec left = Rooms[i];
                RoomSpec right = Rooms[i + 1];

                float startX = left.Center.x + left.Size.x * 0.5f;
                float endX = right.Center.x - right.Size.x * 0.5f;
                float length = endX - startX;
                float centerX = (startX + endX) * 0.5f;

                var corridor = new GameObject($"Corridor_{i}");
                corridor.transform.SetParent(corridorRoot.transform);
                corridor.transform.position = new Vector2(centerX, 0f);

                CreateFloor(corridor.transform, Vector2.zero,
                    new Vector2(length, CorridorWidth), $"Floor_Corridor_{i}");

                CreateWall(corridor.transform, new Vector2(0f, CorridorWidth * 0.5f),
                    new Vector2(length, WallThickness), "Wall_North");
                CreateWall(corridor.transform, new Vector2(0f, -CorridorWidth * 0.5f),
                    new Vector2(length, WallThickness), "Wall_South");
            }
        }

        private static void CreateWall(Transform parent, Vector2 localPos, Vector2 size, string name)
        {
            GameObject wall = CreateQuad(name, new Color(0.42f, 0.47f, 0.53f), sortingOrder: 5);
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = size;
            wall.layer = WallLayer;

            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }

        /// <summary>
        /// 단색 사각형 스프라이트 오브젝트를 만든다.
        /// 그레이박스 단계라 실제 스프라이트 에셋 대신 1x1 흰 텍스처를 색조로 쓴다
        /// (art-audio-asset-guide.md §16: 단색 사각형으로 그레이박스 검증).
        /// </summary>
        private static GameObject CreateQuad(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();

            renderer.sprite = GetWhiteSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            return go;
        }

        private static Sprite _whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            const string path = "Assets/_Project/Art/Textures/SPR_White.png";
            _whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            // 1x1 흰 텍스처를 만들어 저장한다. pixelsPerUnit=1이라
            // localScale이 곧 월드 크기(m)가 된다.
            System.IO.Directory.CreateDirectory("Assets/_Project/Art/Textures");

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            _whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return _whiteSprite;
        }

        private static NoiseService CreateNoiseService(SO_GameBalance balance)
        {
            var go = new GameObject("NoiseService");
            var service = go.AddComponent<NoiseService>();
            SetPrivateField(service, "_balance", balance);
            return service;
        }

        private static GameObject CreatePlayer(SO_GameBalance balance)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector2(-14f, 0f);

            // 2등신 데포르메 (art-audio-asset-guide.md §1.4).
            // 그레이박스에서는 몸통 + 큰 머리 사각형으로 비율만 확인한다.
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.7f, 1.4f);
            collider.direction = CapsuleDirection2D.Vertical;

            var color = new Color(0.20f, 0.78f, 0.78f);

            GameObject torso = CreateQuad("Body", color, sortingOrder: 10);
            torso.transform.SetParent(player.transform);
            torso.transform.localPosition = new Vector2(0f, -0.35f);
            torso.transform.localScale = new Vector2(0.9f, 0.8f);

            GameObject head = CreateQuad("Head", color, sortingOrder: 11);
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector2(0f, 0.45f);
            head.transform.localScale = new Vector2(1.1f, 0.9f);

            // 바라보는 방향 표시 (그레이박스 전용)
            GameObject visor = CreateQuad("Visor", new Color(0.85f, 0.93f, 0.95f), sortingOrder: 12);
            visor.transform.SetParent(player.transform);
            visor.transform.localPosition = new Vector2(0.22f, 0.45f);
            visor.transform.localScale = new Vector2(0.5f, 0.35f);

            var input = player.AddComponent<PlayerInputReader>();

            var motor = player.AddComponent<PlayerMotor>();
            SetPrivateField(motor, "_balance", balance);
            SetPrivateField(motor, "_input", input);
            SetPrivateField(motor, "_sprite", head.GetComponent<SpriteRenderer>());

            var interactor = player.AddComponent<PlayerInteractor>();
            SetPrivateField(interactor, "_balance", balance);
            SetPrivateField(interactor, "_input", input);
            SetPrivateField(interactor, "_interactableMask", (LayerMask)(1 << InteractableLayer));

            var infection = player.AddComponent<PlayerInfection>();
            SetPrivateField(infection, "_balance", balance);

            return player;
        }

        private static Camera CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(target.position.x, target.position.y, -10f);

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.067f, 0.094f, 0.125f);

            var follow = go.AddComponent<TopDownCamera>();
            follow.Target = target;
            follow.SnapToTarget();

            return camera;
        }

        private static FuseMissionStation CreateMissionStation(NoiseService noiseService)
        {
            GameObject go = CreateQuad(
                "P_MissionStation_Fuse", new Color(0.91f, 0.72f, 0.29f), sortingOrder: 8);
            go.transform.position = new Vector2(-16f, 3f);
            go.transform.localScale = new Vector2(1.2f, 1.2f);
            go.layer = InteractableLayer;

            // 상호작용 감지를 위해 트리거 콜라이더를 추가한다.
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = Vector2.one * 1.4f;

            var station = go.AddComponent<FuseMissionStation>();
            SetPrivateField(station, "_noiseService", noiseService);

            return station;
        }

        private static MonsterBrain CreateMonster(
            SO_GameBalance balance, NoiseService noiseService, Transform player)
        {
            var monster = new GameObject("P_Monster_Monkey");
            monster.transform.position = new Vector2(14f, 0f);

            var body = monster.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = monster.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;

            // 괴물은 데포르메하지 않는다 (art-audio-asset-guide.md §3.1).
            // 직원보다 낮고 옆으로 넓은 실루엣.
            var color = new Color(0.84f, 0.23f, 0.26f);

            GameObject shape = CreateQuad("Body", color, sortingOrder: 10);
            shape.transform.SetParent(monster.transform);
            shape.transform.localPosition = Vector2.zero;
            shape.transform.localScale = new Vector2(1.5f, 1.0f);

            GameObject eyes = CreateQuad("Eyes", new Color(1f, 0.85f, 0.3f), sortingOrder: 11);
            eyes.transform.SetParent(monster.transform);
            eyes.transform.localPosition = new Vector2(0.35f, 0.15f);
            eyes.transform.localScale = new Vector2(0.5f, 0.2f);

            var agent = monster.AddComponent<GridPathAgent>();
            agent.Speed = balance.MonsterPatrolSpeed;

            var senses = monster.AddComponent<MonsterSenses>();
            SetPrivateField(senses, "_balance", balance);
            SetPrivateField(senses, "_obstacleMask", (LayerMask)(1 << WallLayer));

            var brain = monster.AddComponent<MonsterBrain>();
            SetPrivateField(brain, "_balance", balance);
            SetPrivateField(brain, "_senses", senses);
            SetPrivateField(brain, "_noiseService", noiseService);
            SetPrivateField(brain, "_player", player);

            var bite = monster.AddComponent<MonsterBiteController>();
            SetPrivateField(bite, "_balance", balance);
            SetPrivateField(bite, "_senses", senses);
            SetPrivateField(bite, "_player", player);

            // 순찰 지점: 세 방을 순환한다.
            var patrolRoot = new GameObject("PatrolPoints");
            var points = new List<Transform>();

            foreach (RoomSpec room in Rooms)
            {
                var point = new GameObject($"Patrol_{room.Name}");
                point.transform.SetParent(patrolRoot.transform);
                point.transform.position = room.Center;
                points.Add(point.transform);
            }

            SetPrivateField(brain, "_patrolPoints", points.ToArray());
            return brain;
        }

        private static void CreateDebugHud(GameObject player, MonsterBrain monster)
        {
            var go = new GameObject("M1DebugHud");
            var hud = go.AddComponent<M1DebugHud>();

            SetPrivateField(hud, "_interactor", player.GetComponent<PlayerInteractor>());
            SetPrivateField(hud, "_infection", player.GetComponent<PlayerInfection>());
            SetPrivateField(hud, "_monster", monster);
        }

        /// <summary>
        /// 런타임에 길찾기 그리드를 굽는 컴포넌트를 배치한다.
        /// NavMesh처럼 에디터에서 미리 구울 수도 있지만, 그리드는 굽기가 빨라
        /// (수천 셀 × OverlapBox) 씬 시작 시 만드는 편이 단순하다.
        /// </summary>
        private static void CreateGridBaker(MonsterBrain monster, GameObject environment)
        {
            var go = new GameObject("PathGridBaker");
            var baker = go.AddComponent<PathGridBaker>();

            SetPrivateField(baker, "_cellSize", CellSize);
            SetPrivateField(baker, "_worldOrigin", new Vector2(-20f, -8f));
            SetPrivateField(baker, "_width", 80);   // 40m / 0.5m
            SetPrivateField(baker, "_height", 32);  // 16m / 0.5m
            SetPrivateField(baker, "_obstacleMask", (LayerMask)(1 << WallLayer));
            SetPrivateField(baker, "_agents", new[] { monster.GetComponent<GridPathAgent>() });
        }

        /// <summary>
        /// [SerializeField] private 필드를 에디터에서 채운다.
        /// 씬을 코드로 만들 때만 쓰는 방법이며 런타임 코드에서는 사용하지 않는다.
        /// </summary>
        private static void SetPrivateField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[M1] 필드를 찾을 수 없다: {target.GetType().Name}.{fieldName}");
                return;
            }

            switch (value)
            {
                case Object unityObject:
                    prop.objectReferenceValue = unityObject;
                    break;
                case LayerMask mask:
                    prop.intValue = mask.value;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case Vector2 v:
                    prop.vector2Value = v;
                    break;
                case Transform[] transforms:
                    prop.arraySize = transforms.Length;
                    for (int k = 0; k < transforms.Length; k++)
                    {
                        prop.GetArrayElementAtIndex(k).objectReferenceValue = transforms[k];
                    }
                    break;
                case GridPathAgent[] agents:
                    prop.arraySize = agents.Length;
                    for (int k = 0; k < agents.Length; k++)
                    {
                        prop.GetArrayElementAtIndex(k).objectReferenceValue = agents[k];
                    }
                    break;
                default:
                    Debug.LogError($"[M1] 지원하지 않는 필드 타입: {value?.GetType().Name}");
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct RoomSpec
        {
            public string Name { get; }
            public Vector2 Center { get; }
            public Vector2 Size { get; }

            public RoomSpec(string name, Vector2 center, Vector2 size)
            {
                Name = name;
                Center = center;
                Size = size;
            }
        }
    }
}
