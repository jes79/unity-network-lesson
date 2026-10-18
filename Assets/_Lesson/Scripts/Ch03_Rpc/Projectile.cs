using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [3장] 서버가 만들고, 서버가 움직이고, 서버가 판정하는 총알.
    /// 클라이언트는 NetworkTransform 이 보내 주는 위치를 그리기만 한다.
    /// </summary>
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] float speed = 14f;
        [SerializeField] float lifeTime = 1.5f;
        const int Damage = 25;

        /// <summary>누가 쐈는지 (서버에서만 사용 — 자기 총알에 맞지 않게)</summary>
        public ulong ShooterClientId;
        float m_Age;

        void Update()
        {
            if (!IsServer || !IsSpawned) return;

            transform.position += transform.forward * (speed * Time.deltaTime);
            m_Age += Time.deltaTime;
            if (m_Age > lifeTime) NetworkObject.Despawn(); // 서버가 Despawn → 모든 복사본이 사라진다
        }

        void OnTriggerEnter(Collider other)
        {
            // 충돌은 모든 프로그램에서 일어나지만, "판정"은 서버만 한다.
            if (!IsServer || !IsSpawned) return;
            if (!other.TryGetComponent<PlayerCombat>(out var target)) return;
            if (target.OwnerClientId == ShooterClientId) return;

            ConnectionHud.Log($"명중! #{ShooterClientId} → #{target.OwnerClientId}");
            target.PlayHitEffectRpc();                                          // 사건(연출) → RPC
            target.GetComponent<PlayerStats>().TakeDamage(Damage, ShooterClientId); // 상태(체력) → NetworkVariable (4장)
            NetworkObject.Despawn();
        }
    }
}
