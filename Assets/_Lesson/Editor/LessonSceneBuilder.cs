using System.IO;
using Unity.Netcode;
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

            // ── 접속 화면
            var hud = new GameObject("ConnectionHud");
            hud.AddComponent<ConnectionHud>();
            hud.AddComponent<CommandLineStarter>();

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
