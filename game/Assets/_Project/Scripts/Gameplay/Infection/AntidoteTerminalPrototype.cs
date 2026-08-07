using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 백신실 중앙 제어 PC다. 백신실 A와 B가 각각 독립된 한 대를 가진다(GDD §14.2~14.3).
    /// 혈청 분석 후 요청자에게만 5자리 배합 코드를 표시한다. 코드는 저장되지 않으며
    /// 창을 닫으면 화면에서 사라진다. 실제 발급·판정은 서버가 수행하고
    /// 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class AntidoteTerminalPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _terminalRenderer;
        [SerializeField] private AntidoteBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private string _displayName = "중앙 제어 PC";
        [SerializeField] private Color _idleColor = new(0.2f, 0.5f, 0.4f, 1f);
        [SerializeField]
        private Color _analyzingColor = new(0.85f, 0.7f, 0.2f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private bool _isAnalyzing;

        public event Action<AntidoteTerminalPrototype> AnalyzingStateChanged;

        public AntidoteBalanceConfig Config => _config;
        public string RoomId => _roomId;
        public string DisplayName => _displayName;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;
        public bool IsAnalyzing => _isAnalyzing;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : _isAnalyzing
                ? "감염체 혈청 분석 중..."
                : "배합 코드 조회";

        public void Configure(
            SpriteRenderer terminalRenderer,
            AntidoteBalanceConfig config,
            string roomId,
            string displayName)
        {
            _terminalRenderer = terminalRenderer;
            _config = config;
            _roomId = roomId;
            _displayName = displayName;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject> requestInteraction)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalInteractionRequest = requestInteraction;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalInteractionRequest = null;
        }

        public void ApplyInteractionFeedback(
            AntidoteRejectionReason rejectionReason)
        {
            _interactionFeedback =
                AntidoteInteractionFeedback.ToPrompt(rejectionReason);
        }

        public void ClearInteractionFeedback()
        {
            _interactionFeedback = string.Empty;
        }

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally = _config != null && isActiveAndEnabled;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor) || _isAnalyzing)
            {
                return;
            }

            _externalInteractionRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 분석 진행 상태를 반영한다. 정답 코드는 전달하지 않는다.</summary>
        public void ApplyAuthoritativeAnalyzingState(bool isAnalyzing)
        {
            if (_isAnalyzing == isAnalyzing)
            {
                return;
            }

            _isAnalyzing = isAnalyzing;
            ApplyVisuals();
            AnalyzingStateChanged?.Invoke(this);
        }

        private void Awake()
        {
            if (_terminalRenderer == null)
            {
                _terminalRenderer = GetComponent<SpriteRenderer>();
            }

            if (_config == null)
            {
                Debug.LogError(
                    "[Antidote] Terminal balance config is missing.",
                    this);
            }
        }

        private void ApplyVisuals()
        {
            if (_terminalRenderer == null)
            {
                return;
            }

            _terminalRenderer.color = _isAnalyzing
                ? _analyzingColor
                : _idleColor;
        }
    }
}
