using System;
using System.IO;
using System.Text;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Persistence;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Tests.EditMode.Runtime.Persistence
{
    public sealed class SaveUxPolicyTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "age-of-survival-save-ux-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        [Test]
        public void SlotPolicy_ExposesExactlyThreeStableSlots()
        {
            Assert.That(SaveSlotPolicy.All.Count, Is.EqualTo(3));
            Assert.That(SaveSlotPolicy.All[0].StorageKey, Is.EqualTo("slot-1"));
            Assert.That(SaveSlotPolicy.All[2].DisplayName, Is.EqualTo("Partie 3"));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void SlotId_RejectsOutOfRangeIndex(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSlotId(index));
        }

        [Test]
        public void Scheduler_DefersAutosaveUntilSafePoint()
        {
            var scheduler = new SaveRequestScheduler();
            scheduler.Advance(SaveSlotPolicy.AutosaveIntervalSeconds);

            Assert.That(scheduler.HasPending, Is.True);
            Assert.That(scheduler.TryConsume(false, out _), Is.False);
            Assert.That(
                scheduler.TryConsume(true, out SaveRequestKind kind),
                Is.True);
            Assert.That(kind, Is.EqualTo(SaveRequestKind.Autosave));
        }

        [Test]
        public void Scheduler_PreservesHighestPriorityRequest()
        {
            var scheduler = new SaveRequestScheduler();
            scheduler.Request(SaveRequestKind.Autosave);
            scheduler.Request(SaveRequestKind.Manual);
            scheduler.Request(SaveRequestKind.ReturnToMainMenu);
            scheduler.Request(SaveRequestKind.Autosave);

            Assert.That(scheduler.TryConsume(true, out SaveRequestKind kind), Is.True);
            Assert.That(kind, Is.EqualTo(SaveRequestKind.ReturnToMainMenu));
        }

        [Test]
        public void Scheduler_MarkSavedRestartsAutosaveInterval()
        {
            var scheduler = new SaveRequestScheduler();
            scheduler.Advance(599d);
            scheduler.MarkSaved();
            scheduler.Advance(2d);

            Assert.That(scheduler.HasPending, Is.False);
            Assert.That(scheduler.ElapsedSinceSave, Is.EqualTo(2d));
        }

        [Test]
        public void Metadata_RoundTripsCanonicalFields()
        {
            var store = new SaveSlotMetadataStore(_temporaryDirectory);
            var expected = new SaveSlotMetadata(
                new SaveSlotId(2),
                new DateTime(638900000000000000L, DateTimeKind.Utc),
                1234.5d,
                new WorldSeed(42UL),
                true);

            store.Write(expected);

            Assert.That(store.TryRead(expected.Slot, out SaveSlotMetadata actual), Is.True);
            Assert.That(actual.Slot, Is.EqualTo(expected.Slot));
            Assert.That(actual.SavedAtUtc, Is.EqualTo(expected.SavedAtUtc));
            Assert.That(actual.PlayedSeconds, Is.EqualTo(expected.PlayedSeconds));
            Assert.That(actual.Seed, Is.EqualTo(expected.Seed));
            Assert.That(actual.RecoveredFromBackup, Is.True);
        }


        [Test]
        public void Metadata_SecondWriteReplacesPreviousValue()
        {
            var store = new SaveSlotMetadataStore(_temporaryDirectory);
            SaveSlotId slot = new SaveSlotId(1);
            store.Write(new SaveSlotMetadata(
                slot,
                new DateTime(638900000000000000L, DateTimeKind.Utc),
                10d,
                new WorldSeed(1UL),
                false));
            store.Write(new SaveSlotMetadata(
                slot,
                new DateTime(638900000100000000L, DateTimeKind.Utc),
                20d,
                new WorldSeed(2UL),
                false));

            Assert.That(store.TryRead(slot, out SaveSlotMetadata actual), Is.True);
            Assert.That(actual.PlayedSeconds, Is.EqualTo(20d));
            Assert.That(actual.Seed, Is.EqualTo(new WorldSeed(2UL)));
            Assert.That(
                File.Exists(Path.Combine(_temporaryDirectory, "slot-1.aosmeta.tmp")),
                Is.False);
        }

        [Test]
        public void MainMenu_SaveActionsExposeContinueAndThreeSlotChoicesPerRoute()
        {
            var actions = new RecordingSaveMainMenuActions();
            var ui = new MainMenuDocument(new VisualElement(), actions);

            Assert.That(ui.ContinueButton.enabledSelf, Is.True);
            Assert.That(ui.LoadButton.enabledSelf, Is.True);
            Assert.That(ui.SlotButtons.Count, Is.EqualTo(6));
            ui.ShowNewGameSlots();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.NewGameSlots));
            ui.ShowLoadSlots();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.LoadSlots));
        }

        [Test]
        public void PauseMenu_SaveActionsExposeManualAndSaveBeforeExitRoutes()
        {
            var ui = new PauseMenuDocument(
                new VisualElement(),
                new RecordingSavePauseActions());

            Assert.That(ui.SaveButton.text, Is.EqualTo("Sauvegarder"));
            Assert.That(ui.MainMenuButton.text, Is.EqualTo("Sauvegarder et retourner"));
            Assert.That(ui.QuitButton.text, Is.EqualTo("Sauvegarder et quitter"));
        }

        [Test]
        public void Metadata_MissingSlotReturnsFalse()
        {
            var store = new SaveSlotMetadataStore(_temporaryDirectory);
            Assert.That(store.TryRead(new SaveSlotId(1), out _), Is.False);
        }

        [Test]
        public void Metadata_InvalidPayloadIsRejected()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(
                Path.Combine(_temporaryDirectory, "slot-1.aosmeta"),
                "version=1\nslot=2\n");
            var store = new SaveSlotMetadataStore(_temporaryDirectory);

            Assert.Throws<InvalidDataException>(() =>
                store.TryRead(new SaveSlotId(1), out _));
        }

        [Test]
        public void SlotView_DescribesEmptySlot()
        {
            var view = new SaveSlotView(
                new SaveSlotId(3),
                false,
                default,
                string.Empty);
            Assert.That(view.Describe(), Is.EqualTo("Partie 3 — vide"));
        }

        [Test]
        public void PrototypeService_RoundTripsCurrentSession()
        {
            var service = new PrototypeSaveService(_temporaryDirectory);
            var session = new InventoryPrototypeSession();
            SaveSlotId slot = new SaveSlotId(1);

            service.Save(slot, session, 75d);
            CoordinatedGameLoadResult loaded = service.Load(
                slot,
                0d,
                out double playedSeconds);

            Assert.That(loaded.Source, Is.EqualTo(GameSaveLoadSource.Primary));
            Assert.That(loaded.State.FixedTick, Is.EqualTo(session.CurrentTick));
            Assert.That(
                loaded.State.PlayerPosition,
                Is.EqualTo(session.CurrentPlayerPosition));
            Assert.That(
                loaded.State.Inventory.CaptureSnapshot().MainContainerId,
                Is.EqualTo(session.Inventory.MainContainer.Id));
            Assert.That(playedSeconds, Is.EqualTo(75d));
        }

        [Test]
        public void PrototypeService_ReportsThreeSlots()
        {
            var service = new PrototypeSaveService(_temporaryDirectory);
            Assert.That(service.ReadSlots().Count, Is.EqualTo(3));
        }


        [Test]
        public void PrototypeService_LoadIgnoresCorruptMetadata()
        {
            var service = new PrototypeSaveService(_temporaryDirectory);
            var session = new InventoryPrototypeSession();
            SaveSlotId slot = new SaveSlotId(1);
            service.Save(slot, session, 75d);
            File.WriteAllText(
                Path.Combine(_temporaryDirectory, "slot-1.aosmeta"),
                "version=1\nslot=2\n");

            CoordinatedGameLoadResult loaded = service.Load(
                slot,
                999d,
                out double playedSeconds);

            Assert.That(loaded.State.FixedTick, Is.EqualTo(session.CurrentTick));
            Assert.That(playedSeconds, Is.Zero);
        }

        [Test]
        public void PrototypeService_ReadSlotsTreatsOverflowingDurationAsUnavailable()
        {
            var service = new PrototypeSaveService(_temporaryDirectory);
            var slot = new SaveSlotId(1);
            service.Save(slot, new InventoryPrototypeSession(), 10d);
            string metadata = string.Join("\n", new[]
            {
                "version=1",
                "slot=1",
                "savedUtcTicks=638900000000000000",
                "playedSeconds=1E+100",
                "seed=0",
                "backup=0",
                string.Empty
            });
            File.WriteAllText(
                Path.Combine(_temporaryDirectory, "slot-1.aosmeta"),
                metadata,
                new UTF8Encoding(false));

            SaveSlotView view = service.ReadSlots()[0];

            Assert.That(view.Exists, Is.True);
            Assert.That(view.HasMetadata, Is.False);
            Assert.That(
                view.Describe(),
                Is.EqualTo(
                    "Partie 1 — sauvegarde présente — informations indisponibles"));
        }



        [Test]
        public void PrototypeService_MultipleSlotsKeepIndependentPositions()
        {
            var service = new PrototypeSaveService(
                _temporaryDirectory);
            WorldPosition[] positions =
            {
                new WorldPosition(11.25d, -3.5d),
                new WorldPosition(-47.75d, 92.125d),
                new WorldPosition(301.5d, -144.25d)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                var session = new InventoryPrototypeSession();
                session.BeginSimulationTick(positions[index]);
                service.Save(
                    new SaveSlotId(index + 1),
                    session,
                    10d + index);
            }

            for (int index = 0; index < positions.Length; index++)
            {
                CoordinatedGameLoadResult loaded = service.Load(
                    new SaveSlotId(index + 1),
                    0d,
                    out double playedSeconds);

                Assert.That(
                    loaded.State.PlayerPosition,
                    Is.EqualTo(positions[index]));
                Assert.That(
                    loaded.State.FixedTick,
                    Is.EqualTo(1L));
                Assert.That(
                    playedSeconds,
                    Is.EqualTo(10d + index));
            }
        }

        [Test]
        public void PrototypeService_RoundTripPreservesHarvestInventoryGroundAndCanonicalBytes()
        {
            WorldPopulationSettings world =
                WorldPopulationDefaults.CreateTemperatePrototypeV1(
                    new WorldSeed(0UL));
            var generator =
                new DeterministicWorldPopulationGenerator(world);
            var coordinate = new ChunkCoordinate(0, 0);
            PopulatedChunk chunk = generator.Generate(coordinate);
            var generated = new ResourceState[chunk.Resources.Count];
            for (int index = 0; index < chunk.Resources.Count; index++)
            {
                GeneratedResourcePlacement placement =
                    chunk.Resources[index];
                generated[index] = new ResourceState(
                    placement.Id,
                    new WorldPosition(
                        placement.Cell.X,
                        placement.Cell.Y));
            }

            Assert.That(generated.Length, Is.GreaterThan(0));

            var session = new InventoryPrototypeSession(
                Array.Empty<ResourceState>());
            session.SynchronizeGeneratedChunkResources(
                generated,
                new[] { coordinate },
                chunk.Layout);

            ResourceState target = session.Resources[0];
            long tick = session.BeginSimulationTick(target.Position);
            ResourceYieldResult yield =
                session.HarvestAndStartTransfer(
                    target.Position,
                    0d,
                    tick);
            Assert.That(yield.Succeeded, Is.True);

            session.AdvanceTransfer(
                tick + 10000L,
                target.Position,
                false);
            Assert.That(
                session.TransferAction.Status,
                Is.Not.EqualTo(TransferActionStatus.Active));

            int expectedGroundQuantity = InventoryOperations.Count(
                yield.Ground.Container,
                InventoryPrototypeCatalog.Branches.Id);
            int expectedCarriedQuantity = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);
            Assert.That(expectedGroundQuantity, Is.GreaterThan(0));

            GameSaveSnapshot expectedSnapshot =
                session.CaptureGameSaveSnapshot();
            byte[] expectedBytes =
                GameSaveBinaryCodec.Encode(expectedSnapshot);
            Assert.That(
                expectedSnapshot.ChunkMutations.Count,
                Is.GreaterThan(0));
            Assert.That(
                expectedSnapshot.ChunkMutations[0]
                    .HarvestedResources.Count,
                Is.GreaterThan(0));

            var service = new PrototypeSaveService(
                _temporaryDirectory);
            var slot = new SaveSlotId(1);

            service.Save(slot, session, 12d);
            CoordinatedGameLoadResult loaded = service.Load(
                slot,
                0d,
                out double playedSeconds);

            InventoryPrototypeSession restored =
                InventoryPrototypeSessionProvider.Install(
                    loaded.State);
            try
            {
                restored.SynchronizeGeneratedChunkResources(
                    generated,
                    new[] { coordinate },
                    chunk.Layout);

                ResourceState restoredResource =
                    restored.FindResource(target.Id);
                GroundContainerState restoredGround =
                    restored.FindGround(yield.Ground.Container.Id);

                Assert.That(playedSeconds, Is.EqualTo(12d));
                Assert.That(restoredResource, Is.Not.Null);
                Assert.That(
                    restoredResource.Availability,
                    Is.EqualTo(ResourceAvailability.Harvested));
                Assert.That(restoredGround, Is.Not.Null);
                Assert.That(
                    InventoryOperations.Count(
                        restoredGround.Container,
                        InventoryPrototypeCatalog.Branches.Id),
                    Is.EqualTo(expectedGroundQuantity));
                Assert.That(
                    InventoryOperations.Count(
                        restored.MainContainer,
                        InventoryPrototypeCatalog.Branches.Id),
                    Is.EqualTo(expectedCarriedQuantity));
                Assert.That(
                    GameSaveBinaryCodec.Encode(
                        restored.CaptureGameSaveSnapshot()),
                    Is.EqualTo(expectedBytes));
            }
            finally
            {
                InventoryPrototypeSessionProvider.ResetForNewGame();
            }
        }

        [Test]
        public void PrototypeService_SaveSurvivesMetadataWriteFailure()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            Directory.CreateDirectory(Path.Combine(
                _temporaryDirectory,
                "slot-1.aosmeta.tmp"));
            var service = new PrototypeSaveService(_temporaryDirectory);
            SaveSlotId slot = new SaveSlotId(1);

            Assert.DoesNotThrow(() =>
                service.Save(slot, new InventoryPrototypeSession(), 10d));
            Assert.That(service.Exists(slot), Is.True);
        }

        [Test]
        public void PrototypeService_RejectsUnknownCarriedContainerFingerprint()
        {
            SaveSlotId slot = new SaveSlotId(1);
            WorldPopulationSettings world =
                WorldPopulationDefaults.CreateTemperatePrototypeV1(
                    new WorldSeed(0UL));
            var main = new ContainerState(
                InventoryPrototypeCatalog.MainContainerId,
                new ContainerDefinition(
                    "player-main",
                    "Carried inventory",
                    new EncumbranceValue(
                        InventoryPrototypeCatalog.MainCapacityUnits + 1)));
            var inventory = new PlayerInventoryState(
                main.Id,
                Array.Empty<ItemDefinition>(),
                new[] { main });
            var snapshot = new GameSaveSnapshot(
                new WorldIdentitySnapshot(
                    world.Generation,
                    world.Profile.Id,
                    world.Profile.Revision),
                0L,
                new WorldPosition(0d, 0d),
                inventory.CaptureSnapshot(),
                Array.Empty<ChunkMutationState>());
            new AtomicGameSaveStorage(_temporaryDirectory).Save(
                slot.StorageKey,
                snapshot);
            var service = new PrototypeSaveService(_temporaryDirectory);

            Assert.Throws<NotSupportedException>(() =>
                service.Load(slot, 0d, out _));
        }

        [Test]
        public void PauseMenu_ExposesQuitWithoutSavingAfterFailure()
        {
            var actions = new RecordingSavePauseActions
            {
                CanQuitWithoutSavingValue = true
            };
            var ui = new PauseMenuDocument(
                new VisualElement(),
                actions);

            ui.SetBusy(true);
            ui.SetBusy(false);
            ui.SetQuitWithoutSavingVisible(
                actions.CanQuitWithoutSaving);

            Assert.That(
                ui.QuitWithoutSavingButton.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ui.SaveButton.enabledSelf, Is.True);
        }

        [Test]
        public void ActiveTransfer_CanBeCancelledWithoutApplyingItems()
        {
            var session = new InventoryPrototypeSession();
            ResourceState target = session.Resources[0];
            long tick = session.BeginSimulationTick(target.Position);
            ResourceYieldResult yield = session.HarvestAndStartTransfer(
                target.Position,
                0d,
                tick);

            Assert.That(yield.Succeeded, Is.True);
            Assert.That(
                session.TransferAction.Status,
                Is.EqualTo(TransferActionStatus.Active));

            int groundBefore = InventoryOperations.Count(
                yield.Ground.Container,
                InventoryPrototypeCatalog.Branches.Id);
            int carriedBefore = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(
                session.CancelActiveTransferForSaveAndQuit(),
                Is.True);
            Assert.That(
                session.TransferAction.Status,
                Is.EqualTo(TransferActionStatus.Interrupted));
            Assert.That(
                InventoryOperations.Count(
                    yield.Ground.Container,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(groundBefore));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(carriedBefore));
        }

        private sealed class RecordingSaveMainMenuActions :
            IMainMenuActions,
            ISaveMainMenuActions
        {
            private readonly SaveSlotView[] _slots =
            {
                new SaveSlotView(
                    new SaveSlotId(1),
                    true,
                    new SaveSlotMetadata(
                        new SaveSlotId(1),
                        new DateTime(638900000000000000L, DateTimeKind.Utc),
                        60d,
                        new WorldSeed(0UL),
                        false),
                    string.Empty),
                new SaveSlotView(new SaveSlotId(2), false, default, string.Empty),
                new SaveSlotView(new SaveSlotId(3), false, default, string.Empty)
            };

            public bool HasSave => true;
            public bool IsBusy => false;
            public IOnlineFrontendAvailability Online =>
                DeferredOnlineFrontendAvailability.Instance;
            public System.Collections.Generic.IReadOnlyList<SaveSlotView> SaveSlots =>
                Array.AsReadOnly(_slots);
            public bool StartNewGame() => true;
            public bool LoadGame() => true;
            public bool ContinueMostRecent() => true;
            public bool StartNewGameInSlot(int slotIndex) => true;
            public bool LoadGameFromSlot(int slotIndex) => true;
            public void Quit()
            {
            }
        }

        private sealed class RecordingSavePauseActions :
            IPauseMenuActions,
            ISavePauseMenuActions
        {
            public bool IsBusy => false;
            public string SaveStatus => string.Empty;
            public bool CanQuitWithoutSavingValue;
            public bool CanQuitWithoutSaving =>
                CanQuitWithoutSavingValue;
            public void Resume()
            {
            }

            public bool ReturnToMainMenu() => true;
            public void Quit()
            {
            }

            public void SaveGame()
            {
            }

            public void SaveAndReturnToMainMenu()
            {
            }

            public void SaveAndQuit()
            {
            }

            public void QuitWithoutSaving()
            {
            }
        }

    }
}
