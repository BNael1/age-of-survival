using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Persistence
{
    public static class ConstructionSaveDefaults
    {
        public const ushort SectionVersion = 1;
        public const string PrototypeCatalogId = "prototype-construction";
        public const int PrototypeCatalogRevision = 1;
        public const string PrototypeInstanceNamespace = "local-prototype";
        public const long InitialNextInstanceSequence =
            ConstructionInstanceIdSequencePolicy.InitialSequence;
    }

    public readonly struct ConstructionSpaceSnapshot :
        IEquatable<ConstructionSpaceSnapshot>
    {
        private readonly byte _initialized;

        public ConstructionSpaceSnapshot(ConstructionSpaceKey space)
        {
            if (!space.IsValid)
            {
                throw new ArgumentException(
                    "A valid construction space is required.",
                    nameof(space));
            }

            Kind = space.Kind;
            AnchorX = space.AnchorCell.X;
            AnchorY = space.AnchorCell.Y;
            EdgeAxis = space.Kind == ConstructionSpaceKind.Edge
                ? space.EdgeAddress.Axis
                : default;
            _initialized = 1;
        }

        private ConstructionSpaceSnapshot(
            ConstructionSpaceKind kind,
            long anchorX,
            long anchorY,
            ConstructionEdgeAxis edgeAxis)
        {
            if (!Enum.IsDefined(typeof(ConstructionSpaceKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == ConstructionSpaceKind.Edge
                && !Enum.IsDefined(typeof(ConstructionEdgeAxis), edgeAxis))
            {
                throw new ArgumentOutOfRangeException(nameof(edgeAxis));
            }

            Kind = kind;
            AnchorX = anchorX;
            AnchorY = anchorY;
            EdgeAxis = kind == ConstructionSpaceKind.Edge
                ? edgeAxis
                : default;
            _initialized = 1;

            // Rebuild immediately so an edge whose adjacent cell would overflow
            // is rejected at the DTO boundary.
            Restore();
        }

        public ConstructionSpaceKind Kind { get; }
        public long AnchorX { get; }
        public long AnchorY { get; }
        public ConstructionEdgeAxis EdgeAxis { get; }
        public bool IsValid => _initialized == 1;

        internal static ConstructionSpaceSnapshot Restore(
            ConstructionSpaceKind kind,
            long anchorX,
            long anchorY,
            ConstructionEdgeAxis edgeAxis)
        {
            return new ConstructionSpaceSnapshot(
                kind,
                anchorX,
                anchorY,
                edgeAxis);
        }

        public ConstructionSpaceKey Restore()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "A valid construction space snapshot is required.");
            }

            var anchor = new WorldCellCoordinate(AnchorX, AnchorY);
            switch (Kind)
            {
                case ConstructionSpaceKind.Surface:
                    return ConstructionSpaceKey.Surface(anchor);
                case ConstructionSpaceKind.Interior:
                    return ConstructionSpaceKey.Interior(anchor);
                case ConstructionSpaceKind.Roof:
                    return ConstructionSpaceKey.Roof(anchor);
                case ConstructionSpaceKind.Edge:
                    return ConstructionSpaceKey.Edge(
                        anchor,
                        EdgeAxis == ConstructionEdgeAxis.Horizontal
                            ? ConstructionCellSide.North
                            : ConstructionCellSide.East);
                default:
                    throw new InvalidOperationException(
                        "The construction space kind is invalid.");
            }
        }

        public bool Equals(ConstructionSpaceSnapshot other)
        {
            return _initialized == other._initialized
                && Kind == other.Kind
                && AnchorX == other.AnchorX
                && AnchorY == other.AnchorY
                && EdgeAxis == other.EdgeAxis;
        }

        public override bool Equals(object obj) =>
            obj is ConstructionSpaceSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _initialized;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ AnchorX.GetHashCode();
                hash = (hash * 397) ^ AnchorY.GetHashCode();
                return (hash * 397) ^ (int)EdgeAxis;
            }
        }
    }

    public readonly struct ConstructionMaterialSnapshot :
        IEquatable<ConstructionMaterialSnapshot>
    {
        public ConstructionMaterialSnapshot(
            ItemDefinitionId definitionId,
            int quantity)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException(
                    "A valid material identifier is required.",
                    nameof(definitionId));
            }

            StableIdentifierValidation.Validate(
                definitionId.Value,
                nameof(definitionId));
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            DefinitionId = definitionId;
            Quantity = quantity;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }

        public bool Equals(ConstructionMaterialSnapshot other) =>
            DefinitionId.Equals(other.DefinitionId)
            && Quantity == other.Quantity;

        public override bool Equals(object obj) =>
            obj is ConstructionMaterialSnapshot other && Equals(other);

        public override int GetHashCode() =>
            (DefinitionId.GetHashCode() * 397) ^ Quantity;
    }

    public sealed class ConstructionSiteSnapshot
    {
        private readonly ConstructionMaterialSnapshot[] _materials;
        private readonly IReadOnlyList<ConstructionMaterialSnapshot> _readOnlyMaterials;

        public ConstructionSiteSnapshot(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceSnapshot space,
            IEnumerable<ConstructionMaterialSnapshot> depositedMaterials,
            int workCompletedUnits)
        {
            ValidateIdentity(instanceId, definitionId);
            if (!space.IsValid)
                throw new ArgumentException("A valid construction space is required.", nameof(space));
            space.Restore();
            if (depositedMaterials == null)
                throw new ArgumentNullException(nameof(depositedMaterials));
            if (workCompletedUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(workCompletedUnits));

            var materials = new List<ConstructionMaterialSnapshot>(depositedMaterials);
            for (int index = 0; index < materials.Count; index++)
            {
                if (!materials[index].DefinitionId.IsValid
                    || materials[index].Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Deposited construction materials must be valid and positive.",
                        nameof(depositedMaterials));
                }
            }
            materials.Sort((left, right) =>
                left.DefinitionId.CompareTo(right.DefinitionId));
            for (int index = 1; index < materials.Count; index++)
            {
                if (materials[index - 1].DefinitionId.Equals(
                    materials[index].DefinitionId))
                {
                    throw new ArgumentException(
                        "Deposited construction materials must be unique.",
                        nameof(depositedMaterials));
                }
            }

            InstanceId = instanceId;
            DefinitionId = definitionId;
            Space = space;
            WorkCompletedUnits = workCompletedUnits;
            _materials = materials.ToArray();
            _readOnlyMaterials = Array.AsReadOnly(_materials);
        }

        public ConstructionInstanceId InstanceId { get; }
        public ConstructionDefinitionId DefinitionId { get; }
        public ConstructionSpaceSnapshot Space { get; }
        public IReadOnlyList<ConstructionMaterialSnapshot> DepositedMaterials =>
            _readOnlyMaterials;
        public int WorkCompletedUnits { get; }

        private static void ValidateIdentity(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId)
        {
            if (!instanceId.IsValid)
                throw new ArgumentException("A valid construction instance identifier is required.", nameof(instanceId));
            if (!definitionId.IsValid)
                throw new ArgumentException("A valid construction definition identifier is required.", nameof(definitionId));
            StableIdentifierValidation.Validate(instanceId.Value, nameof(instanceId));
            StableIdentifierValidation.Validate(definitionId.Value, nameof(definitionId));
        }
    }

    public sealed class CompletedStructureSnapshot
    {
        public CompletedStructureSnapshot(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceSnapshot space)
        {
            if (!instanceId.IsValid)
                throw new ArgumentException("A valid construction instance identifier is required.", nameof(instanceId));
            if (!definitionId.IsValid)
                throw new ArgumentException("A valid construction definition identifier is required.", nameof(definitionId));
            StableIdentifierValidation.Validate(instanceId.Value, nameof(instanceId));
            StableIdentifierValidation.Validate(definitionId.Value, nameof(definitionId));
            if (!space.IsValid)
                throw new ArgumentException("A valid construction space is required.", nameof(space));
            space.Restore();

            InstanceId = instanceId;
            DefinitionId = definitionId;
            Space = space;
        }

        public ConstructionInstanceId InstanceId { get; }
        public ConstructionDefinitionId DefinitionId { get; }
        public ConstructionSpaceSnapshot Space { get; }
    }

    public sealed class ConstructionSaveSnapshot
    {
        private readonly ConstructionSiteSnapshot[] _sites;
        private readonly CompletedStructureSnapshot[] _structures;
        private readonly IReadOnlyList<ConstructionSiteSnapshot> _readOnlySites;
        private readonly IReadOnlyList<CompletedStructureSnapshot> _readOnlyStructures;

        public ConstructionSaveSnapshot(
            ushort sectionVersion,
            string catalogId,
            int catalogRevision,
            string instanceNamespace,
            long nextInstanceSequence,
            IEnumerable<ConstructionSiteSnapshot> sites,
            IEnumerable<CompletedStructureSnapshot> structures)
        {
            if (sectionVersion != ConstructionSaveDefaults.SectionVersion)
                throw new NotSupportedException("The construction save section version is unsupported.");
            StableIdentifierValidation.Validate(catalogId, nameof(catalogId));
            if (catalogRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(catalogRevision));
            StableIdentifierValidation.Validate(instanceNamespace, nameof(instanceNamespace));
            if (nextInstanceSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(nextInstanceSequence));
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            if (structures == null) throw new ArgumentNullException(nameof(structures));

            var siteList = new List<ConstructionSiteSnapshot>(sites);
            var structureList = new List<CompletedStructureSnapshot>(structures);
            for (int index = 0; index < siteList.Count; index++)
            {
                if (siteList[index] == null)
                    throw new ArgumentException("Construction sites must not contain null entries.", nameof(sites));
            }
            for (int index = 0; index < structureList.Count; index++)
            {
                if (structureList[index] == null)
                    throw new ArgumentException("Completed structures must not contain null entries.", nameof(structures));
            }
            siteList.Sort((left, right) => CompareRequired(left, right));
            structureList.Sort((left, right) => CompareRequired(left, right));

            var identities = new HashSet<ConstructionInstanceId>();
            var spaces = new HashSet<ConstructionSpaceKey>();
            for (int index = 0; index < siteList.Count; index++)
            {
                ConstructionSiteSnapshot site = siteList[index];
                ValidateUnique(site.InstanceId, site.Space.Restore(), identities, spaces);
                ValidateOwnedSequenceForm(site.InstanceId, instanceNamespace);
            }

            for (int index = 0; index < structureList.Count; index++)
            {
                CompletedStructureSnapshot structure = structureList[index];
                ValidateUnique(structure.InstanceId, structure.Space.Restore(), identities, spaces);
                ValidateOwnedSequenceForm(structure.InstanceId, instanceNamespace);
            }

            if (nextInstanceSequence
                    != ConstructionInstanceIdSequencePolicy.ExhaustedSequence
                && !ConstructionInstanceIdSequencePolicy.TryFindCommittableCandidate(
                    instanceNamespace,
                    nextInstanceSequence,
                    candidate => !identities.Contains(candidate),
                    out _))
            {
                throw new ArgumentException(
                    "The construction allocator cannot find a committable identifier within its collision window.",
                    nameof(nextInstanceSequence));
            }

            SectionVersion = sectionVersion;
            CatalogId = catalogId;
            CatalogRevision = catalogRevision;
            InstanceNamespace = instanceNamespace;
            NextInstanceSequence = nextInstanceSequence;
            _sites = siteList.ToArray();
            _structures = structureList.ToArray();
            _readOnlySites = Array.AsReadOnly(_sites);
            _readOnlyStructures = Array.AsReadOnly(_structures);
        }

        public ushort SectionVersion { get; }
        public string CatalogId { get; }
        public int CatalogRevision { get; }
        public string InstanceNamespace { get; }
        public long NextInstanceSequence { get; }
        public IReadOnlyList<ConstructionSiteSnapshot> Sites => _readOnlySites;
        public IReadOnlyList<CompletedStructureSnapshot> Structures => _readOnlyStructures;

        public static ConstructionSaveSnapshot Empty => new ConstructionSaveSnapshot(
            ConstructionSaveDefaults.SectionVersion,
            ConstructionSaveDefaults.PrototypeCatalogId,
            ConstructionSaveDefaults.PrototypeCatalogRevision,
            ConstructionSaveDefaults.PrototypeInstanceNamespace,
            ConstructionSaveDefaults.InitialNextInstanceSequence,
            Array.Empty<ConstructionSiteSnapshot>(),
            Array.Empty<CompletedStructureSnapshot>());

        public static ConstructionSaveSnapshot Capture(
            string catalogId,
            int catalogRevision,
            string instanceNamespace,
            long nextInstanceSequence,
            ConstructionWorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            ConstructionSiteState[] liveSites = world.CaptureCanonicalSites();
            var sites = new ConstructionSiteSnapshot[liveSites.Length];
            for (int index = 0; index < liveSites.Length; index++)
            {
                ConstructionSiteState site = liveSites[index];
                ConstructionMaterialQuantity[] deposited =
                    world.CaptureDepositedMaterials(site.InstanceId);
                var materials = new ConstructionMaterialSnapshot[deposited.Length];
                for (int materialIndex = 0; materialIndex < deposited.Length; materialIndex++)
                {
                    materials[materialIndex] = new ConstructionMaterialSnapshot(
                        deposited[materialIndex].DefinitionId,
                        deposited[materialIndex].Quantity);
                }

                sites[index] = new ConstructionSiteSnapshot(
                    site.InstanceId,
                    site.DefinitionId,
                    new ConstructionSpaceSnapshot(site.Space),
                    materials,
                    site.WorkCompletedUnits);
            }

            CompletedStructureState[] liveStructures = world.CaptureCanonicalStructures();
            var structures = new CompletedStructureSnapshot[liveStructures.Length];
            for (int index = 0; index < liveStructures.Length; index++)
            {
                CompletedStructureState structure = liveStructures[index];
                structures[index] = new CompletedStructureSnapshot(
                    structure.InstanceId,
                    structure.DefinitionId,
                    new ConstructionSpaceSnapshot(structure.Space));
            }

            return new ConstructionSaveSnapshot(
                ConstructionSaveDefaults.SectionVersion,
                catalogId,
                catalogRevision,
                instanceNamespace,
                nextInstanceSequence,
                sites,
                structures);
        }

        public ConstructionWorldState RestoreState(
            ConstructionDefinitionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var restored = new ConstructionWorldState(catalog);
            for (int index = 0; index < _sites.Length; index++)
            {
                ConstructionSiteSnapshot site = _sites[index];
                var materials = new ConstructionMaterialQuantity[
                    site.DepositedMaterials.Count];
                for (int materialIndex = 0;
                     materialIndex < site.DepositedMaterials.Count;
                     materialIndex++)
                {
                    ConstructionMaterialSnapshot material =
                        site.DepositedMaterials[materialIndex];
                    materials[materialIndex] = new ConstructionMaterialQuantity(
                        material.DefinitionId,
                        material.Quantity);
                }

                restored.RestoreSite(
                    site.InstanceId,
                    site.DefinitionId,
                    site.Space.Restore(),
                    materials,
                    site.WorkCompletedUnits);
            }

            for (int index = 0; index < _structures.Length; index++)
            {
                CompletedStructureSnapshot structure = _structures[index];
                restored.RestoreCompletedStructure(
                    structure.InstanceId,
                    structure.DefinitionId,
                    structure.Space.Restore());
            }

            return restored;
        }

        private static int CompareRequired(
            ConstructionSiteSnapshot left,
            ConstructionSiteSnapshot right)
        {
            if (left == null || right == null) return left == null ? -1 : 1;
            return left.InstanceId.CompareTo(right.InstanceId);
        }

        private static int CompareRequired(
            CompletedStructureSnapshot left,
            CompletedStructureSnapshot right)
        {
            if (left == null || right == null) return left == null ? -1 : 1;
            return left.InstanceId.CompareTo(right.InstanceId);
        }

        private static void ValidateUnique(
            ConstructionInstanceId instanceId,
            ConstructionSpaceKey space,
            ISet<ConstructionInstanceId> identities,
            ISet<ConstructionSpaceKey> spaces)
        {
            if (!identities.Add(instanceId))
                throw new ArgumentException("Construction instance identifiers must be globally unique.");
            if (!spaces.Add(space))
                throw new ArgumentException("Construction topology cannot be occupied more than once.");
        }

        private static void ValidateOwnedSequenceForm(
            ConstructionInstanceId instanceId,
            string instanceNamespace)
        {
            if (ConstructionInstanceIdSequencePolicy.IsOwnedByNamespace(
                    instanceId,
                    instanceNamespace)
                && !ConstructionInstanceIdSequencePolicy.TryParseOwnedSequence(
                    instanceId,
                    instanceNamespace,
                    out _))
            {
                throw new ArgumentException(
                    "A construction identifier in the allocator namespace has an invalid sequence.");
            }
        }
    }
}
