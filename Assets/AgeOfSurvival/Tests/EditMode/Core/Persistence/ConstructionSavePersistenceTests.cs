using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Core.Shelter;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Persistence
{
    public sealed class ConstructionSavePersistenceTests
    {
        private static readonly ItemDefinition Branches = Item("branches");
        private static readonly ItemDefinition Stones = Item("stones");
        private static readonly ItemDefinition Wood = Item("wood");

        private static readonly ConstructionDefinitionId FloorId =
            new ConstructionDefinitionId("prototype.floor.basic");
        private static readonly ConstructionDefinitionId WallId =
            new ConstructionDefinitionId("prototype.wall.basic");
        private static readonly ConstructionDefinitionId RoofId =
            new ConstructionDefinitionId("synthetic.roof");

        [Test]
        public void V5RoundTripPreservesMultipleHistoriesAndIsByteDeterministic()
        {
            GameSaveSnapshot baseline = CreateGameSnapshot(ConstructionSaveSnapshot.Empty, tick: 17L);
            ShelterHomeState homes = ShelterHomeState.CreateInitialCamp(
                new ShelterId("world-region:camp"));
            ShelterFamiliarityState second = homes.GetOrCreate(
                new ShelterId("persistent-area:second"));
            second.RecordCompletedRest();
            var snapshot = new GameSaveSnapshot(
                baseline.World, baseline.FixedTick, baseline.PlayerPosition,
                baseline.Health, baseline.Food, baseline.Perishables,
                baseline.Inventory, baseline.ChunkMutations, baseline.Construction,
                ShelterSaveSnapshot.Capture(homes));

            byte[] bytes = GameSaveBinaryCodec.Encode(snapshot);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(bytes);
            ShelterHomeState restored = decoded.Shelters.RestoreState();

            Assert.That(ReadUInt16(bytes, 8), Is.EqualTo(5));
            Assert.That(decoded.Shelters.Histories.Select(value => value.ShelterId),
                Is.EqualTo(new[]
                {
                    new ShelterId("persistent-area:second"),
                    new ShelterId("world-region:camp")
                }));
            Assert.That(restored.PrimaryShelterId, Is.EqualTo(new ShelterId("world-region:camp")));
            Assert.That(
                restored.CaptureCanonicalHistories().Single(
                    value => value.ShelterId.Equals(restored.PrimaryShelterId))
                    .FamiliarityHalfPoints,
                Is.EqualTo(140));
            Assert.That(GameSaveBinaryCodec.Encode(decoded), Is.EqualTo(bytes));
        }

        [Test]
        public void V5RejectsTruncatedShelterPayloadAndExcessiveCount()
        {
            byte[] encoded = EncodeShelters(
                new[] { History("a") },
                false,
                default);
            var truncated = new byte[encoded.Length - 1];
            Buffer.BlockCopy(encoded, 0, truncated, 0, truncated.Length);
            WriteUInt32(
                truncated,
                12,
                checked((uint)(truncated.Length - GameSaveCodecLimits.HeaderLength)));
            RefreshPayloadHash(truncated);
            AssertCodecViolation(truncated, GameSaveCodecViolation.UnexpectedEnd);

            byte[] excessive = GameSaveBinaryCodec.Encode(
                CreateGameSnapshot(ConstructionSaveSnapshot.Empty));
            int section = excessive.Length - 5;
            WriteUInt32(
                excessive,
                section,
                checked((uint)GameSaveCodecLimits.MaximumShelterHistories + 1U));
            RefreshPayloadHash(excessive);
            AssertCodecViolation(excessive, GameSaveCodecViolation.CountLimitExceeded);
        }

        [Test]
        public void V5RejectsInvalidNonCanonicalAndDuplicateShelterIds()
        {
            byte[] invalid = EncodeShelters(
                new[] { History("a") }, false, default);
            int invalidSection = ShelterSectionOffset(invalid, new[] { "a" }, null);
            WriteUInt32(invalid, invalidSection + 4, 0U);
            RefreshPayloadHash(invalid);
            AssertCodecViolation(invalid, GameSaveCodecViolation.InvalidStringLength);

            byte[] nonCanonical = EncodeShelters(
                new[] { History("a"), History("b") }, false, default);
            int section = ShelterSectionOffset(nonCanonical, new[] { "a", "b" }, null);
            int firstIdByte = section + 8;
            int secondEntry = section + 4 + ShelterHistoryBinaryLength("a");
            int secondIdByte = secondEntry + 4;
            nonCanonical[firstIdByte] = (byte)'b';
            nonCanonical[secondIdByte] = (byte)'a';
            RefreshPayloadHash(nonCanonical);
            AssertCodecViolation(nonCanonical, GameSaveCodecViolation.NonCanonicalOrder);

            byte[] duplicate = EncodeShelters(
                new[] { History("a"), History("b") }, false, default);
            section = ShelterSectionOffset(duplicate, new[] { "a", "b" }, null);
            secondEntry = section + 4 + ShelterHistoryBinaryLength("a");
            duplicate[secondEntry + 4] = (byte)'a';
            RefreshPayloadHash(duplicate);
            AssertCodecViolation(duplicate, GameSaveCodecViolation.DuplicateIdentity);
        }

        [Test]
        public void V5RejectsInvalidShelterHistoryNumericFields()
        {
            AssertInvalidShelterInt64(HistoryTotalOffset("a"), -1L);
            AssertInvalidShelterInt64(HistoryProgressOffset("a"), -1L);
            AssertInvalidShelterInt64(HistoryProgressOffset("a"), 11L);
            AssertInvalidShelterInt32(HistoryRestOffset("a"), -1);
            AssertInvalidShelterInt32(HistoryNightOffset("a"), -1);
            AssertInvalidShelterInt32(HistoryFamiliarityOffset("a"), -1);
            AssertInvalidShelterInt32(HistoryFamiliarityOffset("a"), 201);
        }

        [Test]
        public void V5RejectsInvalidOrUnknownPrimaryShelter()
        {
            byte[] missingId = GameSaveBinaryCodec.Encode(
                CreateGameSnapshot(ConstructionSaveSnapshot.Empty));
            missingId[missingId.Length - 1] = 1;
            RefreshPayloadHash(missingId);
            AssertCodecViolation(missingId, GameSaveCodecViolation.UnexpectedEnd);

            byte[] unknown = EncodeShelters(
                new[] { History("a") }, true, new ShelterId("a"));
            int section = ShelterSectionOffset(unknown, new[] { "a" }, "a");
            int primaryByte = section + 4 + ShelterHistoryBinaryLength("a") + 1 + 4;
            unknown[primaryByte] = (byte)'b';
            RefreshPayloadHash(unknown);
            AssertCodecViolation(unknown, GameSaveCodecViolation.InvalidDomainValue);
        }

        [Test]
        public void LegacyV4MigratesToEmptyShelterHistoryWithoutRewritingSource()
        {
            byte[] v5 = GameSaveBinaryCodec.Encode(CreateGameSnapshot(ConstructionSaveSnapshot.Empty));
            byte[] v4 = ConvertEmptyV5ToV4(v5);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(v4);

            Assert.That(ReadUInt16(v4, 8), Is.EqualTo(4));
            Assert.That(decoded.Shelters.Histories, Is.Empty);
            Assert.That(decoded.Shelters.HasPrimaryShelter, Is.False);
            Assert.That(ReadUInt16(v4, 8), Is.EqualTo(4));
        }

        [Test]
        public void V5ReloadDoesNotInventAFirstPrimaryHome()
        {
            GameSaveSnapshot baseline = CreateGameSnapshot(ConstructionSaveSnapshot.Empty);
            var a = new ShelterFamiliarityState(new ShelterId("a"), 0, 0, 0, 3, 80);
            var b = new ShelterFamiliarityState(new ShelterId("b"), 0, 0, 0, 3, 80);
            var homes = new ShelterHomeState(new[] { b, a });
            var snapshot = new GameSaveSnapshot(
                baseline.World, baseline.FixedTick, baseline.PlayerPosition,
                baseline.Health, baseline.Food, baseline.Perishables,
                baseline.Inventory, baseline.ChunkMutations, baseline.Construction,
                ShelterSaveSnapshot.Capture(homes));

            ShelterHomeState restored = GameSaveBinaryCodec.Decode(
                GameSaveBinaryCodec.Encode(snapshot)).Shelters.RestoreState();
            Assert.That(
                restored.RecalculatePrimary(new[] { b.ShelterId, a.ShelterId }),
                Is.False);
            Assert.That(restored.HasPrimaryShelter, Is.False);
        }

        [Test]
        public void ShelterSnapshotRejectsDuplicatesAndCanonicalizesOrder()
        {
            var a = new ShelterHistorySnapshot(new ShelterId("a"), 0, 0, 0, 0, 0);
            var b = new ShelterHistorySnapshot(new ShelterId("b"), 0, 0, 0, 0, 0);
            Assert.That(new ShelterSaveSnapshot(new[] { b, a }, false, default)
                .Histories.Select(value => value.ShelterId), Is.EqualTo(new[] { a.ShelterId, b.ShelterId }));
            Assert.Throws<ArgumentException>(() => new ShelterSaveSnapshot(new[] { a, a }, false, default));
            Assert.Throws<ArgumentException>(() => new ShelterSaveSnapshot(new[] { a }, true, b.ShelterId));
        }

        [Test]
        public void EmptyV5RoundTripPreservesEmptyConstructionAndPriorData()
        {
            GameSaveSnapshot original = CreateGameSnapshot(
                ConstructionSaveSnapshot.Empty,
                tick: 17L);

            byte[] bytes = GameSaveBinaryCodec.Encode(original);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(bytes);

            Assert.That(ReadUInt16(bytes, 8), Is.EqualTo(5));
            Assert.That(decoded.Construction.Sites, Is.Empty);
            Assert.That(decoded.Construction.Structures, Is.Empty);
            Assert.That(decoded.Construction.NextInstanceSequence, Is.EqualTo(1L));
            Assert.That(decoded.FixedTick, Is.EqualTo(17L));
            Assert.That(decoded.Health.CurrentHealth, Is.EqualTo(73));
            Assert.That(decoded.Food.CurrentSatiety, Is.EqualTo(61));
            Assert.That(decoded.Inventory.Containers.Count, Is.EqualTo(1));
            Assert.That(decoded.World, Is.EqualTo(original.World));
            Assert.That(GameSaveBinaryCodec.Encode(decoded), Is.EqualTo(bytes));
        }

        [Test]
        public void LegacyV3MigratesToEmptyConstructionInMemory()
        {
            byte[] current = GameSaveBinaryCodec.Encode(CreateGameSnapshot(
                ConstructionSaveSnapshot.Empty));
            byte[] v3 = ConvertEmptyCurrentToV3(current);

            GameSaveSnapshot migrated = GameSaveBinaryCodec.Decode(v3);

            Assert.That(ReadUInt16(v3, 8), Is.EqualTo(3));
            Assert.That(migrated.Construction.Sites, Is.Empty);
            Assert.That(migrated.Construction.Structures, Is.Empty);
            Assert.That(migrated.Construction.NextInstanceSequence, Is.EqualTo(1L));
            Assert.That(ReadUInt16(v3, 8), Is.EqualTo(3),
                "Decoding must not rewrite the source bytes.");
        }

        [Test]
        public void EmptySiteRoundTripPreservesIdentityTopologyAndZeroProgress()
        {
            ConstructionSaveSnapshot saved = SnapshotWithSite(
                "local-prototype:0000000001",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(4, -2)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0,
                2L);

            RestoredConstructionState restored = RoundTripAndRestore(saved);
            ConstructionSiteState site = RequireSite(
                restored.World,
                "local-prototype:0000000001");

            Assert.That(site.DefinitionId, Is.EqualTo(FloorId));
            Assert.That(site.Space, Is.EqualTo(
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(4, -2))));
            Assert.That(site.WorkCompletedUnits, Is.Zero);
            Assert.That(restored.World.CaptureDepositedMaterials(site.InstanceId), Is.Empty);
        }

        [Test]
        public void PartialMaterialsRoundTripExactlyWithoutInventoryRefund()
        {
            ConstructionSaveSnapshot saved = SnapshotWithSite(
                "local-prototype:0000000002",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(1, 2)),
                new[] { Material(Branches.Id, 1) },
                0,
                3L);
            GameSaveSnapshot game = CreateGameSnapshot(saved);
            int inventoryBefore = InventoryQuantity(game.Inventory, Branches.Id);

            RestoredGameState restored = Restore(GameSaveBinaryCodec.Decode(
                GameSaveBinaryCodec.Encode(game)));
            ConstructionMaterialQuantity[] deposited =
                restored.Construction.World.CaptureDepositedMaterials(
                    new ConstructionInstanceId("local-prototype:0000000002"));

            Assert.That(deposited.Length, Is.EqualTo(1));
            Assert.That(deposited[0].DefinitionId, Is.EqualTo(Branches.Id));
            Assert.That(deposited[0].Quantity, Is.EqualTo(1));
            Assert.That(
                InventoryOperations.Count(restored.Inventory.MainContainer, Branches.Id),
                Is.EqualTo(inventoryBefore));
        }

        [Test]
        public void CompleteMaterialsWithZeroWorkRoundTripAsSite()
        {
            ConstructionSaveSnapshot saved = SnapshotWithSite(
                "local-prototype:0000000003",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(3, 3)),
                new[] { Material(Branches.Id, 2), Material(Stones.Id, 1) },
                0,
                4L);

            ConstructionSiteState site = RequireSite(
                RoundTripAndRestore(saved).World,
                "local-prototype:0000000003");

            Assert.That(site.WorkCompletedUnits, Is.Zero);
            Assert.That(site.DefinitionId, Is.EqualTo(FloorId));
        }

        [Test]
        public void PartialWorkRoundTripExactly()
        {
            ConstructionSaveSnapshot saved = SnapshotWithSite(
                "local-prototype:0000000004",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(-8, 5)),
                new[] { Material(Branches.Id, 2), Material(Stones.Id, 1) },
                19,
                5L);

            ConstructionSiteState site = RequireSite(
                RoundTripAndRestore(saved).World,
                "local-prototype:0000000004");

            Assert.That(site.WorkCompletedUnits, Is.EqualTo(19));
        }

        [Test]
        public void CompletedSurfaceAndEdgeRoundTripWithCanonicalTopology()
        {
            var surfaceId = new ConstructionInstanceId(
                "local-prototype:0000000005");
            var edgeId = new ConstructionInstanceId(
                "local-prototype:0000000006");
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(-1, -1));
            ConstructionSpaceKey edge = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(7, 9),
                ConstructionCellSide.West);
            var saved = new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                7L,
                Array.Empty<ConstructionSiteSnapshot>(),
                new[]
                {
                    Structure(edgeId, WallId, edge),
                    Structure(surfaceId, FloorId, surface)
                });

            RestoredConstructionState restored = RoundTripAndRestore(saved);

            Assert.That(restored.World.TryFindStructure(surfaceId, out CompletedStructureState floor), Is.True);
            Assert.That(restored.World.TryFindStructure(edgeId, out CompletedStructureState wall), Is.True);
            Assert.That(floor.Space, Is.EqualTo(surface));
            Assert.That(wall.Space, Is.EqualTo(edge));
            Assert.That(restored.World.OccupiedSpaceCount, Is.EqualTo(2));
        }

        [Test]
        public void V5RoundTrip_RebuildsTheSameDerivedBoundedRoofSupport()
        {
            var saved = new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                "synthetic.support.catalog",
                1,
                "support",
                6L,
                Array.Empty<ConstructionSiteSnapshot>(),
                new[]
                {
                    Structure(
                        new ConstructionInstanceId("support:0000000001"),
                        WallId,
                        ConstructionSpaceKey.Edge(
                            new WorldCellCoordinate(0L, 0L),
                            ConstructionCellSide.West)),
                    Structure(
                        new ConstructionInstanceId("support:0000000002"),
                        RoofId,
                        ConstructionSpaceKey.Roof(new WorldCellCoordinate(0L, 0L))),
                    Structure(
                        new ConstructionInstanceId("support:0000000003"),
                        RoofId,
                        ConstructionSpaceKey.Roof(new WorldCellCoordinate(1L, 0L))),
                    Structure(
                        new ConstructionInstanceId("support:0000000004"),
                        RoofId,
                        ConstructionSpaceKey.Roof(new WorldCellCoordinate(2L, 0L))),
                    Structure(
                        new ConstructionInstanceId("support:0000000005"),
                        RoofId,
                        ConstructionSpaceKey.Roof(new WorldCellCoordinate(3L, 0L)))
                });
            ConstructionDefinitionCatalog catalog = CreateSupportCatalog();
            var policy = new ConstructionRoofSupportPolicy(new[] { WallId }, 2);
            ConstructionWorldState originalWorld = saved.RestoreState(catalog);
            ConstructionSupportGraph before = ConstructionRoofSupportGraphBuilder.Build(
                catalog,
                originalWorld.CaptureCanonicalStructures(),
                policy);

            byte[] bytes = GameSaveBinaryCodec.Encode(CreateGameSnapshot(saved));
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(bytes);
            ConstructionWorldState restoredWorld = decoded.Construction.RestoreState(catalog);
            ConstructionSupportGraph after = ConstructionRoofSupportGraphBuilder.Build(
                catalog,
                restoredWorld.CaptureCanonicalStructures(),
                policy);

            Assert.That(GameSaveBinaryCodec.CurrentVersion, Is.EqualTo(5));
            Assert.That(ReadUInt16(bytes, 8), Is.EqualTo(5));
            Assert.That(
                restoredWorld.CaptureCanonicalStructures().Select(
                    structure => structure.InstanceId),
                Is.EqualTo(originalWorld.CaptureCanonicalStructures().Select(
                    structure => structure.InstanceId)));
            Assert.That(after.NodeIds, Is.EqualTo(before.NodeIds));
            Assert.That(after.RootIds, Is.EqualTo(before.RootIds));
            Assert.That(after.Links, Is.EqualTo(before.Links));
            Assert.That(after.Evaluate().RoofStates,
                Is.EqualTo(before.Evaluate().RoofStates));
            Assert.That(after.Evaluate().RequireRoofState(
                new ConstructionInstanceId("support:0000000005")).IsSupported,
                Is.False);
        }

        [Test]
        public void MultipleEntriesUseCanonicalIdentityOrderAndStableBytes()
        {
            var saved = new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                9L,
                new[]
                {
                    Site("local-prototype:0000000008", FloorId, 8, 0),
                    Site("local-prototype:0000000002", FloorId, 2, 7)
                },
                new[]
                {
                    Structure(
                        new ConstructionInstanceId("local-prototype:0000000006"),
                        WallId,
                        ConstructionSpaceKey.Edge(
                            new WorldCellCoordinate(6, 0),
                            ConstructionCellSide.North))
                });
            GameSaveSnapshot game = CreateGameSnapshot(saved);

            byte[] first = GameSaveBinaryCodec.Encode(game);
            byte[] second = GameSaveBinaryCodec.Encode(game);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(first);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(decoded.Construction.Sites[0].InstanceId.Value,
                Is.EqualTo("local-prototype:0000000002"));
            Assert.That(decoded.Construction.Sites[1].InstanceId.Value,
                Is.EqualTo("local-prototype:0000000008"));
            Assert.That(decoded.Construction.Structures[0].InstanceId.Value,
                Is.EqualTo("local-prototype:0000000006"));
        }

        [Test]
        public void NextSequenceRoundTripIsExact()
        {
            ConstructionSaveSnapshot saved = SnapshotWithSite(
                "local-prototype:0000000041",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(41, 0)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0,
                42L);

            RestoredConstructionState restored = RoundTripAndRestore(saved);

            Assert.That(restored.NextInstanceSequence, Is.EqualTo(42L));
            Assert.That(restored.InstanceNamespace,
                Is.EqualTo(ConstructionSaveDefaults.PrototypeInstanceNamespace));
        }

        [Test]
        public void DuplicateIdentityAcrossSiteAndStructureIsRejected()
        {
            var id = new ConstructionInstanceId("local-prototype:0000000001");

            Assert.Throws<ArgumentException>(() => new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                2L,
                new[]
                {
                    new ConstructionSiteSnapshot(
                        id,
                        FloorId,
                        new ConstructionSpaceSnapshot(ConstructionSpaceKey.Surface(
                            new WorldCellCoordinate(0, 0))),
                        Array.Empty<ConstructionMaterialSnapshot>(),
                        0)
                },
                new[]
                {
                    Structure(id, FloorId, ConstructionSpaceKey.Surface(
                        new WorldCellCoordinate(1, 0)))
                }));
        }

        [Test]
        public void ContradictoryTopologyIsRejectedBeforeRestore()
        {
            ConstructionSpaceKey occupied = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(12, 12));

            Assert.Throws<ArgumentException>(() => new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                3L,
                new[] { Site("local-prototype:0000000001", FloorId, occupied, 0) },
                new[]
                {
                    Structure(
                        new ConstructionInstanceId("local-prototype:0000000002"),
                        FloorId,
                        occupied)
                }));
        }

        [Test]
        public void CollisionAtNextSequenceIsAcceptedWhenAllocatorCanSkipIt()
        {
            ConstructionSaveSnapshot snapshot = SnapshotWithSite(
                "local-prototype:0000000009",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(9, 0)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0,
                9L);

            Assert.That(snapshot.NextInstanceSequence, Is.EqualTo(9L));
        }

        [Test]
        public void FullAllocatorCollisionWindowIsRejected()
        {
            var sites = new List<ConstructionSiteSnapshot>();
            for (int sequence = 1;
                 sequence <= ConstructionInstanceIdSequencePolicy.MaximumCollisionAttempts;
                 sequence++)
            {
                sites.Add(Site(
                    "local-prototype:" + sequence.ToString(
                        "D10",
                        CultureInfo.InvariantCulture),
                    FloorId,
                    sequence,
                    0));
            }

            Assert.Throws<ArgumentException>(() => new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                1L,
                sites,
                Array.Empty<CompletedStructureSnapshot>()));
        }

        [Test]
        public void ExhaustedSequenceIsAnExplicitRoundTrippableSentinel()
        {
            var exhausted = new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                ConstructionInstanceIdSequencePolicy.ExhaustedSequence,
                Array.Empty<ConstructionSiteSnapshot>(),
                Array.Empty<CompletedStructureSnapshot>());

            RestoredConstructionState restored = RoundTripAndRestore(exhausted);

            Assert.That(
                restored.NextInstanceSequence,
                Is.EqualTo(ConstructionInstanceIdSequencePolicy.ExhaustedSequence));
        }

        [Test]
        public void NonCanonicalOwnedSequenceIsRejected()
        {
            Assert.Throws<ArgumentException>(() => SnapshotWithSite(
                "local-prototype:9",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(10, 0)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0,
                10L));
        }

        [Test]
        public void UnknownDefinitionAndOutOfBoundsProgressAreRejected()
        {
            ConstructionSaveSnapshot unknown = SnapshotWithSite(
                "local-prototype:0000000001",
                new ConstructionDefinitionId("unknown"),
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0, 0)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0,
                2L);
            ConstructionSaveSnapshot excessiveMaterial = SnapshotWithSite(
                "local-prototype:0000000001",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(1, 0)),
                new[] { Material(Branches.Id, 3) },
                0,
                2L);
            ConstructionSaveSnapshot excessiveWork = SnapshotWithSite(
                "local-prototype:0000000001",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(2, 0)),
                Array.Empty<ConstructionMaterialSnapshot>(),
                31,
                2L);

            Assert.Throws<InvalidOperationException>(() => Restore(CreateGameSnapshot(unknown)));
            Assert.Throws<InvalidOperationException>(() => Restore(CreateGameSnapshot(excessiveMaterial)));
            Assert.Throws<InvalidOperationException>(() => Restore(CreateGameSnapshot(excessiveWork)));
        }

        [Test]
        public void CompletedSiteEncodingIsRejected()
        {
            ConstructionSaveSnapshot completedAsSite = SnapshotWithSite(
                "local-prototype:0000000001",
                FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0, 0)),
                new[] { Material(Branches.Id, 2), Material(Stones.Id, 1) },
                30,
                2L);

            Assert.Throws<InvalidOperationException>(() =>
                Restore(CreateGameSnapshot(completedAsSite)));
        }

        [Test]
        public void NegativeProgressAndInvalidTopologyAreRejectedByDtos()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConstructionMaterialSnapshot(Branches.Id, -1));
            Assert.Throws<ArgumentException>(() => new ConstructionSiteSnapshot(
                default,
                FloorId,
                new ConstructionSpaceSnapshot(ConstructionSpaceKey.Surface(
                    new WorldCellCoordinate(0, 0))),
                Array.Empty<ConstructionMaterialSnapshot>(),
                0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConstructionSiteSnapshot(
                new ConstructionInstanceId("local-prototype:0000000001"),
                FloorId,
                new ConstructionSpaceSnapshot(ConstructionSpaceKey.Surface(
                    new WorldCellCoordinate(0, 0))),
                Array.Empty<ConstructionMaterialSnapshot>(),
                -1));
            Assert.Throws<OverflowException>(() => new ConstructionSpaceSnapshot(
                ConstructionSpaceKey.Edge(
                    new WorldCellCoordinate(long.MaxValue, 0L),
                    ConstructionCellSide.East)));
        }

        [Test]
        public void InvalidV4SectionVersionIsRejected()
        {
            byte[] encoded = GameSaveBinaryCodec.Encode(CreateGameSnapshot(
                ConstructionSaveSnapshot.Empty));
            int constructionOffset = EmptyConstructionOffset(encoded);
            WriteUInt16(encoded, constructionOffset, 2);
            RefreshPayloadHash(encoded);

            GameSaveCodecException exception = Assert.Throws<GameSaveCodecException>(() =>
                GameSaveBinaryCodec.Decode(encoded));

            Assert.That(exception.Violation,
                Is.EqualTo(GameSaveCodecViolation.UnsupportedSectionVersion));
        }

        private static ConstructionSiteSnapshot Site(
            string id,
            ConstructionDefinitionId definitionId,
            long x,
            int work)
        {
            return Site(
                id,
                definitionId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(x, 0)),
                work);
        }

        private static ConstructionSiteSnapshot Site(
            string id,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space,
            int work)
        {
            return new ConstructionSiteSnapshot(
                new ConstructionInstanceId(id),
                definitionId,
                new ConstructionSpaceSnapshot(space),
                Array.Empty<ConstructionMaterialSnapshot>(),
                work);
        }

        private static CompletedStructureSnapshot Structure(
            ConstructionInstanceId id,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            return new CompletedStructureSnapshot(
                id,
                definitionId,
                new ConstructionSpaceSnapshot(space));
        }

        private static ConstructionMaterialSnapshot Material(
            ItemDefinitionId id,
            int quantity) => new ConstructionMaterialSnapshot(id, quantity);

        private static ConstructionSaveSnapshot SnapshotWithSite(
            string instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space,
            IEnumerable<ConstructionMaterialSnapshot> materials,
            int work,
            long nextSequence)
        {
            return new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision,
                ConstructionSaveDefaults.PrototypeInstanceNamespace,
                nextSequence,
                new[]
                {
                    new ConstructionSiteSnapshot(
                        new ConstructionInstanceId(instanceId),
                        definitionId,
                        new ConstructionSpaceSnapshot(space),
                        materials,
                        work)
                },
                Array.Empty<CompletedStructureSnapshot>());
        }

        private static RestoredConstructionState RoundTripAndRestore(
            ConstructionSaveSnapshot construction)
        {
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(
                GameSaveBinaryCodec.Encode(CreateGameSnapshot(construction)));
            return Restore(decoded).Construction;
        }

        private static RestoredGameState Restore(GameSaveSnapshot snapshot)
        {
            var resolver = new Resolver(CreateWorld(), CreateCatalog());
            return GameSaveSnapshotRestorer.Restore(
                snapshot,
                resolver,
                resolver,
                resolver);
        }

        private static ConstructionSiteState RequireSite(
            ConstructionWorldState world,
            string id)
        {
            Assert.That(world.TryFindSite(
                new ConstructionInstanceId(id),
                out ConstructionSiteState site), Is.True);
            return site;
        }

        private static GameSaveSnapshot CreateGameSnapshot(
            ConstructionSaveSnapshot construction,
            long tick = 5L)
        {
            WorldPopulationSettings world = CreateWorld();
            PlayerInventoryState inventory = CreateInventory();
            return new GameSaveSnapshot(
                new WorldIdentitySnapshot(
                    world.Generation,
                    world.Profile.Id,
                    world.Profile.Revision),
                tick,
                new WorldPosition(3.5, -8.25),
                new PlayerHealthSnapshot(100, 73, tick, tick + 510L),
                new PlayerFoodSnapshot(
                    100,
                    61,
                    tick,
                    PlayerFoodRules.FirstSatietyLossTickAfter(tick)),
                PerishableInventorySnapshot.Empty,
                inventory.CaptureSnapshot(),
                Array.Empty<ChunkMutationState>(),
                construction);
        }

        private static PlayerInventoryState CreateInventory()
        {
            var main = new ContainerState(
                new ContainerId("player-main"),
                new ContainerDefinition(
                    "player-main",
                    "Player",
                    new EncumbranceValue(50000)));
            Assert.That(InventoryOperations.AddStack(main, Branches, 4).Accepted,
                Is.EqualTo(4));
            return new PlayerInventoryState(
                main.Id,
                new[] { Branches, Stones, Wood },
                new[] { main });
        }

        private static ConstructionDefinitionCatalog CreateCatalog()
        {
            return new ConstructionDefinitionCatalog(new[]
            {
                new ConstructionDefinition(
                    FloorId,
                    ConstructionSpaceKind.Surface,
                    30,
                    new[]
                    {
                        new ConstructionMaterialRequirement(Branches.Id, 2),
                        new ConstructionMaterialRequirement(Stones.Id, 1)
                    }),
                new ConstructionDefinition(
                    WallId,
                    ConstructionSpaceKind.Edge,
                    45,
                    new[]
                    {
                        new ConstructionMaterialRequirement(Branches.Id, 3),
                        new ConstructionMaterialRequirement(Wood.Id, 1)
                    })
            });
        }

        private static ConstructionDefinitionCatalog CreateSupportCatalog()
        {
            var definitions = new List<ConstructionDefinition>(
                CreateCatalog().Definitions)
            {
                new ConstructionDefinition(
                    RoofId,
                    ConstructionSpaceKind.Roof,
                    20,
                    new[]
                    {
                        new ConstructionMaterialRequirement(Branches.Id, 2),
                        new ConstructionMaterialRequirement(Wood.Id, 1)
                    })
            };
            return new ConstructionDefinitionCatalog(definitions);
        }

        private static WorldPopulationSettings CreateWorld() =>
            WorldPopulationDefaults.CreateTemperatePrototypeV1(
                new WorldSeed(0xC0FFEEUL));

        private static ItemDefinition Item(string id) => new ItemDefinition(
            new ItemDefinitionId(id),
            id,
            ItemStateKind.Stackable,
            new EncumbranceValue(1000));

        private static int InventoryQuantity(
            PlayerInventorySnapshot inventory,
            ItemDefinitionId definitionId)
        {
            int total = 0;
            for (int containerIndex = 0;
                 containerIndex < inventory.Containers.Count;
                 containerIndex++)
            {
                for (int entryIndex = 0;
                     entryIndex < inventory.Containers[containerIndex].Entries.Count;
                     entryIndex++)
                {
                    InventoryEntrySnapshot entry =
                        inventory.Containers[containerIndex].Entries[entryIndex];
                    if (entry.DefinitionId.Equals(definitionId)) total += entry.Quantity;
                }
            }
            return total;
        }

        private static ShelterHistorySnapshot History(string id) =>
            new ShelterHistorySnapshot(new ShelterId(id), 10L, 2L, 1, 3, 50);

        private static byte[] EncodeShelters(
            IEnumerable<ShelterHistorySnapshot> histories,
            bool hasPrimary,
            ShelterId primary)
        {
            GameSaveSnapshot baseline = CreateGameSnapshot(ConstructionSaveSnapshot.Empty);
            return GameSaveBinaryCodec.Encode(new GameSaveSnapshot(
                baseline.World,
                baseline.FixedTick,
                baseline.PlayerPosition,
                baseline.Health,
                baseline.Food,
                baseline.Perishables,
                baseline.Inventory,
                baseline.ChunkMutations,
                baseline.Construction,
                new ShelterSaveSnapshot(histories, hasPrimary, primary)));
        }

        private static int ShelterSectionOffset(
            byte[] encoded,
            IEnumerable<string> historyIds,
            string primaryId)
        {
            int length = 4 + 1;
            foreach (string id in historyIds) length += ShelterHistoryBinaryLength(id);
            if (primaryId != null)
                length += 4 + System.Text.Encoding.UTF8.GetByteCount(primaryId);
            return encoded.Length - length;
        }

        private static int ShelterHistoryBinaryLength(string id) =>
            4 + System.Text.Encoding.UTF8.GetByteCount(id) + 8 + 8 + 4 + 4 + 4;

        private static int HistoryTotalOffset(string id) =>
            4 + 4 + System.Text.Encoding.UTF8.GetByteCount(id);

        private static int HistoryProgressOffset(string id) => HistoryTotalOffset(id) + 8;
        private static int HistoryRestOffset(string id) => HistoryProgressOffset(id) + 8;
        private static int HistoryNightOffset(string id) => HistoryRestOffset(id) + 4;
        private static int HistoryFamiliarityOffset(string id) => HistoryNightOffset(id) + 4;

        private static void AssertInvalidShelterInt64(int relativeOffset, long value)
        {
            byte[] encoded = EncodeShelters(
                new[] { History("a") }, false, default);
            int section = ShelterSectionOffset(encoded, new[] { "a" }, null);
            WriteInt64(encoded, section + relativeOffset, value);
            RefreshPayloadHash(encoded);
            AssertCodecViolation(encoded, GameSaveCodecViolation.InvalidDomainValue);
        }

        private static void AssertInvalidShelterInt32(int relativeOffset, int value)
        {
            byte[] encoded = EncodeShelters(
                new[] { History("a") }, false, default);
            int section = ShelterSectionOffset(encoded, new[] { "a" }, null);
            WriteUInt32(encoded, section + relativeOffset, unchecked((uint)value));
            RefreshPayloadHash(encoded);
            AssertCodecViolation(encoded, GameSaveCodecViolation.InvalidDomainValue);
        }

        private static void AssertCodecViolation(
            byte[] encoded,
            GameSaveCodecViolation expected)
        {
            GameSaveCodecException exception = Assert.Throws<GameSaveCodecException>(
                () => GameSaveBinaryCodec.Decode(encoded));
            Assert.That(exception.Violation, Is.EqualTo(expected));
        }

        private static byte[] ConvertEmptyCurrentToV3(byte[] encoded)
        {
            int offset = EmptyConstructionOffset(encoded);
            var legacy = new byte[offset];
            Buffer.BlockCopy(encoded, 0, legacy, 0, offset);
            WriteUInt16(legacy, 8, 3);
            WriteUInt32(
                legacy,
                12,
                checked((uint)(legacy.Length - GameSaveCodecLimits.HeaderLength)));
            RefreshPayloadHash(legacy);
            return legacy;
        }

        private static byte[] ConvertEmptyV5ToV4(byte[] encoded)
        {
            if (ReadUInt16(encoded, 8) != 5)
                throw new InvalidDataException("Expected a V5 fixture.");
            const int emptyShelterLength = 5;
            int payloadLength = checked((int)ReadUInt32(encoded, 12));
            int legacyPayloadLength = payloadLength - emptyShelterLength;
            var legacy = new byte[GameSaveCodecLimits.HeaderLength + legacyPayloadLength];
            Buffer.BlockCopy(encoded, 0, legacy, 0, legacy.Length);
            WriteUInt16(legacy, 8, 4);
            WriteUInt32(legacy, 12, checked((uint)legacyPayloadLength));
            RefreshPayloadHash(legacy);
            return legacy;
        }

        private static int EmptyConstructionOffset(byte[] encoded)
        {
            int extensionLength = 2
                + 4 + System.Text.Encoding.UTF8.GetByteCount(
                    ConstructionSaveDefaults.PrototypeCatalogId)
                + 4
                + 4 + System.Text.Encoding.UTF8.GetByteCount(
                    ConstructionSaveDefaults.PrototypeInstanceNamespace)
                + 8
                + 4
                + 4
                + 5;
            return encoded.Length - extensionLength;
        }

        private static void RefreshPayloadHash(byte[] encoded)
        {
            int length = checked((int)ReadUInt32(encoded, 12));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    encoded,
                    GameSaveCodecLimits.HeaderLength,
                    length);
                Buffer.BlockCopy(hash, 0, encoded, 16, hash.Length);
            }
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt64(byte[] bytes, int offset, long value)
        {
            ulong raw = unchecked((ulong)value);
            for (int index = 0; index < 8; index++)
                bytes[offset + index] = (byte)(raw >> (index * 8));
        }

        private sealed class Resolver :
            IWorldPopulationSettingsResolver,
            IInventoryDefinitionResolver,
            IConstructionDefinitionResolver
        {
            private readonly WorldPopulationSettings _world;
            private readonly ConstructionDefinitionCatalog _construction;
            private readonly Dictionary<ItemDefinitionId, ItemDefinition> _items =
                new Dictionary<ItemDefinitionId, ItemDefinition>
                {
                    { Branches.Id, Branches },
                    { Stones.Id, Stones },
                    { Wood.Id, Wood }
                };

            public Resolver(
                WorldPopulationSettings world,
                ConstructionDefinitionCatalog construction)
            {
                _world = world;
                _construction = construction;
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
                out ItemDefinition definition) =>
                _items.TryGetValue(saved.Id, out definition);

            public bool TryResolveContainerDefinition(
                InventoryContainerSnapshot saved,
                out ContainerDefinition definition)
            {
                definition = new ContainerDefinition(
                    saved.DefinitionKey,
                    saved.DefinitionKey,
                    saved.Capacity);
                return true;
            }

            public bool TryResolveConstructionCatalog(
                ConstructionSaveSnapshot saved,
                out ConstructionDefinitionCatalog catalog)
            {
                catalog = _construction;
                return saved != null
                    && string.Equals(
                        saved.CatalogId,
                        ConstructionSaveDefaults.PrototypeCatalogId,
                        StringComparison.Ordinal)
                    && saved.CatalogRevision
                        == ConstructionSaveDefaults.PrototypeCatalogRevision;
            }
        }
    }
}
