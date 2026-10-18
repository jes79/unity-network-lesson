using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [6장] 인터넷 너머 친구와 하기: Unity Multiplayer Services 의 "세션 + Relay".
    ///
    ///   방 만들기 : 로그인(익명) → Relay 서버에 자리 예약 → 세션 생성 → 6자리 "참가 코드" 발급 → NGO Host 자동 시작
    ///   코드로 참가: 로그인(익명) → 코드로 세션 찾기 → Relay 를 통해 Host 에 연결 → NGO Client 자동 시작
    ///
    /// 공유기(NAT) 뒤에 있는 PC 는 밖에서 직접 접속할 수 없지만, 둘 다 Relay 서버로 "나가는" 연결은 할 수 있다.
    /// Relay 가 중간에서 패킷을 전달해 주므로 IP 주소·포트포워딩이 필요 없다.
    ///
    /// ⚠️ 사전 준비: Unity Cloud 프로젝트 연결 (Edit ▸ Project Settings ▸ Services) — docs/06-relay.md 참고
    /// </summary>
    public class SessionHud : MonoBehaviour
    {
        [SerializeField] int maxPlayers = 4;

        ISession m_Session;
        string m_JoinCode = "";
        string m_Status = "인터넷 방: 아직 연결 안 됨";
        bool m_Busy;

        async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // 같은 PC 에서 창 여러 개(Multiplayer Play Mode)로 테스트하면 로그인 계정이 겹친다.
                // 프로필 이름을 창마다 다르게 주면 서로 다른 익명 플레이어로 로그인된다.
                var options = new InitializationOptions().SetProfile($"p{UnityEngine.Random.Range(0, 1_000_000)}");
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            ConnectionHud.Log($"Unity 서비스 로그인: {AuthenticationService.Instance.PlayerId}");
        }

        /// <summary>1장과 같은 이름을 접속 데이터로 보낸다. (세션이 NGO 를 대신 시작해 주므로 미리 넣어 둔다)</summary>
        void PrepareConnectionData() =>
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(FindAnyObjectByType<ConnectionHud>().PlayerName);

        async void CreateRoom() => await Run("방 만드는 중…", async () =>
        {
            if (NetworkManager.Singleton.IsListening) throw new InvalidOperationException("이미 직접 접속(1장 방식) 중입니다");
            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork(); // Relay 사용
            m_Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            m_Status = $"방 코드: {m_Session.Code}   (친구에게 알려 주세요)";
        });

        async void JoinRoom() => await Run("참가하는 중…", async () =>
        {
            if (NetworkManager.Singleton.IsListening) throw new InvalidOperationException("이미 직접 접속(1장 방식) 중입니다");
            m_Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(m_JoinCode.Trim().ToUpperInvariant());
            m_Status = $"참가 완료: {m_Session.Code}";
        });

        async void LeaveRoom() => await Run("나가는 중…", async () =>
        {
            await m_Session.LeaveAsync();
            m_Session = null;
            m_Status = "방에서 나왔습니다";
        });

        async Task Run(string busyText, Func<Task> action)
        {
            if (m_Busy) return;
            m_Busy = true;
            ConnectionHud.SessionBusy = true; // 진행 중에는 1장의 직접 접속 버튼을 막는다 (두 방식이 섞이지 않게)
            m_Status = busyText;
            try
            {
                await SignInAsync();
                PrepareConnectionData();
                await action();
            }
            catch (Exception e)
            {
                m_Status = "실패: " + e.Message;
            }
            finally
            {
                m_Busy = false;
                ConnectionHud.SessionBusy = m_Session != null; // 세션 안에 있는 동안에도 계속 막는다
                ConnectionHud.Log(m_Status);
            }
        }

        void OnGUI()
        {
            var net = NetworkManager.Singleton;
            GUILayout.BeginArea(new Rect(10, 270, 330, 130), GUI.skin.box);
            GUILayout.Label(m_Status);
            GUI.enabled = !m_Busy;
            if (m_Session == null && !net.IsListening)
            {
                if (GUILayout.Button("인터넷 방 만들기 (Relay)")) CreateRoom();
                GUILayout.BeginHorizontal();
                m_JoinCode = GUILayout.TextField(m_JoinCode, 8, GUILayout.Width(120));
                if (GUILayout.Button("코드로 참가")) JoinRoom();
                GUILayout.EndHorizontal();
            }
            else if (m_Session != null)
            {
                GUILayout.Label($"인원: {m_Session.PlayerCount} / {m_Session.MaxPlayers}");
                if (GUILayout.Button("방 나가기")) LeaveRoom();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
