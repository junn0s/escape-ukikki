using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Players
{
    /// <summary>
    /// 2D 이동과 바라보는 방향만 담당한다. 입력 읽기와 네트워크 전송은 하지 않는다.
    /// 속도는 SO_GameBalance에서 읽는다 (매직 넘버 금지, project-structure.md §7).
    ///
    /// 탑다운이므로 중력이 없다. Rigidbody2D의 gravityScale을 0으로 두고
    /// MovePosition으로 이동한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _viewCamera;

        [Tooltip("캐릭터 스프라이트. 마우스 방향에 따라 좌우 반전된다")]
        [SerializeField] private SpriteRenderer _sprite;

        private Rigidbody2D _body;
        private Vector2 _facing = Vector2.down;

        /// <summary>미션 수행·사망 등으로 이동이 잠긴 상태.</summary>
        public bool IsMovementLocked { get; set; }

        /// <summary>바라보는 방향. 손전등과 감지 판정이 참조한다.</summary>
        public Vector2 Facing => _facing;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();

            // 탑다운 2D: 중력 없음, 회전 없음
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            // 필수 참조는 Awake에서 확인한다 (project-structure.md §7).
            if (_balance == null)
            {
                Debug.LogError($"[{nameof(PlayerMotor)}] {nameof(_balance)} 미할당", this);
                enabled = false;
                return;
            }

            if (_input == null)
            {
                Debug.LogError($"[{nameof(PlayerMotor)}] {nameof(_input)} 미할당", this);
                enabled = false;
                return;
            }

            if (_viewCamera == null)
            {
                _viewCamera = Camera.main;
            }
        }

        private void Update()
        {
            // 바라보는 방향은 렌더 프레임마다 갱신해 즉각적으로 보이게 한다.
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            if (IsMovementLocked)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            // 화면 방향 기준 이동. 2D 탑다운에서 화면 축과 월드 축이 일치하므로
            // 카메라 기준 변환이 필요하지 않다 (GDD §7.2).
            Vector2 move = _input.MoveInput * _balance.PlayerMoveSpeed;
            _body.linearVelocity = move;
        }

        private void UpdateFacing()
        {
            if (_viewCamera == null)
            {
                return;
            }

            Vector3 pointerWorld = _viewCamera.ScreenToWorldPoint(_input.PointerScreenPosition);
            Vector2 toPointer = (Vector2)pointerWorld - _body.position;

            if (toPointer.sqrMagnitude < 0.01f)
            {
                return;
            }

            _facing = toPointer.normalized;

            // 스프라이트는 회전시키지 않고 좌우만 반전한다.
            // 정면 스프라이트를 쓰므로 회전하면 캐릭터가 누워버린다
            // (art-audio-asset-guide.md §1.4).
            if (_sprite != null && Mathf.Abs(_facing.x) > 0.1f)
            {
                _sprite.flipX = _facing.x < 0f;
            }
        }
    }
}
