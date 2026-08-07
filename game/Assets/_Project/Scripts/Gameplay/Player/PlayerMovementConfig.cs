using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Monkey Lab/Player Movement Config", fileName = "SO_PlayerMovement_Default")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float _moveSpeed = 4.0f;
        [SerializeField, Min(0.1f)]
        private float _batteryCarryMoveSpeed = 3.0f;
        [SerializeField, Min(0.1f)] private float _ghostMoveSpeed = 4.8f;
        [SerializeField, Range(0.1f, 1f)]
        private float _infectedMoveSpeedMultiplier = 0.8f;
        [SerializeField, Min(1f)] private float _rotationSpeedDegrees = 720f;
        [SerializeField, Min(0f)] private float _gravity = 25f;

        public float MoveSpeed => _moveSpeed;
        public float BatteryCarryMoveSpeed => _batteryCarryMoveSpeed;

        /// <summary>유령 이동 속도다(balance-and-telemetry.md §3).</summary>
        public float GhostMoveSpeed => _ghostMoveSpeed;

        /// <summary>
        /// 감염 중 기본 이동 속도에 곱하는 배율이다(GDD §14.1, balance-and-telemetry.md §8).
        /// </summary>
        public float InfectedMoveSpeedMultiplier => _infectedMoveSpeedMultiplier;
        public float RotationSpeedDegrees => _rotationSpeedDegrees;
        public float Gravity => _gravity;
    }
}
