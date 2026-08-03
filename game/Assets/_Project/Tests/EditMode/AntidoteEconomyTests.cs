using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 해독제 경제(레시피·제작기·획득·보관)를 검증한다.
    /// 기준: GDD §14, docs/system-design-document.md §12,
    /// docs/balance-and-telemetry.md §8, docs/map-level-design.md §7.2.
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
            Assert.That(
                _antidoteConfig.CraftDurationSeconds,
                Is.EqualTo(180f).Within(0.001f));
            Assert.That(_antidoteConfig.FabricatorCount, Is.EqualTo(2));
            Assert.That(
                _antidoteConfig.FabricatorQueueCapacity,
                Is.EqualTo(1));
            Assert.That(_antidoteConfig.MaxCarryCount, Is.EqualTo(1));
            Assert.That(
                _antidoteConfig.UseDurationSeconds,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                _antidoteConfig.StorageLockerSlotCount,
                Is.EqualTo(2));
        }

        [Test]
        public void ItemPickupRange_MatchesBalanceTable()
        {
            Assert.That(
                _interactionConfig.ItemPickupRangeMeters,
                Is.EqualTo(1.2f).Within(0.001f));
        }

        // --- 제작 시작 검증 (SDD §12.2) ---

        [Test]
        public void CraftStart_AllowsSurvivorWithRecipeAtIdleFabricator()
        {
            Assert.That(
                ValidateCraft(),
                Is.EqualTo(AntidoteRejectionReason.None));
        }

        [Test]
        public void CraftStart_RejectsVillain()
        {
            Assert.That(
                ValidateCraft(role: PlayerRole.Villain),
                Is.EqualTo(AntidoteRejectionReason.NotSurvivor));
        }

        [Test]
        public void CraftStart_RejectsGhost()
        {
            Assert.That(
                ValidateCraft(lifeState: PlayerLifeState.DeadGhost),
                Is.EqualTo(AntidoteRejectionReason.NotAlive));
        }

        [Test]
        public void CraftStart_RejectsWithoutRecipe()
        {
            Assert.That(
                ValidateCraft(hasRecipe: false),
                Is.EqualTo(AntidoteRejectionReason.RecipeMissing));
        }

        [Test]
        public void CraftStart_RejectsBusyFabricator()
        {
            Assert.That(
                ValidateCraft(state: FabricatorState.Producing),
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

        // --- 제작 타이머 (GDD §14.3, §16.2) ---

        [Test]
        public void Fabricator_BecomesReadyAfterCraftDuration()
        {
            var fabricator = new AntidoteFabricator();
            Assert.That(
                fabricator.TryBeginCraft(
                    1UL,
                    _antidoteConfig.CraftDurationSeconds),
                Is.True);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Producing));

            fabricator.Tick(179f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Producing),
                "179초에는 아직 완성되지 않아야 한다.");

            fabricator.Tick(1f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Ready));
            Assert.That(
                fabricator.RemainingSeconds,
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Fabricator_PausesDuringMeetingAndKeepsRemainingTime()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCraft(1UL, 180f);
            fabricator.Tick(60f);
            var remainingBeforeMeeting = fabricator.RemainingSeconds;

            fabricator.SetPaused(true);
            fabricator.Tick(90f);
            Assert.That(
                fabricator.RemainingSeconds,
                Is.EqualTo(remainingBeforeMeeting).Within(0.001f),
                "회의 중에는 제작 타이머가 정지해야 한다.");
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Producing));

            fabricator.SetPaused(false);
            fabricator.Tick(120f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Ready),
                "회의가 끝나면 남은 값에서 이어져야 한다.");
        }

        [Test]
        public void Fabricator_RejectsSecondCraftWhileProducing()
        {
            var fabricator = new AntidoteFabricator();
            Assert.That(fabricator.TryBeginCraft(1UL, 180f), Is.True);
            Assert.That(
                fabricator.TryBeginCraft(2UL, 180f),
                Is.False,
                "제작기는 동시에 한 개만 생산한다(SDD §12.1).");
        }

        [Test]
        public void Fabricator_CollectIsFirstComeFirstServed()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCraft(1UL, 180f);
            fabricator.Tick(180f);

            Assert.That(fabricator.TryCollect(), Is.True);
            Assert.That(
                fabricator.TryCollect(),
                Is.False,
                "완성품 하나를 두 명이 가져갈 수 없다.");
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Idle),
                "획득 후 제작기는 Idle로 돌아간다(SDD §12.1).");
        }

        [Test]
        public void Fabricator_ReadyStateDoesNotDecayOnTick()
        {
            var fabricator = new AntidoteFabricator();
            fabricator.TryBeginCraft(1UL, 180f);
            fabricator.Tick(180f);
            fabricator.Tick(600f);
            Assert.That(
                fabricator.State,
                Is.EqualTo(FabricatorState.Ready),
                "완성품 수명은 라운드 종료까지다(밸런스 §8).");
        }

        // --- 완성품 획득 (SDD §12.3) ---

        [Test]
        public void Collect_AllowsVillain()
        {
            Assert.That(
                ValidateCollect(),
                Is.EqualTo(AntidoteRejectionReason.None),
                "빌런도 완성품을 획득할 수 있다(SDD §12.2).");
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
                ValidateCollect(state: FabricatorState.Producing),
                Is.EqualTo(AntidoteRejectionReason.NothingToCollect));
        }

        // --- 보관 칸 (GDD §14.3, SDD §12.3) ---

        [Test]
        public void Store_RequiresCarriedAntidoteAndFreeSlot()
        {
            Assert.That(
                AntidoteCraftRules.ValidateStore(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 1,
                    usedSlotCount: 0,
                    slotCapacity: 2,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.None));
            Assert.That(
                AntidoteCraftRules.ValidateStore(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 0,
                    usedSlotCount: 0,
                    slotCapacity: 2,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.NotCarrying));
            Assert.That(
                AntidoteCraftRules.ValidateStore(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 1,
                    usedSlotCount: 2,
                    slotCapacity: 2,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.StorageFull));
        }

        [Test]
        public void Withdraw_RespectsCarryLimitAndEmptyStorage()
        {
            Assert.That(
                AntidoteCraftRules.ValidateWithdraw(
                    PlayerLifeState.AliveInfected,
                    carriedCount: 0,
                    maxCarryCount: 1,
                    usedSlotCount: 1,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.None));
            Assert.That(
                AntidoteCraftRules.ValidateWithdraw(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 0,
                    maxCarryCount: 1,
                    usedSlotCount: 0,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.StorageEmpty));
            Assert.That(
                AntidoteCraftRules.ValidateWithdraw(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 1,
                    maxCarryCount: 1,
                    usedSlotCount: 1,
                    allowsMissionInteraction: true,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.CarryLimitReached));
        }

        [Test]
        public void StorageAndWithdraw_AreBlockedDuringMeeting()
        {
            Assert.That(
                AntidoteCraftRules.ValidateStore(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 1,
                    usedSlotCount: 0,
                    slotCapacity: 2,
                    allowsMissionInteraction: false,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.RoundPhaseBlocked));
            Assert.That(
                AntidoteCraftRules.ValidateWithdraw(
                    PlayerLifeState.AliveHealthy,
                    carriedCount: 0,
                    maxCarryCount: 1,
                    usedSlotCount: 1,
                    allowsMissionInteraction: false,
                    isWithinRange: true),
                Is.EqualTo(AntidoteRejectionReason.RoundPhaseBlocked));
        }

        // --- 레시피 배치 (map-level-design.md §7.2) ---

        [Test]
        public void RecipeAssignment_GivesEverySurvivorADistinctCandidate()
        {
            var survivors = new ulong[] { 1, 2, 3, 4, 5 };
            var assignment = new Dictionary<ulong, int>();

            Assert.That(
                RecipeAssignmentService.TryAssign(
                    survivors,
                    candidateCount: 8,
                    seed: 1234,
                    assignment),
                Is.True);
            Assert.That(assignment.Count, Is.EqualTo(5));

            var used = new HashSet<int>(assignment.Values);
            Assert.That(
                used.Count,
                Is.EqualTo(5),
                "생존자 5명은 서로 다른 후보를 받아야 한다.");
            foreach (var candidate in assignment.Values)
            {
                Assert.That(candidate, Is.InRange(0, 7));
            }
        }

        [Test]
        public void RecipeAssignment_FailsWhenCandidatesAreFewerThanSurvivors()
        {
            var survivors = new ulong[] { 1, 2, 3, 4, 5 };
            var assignment = new Dictionary<ulong, int>();

            Assert.That(
                RecipeAssignmentService.TryAssign(
                    survivors,
                    candidateCount: 4,
                    seed: 1234,
                    assignment),
                Is.False);
            Assert.That(assignment, Is.Empty);
        }

        [Test]
        public void RecipeAssignment_IsDeterministicForTheSameSeed()
        {
            var survivors = new ulong[] { 7, 8, 9, 10, 11 };
            var first = new Dictionary<ulong, int>();
            var second = new Dictionary<ulong, int>();

            RecipeAssignmentService.TryAssign(survivors, 8, 4242, first);
            RecipeAssignmentService.TryAssign(survivors, 8, 4242, second);

            Assert.That(first, Is.EquivalentTo(second));
        }

        [Test]
        public void RecipeAssignment_ExcludesVillainByTakingSurvivorsOnly()
        {
            // 빌런에게는 레시피가 없다(SDD §4 정보 공개 표).
            var survivors = new ulong[] { 1, 2, 3, 4, 5 };
            var assignment = new Dictionary<ulong, int>();

            RecipeAssignmentService.TryAssign(survivors, 8, 99, assignment);

            Assert.That(assignment.ContainsKey(6UL), Is.False);
        }

        [Test]
        public void LocalPrototype_CompletesRecipeCraftCollectAndStorageFlow()
        {
            var root = new GameObject("LocalAntidoteEconomyTest");
            root.SetActive(false);
            try
            {
                var infection = root.AddComponent<InfectionService>();
                var antidote = root.AddComponent<AntidoteService>();
                antidote.Configure(_antidoteConfig, infection, null, null);

                var notes = new RecipeNotePrototype[2];
                for (var index = 0; index < notes.Length; index++)
                {
                    var noteObject = new GameObject($"Recipe_{index}");
                    noteObject.transform.SetParent(root.transform);
                    var renderer = noteObject.AddComponent<SpriteRenderer>();
                    notes[index] =
                        noteObject.AddComponent<RecipeNotePrototype>();
                    notes[index].Configure(renderer, index, "TestRoom");
                }

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

                var lockerObject = new GameObject("Locker");
                lockerObject.transform.SetParent(root.transform);
                var lockerRenderer = lockerObject.AddComponent<SpriteRenderer>();
                var locker =
                    lockerObject.AddComponent<AntidoteStorageLocker>();
                locker.Configure(
                    lockerRenderer,
                    _antidoteConfig,
                    "VaccineA");

                var localEconomy = root
                    .AddComponent<LocalAntidoteEconomyPrototype>();
                localEconomy.Configure(
                    antidote,
                    infection,
                    notes,
                    new[] { fabricator },
                    new[] { locker });

                Assert.That(localEconomy.Initialize(seed: 20260803), Is.True);
                Assert.That(
                    fabricator.InteractionAuthorityOwner,
                    Is.SameAs(localEconomy));
                Assert.That(
                    locker.InteractionAuthorityOwner,
                    Is.SameAs(localEconomy));

                var assignedNote = notes[localEconomy.AssignedCandidateIndex];
                localEconomy.HandleRecipeInteraction(root, assignedNote);
                Assert.That(antidote.HasRecipe, Is.True);
                Assert.That(
                    assignedNote.IsDiscoveredByLocalPlayer,
                    Is.True);

                localEconomy.HandleFabricatorInteraction(root, fabricator);
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.Producing));
                fabricator.Fabricator.Tick(
                    _antidoteConfig.CraftDurationSeconds);
                localEconomy.HandleFabricatorInteraction(root, fabricator);
                Assert.That(antidote.CarriedCount, Is.EqualTo(1));
                Assert.That(
                    fabricator.Fabricator.State,
                    Is.EqualTo(FabricatorState.Idle));

                localEconomy.HandleLockerInteraction(root, locker);
                Assert.That(antidote.CarriedCount, Is.Zero);
                Assert.That(locker.StoredCount, Is.EqualTo(1));
                localEconomy.HandleLockerInteraction(root, locker);
                Assert.That(antidote.CarriedCount, Is.EqualTo(1));
                Assert.That(locker.StoredCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecipeRecordPrompt_ExplainsWrongAndCorrectCandidates()
        {
            var noteObject = new GameObject("RecipePromptTest");
            noteObject.SetActive(false);
            try
            {
                var renderer = noteObject.AddComponent<SpriteRenderer>();
                var note = noteObject.AddComponent<RecipeNotePrototype>();
                note.Configure(renderer, 0, "TestRoom");
                noteObject.SetActive(true);

                Assert.That(note.Prompt, Does.Contain("레시피 기록 확인"));

                note.Interact(noteObject);
                Assert.That(note.Prompt, Does.Contain("내 레시피 아님"));

                note.ApplyLocalDiscovery();
                Assert.That(note.Prompt, Does.Contain("레시피 확보"));
            }
            finally
            {
                Object.DestroyImmediate(noteObject);
            }
        }

        [Test]
        public void FabricatorFeedback_ExplainsMissingRecipe()
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
                    AntidoteRejectionReason.RecipeMissing);

                Assert.That(fabricator.Prompt, Does.Contain("레시피"));
            }
            finally
            {
                Object.DestroyImmediate(fabricatorObject);
            }
        }

        // --- 헬퍼 ---

        private static AntidoteRejectionReason ValidateCraft(
            PlayerRole role = PlayerRole.Survivor,
            PlayerLifeState lifeState = PlayerLifeState.AliveHealthy,
            bool hasRecipe = true,
            FabricatorState state = FabricatorState.Idle,
            bool allowsInteraction = true,
            bool isWithinRange = true)
        {
            return AntidoteCraftRules.ValidateCraftStart(
                role,
                lifeState,
                hasRecipe,
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
