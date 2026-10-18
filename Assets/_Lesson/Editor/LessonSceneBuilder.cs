using System.IO;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lesson.EditorTools
{
    /// <summary>
    /// 교안용 씬과 프리팹을 코드로 다시 만듭니다. (메뉴: Lesson ▸ Rebuild Scene)
    /// 씬 파일은 이미 저장소에 들어 있으므로 평소에는 실행할 필요가 없습니다.
    /// "씬에 무엇이 들어 있는지"를 코드로 읽어 보는 용도로도 쓰세요.
    /// </summary>
    public static class LessonSceneBuilder
    {
        const string Root = "Assets/_Lesson";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Lesson/Rebuild Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main.transform;
            cam.position = new Vector3(0, 18, -14);
            cam.rotation = Quaternion.Euler(52, 0, 0);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2, 1, 2);

            // ── NetworkManager: 네트워크의 "본부". 씬에 딱 하나만 둡니다.
            var netGo = new GameObject("NetworkManager");
            var net = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>(); // 실제로 UDP 패킷을 주고받는 부품
            net.NetworkConfig ??= new NetworkConfig();
            net.NetworkConfig.NetworkTransport = transport;
            net.NetworkConfig.ConnectionApproval = true; // 접속 승인 콜백 사용
            net.NetworkConfig.TickRate = 30;             // 1초에 30번 상태를 주고받음

            // ── [5장] 지연 실험실: Network Simulator 가 UnityTransport 에 일부러 지연/손실을 넣는다.
            var simulator = netGo.AddComponent<NetworkSimulator>();
            var lab = netGo.AddComponent<LatencyLab>();
            var labSo = new SerializedObject(lab);
            labSo.FindProperty("simulator").objectReferenceValue = simulator;
            labSo.ApplyModifiedPropertiesWithoutUndo();

            // ── [2장] 플레이어 프리팹: 접속이 승인되면 서버가 클라이언트마다 하나씩 만들어 준다.
            Directory.CreateDirectory(Root + "/Prefabs");
            var projectile = CreateProjectilePrefab(); // [3장]
            net.NetworkConfig.PlayerPrefab = CreatePlayerPrefab(projectile);

            // 네트워크로 만들 수 있는 프리팹 목록. 서버와 클라이언트가 "같은 목록"을 가져야 한다.
            var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (!prefabList.Contains(projectile)) prefabList.Add(new NetworkPrefab { Prefab = projectile });
            EditorUtility.SetDirty(prefabList);
            net.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            net.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            // ── [3장] 채팅: 씬에 미리 놓인 네트워크 오브젝트. 프리팹이 아니라 씬에 들어 있으므로 Spawn 하지 않아도
            //          서버가 시작될 때 자동으로 네트워크에 등록된다. (주인 = 서버)
            var chat = new GameObject("Chat");
            chat.AddComponent<NetworkObject>();
            chat.AddComponent<ChatSystem>();

            // ── 접속 화면
            var hud = new GameObject("ConnectionHud");
            hud.AddComponent<ConnectionHud>();
            hud.AddComponent<CommandLineStarter>();
            hud.AddComponent<SpawnedObjectsReport>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // 창을 여러 개 띄워 테스트하므로, 포커스가 없어도 계속 실행되게 합니다.
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1024;
            PlayerSettings.defaultScreenHeight = 640;
            AssetDatabase.SaveAssets();
            Debug.Log("[Lesson] 씬 생성 완료: " + ScenePath);
        }

        static GameObject CreateProjectilePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<Rigidbody>().isKinematic = true; // 트리거 충돌 이벤트를 받기 위해 필요
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncPositionY = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false; // 공은 회전이 필요 없다
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            go.AddComponent<Projectile>();
            return SavePrefab(go, "Projectile");
        }

        static GameObject CreatePlayerPrefab(GameObject projectile)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";

            // 바라보는 방향 표시용 "코"
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.name = "Nose";
            nose.transform.SetParent(go.transform);
            nose.transform.localPosition = new Vector3(0, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.2f, 0.4f);

            go.AddComponent<NetworkObject>(); // "이 오브젝트는 네트워크에 존재한다"는 신분증 (고유 번호 NetworkObjectId)

            // [2장~4장] 이동 = PlayerMovement(서버 권한) + NetworkTransform(위치 복사)
            //   var nt = go.AddComponent<NetworkTransform>();
            //   nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            //   nt.SyncPositionY = false; nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            //   nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            //   go.AddComponent<PlayerMovement>();
            // [5장~] 이동 = PredictedMovement (예측 + 보정 + 보간을 직접 구현, 위치는 NetworkVariable 로 전송)
            go.AddComponent<PredictedMovement>();

            go.AddComponent<PlayerLabel>();
            go.AddComponent<PlayerStats>(); // [4장]

            var combat = go.AddComponent<PlayerCombat>(); // [3장]
            var so = new SerializedObject(combat);
            so.FindProperty("projectilePrefab").objectReferenceValue = projectile.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "Player");
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{Root}/Prefabs/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>배치 모드용: 씬을 만들고 Windows 실행 파일까지 빌드합니다.</summary>
        public static void BuildWindowsPlayer()
        {
            Build();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Win/UnityNetworkLesson.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("[Lesson] 빌드 결과: " + report.summary.result);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
