using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Tests.EditMode.Core
{
    public sealed class ShelterVerticalSliceTests
    {
        private static readonly ItemDefinitionId Material = new ItemDefinitionId("test.material");
        private static readonly ConstructionDefinitionId Floor = new ConstructionDefinitionId("test.floor");
        private static readonly ConstructionDefinitionId Wall = new ConstructionDefinitionId("test.wall");
        private static readonly ConstructionDefinitionId Opening = new ConstructionDefinitionId("test.opening");
        private static readonly ConstructionDefinitionId OtherEdge = new ConstructionDefinitionId("test.edge.other");
        private static readonly ConstructionDefinitionId Roof = new ConstructionDefinitionId("test.roof");
        private static readonly ConstructionDefinitionId Bed = new ConstructionDefinitionId("test.bed");

        [Test]
        public void BlockingPolicyBuildsCanonicalCompletedEdgesOnly()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            Complete(world, Wall, ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), ConstructionCellSide.North), "wall-b");
            Complete(world, OtherEdge, ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), ConstructionCellSide.East), "other");
            Complete(world, Opening, ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), ConstructionCellSide.West), "opening");

            IReadOnlyList<ConstructionEdgeAddress> edges = ConstructionEnclosureBlockingEdgeBuilder.Build(
                catalog,
                world.CaptureCanonicalStructures().Reverse(),
                new ConstructionEnclosureBlockingPolicy(new[] { Wall, Opening }));

            Assert.That(edges.Count, Is.EqualTo(2));
            Assert.That(edges, Is.Ordered);
            Assert.That(
                edges.Contains(ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(0, 0),
                    ConstructionCellSide.East)),
                Is.False);
            CompletedStructureState duplicate = world.CaptureCanonicalStructures()[0];
            Assert.Throws<ArgumentException>(() => ConstructionEnclosureBlockingEdgeBuilder.Build(
                catalog,
                new[] { duplicate, duplicate },
                new ConstructionEnclosureBlockingPolicy(new[] { Wall })));
        }

        [Test]
        public void BlockingPolicyRejectsUnknownAndNonEdgeDefinitions()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            Assert.Throws<KeyNotFoundException>(() => ConstructionEnclosureBlockingEdgeBuilder.Build(
                catalog, Array.Empty<CompletedStructureState>(),
                new ConstructionEnclosureBlockingPolicy(new[] { new ConstructionDefinitionId("missing") })));
            Assert.Throws<ArgumentException>(() => ConstructionEnclosureBlockingEdgeBuilder.Build(
                catalog, Array.Empty<CompletedStructureState>(),
                new ConstructionEnclosureBlockingPolicy(new[] { Floor })));
        }

        [Test]
        public void BlockingPolicyAndInputOrderDoNotChangeDerivedEdgesOrRooms()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            WorldCellCoordinate[] cells = Rectangle(0, 0, 3, 2);
            int index = 0;
            foreach (ConstructionEdgeAddress edge in Boundary(cells))
            {
                ConstructionDefinitionId definition = index++ % 2 == 0 ? Wall : Opening;
                Complete(world, definition, ConstructionSpaceKey.Edge(edge), "edge-" + index);
            }

            CompletedStructureState[] forward = world.CaptureCanonicalStructures();
            IReadOnlyList<ConstructionEdgeAddress> first =
                ConstructionEnclosureBlockingEdgeBuilder.Build(
                    catalog,
                    forward,
                    new ConstructionEnclosureBlockingPolicy(new[] { Wall, Opening }));
            IReadOnlyList<ConstructionEdgeAddress> second =
                ConstructionEnclosureBlockingEdgeBuilder.Build(
                    catalog,
                    forward.Reverse(),
                    new ConstructionEnclosureBlockingPolicy(new[] { Opening, Wall }));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(
                ConstructionRoomAnalyzer.Analyze(second.Reverse(), Limits()).Rooms.Single().Cells,
                Is.EqualTo(ConstructionRoomAnalyzer.Analyze(first, Limits()).Rooms.Single().Cells));
        }

        [Test]
        public void RoomAnalyzerHandlesOneCellRectanglesAndOpeningThenClosure()
        {
            var cell = new WorldCellCoordinate(0, 0);
            List<ConstructionEdgeAddress> closed = Boundary(new[] { cell });
            ConstructionRoomAnalysis complete = ConstructionRoomAnalyzer.Analyze(closed, Limits());
            Assert.That(complete.Rooms.Count, Is.EqualTo(1));
            Assert.That(complete.Rooms[0].Cells, Is.EqualTo(new[] { cell }));

            ConstructionEdgeAddress removed = closed[0];
            List<ConstructionEdgeAddress> open = closed.Where(edge => !edge.Equals(removed)).ToList();
            Assert.That(ConstructionRoomAnalyzer.AnalyzeAffected(open, new[] { removed }, Limits()).Rooms, Is.Empty);
            Assert.That(ConstructionRoomAnalyzer.AnalyzeAffected(closed, new[] { removed }, Limits()).Rooms.Count, Is.EqualTo(1));
        }

        [Test]
        public void RoomAnalyzerHandlesLShapeNegativeCoordinatesAndChunkSeam()
        {
            var shape = new[]
            {
                new WorldCellCoordinate(-1, -1),
                new WorldCellCoordinate(0, -1),
                new WorldCellCoordinate(-1, 0),
                new WorldCellCoordinate(31, 4),
                new WorldCellCoordinate(32, 4)
            };
            var first = shape.Take(3).ToArray();
            var seam = shape.Skip(3).ToArray();
            var edges = Boundary(first).Concat(Boundary(seam)).Reverse().ToArray();
            ConstructionRoomAnalysis rooms = ConstructionRoomAnalyzer.Analyze(edges, Limits());
            Assert.That(rooms.Rooms.Count, Is.EqualTo(2));
            Assert.That(rooms.Rooms[0].Cells, Is.EqualTo(first.OrderBy(value => value).ToArray()));
            Assert.That(rooms.Rooms[1].Cells, Is.EqualTo(seam.OrderBy(value => value).ToArray()));
        }

        [Test]
        public void RoomAnalyzerHandlesRectangleAndLargeBoundedScenario()
        {
            WorldCellCoordinate[] rectangle = Rectangle(-2, 3, 3, 2);
            ConstructionRoomAnalysis small = ConstructionRoomAnalyzer.Analyze(
                Boundary(rectangle).Reverse<ConstructionEdgeAddress>(),
                Limits());
            Assert.That(small.Rooms.Single().Cells, Is.EqualTo(rectangle));

            WorldCellCoordinate[] large = Rectangle(0, 0, 64, 64);
            ConstructionRoomAnalysis largeResult = ConstructionRoomAnalyzer.Analyze(
                Boundary(large),
                new ConstructionRoomAnalysisLimits(4, 4_500));
            Assert.That(largeResult.Rooms.Single().Cells.Count, Is.EqualTo(4_096));
        }

        [Test]
        public void SharedWallCreatesTwoCanonicalAdjacentRooms()
        {
            var left = new WorldCellCoordinate(0, 0);
            var right = new WorldCellCoordinate(1, 0);
            List<ConstructionEdgeAddress> edges = Boundary(new[] { left, right });
            edges.Add(ConstructionEdgeAddress.Create(left, ConstructionCellSide.East));
            ConstructionRoomAnalysis rooms = ConstructionRoomAnalyzer.Analyze(edges.Reverse<ConstructionEdgeAddress>(), Limits());
            Assert.That(rooms.Rooms.Count, Is.EqualTo(2));
            Assert.That(rooms.Rooms[0].Cells, Is.EqualTo(new[] { left }));
            Assert.That(rooms.Rooms[1].Cells, Is.EqualTo(new[] { right }));
        }

        [Test]
        public void AddingAndRemovingSharedWallDeterministicallySplitsAndMergesRoom()
        {
            var left = new WorldCellCoordinate(0, 0);
            var right = new WorldCellCoordinate(1, 0);
            List<ConstructionEdgeAddress> merged = Boundary(new[] { left, right });
            ConstructionEdgeAddress shared = ConstructionEdgeAddress.Create(left, ConstructionCellSide.East);

            Assert.That(ConstructionRoomAnalyzer.Analyze(merged, Limits()).Rooms.Single().Cells.Count, Is.EqualTo(2));
            Assert.That(ConstructionRoomAnalyzer.Analyze(merged.Concat(new[] { shared }), Limits()).Rooms.Count, Is.EqualTo(2));
            Assert.That(ConstructionRoomAnalyzer.AnalyzeAffected(merged, new[] { shared }, Limits()).Rooms.Single().Cells.Count, Is.EqualTo(2));
        }

        [Test]
        public void CornerTouchingEnclosuresRemainTwoCanonicalRooms()
        {
            var first = new WorldCellCoordinate(0, 0);
            var second = new WorldCellCoordinate(1, 1);
            ConstructionRoomAnalysis analysis = ConstructionRoomAnalyzer.Analyze(
                Boundary(new[] { first }).Concat(Boundary(new[] { second })).Reverse(),
                Limits());

            Assert.That(analysis.Rooms.Count, Is.EqualTo(2));
            Assert.That(analysis.Rooms[0].Cells, Is.EqualTo(new[] { first }));
            Assert.That(analysis.Rooms[1].Cells, Is.EqualTo(new[] { second }));
        }

        [Test]
        public void TJunctionCreatesThreeRoomsWithoutLosingSharedEdges()
        {
            WorldCellCoordinate[] cells = Rectangle(0, 0, 2, 2);
            List<ConstructionEdgeAddress> edges = Boundary(cells);
            edges.Add(ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(0, 0), ConstructionCellSide.East));
            edges.Add(ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(0, 1), ConstructionCellSide.East));
            edges.Add(ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(0, 0), ConstructionCellSide.North));

            ConstructionRoomAnalysis analysis = ConstructionRoomAnalyzer.Analyze(edges, Limits());
            Assert.That(analysis.Rooms.Count, Is.EqualTo(3));
            Assert.That(
                analysis.Rooms.Select(room => room.Cells.Count),
                Is.EquivalentTo(new[] { 1, 1, 2 }));
        }

        [Test]
        public void OverlappingComponentBoundsAreMergedBeforeFloodFill()
        {
            WorldCellCoordinate[] outer = Rectangle(0, 0, 3, 3);
            var center = new WorldCellCoordinate(1, 1);
            IEnumerable<ConstructionEdgeAddress> edges =
                Boundary(outer).Concat(Boundary(new[] { center }));

            ConstructionRoomAnalysis analysis = ConstructionRoomAnalyzer.Analyze(
                edges,
                new ConstructionRoomAnalysisLimits(1, 25));

            Assert.That(analysis.Rooms.Count, Is.EqualTo(2));
            Assert.That(
                analysis.Rooms.Any(room => room.Cells.SequenceEqual(new[] { center })),
                Is.True);
            Assert.That(analysis.Rooms.Sum(room => room.Cells.Count), Is.EqualTo(9));
        }

        [Test]
        public void CellLimitAndLocalInt64ComponentsAreHandledWithoutOverflow()
        {
            Assert.Throws<InvalidOperationException>(() => ConstructionRoomAnalyzer.Analyze(
                Boundary(Rectangle(0, 0, 4, 4)),
                new ConstructionRoomAnalysisLimits(1, 35)));

            var nearMinimum = new WorldCellCoordinate(
                long.MinValue + 1,
                long.MinValue + 1);
            var nearMaximum = new WorldCellCoordinate(
                long.MaxValue - 1,
                long.MaxValue - 1);
            ConstructionRoomAnalysis extremes = ConstructionRoomAnalyzer.Analyze(
                Boundary(new[] { nearMinimum }).Concat(Boundary(new[] { nearMaximum })),
                new ConstructionRoomAnalysisLimits(2, 9));
            Assert.That(extremes.Rooms.Count, Is.EqualTo(2));
        }

        [Test]
        public void SuccessiveSplitsAndMergesRemainDeterministic()
        {
            WorldCellCoordinate[] cells = Rectangle(0, 0, 2, 2);
            List<ConstructionEdgeAddress> outer = Boundary(cells);
            var vertical = new[]
            {
                ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(0, 0), ConstructionCellSide.East),
                ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(0, 1), ConstructionCellSide.East)
            };
            var horizontal = new[]
            {
                ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(0, 0), ConstructionCellSide.North),
                ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(1, 0), ConstructionCellSide.North)
            };

            Assert.That(
                ConstructionRoomAnalyzer.Analyze(outer, Limits()).Rooms.Count,
                Is.EqualTo(1));
            Assert.That(
                ConstructionRoomAnalyzer.Analyze(outer.Concat(vertical), Limits()).Rooms.Count,
                Is.EqualTo(2));
            Assert.That(
                ConstructionRoomAnalyzer.Analyze(
                    outer.Concat(vertical).Concat(horizontal).Reverse(), Limits()).Rooms.Count,
                Is.EqualTo(4));
            Assert.That(
                ConstructionRoomAnalyzer.Analyze(outer.Concat(horizontal), Limits()).Rooms.Count,
                Is.EqualTo(2));
            Assert.That(
                ConstructionRoomAnalyzer.Analyze(
                    outer.Reverse<ConstructionEdgeAddress>(), Limits()).Rooms.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void DistantScopesNeverAllocateTheirCombinedWorldRectangle()
        {
            var first = new WorldCellCoordinate(long.MinValue + 2, 0);
            var second = new WorldCellCoordinate(long.MaxValue - 2, 0);
            ConstructionRoomAnalysis rooms = ConstructionRoomAnalyzer.Analyze(
                Boundary(new[] { first }).Concat(Boundary(new[] { second })),
                new ConstructionRoomAnalysisLimits(2, 9));
            Assert.That(rooms.Rooms.Count, Is.EqualTo(2));
        }

        [Test]
        public void ScopeLimitRejectsBeforeLargeAllocation()
        {
            var edges = new[]
            {
                ConstructionEdgeAddress.Create(new WorldCellCoordinate(0, 0), ConstructionCellSide.North),
                ConstructionEdgeAddress.Create(new WorldCellCoordinate(100, 100), ConstructionCellSide.North)
            };
            Assert.Throws<InvalidOperationException>(() => ConstructionRoomAnalyzer.Analyze(
                edges, new ConstructionRoomAnalysisLimits(1, 16)));
        }

        [Test]
        public void CoverageCountsCompletedAndActuallySupportedRoofsWithoutFloat()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            var cells = new[] { new WorldCellCoordinate(0, 0), new WorldCellCoordinate(1, 0) };
            foreach (ConstructionEdgeAddress edge in Boundary(cells))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "wall-" + edge);
            Complete(world, Roof, ConstructionSpaceKey.Roof(cells[0]), "roof-0");
            Complete(world, Roof, ConstructionSpaceKey.Roof(cells[1]), "roof-1");

            ConstructionDerivedRooms supported = BuildRooms(catalog, world, 0);
            Assert.That(supported.Rooms.Single().CompletedRoofCellCount, Is.EqualTo(2));
            Assert.That(supported.Rooms.Single().SupportedRoofCellCount, Is.EqualTo(2));
            Assert.That(supported.Rooms.Single().SupportedCoverage.ToString(), Is.EqualTo("2/2"));

            ConstructionDerivedRooms unsupported = BuildRooms(
                catalog, world,
                maximumRoofDistance: 0,
                supportingIds: new[] { OtherEdge });
            Assert.That(unsupported.Rooms.Single().CompletedRoofCellCount, Is.EqualTo(2));
            Assert.That(unsupported.Rooms.Single().SupportedRoofCellCount, Is.Zero);
            Assert.That(unsupported.Rooms.Single().IsFullySupportedRoofed, Is.False);
        }

        [Test]
        public void CoverageDistinguishesAbsentAndPartialRoof()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            WorldCellCoordinate[] cells = Rectangle(0, 0, 2, 1);
            foreach (ConstructionEdgeAddress edge in Boundary(cells))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "wall-" + edge);

            ConstructionRoomCoverage absent = BuildRooms(catalog, world, 1).Rooms.Single();
            Assert.That(absent.CompletedRoofCellCount, Is.Zero);
            Assert.That(absent.SupportedRoofCellCount, Is.Zero);
            Assert.That(absent.IsFullySupportedRoofed, Is.False);

            Complete(world, Roof, ConstructionSpaceKey.Roof(cells[0]), "roof-partial");
            ConstructionRoomCoverage partial = BuildRooms(catalog, world, 1).Rooms.Single();
            Assert.That(partial.CompletedRoofCellCount, Is.EqualTo(1));
            Assert.That(partial.SupportedRoofCellCount, Is.EqualTo(1));
            Assert.That(partial.SupportedCoverage, Is.EqualTo(new ConstructionCoverageRatio(1, 2)));
            Assert.That(partial.IsFullySupportedRoofed, Is.False);
        }

        [Test]
        public void SyntheticCandidatePolicyCanRequireInteriorFloorRoofAndBoundaries()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            ConstructionWorldState world = CreateValidShelterWorld(catalog, "a", new WorldCellCoordinate(0, 0));
            ShelterEvaluation evaluation = EvaluateShelters(catalog, world);
            ShelterAssessment shelter = evaluation.Assessments.Single();
            Assert.That(shelter.IsValid, Is.True);
            Assert.That(shelter.AdjacentBoundaryCount, Is.EqualTo(4));
            Assert.That(shelter.Id, Is.EqualTo(new ShelterId("candidate-anchor:a")));
        }

        [Test]
        public void CandidatePolicyDoesNotRequireInteriorSurfaceBoundariesOrRoofGameplayRules()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            var cell = new WorldCellCoordinate(0, 0);
            foreach (ConstructionEdgeAddress edge in Boundary(new[] { cell }))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "wall-" + edge);
            CompletedStructureState source = Complete(
                world,
                Roof,
                ConstructionSpaceKey.Roof(cell),
                "source");
            CompletedStructureState[] structures = world.CaptureCanonicalStructures();
            ConstructionDerivedRooms rooms = ConstructionDerivedRoomBuilder.Build(
                catalog,
                structures,
                new ConstructionEnclosureBlockingPolicy(new[] { Wall }),
                Limits(),
                new ConstructionRoofSupportPolicy(new[] { Wall }, 0));

            ShelterEvaluation evaluation = ShelterEvaluator.Evaluate(
                catalog,
                structures,
                rooms,
                new ShelterCandidateEvaluationPolicy(
                    Array.Empty<ConstructionDefinitionId>(),
                    new[] { Roof },
                    Array.Empty<ConstructionDefinitionId>(),
                    false,
                    0,
                    false),
                new AnchorBasedShelterIdentityCandidateStrategy());

            Assert.That(evaluation.Assessments.Single().IsValid, Is.True);
            Assert.That(
                evaluation.Assessments.Single().CandidateSourceInstanceId,
                Is.EqualTo(source.InstanceId));
        }

        [Test]
        public void SyntheticAnchorCandidateKeepsIdentityOnlyInContainingRoomAfterSplit()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            var left = new WorldCellCoordinate(0, 0);
            var right = new WorldCellCoordinate(1, 0);
            foreach (ConstructionEdgeAddress edge in Boundary(new[] { left, right }))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "wall-" + edge);
            foreach (WorldCellCoordinate cell in new[] { left, right })
            {
                Complete(world, Floor, ConstructionSpaceKey.Surface(cell), "floor-" + cell);
                Complete(world, Roof, ConstructionSpaceKey.Roof(cell), "roof-" + cell);
            }
            Complete(world, Bed, ConstructionSpaceKey.Interior(left), "anchor");
            ShelterEvaluation before = EvaluateShelters(catalog, world);
            Assert.That(before.Assessments.Single().Id, Is.EqualTo(new ShelterId("candidate-anchor:anchor")));

            Complete(world, Wall, ConstructionSpaceKey.Edge(left, ConstructionCellSide.East), "divider");
            ShelterEvaluation after = EvaluateShelters(catalog, world);
            Assert.That(after.Assessments.Count, Is.EqualTo(1));
            Assert.That(after.Assessments[0].Id, Is.EqualTo(new ShelterId("candidate-anchor:anchor")));
            Assert.That(after.Assessments[0].Room.Room.Cells, Is.EqualTo(new[] { left }));
        }

        [Test]
        public void SyntheticAnchorCandidatesRemainSeparateAndInvalidAfterMerge()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            var left = new WorldCellCoordinate(0, 0);
            var right = new WorldCellCoordinate(1, 0);
            foreach (ConstructionEdgeAddress edge in Boundary(new[] { left, right }))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "wall-" + edge);
            foreach (WorldCellCoordinate cell in new[] { left, right })
            {
                Complete(world, Floor, ConstructionSpaceKey.Surface(cell), "floor-" + cell);
                Complete(world, Roof, ConstructionSpaceKey.Roof(cell), "roof-" + cell);
                Complete(world, Bed, ConstructionSpaceKey.Interior(cell), cell.Equals(left) ? "a" : "b");
            }
            ShelterEvaluation evaluation = EvaluateShelters(catalog, world);
            Assert.That(evaluation.Assessments.Select(value => value.Id),
                Is.EqualTo(new[]
                {
                    new ShelterId("candidate-anchor:a"),
                    new ShelterId("candidate-anchor:b")
                }));
            Assert.That(evaluation.Assessments.All(value => !value.IsValid), Is.True);
            Assert.That(
                evaluation.Assessments.All(value =>
                    value.InvalidReason
                    == ShelterInvalidReason.MultipleCandidateSourcesInRoom),
                Is.True);
        }

        [Test]
        public void CandidateIdentityStrategyCannotReturnDefaultShelterId()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            ConstructionWorldState world = CreateValidShelterWorld(
                catalog,
                "invalid-id",
                new WorldCellCoordinate(0, 0));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EvaluateShelters(
                    catalog,
                    world,
                    new InvalidCandidateIdentityStrategy()));

            StringAssert.Contains("invalid identifier", exception.Message);
        }

        [Test]
        public void CandidateIdentityStrategyCannotReturnDuplicateShelterIds()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            var world = new ConstructionWorldState(catalog);
            WorldCellCoordinate[] cells =
            {
                new WorldCellCoordinate(0, 0),
                new WorldCellCoordinate(3, 0)
            };
            foreach (WorldCellCoordinate cell in cells)
            {
                foreach (ConstructionEdgeAddress edge in Boundary(new[] { cell }))
                    Complete(world, Wall, ConstructionSpaceKey.Edge(edge),
                        "wall-" + cell + "-" + edge);
                Complete(world, Floor, ConstructionSpaceKey.Surface(cell),
                    "floor-" + cell);
                Complete(world, Roof, ConstructionSpaceKey.Roof(cell),
                    "roof-" + cell);
                Complete(world, Bed, ConstructionSpaceKey.Interior(cell),
                    "source-" + cell);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EvaluateShelters(
                    catalog,
                    world,
                    new ConstantCandidateIdentityStrategy()));

            StringAssert.Contains("duplicate identifier", exception.Message);
        }

        [Test]
        public void FamiliarityUsesValidatedHalfPointRulesAndNeverDecays()
        {
            var state = new ShelterFamiliarityState(new ShelterId("s"));
            var rules = new ShelterFamiliarityRules(10);
            state.RecordPresence(9, rules);
            Assert.That(state.FamiliarityHalfPoints, Is.Zero);
            state.RecordPresence(1, rules);
            state.RecordCompletedRest();
            state.RecordCompletedNight();
            Assert.That(state.FamiliarityHalfPoints, Is.EqualTo(19));
            Assert.That(state.TotalPresenceTicks, Is.EqualTo(10));
            Assert.That(state.CompletedRestCount, Is.EqualTo(1));
            Assert.That(state.CompletedNightCount, Is.EqualTo(1));
            state.RecordPresence(0, rules);
            Assert.That(state.FamiliarityHalfPoints, Is.EqualTo(19));
        }

        [TestCase(59, ShelterFamiliarityLevel.Unknown)]
        [TestCase(60, ShelterFamiliarityLevel.Familiar)]
        [TestCase(139, ShelterFamiliarityLevel.Familiar)]
        [TestCase(140, ShelterFamiliarityLevel.VeryFamiliar)]
        public void FamiliarityLevelsUseExactThresholds(int halfPoints, ShelterFamiliarityLevel expected)
        {
            Assert.That(new ShelterFamiliarityState(new ShelterId("s"), 0, 0, 0, 0, halfPoints).Level, Is.EqualTo(expected));
        }

        [TestCase(ShelterFamiliarityLevel.Unknown, false, 75)]
        [TestCase(ShelterFamiliarityLevel.Familiar, false, 85)]
        [TestCase(ShelterFamiliarityLevel.VeryFamiliar, false, 90)]
        [TestCase(ShelterFamiliarityLevel.Unknown, true, 100)]
        [TestCase(ShelterFamiliarityLevel.Familiar, true, 100)]
        [TestCase(ShelterFamiliarityLevel.VeryFamiliar, true, 100)]
        public void SleepRecoveryLimitUsesValidatedFamiliarityAndPrimaryHomeCeilings(
            ShelterFamiliarityLevel familiarity,
            bool isPrimaryHome,
            int expectedPercent)
        {
            Assert.That(
                ShelterSleepRecoveryLimit.MaximumPercent(familiarity, isPrimaryHome),
                Is.EqualTo(expectedPercent));
        }

        [Test]
        public void FamiliarityCapsAtOneHundredAndCountersFailSafelyAtBounds()
        {
            var state = new ShelterFamiliarityState(new ShelterId("cap"));
            state.RecordPresence(long.MaxValue, new ShelterFamiliarityRules(1));
            Assert.That(state.FamiliarityHalfPoints, Is.EqualTo(200));
            Assert.That(state.TotalPresenceTicks, Is.EqualTo(long.MaxValue));
            Assert.Throws<OverflowException>(() =>
                state.RecordPresence(1, new ShelterFamiliarityRules(1)));
            Assert.That(state.TotalPresenceTicks, Is.EqualTo(long.MaxValue));

            var rests = new ShelterFamiliarityState(
                new ShelterId("rests"), 0, 0, int.MaxValue, 0, 0);
            var nights = new ShelterFamiliarityState(
                new ShelterId("nights"), 0, 0, 0, int.MaxValue, 0);
            Assert.Throws<OverflowException>(() => rests.RecordCompletedRest());
            Assert.Throws<OverflowException>(() => nights.RecordCompletedNight());
            Assert.That(rests.CompletedRestCount, Is.EqualTo(int.MaxValue));
            Assert.That(nights.CompletedNightCount, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void HomeChangesOnlyAfterThreeNightsAndFifteenPointAdvantage()
        {
            var current = new ShelterFamiliarityState(new ShelterId("current"), 0, 0, 0, 3, 100);
            var candidate = new ShelterFamiliarityState(new ShelterId("candidate"), 0, 0, 0, 2, 130);
            var homes = new ShelterHomeState(new[] { current, candidate }, current.ShelterId, true);
            Assert.That(homes.RecalculatePrimary(new[] { current.ShelterId, candidate.ShelterId }), Is.False);
            candidate.RecordCompletedNight();
            Assert.That(homes.RecalculatePrimary(new[] { current.ShelterId, candidate.ShelterId }), Is.True);
            Assert.That(homes.PrimaryShelterId, Is.EqualTo(candidate.ShelterId));
        }

        [Test]
        public void HomeKeepsCurrentAtFourteenAndAHalfButChangesAtExactFifteen()
        {
            var current = new ShelterFamiliarityState(
                new ShelterId("current"), 0, 0, 0, 3, 100);
            var below = new ShelterFamiliarityState(
                new ShelterId("below"), 0, 0, 0, 3, 129);
            var exact = new ShelterFamiliarityState(
                new ShelterId("exact"), 0, 0, 0, 3, 130);
            var homes = new ShelterHomeState(
                new[] { current, below, exact }, current.ShelterId, true);

            Assert.That(
                homes.RecalculatePrimary(new[] { current.ShelterId, below.ShelterId }),
                Is.False);
            Assert.That(homes.PrimaryShelterId, Is.EqualTo(current.ShelterId));
            Assert.That(
                homes.RecalculatePrimary(
                    new[] { current.ShelterId, below.ShelterId, exact.ShelterId }),
                Is.True);
            Assert.That(homes.PrimaryShelterId, Is.EqualTo(exact.ShelterId));
        }

        [Test]
        public void HomeChoosesBestQualifiedChallengerIndependentOfValidIdOrder()
        {
            var current = new ShelterFamiliarityState(
                new ShelterId("current"), 0, 0, 0, 3, 100);
            var b = new ShelterFamiliarityState(new ShelterId("b"), 0, 0, 0, 3, 130);
            var c = new ShelterFamiliarityState(new ShelterId("c"), 0, 0, 0, 4, 150);
            var first = new ShelterHomeState(
                new[] { current, b, c }, current.ShelterId, true);
            var second = new ShelterHomeState(
                new[] { c, b, current }, current.ShelterId, true);

            first.RecalculatePrimary(new[] { current.ShelterId, b.ShelterId, c.ShelterId });
            second.RecalculatePrimary(new[] { c.ShelterId, b.ShelterId, current.ShelterId });
            Assert.That(first.PrimaryShelterId, Is.EqualTo(c.ShelterId));
            Assert.That(second.PrimaryShelterId, Is.EqualTo(c.ShelterId));
        }

        [Test]
        public void InvalidatedHomeKeepsPrimaryIdentityAndHistoryWithoutReplacement()
        {
            var history = new ShelterFamiliarityState(
                new ShelterId("home"), 0, 0, 0, 3, 140);
            var challenger = new ShelterFamiliarityState(
                new ShelterId("challenger"), 0, 0, 0, 5, 200);
            var homes = new ShelterHomeState(
                new[] { history, challenger }, history.ShelterId, true);

            Assert.That(
                homes.RecalculatePrimary(new[] { challenger.ShelterId }),
                Is.False);
            Assert.That(homes.HasPrimaryShelter, Is.True);
            Assert.That(homes.PrimaryShelterId, Is.EqualTo(history.ShelterId));
            Assert.That(homes.TryFind(history.ShelterId, out ShelterFamiliarityState preserved), Is.True);
            Assert.That(preserved, Is.SameAs(history));
            Assert.That(homes.RecalculatePrimary(new[] { history.ShelterId }), Is.False);
            Assert.That(homes.PrimaryShelterId, Is.EqualTo(history.ShelterId));
        }

        [Test]
        public void MissingPrimaryDoesNotPromoteQualifiedHistories()
        {
            var b = new ShelterFamiliarityState(new ShelterId("b"), 0, 0, 0, 3, 80);
            var a = new ShelterFamiliarityState(new ShelterId("a"), 0, 0, 0, 3, 80);
            var homes = new ShelterHomeState(new[] { b, a });
            Assert.That(
                homes.RecalculatePrimary(new[] { b.ShelterId, a.ShelterId }),
                Is.False);
            Assert.That(homes.HasPrimaryShelter, Is.False);
            Assert.That(homes.PrimaryShelterId.IsValid, Is.False);
            Assert.That(homes.CaptureCanonicalHistories().Count, Is.EqualTo(2));
        }

        [Test]
        public void InitialCampUsesSeventyPointsThreeNightsAndPrimaryStatus()
        {
            ShelterHomeState camp = ShelterHomeState.CreateInitialCamp(new ShelterId("camp"));
            Assert.That(camp.HasPrimaryShelter, Is.True);
            Assert.That(camp.PrimaryShelterId, Is.EqualTo(new ShelterId("camp")));
            Assert.That(camp.CaptureCanonicalHistories().Single().FamiliarityHalfPoints, Is.EqualTo(140));
            Assert.That(camp.CaptureCanonicalHistories().Single().CompletedNightCount, Is.EqualTo(3));
        }

        [Test]
        public void RealRoomQualificationRequiresEveryFloorAndSupportedRoofAndUsesGeometryFingerprint()
        {
            ConstructionDefinitionCatalog catalog = CreateCatalog();
            WorldCellCoordinate[] cells = Rectangle(-1, -1, 2, 1);
            ConstructionWorldState world = new ConstructionWorldState(catalog);
            foreach (ConstructionEdgeAddress edge in Boundary(cells))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), "edge-" + edge);
            foreach (WorldCellCoordinate cell in cells)
            {
                Complete(world, Floor, ConstructionSpaceKey.Surface(cell), "floor-" + cell);
                Complete(world, Roof, ConstructionSpaceKey.Roof(cell), "roof-" + cell);
            }
            ConstructionDerivedRooms rooms = ConstructionDerivedRoomBuilder.Build(catalog, world.CaptureCanonicalStructures(),
                new ConstructionEnclosureBlockingPolicy(new[] { Wall }), Limits(), new ConstructionRoofSupportPolicy(new[] { Wall }, 2));
            var identity = new RoomFingerprintIdentityStrategy();
            ShelterEvaluation evaluation = RoomShelterEvaluator.Evaluate(world.CaptureCanonicalStructures(), Floor,
                rooms, new FullFloorSupportedRoofShelterPolicy(), identity);
            Assert.That(evaluation.Assessments.Single().IsValid, Is.True);
            ShelterId id = evaluation.Assessments.Single().Id;
            Assert.That(id.Value.StartsWith("room-v1-sha256:"), Is.True);
            Assert.That(identity.CreateId(cells.Reverse()), Is.EqualTo(id));
            Assert.That(identity.CreateId(new[] { cells[0] }), Is.Not.EqualTo(id));
            var colliding = new RoomFingerprintIdentityStrategy(_ => new byte[32]);
            colliding.CreateId(new[] { cells[0] });
            Assert.Throws<InvalidOperationException>(() => colliding.CreateId(cells));
        }

        private static ConstructionDerivedRooms BuildRooms(
            ConstructionDefinitionCatalog catalog,
            ConstructionWorldState world,
            int maximumRoofDistance,
            IEnumerable<ConstructionDefinitionId> supportingIds = null)
        {
            return ConstructionDerivedRoomBuilder.Build(
                catalog, world.CaptureCanonicalStructures(),
                new ConstructionEnclosureBlockingPolicy(new[] { Wall, Opening }),
                Limits(), new ConstructionRoofSupportPolicy(supportingIds ?? new[] { Wall, Opening }, maximumRoofDistance));
        }

        private static ShelterEvaluation EvaluateShelters(
            ConstructionDefinitionCatalog catalog,
            ConstructionWorldState world,
            IShelterCandidateIdentityStrategy identityStrategy = null)
        {
            CompletedStructureState[] structures = world.CaptureCanonicalStructures();
            ConstructionDerivedRooms rooms = ConstructionDerivedRoomBuilder.Build(
                catalog, structures, new ConstructionEnclosureBlockingPolicy(new[] { Wall, Opening }),
                Limits(), new ConstructionRoofSupportPolicy(new[] { Wall, Opening }, 2));
            return ShelterEvaluator.Evaluate(
                catalog, structures, rooms,
                new ShelterCandidateEvaluationPolicy(
                    new[] { Floor },
                    new[] { Bed },
                    new[] { Wall, Opening },
                    true,
                    2,
                    true),
                identityStrategy
                    ?? new AnchorBasedShelterIdentityCandidateStrategy());
        }

        private static ConstructionWorldState CreateValidShelterWorld(
            ConstructionDefinitionCatalog catalog, string prefix, WorldCellCoordinate cell)
        {
            var world = new ConstructionWorldState(catalog);
            foreach (ConstructionEdgeAddress edge in Boundary(new[] { cell }))
                Complete(world, Wall, ConstructionSpaceKey.Edge(edge), prefix + "-wall-" + edge);
            Complete(world, Floor, ConstructionSpaceKey.Surface(cell), prefix + "-floor");
            Complete(world, Roof, ConstructionSpaceKey.Roof(cell), prefix + "-roof");
            Complete(world, Bed, ConstructionSpaceKey.Interior(cell), prefix);
            return world;
        }

        private static List<ConstructionEdgeAddress> Boundary(IEnumerable<WorldCellCoordinate> source)
        {
            var cells = new HashSet<WorldCellCoordinate>(source);
            var edges = new HashSet<ConstructionEdgeAddress>();
            ConstructionCellSide[] sides = { ConstructionCellSide.North, ConstructionCellSide.East, ConstructionCellSide.South, ConstructionCellSide.West };
            foreach (WorldCellCoordinate cell in cells)
                foreach (ConstructionCellSide side in sides)
                    if (ConstructionEdgeAddress.TryCreate(cell, side, out ConstructionEdgeAddress edge))
                    {
                        WorldCellCoordinate neighbour = edge.FirstCell.Equals(cell) ? edge.SecondCell : edge.FirstCell;
                        if (!cells.Contains(neighbour)) edges.Add(edge);
                    }
            var result = new List<ConstructionEdgeAddress>(edges);
            result.Sort();
            return result;
        }

        private static WorldCellCoordinate[] Rectangle(long minX, long minY, int width, int height)
        {
            var cells = new List<WorldCellCoordinate>(checked(width * height));
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells.Add(new WorldCellCoordinate(checked(minX + x), checked(minY + y)));
            cells.Sort();
            return cells.ToArray();
        }

        private static ConstructionRoomAnalysisLimits Limits() => new ConstructionRoomAnalysisLimits(32, 4096);

        private static ConstructionDefinitionCatalog CreateCatalog()
        {
            return new ConstructionDefinitionCatalog(new[]
            {
                Definition(Floor, ConstructionSpaceKind.Surface),
                Definition(Wall, ConstructionSpaceKind.Edge),
                Definition(Opening, ConstructionSpaceKind.Edge),
                Definition(OtherEdge, ConstructionSpaceKind.Edge),
                Definition(Roof, ConstructionSpaceKind.Roof),
                Definition(Bed, ConstructionSpaceKind.Interior)
            });
        }

        private static ConstructionDefinition Definition(ConstructionDefinitionId id, ConstructionSpaceKind kind) =>
            new ConstructionDefinition(id, kind, 1, new[] { new ConstructionMaterialRequirement(Material, 1) });

        private static CompletedStructureState Complete(
            ConstructionWorldState world, ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space, string instance)
        {
            var id = new ConstructionInstanceId(instance);
            Assert.That(world.TryStartSite(id, definitionId, space, out _, out _), Is.True);
            Assert.That(world.DepositMaterial(id, Material, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.AddWork(id, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.TryComplete(id, out CompletedStructureState completed, out _), Is.True);
            return completed;
        }

        private sealed class InvalidCandidateIdentityStrategy :
            IShelterCandidateIdentityStrategy
        {
            public ShelterId CreateCandidateId(CompletedStructureState candidateSource) =>
                default;
        }

        private sealed class ConstantCandidateIdentityStrategy :
            IShelterCandidateIdentityStrategy
        {
            public ShelterId CreateCandidateId(CompletedStructureState candidateSource) =>
                new ShelterId("constant-candidate");
        }
    }
}
