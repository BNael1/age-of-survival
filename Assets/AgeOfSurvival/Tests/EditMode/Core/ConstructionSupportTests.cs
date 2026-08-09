using System;
using System.Linq;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Tests.EditMode.Core
{
    public sealed class ConstructionSupportTests
    {
        private static readonly ItemDefinitionId Branch = new ItemDefinitionId("branch");
        private static readonly ConstructionDefinitionId FoundationDefinitionId = new ConstructionDefinitionId("foundation");
        private static readonly ConstructionDefinitionId BeamDefinitionId = new ConstructionDefinitionId("beam");
        private static readonly ConstructionDefinitionId RoofDefinitionId = new ConstructionDefinitionId("roof");

        private static readonly ConstructionInstanceId A = new ConstructionInstanceId("a");
        private static readonly ConstructionInstanceId B = new ConstructionInstanceId("b");
        private static readonly ConstructionInstanceId C = new ConstructionInstanceId("c");
        private static readonly ConstructionInstanceId D = new ConstructionInstanceId("d");

        [Test]
        public void SupportLink_RejectsInvalidProvider()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportLink(default, A));
        }

        [Test]
        public void SupportLink_RejectsInvalidDependent()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportLink(A, default));
        }

        [Test]
        public void SupportLink_RejectsDirectSelfSupport()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportLink(A, A));
        }

        [Test]
        public void SupportLink_IsDirectedAndUsesCanonicalOrdinalOrder()
        {
            var ab = new ConstructionSupportLink(A, B);
            var ba = new ConstructionSupportLink(B, A);
            var ac = new ConstructionSupportLink(A, C);

            Assert.That(ab, Is.Not.EqualTo(ba));
            Assert.That(ab.CompareTo(ac), Is.LessThan(0));
            Assert.That(ab.ToString(), Is.EqualTo("a->b"));
        }

        [Test]
        public void Graph_RejectsNullNodeCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConstructionSupportGraph(
                null,
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsNullRootCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConstructionSupportGraph(
                new[] { A },
                null,
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsNullLinkCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                null,
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsNullRoofCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                null));
        }

        [Test]
        public void Graph_RejectsInvalidNodeIdentifier()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A, default(ConstructionInstanceId) },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsDuplicateNodes()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A, A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsUnknownRoot()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A },
                new[] { B },
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsDuplicateRoots()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A },
                new[] { A, A },
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsUnknownRoof()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                new[] { B }));
        }

        [Test]
        public void Graph_RejectsDuplicateRoofs()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                new[] { A, A }));
        }

        [Test]
        public void Graph_RejectsLinkEndpointOutsideNodes()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A },
                new[] { A },
                new[] { new ConstructionSupportLink(A, B) },
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_RejectsDuplicateLinks()
        {
            ConstructionSupportLink link = new ConstructionSupportLink(A, B);
            Assert.Throws<ArgumentException>(() => new ConstructionSupportGraph(
                new[] { A, B },
                new[] { A },
                new[] { link, link },
                Array.Empty<ConstructionInstanceId>()));
        }

        [Test]
        public void Graph_CanonicalizesNodesRootsRoofsAndLinks()
        {
            var graph = new ConstructionSupportGraph(
                new[] { D, B, C, A },
                new[] { C, A },
                new[]
                {
                    new ConstructionSupportLink(C, D),
                    new ConstructionSupportLink(A, C),
                    new ConstructionSupportLink(A, B)
                },
                new[] { D, B });

            Assert.That(graph.NodeIds, Is.EqualTo(new[] { A, B, C, D }));
            Assert.That(graph.RootIds, Is.EqualTo(new[] { A, C }));
            Assert.That(graph.RoofIds, Is.EqualTo(new[] { B, D }));
            Assert.That(graph.Links, Is.EqualTo(new[]
            {
                new ConstructionSupportLink(A, B),
                new ConstructionSupportLink(A, C),
                new ConstructionSupportLink(C, D)
            }));
        }

        [Test]
        public void EmptyGraph_EvaluatesToEmptySnapshot()
        {
            ConstructionSupportEvaluation evaluation = new ConstructionSupportGraph(
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                Array.Empty<ConstructionInstanceId>()).Evaluate();

            Assert.That(evaluation.SupportedInstanceIds, Is.Empty);
            Assert.That(evaluation.RoofStates, Is.Empty);
        }

        [Test]
        public void Root_IsSupportedWithoutIncomingLink()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A },
                new[] { A },
                Array.Empty<ConstructionSupportLink>()).Evaluate();

            Assert.That(evaluation.ContainsNode(A), Is.True);
            Assert.That(evaluation.IsSupported(A), Is.True);
        }

        [Test]
        public void Support_PropagatesTransitivelyFromRoots()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A, B, C },
                new[] { A },
                new[] { new ConstructionSupportLink(A, B), new ConstructionSupportLink(B, C) }).Evaluate();

            Assert.That(evaluation.IsSupported(A), Is.True);
            Assert.That(evaluation.IsSupported(B), Is.True);
            Assert.That(evaluation.IsSupported(C), Is.True);
        }

        [Test]
        public void DisconnectedNode_RemainsUnsupported()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A, B },
                new[] { A },
                Array.Empty<ConstructionSupportLink>()).Evaluate();

            Assert.That(evaluation.IsSupported(A), Is.True);
            Assert.That(evaluation.IsSupported(B), Is.False);
        }

        [Test]
        public void CycleWithoutRoot_DoesNotSelfSupport()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A, B },
                Array.Empty<ConstructionInstanceId>(),
                new[] { new ConstructionSupportLink(A, B), new ConstructionSupportLink(B, A) }).Evaluate();

            Assert.That(evaluation.IsSupported(A), Is.False);
            Assert.That(evaluation.IsSupported(B), Is.False);
        }

        [Test]
        public void CycleConnectedToRoot_BecomesSupported()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A, B, C },
                new[] { A },
                new[]
                {
                    new ConstructionSupportLink(A, B),
                    new ConstructionSupportLink(B, C),
                    new ConstructionSupportLink(C, B)
                }).Evaluate();

            Assert.That(evaluation.SupportedInstanceIds, Is.EqualTo(new[] { A, B, C }));
        }

        [Test]
        public void IndependentComponents_AreEvaluatedSeparately()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { A, B, C, D },
                new[] { A },
                new[] { new ConstructionSupportLink(A, B), new ConstructionSupportLink(C, D) }).Evaluate();

            Assert.That(evaluation.IsSupported(A), Is.True);
            Assert.That(evaluation.IsSupported(B), Is.True);
            Assert.That(evaluation.IsSupported(C), Is.False);
            Assert.That(evaluation.IsSupported(D), Is.False);
        }

        [Test]
        public void SupportedIdentifiers_AreCanonicalIndependentOfTraversalOrder()
        {
            ConstructionSupportEvaluation evaluation = Graph(
                new[] { D, C, B, A },
                new[] { A },
                new[]
                {
                    new ConstructionSupportLink(A, D),
                    new ConstructionSupportLink(A, C),
                    new ConstructionSupportLink(A, B)
                }).Evaluate();

            Assert.That(evaluation.SupportedInstanceIds, Is.EqualTo(new[] { A, B, C, D }));
        }

        [Test]
        public void SupportedRoof_CountsTowardShelter()
        {
            ConstructionSupportEvaluation evaluation = new ConstructionSupportGraph(
                new[] { A, B },
                new[] { A },
                new[] { new ConstructionSupportLink(A, B) },
                new[] { B }).Evaluate();

            ConstructionRoofSupportState roof = evaluation.RequireRoofState(B);
            Assert.That(roof.IsSupported, Is.True);
            Assert.That(roof.CountsTowardShelter, Is.True);
        }

        [Test]
        public void UnsupportedRoof_RemainsRepresentedButDoesNotCountAsShelter()
        {
            ConstructionSupportEvaluation evaluation = new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                new[] { A }).Evaluate();

            ConstructionRoofSupportState roof = evaluation.RequireRoofState(A);
            Assert.That(roof.IsSupported, Is.False);
            Assert.That(roof.CountsTowardShelter, Is.False);
            Assert.That(evaluation.RoofStates.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGetRoofState_ReturnsFalseForInvalidOrUnknownIdentifier()
        {
            ConstructionSupportEvaluation evaluation = new ConstructionSupportGraph(
                new[] { A },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>(),
                new[] { A }).Evaluate();

            Assert.That(evaluation.TryGetRoofState(default, out _), Is.False);
            Assert.That(evaluation.TryGetRoofState(B, out _), Is.False);
        }

        [Test]
        public void RequireRoofState_ThrowsForNonRoofOrUnknownIdentifier()
        {
            ConstructionSupportEvaluation evaluation = new ConstructionSupportGraph(
                new[] { A, B },
                new[] { A },
                Array.Empty<ConstructionSupportLink>(),
                new[] { B }).Evaluate();

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => evaluation.RequireRoofState(A));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => evaluation.RequireRoofState(C));
        }

        [Test]
        public void CompletedStructureFactory_DerivesNodesAndRoofsCanonically()
        {
            ConstructionWorldState world = CreateWorld();
            Complete(world, C, RoofDefinitionId, ConstructionSpaceKey.Roof(new WorldCellCoordinate(2L, 0L)));
            Complete(world, A, FoundationDefinitionId, ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L)));
            Complete(world, B, RoofDefinitionId, ConstructionSpaceKey.Roof(new WorldCellCoordinate(1L, 0L)));

            ConstructionSupportGraph graph = ConstructionSupportGraph.FromCompletedStructures(
                world.CaptureCanonicalStructures().Reverse(),
                new[] { A },
                new[] { new ConstructionSupportLink(A, B) });

            Assert.That(graph.NodeIds, Is.EqualTo(new[] { A, B, C }));
            Assert.That(graph.RoofIds, Is.EqualTo(new[] { B, C }));
        }

        [Test]
        public void CompletedStructureFactory_RejectsNullStructureEntry()
        {
            Assert.Throws<ArgumentException>(() => ConstructionSupportGraph.FromCompletedStructures(
                new CompletedStructureState[] { null },
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>()));
        }

        [Test]
        public void CompletedStructureFactory_RejectsDuplicateInstanceEntries()
        {
            ConstructionWorldState world = CreateWorld();
            CompletedStructureState structure = Complete(
                world,
                A,
                FoundationDefinitionId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L)));

            Assert.Throws<ArgumentException>(() => ConstructionSupportGraph.FromCompletedStructures(
                new[] { structure, structure },
                new[] { A },
                Array.Empty<ConstructionSupportLink>()));
        }

        [Test]
        public void IncompleteSite_IsNotSilentlyPromotedIntoCompletedSupportGraph()
        {
            ConstructionWorldState world = CreateWorld();
            Assert.That(world.TryStartSite(
                A,
                FoundationDefinitionId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L)),
                out _,
                out _), Is.True);

            Assert.That(world.CaptureCanonicalStructures(), Is.Empty);
            Assert.Throws<ArgumentException>(() => ConstructionSupportGraph.FromCompletedStructures(
                world.CaptureCanonicalStructures(),
                new[] { A },
                Array.Empty<ConstructionSupportLink>()));
        }

        [Test]
        public void RemovingSupport_ReevaluationKeepsRoofStructureButMarksItUnsupported()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionSpaceKey foundationSpace = ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L));
            ConstructionSpaceKey roofSpace = ConstructionSpaceKey.Roof(new WorldCellCoordinate(0L, 0L));
            Complete(world, A, FoundationDefinitionId, foundationSpace);
            Complete(world, B, RoofDefinitionId, roofSpace);

            ConstructionSupportEvaluation before = ConstructionSupportGraph.FromCompletedStructures(
                world.CaptureCanonicalStructures(),
                new[] { A },
                new[] { new ConstructionSupportLink(A, B) }).Evaluate();
            Assert.That(before.RequireRoofState(B).CountsTowardShelter, Is.True);

            Assert.That(world.TryDismantleStructure(A, out _, out _), Is.True);
            Assert.That(world.TryFindStructure(B, out CompletedStructureState roof), Is.True);
            Assert.That(roof.Space, Is.EqualTo(roofSpace));
            Assert.That(world.IsOccupied(roofSpace), Is.True);

            ConstructionSupportEvaluation after = ConstructionSupportGraph.FromCompletedStructures(
                world.CaptureCanonicalStructures(),
                Array.Empty<ConstructionInstanceId>(),
                Array.Empty<ConstructionSupportLink>()).Evaluate();

            Assert.That(after.RequireRoofState(B).IsSupported, Is.False);
            Assert.That(after.RequireRoofState(B).CountsTowardShelter, Is.False);
            Assert.That(world.TryFindStructure(B, out _), Is.True);
            Assert.That(world.StructureCount, Is.EqualTo(1));
        }

        private static ConstructionSupportGraph Graph(
            ConstructionInstanceId[] nodes,
            ConstructionInstanceId[] roots,
            ConstructionSupportLink[] links)
        {
            return new ConstructionSupportGraph(
                nodes,
                roots,
                links,
                Array.Empty<ConstructionInstanceId>());
        }

        private static ConstructionWorldState CreateWorld()
        {
            return new ConstructionWorldState(new ConstructionDefinitionCatalog(new[]
            {
                Definition(FoundationDefinitionId, ConstructionSpaceKind.Surface),
                Definition(BeamDefinitionId, ConstructionSpaceKind.Interior),
                Definition(RoofDefinitionId, ConstructionSpaceKind.Roof)
            }));
        }

        private static ConstructionDefinition Definition(
            ConstructionDefinitionId id,
            ConstructionSpaceKind kind)
        {
            return new ConstructionDefinition(
                id,
                kind,
                1,
                new[] { new ConstructionMaterialRequirement(Branch, 1) });
        }

        private static CompletedStructureState Complete(
            ConstructionWorldState world,
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            Assert.That(world.TryStartSite(instanceId, definitionId, space, out _, out ConstructionOperationReason startReason), Is.True);
            Assert.That(startReason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(world.DepositMaterial(instanceId, Branch, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.AddWork(instanceId, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.TryComplete(instanceId, out CompletedStructureState structure, out ConstructionOperationReason completeReason), Is.True);
            Assert.That(completeReason, Is.EqualTo(ConstructionOperationReason.None));
            return structure;
        }
    }
}
