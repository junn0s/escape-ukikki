using System.Collections.Generic;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 서버가 문 센서 안의 플레이어와 괴물을 세고 열림 상태를 전원에게 복제한다.
    /// 네트워크가 시작되지 않은 로컬 회색상자에서는 같은 규칙을 로컬로 수행한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AutomaticDoorMotor))]
    public sealed class NetworkAutomaticDoorAuthority : NetworkBehaviour
    {
        [SerializeField] private AutomaticDoorMotor _motor;
        [SerializeField] private Collider2D _sensor;
        [SerializeField] private DoorBalanceConfig _config;

        private readonly NetworkVariable<bool> _isOpen = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<int, Collider2D> _occupantColliders =
            new();
        private readonly List<int> _staleColliderIds = new();

        private double _emptySince;
        private bool _hasEmptyTimestamp;

        public bool IsOpen => IsSpawned ? _isOpen.Value :
            _motor != null && _motor.IsOpen;

        public void Configure(
            AutomaticDoorMotor motor,
            Collider2D sensor,
            DoorBalanceConfig config)
        {
            _motor = motor;
            _sensor = sensor;
            _config = config;
        }

        public override void OnNetworkSpawn()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            _isOpen.OnValueChanged += HandleOpenStateChanged;
            _motor.SetOpen(_isOpen.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isOpen.OnValueChanged -= HandleOpenStateChanged;
            _occupantColliders.Clear();
            _staleColliderIds.Clear();
            _hasEmptyTimestamp = false;
            _motor?.SetOpen(false);
        }

        private void Awake()
        {
            ValidateReferences();
        }

        private void Update()
        {
            if (!CanControlDoor() || _config == null)
            {
                return;
            }

            PruneMissingOccupants();
            if (_occupantColliders.Count > 0 || !_hasEmptyTimestamp)
            {
                return;
            }

            if (Time.unscaledTimeAsDouble - _emptySince >=
                _config.CloseDelaySeconds)
            {
                SetAuthoritativeOpen(false);
                _hasEmptyTimestamp = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RegisterOccupant(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            RegisterOccupant(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!CanControlDoor() || other == null ||
                !_occupantColliders.Remove(other.GetInstanceID()) ||
                _occupantColliders.Count > 0)
            {
                return;
            }

            _emptySince = Time.unscaledTimeAsDouble;
            _hasEmptyTimestamp = true;
        }

        private void RegisterOccupant(Collider2D other)
        {
            if (!CanControlDoor() || !IsDoorActor(other) ||
                _occupantColliders.ContainsKey(other.GetInstanceID()))
            {
                return;
            }

            _occupantColliders.Add(other.GetInstanceID(), other);
            _hasEmptyTimestamp = false;
            SetAuthoritativeOpen(true);
        }

        private void PruneMissingOccupants()
        {
            _staleColliderIds.Clear();
            foreach (var pair in _occupantColliders)
            {
                var collider = pair.Value;
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    _staleColliderIds.Add(pair.Key);
                }
            }

            for (var index = 0;
                 index < _staleColliderIds.Count;
                 index++)
            {
                _occupantColliders.Remove(_staleColliderIds[index]);
            }

            if (_staleColliderIds.Count > 0 &&
                _occupantColliders.Count == 0 && !_hasEmptyTimestamp)
            {
                _emptySince = Time.unscaledTimeAsDouble;
                _hasEmptyTimestamp = true;
            }
        }

        private void SetAuthoritativeOpen(bool isOpen)
        {
            if (IsSpawned)
            {
                if (IsServer && _isOpen.Value != isOpen)
                {
                    _isOpen.Value = isOpen;
                }

                return;
            }

            _motor?.SetOpen(isOpen);
        }

        private bool CanControlDoor()
        {
            if (IsSpawned)
            {
                return IsServer;
            }

            return NetworkManager.Singleton == null ||
                   !NetworkManager.Singleton.IsListening;
        }

        private static bool IsDoorActor(Collider2D other)
        {
            return other != null &&
                   (other.GetComponentInParent<PlayerMotor>() != null ||
                    other.GetComponentInParent<MonsterBrain>() != null);
        }

        private bool ValidateReferences()
        {
            var isValid = _motor != null && _sensor != null &&
                          _sensor.isTrigger && _config != null;
            if (!isValid)
            {
                Debug.LogError(
                    "[Door] Motor, trigger sensor or balance config is missing.",
                    this);
            }

            return isValid;
        }

        private void HandleOpenStateChanged(
            bool previousValue,
            bool currentValue)
        {
            _motor?.SetOpen(currentValue);
        }
    }
}
