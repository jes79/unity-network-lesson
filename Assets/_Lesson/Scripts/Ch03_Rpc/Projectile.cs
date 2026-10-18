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
        const float Radius = 0.175f;

        /// <summary>누가 쐈는지 (서버에서만 사용 — 자기 총알에 맞지 않게)</summary>
        public ulong ShooterClientId;
        float m_Age;

        void Update()
        {
            // 움직임과 판정은 서버만 한다. 클라이언트는 NetworkTransform 이 보내 준 위치를 그리기만 한다.
            if (!IsServer || !IsSpawned) return;

            // 이번 프레임에 "지나갈 길 전체"를 검사한다(sweep). 서버 프레임이 0.1초 멈추면 총알은 1.4m 를 건너뛰므로,
            // 도착 지점에서만 충돌을 보면 캐릭터를 뚫고 지나갈 수 있다.
            float step = speed * Time.deltaTime;
            var hits = Physics.SphereCastAll(transform.position, Radius, transform.forward, step, ~0, QueryTriggerInteraction.Ignore);
            PlayerCombat target = null;
            float nearest = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.distance < nearest && hit.collider.TryGetComponent<PlayerCombat>(out var player) && player.OwnerClientId != ShooterClientId)
                {
                    nearest = hit.distance;
                    target = player;
                }
            }
            if (target != null)
            {
                Hit(target);
                return;
            }

            transform.position += transform.forward * step;
            m_Age += Time.deltaTime;
            if (m_Age > lifeTime) NetworkObject.Despawn(); // 서버가 Despawn → 모든 복사본이 사라진다
        }

        void Hit(PlayerCombat target)
        {
            ConnectionHud.Log($"명중! #{ShooterClientId} → #{target.OwnerClientId}");
            target.PlayHitEffectRpc();                                          // 사건(연출) → RPC
            target.GetComponent<PlayerStats>().TakeDamage(Damage, ShooterClientId); // 상태(체력) → NetworkVariable (4장)
            NetworkObject.Despawn();
        }
    }
}
