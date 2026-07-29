using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Monkey Lab/Player Movement Config", fileName = "SO_PlayerMovement_Default")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float _moveSpeed = 4.0f;
        [SerializeField, Min(1f)] private float _rotationSpeedDegrees = 720f;
        [SerializeField, Min(0f)] private float _gravity = 25f;

        public float MoveSpeed => _moveSpeed;
        public float RotationSpeedDegrees => _rotationSpeedDegrees;
        public float Gravity => _gravity;
    }
}
