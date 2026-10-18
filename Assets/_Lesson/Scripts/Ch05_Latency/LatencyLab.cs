using System;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [5장] 지연 실험실.
    ///   - Network Simulator(Multiplayer Tools 패키지)로 "일부러" 지연·흔들림·손실을 만든다.
    ///   - 지연을 숨기는 세 가지 기술을 켜고 끄며 차이를 체험한다.
    ///       보간(Interpolation)   : 남의 캐릭터를 부드럽게 (조금 과거를 보여 주는 대신)
    ///       예측(Prediction)       : 내 캐릭터는 서버 답을 기다리지 않고 바로 움직임
    ///       보정(Reconciliation)   : 예측이 틀렸으면 서버 기준으로 고치고, 아직 답 못 받은 입력을 다시 적용
    ///
    /// 실행 옵션: -lag 120 -jitter 20 -loss 5 -noPredict -noReconcile -noInterp
    /// ⚠️ 시뮬레이터는 에디터와 Development 빌드에서만 동작합니다.
    /// </summary>
    public class LatencyLab : MonoBehaviour
    {
        public static bool Prediction = true;
        public static bool Reconciliation = true;
        public static bool Interpolation = true;
        public static bool ShowGhost = true;

        /// <summary>남의 캐릭터를 몇 초 과거로 그릴지 (보간 버퍼). 틱 간격(33ms)의 약 3배.</summary>
        public const float InterpolationDelay = 0.1f;

        /// <summary>예측이 틀려서 보정한 횟수 (PredictedMovement 가 올린다)</summary>
        public static int CorrectionCount;

        [SerializeField] NetworkSimulator simulator;

        int m_Delay, m_Jitter, m_Loss;
        float m_NextReport;
        static readonly bool s_Report = Array.IndexOf(Environment.GetCommandLineArgs(), "-report") >= 0;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int.TryParse(GetArg(args, "-lag"), out m_Delay);
            int.TryParse(GetArg(args, "-jitter"), out m_Jitter);
            int.TryParse(GetArg(args, "-loss"), out m_Loss);
            if (Array.IndexOf(args, "-noPredict") >= 0) Prediction = false;
            if (Array.IndexOf(args, "-noReconcile") >= 0) Reconciliation = false;
            if (Array.IndexOf(args, "-noInterp") >= 0) Interpolation = false;
            ApplyCustom();
        }

        void ApplyPreset(NetworkSimulatorPreset preset)
        {
            simulator.ConnectionPreset = preset;
            m_Delay = preset.PacketDelayMs;
            m_Jitter = preset.PacketJitterMs;
            m_Loss = preset.PacketLossPercent;
            ConnectionHud.Log($"네트워크 상태: {preset.Name} (지연 {m_Delay}ms, 흔들림 {m_Jitter}ms, 손실 {m_Loss}%)");
        }

        void ApplyCustom() => ApplyPreset(NetworkSimulatorPreset.Create("직접 설정", "", m_Delay, m_Jitter, 0, m_Loss));

        /// <summary>서버까지 왕복 시간(RTT). 클라이언트에서만 의미 있다.</summary>
        static ulong Rtt()
        {
            var net = NetworkManager.Singleton;
            if (net == null || !net.IsConnectedClient || net.IsServer) return 0;
            return net.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        void Update()
        {
            var net = NetworkManager.Singleton;
            if (!s_Report || Time.time < m_NextReport || net == null || !net.IsConnectedClient || net.IsServer) return;
            m_NextReport = Time.time + 3f;
            ConnectionHud.Log($"[지연] RTT {Rtt()}ms, 예측 {Prediction}, 보정 {Reconciliation}, 보정 횟수 {CorrectionCount}");
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 330), GUI.skin.box);
            GUILayout.Label($"지연 실험실   RTT: {Rtt()} ms");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("없음")) ApplyPreset(NetworkSimulatorPresets.None);
            if (GUILayout.Button("광랜")) ApplyPreset(NetworkSimulatorPresets.HomeBroadband);
            if (GUILayout.Button("4G")) ApplyPreset(NetworkSimulatorPresets.Mobile4G);
            if (GUILayout.Button("3G")) ApplyPreset(NetworkSimulatorPresets.Mobile3G);
            GUILayout.EndHorizontal();

            GUILayout.Label($"지연 {m_Delay} ms");
            int delay = (int)GUILayout.HorizontalSlider(m_Delay, 0, 300);
            GUILayout.Label($"흔들림(jitter) {m_Jitter} ms");
            int jitter = (int)GUILayout.HorizontalSlider(m_Jitter, 0, 100);
            GUILayout.Label($"패킷 손실 {m_Loss} %");
            int loss = (int)GUILayout.HorizontalSlider(m_Loss, 0, 30);
            if (delay != m_Delay || jitter != m_Jitter || loss != m_Loss)
            {
                m_Delay = delay; m_Jitter = jitter; m_Loss = loss;
                ApplyCustom();
            }

            GUILayout.Space(6);
            Prediction = GUILayout.Toggle(Prediction, " 예측 (내 캐릭터 즉시 반응)");
            Reconciliation = GUILayout.Toggle(Reconciliation, " 보정 (서버 답으로 고치기)");
            Interpolation = GUILayout.Toggle(Interpolation, " 보간 (남의 캐릭터 부드럽게)");
            ShowGhost = GUILayout.Toggle(ShowGhost, " 유령 표시 (서버가 확정한 내 위치)");
            GUILayout.Label($"보정 횟수: {CorrectionCount}");
            GUILayout.EndArea();
        }

        static string GetArg(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
