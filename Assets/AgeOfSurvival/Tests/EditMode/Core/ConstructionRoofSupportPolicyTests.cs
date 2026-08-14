using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ConstructionRoofSupportPolicyTests
    {
        private static readonly ItemDefinitionId Component =
            new ItemDefinitionId("support.component");

        private static readonly ConstructionDefinitionId RoofDefinitionId =
            new ConstructionDefinitionId("support.roof");
        private static readonly ConstructionDefinitionId WallDefinitionId =
            new ConstructionDefinitionId("support.wall");
        private static readonly ConstructionDefinitionId OpeningDefinitionId =
            new ConstructionDefinitionId("support.opening");
        private static readonly ConstructionDefinitionId OtherEdgeDefinitionId =
            new ConstructionDefinitionId("support.edge.other");
        private static readonly ConstructionDefinitionId SurfaceDefinitionId =
            new ConstructionDefinitionId("support.surface");

        [Test]
        public void Policy_RejectsNegativeMaximumDistance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConstructionRoofSupportPolicy(
                    new[] { WallDefinitionId },
                    -1));
        }

        [Test]
        public void Policy_RejectsNullSupportingIdentifiers()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConstructionRoofSupportPolicy(null, 2));
        }

        [Test]
        public void Policy_RejectsInvalidSupportingIdentifier()
        {
            Assert.Throws<ArgumentException>(() =>
                new ConstructionRoofSupportPolicy(
                    new[] { WallDefinitionId, default(ConstructionDefinitionId) },
                    2));
        }

        [Test]
        public void Policy_RejectsDuplicateSupportingIdentifier()
        {
            Assert.Throws<ArgumentException>(() =>
                new ConstructionRoofSupportPolicy(
                    new[] { WallDefinitionId, WallDefinitionId },
                    2));
        }

        [Test]
        public void Policy_CanonicalizesSupportingIdentifiers()
        {
            var policy = new ConstructionRoofSupportPolicy(
                new[] { WallDefinitionId, OpeningDefinitionId },
                2);

            Assert.That(policy.SupportingEdgeDefinitionIds, Is.EqualTo(new[]
            {
                OpeningDefinitionId,
                WallDefinitionId
            }));
            Assert.That(policy.MaximumRoofPropagationDistance, Is.EqualTo(2));
        }

        [Test]
        public void Builder_RejectsUnknownSupportingDefinition()
        {
            var policy = new ConstructionRoofSupportPolicy(
                new[] { new ConstructionDefinitionId("support.unknown") },
                2);

            Assert.Throws<ArgumentException>(() => Build(
                CreateWorld(),
                policy));
        }

        [Test]
        public void Builder_RejectsSupportingDefinitionOutsideEdgeSpace()
        {
            var policy = new ConstructionRoofSupportPolicy(
                new[] { SurfaceDefinitionId },
                2);

            Assert.Throws<ArgumentException>(() => Build(
                CreateWorld(),
                policy));
        }

        [Test]
        public void Builder_RejectsNullArguments()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            ConstructionRoofSupportPolicy policy = Policy();

            Assert.Throws<ArgumentNullException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    null,
                    Array.Empty<CompletedStructureState>(),
                    policy));
            Assert.Throws<ArgumentNullException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    catalog,
                    null,
                    policy));
            Assert.Throws<ArgumentNullException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    catalog,
                    Array.Empty<CompletedStructureState>(),
                    null));
        }

        [Test]
        public void Builder_RejectsNullStructureEntry()
        {
            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    CreateCatalog(),
                    new CompletedStructureState[] { null },
                    Policy()));
        }

        [Test]
        public void Builder_RejectsDuplicateInstanceIdentifiers()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var firstWorld = new ConstructionWorldState(catalog);
            var secondWorld = new ConstructionWorldState(catalog);
            CompletedStructureState first = Complete(
                firstWorld,
                Id("same"),
                RoofDefinitionId,
                Roof(0L, 0L));
            CompletedStructureState second = Complete(
                secondWorld,
                Id("same"),
                WallDefinitionId,
                Edge(10L, 10L, ConstructionCellSide.North));

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    catalog,
                    new[] { first, second },
                    Policy()));
        }

        [Test]
        public void Builder_RejectsDuplicateOccupations()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var firstWorld = new ConstructionWorldState(catalog);
            var secondWorld = new ConstructionWorldState(catalog);
            CompletedStructureState first = Complete(
                firstWorld,
                Id("roof-a"),
                RoofDefinitionId,
                Roof(0L, 0L));
            CompletedStructureState second = Complete(
                secondWorld,
                Id("roof-b"),
                RoofDefinitionId,
                Roof(0L, 0L));

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    catalog,
                    new[] { first, second },
                    Policy()));
        }

        [Test]
        public void Builder_RejectsUnknownStructureDefinition()
        {
            ConstructionDefinitionId foreignId =
                new ConstructionDefinitionId("support.foreign");
            var foreignCatalog = new ConstructionDefinitionCatalog(new[]
            {
                Definition(foreignId, ConstructionSpaceKind.Roof)
            });
            var foreignWorld = new ConstructionWorldState(foreignCatalog);
            CompletedStructureState foreign = Complete(
                foreignWorld,
                Id("foreign"),
                foreignId,
                Roof(0L, 0L));

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    CreateCatalog(),
                    new[] { foreign },
                    Policy()));
        }

        [Test]
        public void Builder_RejectsDefinitionSpaceMismatch()
        {
            ConstructionDefinitionId mutableId =
                new ConstructionDefinitionId("support.mutable");
            var sourceCatalog = new ConstructionDefinitionCatalog(new[]
            {
                Definition(mutableId, ConstructionSpaceKind.Surface)
            });
            var sourceWorld = new ConstructionWorldState(sourceCatalog);
            CompletedStructureState source = Complete(
                sourceWorld,
                Id("mutable"),
                mutableId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L)));
            ConstructionDefinitionCatalog targetCatalog = CreateCatalog(
                Definition(mutableId, ConstructionSpaceKind.Roof));

            Assert.Throws<ArgumentException>(() =>
                ConstructionRoofSupportGraphBuilder.Build(
                    targetCatalog,
                    new[] { source },
                    Policy()));
        }

        [Test]
        public void EmptyWorld_ProducesEmptyGraphAndEvaluation()
        {
            ConstructionSupportGraph graph = Build(CreateWorld());
            ConstructionSupportEvaluation evaluation = graph.Evaluate();

            Assert.That(graph.NodeIds, Is.Empty);
            Assert.That(graph.RootIds, Is.Empty);
            Assert.That(graph.Links, Is.Empty);
            Assert.That(evaluation.RoofStates, Is.Empty);
        }

        [Test]
        public void RoofAlone_IsUnsupported()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof"), RoofDefinitionId, Roof(0L, 0L));

            AssertUnsupported(Build(world), "roof");
        }

        [TestCase("wall", true)]
        [TestCase("opening", true)]
        [TestCase("other", false)]
        public void ContactingEdge_UsesOnlyConfiguredSupportingDefinitions(
            string kind,
            bool expectedSupported)
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionDefinitionId edgeDefinition = kind == "wall"
                ? WallDefinitionId
                : kind == "opening"
                    ? OpeningDefinitionId
                    : OtherEdgeDefinitionId;
            Complete(world, Id("roof"), RoofDefinitionId, Roof(0L, 0L));
            Complete(
                world,
                Id("edge"),
                edgeDefinition,
                Edge(0L, 0L, ConstructionCellSide.West));

            ConstructionSupportGraph graph = Build(world);

            Assert.That(
                graph.Evaluate().RequireRoofState(Id("roof")).IsSupported,
                Is.EqualTo(expectedSupported));
            Assert.That(graph.RootIds.Count, Is.EqualTo(expectedSupported ? 1 : 0));
        }

        [Test]
        public void DistantWall_DoesNotSupportRoof()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof"), RoofDefinitionId, Roof(0L, 0L));
            Complete(
                world,
                Id("wall"),
                WallDefinitionId,
                Edge(5L, 5L, ConstructionCellSide.North));

            ConstructionSupportGraph graph = Build(world);

            AssertUnsupported(graph, "roof");
            Assert.That(graph.RootIds, Is.Empty);
        }

        [Test]
        public void IncompleteWallSite_DoesNotSupportRoof()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof"), RoofDefinitionId, Roof(0L, 0L));
            Assert.That(world.TryStartSite(
                Id("wall-site"),
                WallDefinitionId,
                Edge(0L, 0L, ConstructionCellSide.West),
                out _,
                out _), Is.True);

            AssertUnsupported(Build(world), "roof");
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 3)]
        public void UnilateralWall_SupportsExactlyThroughDistanceTwo(
            int roofCount,
            int expectedSupported)
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, roofCount);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RoofStates.Count(state => state.IsSupported),
                Is.EqualTo(expectedSupported));
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        public void ConfiguredMaximumDistance_IsAppliedByLinkGeneration(
            int maximumDistance,
            int expectedSupported)
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 3);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportGraph graph = Build(
                world,
                new ConstructionRoofSupportPolicy(
                    new[] { WallDefinitionId, OpeningDefinitionId },
                    maximumDistance));

            Assert.That(graph.Evaluate().RoofStates.Count(state => state.IsSupported),
                Is.EqualTo(expectedSupported));
        }

        [TestCase(6, 6)]
        [TestCase(7, 6)]
        public void SupportsAtBothEnds_UseShortestDistanceFromEitherSource(
            int roofCount,
            int expectedSupported)
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, roofCount);
            AddWestSupport(world, "wall-left", 0L, 0L, WallDefinitionId);
            Complete(
                world,
                Id("wall-right"),
                WallDefinitionId,
                Edge(roofCount - 1L, 0L, ConstructionCellSide.East));

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RoofStates.Count(state => state.IsSupported),
                Is.EqualTo(expectedSupported));
            if (roofCount == 7)
            {
                Assert.That(evaluation.RequireRoofState(Id("roof-3")).IsSupported,
                    Is.False);
            }
        }

        [Test]
        public void AdditionalRoot_WinsThroughTheShortestPath()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 5);
            AddWestSupport(world, "wall-left", 0L, 0L, WallDefinitionId);
            Complete(
                world,
                Id("wall-near"),
                OpeningDefinitionId,
                Edge(3L, 0L, ConstructionCellSide.North));

            ConstructionSupportGraph graph = Build(world);

            Assert.That(graph.Evaluate().RoofStates.All(state => state.IsSupported), Is.True);
            Assert.That(graph.Links, Does.Contain(new ConstructionSupportLink(
                Id("wall-near"),
                Id("roof-3"))));
            Assert.That(graph.Links, Does.Contain(new ConstructionSupportLink(
                Id("roof-3"),
                Id("roof-2"))));
        }

        [Test]
        public void MultipleSupportingContactsOnOneRoof_ProduceDistinctRootLinksOnly()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("wall"), WallDefinitionId,
                Edge(0L, 0L, ConstructionCellSide.West));
            Complete(world, Id("opening"), OpeningDefinitionId,
                Edge(0L, 0L, ConstructionCellSide.North));

            ConstructionSupportGraph graph = Build(world);

            Assert.That(graph.RootIds, Is.EqualTo(new[] { Id("opening"), Id("wall") }));
            Assert.That(graph.Links, Is.EqualTo(new[]
            {
                new ConstructionSupportLink(Id("opening"), Id("roof")),
                new ConstructionSupportLink(Id("wall"), Id("roof"))
            }));
        }

        [Test]
        public void DiagonalRoofs_DoNotPropagateSupport()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-a"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("roof-b"), RoofDefinitionId, Roof(1L, 1L));
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("roof-a")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-b")).IsSupported, Is.False);
        }

        [Test]
        public void MissingRoofCell_BreaksPropagation()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-a"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("roof-c"), RoofDefinitionId, Roof(2L, 0L));
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("roof-a")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-c")).IsSupported, Is.False);
        }

        [Test]
        public void CardinalLBranch_UsesRealPathDistance()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-0"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("roof-1"), RoofDefinitionId, Roof(1L, 0L));
            Complete(world, Id("roof-2"), RoofDefinitionId, Roof(1L, 1L));
            Complete(world, Id("roof-3"), RoofDefinitionId, Roof(1L, 2L));
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("roof-0")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-1")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-2")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-3")).IsSupported, Is.False);
        }

        [Test]
        public void SharedSupportingEdge_MakesBothAdjacentRoofsDistanceZero()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-south"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("roof-north"), RoofDefinitionId, Roof(0L, 1L));
            Complete(world, Id("wall"), WallDefinitionId,
                Edge(0L, 0L, ConstructionCellSide.North));

            ConstructionSupportGraph graph = Build(
                world,
                new ConstructionRoofSupportPolicy(new[] { WallDefinitionId }, 0));

            Assert.That(graph.Evaluate().RoofStates.All(state => state.IsSupported), Is.True);
            Assert.That(graph.Links, Is.EqualTo(new[]
            {
                new ConstructionSupportLink(Id("wall"), Id("roof-north")),
                new ConstructionSupportLink(Id("wall"), Id("roof-south"))
            }));
        }

        [Test]
        public void ChunkSeam_UsesWorldCardinalAdjacency()
        {
            var layout = new ChunkLayout(32, 32);
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-0"), RoofDefinitionId, Roof(31L, 4L));
            Complete(world, Id("roof-1"), RoofDefinitionId, Roof(32L, 4L));
            Complete(world, Id("roof-2"), RoofDefinitionId, Roof(33L, 4L));
            AddWestSupport(world, "wall", 31L, 4L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(Roof(31L, 4L).GetOwningChunk(layout),
                Is.Not.EqualTo(Roof(32L, 4L).GetOwningChunk(layout)));
            Assert.That(evaluation.RoofStates.All(state => state.IsSupported), Is.True);
        }

        [Test]
        public void NegativeCoordinates_UseTheSamePropagationPolicy()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-0"), RoofDefinitionId, Roof(-3L, -7L));
            Complete(world, Id("roof-1"), RoofDefinitionId, Roof(-2L, -7L));
            Complete(world, Id("roof-2"), RoofDefinitionId, Roof(-1L, -7L));
            AddWestSupport(world, "wall", -3L, -7L, WallDefinitionId);

            Assert.That(Build(world).Evaluate().RoofStates.All(state => state.IsSupported),
                Is.True);
        }

        [Test]
        public void Int64Limits_DoNotOverflowOrWrapAdjacency()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-0"), RoofDefinitionId, Roof(long.MaxValue - 2L, 0L));
            Complete(world, Id("roof-1"), RoofDefinitionId, Roof(long.MaxValue - 1L, 0L));
            Complete(world, Id("roof-2"), RoofDefinitionId, Roof(long.MaxValue, 0L));
            Complete(world, Id("wrapped"), RoofDefinitionId, Roof(long.MinValue, 0L));
            AddWestSupport(
                world,
                "wall",
                long.MaxValue - 2L,
                0L,
                WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("roof-0")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-1")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-2")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("wrapped")).IsSupported, Is.False);
        }

        [Test]
        public void DismantlingSupport_KeepsRoofButMakesEntireChainUnsupported()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 3);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);
            Assert.That(Build(world).Evaluate().RoofStates.All(state => state.IsSupported),
                Is.True);

            Assert.That(world.TryDismantleStructure(Id("wall"), out _, out _), Is.True);
            ConstructionSupportEvaluation after = Build(world).Evaluate();

            Assert.That(after.RoofStates.All(state => !state.IsSupported), Is.True);
            Assert.That(world.CaptureCanonicalStructures().Count(
                structure => structure.Space.Kind == ConstructionSpaceKind.Roof),
                Is.EqualTo(3));
        }

        [Test]
        public void DistanceTwoRoof_NeverGetsALinkToDistanceThreeRoof()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 4);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportGraph graph = Build(world);

            Assert.That(graph.Links, Does.Contain(new ConstructionSupportLink(
                Id("roof-1"),
                Id("roof-2"))));
            Assert.That(graph.Links, Has.None.EqualTo(new ConstructionSupportLink(
                Id("roof-2"),
                Id("roof-3"))));
            Assert.That(graph.Evaluate().RequireRoofState(Id("roof-3")).IsSupported,
                Is.False);
        }

        [Test]
        public void RemovingIntermediateRoof_BreaksTheChainWithoutRemovingDependentRoof()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 3);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);
            Assert.That(world.TryDismantleStructure(Id("roof-1"), out _, out _), Is.True);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("roof-0")).IsSupported, Is.True);
            Assert.That(evaluation.RequireRoofState(Id("roof-2")).IsSupported, Is.False);
            Assert.That(world.TryFindStructure(Id("roof-2"), out _), Is.True);
        }

        [Test]
        public void RebuildAfterMutations_HasNoStaleCacheAndCanRestoreSupport()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 2);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);
            Assert.That(Build(world).Evaluate().RoofStates.All(state => state.IsSupported),
                Is.True);

            Assert.That(world.TryDismantleStructure(Id("wall"), out _, out _), Is.True);
            Assert.That(Build(world).Evaluate().RoofStates.All(state => !state.IsSupported),
                Is.True);

            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);
            Assert.That(Build(world).Evaluate().RoofStates.All(state => state.IsSupported),
                Is.True);
        }

        [Test]
        public void ReversedStructures_ProduceIdenticalCanonicalGraphAndRoofStates()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 5);
            AddWestSupport(world, "wall-left", 0L, 0L, WallDefinitionId);
            Complete(world, Id("opening-right"), OpeningDefinitionId,
                Edge(4L, 0L, ConstructionCellSide.East));
            CompletedStructureState[] structures = world.CaptureCanonicalStructures();

            ConstructionSupportGraph normal = ConstructionRoofSupportGraphBuilder.Build(
                CreateCatalog(),
                structures,
                Policy());
            ConstructionSupportGraph reversed = ConstructionRoofSupportGraphBuilder.Build(
                CreateCatalog(),
                structures.Reverse(),
                Policy());

            AssertGraphEqual(normal, reversed);
        }

        [Test]
        public void ReversedSupportingIdentifiers_ProduceIdenticalCanonicalGraphAndRoofStates()
        {
            ConstructionWorldState world = CreateWorld();
            AddChain(world, 4);
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);
            Complete(world, Id("opening"), OpeningDefinitionId,
                Edge(3L, 0L, ConstructionCellSide.East));

            ConstructionSupportGraph first = Build(
                world,
                new ConstructionRoofSupportPolicy(
                    new[] { WallDefinitionId, OpeningDefinitionId },
                    2));
            ConstructionSupportGraph second = Build(
                world,
                new ConstructionRoofSupportPolicy(
                    new[] { OpeningDefinitionId, WallDefinitionId },
                    2));

            AssertGraphEqual(first, second);
        }

        [Test]
        public void MultipleMinimalParents_AreAllKeptCanonically()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("roof-center"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("roof-west"), RoofDefinitionId, Roof(-1L, 0L));
            Complete(world, Id("roof-south"), RoofDefinitionId, Roof(0L, -1L));
            AddWestSupport(world, "wall-west", -1L, 0L, WallDefinitionId);
            Complete(world, Id("wall-south"), OpeningDefinitionId,
                Edge(0L, -1L, ConstructionCellSide.South));

            ConstructionSupportGraph graph = Build(world);

            Assert.That(graph.Links, Does.Contain(new ConstructionSupportLink(
                Id("roof-south"),
                Id("roof-center"))));
            Assert.That(graph.Links, Does.Contain(new ConstructionSupportLink(
                Id("roof-west"),
                Id("roof-center"))));
        }

        [Test]
        public void ShelterContract_FollowsDerivedSupportOnly()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, Id("supported"), RoofDefinitionId, Roof(0L, 0L));
            Complete(world, Id("unsupported"), RoofDefinitionId, Roof(10L, 10L));
            AddWestSupport(world, "wall", 0L, 0L, WallDefinitionId);

            ConstructionSupportEvaluation evaluation = Build(world).Evaluate();

            Assert.That(evaluation.RequireRoofState(Id("supported")).CountsTowardShelter,
                Is.True);
            Assert.That(evaluation.RequireRoofState(Id("unsupported")).CountsTowardShelter,
                Is.False);
        }

        private static ConstructionRoofSupportPolicy Policy()
        {
            return new ConstructionRoofSupportPolicy(
                new[] { WallDefinitionId, OpeningDefinitionId },
                2);
        }

        private static ConstructionSupportGraph Build(
            ConstructionWorldState world,
            ConstructionRoofSupportPolicy policy = null)
        {
            return ConstructionRoofSupportGraphBuilder.Build(
                CreateCatalog(),
                world.CaptureCanonicalStructures(),
                policy ?? Policy());
        }

        private static void AddChain(ConstructionWorldState world, int count)
        {
            for (int index = 0; index < count; index++)
            {
                Complete(
                    world,
                    Id("roof-" + index),
                    RoofDefinitionId,
                    Roof(index, 0L));
            }
        }

        private static void AddWestSupport(
            ConstructionWorldState world,
            string id,
            long x,
            long y,
            ConstructionDefinitionId definitionId)
        {
            Complete(
                world,
                Id(id),
                definitionId,
                Edge(x, y, ConstructionCellSide.West));
        }

        private static ConstructionDefinitionCatalog CreateCatalog(
            params ConstructionDefinition[] additional)
        {
            var definitions = new List<ConstructionDefinition>
            {
                Definition(RoofDefinitionId, ConstructionSpaceKind.Roof),
                Definition(WallDefinitionId, ConstructionSpaceKind.Edge),
                Definition(OpeningDefinitionId, ConstructionSpaceKind.Edge),
                Definition(OtherEdgeDefinitionId, ConstructionSpaceKind.Edge),
                Definition(SurfaceDefinitionId, ConstructionSpaceKind.Surface)
            };
            definitions.AddRange(additional);
            return new ConstructionDefinitionCatalog(definitions);
        }

        private static ConstructionWorldState CreateWorld()
        {
            return new ConstructionWorldState(CreateCatalog());
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
            Assert.That(world.TryStartSite(
                instanceId,
                definitionId,
                space,
                out _,
                out ConstructionOperationReason startReason), Is.True);
            Assert.That(startReason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(world.DepositMaterial(instanceId, Component, 1).Accepted,
                Is.EqualTo(1));
            Assert.That(world.AddWork(instanceId, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.TryComplete(
                instanceId,
                out CompletedStructureState structure,
                out ConstructionOperationReason completeReason), Is.True);
            Assert.That(completeReason, Is.EqualTo(ConstructionOperationReason.None));
            return structure;
        }

        private static ConstructionSpaceKey Roof(long x, long y)
        {
            return ConstructionSpaceKey.Roof(new WorldCellCoordinate(x, y));
        }

        private static ConstructionSpaceKey Edge(
            long x,
            long y,
            ConstructionCellSide side)
        {
            return ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(x, y),
                side);
        }

        private static ConstructionInstanceId Id(string value)
        {
            return new ConstructionInstanceId(value);
        }

        private static void AssertUnsupported(
            ConstructionSupportGraph graph,
            string roofId)
        {
            ConstructionRoofSupportState state = graph.Evaluate().RequireRoofState(
                Id(roofId));
            Assert.That(state.IsSupported, Is.False);
            Assert.That(state.CountsTowardShelter, Is.False);
        }

        private static void AssertGraphEqual(
            ConstructionSupportGraph expected,
            ConstructionSupportGraph actual)
        {
            Assert.That(actual.NodeIds, Is.EqualTo(expected.NodeIds));
            Assert.That(actual.RootIds, Is.EqualTo(expected.RootIds));
            Assert.That(actual.Links, Is.EqualTo(expected.Links));
            Assert.That(actual.Evaluate().RoofStates,
                Is.EqualTo(expected.Evaluate().RoofStates));
        }
    }
}
