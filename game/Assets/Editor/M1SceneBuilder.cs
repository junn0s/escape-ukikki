using System.Collections.Generic;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Players;
using MonkeyLab.Presentation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// M1 그레이박스 씬을 코드로 구성한다.
    /// 방 3개 + 연결 복도, 플레이어, 괴물 1마리, 퓨즈 미션 스테이션.
    ///
    /// 맵 수치는 docs/map-level-design.md §3 그레이박스 시작값을 따른다.
    /// (복도 폭 3m, 문 폭 1.8m, 천장 3.5m, 중형 방 8×10m)
    /// </summary>
    public static class M1SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/91_GameplaySandbox.unity";
        private const string BalancePath = "Assets/_Project/Data/Balance/SO_GameBalance_Default.asset";

        private const float WallHeight = 3.5f;
        private const float WallThickness = 0.3f;
        private const float FloorY = 0f;

        // 방 3개: 전력 복구실(미션) — 복도 — 실험실 — 복도 — 백신실
        private static readonly RoomSpec[] Rooms =
        {
            new("Room_PowerRestore", new Vector3(-14f, 0f, 0f), new Vector2(8f, 10f)),
            new("Room_LabA",         new Vector3(0f, 0f, 0f),   new Vector2(10f, 12f)),
            new("Room_VaccineA",     new Vector3(14f, 0f, 0f),  new Vector2(8f, 10f))
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

            CreateLighting();
            GameObject environment = CreateEnvironment();
            NoiseService noiseService = CreateNoiseService(balance);

            GameObject player = CreatePlayer(balance);
            Camera camera = CreateCamera(player.transform);
            player.GetComponent<PlayerMotor>();

            FuseMissionStation station = CreateMissionStation(balance, noiseService);
            MonsterBrain monster = CreateMonster(balance, noiseService, player.transform);

            CreateDebugHud(player, monster);
            BakeNavMesh(environment);

            // 카메라 참조를 플레이어 모터에 연결
            var motor = player.GetComponent<PlayerMotor>();
            SetPrivateField(motor, "_viewCamera", camera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[M1] 씬 구성 완료: {ScenePath}");
            Debug.Log($"[M1] 미션 스테이션: {station.name} / 괴물: {monster.name}");
        }

        private static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;
            light.color = new Color(0.78f, 0.85f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 그레이박스 단계에서는 어둡게 하지 않는다. 조명 연출은 M6.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.45f);
        }

        private static GameObject CreateEnvironment()
        {
            var root = new GameObject("Environment");
            root.isStatic = true;

            Material floorMat = CreateMaterial("M_Greybox_Floor", new Color(0.24f, 0.27f, 0.30f));
            Material wallMat = CreateMaterial("M_Greybox_Wall", new Color(0.35f, 0.40f, 0.45f));

            foreach (RoomSpec room in Rooms)
            {
                GameObject roomRoot = new GameObject(room.Name);
                roomRoot.transform.SetParent(root.transform);
                roomRoot.transform.position = room.Center;

                CreateFloor(roomRoot.transform, room, floorMat);
                CreateRoomWalls(roomRoot.transform, room, wallMat);
            }

            CreateCorridors(root.transform, floorMat, wallMat);
            return root;
        }

        private static void CreateFloor(Transform parent, in RoomSpec room, Material mat)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent);
            floor.transform.localPosition = new Vector3(0f, FloorY - 0.1f, 0f);
            floor.transform.localScale = new Vector3(room.Size.x, 0.2f, room.Size.y);
            floor.GetComponent<MeshRenderer>().sharedMaterial = mat;
            floor.isStatic = true;
        }

        /// <summary>
        /// 방의 네 벽을 만들되, 좌우 벽에는 복도로 통하는 문을 뚫는다.
        /// 문 폭은 map-level-design.md §3의 1.8m를 쓴다.
        /// </summary>
        private static void CreateRoomWalls(Transform parent, in RoomSpec room, Material mat)
        {
            const float doorWidth = 1.8f;
            float halfX = room.Size.x * 0.5f;
            float halfZ = room.Size.y * 0.5f;

            // 앞뒤 벽 (막힘)
            CreateWall(parent, new Vector3(0f, WallHeight * 0.5f, halfZ),
                new Vector3(room.Size.x, WallHeight, WallThickness), mat, "Wall_North");
            CreateWall(parent, new Vector3(0f, WallHeight * 0.5f, -halfZ),
                new Vector3(room.Size.x, WallHeight, WallThickness), mat, "Wall_South");

            // 좌우 벽 (문 있음): 문 위아래로 나눠 만든다
            float sideSegment = (room.Size.y - doorWidth) * 0.5f;
            float segmentOffset = (doorWidth + sideSegment) * 0.5f;

            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "West" : "East";

                CreateWall(parent, new Vector3(sign * halfX, WallHeight * 0.5f, segmentOffset),
                    new Vector3(WallThickness, WallHeight, sideSegment), mat, $"Wall_{side}_A");
                CreateWall(parent, new Vector3(sign * halfX, WallHeight * 0.5f, -segmentOffset),
                    new Vector3(WallThickness, WallHeight, sideSegment), mat, $"Wall_{side}_B");
            }
        }

        private static void CreateCorridors(Transform parent, Material floorMat, Material wallMat)
        {
            // 복도 폭 3m (map-level-design.md §3)
            const float corridorWidth = 3f;

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
                corridor.transform.position = new Vector3(centerX, 0f, 0f);

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(corridor.transform);
                floor.transform.localPosition = new Vector3(0f, FloorY - 0.1f, 0f);
                floor.transform.localScale = new Vector3(length, 0.2f, corridorWidth);
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                floor.isStatic = true;

                CreateWall(corridor.transform,
                    new Vector3(0f, WallHeight * 0.5f, corridorWidth * 0.5f),
                    new Vector3(length, WallHeight, WallThickness), wallMat, "Wall_North");
                CreateWall(corridor.transform,
                    new Vector3(0f, WallHeight * 0.5f, -corridorWidth * 0.5f),
                    new Vector3(length, WallHeight, WallThickness), wallMat, "Wall_South");
            }
        }

        private static void CreateWall(
            Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
            wall.layer = LayerMask.NameToLayer("Default");
            wall.isStatic = true;
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
            player.transform.position = new Vector3(-14f, 1f, 0f);

            // 3등신 데포르메 기준 (art-audio-asset-guide.md §1.4): 실제 높이 1.6m.
            // 그레이박스에서는 몸통 캡슐 + 큰 머리 큐브로 비율만 확인한다.
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.8f, 0f);

            Material bodyMat = CreateMaterial("M_Greybox_Player", new Color(0.20f, 0.78f, 0.78f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            head.transform.localScale = Vector3.one * 0.72f;
            head.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // 바라보는 방향 표시용 (그레이박스 전용)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(player.transform);
            nose.transform.localPosition = new Vector3(0f, 1.3f, 0.42f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.25f);
            nose.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial("M_Greybox_Facing", new Color(0.91f, 0.72f, 0.29f));
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            var input = player.AddComponent<PlayerInputReader>();

            var motor = player.AddComponent<PlayerMotor>();
            SetPrivateField(motor, "_balance", balance);
            SetPrivateField(motor, "_input", input);

            var interactor = player.AddComponent<PlayerInteractor>();
            SetPrivateField(interactor, "_balance", balance);
            SetPrivateField(interactor, "_input", input);

            var infection = player.AddComponent<PlayerInfection>();
            SetPrivateField(infection, "_balance", balance);

            return player;
        }

        private static Camera CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.067f, 0.094f, 0.125f);

            var follow = go.AddComponent<QuarterViewCamera>();
            follow.Target = target;
            follow.SnapToTarget();

            return camera;
        }

        private static FuseMissionStation CreateMissionStation(
            SO_GameBalance balance, NoiseService noiseService)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "P_MissionStation_Fuse";
            go.transform.position = new Vector3(-16f, 0.6f, 3f);
            go.transform.localScale = new Vector3(1.2f, 1.2f, 0.6f);
            go.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial("M_Greybox_Station", new Color(0.91f, 0.72f, 0.29f));

            // 상호작용 감지를 위해 트리거 콜라이더를 추가한다.
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one * 1.5f;

            var station = go.AddComponent<FuseMissionStation>();
            SetPrivateField(station, "_noiseService", noiseService);

            return station;
        }

        private static MonsterBrain CreateMonster(
            SO_GameBalance balance, NoiseService noiseService, Transform player)
        {
            var monster = new GameObject("P_Monster_Monkey");
            monster.transform.position = new Vector3(14f, 0f, 0f);

            // 괴물은 데포르메하지 않는다 (art-audio-asset-guide.md §3.1).
            // 직원보다 낮고 옆으로 넓은 실루엣.
            Material mat = CreateMaterial("M_Greybox_Monster", new Color(0.84f, 0.23f, 0.26f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(monster.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(1.0f, 0.5f, 0.8f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "FacingMarker";
            marker.transform.SetParent(monster.transform);
            marker.transform.localPosition = new Vector3(0f, 0.7f, 0.5f);
            marker.transform.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var agent = monster.AddComponent<NavMeshAgent>();
            agent.height = 1.2f;
            agent.radius = 0.45f;
            agent.speed = balance.MonsterPatrolSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.4f;

            var senses = monster.AddComponent<MonsterSenses>();
            SetPrivateField(senses, "_balance", balance);
            SetPrivateField(senses, "_obstacleMask", (LayerMask)1); // Default 레이어

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
                point.transform.position = new Vector3(room.Center.x, 0f, room.Center.z);
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

        private static void BakeNavMesh(GameObject environment)
        {
            var surfaceGo = new GameObject("NavMeshSurface");
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // 괴물 에이전트 크기에 맞춘다 (반지름 0.45, 높이 1.2).
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1666f;

            surface.BuildNavMesh();

            // 베이크 결과를 별도 에셋으로 저장한다.
            // 씬에 그대로 두면 바이너리 데이터가 섞여 씬 전체가 바이너리로 저장되고,
            // 그러면 project-structure.md §10.3의 "씬은 텍스트" 규칙이 깨진다.
            NavMeshData data = surface.navMeshData;
            if (data != null)
            {
                const string dir = "Assets/_Project/Data/Maps";
                System.IO.Directory.CreateDirectory(dir);

                string path = $"{dir}/NavMesh_GameplaySandbox.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();

                surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                Debug.Log($"[M1] NavMesh 에셋 저장: {path}");
            }
            else
            {
                Debug.LogWarning("[M1] NavMesh 베이크 결과가 비어 있다");
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            const string dir = "Assets/_Project/Art/Materials";
            string path = $"{dir}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            material.color = color;

            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
                case Transform[] transforms:
                    prop.arraySize = transforms.Length;
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
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
            public Vector3 Center { get; }
            public Vector2 Size { get; }

            public RoomSpec(string name, Vector3 center, Vector2 size)
            {
                Name = name;
                Center = center;
                Size = size;
            }
        }
    }
}
