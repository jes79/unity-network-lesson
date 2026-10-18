using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// "지금 내 프로그램에 보이는 네트워크 오브젝트"를 주기적으로 로그에 적습니다.
    /// 실행 옵션 -report 를 주거나 F1 키를 누르면 출력합니다.
    /// 서버 로그와 클라이언트 로그를 나란히 놓고 보면 "같은 오브젝트가 같은 위치에 있는지"를 비교할 수 있습니다.
    /// </summary>
    public class SpawnedObjectsReport : MonoBehaviour
    {
        static readonly bool s_AutoReport = Array.IndexOf(Environment.GetCommandLineArgs(), "-report") >= 0;
        float m_Next;

        void Update()
        {
            var net = NetworkManager.Singleton;
            if (net == null || !net.IsListening) return;

            bool due = s_AutoReport && Time.time >= m_Next;
            if (!due && !Input.GetKeyDown(KeyCode.F1)) return;
            m_Next = Time.time + 2f;

            var lines = net.SpawnManager.SpawnedObjectsList
                .OrderBy(o => o.NetworkObjectId)
                .Select(o => $"{o.name}(id={o.NetworkObjectId}, owner=#{o.OwnerClientId}) @ {o.transform.position:F1}");
            ConnectionHud.Log($"[보이는 오브젝트 {net.SpawnManager.SpawnedObjectsList.Count}개] " + string.Join(" | ", lines));
        }
    }
}
