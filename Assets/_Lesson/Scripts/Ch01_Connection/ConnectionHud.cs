using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [1장] 접속 화면.
    /// Host / Server / Client 로 시작하고, 접속 과정에서 일어나는 이벤트를 화면 로그로 보여 줍니다.
    ///
    /// 핵심 흐름
    ///   1) 클라이언트가 서버 주소(IP:포트)로 "접속 요청"을 보낸다. 이때 이름(ConnectionData)을 같이 보낸다.
    ///   2) 서버는 ConnectionApprovalCallback 에서 요청을 보고 승인/거절한다. (인원 제한)
    ///   3) 승인되면 양쪽에서 OnClientConnectedCallback 이 불린다.
    ///   4) 끊기면 OnClientDisconnectCallback 이 불리고, 클라이언트는 DisconnectReason 으로 이유를 볼 수 있다.
    /// </summary>
    public class ConnectionHud : MonoBehaviour
    {
        [Header("접속 정보")]
        [SerializeField] string address = "127.0.0.1"; // 내 컴퓨터 자신을 뜻하는 주소(localhost)
        [SerializeField] string port = "7777";         // NGO 기본 포트
        [SerializeField] string playerName = "Player";
        [SerializeField] int maxPlayers = 4;

        /// <summary>화면에 입력한 내 이름 (6장: 세션 접속 때도 사용)</summary>
        public string PlayerName => playerName;

        /// <summary>서버만 채우는 표: 클라이언트 번호 → 접속할 때 보낸 이름. (4장에서 이름 표시에 사용)</summary>
        public static readonly Dictionary<ulong, string> ApprovedNames = new();

        /// <summary>다른 장의 스크립트도 화면 로그에 글을 남길 수 있게 열어 둔 함수.</summary>
        public static void Log(string message)
        {
            Debug.Log($"[Lesson] {message}");
            s_Log.Add($"{System.DateTime.Now:HH:mm:ss} {message}");
            if (s_Log.Count > 12) s_Log.RemoveAt(0);
        }

        static readonly List<string> s_Log = new();
        NetworkManager m_Net;

        void Start()
        {
            m_Net = NetworkManager.Singleton;
            m_Net.ConnectionApprovalCallback = ApproveConnection;
            m_Net.OnServerStarted += () => Log("서버 시작: 접속을 기다리는 중");
            m_Net.OnClientConnectedCallback += OnClientConnected;
            m_Net.OnClientDisconnectCallback += OnClientDisconnected;
            m_Net.OnTransportFailure += () => Log("전송 계층(Transport) 오류 — 주소/포트/방화벽을 확인하세요");
        }

        // ── 시작 버튼들 ─────────────────────────────────────────────

        /// <summary>mode: "host" | "server" | "client". 명령줄 실행(CommandLineStarter)도 이 함수를 씁니다.</summary>
        public void StartAs(string mode, string ip = null, string portText = null, string name = null)
        {
            if (ip != null) address = ip;
            if (portText != null) port = portText;
            if (name != null) playerName = name;

            if (!ushort.TryParse(port, out var portNumber))
            {
                Log($"포트 번호가 잘못됐습니다: {port}");
                return;
            }

            // 서버는 0.0.0.0(모든 네트워크 카드)에서 기다려야 같은 공유기의 다른 PC도 들어올 수 있습니다.
            var transport = (UnityTransport)m_Net.NetworkConfig.NetworkTransport;
            transport.SetConnectionData(address, portNumber, "0.0.0.0");

            // 접속 요청에 실어 보낼 데이터. 서버의 ApproveConnection 에서 request.Payload 로 받습니다.
            m_Net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName);

            bool ok = mode switch
            {
                "host" => m_Net.StartHost(),     // 서버 + 클라이언트를 한 프로그램에서 (Listen Server)
                "server" => m_Net.StartServer(), // 서버만 (Dedicated Server)
                _ => m_Net.StartClient(),        // 서버에 접속하는 쪽
            };
            Log(ok ? $"{mode} 시작 → {address}:{port}" : $"{mode} 시작 실패 (포트가 이미 사용 중일 수 있음)");
        }

        // ── 서버 쪽: 접속 승인 ───────────────────────────────────────

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
        {
            string requestedName = Encoding.UTF8.GetString(request.Payload);
            if (string.IsNullOrWhiteSpace(requestedName)) requestedName = $"Player{request.ClientNetworkId}";

            bool full = m_Net.ConnectedClientsIds.Count >= maxPlayers;
            response.Approved = !full;
            response.Reason = full ? $"방이 가득 찼습니다 (최대 {maxPlayers}명)" : null;
            // 플레이어 프리팹이 등록돼 있을 때만(2장부터) 플레이어 오브젝트를 만들어 줍니다.
            response.CreatePlayerObject = m_Net.NetworkConfig.PlayerPrefab != null;

            if (response.Approved) ApprovedNames[request.ClientNetworkId] = requestedName;
            Log($"접속 요청: {requestedName} (#{request.ClientNetworkId}) → {(response.Approved ? "승인" : "거절")}");
        }

        // ── 양쪽: 연결/해제 이벤트 ───────────────────────────────────

        void OnClientConnected(ulong clientId)
        {
            if (m_Net.IsServer) Log($"클라이언트 #{clientId} 접속 완료 (현재 {m_Net.ConnectedClientsIds.Count}명)");
            else Log($"서버에 접속했습니다. 내 번호는 #{m_Net.LocalClientId}");
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (m_Net.IsServer)
            {
                ApprovedNames.Remove(clientId);
                Log($"클라이언트 #{clientId} 나감");
            }
            else
            {
                string reason = string.IsNullOrEmpty(m_Net.DisconnectReason) ? "서버 종료 또는 연결 시간 초과" : m_Net.DisconnectReason;
                Log($"서버와 연결이 끊겼습니다: {reason}");
            }
        }

        // ── 화면 ──────────────────────────────────────────────────

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 330, 255), GUI.skin.box);
            if (!m_Net.IsClient && !m_Net.IsServer)
            {
                GUILayout.Label("IP 주소"); address = GUILayout.TextField(address);
                GUILayout.Label("포트"); port = GUILayout.TextField(port);
                GUILayout.Label("이름"); playerName = GUILayout.TextField(playerName, 16);
                GUILayout.Space(6);
                if (GUILayout.Button("Host (서버+플레이어)")) StartAs("host");
                if (GUILayout.Button("Server (서버만)")) StartAs("server");
                if (GUILayout.Button("Client (접속)")) StartAs("client");
            }
            else
            {
                string role = m_Net.IsHost ? "Host" : m_Net.IsServer ? "Server" : "Client";
                GUILayout.Label($"역할: {role}   내 번호: #{m_Net.LocalClientId}");
                GUILayout.Label($"IsServer={m_Net.IsServer}  IsClient={m_Net.IsClient}  IsHost={m_Net.IsHost}");
                if (m_Net.IsServer) GUILayout.Label($"접속자: {string.Join(", ", m_Net.ConnectedClientsIds)}");
                if (GUILayout.Button("연결 종료 (Shutdown)"))
                {
                    m_Net.Shutdown();
                    ApprovedNames.Clear();
                    Log("연결을 종료했습니다");
                }
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(10, Screen.height - 230, 480, 220), GUI.skin.box);
            GUILayout.Label("이벤트 로그");
            foreach (var line in s_Log) GUILayout.Label(line);
            GUILayout.EndArea();
        }
    }
}
