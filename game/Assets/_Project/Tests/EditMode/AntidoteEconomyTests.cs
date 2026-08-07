using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 해독제 경제(배합 코드·제작대·획득)를 검증한다.
    /// 기준: GDD §14, docs/system-design-document.md §12,
    /// docs/balance-and-telemetry.md §8.
    /// </summary>
    public sealed class AntidoteEconomyTests
    {
        private AntidoteBalanceConfig _antidoteConfig;
        private InteractionBalanceConfig _interactionConfig;

        [SetUp]
        public void SetUp()
        {
            _antidoteConfig =
                ScriptableObject.CreateInstance<AntidoteBalanceConfig>();
            _interactionConfig =
                ScriptableObject.CreateInstance<InteractionBalanceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_antidoteConfig);
            Object.DestroyImmediate(_interactionConfig);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §3, §8) ---

        [Test]
        public void AntidoteBalance_MatchesBalanceTable()
        {
            Assert.That(_antidoteConfig.FabricatorCount, Is.EqualTo(2));
            Assert.That(
                _antidoteConfig.FabricatorQueueCapacity,
                Is.EqualTo(1));
            Assert.That(_antidoteConfig.MaxCarryCount, Is.EqualTo(1));
            Assert.That(
                _antidoteConfig.UseDurationSeconds,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                _antidoteConfig.CodeAnalysisSeconds,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(_antidoteConfig.CodeLength, Is.EqualTo(5));
            Assert.That(_antidoteConfig.MaxCodeAttempts, Is.EqualTo(3));
            Assert.That(
                _antidoteConfig.SynthesisSeconds,
                Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void ItemPickupRange_MatchesBalanceTable()
        {
            Assert.That(
                _interactionConfig.ItemPickupRangeMeters,
                Is.EqualTo(1.2f).Within(0.001f));
        }

        // --- 배합 코드 생성 (GDD §14.2, SDD §12.1) ---

        [Test]
        public void CodeGenerator_IsDeterministicForTheSameSeed()
        {
            var first = AntidoteCodeGenerator.Generate(5, 4242);
            var second = AntidoteCodeGenerator.Generate(5, 4242);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Length, Is.EqualTo(5));
        }

        [Test]
        public void CodeGenerator_OnlyUsesUppercaseLetters()
        {
            var code = AntidoteCodeGenerator.Generate(5, 20260807);

            foreach (var character in code)
            {
                Assert.That(character, Is.InRange('A', 'Z'));
            }
        }

        // --- 코드 세션 판정 (SDD §12.4) ---

        [Test]
        public void CodeEntry_RejectsWrongCodeAndResetsInput()
        {
            var session = new AntidoteCodeSession();
            session.IssueCode("ABCDE");

            Assert.That(session.TrySubmit("WRONG", maxAttempts: 3), Is.False);
            Assert.That(
                session.HasValidCode,
                Is.True,
                "1회 오입으로는 코드가 무효화되지 않아야 한다.");
            Assert.That(session.FailedAttemptCount, Is.EqualTo(1));
        }

        [Test]
        public void CodeEntry_InvalidatesCodeAfterThreeFailures()
        {
            var session = new AntidoteCodeSession();
            session.IssueCode("ABCDE");

            session.TrySubmit("WRONG", maxAttempts: 3);
            session.TrySubmit("WRONG", maxAttempts: 3);
            Assert.That(
                session.HasValidCode,
                Is.True,
                "2회 오입까지는 코드가 유지되어야 한다.");

            session.TrySubmit("WRONG", maxAttempts: 3);
            Assert.That(
                session.HasValidCode,
                Is.False,
                "3회 오입 시 코드가 무효화되어야 한다(GDD §14.2).");
        }

        [Test]
        public void CodeEntry_AcceptsCorrectCodeAndKeepsSessionValid()
        {
            var session = new AntidoteCodeSession();
            session.IssueCode("ABCDE");

            Assert.That(session.TrySubmit("ABCDE", maxAttempts: 3), Is.True);
            Assert.That(session.FailedAttemptCount, Is.Zero);
        }

        [Test]
        public void CodeEntry_IssuingNewCodeResetsFailedAttempts()
        {
            var session = new AntidoteCodeSession();
            session.IssueCode("ABCDE");
            session.TrySubmit("WRONG", maxAttempts: 3);

            session.IssueCode("FGHIJ");

            Assert.That(session.FailedAttemptCount, Is.Zero);
            Assert.That(session.Code, Is.EqualTo("FGHIJ"));
        }

        // --- 코드 발급 검증 (SDD §12.1) ---

        [Test]
        public void CodeIssue_AllowsAnyLivingPlayer()
        {
            Assert.That(
                AntidoteCraftRules.ValidateCodeIssue(
                    PlayerLifeState.AliveHealthy,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.None));
        }

        [Test]
        public void CodeIssue_RejectsGhost()
        {
            Assert.That(
                AntidoteCraftRules.ValidateCodeIssue(
                    PlayerLifeState.DeadGhost,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.NotAlive));
        }

        [Test]
        public void CodeIssue_RejectsDuringMeeting()
        {
            Assert.That(
                AntidoteCraftRules.ValidateCodeIssue(
                    PlayerLifeState.AliveHealthy,
                    allowsMissionInteraction: false,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.RoundPhaseBlocked));
        }

        [Test]
        public void CodeIssue_RejectsOutOfRange()
        {
            Assert.That(
                AntidoteCraftRules.ValidateCodeIssue(
                    PlayerLifeState.AliveHealthy,
                    allowsMissionInteraction: true,
                    isWithinRange: false),
                Is.EqualTo(AntidoteRejectionReason.OutOfRange));
        }

        // --- 제작 시작 검증 (SDD §12.3) ---

        [Test]
        public void CraftStart_AllowsSurvivorWithCodeAtIdleFabricator()
        {
            Assert.That(
                ValidateCraft(),
                Is.EqualTo(AntidoteRejectionReason.None));
        }

        [Test]
        public void CraftStart_AllowsVillain()
        {
            // 빌런도 감염되므로 생존자와 동일하게 제작할 수 있다(GDD §14.3).
            // 역할은 검사 대상이 아니므로 ValidateCraftStart에 역할 매개변수가 없다.
            Assert.That(
                ValidateCraft(),
                Is.EqualTo(AntidoteRejectionReason.None));
        }

        [Test]
        public void CraftStart_RejectsGhost()
        {
            Assert.That(
                ValidateCraft(lifeState: PlayerLifeState.DeadGhost),
                Is.EqualTo(AntidoteRejectionReason.NotAlive));
        }

        [Test]
        public void CraftStart_RejectsWithoutCode()
        {
            Assert.That(
                ValidateCraft(hasValidCode: false),
                Is.EqualTo(AntidoteRejectionReason.CodeMissing));
        }

        [Test]
        public void CraftStart_RejectsBusyFabricator()
        {
            Assert.That(
                ValidateCraft(state: FabricatorState.AwaitingCode),
                Is.EqualTo(AntidoteRejectionReason.FabricatorBusy));
            Assert.That(
                ValidateCraft(state: FabricatorState.Synthesizing),
                Is.EqualTo(AntidoteRejectionReason.FabricatorBusy));
            Assert.That(
                ValidateCraft(state: FabricatorState.Ready),
                Is.EqualTo(AntidoteRejectionReason.FabricatorBusy));
        }

        [Test]
        public void CraftStart_RejectsDuringMeeting()
        {
            Assert.That(
                ValidateCraft(allowsInteraction: false),
                Is.EqualTo(AntidoteRejectionReason.RoundPhaseBlocked));
        }

        [Test]
        public void CraftStart_RejectsOutOfRange()
        {
            Assert.That(
                ValidateCraft(isWithinRange: false),
                Is.EqualTo(AntidoteRejectionReason.OutOfRange));
        }

        [Test]
        public void CraftStart_InfectedSurvivorCanStillCraft()
        {
            Assert.That(
                ValidateCraft(lifeState: PlayerLifeState.AliveInfected),
                Is.EqualTo(AntidoteRejectionReason.None));
        }

        // --- 제작대 상태 (GDD §14.3, §16.2) ---

        [Test]
        public void Fabricator_MovesThroughCodeEntryAndSynthesis()
        {
            var fabricator = new AntidoteFabricator();
            Assert.That(fabricator.TryBeginCodeEntry(1UL), Is.True);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.AwaitingCode));

            Assert.That(
                fabricator.TryBeginSynthesis(_antidoteConfig.SynthesisSeconds),
                Is.True);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Synthesizing));

            fabricator.Tick(_antidoteConfig.SynthesisSeconds - 1f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Synthesizing),
                "합성 시간이 다 되기 전에는 완성되지 않아야 한다.");

            fabricator.Tick(1f);
            Assert.That(fabricator.State, Is.EqualTo(FabricatorState.Ready));
            Assert.That(
                fabricator.RemainingSeconds,
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Fabricator_PausesDuringMeetingAndKeepsRemainingTime()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCodeEntry(1UL);
            fabricator.TryBeginSynthesis(4f);
            fabricator.Tick(1f);
            var remainingBeforeMeeting = fabricator.RemainingSeconds;

            fabricator.SetPaused(true);
            fabricator.Tick(90f);
            Assert.That(
                fabricator.RemainingSeconds,
                Is.EqualTo(remainingBeforeMeeting).Within(0.001f),
                "회의 중에는 합성 타이머가 정지해야 한다.");
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Synthesizing));

            fabricator.SetPaused(false);
            fabricator.Tick(120f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Ready),
                "회의가 끝나면 남은 값에서 이어져야 한다.");
        }

        [Test]
        public void Fabricator_RejectsSecondCodeEntryWhileAwaitingCode()
        {
            var fabricator = new AntidoteFabricator();
            Assert.That(fabricator.TryBeginCodeEntry(1UL), Is.True);
            Assert.That(
                fabricator.TryBeginCodeEntry(2UL),
                Is.False,
                "제작대는 동시에 한 명만 사용한다(SDD §12.2).");
        }

        [Test]
        public void Fabricator_CollectIsFirstComeFirstServed()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCodeEntry(1UL);
            fabricator.TryBeginSynthesis(4f);
            fabricator.Tick(4f);

            Assert.That(fabricator.TryCollect(), Is.True);
            Assert.That(
                fabricator.TryCollect(),
                Is.False,
                "완성품 하나를 두 명이 가져갈 수 없다.");
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Idle),
                "획득 후 제작대는 Idle로 돌아간다(SDD §12.2).");
        }

        [Test]
        public void Fabricator_ReadyStateDoesNotDecayOnTick()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCodeEntry(1UL);
            fabricator.TryBeginSynthesis(4f);
            fabricator.Tick(4f);
            fabricator.Tick(600f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Ready),
                "완성품 수명은 라운드 종료까지다(밸런스 §8).");
        }

        [Test]
        public void Fabricator_ResetReturnsToIdleFromAnyState()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCodeEntry(1UL);

            fabricator.Reset();

            Assert.That(fabricator.State, Is.EqualTo(FabricatorState.Idle));
            Assert.That(
                fabricator.CrafterClientId,
                Is.EqualTo(AntidoteFabricator.NoCrafterClientId));
        }

        // --- 완성품 획득 (SDD §12.5) ---

        [Test]
        public void Collect_AllowsVillain()
        {
            Assert.That(
                ValidateCollect(),
                Is.EqualTo(AntidoteRejectionReason.None),
                "빌런도 완성품을 획득할 수 있다(SDD §12.5).");
        }

        [Test]
        public void Collect_RejectsGhost()
        {
            Assert.That(
                ValidateCollect(lifeState: PlayerLifeState.DeadGhost),
                Is.EqualTo(AntidoteRejectionReason.NotAlive));
        }

        [Test]
        public void Collect_RejectsWhenCarryLimitReached()
        {
            Assert.That(
                ValidateCollect(carriedCount: 1),
                Is.EqualTo(AntidoteRejectionReason.CarryLimitReached));
        }

        [Test]
        public void Collect_RejectsWhenFabricatorIsNotReady()
        {
            Assert.That(
                ValidateCollect(state: FabricatorState.Idle),
                Is.EqualTo(AntidoteRejectionReason.NothingToCollect));
            Assert.That(
                ValidateCollect(state: FabricatorState.Synthesizing),
                Is.EqualTo(AntidoteRejectionReason.NothingToCollect));
        }

        // --- 감염 중 이동 (GDD §14.1, SDD §13.2.1) ---

        [Test]
        public void InfectedMoveSpeedMultiplier_MatchesBalanceTable()
        {
            var movementConfig =
                ScriptableObject.CreateInstance<
                    MonkeyLab.Gameplay.Player.PlayerMovementConfig>();
            try
            {
                Assert.That(
                    movementConfig.InfectedMoveSpeedMultiplier,
                    Is.EqualTo(0.8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(movementConfig);
            }
        }

        // --- 로컬 프로토타입 통합 흐름 ---

        [Test]
        public void LocalPrototype_CompletesTerminalToFabricatorFlow()
        {
            var root = new GameObject("LocalAntidoteEconomyTest");
            root.SetActive(false);
            try
            {
                var infection = root.AddComponent<InfectionService>();
                var antidote = root.AddComponent<AntidoteService>();
                antidote.Configure(_antidoteConfig, infection, null, null);

                var terminalObject = new GameObject("Terminal");
                terminalObject.transform.SetParent(root.transform);
                var terminalRenderer =
                    terminalObject.AddComponent<SpriteRenderer>();
                var terminal = terminalObject
                    .AddComponent<AntidoteTerminalPrototype>();
                terminal.Configure(
                    terminalRenderer,
                    _antidoteConfig,
                    "VaccineA",
                    "Test Terminal");

                var fabricatorObject = new GameObject("Fabricator");
                fabricatorObject.transform.SetParent(root.transform);
                var fabricatorRenderer =
                    fabricatorObject.AddComponent<SpriteRenderer>();
                var fabricator = fabricatorObject
                    .AddComponent<AntidoteFabricatorPrototype>();
                fabricator.Configure(
                    fabricatorRenderer,
                    _antidoteConfig,
                    "VaccineA",
                    "Test Fabricator");

                var localEconomy = root
                    .AddComponent<LocalAntidoteEconomyPrototype>();
                localEconomy.Configure(
                    antidote,
                    infection,
                    new[] { terminal },
                    new[] { fabricator });

                Assert.That(localEconomy.Initialize(seed: 20260807), Is.True);
                Assert.That(
                    terminal.InteractionAuthorityOwner,
                    Is.SameAs(localEconomy));
                Assert.That(
                    fabricator.InteractionAuthorityOwner,
                    Is.SameAs(localEconomy));

                localEconomy.HandleTerminalInteraction(root, terminal, 555);
                Assert.That(antidote.HasValidCode, Is.True);
                Assert.That(antidote.IssuedCode.Length, Is.EqualTo(5));
                var issuedCode = antidote.IssuedCode;

                localEconomy.HandleFabricatorInteraction(root, fabricator);
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.AwaitingCode));

                localEconomy.HandleCodeSubmit(root, fabricator, "WRONGCODE");
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.AwaitingCode),
                    "1회 오입으로는 제작대가 초기화되지 않아야 한다.");

                localEconomy.HandleCodeSubmit(root, fabricator, issuedCode);
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.Synthesizing));

                fabricator.Fabricator.Tick(_antidoteConfig.SynthesisSeconds);
                localEconomy.HandleFabricatorInteraction(root, fabricator);
                Assert.That(antidote.CarriedCount, Is.EqualTo(1));
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.Idle));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalPrototype_InvalidatesCodeAfterThreeWrongAttempts()
        {
            var root = new GameObject("LocalAntidoteEconomyInvalidateTest");
            root.SetActive(false);
            try
            {
                var infection = root.AddComponent<InfectionService>();
                var antidote = root.AddComponent<AntidoteService>();
                antidote.Configure(_antidoteConfig, infection, null, null);

                var terminalObject = new GameObject("Terminal");
                terminalObject.transform.SetParent(root.transform);
                var terminal = terminalObject
                    .AddComponent<AntidoteTerminalPrototype>();
                terminal.Configure(
                    terminalObject.AddComponent<SpriteRenderer>(),
                    _antidoteConfig,
                    "VaccineA",
                    "Test Terminal");

                var fabricatorObject = new GameObject("Fabricator");
                fabricatorObject.transform.SetParent(root.transform);
                var fabricator = fabricatorObject
                    .AddComponent<AntidoteFabricatorPrototype>();
                fabricator.Configure(
                    fabricatorObject.AddComponent<SpriteRenderer>(),
                    _antidoteConfig,
                    "VaccineA",
                    "Test Fabricator");

                var localEconomy = root
                    .AddComponent<LocalAntidoteEconomyPrototype>();
                localEconomy.Configure(
                    antidote,
                    infection,
                    new[] { terminal },
                    new[] { fabricator });
                localEconomy.Initialize(seed: 777);

                localEconomy.HandleTerminalInteraction(root, terminal, 777);
                localEconomy.HandleFabricatorInteraction(root, fabricator);

                localEconomy.HandleCodeSubmit(root, fabricator, "WRONG1");
                localEconomy.HandleCodeSubmit(root, fabricator, "WRONG2");
                localEconomy.HandleCodeSubmit(root, fabricator, "WRONG3");

                Assert.That(
                    antidote.HasValidCode,
                    Is.False,
                    "3회 오입 시 코드가 무효화되어야 한다(GDD §14.2).");
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.Idle),
                    "코드 무효화 시 제작대도 Idle로 돌아가야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FabricatorFeedback_ExplainsMissingCode()
        {
            var fabricatorObject = new GameObject("FabricatorFeedbackTest");
            fabricatorObject.SetActive(false);
            try
            {
                var renderer = fabricatorObject.AddComponent<SpriteRenderer>();
                var fabricator = fabricatorObject
                    .AddComponent<AntidoteFabricatorPrototype>();
                fabricator.Configure(
                    renderer,
                    _antidoteConfig,
                    "VaccineA",
                    "Test Fabricator");

                fabricator.ApplyInteractionFeedback(
                    AntidoteRejectionReason.CodeMissing);

                Assert.That(fabricator.Prompt, Does.Contain("배합 코드"));
            }
            finally
            {
                Object.DestroyImmediate(fabricatorObject);
            }
        }

        // --- 헬퍼 ---

        private static AntidoteRejectionReason ValidateCraft(
            PlayerLifeState lifeState = PlayerLifeState.AliveHealthy,
            bool hasValidCode = true,
            FabricatorState state = FabricatorState.Idle,
            bool allowsInteraction = true,
            bool isWithinRange = true)
        {
            return AntidoteCraftRules.ValidateCraftStart(
                lifeState,
                hasValidCode,
                state,
                allowsInteraction,
                isWithinRange);
        }

        private static AntidoteRejectionReason ValidateCollect(
            PlayerLifeState lifeState = PlayerLifeState.AliveHealthy,
            FabricatorState state = FabricatorState.Ready,
            int carriedCount = 0,
            bool allowsInteraction = true,
            bool isWithinRange = true)
        {
            return AntidoteCraftRules.ValidateCollect(
                lifeState,
                state,
                carriedCount,
                maxCarryCount: 1,
                allowsInteraction,
                isWithinRange);
        }
    }
}
