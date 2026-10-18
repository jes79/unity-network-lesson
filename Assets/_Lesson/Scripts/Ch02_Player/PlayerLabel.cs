using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [2장] 캐릭터 머리 위 글자. "누가 주인인가(소유권)"를 눈으로 확인하기 위한 표시입니다.
    /// </summary>
    public class PlayerLabel : NetworkBehaviour
    {
        string Text => $"#{OwnerClientId}{(IsOwner ? " (나)" : "")}";

        void OnGUI()
        {
            if (!IsSpawned || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.4f);
            if (screen.z < 0) return;
            var rect = new Rect(screen.x - 80, Screen.height - screen.y - 20, 160, 40);
            GUI.Label(rect, Text, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
        }
    }
}
