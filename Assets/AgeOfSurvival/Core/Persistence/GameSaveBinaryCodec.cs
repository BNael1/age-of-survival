using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Persistence
{
    /// <summary>
    /// Deterministic, versioned, in-memory binary codec for canonical saves.
    /// It performs no disk I/O, writes V3, and reads canonical V1/V2/V3 payloads.
    /// </summary>
    public static class GameSaveBinaryCodec
    {
        public const ushort CurrentVersion = 3;
        private const ushort MinimumSupportedVersion = 1;
        private const ushort CurrentFlags = 0;

        private static readonly byte[] Magic =
        {
            (byte)'A', (byte)'O', (byte)'S', (byte)'S',
            (byte)'A', (byte)'V', (byte)'E', 0
        };

        public static byte[] Encode(GameSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            byte[] payload = EncodePayload(snapshot);
            if (payload.Length > GameSaveCodecLimits.MaximumPayloadLength)
            {
                throw Violation(
                    GameSaveCodecViolation.PayloadTooLarge,
                    "The save payload exceeds the configured limit.");
            }

            byte[] hash = ComputeHash(payload);
            using (var writer = new SaveBufferWriter())
            {
                writer.WriteBytes(Magic);
                writer.WriteUInt16(CurrentVersion);
                writer.WriteUInt16(CurrentFlags);
                writer.WriteUInt32(checked((uint)payload.Length));
                writer.WriteBytes(hash);
                writer.WriteBytes(payload);
                byte[] result = writer.ToArray();
                if (result.Length != GameSaveCodecLimits.HeaderLength + payload.Length)
                {
                    throw new InvalidOperationException(
                        "The save envelope length was constructed incorrectly.");
                }

                return result;
            }
        }

        public static GameSaveSnapshot Decode(byte[] data)
        {
            if (data == null)
            {
                throw Violation(
                    GameSaveCodecViolation.InputMissing,
                    "Save bytes are required.");
            }

            if (data.Length < GameSaveCodecLimits.HeaderLength)
            {
                throw Violation(
                    GameSaveCodecViolation.InputTooSmall,
                    "The save is smaller than the required header.");
            }

            var envelope = new SaveBufferReader(data);
            byte[] magic = envelope.ReadBytes(Magic.Length);
            for (int index = 0; index < Magic.Length; index++)
            {
                if (magic[index] != Magic[index])
                {
                    throw Violation(
                        GameSaveCodecViolation.InvalidMagic,
                        "The save magic is invalid.");
                }
            }

            ushort version = envelope.ReadUInt16();
            if (version < MinimumSupportedVersion
                || version > CurrentVersion)
            {
                throw Violation(
                    GameSaveCodecViolation.UnsupportedVersion,
                    $"Save version {version} is not supported.");
            }

            ushort flags = envelope.ReadUInt16();
            if (flags != CurrentFlags)
            {
                throw Violation(
                    GameSaveCodecViolation.UnknownFlags,
                    "The save uses unknown flags.");
            }

            uint rawPayloadLength = envelope.ReadUInt32();
            if (rawPayloadLength > GameSaveCodecLimits.MaximumPayloadLength)
            {
                throw Violation(
                    GameSaveCodecViolation.PayloadTooLarge,
                    "The declared payload exceeds the configured limit.");
            }

            int payloadLength = checked((int)rawPayloadLength);
            if (data.Length != GameSaveCodecLimits.HeaderLength + payloadLength)
            {
                throw Violation(
                    GameSaveCodecViolation.LengthMismatch,
                    "The declared payload length does not match the input.");
            }

            byte[] expectedHash = envelope.ReadBytes(
                GameSaveCodecLimits.HashLength);
            byte[] payload = envelope.ReadBytes(payloadLength);
            envelope.RequireEnd();

            byte[] actualHash = ComputeHash(payload);
            if (!FixedTimeEquals(expectedHash, actualHash))
            {
                throw Violation(
                    GameSaveCodecViolation.IntegrityMismatch,
                    "The save payload hash does not match.");
            }

            return DecodePayload(version, payload);
        }

        private static byte[] EncodePayload(GameSaveSnapshot snapshot)
        {
            ValidateCount(
                snapshot.Inventory.Definitions.Count,
                GameSaveCodecLimits.MaximumDefinitions,
                "Definition count");
            ValidateCount(
                snapshot.Inventory.Containers.Count,
                GameSaveCodecLimits.MaximumPlayerContainers,
                "Player container count");
            ValidateCount(
                snapshot.Perishables.Batches.Count,
                GameSaveCodecLimits.MaximumPerishableBatches,
                "Perishable batch count");
            ValidateCount(
                snapshot.ChunkMutations.Count,
                GameSaveCodecLimits.MaximumChunkMutations,
                "Chunk mutation count");

            using (var writer = new SaveBufferWriter(
                GameSaveCodecLimits.MaximumPayloadLength))
            {
                WriteWorld(writer, snapshot.World);
                writer.WriteInt64(snapshot.FixedTick);
                writer.WriteDouble(snapshot.PlayerPosition.X);
                writer.WriteDouble(snapshot.PlayerPosition.Y);
                WriteHealth(writer, snapshot.Health);
                WriteFood(writer, snapshot.Food);
                WritePerishables(writer, snapshot.Perishables);
                WriteInventory(writer, snapshot.Inventory);
                WriteChunks(writer, snapshot.ChunkMutations);
                return writer.ToArray();
            }
        }

        private static GameSaveSnapshot DecodePayload(
            ushort version,
            byte[] payload)
        {
            var reader = new SaveBufferReader(payload);
            try
            {
                WorldIdentitySnapshot world = ReadWorld(reader);
                long fixedTick = reader.ReadInt64();
                var playerPosition = new WorldPosition(
                    reader.ReadDouble(),
                    reader.ReadDouble());
                PlayerHealthSnapshot health = version == 1
                    ? new PlayerHealthSnapshot(
                        PlayerHealthRules.DefaultMaximumHealth,
                        PlayerHealthRules.DefaultMaximumHealth,
                        fixedTick,
                        null)
                    : ReadHealth(reader);
                PlayerFoodSnapshot food = version >= 3
                    ? ReadFood(reader)
                    : PlayerFoodSnapshot.CreateFull(fixedTick);
                PerishableInventorySnapshot perishables = version >= 3
                    ? ReadPerishables(reader)
                    : PerishableInventorySnapshot.Empty;
                PlayerInventorySnapshot inventory = ReadInventory(reader);
                IReadOnlyList<ChunkMutationState> mutations =
                    ReadChunks(reader, world.Generation.ChunkLayout);
                reader.RequireEnd();
                return new GameSaveSnapshot(
                    world,
                    fixedTick,
                    playerPosition,
                    health,
                    food,
                    perishables,
                    inventory,
                    mutations);
            }
            catch (GameSaveCodecException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidDomainValue,
                    "The payload violates a Core save invariant.",
                    exception);
            }
        }

        private static void WriteWorld(
            SaveBufferWriter writer,
            WorldIdentitySnapshot world)
        {
            writer.WriteUInt64(world.Generation.Seed.Value);
            writer.WriteInt32(world.Generation.Version.Value);
            writer.WriteInt32(world.Generation.ChunkLayout.Width);
            writer.WriteInt32(world.Generation.ChunkLayout.Height);
            writer.WriteRequiredString(world.PopulationProfileId.Value);
            writer.WriteInt32(world.PopulationRevision);
        }

        private static WorldIdentitySnapshot ReadWorld(SaveBufferReader reader)
        {
            var generation = new WorldGenerationSettings(
                new WorldSeed(reader.ReadUInt64()),
                new WorldGeneratorVersion(reader.ReadInt32()),
                new ChunkLayout(reader.ReadInt32(), reader.ReadInt32()));
            return new WorldIdentitySnapshot(
                generation,
                new WorldPopulationProfileId(reader.ReadRequiredString()),
                reader.ReadInt32());
        }

        private static void WriteHealth(
            SaveBufferWriter writer,
            PlayerHealthSnapshot health)
        {
            writer.WriteInt32(health.MaximumHealth);
            writer.WriteInt32(health.CurrentHealth);
            writer.WriteInt64(health.CurrentTick);
            writer.WriteBoolean(
                health.NextRegenerationTick.HasValue);
            if (health.NextRegenerationTick.HasValue)
            {
                writer.WriteInt64(
                    health.NextRegenerationTick.Value);
            }
        }

        private static PlayerHealthSnapshot ReadHealth(
            SaveBufferReader reader)
        {
            int maximumHealth = reader.ReadInt32();
            int currentHealth = reader.ReadInt32();
            long currentTick = reader.ReadInt64();
            long? nextRegenerationTick = reader.ReadBoolean()
                ? reader.ReadInt64()
                : (long?)null;

            return new PlayerHealthSnapshot(
                maximumHealth,
                currentHealth,
                currentTick,
                nextRegenerationTick);
        }

        private static void WriteFood(
            SaveBufferWriter writer,
            PlayerFoodSnapshot food)
        {
            writer.WriteInt32(food.MaximumSatiety);
            writer.WriteInt32(food.CurrentSatiety);
            writer.WriteInt64(food.CurrentTick);
            writer.WriteInt64(food.NextSatietyLossTick);
        }

        private static PlayerFoodSnapshot ReadFood(SaveBufferReader reader)
        {
            return new PlayerFoodSnapshot(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt64(),
                reader.ReadInt64());
        }

        private static void WritePerishables(
            SaveBufferWriter writer,
            PerishableInventorySnapshot perishables)
        {
            writer.WriteUInt32(checked((uint)perishables.Batches.Count));
            for (int index = 0; index < perishables.Batches.Count; index++)
            {
                PerishableBatchSnapshot batch = perishables.Batches[index];
                writer.WriteRequiredString(batch.Id.Value);
                writer.WriteRequiredString(batch.ContainerId.Value);
                writer.WriteRequiredString(batch.DefinitionId.Value);
                writer.WriteInt32(batch.Quantity);
                writer.WriteInt64(batch.AccumulatedDecayMilliTicks);
                writer.WriteInt64(batch.LastEvaluatedTick);
            }
        }

        private static PerishableInventorySnapshot ReadPerishables(SaveBufferReader reader)
        {
            int count = reader.ReadCount(GameSaveCodecLimits.MaximumPerishableBatches, "Perishable batch count");
            var batches = new List<PerishableBatchSnapshot>(count);
            PerishableBatchSnapshot previous = default;
            for (int index = 0; index < count; index++)
            {
                PerishableBatchSnapshot batch = PerishableBatchSnapshot.Restore(
                    new FoodBatchId(reader.ReadRequiredString()),
                    new ContainerId(reader.ReadRequiredString()),
                    new ItemDefinitionId(reader.ReadRequiredString()),
                    reader.ReadInt32(),
                    reader.ReadInt64(),
                    reader.ReadInt64());
                if (index > 0)
                {
                    RequireCanonicalComparison(
                        PerishableBatchSnapshot.Compare(previous, batch),
                        "Perishable batches");
                }
                previous = batch;
                batches.Add(batch);
            }
            return PerishableInventorySnapshot.Restore(batches);
        }

        private static void WriteInventory(
            SaveBufferWriter writer,
            PlayerInventorySnapshot inventory)
        {
            writer.WriteRequiredString(inventory.MainContainerId.Value);

            writer.WriteUInt32(checked((uint)inventory.Definitions.Count));
            for (int index = 0; index < inventory.Definitions.Count; index++)
            {
                InventoryDefinitionSnapshot definition =
                    inventory.Definitions[index];
                writer.WriteRequiredString(definition.Id.Value);
                writer.WriteByte((byte)definition.StateKind);
                writer.WriteInt64(definition.UnitEncumbrance.Units);
                writer.WriteBoolean(definition.HasEquipment);
                writer.WriteByte((byte)definition.CompatibleSlots);
                writer.WriteInt32(
                    definition.ContainedContainerReductionPercent);
            }

            writer.WriteUInt32(checked((uint)inventory.Containers.Count));
            for (int containerIndex = 0;
                 containerIndex < inventory.Containers.Count;
                 containerIndex++)
            {
                InventoryContainerSnapshot container =
                    inventory.Containers[containerIndex];
                ValidateCount(
                    container.Entries.Count,
                    GameSaveCodecLimits.MaximumEntriesPerContainer,
                    "Inventory entry count");
                writer.WriteRequiredString(container.Id.Value);
                writer.WriteRequiredString(container.DefinitionKey);
                writer.WriteInt64(container.Capacity.Units);
                writer.WriteUInt32(checked((uint)container.Entries.Count));

                for (int entryIndex = 0;
                     entryIndex < container.Entries.Count;
                     entryIndex++)
                {
                    InventoryEntrySnapshot entry =
                        container.Entries[entryIndex];
                    writer.WriteByte((byte)entry.Kind);
                    writer.WriteRequiredString(entry.DefinitionId.Value);
                    writer.WriteInt32(entry.Quantity);
                    writer.WriteOptionalString(entry.InstanceId.Value);
                    writer.WriteOptionalString(
                        entry.ContainedContainerId.Value);
                }
            }

            writer.WriteOptionalString(inventory.Equipment.LeftHand.Value);
            writer.WriteOptionalString(inventory.Equipment.RightHand.Value);
            writer.WriteOptionalString(inventory.Equipment.Back.Value);
        }

        private static PlayerInventorySnapshot ReadInventory(
            SaveBufferReader reader)
        {
            var mainContainerId = new ContainerId(reader.ReadRequiredString());

            int definitionCount = reader.ReadCount(
                GameSaveCodecLimits.MaximumDefinitions,
                "Definition count");
            var definitions =
                new List<InventoryDefinitionSnapshot>(definitionCount);
            ItemDefinitionId previousDefinitionId = default;
            for (int index = 0; index < definitionCount; index++)
            {
                var definition = InventoryDefinitionSnapshot.Restore(
                    new ItemDefinitionId(reader.ReadRequiredString()),
                    ReadItemStateKind(reader),
                    new EncumbranceValue(reader.ReadInt64()),
                    reader.ReadBoolean(),
                    ReadEquipmentSlotMask(reader),
                    reader.ReadInt32());
                if (index > 0)
                {
                    RequireCanonicalComparison(
                        previousDefinitionId.CompareTo(definition.Id),
                        "Inventory definitions");
                }

                previousDefinitionId = definition.Id;
                definitions.Add(definition);
            }

            int containerCount = reader.ReadCount(
                GameSaveCodecLimits.MaximumPlayerContainers,
                "Player container count");
            var containers =
                new List<InventoryContainerSnapshot>(containerCount);
            ContainerId previousContainerId = default;
            for (int containerIndex = 0;
                 containerIndex < containerCount;
                 containerIndex++)
            {
                var id = new ContainerId(reader.ReadRequiredString());
                if (containerIndex > 0)
                {
                    RequireCanonicalComparison(
                        previousContainerId.CompareTo(id),
                        "Player containers");
                }

                previousContainerId = id;
                string definitionKey = reader.ReadRequiredString();
                var capacity = new EncumbranceValue(reader.ReadInt64());
                int entryCount = reader.ReadCount(
                    GameSaveCodecLimits.MaximumEntriesPerContainer,
                    "Inventory entry count");
                var entries =
                    new List<InventoryEntrySnapshot>(entryCount);
                InventoryEntrySnapshot previousEntry = default;
                for (int entryIndex = 0;
                     entryIndex < entryCount;
                     entryIndex++)
                {
                    ItemStateKind kind = ReadItemStateKind(reader);
                    var definitionId =
                        new ItemDefinitionId(reader.ReadRequiredString());
                    int quantity = reader.ReadInt32();
                    string instance = reader.ReadOptionalString();
                    string contained = reader.ReadOptionalString();
                    InventoryEntrySnapshot entry =
                        InventoryEntrySnapshot.Restore(
                            kind,
                            definitionId,
                            quantity,
                            string.IsNullOrEmpty(instance)
                                ? default
                                : new ItemInstanceId(instance),
                            string.IsNullOrEmpty(contained)
                                ? default
                                : new ContainerId(contained));
                    if (entryIndex > 0)
                    {
                        RequireCanonicalComparison(
                            InventoryEntrySnapshot.Compare(
                                previousEntry,
                                entry),
                            "Inventory entries");
                    }

                    previousEntry = entry;
                    entries.Add(entry);
                }

                containers.Add(InventoryContainerSnapshot.Restore(
                    id,
                    definitionKey,
                    capacity,
                    entries));
            }

            var equipment = new InventoryEquipmentSnapshot(
                ReadOptionalItemInstanceId(reader),
                ReadOptionalItemInstanceId(reader),
                ReadOptionalItemInstanceId(reader));

            return PlayerInventorySnapshot.Restore(
                mainContainerId,
                definitions,
                containers,
                equipment);
        }

        private static void WriteChunks(
            SaveBufferWriter writer,
            IReadOnlyList<ChunkMutationState> chunks)
        {
            writer.WriteUInt32(checked((uint)chunks.Count));
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                ChunkMutationState chunk = chunks[chunkIndex];
                ValidateCount(
                    chunk.HarvestedResources.Count,
                    GameSaveCodecLimits.MaximumHarvestedResourcesPerChunk,
                    "Harvested resource count");
                ValidateCount(
                    chunk.GroundContainers.Count,
                    GameSaveCodecLimits.MaximumGroundContainersPerChunk,
                    "Ground container count");

                writer.WriteInt64(chunk.Coordinate.X);
                writer.WriteInt64(chunk.Coordinate.Y);

                writer.WriteUInt32(
                    checked((uint)chunk.HarvestedResources.Count));
                for (int index = 0;
                     index < chunk.HarvestedResources.Count;
                     index++)
                {
                    HarvestedResourceMutation resource =
                        chunk.HarvestedResources[index];
                    writer.WriteRequiredString(resource.Id.Value);
                    writer.WriteDouble(resource.Position.X);
                    writer.WriteDouble(resource.Position.Y);
                }

                writer.WriteUInt32(
                    checked((uint)chunk.GroundContainers.Count));
                for (int index = 0;
                     index < chunk.GroundContainers.Count;
                     index++)
                {
                    WriteGroundContainer(
                        writer,
                        chunk.GroundContainers[index]);
                }
            }
        }

        private static IReadOnlyList<ChunkMutationState> ReadChunks(
            SaveBufferReader reader,
            ChunkLayout layout)
        {
            int chunkCount = reader.ReadCount(
                GameSaveCodecLimits.MaximumChunkMutations,
                "Chunk mutation count");
            var result = new List<ChunkMutationState>(chunkCount);
            ChunkCoordinate previousCoordinate = default;

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var coordinate = new ChunkCoordinate(
                    reader.ReadInt64(),
                    reader.ReadInt64());
                if (chunkIndex > 0)
                {
                    RequireCanonicalComparison(
                        previousCoordinate.CompareTo(coordinate),
                        "Chunk mutations");
                }

                previousCoordinate = coordinate;

                int harvestedCount = reader.ReadCount(
                    GameSaveCodecLimits.MaximumHarvestedResourcesPerChunk,
                    "Harvested resource count");
                var harvested =
                    new List<HarvestedResourceMutation>(harvestedCount);
                ResourceId previousResourceId = default;
                for (int index = 0; index < harvestedCount; index++)
                {
                    var resource = new HarvestedResourceMutation(
                        new ResourceId(reader.ReadRequiredString()),
                        new WorldPosition(
                            reader.ReadDouble(),
                            reader.ReadDouble()));
                    if (index > 0)
                    {
                        RequireCanonicalComparison(
                            previousResourceId.CompareTo(resource.Id),
                            "Harvested resources");
                    }

                    previousResourceId = resource.Id;
                    harvested.Add(resource);
                }

                int groundCount = reader.ReadCount(
                    GameSaveCodecLimits.MaximumGroundContainersPerChunk,
                    "Ground container count");
                var ground =
                    new List<GroundContainerMutation>(groundCount);
                GroundContainerId previousGroundId = default;
                for (int index = 0; index < groundCount; index++)
                {
                    GroundContainerMutation container =
                        ReadGroundContainer(reader);
                    if (index > 0)
                    {
                        RequireCanonicalComparison(
                            previousGroundId.CompareTo(container.Id),
                            "Ground containers");
                    }

                    previousGroundId = container.Id;
                    ground.Add(container);
                }

                result.Add(new ChunkMutationState(
                    coordinate,
                    layout,
                    harvested,
                    ground));
            }

            return result.AsReadOnly();
        }

        private static void WriteGroundContainer(
            SaveBufferWriter writer,
            GroundContainerMutation ground)
        {
            ValidateCount(
                ground.Items.Count,
                GameSaveCodecLimits.MaximumGroundItemsPerContainer,
                "Ground item count");
            writer.WriteRequiredString(ground.Id.Value);
            writer.WriteRequiredString(ground.ContainerId.Value);
            writer.WriteDouble(ground.Position.X);
            writer.WriteDouble(ground.Position.Y);
            writer.WriteRequiredString(ground.DefinitionKey);
            writer.WriteRequiredString(ground.DisplayName);
            writer.WriteInt64(ground.Capacity.Units);
            writer.WriteUInt32(checked((uint)ground.Items.Count));

            for (int index = 0; index < ground.Items.Count; index++)
            {
                StoredItemMutation item = ground.Items[index];
                writer.WriteRequiredString(item.DefinitionId.Value);
                writer.WriteByte((byte)item.Kind);
                writer.WriteInt64(item.UnitEncumbrance.Units);
                writer.WriteInt32(item.Quantity);
                writer.WriteOptionalString(item.InstanceId.Value);
                writer.WriteOptionalString(item.ContainedContainerId.Value);
            }
        }

        private static GroundContainerMutation ReadGroundContainer(
            SaveBufferReader reader)
        {
            var id = new GroundContainerId(reader.ReadRequiredString());
            var containerId =
                new ContainerId(reader.ReadRequiredString());
            var position = new WorldPosition(
                reader.ReadDouble(),
                reader.ReadDouble());
            string definitionKey = reader.ReadRequiredString();
            string displayName = reader.ReadRequiredString();
            var capacity = new EncumbranceValue(reader.ReadInt64());

            int itemCount = reader.ReadCount(
                GameSaveCodecLimits.MaximumGroundItemsPerContainer,
                "Ground item count");
            var items = new List<StoredItemMutation>(itemCount);
            StoredItemMutation previousItem = null;
            for (int index = 0; index < itemCount; index++)
            {
                var definitionId =
                    new ItemDefinitionId(reader.ReadRequiredString());
                ItemStateKind kind = ReadItemStateKind(reader);
                var unitEncumbrance =
                    new EncumbranceValue(reader.ReadInt64());
                int quantity = reader.ReadInt32();
                string instance = reader.ReadOptionalString();
                string contained = reader.ReadOptionalString();

                var item = new StoredItemMutation(
                    definitionId,
                    kind,
                    unitEncumbrance,
                    quantity,
                    string.IsNullOrEmpty(instance)
                        ? default
                        : new ItemInstanceId(instance),
                    string.IsNullOrEmpty(contained)
                        ? default
                        : new ContainerId(contained));
                if (index > 0)
                {
                    RequireCanonicalComparison(
                        CompareStoredItems(previousItem, item),
                        "Ground items");
                }

                previousItem = item;
                items.Add(item);
            }

            return new GroundContainerMutation(
                id,
                containerId,
                position,
                definitionKey,
                displayName,
                capacity,
                items);
        }

        private static ItemStateKind ReadItemStateKind(
            SaveBufferReader reader)
        {
            byte raw = reader.ReadByte();
            if (raw > (byte)ItemStateKind.Unique)
            {
                throw Violation(
                    GameSaveCodecViolation.UnknownEnumValue,
                    "Unknown item state kind.");
            }

            return (ItemStateKind)raw;
        }

        private static EquipmentSlotMask ReadEquipmentSlotMask(
            SaveBufferReader reader)
        {
            byte raw = reader.ReadByte();
            const byte all =
                (byte)(EquipmentSlotMask.LeftHand
                    | EquipmentSlotMask.RightHand
                    | EquipmentSlotMask.Back);
            if ((raw & ~all) != 0)
            {
                throw Violation(
                    GameSaveCodecViolation.UnknownEnumValue,
                    "Unknown equipment slot mask.");
            }

            return (EquipmentSlotMask)raw;
        }

        private static ItemInstanceId ReadOptionalItemInstanceId(
            SaveBufferReader reader)
        {
            string value = reader.ReadOptionalString();
            return string.IsNullOrEmpty(value)
                ? default
                : new ItemInstanceId(value);
        }

        private static int CompareStoredItems(
            StoredItemMutation left,
            StoredItemMutation right)
        {
            int comparison = left.DefinitionId.CompareTo(right.DefinitionId);
            if (comparison != 0) return comparison;
            comparison = ((int)left.Kind).CompareTo((int)right.Kind);
            if (comparison != 0) return comparison;
            return left.InstanceId.CompareTo(right.InstanceId);
        }

        private static void RequireCanonicalComparison(
            int comparison,
            string label)
        {
            if (comparison == 0)
            {
                throw Violation(
                    GameSaveCodecViolation.DuplicateIdentity,
                    $"{label} contain a duplicate identity.");
            }

            if (comparison > 0)
            {
                throw Violation(
                    GameSaveCodecViolation.NonCanonicalOrder,
                    $"{label} are not in canonical order.");
            }
        }

        private static void ValidateCount(
            int count,
            int maximum,
            string label)
        {
            if (count < 0 || count > maximum)
            {
                throw Violation(
                    GameSaveCodecViolation.CountLimitExceeded,
                    $"{label} exceeds the configured limit.");
            }
        }

        private static byte[] ComputeHash(byte[] payload)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(payload);
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        private static GameSaveCodecException Violation(
            GameSaveCodecViolation violation,
            string message,
            Exception innerException = null)
        {
            return new GameSaveCodecException(
                violation,
                message,
                innerException);
        }
    }
}
