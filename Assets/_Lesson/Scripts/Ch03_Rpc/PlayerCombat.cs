using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [3장] RPC 로 "사건(event)"을 주고받기.
    ///
    ///   FireRpc            클라이언트(주인) → 서버   "쏘고 싶어요"         서버가 쿨타임 검사 후 총알을 Spawn
    ///   PlayHitEffectRpc   서버 → 모든 클라이언트     "얘가 맞았어요"       각자 화면에서 깜빡임 연출
    ///
    /// RPC 는 "한 번 일어나고 끝나는 일"에 씁니다. 나중에 들어온 사람은 지난 RPC 를 받지 못합니다.
    /// (계속 유지돼야 하는 값 — 체력 등 — 은 4장의 NetworkVariable 로)
    /// </summary>
    public class PlayerCombat : NetworkBehaviour
    {
        [SerializeField] NetworkObject projectilePrefab;
        [SerializeField] float fireCooldown = 0.4f;

        float m_NextFireTime; // 서버에서만 사용
        Renderer m_Renderer;
        PlayerStats m_Stats;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Stats = GetComponent<PlayerStats>();
        }

        void Update()
        {
            if (IsSpawned && IsOwner && LessonInput.Fire()) FireRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void FireRpc()
        {
            // 쿨타임은 서버가 검사한다. 클라이언트가 연사 핵을 써도 서버가 무시한다.
            if (Time.time < m_NextFireTime || m_Stats.IsDead.Value) return;
            m_NextFireTime = Time.time + fireCooldown;

            var spawnPos = transform.position + transform.forward * 0.9f;
            var projectile = Instantiate(projectilePrefab, spawnPos, transform.rotation);
            projectile.GetComponent<Projectile>().ShooterClientId = OwnerClientId;
            projectile.Spawn(true); // 서버가 Spawn 해야 모든 클라이언트에 복사본이 생긴다
        }

        /// <summary>서버가 "맞았다"는 사건을 모두에게 알린다. 실제 판정은 이미 서버에서 끝났다.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        public void PlayHitEffectRpc()
        {
            if (gameObject.activeInHierarchy) StartCoroutine(Flash());
        }

        IEnumerator Flash()
        {
            m_Renderer.material.color = Color.white;
            yield return new WaitForSeconds(0.12f);
            m_Renderer.material.color = m_Stats.CurrentColor;
        }
    }
}
