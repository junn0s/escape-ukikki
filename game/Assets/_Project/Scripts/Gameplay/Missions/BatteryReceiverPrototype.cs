using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 전력실에서 분리한 배터리를 장착하는 반대편 단말기다.
    /// 실제 완료 판정은 원본 스테이션의 네트워크 권위가 처리한다.
    /// </summary>
    public sealed class BatteryReceiverPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private FuseStationPrototype _sourceStation;

        public string Prompt => "비상 배터리 장착";
        public Transform InteractionTransform => transform;
        public FuseStationPrototype SourceStation => _sourceStation;

        public void Configure(FuseStationPrototype sourceStation)
        {
            _sourceStation = sourceStation;
            _sourceStation?.ConfigureBatteryReceiver(this);
        }

        private void Awake()
        {
            _sourceStation?.ConfigureBatteryReceiver(this);
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && _sourceStation != null &&
                   _sourceStation.CanPresentBatteryInsertion(interactor);
        }

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor))
            {
                _sourceStation.BeginBatteryInsertion(interactor);
            }
        }
    }
}
