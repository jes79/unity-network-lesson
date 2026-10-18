using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [4장] NetworkVariable = "모두가 함께 보는 값". 값이 바뀌면 NGO 가 알아서 모두에게 보내 주고,
    /// 나중에 들어온 사람도 접속하는 순간 "현재 값"을 받습니다. (RPC 와 가장 큰 차이)
    ///
    ///   변수          쓰기 권한   설명
    ///   PlayerName    서버       1장에서 접속할 때 보낸 이름을 서버가 넣어 준다
    ///   Health        서버       총알 판정을 서버가 하므로 체력도 서버만 바꾼다
    ///   Kills         서버       처치 수
    ///   IsDead        서버       죽었는지. 모든 화면에서 숨김/표시를 이 값으로 결정
    ///   ColorIndex    주인(Owner) 색은 플레이어가 고르는 "취향"이라 주인이 직접 바꾼다 (C 키)
    /// </summary>
    public class PlayerStats : NetworkBehaviour
    {
        public const int MaxHealth = 100;
        [SerializeField] float respawnDelay = 3f;

        // 기본 권한: 모두 읽기, 서버만 쓰기
        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new();
        public readonly NetworkVariable<int> Health = new(MaxHealth);
        public readonly NetworkVariable<int> Kills = new();
        public readonly NetworkVariable<bool> IsDead = new();

        // 주인이 쓸 수 있는 변수 (클라이언트 권한 데이터의 예)
        public readonly NetworkVariable<int> ColorIndex = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Color CurrentColor => PlayerMovement.ColorFor(OwnerClientId + (ulong)ColorIndex.Value);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                string name = ConnectionHud.ApprovedNames.TryGetValue(OwnerClientId, out var n) ? n : $"Player{OwnerClientId}";
                var fixedName = new FixedString64Bytes();
                fixedName.CopyFromTruncated(name); // 고정 길이 문자열: 너무 길면 잘라서 넣는다
                PlayerName.Value = fixedName;
            }

            // 값이 "바뀔 때" 불리는 콜백 등록
            Health.OnValueChanged += OnHealthChanged;
            IsDead.OnValueChanged += OnDeadChanged;
            ColorIndex.OnValueChanged += OnColorChanged;

            // ⚠️ 콜백은 "바뀔 때"만 불린다. 접속 시점에 이미 있던 값은 직접 한 번 적용해야 한다. (늦게 들어온 사람!)
            ApplyColor();
            ApplyDeadVisual(IsDead.Value);
            ConnectionHud.Log($"#{OwnerClientId} '{PlayerName.Value}' 받은 현재 값: 체력 {Health.Value}, 처치 {Kills.Value}, 죽음 {IsDead.Value}");
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= OnHealthChanged;
            IsDead.OnValueChanged -= OnDeadChanged;
            ColorIndex.OnValueChanged -= OnColorChanged;
        }

        void Update()
        {
            // 주인만 자기 색을 바꿀 수 있다. 남의 ColorIndex 에 쓰려고 하면 NGO 가 오류를 낸다.
            if (IsSpawned && IsOwner && !LessonInput.BlockKeyboard && Input.GetKeyDown(KeyCode.C))
                ColorIndex.Value = (ColorIndex.Value + 1) % 8;
        }

        // ── 서버 전용 ────────────────────────────────────────────

        /// <summary>서버에서만 호출: 총알이 맞았을 때 (Projectile.OnTriggerEnter)</summary>
        public void TakeDamage(int amount, ulong attackerClientId)
        {
            if (!IsServer || IsDead.Value) return;

            Health.Value = Mathf.Max(0, Health.Value - amount);
            if (Health.Value > 0) return;

            IsDead.Value = true;
            if (NetworkManager.ConnectedClients.TryGetValue(attackerClientId, out var attacker) && attacker.PlayerObject != null)
                attacker.PlayerObject.GetComponent<PlayerStats>().Kills.Value++;
            ConnectionHud.Log($"#{attackerClientId} 가 #{OwnerClientId} 를 처치!");
            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            // 순간이동: 보간하지 말고 바로 옮기라는 표시를 같이 보낸다. 그냥 위치만 바꾸면 맵을 가로질러 미끄러져 보인다.
            // (4장에서는 NetworkTransform.Teleport, 5장부터는 직접 만든 PredictedMovement 의 순간이동을 쓴다)
            GetComponent<PredictedMovement>().ServerTeleport(PlayerMovement.SpawnPoint(OwnerClientId));
            Health.Value = MaxHealth;
            IsDead.Value = false;
        }

        // ── 모든 프로그램: 값 → 화면 ───────────────────────────────

        void OnHealthChanged(int previous, int current) =>
            ConnectionHud.Log($"#{OwnerClientId} 체력 {previous} → {current}");

        void OnDeadChanged(bool previous, bool current) => ApplyDeadVisual(current);

        void OnColorChanged(int previous, int current) => ApplyColor();

        void ApplyColor() => GetComponent<Renderer>().material.color = CurrentColor;

        void ApplyDeadVisual(bool dead)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !dead;
            GetComponent<Collider>().enabled = !dead;
        }
    }
}
