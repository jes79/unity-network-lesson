using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [3장] 채팅. 씬에 미리 놓인 네트워크 오브젝트(In-Scene Placed NetworkObject) 하나가 담당합니다.
    ///
    ///   클라이언트 ──SendChatRpc("안녕")──▶ 서버 ──ReceiveChatRpc("철수: 안녕")──▶ 모든 클라이언트
    ///
    /// 왜 서버를 거칠까?
    ///   - 서버가 "보낸 사람 이름"을 붙인다. 클라이언트가 남의 이름을 사칭할 수 없다.
    ///   - 서버가 길이 제한·욕설 필터 같은 검사를 할 수 있다.
    /// </summary>
    public class ChatSystem : NetworkBehaviour
    {
        const int MaxLength = 80;
        readonly List<string> m_Lines = new();
        string m_Draft = "";
        bool m_BotGreeted;

        void Update()
        {
            // 자동 입력(-bot) 모드에서는 접속 3초 뒤 인사를 한 번 보낸다.
            if (LessonInput.IsBot && IsSpawned && IsClient && !m_BotGreeted && Time.timeSinceLevelLoad > 3f)
            {
                m_BotGreeted = true;
                SendChatRpc("안녕하세요!");
            }
        }

        // 기본 InvokePermission 은 Everyone: 누구나 호출할 수 있다. (이 오브젝트의 주인은 서버)
        [Rpc(SendTo.Server)]
        void SendChatRpc(string text, RpcParams rpcParams = default)
        {
            text = text.Trim();
            if (text.Length == 0) return;
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);

            // 보낸 사람은 NGO 가 알려 준다 → 이름 사칭 불가
            ulong sender = rpcParams.Receive.SenderClientId;
            string name = ConnectionHud.ApprovedNames.TryGetValue(sender, out var n) ? n : $"#{sender}";
            string line = $"{name}: {text}";

            if (!IsClient) ConnectionHud.Log("채팅 " + line); // 전용 서버는 아래 RPC 를 받지 않으므로 여기서 기록
            ReceiveChatRpc(line);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ReceiveChatRpc(string line)
        {
            m_Lines.Add(line);
            if (m_Lines.Count > 8) m_Lines.RemoveAt(0);
            ConnectionHud.Log("채팅 " + line);
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsClient) return;

            GUILayout.BeginArea(new Rect(Screen.width - 330, Screen.height - 230, 320, 220), GUI.skin.box);
            GUILayout.Label("채팅  (나중에 들어온 사람은 이전 대화를 못 봅니다 — RPC 이므로)");
            foreach (var line in m_Lines) GUILayout.Label(line);
            GUILayout.FlexibleSpace();

            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("chat");
            m_Draft = GUILayout.TextField(m_Draft, MaxLength);
            bool send = GUILayout.Button("보내기", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 채팅 입력 중에는 WASD/스페이스가 캐릭터를 움직이지 않게
            LessonInput.BlockKeyboard = GUI.GetNameOfFocusedControl() == "chat";

            if ((send || (enter && LessonInput.BlockKeyboard)) && m_Draft.Trim().Length > 0)
            {
                SendChatRpc(m_Draft);
                m_Draft = "";
            }
        }
    }
}
