using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Players
{
    /// <summary>
    /// 이동과 회전만 담당한다. 입력 읽기와 네트워크 전송은 하지 않는다.
    /// 속도는 SO_GameBalance에서 읽는다 (매직 넘버 금지, project-structure.md §7).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _viewCamera;

        [Tooltip("마우스 조준 평면의 높이. 캐릭터 발밑 기준 오프셋")]
        [SerializeField] private float _aimPlaneHeight = 0f;

        private CharacterController _controller;
        private float _verticalVelocity;

        /// <summary>미션 수행·사망 등으로 이동이 잠긴 상태.</summary>
        public bool IsMovementLocked { get; set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

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
            if (IsMovementLocked)
            {
                ApplyGravityOnly();
                return;
            }

            MoveByInput();
            FacePointer();
        }

        private void MoveByInput()
        {
            Vector2 input = _input.MoveInput;

            // 화면 방향 기준 이동 (GDD §7.2). 카메라가 회전하지 않으므로
            // 카메라의 forward를 수평면에 투영해 기준 축으로 쓴다.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_viewCamera != null)
            {
                Transform cam = _viewCamera.transform;
                forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            }

            Vector3 move = (forward * input.y + right * input.x) * _balance.PlayerMoveSpeed;

            ApplyGravity();
            move.y = _verticalVelocity;

            _controller.Move(move * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            ApplyGravity();
            _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                // 접지 판정을 유지할 만큼의 작은 음수값
                _verticalVelocity = -2f;
                return;
            }

            _verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        private void FacePointer()
        {
            if (_viewCamera == null)
            {
                return;
            }

            // 캐릭터 높이의 수평 평면과 마우스 광선의 교점을 바라본다.
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + _aimPlaneHeight, 0f));
            Ray ray = _viewCamera.ScreenPointToRay(_input.PointerScreenPosition);

            if (!plane.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 target = ray.GetPoint(distance);
            Vector3 direction = target - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desired,
                _balance.PlayerTurnSpeedDegrees * Time.deltaTime);
        }
    }
}
