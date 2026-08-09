using System;
using System.Linq;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Tests.EditMode.Core
{
    public sealed class ConstructionLifecycleTests
    {
        private static readonly ItemDefinitionId Branch = new ItemDefinitionId("branch");
        private static readonly ItemDefinitionId Stone = new ItemDefinitionId("stone");
        private static readonly ItemDefinitionId Wood = new ItemDefinitionId("wood");
        private static readonly ConstructionDefinitionId WallId = new ConstructionDefinitionId("wood-wall");

        [Test]
        public void ConstructionIdentifiers_DefaultInvalid_AndUseOrdinalIdentity()
        {
            ConstructionDefinitionId invalidDefinition = default;
            ConstructionInstanceId invalidInstance = default;
            var lower = new ConstructionDefinitionId("wall-a");
            var upper = new ConstructionDefinitionId("wall-b");

            Assert.That(invalidDefinition.IsValid, Is.False);
            Assert.That(invalidInstance.IsValid, Is.False);
            Assert.That(lower.CompareTo(upper), Is.LessThan(0));
            Assert.That(new ConstructionDefinitionId("wall-a"), Is.EqualTo(lower));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConstructionDefinitionId_RejectsEmptyValues(string value)
        {
            Assert.Throws<ArgumentException>(() => new ConstructionDefinitionId(value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConstructionInstanceId_RejectsEmptyValues(string value)
        {
            Assert.Throws<ArgumentException>(() => new ConstructionInstanceId(value));
        }

        [Test]
        public void MaterialRequirement_RejectsInvalidIdentifierAndNonPositiveQuantity()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionMaterialRequirement(default, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConstructionMaterialRequirement(Branch, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConstructionMaterialRequirement(Branch, -1));
        }

        [Test]
        public void Definition_CanonicalizesMaterialsByItemIdentifier()
        {
            ConstructionDefinition definition = CreateDefinition(
                ConstructionSpaceKind.Edge,
                10,
                new ConstructionMaterialRequirement(Wood, 2),
                new ConstructionMaterialRequirement(Branch, 4),
                new ConstructionMaterialRequirement(Stone, 3));

            Assert.That(
                definition.Materials.Select(material => material.DefinitionId).ToArray(),
                Is.EqualTo(new[] { Branch, Stone, Wood }));
        }

        [Test]
        public void Definition_RejectsDuplicateMaterials()
        {
            Assert.Throws<ArgumentException>(() => CreateDefinition(
                ConstructionSpaceKind.Edge,
                10,
                new ConstructionMaterialRequirement(Branch, 1),
                new ConstructionMaterialRequirement(Branch, 2)));
        }

        [Test]
        public void Definition_RejectsEmptyMaterialsAndNonPositiveWork()
        {
            Assert.Throws<ArgumentException>(() => new ConstructionDefinition(
                WallId,
                ConstructionSpaceKind.Edge,
                10,
                Array.Empty<ConstructionMaterialRequirement>()));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ConstructionDefinition(
                WallId,
                ConstructionSpaceKind.Edge,
                0,
                new[] { new ConstructionMaterialRequirement(Branch, 1) }));
        }

        [Test]
        public void Catalog_SortsDefinitionsAndRejectsDuplicateIds()
        {
            ConstructionDefinition b = CreateDefinition(
                new ConstructionDefinitionId("b"),
                ConstructionSpaceKind.Surface,
                1,
                new ConstructionMaterialRequirement(Stone, 1));
            ConstructionDefinition a = CreateDefinition(
                new ConstructionDefinitionId("a"),
                ConstructionSpaceKind.Surface,
                1,
                new ConstructionMaterialRequirement(Stone, 1));
            var catalog = new ConstructionDefinitionCatalog(new[] { b, a });

            Assert.That(catalog.Definitions.Select(definition => definition.Id).ToArray(), Is.EqualTo(new[] { a.Id, b.Id }));
            Assert.Throws<ArgumentException>(() => new ConstructionDefinitionCatalog(new[] { a, a }));
        }

        [Test]
        public void StartSite_OccupiesSpaceAndPreservesStableInstanceIdentity()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId instanceId = new ConstructionInstanceId("site-001");
            ConstructionSpaceKey space = WallSpace(0L, 0L);

            bool started = world.TryStartSite(instanceId, WallId, space, out ConstructionSiteState site, out ConstructionOperationReason reason);

            Assert.That(started, Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(site.InstanceId, Is.EqualTo(instanceId));
            Assert.That(site.DefinitionId, Is.EqualTo(WallId));
            Assert.That(site.Space, Is.EqualTo(space));
            Assert.That(world.IsOccupied(space), Is.True);
            Assert.That(world.SiteCount, Is.EqualTo(1));
            Assert.That(world.StructureCount, Is.Zero);
        }

        [Test]
        public void StartSite_RejectsSpaceKindMismatchWithoutOccupying()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L));

            bool started = world.TryStartSite(
                new ConstructionInstanceId("site-001"),
                WallId,
                surface,
                out _,
                out ConstructionOperationReason reason);

            Assert.That(started, Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.SpaceKindMismatch));
            Assert.That(world.IsOccupied(surface), Is.False);
            Assert.That(world.SiteCount, Is.Zero);
        }

        [Test]
        public void StartSite_RejectsCanonicalEdgeAliasCollision()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionSpaceKey east = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(31L, 5L),
                ConstructionCellSide.East);
            ConstructionSpaceKey west = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(32L, 5L),
                ConstructionCellSide.West);

            Assert.That(world.TryStartSite(new ConstructionInstanceId("a"), WallId, east, out _, out _), Is.True);
            Assert.That(
                world.TryStartSite(new ConstructionInstanceId("b"), WallId, west, out _, out ConstructionOperationReason reason),
                Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.SpaceOccupied));
            Assert.That(world.SiteCount, Is.EqualTo(1));
            Assert.That(world.OccupiedSpaceCount, Is.EqualTo(1));
        }

        [Test]
        public void StartSite_RejectsDuplicateInstanceWithoutOccupyingSecondSpace()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId id = new ConstructionInstanceId("same-id");
            ConstructionSpaceKey first = WallSpace(0L, 0L);
            ConstructionSpaceKey second = WallSpace(5L, 0L);

            Assert.That(world.TryStartSite(id, WallId, first, out _, out _), Is.True);
            Assert.That(world.TryStartSite(id, WallId, second, out _, out ConstructionOperationReason reason), Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.DuplicateInstance));
            Assert.That(world.IsOccupied(second), Is.False);
        }

        [Test]
        public void DepositMaterial_AcceptsPartialAndCapsAtRequirement()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            ConstructionQuantityResult first = world.DepositMaterial(id, Branch, 2);
            ConstructionQuantityResult second = world.DepositMaterial(id, Branch, 99);
            ConstructionQuantityResult third = world.DepositMaterial(id, Branch, 1);

            Assert.That(first.Accepted, Is.EqualTo(2));
            Assert.That(second.Accepted, Is.EqualTo(2));
            Assert.That(third.Accepted, Is.Zero);
            Assert.That(third.Reason, Is.EqualTo(ConstructionOperationReason.MaterialRequirementSatisfied));
            Assert.That(world.CaptureDepositedMaterials(id), Is.EqualTo(new[] { new ConstructionMaterialQuantity(Branch, 4) }));
        }

        [Test]
        public void DepositMaterial_RejectsUnrequiredMaterialWithoutMutation()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            ConstructionQuantityResult result = world.DepositMaterial(id, Wood, 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ConstructionOperationReason.MaterialNotRequired));
            Assert.That(world.CaptureDepositedMaterials(id), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void DepositMaterial_RejectsNonPositiveRequest(int quantity)
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            ConstructionQuantityResult result = world.DepositMaterial(id, Branch, quantity);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ConstructionOperationReason.InvalidRequest));
            Assert.That(world.CaptureDepositedMaterials(id), Is.Empty);
        }

        [Test]
        public void DepositedMaterials_AreCapturedInDefinitionCanonicalOrder()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);
            world.DepositMaterial(id, Stone, 2);
            world.DepositMaterial(id, Branch, 3);

            ConstructionMaterialQuantity[] captured = world.CaptureDepositedMaterials(id);

            Assert.That(captured, Is.EqualTo(new[]
            {
                new ConstructionMaterialQuantity(Branch, 3),
                new ConstructionMaterialQuantity(Stone, 2)
            }));
        }

        [Test]
        public void AddWork_AcceptsPartialAndCapsAtRequirement()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            ConstructionQuantityResult first = world.AddWork(id, 40);
            ConstructionQuantityResult second = world.AddWork(id, int.MaxValue);
            ConstructionQuantityResult third = world.AddWork(id, 1);

            Assert.That(first.Accepted, Is.EqualTo(40));
            Assert.That(second.Accepted, Is.EqualTo(60));
            Assert.That(third.Accepted, Is.Zero);
            Assert.That(third.Reason, Is.EqualTo(ConstructionOperationReason.WorkRequirementSatisfied));
            Assert.That(world.TryFindSite(id, out ConstructionSiteState site), Is.True);
            Assert.That(site.WorkCompletedUnits, Is.EqualTo(100));
        }

        [Test]
        public void WorkAndMaterialProgress_AreIndependentUntilCompletionCheck()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            world.AddWork(id, 100);

            Assert.That(world.TryFindSite(id, out ConstructionSiteState site), Is.True);
            Assert.That(site.WorkCompletedUnits, Is.EqualTo(100));
            Assert.That(world.CaptureDepositedMaterials(id), Is.Empty);
            Assert.That(world.IsReadyToComplete(id), Is.False);
        }

        [Test]
        public void Completion_RejectsIncompleteRequirementsWithoutMutation()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);
            world.DepositMaterial(id, Branch, 4);
            world.DepositMaterial(id, Stone, 3);
            world.AddWork(id, 99);

            bool completed = world.TryComplete(id, out _, out ConstructionOperationReason reason);

            Assert.That(completed, Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.RequirementsIncomplete));
            Assert.That(world.SiteCount, Is.EqualTo(1));
            Assert.That(world.StructureCount, Is.Zero);
            Assert.That(world.IsOccupied(WallSpace(0L, 0L)), Is.True);
        }

        [Test]
        public void Completion_ReplacesSiteAtomicallyAndKeepsSpaceOccupied()
        {
            ConstructionWorldState world = CreateReadyWorld(out ConstructionInstanceId id, out ConstructionSpaceKey space);

            bool completed = world.TryComplete(id, out CompletedStructureState structure, out ConstructionOperationReason reason);

            Assert.That(completed, Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(world.SiteCount, Is.Zero);
            Assert.That(world.StructureCount, Is.EqualTo(1));
            Assert.That(world.IsOccupied(space), Is.True);
            Assert.That(structure.InstanceId, Is.EqualTo(id));
            Assert.That(structure.DefinitionId, Is.EqualTo(WallId));
            Assert.That(structure.Space, Is.EqualTo(space));
        }

        [Test]
        public void CompletedInstanceId_CannotBeReusedForAnotherSite()
        {
            ConstructionWorldState world = CreateReadyWorld(out ConstructionInstanceId id, out _);
            Assert.That(world.TryComplete(id, out _, out _), Is.True);

            bool started = world.TryStartSite(id, WallId, WallSpace(10L, 0L), out _, out ConstructionOperationReason reason);

            Assert.That(started, Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.DuplicateInstance));
        }

        [Test]
        public void DismantleIncompleteSite_ReturnsExactlyDepositedMaterialsAndVacates()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);
            ConstructionSpaceKey space = WallSpace(0L, 0L);
            world.DepositMaterial(id, Stone, 2);
            world.DepositMaterial(id, Branch, 3);
            world.AddWork(id, 75);

            bool dismantled = world.TryDismantleSite(id, out ConstructionRecovery recovery, out ConstructionOperationReason reason);

            Assert.That(dismantled, Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(recovery.Materials, Is.EqualTo(new[]
            {
                new ConstructionMaterialQuantity(Branch, 3),
                new ConstructionMaterialQuantity(Stone, 2)
            }));
            Assert.That(world.SiteCount, Is.Zero);
            Assert.That(world.IsOccupied(space), Is.False);
        }

        [Test]
        public void DismantleEmptySite_ReturnsEmptyRecovery()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);

            Assert.That(world.TryDismantleSite(id, out ConstructionRecovery recovery, out _), Is.True);
            Assert.That(recovery.IsEmpty, Is.True);
        }

        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(10, 7)]
        [TestCase(101, 70)]
        [TestCase(int.MaxValue, 1503238552)]
        public void CompletedRecovery_IsSeventyPercentFlooredWithoutIntOverflow(int required, int expected)
        {
            ConstructionDefinition definition = CreateDefinition(
                new ConstructionDefinitionId("single-material"),
                ConstructionSpaceKind.Surface,
                1,
                new ConstructionMaterialRequirement(Branch, required));
            var world = new ConstructionWorldState(new ConstructionDefinitionCatalog(new[] { definition }));
            ConstructionInstanceId id = new ConstructionInstanceId("instance");
            ConstructionSpaceKey space = ConstructionSpaceKey.Surface(new WorldCellCoordinate(0L, 0L));
            Assert.That(world.TryStartSite(id, definition.Id, space, out _, out _), Is.True);
            Assert.That(world.DepositMaterial(id, Branch, required).Accepted, Is.EqualTo(required));
            Assert.That(world.AddWork(id, 1).Accepted, Is.EqualTo(1));
            Assert.That(world.TryComplete(id, out _, out _), Is.True);

            Assert.That(world.TryDismantleStructure(id, out ConstructionRecovery recovery, out _), Is.True);

            if (expected == 0)
            {
                Assert.That(recovery.Materials, Is.Empty);
            }
            else
            {
                Assert.That(recovery.Materials, Is.EqualTo(new[] { new ConstructionMaterialQuantity(Branch, expected) }));
            }
        }

        [Test]
        public void DismantleCompletedStructure_UsesDefinitionRequirementsNotMutableInventory()
        {
            ConstructionWorldState world = CreateReadyWorld(out ConstructionInstanceId id, out ConstructionSpaceKey space);
            Assert.That(world.TryComplete(id, out _, out _), Is.True);

            Assert.That(world.TryDismantleStructure(id, out ConstructionRecovery recovery, out ConstructionOperationReason reason), Is.True);

            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
            Assert.That(recovery.Materials, Is.EqualTo(new[]
            {
                new ConstructionMaterialQuantity(Branch, 2),
                new ConstructionMaterialQuantity(Stone, 2)
            }));
            Assert.That(world.StructureCount, Is.Zero);
            Assert.That(world.IsOccupied(space), Is.False);
        }

        [Test]
        public void VacatedSpace_CanBeUsedByANewInstanceAfterDismantling()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId first);
            ConstructionSpaceKey space = WallSpace(0L, 0L);
            Assert.That(world.TryDismantleSite(first, out _, out _), Is.True);

            Assert.That(
                world.TryStartSite(new ConstructionInstanceId("replacement"), WallId, space, out _, out ConstructionOperationReason reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
        }

        [Test]
        public void CanonicalCaptures_AreIndependentOfCreationOrder()
        {
            ConstructionWorldState world = CreateWorld();
            ConstructionInstanceId c = new ConstructionInstanceId("c");
            ConstructionInstanceId a = new ConstructionInstanceId("a");
            ConstructionInstanceId b = new ConstructionInstanceId("b");
            Assert.That(world.TryStartSite(c, WallId, WallSpace(10L, 0L), out _, out _), Is.True);
            Assert.That(world.TryStartSite(a, WallId, WallSpace(20L, 0L), out _, out _), Is.True);
            Assert.That(world.TryStartSite(b, WallId, WallSpace(30L, 0L), out _, out _), Is.True);

            Assert.That(world.CaptureCanonicalSites().Select(site => site.InstanceId).ToArray(), Is.EqualTo(new[] { a, b, c }));
        }

        [Test]
        public void FailedOperations_DoNotMutateWorldCountsOrOccupancy()
        {
            ConstructionWorldState world = CreateWorld(out ConstructionInstanceId id);
            ConstructionSpaceKey space = WallSpace(0L, 0L);

            Assert.That(world.DepositMaterial(new ConstructionInstanceId("missing"), Branch, 1).Reason, Is.EqualTo(ConstructionOperationReason.SiteNotFound));
            Assert.That(world.AddWork(new ConstructionInstanceId("missing"), 1).Reason, Is.EqualTo(ConstructionOperationReason.SiteNotFound));
            Assert.That(world.TryComplete(new ConstructionInstanceId("missing"), out _, out _), Is.False);
            Assert.That(world.TryDismantleStructure(id, out _, out ConstructionOperationReason reason), Is.False);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.StructureNotFound));
            Assert.That(world.SiteCount, Is.EqualTo(1));
            Assert.That(world.StructureCount, Is.Zero);
            Assert.That(world.IsOccupied(space), Is.True);
        }

        private static ConstructionWorldState CreateWorld()
        {
            return new ConstructionWorldState(new ConstructionDefinitionCatalog(new[]
            {
                CreateDefinition(
                    WallId,
                    ConstructionSpaceKind.Edge,
                    100,
                    new ConstructionMaterialRequirement(Stone, 3),
                    new ConstructionMaterialRequirement(Branch, 4))
            }));
        }

        private static ConstructionWorldState CreateWorld(out ConstructionInstanceId id)
        {
            ConstructionWorldState world = CreateWorld();
            id = new ConstructionInstanceId("site-001");
            Assert.That(world.TryStartSite(id, WallId, WallSpace(0L, 0L), out _, out ConstructionOperationReason reason), Is.True);
            Assert.That(reason, Is.EqualTo(ConstructionOperationReason.None));
            return world;
        }

        private static ConstructionWorldState CreateReadyWorld(
            out ConstructionInstanceId id,
            out ConstructionSpaceKey space)
        {
            ConstructionWorldState world = CreateWorld(out id);
            space = WallSpace(0L, 0L);
            Assert.That(world.DepositMaterial(id, Branch, 4).Accepted, Is.EqualTo(4));
            Assert.That(world.DepositMaterial(id, Stone, 3).Accepted, Is.EqualTo(3));
            Assert.That(world.AddWork(id, 100).Accepted, Is.EqualTo(100));
            Assert.That(world.IsReadyToComplete(id), Is.True);
            return world;
        }

        private static ConstructionSpaceKey WallSpace(long x, long y)
        {
            return ConstructionSpaceKey.Edge(new WorldCellCoordinate(x, y), ConstructionCellSide.East);
        }

        private static ConstructionDefinition CreateDefinition(
            ConstructionSpaceKind spaceKind,
            int work,
            params ConstructionMaterialRequirement[] materials)
        {
            return CreateDefinition(WallId, spaceKind, work, materials);
        }

        private static ConstructionDefinition CreateDefinition(
            ConstructionDefinitionId id,
            ConstructionSpaceKind spaceKind,
            int work,
            params ConstructionMaterialRequirement[] materials)
        {
            return new ConstructionDefinition(id, spaceKind, work, materials);
        }
    }
}
