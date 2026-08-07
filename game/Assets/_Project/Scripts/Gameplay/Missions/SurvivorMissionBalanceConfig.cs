using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 방별 생존자 미션(GDD §10.2)의 조작값이다.
    /// docs/balance-and-telemetry.md §7.2 표의 키와 필드 이름을 맞춘다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Survivor Mission",
        fileName = "SO_SurvivorMissionBalance_Default")]
    public sealed class SurvivorMissionBalanceConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)]
        private float _vaccineDataDownloadHoldSeconds = 8f;
        [SerializeField, Min(1)]
        private int _contaminatedSyringeCount = 3;
        [SerializeField, Min(1)]
        private int _slideGlassStainCount = 3;
        [SerializeField, Min(1)]
        private int _slideGlassScrubsPerStain = 5;
        [SerializeField, Min(1)]
        private int _reagentBottleCount = 3;
        [SerializeField, Min(1)]
        private int _wireConnectCount = 4;
        [SerializeField, Min(1f)]
        private float _airlockDialToleranceDegrees = 8f;
        [SerializeField, Min(1f)]
        private float _airlockDialStartOffsetDegrees = 90f;
        [SerializeField, Min(0.1f)]
        private float _hazmatDecontaminationSeconds = 6f;
        [SerializeField, Range(0.01f, 0.49f)]
        private float _ivDripTargetHalfWidthNormalized = 0.08f;
        [SerializeField, Min(0.1f)]
        private float _ivDripCycleSeconds = 2f;
        [SerializeField, Min(1)]
        private int _patientVitalsCodeLength = 4;
        [SerializeField, Min(1f)]
        private float _valveLockTurns = 3f;
        [SerializeField, Min(0.1f)]
        private float _wasteCompactorHoldSeconds = 5f;
        [SerializeField, Min(0.1f)]
        private float _idCardSwipeMinSeconds = 0.4f;
        [SerializeField, Min(0.1f)]
        private float _idCardSwipeMaxSeconds = 1.2f;
        [SerializeField, Min(1)]
        private int _cctvScreenScrubCount = 10;

        /// <summary>백신 데이터 다운로드 — 손을 떼면 초기화되는 누르기 시간이다.</summary>
        public float VaccineDataDownloadHoldSeconds =>
            _vaccineDataDownloadHoldSeconds;

        /// <summary>오염된 주사기 폐기 — 휴지통으로 드래그할 주사기 수다.</summary>
        public int ContaminatedSyringeCount => _contaminatedSyringeCount;

        /// <summary>슬라이드 글라스 닦기 — 지워야 할 얼룩 수다.</summary>
        public int SlideGlassStainCount => _slideGlassStainCount;

        /// <summary>슬라이드 글라스 닦기 — 얼룩 하나당 필요한 문지름 횟수다.</summary>
        public int SlideGlassScrubsPerStain => _slideGlassScrubsPerStain;

        /// <summary>시약병 분류 — 색상별 시약병 수다(빨강·파랑·노랑 각 1개 기준).</summary>
        public int ReagentBottleCount => _reagentBottleCount;

        /// <summary>배선 복구 — 연결할 전선 수다(빨강·파랑·노랑·초록 기준).</summary>
        public int WireConnectCount => _wireConnectCount;

        /// <summary>에어록 압력 조절 — 0에서 완료로 인정하는 허용 오차(도)다.</summary>
        public float AirlockDialToleranceDegrees =>
            _airlockDialToleranceDegrees;

        /// <summary>에어록 압력 조절 — 시작 시 0에서 벗어난 각도(도)다.</summary>
        public float AirlockDialStartOffsetDegrees =>
            _airlockDialStartOffsetDegrees;

        /// <summary>방호복 소독 — 시야가 막히는 시간이다.</summary>
        public float HazmatDecontaminationSeconds =>
            _hazmatDecontaminationSeconds;

        /// <summary>수액 속도 조절 — 목표 구간의 중앙 기준 절반 폭(0~1 정규화)이다.</summary>
        public float IvDripTargetHalfWidthNormalized =>
            _ivDripTargetHalfWidthNormalized;

        /// <summary>수액 속도 조절 — 슬라이더 왕복 주기다.</summary>
        public float IvDripCycleSeconds => _ivDripCycleSeconds;

        /// <summary>환자 바이탈 기록 — 입력할 숫자 자릿수다.</summary>
        public int PatientVitalsCodeLength => _patientVitalsCodeLength;

        /// <summary>밸브 잠그기·밸브 압력 풀기 — 완료까지 필요한 회전 바퀴 수다.</summary>
        public float ValveLockTurns => _valveLockTurns;

        /// <summary>폐기물 통 압축 — 손을 떼면 초기화되는 누르기 시간이다.</summary>
        public float WasteCompactorHoldSeconds =>
            _wasteCompactorHoldSeconds;

        /// <summary>ID 카드 긁기 — 성공으로 인정하는 최소 드래그 시간이다.</summary>
        public float IdCardSwipeMinSeconds => _idCardSwipeMinSeconds;

        /// <summary>ID 카드 긁기 — 성공으로 인정하는 최대 드래그 시간이다.</summary>
        public float IdCardSwipeMaxSeconds => _idCardSwipeMaxSeconds;

        /// <summary>CCTV 화면 닦기 — 완료까지 필요한 문지름 횟수다.</summary>
        public int CctvScreenScrubCount => _cctvScreenScrubCount;
    }
}
