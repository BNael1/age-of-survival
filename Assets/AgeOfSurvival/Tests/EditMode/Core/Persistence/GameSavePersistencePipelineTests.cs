using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Persistence
{
    public sealed class GameSavePersistencePipelineTests
    {
        private static readonly ItemDefinition Rations = new ItemDefinition(
            new ItemDefinitionId("rations"),
            "Rations",
            ItemStateKind.Stackable,
            new EncumbranceValue(500));

        private static readonly ItemDefinition Backpack = new ItemDefinition(
            new ItemDefinitionId("backpack"),
            "Backpack",
            ItemStateKind.Unique,
            new EncumbranceValue(1000),
            new EquipmentDefinition(
                EquipmentSlotMask.Back,
                50));

        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "aos-save-tests-" + Guid.NewGuid().ToString("N"));
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
        public void CodecRoundTripsCompleteSnapshot()
        {
            GameSaveSnapshot original = CreateSnapshot(42);

            byte[] encoded = GameSaveBinaryCodec.Encode(original);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(encoded);

            Assert.That(decoded.FixedTick, Is.EqualTo(42));
            Assert.That(decoded.World, Is.EqualTo(original.World));
            Assert.That(decoded.PlayerPosition, Is.EqualTo(original.PlayerPosition));
            Assert.That(decoded.Health, Is.EqualTo(original.Health));
            Assert.That(decoded.Inventory.Definitions.Count, Is.EqualTo(2));
            Assert.That(decoded.Inventory.Containers.Count, Is.EqualTo(2));
            Assert.That(
                decoded.Inventory.Equipment.Back,
                Is.EqualTo(new ItemInstanceId("backpack-1")));
            Assert.That(decoded.ChunkMutations.Count, Is.EqualTo(1));
            Assert.That(
                decoded.ChunkMutations[0].GroundContainers.Count,
                Is.EqualTo(1));
            Assert.That(
                GameSaveBinaryCodec.Encode(decoded),
                Is.EqualTo(encoded));
        }

        [Test]
        public void CodecReadsLegacyV1AsFullHealthAtSavedTick()
        {
            byte[] legacy = ConvertV2ToLegacyV1(
                ConvertV3ToLegacyV2(
                    GameSaveBinaryCodec.Encode(CreateSnapshot(42))));

            Assert.That(ReadUInt16(legacy, 8), Is.EqualTo(1));

            GameSaveSnapshot decoded =
                GameSaveBinaryCodec.Decode(legacy);

            Assert.That(decoded.FixedTick, Is.EqualTo(42L));
            Assert.That(
                decoded.Health.MaximumHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(
                decoded.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(decoded.Health.CurrentTick, Is.EqualTo(42L));
            Assert.That(
                decoded.Health.NextRegenerationTick,
                Is.Null);
            Assert.That(decoded.Food.CurrentSatiety, Is.EqualTo(PlayerFoodRules.DefaultMaximumSatiety));
            Assert.That(decoded.Food.CurrentTick, Is.EqualTo(42L));
            Assert.That(decoded.Perishables.Batches, Is.Empty);
            Assert.That(
                ReadUInt16(
                    GameSaveBinaryCodec.Encode(decoded),
                    8),
                Is.EqualTo(3));
        }

        [Test]
        public void CodecReadsLegacyV2AsFullFoodAtSavedTick()
        {
            byte[] legacy = ConvertV3ToLegacyV2(
                GameSaveBinaryCodec.Encode(CreateSnapshot(42)));

            Assert.That(ReadUInt16(legacy, 8), Is.EqualTo(2));
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(legacy);

            Assert.That(decoded.FixedTick, Is.EqualTo(42L));
            Assert.That(decoded.Health.CurrentHealth, Is.EqualTo(65));
            Assert.That(decoded.Food.CurrentSatiety, Is.EqualTo(PlayerFoodRules.DefaultMaximumSatiety));
            Assert.That(decoded.Food.CurrentTick, Is.EqualTo(42L));
            Assert.That(decoded.Perishables.Batches, Is.Empty);
        }

        [Test]
        public void CodecRejectsInvalidV2Health()
        {
            byte[] encoded = ConvertV3ToLegacyV2(
                GameSaveBinaryCodec.Encode(CreateSnapshot(0)));
            int healthOffset = GetHealthOffset(encoded);
            WriteInt32(encoded, healthOffset, 0);
            RefreshPayloadHash(encoded);

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.InvalidDomainValue));
        }

        [Test]
        public void CodecProducesDeterministicBytes()
        {
            GameSaveSnapshot snapshot = CreateSnapshot(12);

            byte[] first = GameSaveBinaryCodec.Encode(snapshot);
            byte[] second = GameSaveBinaryCodec.Encode(snapshot);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void CodecWritesExpectedEnvelope()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));

            Assert.That(encoded[0], Is.EqualTo((byte)'A'));
            Assert.That(encoded[1], Is.EqualTo((byte)'O'));
            Assert.That(encoded[2], Is.EqualTo((byte)'S'));
            Assert.That(encoded[7], Is.EqualTo(0));
            Assert.That(ReadUInt16(encoded, 8), Is.EqualTo(3));
            Assert.That(ReadUInt16(encoded, 10), Is.EqualTo(0));
            Assert.That(
                ReadUInt32(encoded, 12),
                Is.EqualTo(encoded.Length - GameSaveCodecLimits.HeaderLength));
        }

        [Test]
        public void CodecRejectsMissingInput()
        {
            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(null));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.InputMissing));
        }

        [Test]
        public void CodecRejectsInputSmallerThanHeader()
        {
            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(new byte[47]));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.InputTooSmall));
        }

        [Test]
        public void CodecRejectsInvalidMagic()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            encoded[0] ^= 0x7f;

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.InvalidMagic));
        }

        [Test]
        public void CodecRejectsUnsupportedVersion()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            WriteUInt16(encoded, 8, 4);

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.UnsupportedVersion));
        }

        [Test]
        public void CodecRejectsUnknownFlags()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            WriteUInt16(encoded, 10, 1);

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.UnknownFlags));
        }

        [Test]
        public void CodecRejectsDeclaredPayloadAboveLimit()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            WriteUInt32(
                encoded,
                12,
                (uint)GameSaveCodecLimits.MaximumPayloadLength + 1u);

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.PayloadTooLarge));
        }

        [Test]
        public void CodecRejectsEnvelopeLengthMismatch()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            Array.Resize(ref encoded, encoded.Length + 1);

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.LengthMismatch));
        }

        [Test]
        public void CodecRejectsIntegrityMismatch()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateSnapshot(0));
            encoded[encoded.Length - 1] ^= 0x01;

            GameSaveCodecException exception =
                Assert.Throws<GameSaveCodecException>(() =>
                    GameSaveBinaryCodec.Decode(encoded));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.IntegrityMismatch));
        }

        [Test]
        public void StorageSavesAndLoadsPrimary()
        {
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);

            storage.Save("slot_1", CreateSnapshot(17));
            GameSaveLoadResult loaded = storage.Load("slot_1");

            Assert.That(loaded.Source, Is.EqualTo(GameSaveLoadSource.Primary));
            Assert.That(loaded.Snapshot.FixedTick, Is.EqualTo(17));
            Assert.That(File.Exists(storage.GetPrimaryPath("slot_1")), Is.True);
            Assert.That(File.Exists(storage.GetTemporaryPath("slot_1")), Is.False);
        }

        [Test]
        public void StorageKeepsPreviousPrimaryAsBackup()
        {
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);

            storage.Save("slot", CreateSnapshot(10));
            storage.Save("slot", CreateSnapshot(20));

            Assert.That(storage.Load("slot").Snapshot.FixedTick, Is.EqualTo(20));
            Assert.That(File.Exists(storage.GetBackupPath("slot")), Is.True);
            Assert.That(
                GameSaveBinaryCodec.Decode(
                    File.ReadAllBytes(storage.GetBackupPath("slot"))).FixedTick,
                Is.EqualTo(10));
        }

        [Test]
        public void StorageFallsBackToBackupWhenPrimaryIsCorrupt()
        {
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);
            storage.Save("slot", CreateSnapshot(10));
            storage.Save("slot", CreateSnapshot(20));
            File.WriteAllBytes(
                storage.GetPrimaryPath("slot"),
                new byte[] { 1, 2, 3 });

            GameSaveLoadResult loaded = storage.Load("slot");

            Assert.That(loaded.Source, Is.EqualTo(GameSaveLoadSource.Backup));
            Assert.That(loaded.Snapshot.FixedTick, Is.EqualTo(10));
        }

        [Test]
        public void StorageRejectsUnsafeSlot()
        {
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);

            Assert.Throws<ArgumentException>(() =>
                storage.Exists("../escape"));
        }

        [Test]
        public void StorageReportsMissingSave()
        {
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);

            Assert.Throws<FileNotFoundException>(() =>
                storage.Load("missing"));
        }

        [Test]
        public void RestorerBuildsNewInventoryAndChunkLifecycle()
        {
            WorldPopulationSettings world = CreateWorld();
            GameSaveSnapshot snapshot = CreateSnapshot(55, world);
            var resolver = new CatalogResolver(world, Rations, Backpack);

            RestoredGameState restored = GameSaveSnapshotRestorer.Restore(
                snapshot,
                resolver,
                resolver);

            Assert.That(restored.FixedTick, Is.EqualTo(55));
            Assert.That(restored.Health.CurrentHealth, Is.EqualTo(65));
            Assert.That(restored.Health.CurrentTick, Is.EqualTo(55L));
            Assert.That(
                restored.Health.NextRegenerationTick,
                Is.EqualTo(565L));
            Assert.That(restored.World.Generation, Is.EqualTo(world.Generation));
            Assert.That(restored.Inventory.MainContainer.Id, Is.EqualTo(snapshot.Inventory.MainContainerId));
            Assert.That(restored.Inventory.Containers.Count, Is.EqualTo(2));
            Assert.That(
                restored.Inventory.Equipment.Get(EquipmentSlot.Back),
                Is.EqualTo(new ItemInstanceId("backpack-1")));
            Assert.That(restored.Chunks.Store.Count, Is.EqualTo(1));
            Assert.That(restored.Chunks.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void RestoredLifecycleCanActivatePersistedGroundMutation()
        {
            WorldPopulationSettings world = CreateWorld();
            GameSaveSnapshot snapshot = CreateSnapshot(0, world);
            var resolver = new CatalogResolver(world, Rations, Backpack);
            RestoredGameState restored = GameSaveSnapshotRestorer.Restore(
                snapshot,
                resolver,
                resolver);

            ActiveChunkState active = restored.Chunks.Activate(
                new ChunkCoordinate(0, 0));

            Assert.That(active.GroundContainers.Count, Is.EqualTo(1));
            Assert.That(restored.Chunks.Store.Count, Is.EqualTo(0));
        }

        [Test]
        public void RestorerRejectsUnsupportedWorld()
        {
            GameSaveSnapshot snapshot = CreateSnapshot(0);
            var resolver = new RejectingWorldResolver();
            var catalog = new CatalogResolver(CreateWorld(), Rations, Backpack);

            Assert.Throws<NotSupportedException>(() =>
                GameSaveSnapshotRestorer.Restore(
                    snapshot,
                    resolver,
                    catalog));
        }

        [Test]
        public void RestorerRejectsIncompatibleItemDefinition()
        {
            WorldPopulationSettings world = CreateWorld();
            GameSaveSnapshot snapshot = CreateSnapshot(0, world);
            var incompatibleRations = new ItemDefinition(
                Rations.Id,
                "Changed",
                ItemStateKind.Stackable,
                new EncumbranceValue(501));
            var resolver = new CatalogResolver(
                world,
                incompatibleRations,
                Backpack);

            Assert.Throws<NotSupportedException>(() =>
                GameSaveSnapshotRestorer.Restore(
                    snapshot,
                    resolver,
                    resolver));
        }

        [Test]
        public void RestorerRejectsIncompatibleContainerCapacity()
        {
            WorldPopulationSettings world = CreateWorld();
            GameSaveSnapshot snapshot = CreateSnapshot(0, world);
            var resolver = new CatalogResolver(
                world,
                Rations,
                Backpack,
                capacityOffset: 1);

            Assert.Throws<NotSupportedException>(() =>
                GameSaveSnapshotRestorer.Restore(
                    snapshot,
                    resolver,
                    resolver));
        }

        private static GameSaveSnapshot CreateSnapshot(long tick)
        {
            return CreateSnapshot(tick, CreateWorld());
        }

        private static GameSaveSnapshot CreateSnapshot(
            long tick,
            WorldPopulationSettings world)
        {
            PlayerInventoryState inventory = CreateInventory();
            ChunkLayout layout = world.Generation.ChunkLayout;
            WorldCellCoordinate origin = ChunkAddressing.GetWorldOrigin(
                new ChunkCoordinate(0, 0),
                layout);
            var ground = new GroundContainerMutation(
                new GroundContainerId("ground-cache-1"),
                new ContainerId("ground-container-1"),
                new WorldPosition(origin.X, origin.Y),
                "ground-cache",
                "Ground cache",
                new EncumbranceValue(5000),
                new[]
                {
                    new StoredItemMutation(
                        Rations.Id,
                        ItemStateKind.Stackable,
                        Rations.UnitEncumbrance,
                        2)
                });
            var mutation = new ChunkMutationState(
                new ChunkCoordinate(0, 0),
                layout,
                Array.Empty<HarvestedResourceMutation>(),
                new[] { ground });

            return new GameSaveSnapshot(
                new WorldIdentitySnapshot(
                    world.Generation,
                    world.Profile.Id,
                    world.Profile.Revision),
                tick,
                new WorldPosition(12.5, -3.25),
                new PlayerHealthSnapshot(
                    100,
                    65,
                    tick,
                    checked(tick + 510L)),
                inventory.CaptureSnapshot(),
                new[] { mutation });
        }

        private static WorldPopulationSettings CreateWorld()
        {
            return WorldPopulationDefaults.CreateTemperatePrototypeV1(
                new WorldSeed(0x0123456789ABCDEFUL));
        }

        private static PlayerInventoryState CreateInventory()
        {
            var mainDefinition = new ContainerDefinition(
                "player-main",
                "Player",
                new EncumbranceValue(30000));
            var packDefinition = new ContainerDefinition(
                "backpack-container",
                "Backpack contents",
                new EncumbranceValue(10000));
            var main = new ContainerState(
                new ContainerId("player-main"),
                mainDefinition);
            var pack = new ContainerState(
                new ContainerId("backpack-contents"),
                packDefinition);
            InventoryOperations.AddStack(main, Rations, 3);
            InventoryOperations.AddUnique(
                main,
                Backpack,
                new UniqueItemState(
                    Backpack.Id,
                    new ItemInstanceId("backpack-1"),
                    pack.Id));
            InventoryOperations.AddStack(pack, Rations, 2);

            return new PlayerInventoryState(
                main.Id,
                new[] { Backpack, Rations },
                new[] { pack, main },
                new InventoryEquipmentSnapshot(
                    default,
                    default,
                    new ItemInstanceId("backpack-1")));
        }

        private static byte[] ConvertV3ToLegacyV2(byte[] encoded)
        {
            if (ReadUInt16(encoded, 8) != 3)
            {
                throw new InvalidDataException("Expected a V3 fixture.");
            }

            int healthOffset = GetHealthOffset(encoded);
            int healthLength = GetHealthLength(encoded, healthOffset);
            int foodOffset = healthOffset + healthLength;
            const int foodLength = 4 + 4 + 8 + 8;
            int perishableCountOffset = foodOffset + foodLength;
            if (ReadUInt32(encoded, perishableCountOffset) != 0u)
            {
                throw new InvalidDataException(
                    "Legacy V2 fixture conversion requires no perishable batches.");
            }

            const int perishableEmptyLength = 4;
            int extensionLength = foodLength + perishableEmptyLength;
            int payloadLength = checked((int)ReadUInt32(encoded, 12));
            int legacyPayloadLength = payloadLength - extensionLength;
            var legacy = new byte[
                GameSaveCodecLimits.HeaderLength + legacyPayloadLength];

            Buffer.BlockCopy(encoded, 0, legacy, 0, foodOffset);
            Buffer.BlockCopy(
                encoded,
                foodOffset + extensionLength,
                legacy,
                foodOffset,
                encoded.Length - foodOffset - extensionLength);

            WriteUInt16(legacy, 8, 2);
            WriteUInt32(legacy, 12, checked((uint)legacyPayloadLength));
            RefreshPayloadHash(legacy);
            return legacy;
        }

        private static byte[] ConvertV2ToLegacyV1(byte[] encoded)
        {
            int healthOffset = GetHealthOffset(encoded);
            int healthLength = GetHealthLength(
                encoded,
                healthOffset);
            int payloadLength = checked(
                (int)ReadUInt32(encoded, 12));
            int legacyPayloadLength =
                payloadLength - healthLength;
            var legacy = new byte[
                GameSaveCodecLimits.HeaderLength
                + legacyPayloadLength];

            Buffer.BlockCopy(
                encoded,
                0,
                legacy,
                0,
                healthOffset);
            Buffer.BlockCopy(
                encoded,
                healthOffset + healthLength,
                legacy,
                healthOffset,
                encoded.Length - healthOffset - healthLength);

            WriteUInt16(legacy, 8, 1);
            WriteUInt32(
                legacy,
                12,
                checked((uint)legacyPayloadLength));
            RefreshPayloadHash(legacy);
            return legacy;
        }

        private static int GetHealthOffset(byte[] encoded)
        {
            int payloadStart = GameSaveCodecLimits.HeaderLength;
            int profileLengthOffset =
                payloadStart + 8 + 4 + 4 + 4;
            int profileLength = checked(
                (int)ReadUInt32(encoded, profileLengthOffset));
            return checked(
                profileLengthOffset
                + 4
                + profileLength
                + 4
                + 8
                + 8
                + 8);
        }

        private static int GetHealthLength(
            byte[] encoded,
            int healthOffset)
        {
            int optionalTickOffset =
                healthOffset + 4 + 4 + 8;
            byte hasNextTick = encoded[optionalTickOffset];
            if (hasNextTick > 1)
            {
                throw new InvalidDataException(
                    "Invalid V2 health fixture boolean.");
            }

            return 4 + 4 + 8 + 1
                + (hasNextTick == 1 ? 8 : 0);
        }

        private static void RefreshPayloadHash(byte[] encoded)
        {
            int payloadLength = checked(
                (int)ReadUInt32(encoded, 12));
            var payload = new byte[payloadLength];
            Buffer.BlockCopy(
                encoded,
                GameSaveCodecLimits.HeaderLength,
                payload,
                0,
                payloadLength);

            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(payload);
            }

            Buffer.BlockCopy(
                hash,
                0,
                encoded,
                16,
                GameSaveCodecLimits.HashLength);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset]
                | ((uint)bytes[offset + 1] << 8)
                | ((uint)bytes[offset + 2] << 16)
                | ((uint)bytes[offset + 3] << 24);
        }

        private static void WriteUInt16(
            byte[] bytes,
            int offset,
            ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(
            byte[] bytes,
            int offset,
            int value)
        {
            WriteUInt32(bytes, offset, unchecked((uint)value));
        }

        private static void WriteUInt32(
            byte[] bytes,
            int offset,
            uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private sealed class RejectingWorldResolver
            : IWorldPopulationSettingsResolver
        {
            public bool TryResolve(
                WorldIdentitySnapshot identity,
                out WorldPopulationSettings settings)
            {
                settings = default;
                return false;
            }
        }

        private sealed class CatalogResolver
            : IWorldPopulationSettingsResolver,
              IInventoryDefinitionResolver
        {
            private readonly WorldPopulationSettings _world;
            private readonly Dictionary<ItemDefinitionId, ItemDefinition> _items;
            private readonly int _capacityOffset;

            public CatalogResolver(
                WorldPopulationSettings world,
                ItemDefinition first,
                ItemDefinition second,
                int capacityOffset = 0)
            {
                _world = world;
                _capacityOffset = capacityOffset;
                _items = new Dictionary<ItemDefinitionId, ItemDefinition>
                {
                    { first.Id, first },
                    { second.Id, second }
                };
            }

            public bool TryResolve(
                WorldIdentitySnapshot identity,
                out WorldPopulationSettings settings)
            {
                settings = _world;
                return true;
            }

            public bool TryResolveItemDefinition(
                InventoryDefinitionSnapshot saved,
                out ItemDefinition definition)
            {
                return _items.TryGetValue(saved.Id, out definition);
            }

            public bool TryResolveContainerDefinition(
                InventoryContainerSnapshot saved,
                out ContainerDefinition definition)
            {
                definition = new ContainerDefinition(
                    saved.DefinitionKey,
                    saved.DefinitionKey,
                    new EncumbranceValue(
                        saved.Capacity.Units + _capacityOffset));
                return true;
            }
        }
    }
}
