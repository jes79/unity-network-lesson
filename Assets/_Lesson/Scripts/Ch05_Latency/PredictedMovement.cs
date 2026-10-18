using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [5장] 클라이언트 예측(Client-side Prediction) + 서버 보정(Server Reconciliation) + 보간(Interpolation).
    /// 2장의 PlayerMovement + NetworkTransform 을 대신합니다. 여전히 "진짜 위치"는 서버가 정합니다.
    ///
    ///   매 틱마다 (30Hz)
    ///     주인   : 입력에 번호(seq)를 붙여 서버로 보내고, 기다리지 않고 내 화면에서 먼저 움직인다 (예측)
    ///     서버   : 받은 입력으로 진짜 위치 계산 → State = (위치, 마지막으로 처리한 입력 번호)
    ///   State 를 받으면
    ///     주인   : 서버 위치에서 시작해 "아직 서버가 처리 안 한 입력들"을 다시 적용 (보정)
    ///     남들   : 받은 위치들을 쌓아 두고 0.1초 과거 시점을 부드럽게 그린다 (보간)
    ///
    /// 핵심: 이동 계산(Simulate)이 클라이언트와 서버에서 "똑같은 함수"여야 예측이 맞는다.
    /// </summary>
    public class PredictedMovement : NetworkBehaviour
    {
        /// <summary>서버가 보내는 상태. 위치 + "몇 번 입력까지 반영했는지"</summary>
        public struct MoveState : INetworkSerializeByMemcpy
        {
            public Vector3 Position;
            public int LastProcessedInput;
            public int TeleportCount; // 바뀌면 보간하지 말고 바로 옮기라는 표시
        }

        struct PendingInput
        {
            public int Seq;
            public Vector2 Move;
        }

        [SerializeField] float speed = 6f;

        readonly NetworkVariable<MoveState> m_State = new(); // 서버만 쓰기
        PlayerStats m_Stats;

        // 주인 전용
        int m_NextSeq;
        Vector3 m_Predicted;
        readonly List<PendingInput> m_Pending = new();
        Transform m_Ghost;

        // 서버·주인: 마지막으로 받아들인 입력 방향 (캐릭터가 바라보는 방향)
        Vector3 m_Facing = Vector3.forward;

        // 서버 전용
        Vector3 m_ServerPos;
        int m_LastProcessed;
        int m_InputBudget; // 틱당 처리할 수 있는 입력 수 제한 (스피드 핵 방지)

        // 관찰자(남의 캐릭터) 전용
        readonly List<(double time, Vector3 pos)> m_Snapshots = new();

        float TickDelta => 1f / NetworkManager.NetworkTickSystem.TickRate;

        void Awake() => m_Stats = GetComponent<PlayerStats>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_ServerPos = PlayerMovement.SpawnPoint(OwnerClientId);
                m_State.Value = new MoveState { Position = m_ServerPos };
            }
            m_Predicted = m_State.Value.Position;
            transform.position = m_Predicted;

            NetworkManager.NetworkTickSystem.Tick += OnTick;
            m_State.OnValueChanged += OnStateChanged;

            if (IsOwner && !IsServer) m_Ghost = CreateGhost();
        }

        public override void OnNetworkDespawn()
        {
            NetworkManager.NetworkTickSystem.Tick -= OnTick;
            m_State.OnValueChanged -= OnStateChanged;
            if (m_Ghost != null) Destroy(m_Ghost.gameObject);
        }

        /// <summary>클라이언트와 서버가 똑같이 쓰는 이동 계산. 결과가 같아야 예측이 맞는다.</summary>
        Vector3 Simulate(Vector3 position, Vector2 input)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            position += new Vector3(input.x, 0, input.y) * (speed * TickDelta);
            position.x = Mathf.Clamp(position.x, -PlayerMovement.ArenaHalfSize, PlayerMovement.ArenaHalfSize);
            position.z = Mathf.Clamp(position.z, -PlayerMovement.ArenaHalfSize, PlayerMovement.ArenaHalfSize);
            return position;
        }

        // ── 틱 ────────────────────────────────────────────────────

        void OnTick()
        {
            // 죽어 있는 동안은 입력을 만들지도, 보내지도 않는다. (서버도 무시하므로 예측·재적용과 규칙이 같아진다)
            if (IsOwner && !m_Stats.IsDead.Value)
            {
                var input = LessonInput.Move();
                int seq = ++m_NextSeq;
                if (!IsServer)
                {
                    m_Pending.Add(new PendingInput { Seq = seq, Move = input });
                    if (m_Pending.Count > 120) m_Pending.RemoveAt(0);
                    if (input.sqrMagnitude > 0.01f) m_Facing = new Vector3(input.x, 0, input.y);
                    if (LatencyLab.Prediction)
                        m_Predicted = Simulate(m_Predicted, input); // ① 예측: 기다리지 않고 먼저 움직인다
                }
                SubmitInputRpc(seq, input);
            }

            if (IsServer)
            {
                m_InputBudget = Mathf.Min(m_InputBudget + 1, 4); // 1틱에 입력 1개 + 지터 대비 여유
                var state = m_State.Value;
                if (state.Position != m_ServerPos || state.LastProcessedInput != m_LastProcessed)
                {
                    state.Position = m_ServerPos;
                    state.LastProcessedInput = m_LastProcessed;
                    m_State.Value = state; // 다음 네트워크 틱에 모두에게 전송
                }
            }
        }

        /// <summary>
        /// 입력은 Unreliable(재전송 없음)로 보낸다: 매 틱 새 입력이 가므로 옛 입력을 다시 보낼 필요가 없다.
        /// 잃어버린 입력 때문에 생긴 차이는 "보정"이 고친다.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        void SubmitInputRpc(int seq, Vector2 input)
        {
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y)) return; // NaN·무한대 입력은 위치를 망가뜨린다
            if (seq <= m_LastProcessed) return; // 늦게 도착한 옛 입력은 버림 (순서 뒤바뀜)
            m_LastProcessed = seq;
            if (m_Stats.IsDead.Value || m_InputBudget <= 0) return; // 죽었거나, 너무 많이 보내면 무시
            m_InputBudget--;
            m_ServerPos = Simulate(m_ServerPos, input);
            if (input.sqrMagnitude > 0.01f) m_Facing = new Vector3(input.x, 0, input.y); // 서버가 쏘는 방향 = 입력 방향
        }

        /// <summary>서버 전용: 부활 등 순간이동</summary>
        public void ServerTeleport(Vector3 position)
        {
            m_ServerPos = position;
            transform.position = position; // 콜라이더가 다시 켜지기 전에 실제 위치도 즉시 옮긴다 (죽은 자리에서 맞지 않게)
            var state = m_State.Value;
            state.Position = position;
            state.TeleportCount++;
            m_State.Value = state;
        }

        // ── 서버 상태 도착 ─────────────────────────────────────────

        void OnStateChanged(MoveState previous, MoveState current)
        {
            if (IsServer) return;

            if (IsOwner)
            {
                m_Pending.RemoveAll(p => p.Seq <= current.LastProcessedInput); // 서버가 처리한 입력은 버린다

                if (current.TeleportCount != previous.TeleportCount)
                {
                    m_Pending.Clear(); // 순간이동 전의 입력은 새 위치에 다시 적용하면 안 된다
                    m_Predicted = current.Position;
                    return;
                }
                if (!LatencyLab.Prediction)
                {
                    m_Predicted = current.Position; // 예측을 안 하면 서버 위치를 그대로 쓴다 (2장과 같은 느낌)
                    return;
                }
                if (!LatencyLab.Reconciliation) return; // 보정 끔: 서버가 뭐라 하든 내 예측만 믿는다 → 어긋남이 쌓인다

                // ② 보정: 서버가 확정한 위치에서 시작해, 아직 처리 안 된 입력을 다시 적용
                var replayed = current.Position;
                foreach (var p in m_Pending) replayed = Simulate(replayed, p.Move);
                if ((replayed - m_Predicted).sqrMagnitude > 0.0001f) LatencyLab.CorrectionCount++;
                m_Predicted = replayed;
            }
            else
            {
                if (current.TeleportCount != previous.TeleportCount) m_Snapshots.Clear();
                m_Snapshots.Add((Time.timeAsDouble, current.Position)); // ③ 보간용으로 쌓아 둔다
                if (m_Snapshots.Count > 30) m_Snapshots.RemoveAt(0);
            }
        }

        // ── 화면에 그리기 ──────────────────────────────────────────

        void Update()
        {
            if (!IsSpawned) return;
            Vector3 before = transform.position;

            if (IsServer) transform.position = m_ServerPos;
            else if (IsOwner) transform.position = m_Predicted;
            else transform.position = LatencyLab.Interpolation ? Interpolated() : m_State.Value.Position;

            // 바라보는 방향: 서버·주인은 "입력 방향"(보정으로 뒤로 튀어도 돌아서지 않게), 남들은 화면상 이동 방향
            Vector3 facing = IsServer || IsOwner ? m_Facing : transform.position - before;
            facing.y = 0;
            if (facing.sqrMagnitude > 0.00001f) transform.rotation = Quaternion.LookRotation(facing);

            if (m_Ghost != null)
            {
                m_Ghost.gameObject.SetActive(LatencyLab.ShowGhost);
                m_Ghost.position = new Vector3(m_State.Value.Position.x, 0.02f, m_State.Value.Position.z);
            }
        }

        /// <summary>InterpolationDelay 만큼 과거 시점의 위치를, 그 앞뒤로 받은 두 위치 사이에서 계산</summary>
        Vector3 Interpolated()
        {
            if (m_Snapshots.Count == 0) return m_State.Value.Position;
            double renderTime = Time.timeAsDouble - LatencyLab.InterpolationDelay;
            for (int i = m_Snapshots.Count - 1; i > 0; i--)
            {
                var (t0, p0) = m_Snapshots[i - 1];
                var (t1, p1) = m_Snapshots[i];
                if (t0 <= renderTime && renderTime <= t1)
                    return Vector3.Lerp(p0, p1, (float)((renderTime - t0) / (t1 - t0)));
            }
            return renderTime < m_Snapshots[0].time ? m_Snapshots[0].pos : m_Snapshots[^1].pos;
        }

        /// <summary>바닥에 깔리는 흰 원판 = "서버가 확정한 내 위치". 예측한 캐릭터와 얼마나 떨어지는지 보인다.</summary>
        Transform CreateGhost()
        {
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ghost.name = "Ghost (서버가 확정한 위치)";
            Destroy(ghost.GetComponent<Collider>());
            ghost.GetComponent<Renderer>().material.color = Color.white;
            ghost.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f);
            return ghost.transform;
        }
    }
}
