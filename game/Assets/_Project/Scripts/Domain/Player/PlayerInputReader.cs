using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MonkeyLab.Gameplay.Players
{
    /// <summary>
    /// 입력만 읽어 값으로 노출한다. 이동·상호작용 판단은 하지 않는다.
    /// docs/technical-design-document.md §9.1 (입력·이동·네트워크·표현 분리)
    ///
    /// 키 재지정을 지원할 수 있도록 행동 단위로 다룬다 (GDD §7.2).
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        /// <summary>WASD. 화면 방향 기준 정규화된 입력.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>마우스 스크린 좌표.</summary>
        public Vector2 PointerScreenPosition { get; private set; }

        /// <summary>이번 프레임에 상호작용(E)이 눌렸는지.</summary>
        public bool InteractPressedThisFrame { get; private set; }

        /// <summary>이번 프레임에 손전등(F)이 눌렸는지.</summary>
        public bool FlashlightPressedThisFrame { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                MoveInput = Vector2.zero;
                InteractPressedThisFrame = false;
                FlashlightPressedThisFrame = false;
                return;
            }

            var raw = new Vector2(
                ReadAxis(keyboard.dKey, keyboard.aKey),
                ReadAxis(keyboard.wKey, keyboard.sKey));

            // 대각선 입력이 빨라지지 않도록 정규화한다.
            MoveInput = raw.sqrMagnitude > 1f ? raw.normalized : raw;

            InteractPressedThisFrame = keyboard.eKey.wasPressedThisFrame;
            FlashlightPressedThisFrame = keyboard.fKey.wasPressedThisFrame;

            if (mouse != null)
            {
                PointerScreenPosition = mouse.position.ReadValue();
            }
        }

        private static float ReadAxis(ButtonControl positive, ButtonControl negative)
        {
            float value = 0f;
            if (positive.isPressed) value += 1f;
            if (negative.isPressed) value -= 1f;
            return value;
        }
    }
}
