using Unity.Netcode;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [2장] 서버 권한(Server Authoritative) 이동.
    ///
    ///   주인(Owner) 클라이언트 : 키 입력만 읽어서 서버에 "이렇게 움직이고 싶어요"라고 보낸다. (직접 움직이지 않는다!)
    ///   서버                  : 받은 입력을 검사하고, 진짜 위치를 계산해 옮긴다.
    ///   NetworkTransform       : 서버가 옮긴 위치를 모든 클라이언트에 퍼뜨린다.
    ///
    /// 클라이언트가 위치를 마음대로 정할 수 없으므로 "순간이동 핵"이 통하지 않습니다.
    /// 대신 내 캐릭터도 서버를 한 번 왕복한 뒤에 움직이므로, 지연이 크면 조작이 굼떠집니다. → 5장에서 해결
    /// </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        public const float ArenaHalfSize = 9f;
        [SerializeField] float speed = 6f;

        Vector2 m_ServerInput;   // 서버에서만 의미 있음: 이 플레이어가 마지막으로 보낸 입력
        Vector2 m_LastSentInput; // 주인에서만 의미 있음: 마지막으로 보낸 입력 (같은 값을 매 프레임 보내지 않기 위해)

        public override void OnNetworkSpawn()
        {
            // 시작 위치는 서버가 정한다. NetworkTransform 이 이 값을 모두에게 전달한다.
            if (IsServer) transform.position = SpawnPoint(OwnerClientId);

            // 색깔은 동기화하지 않는다. 모두가 알고 있는 OwnerClientId 로 "각자 계산"하면 결과가 같기 때문.
            GetComponent<Renderer>().material.color = ColorFor(OwnerClientId);

            ConnectionHud.Log($"플레이어 생성: 주인 #{OwnerClientId}  IsOwner={IsOwner}  IsServer={IsServer}");
        }

        void Update()
        {
            if (!IsSpawned) return;

            // ① 주인: 입력이 "바뀌었을 때만" 서버로 보낸다 → 네트워크 사용량 절약
            if (IsOwner)
            {
                Vector2 input = LessonInput.Move();
                if (input != m_LastSentInput)
                {
                    m_LastSentInput = input;
                    SubmitInputRpc(input);
                }
            }

            // ② 서버: 진짜 위치 계산
            if (IsServer)
            {
                var move = new Vector3(m_ServerInput.x, 0, m_ServerInput.y);
                var pos = transform.position + move * (speed * Time.deltaTime);
                pos.x = Mathf.Clamp(pos.x, -ArenaHalfSize, ArenaHalfSize);
                pos.z = Mathf.Clamp(pos.z, -ArenaHalfSize, ArenaHalfSize);
                transform.position = pos;
                if (move.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(move);
            }
        }

        /// <summary>
        /// 클라이언트 → 서버 RPC. InvokePermission.Owner: 이 캐릭터의 주인만 호출할 수 있다.
        /// 남의 캐릭터를 조종하려는 요청은 서버가 자동으로 거부한다.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitInputRpc(Vector2 input)
        {
            // 서버는 클라이언트를 믿지 않는다: 길이가 1을 넘는 "과속 입력"은 잘라낸다.
            m_ServerInput = Vector2.ClampMagnitude(input, 1f);
        }

        public static Vector3 SpawnPoint(ulong clientId)
        {
            float angle = clientId * 1.9f;
            return new Vector3(Mathf.Cos(angle) * 4f, 1f, Mathf.Sin(angle) * 4f);
        }

        public static Color ColorFor(ulong clientId) => Color.HSVToRGB(clientId * 0.23f % 1f, 0.7f, 0.95f);
    }
}
