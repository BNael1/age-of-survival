using System;
using System.Linq;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ConstructionRoofBoundaryGeometryTests
    {
        private static readonly ItemDefinitionId Component =
            new ItemDefinitionId("synthetic.component");

        private static readonly ConstructionDefinitionId RoofDefinitionId =
            new ConstructionDefinitionId("synthetic.roof");
        private static readonly ConstructionDefinitionId EdgeDefinitionAId =
            new ConstructionDefinitionId("synthetic.edge.a");
        private static readonly ConstructionDefinitionId EdgeDefinitionBId =
            new ConstructionDefinitionId("synthetic.edge.b");
        private static readonly ConstructionDefinitionId SurfaceDefinitionId =
            new ConstructionDefinitionId("synthetic.surface");
        private static readonly ConstructionDefinitionId InteriorDefinitionId =
            new ConstructionDefinitionId("synthetic.interior");

        [Test]
        public void EmptyCompletedCollection_ProducesEmptyGeometry()
        {
            ConstructionRoofBoundaryGeometry geometry =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    Array.Empty<CompletedStructureState>());

            Assert.That(geometry.Roofs, Is.Empty);
        }

        [Test]
        public void RoofWithoutEdges_RemainsRepresentedWithNoContacts()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(3L, -2L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(roof.RoofCell, Is.EqualTo(cell));
            Assert.That(roof.Contacts, Is.Empty);
        }

        [TestCase(ConstructionCellSide.North)]
        [TestCase(ConstructionCellSide.East)]
        [TestCase(ConstructionCellSide.South)]
        [TestCase(ConstructionCellSide.West)]
        public void CardinalBoundaryEdge_ProducesOneExactContact(
            ConstructionCellSide side)
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            ConstructionInstanceId edgeId = Id("edge");
            WorldCellCoordinate cell = new WorldCellCoordinate(12L, -7L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(
                world,
                edgeId,
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(cell, side));

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(roof.Contacts.Count, Is.EqualTo(1));
            Assert.That(roof.Contacts[0].RoofInstanceId, Is.EqualTo(roofId));
            Assert.That(roof.Contacts[0].EdgeInstanceId, Is.EqualTo(edgeId));
            Assert.That(
                roof.Contacts[0].EdgeAddress,
                Is.EqualTo(ConstructionEdgeAddress.Create(cell, side)));
        }

        [Test]
        public void FourBoundaryEdges_ProduceFourUniqueCanonicallyOrderedContacts()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(0L, 0L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge-n"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.North));
            Complete(world, Id("edge-e"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.East));
            Complete(world, Id("edge-s"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.South));
            Complete(world, Id("edge-w"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.West));

            ConstructionRoofBoundaryContact[] contacts =
                RequireRoof(Build(world), roofId).Contacts.ToArray();
            ConstructionEdgeAddress[] addresses =
                contacts.Select(contact => contact.EdgeAddress).ToArray();
            ConstructionEdgeAddress[] sortedAddresses = addresses.ToArray();
            Array.Sort(sortedAddresses);

            Assert.That(contacts.Length, Is.EqualTo(4));
            Assert.That(addresses, Is.EqualTo(sortedAddresses));
            Assert.That(
                contacts.All(contact => contact.RoofInstanceId.Equals(roofId)),
                Is.True);
            Assert.That(RequireEdgeOccupant(
                contacts,
                cell,
                ConstructionCellSide.North), Is.EqualTo(Id("edge-n")));
            Assert.That(RequireEdgeOccupant(
                contacts,
                cell,
                ConstructionCellSide.East), Is.EqualTo(Id("edge-e")));
            Assert.That(RequireEdgeOccupant(
                contacts,
                cell,
                ConstructionCellSide.South), Is.EqualTo(Id("edge-s")));
            Assert.That(RequireEdgeOccupant(
                contacts,
                cell,
                ConstructionCellSide.West), Is.EqualTo(Id("edge-w")));
        }

        [TestCase(
            ConstructionCellSide.North,
            0L,
            1L,
            ConstructionCellSide.South)]
        [TestCase(
            ConstructionCellSide.East,
            1L,
            0L,
            ConstructionCellSide.West)]
        public void OppositeSideAlias_ProducesTheSameBoundaryContact(
            ConstructionCellSide roofSide,
            long neighbourDeltaX,
            long neighbourDeltaY,
            ConstructionCellSide neighbourSide)
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(-8L, 11L);
            WorldCellCoordinate neighbour = cell.Offset(neighbourDeltaX, neighbourDeltaY);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(
                world,
                Id("edge"),
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(neighbour, neighbourSide));

            ConstructionRoofBoundaryContact contact =
                RequireRoof(Build(world), roofId).Contacts.Single();

            Assert.That(
                contact.EdgeAddress,
                Is.EqualTo(ConstructionEdgeAddress.Create(cell, roofSide)));
        }

        [TestCase(2L, 0L, ConstructionCellSide.North)]
        [TestCase(1L, 1L, ConstructionCellSide.East)]
        public void NonBoundaryEdge_ProducesNoContact(
            long edgeCellDeltaX,
            long edgeCellDeltaY,
            ConstructionCellSide edgeSide)
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(5L, 5L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(
                world,
                Id("edge"),
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(
                    cell.Offset(edgeCellDeltaX, edgeCellDeltaY),
                    edgeSide));

            Assert.That(RequireRoof(Build(world), roofId).Contacts, Is.Empty);
        }

        [TestCase(ConstructionSpaceKind.Surface)]
        [TestCase(ConstructionSpaceKind.Interior)]
        [TestCase(ConstructionSpaceKind.Roof)]
        public void NonEdgeStructure_DoesNotBecomeABoundaryContact(
            ConstructionSpaceKind kind)
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof-a");
            WorldCellCoordinate cell = new WorldCellCoordinate(2L, 2L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));

            switch (kind)
            {
                case ConstructionSpaceKind.Surface:
                    Complete(world, Id("other"), SurfaceDefinitionId, ConstructionSpaceKey.Surface(cell));
                    break;

                case ConstructionSpaceKind.Interior:
                    Complete(world, Id("other"), InteriorDefinitionId, ConstructionSpaceKey.Interior(cell));
                    break;

                case ConstructionSpaceKind.Roof:
                    Complete(
                        world,
                        Id("roof-b"),
                        RoofDefinitionId,
                        ConstructionSpaceKey.Roof(cell.Offset(1L, 0L)));
                    break;

                default:
                    Assert.Fail("Unexpected construction space kind in test.");
                    break;
            }

            Assert.That(RequireRoof(Build(world), roofId).Contacts, Is.Empty);
        }

        [Test]
        public void SharedEdge_ProducesOneContactForEachAdjacentRoof()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId southRoofId = Id("roof-south");
            ConstructionInstanceId northRoofId = Id("roof-north");
            ConstructionInstanceId edgeId = Id("shared-edge");
            WorldCellCoordinate southCell = new WorldCellCoordinate(7L, 9L);
            WorldCellCoordinate northCell = southCell.Offset(0L, 1L);
            ConstructionEdgeAddress expectedEdge = ConstructionEdgeAddress.Create(
                southCell,
                ConstructionCellSide.North);

            Complete(world, southRoofId, RoofDefinitionId, ConstructionSpaceKey.Roof(southCell));
            Complete(world, northRoofId, RoofDefinitionId, ConstructionSpaceKey.Roof(northCell));
            Complete(world, edgeId, EdgeDefinitionAId, ConstructionSpaceKey.Edge(expectedEdge));

            ConstructionRoofBoundaryGeometry geometry = Build(world);
            ConstructionRoofBoundaryContact southContact =
                RequireRoof(geometry, southRoofId).Contacts.Single();
            ConstructionRoofBoundaryContact northContact =
                RequireRoof(geometry, northRoofId).Contacts.Single();

            Assert.That(southContact.EdgeInstanceId, Is.EqualTo(edgeId));
            Assert.That(northContact.EdgeInstanceId, Is.EqualTo(edgeId));
            Assert.That(southContact.EdgeAddress, Is.EqualTo(expectedEdge));
            Assert.That(northContact.EdgeAddress, Is.EqualTo(expectedEdge));
        }

        [Test]
        public void IncompleteEdgeSite_IsExcludedFromCompletedGeometry()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(0L, 0L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Assert.That(
                world.TryStartSite(
                    Id("edge-site"),
                    EdgeDefinitionAId,
                    ConstructionSpaceKey.Edge(cell, ConstructionCellSide.North),
                    out _,
                    out ConstructionOperationReason reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));

            ConstructionRoofBoundary roof = RequireRoof(
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    world.CaptureCanonicalStructures()),
                roofId);

            Assert.That(world.SiteCount, Is.EqualTo(1));
            Assert.That(roof.Contacts, Is.Empty);
        }

        [Test]
        public void ReversedInput_ProducesExactlyTheSameCanonicalGeometry()
        {
            ConstructionWorldState world = CreateWorld();
            WorldCellCoordinate firstCell = new WorldCellCoordinate(-2L, 4L);
            WorldCellCoordinate secondCell = new WorldCellCoordinate(9L, -3L);
            Complete(world, Id("roof-b"), RoofDefinitionId, ConstructionSpaceKey.Roof(firstCell));
            Complete(world, Id("edge-b"), EdgeDefinitionBId, ConstructionSpaceKey.Edge(firstCell, ConstructionCellSide.West));
            Complete(world, Id("edge-c"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(firstCell, ConstructionCellSide.North));
            Complete(world, Id("edge-d"), EdgeDefinitionBId, ConstructionSpaceKey.Edge(firstCell, ConstructionCellSide.East));
            Complete(world, Id("roof-a"), RoofDefinitionId, ConstructionSpaceKey.Roof(secondCell));
            Complete(world, Id("edge-a"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(secondCell, ConstructionCellSide.East));
            Complete(world, Id("surface"), SurfaceDefinitionId, ConstructionSpaceKey.Surface(firstCell));
            CompletedStructureState[] structures = world.CaptureCanonicalStructures();

            ConstructionRoofBoundaryGeometry normal =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(structures);
            ConstructionRoofBoundaryGeometry reversed =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(structures.Reverse());

            AssertGeometryEqual(normal, reversed);
        }

        [Test]
        public void MultipleRoofs_AreOrderedByCanonicalInstanceIdentifier()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-z"), RoofDefinitionId, ConstructionSpaceKey.Roof(new WorldCellCoordinate(-10L, -10L)));
            Complete(world, Id("roof-a"), RoofDefinitionId, ConstructionSpaceKey.Roof(new WorldCellCoordinate(10L, 10L)));
            Complete(world, Id("roof-m"), RoofDefinitionId, ConstructionSpaceKey.Roof(new WorldCellCoordinate(0L, 0L)));

            ConstructionInstanceId[] ids = Build(world).Roofs
                .Select(roof => roof.RoofInstanceId)
                .ToArray();

            Assert.That(ids, Is.EqualTo(new[]
            {
                Id("roof-a"),
                Id("roof-m"),
                Id("roof-z")
            }));
        }

        [Test]
        public void NullCompletedCollection_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(null));
        }

        [Test]
        public void NullCompletedEntry_IsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    new CompletedStructureState[] { null }));
        }

        [Test]
        public void DuplicateCompletedInstanceIdentifier_IsRejected()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var firstWorld = new ConstructionWorldState(catalog);
            var secondWorld = new ConstructionWorldState(catalog);
            CompletedStructureState first = Complete(
                firstWorld,
                Id("same-id"),
                RoofDefinitionId,
                ConstructionSpaceKey.Roof(new WorldCellCoordinate(0L, 0L)));
            CompletedStructureState second = Complete(
                secondWorld,
                Id("same-id"),
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(
                    new WorldCellCoordinate(10L, 10L),
                    ConstructionCellSide.North));

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    new[] { first, second }));
        }

        [Test]
        public void DuplicateOccupiedSpace_IsRejectedEvenWithDifferentIdentifiers()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var firstWorld = new ConstructionWorldState(catalog);
            var secondWorld = new ConstructionWorldState(catalog);
            ConstructionSpaceKey occupied = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(0L, 0L),
                ConstructionCellSide.North);
            CompletedStructureState first = Complete(
                firstWorld,
                Id("edge-a"),
                EdgeDefinitionAId,
                occupied);
            CompletedStructureState second = Complete(
                secondWorld,
                Id("edge-b"),
                EdgeDefinitionBId,
                occupied);

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    new[] { first, second }));
        }

        [Test]
        public void ChunkSeam_DoesNotDuplicateOrLoseTheCrossChunkContact()
        {
            var layout = new ChunkLayout(32, 32);
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate roofCell = new WorldCellCoordinate(32L, 5L);
            ConstructionSpaceKey edgeSpace = ConstructionSpaceKey.Edge(
                roofCell,
                ConstructionCellSide.West);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(roofCell));
            Complete(world, Id("edge"), EdgeDefinitionAId, edgeSpace);

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(
                ConstructionSpaceKey.Roof(roofCell).GetOwningChunk(layout),
                Is.EqualTo(new ChunkCoordinate(1L, 0L)));
            Assert.That(
                edgeSpace.GetOwningChunk(layout),
                Is.EqualTo(new ChunkCoordinate(0L, 0L)));
            Assert.That(roof.Contacts.Count, Is.EqualTo(1));
            Assert.That(roof.Contacts[0].EdgeAddress, Is.EqualTo(edgeSpace.EdgeAddress));
        }

        [Test]
        public void NegativeCoordinates_UseTheSameCanonicalBoundaryRules()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(-33L, -65L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(
                world,
                Id("edge"),
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(
                    cell.Offset(0L, 1L),
                    ConstructionCellSide.South));

            ConstructionRoofBoundaryContact contact =
                RequireRoof(Build(world), roofId).Contacts.Single();

            Assert.That(
                contact.EdgeAddress,
                Is.EqualTo(ConstructionEdgeAddress.Create(
                    cell,
                    ConstructionCellSide.North)));
        }

        [TestCase(long.MaxValue, 0L, ConstructionCellSide.East)]
        [TestCase(0L, long.MaxValue, ConstructionCellSide.North)]
        [TestCase(long.MinValue, 0L, ConstructionCellSide.West)]
        [TestCase(0L, long.MinValue, ConstructionCellSide.South)]
        public void EdgeTryCreate_ReturnsFalseForUnrepresentableNeighbour(
            long x,
            long y,
            ConstructionCellSide side)
        {
            bool created = ConstructionEdgeAddress.TryCreate(
                new WorldCellCoordinate(x, y),
                side,
                out ConstructionEdgeAddress edge);

            Assert.That(created, Is.False);
            Assert.That(edge.IsValid, Is.False);
        }

        [TestCase(ConstructionCellSide.North)]
        [TestCase(ConstructionCellSide.East)]
        [TestCase(ConstructionCellSide.South)]
        [TestCase(ConstructionCellSide.West)]
        public void EdgeTryCreate_MatchesStrictCreateWhenRepresentable(
            ConstructionCellSide side)
        {
            WorldCellCoordinate cell = new WorldCellCoordinate(-1L, 1L);

            bool created = ConstructionEdgeAddress.TryCreate(
                cell,
                side,
                out ConstructionEdgeAddress edge);

            Assert.That(created, Is.True);
            Assert.That(edge, Is.EqualTo(ConstructionEdgeAddress.Create(cell, side)));
        }

        [Test]
        public void EdgeTryCreate_RejectsUnknownSide()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ConstructionEdgeAddress.TryCreate(
                    new WorldCellCoordinate(0L, 0L),
                    (ConstructionCellSide)999,
                    out _));
        }

        [Test]
        public void MaximumCorner_OmitsNorthAndEastButKeepsSouthAndWest()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(long.MaxValue, long.MaxValue);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge-s"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.South));
            Complete(world, Id("edge-w"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.West));

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(roof.Contacts.Count, Is.EqualTo(2));
            Assert.That(roof.Contacts.Select(contact => contact.EdgeAddress), Is.EqualTo(
                CanonicalEdges(cell, ConstructionCellSide.South, ConstructionCellSide.West)));
        }

        [Test]
        public void MinimumCorner_OmitsSouthAndWestButKeepsNorthAndEast()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(long.MinValue, long.MinValue);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge-n"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.North));
            Complete(world, Id("edge-e"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.East));

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(roof.Contacts.Count, Is.EqualTo(2));
            Assert.That(roof.Contacts.Select(contact => contact.EdgeAddress), Is.EqualTo(
                CanonicalEdges(cell, ConstructionCellSide.North, ConstructionCellSide.East)));
        }

        [Test]
        public void DismantledEdge_DisappearsOnRebuildWhileRoofRemains()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            ConstructionInstanceId edgeId = Id("edge");
            WorldCellCoordinate cell = new WorldCellCoordinate(1L, 1L);
            ConstructionSpaceKey roofSpace = ConstructionSpaceKey.Roof(cell);
            Complete(world, roofId, RoofDefinitionId, roofSpace);
            Complete(
                world,
                edgeId,
                EdgeDefinitionAId,
                ConstructionSpaceKey.Edge(cell, ConstructionCellSide.East));
            Assert.That(RequireRoof(Build(world), roofId).Contacts.Count, Is.EqualTo(1));

            Assert.That(world.TryDismantleStructure(edgeId, out _, out _), Is.True);
            ConstructionRoofBoundary after = RequireRoof(Build(world), roofId);

            Assert.That(after.RoofCell, Is.EqualTo(cell));
            Assert.That(after.Contacts, Is.Empty);
            Assert.That(world.TryFindStructure(roofId, out CompletedStructureState roof), Is.True);
            Assert.That(roof.Space, Is.EqualTo(roofSpace));
        }

        [Test]
        public void SameCompletedCapture_RebuildsToExactlyTheSameGeometry()
        {
            ConstructionWorldState world = CreateWorld();
            WorldCellCoordinate cell = new WorldCellCoordinate(4L, 6L);
            Complete(world, Id("roof"), RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.South));
            CompletedStructureState[] structures = world.CaptureCanonicalStructures();

            ConstructionRoofBoundaryGeometry first =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(structures);
            ConstructionRoofBoundaryGeometry second =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(structures);

            AssertGeometryEqual(first, second);
        }

        [Test]
        public void RestoredCanonicalWorld_RebuildsToExactlyTheSameGeometry()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            WorldCellCoordinate cell = new WorldCellCoordinate(-17L, 31L);
            Complete(world, Id("roof"), RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge-a"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.North));
            Complete(world, Id("edge-b"), EdgeDefinitionBId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.West));
            ConstructionRoofBoundaryGeometry before = Build(world);

            ConstructionSaveSnapshot snapshot = ConstructionSaveSnapshot.Capture(
                "synthetic.catalog",
                1,
                "synthetic.instances",
                1L,
                world);
            ConstructionWorldState restored = snapshot.RestoreState(catalog);
            ConstructionRoofBoundaryGeometry after = Build(restored);

            AssertGeometryEqual(before, after);
            Assert.That(
                restored.CaptureCanonicalStructures()
                    .Select(structure => structure.InstanceId),
                Is.EqualTo(world.CaptureCanonicalStructures()
                    .Select(structure => structure.InstanceId)));
        }

        [Test]
        public void DifferentEdgeDefinitions_AreBothPurelyGeometricOccupants()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId roofId = Id("roof");
            WorldCellCoordinate cell = new WorldCellCoordinate(8L, 8L);
            Complete(world, roofId, RoofDefinitionId, ConstructionSpaceKey.Roof(cell));
            Complete(world, Id("edge-a"), EdgeDefinitionAId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.North));
            Complete(world, Id("edge-b"), EdgeDefinitionBId, ConstructionSpaceKey.Edge(cell, ConstructionCellSide.East));

            ConstructionRoofBoundary roof = RequireRoof(Build(world), roofId);

            Assert.That(roof.Contacts.Count, Is.EqualTo(2));
            Assert.That(
                roof.Contacts.Select(contact => contact.EdgeInstanceId),
                Does.Contain(Id("edge-a")));
            Assert.That(
                roof.Contacts.Select(contact => contact.EdgeInstanceId),
                Does.Contain(Id("edge-b")));
        }

        private static ConstructionRoofBoundaryGeometry Build(
            ConstructionWorldState world)
        {
            return ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                world.CaptureCanonicalStructures());
        }

        private static ConstructionRoofBoundary RequireRoof(
            ConstructionRoofBoundaryGeometry geometry,
            ConstructionInstanceId roofId)
        {
            return geometry.Roofs.Single(roof =>
                roof.RoofInstanceId.Equals(roofId));
        }

        private static ConstructionEdgeAddress[] CanonicalEdges(
            WorldCellCoordinate cell,
            params ConstructionCellSide[] sides)
        {
            ConstructionEdgeAddress[] edges = sides
                .Select(side => ConstructionEdgeAddress.Create(cell, side))
                .ToArray();
            Array.Sort(edges);
            return edges;
        }

        private static ConstructionInstanceId RequireEdgeOccupant(
            ConstructionRoofBoundaryContact[] contacts,
            WorldCellCoordinate roofCell,
            ConstructionCellSide side)
        {
            ConstructionEdgeAddress address = ConstructionEdgeAddress.Create(
                roofCell,
                side);
            return contacts.Single(contact =>
                contact.EdgeAddress.Equals(address)).EdgeInstanceId;
        }

        private static void AssertGeometryEqual(
            ConstructionRoofBoundaryGeometry expected,
            ConstructionRoofBoundaryGeometry actual)
        {
            Assert.That(actual.Roofs.Count, Is.EqualTo(expected.Roofs.Count));
            for (int roofIndex = 0; roofIndex < expected.Roofs.Count; roofIndex++)
            {
                ConstructionRoofBoundary expectedRoof = expected.Roofs[roofIndex];
                ConstructionRoofBoundary actualRoof = actual.Roofs[roofIndex];
                Assert.That(actualRoof.RoofInstanceId, Is.EqualTo(expectedRoof.RoofInstanceId));
                Assert.That(actualRoof.RoofCell, Is.EqualTo(expectedRoof.RoofCell));
                Assert.That(actualRoof.Contacts, Is.EqualTo(expectedRoof.Contacts));
            }
        }

        private static ConstructionWorldState CreateWorld()
        {
            return new ConstructionWorldState(CreateCatalog());
        }

        private static ConstructionDefinitionCatalog CreateCatalog()
        {
            return new ConstructionDefinitionCatalog(new[]
            {
                Definition(RoofDefinitionId, ConstructionSpaceKind.Roof),
                Definition(EdgeDefinitionAId, ConstructionSpaceKind.Edge),
                Definition(EdgeDefinitionBId, ConstructionSpaceKind.Edge),
                Definition(SurfaceDefinitionId, ConstructionSpaceKind.Surface),
                Definition(InteriorDefinitionId, ConstructionSpaceKind.Interior)
            });
        }

        private static ConstructionDefinition Definition(
            ConstructionDefinitionId id,
            ConstructionSpaceKind kind)
        {
            return new ConstructionDefinition(
                id,
                kind,
                1,
                new[] { new ConstructionMaterialRequirement(Component, 1) });
        }

        private static CompletedStructureState Complete(
            ConstructionWorldState world,
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            Assert.That(
                world.TryStartSite(
                    instanceId,
                    definitionId,
                    space,
                    out _,
                    out ConstructionOperationReason startReason),
                Is.True);
            Assert.That(startReason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(world.DepositMaterial(instanceId, Component, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.AddWork(instanceId, 1).Accepted, Is.EqualTo(1));
            Assert.That(
                world.TryComplete(
                    instanceId,
                    out CompletedStructureState structure,
                    out ConstructionOperationReason completeReason),
                Is.True);
            Assert.That(completeReason, Is.EqualTo(ConstructionOperationReason.None));
            return structure;
        }

        private static ConstructionInstanceId Id(string value)
        {
            return new ConstructionInstanceId(value);
        }
    }
}
