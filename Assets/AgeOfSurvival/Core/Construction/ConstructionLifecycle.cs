using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Construction
{
    public enum ConstructionOperationReason
    {
        None = 0,
        InvalidRequest = 1,
        UnknownDefinition = 2,
        DuplicateInstance = 3,
        SpaceKindMismatch = 4,
        SpaceOccupied = 5,
        SiteNotFound = 6,
        StructureNotFound = 7,
        MaterialNotRequired = 8,
        MaterialRequirementSatisfied = 9,
        WorkRequirementSatisfied = 10,
        RequirementsIncomplete = 11
    }

    public readonly struct ConstructionQuantityResult
    {
        public ConstructionQuantityResult(int requested, int accepted, ConstructionOperationReason reason)
        {
            Requested = requested;
            Accepted = accepted;
            Reason = reason;
        }

        public int Requested { get; }
        public int Accepted { get; }
        public ConstructionOperationReason Reason { get; }
        public bool Succeeded => Reason == ConstructionOperationReason.None && Accepted > 0;
    }

    public sealed class ConstructionRecovery
    {
        private readonly IReadOnlyList<ConstructionMaterialQuantity> _materials;

        internal ConstructionRecovery(IList<ConstructionMaterialQuantity> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var copy = new ConstructionMaterialQuantity[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                copy[i] = materials[i];
            }

            _materials = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<ConstructionMaterialQuantity> Materials => _materials;
        public bool IsEmpty => _materials.Count == 0;
    }

    public sealed class ConstructionSiteState
    {
        private readonly Dictionary<ItemDefinitionId, int> _deposited =
            new Dictionary<ItemDefinitionId, int>();

        internal ConstructionSiteState(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Space = space;
        }

        public ConstructionInstanceId InstanceId { get; }
        public ConstructionDefinitionId DefinitionId { get; }
        public ConstructionSpaceKey Space { get; }
        public int WorkCompletedUnits { get; internal set; }

        internal int GetDeposited(ItemDefinitionId definitionId)
        {
            return _deposited.TryGetValue(definitionId, out int quantity) ? quantity : 0;
        }

        internal void AddDeposited(ItemDefinitionId definitionId, int quantity)
        {
            int current = GetDeposited(definitionId);
            _deposited[definitionId] = checked(current + quantity);
        }
    }

    public sealed class CompletedStructureState
    {
        internal CompletedStructureState(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Space = space;
        }

        public ConstructionInstanceId InstanceId { get; }
        public ConstructionDefinitionId DefinitionId { get; }
        public ConstructionSpaceKey Space { get; }
    }

    public sealed class ConstructionWorldState
    {
        public const int CompletedRecoveryPercent = 70;

        private readonly ConstructionDefinitionCatalog _catalog;
        private readonly ConstructionOccupancyRegistry _occupancy = new ConstructionOccupancyRegistry();
        private readonly Dictionary<ConstructionInstanceId, ConstructionSiteState> _sites =
            new Dictionary<ConstructionInstanceId, ConstructionSiteState>();
        private readonly Dictionary<ConstructionInstanceId, CompletedStructureState> _structures =
            new Dictionary<ConstructionInstanceId, CompletedStructureState>();

        public ConstructionWorldState(ConstructionDefinitionCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int SiteCount => _sites.Count;
        public int StructureCount => _structures.Count;
        public int OccupiedSpaceCount => _occupancy.Count;

        public bool IsOccupied(ConstructionSpaceKey space) => _occupancy.IsOccupied(space);

        public bool TryFindSite(ConstructionInstanceId instanceId, out ConstructionSiteState site)
        {
            if (!instanceId.IsValid)
            {
                site = null;
                return false;
            }

            return _sites.TryGetValue(instanceId, out site);
        }

        public bool TryFindStructure(ConstructionInstanceId instanceId, out CompletedStructureState structure)
        {
            if (!instanceId.IsValid)
            {
                structure = null;
                return false;
            }

            return _structures.TryGetValue(instanceId, out structure);
        }

        public bool TryStartSite(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space,
            out ConstructionSiteState site,
            out ConstructionOperationReason reason)
        {
            site = null;
            reason = ConstructionOperationReason.InvalidRequest;

            if (!instanceId.IsValid || !definitionId.IsValid || !space.IsValid)
            {
                return false;
            }

            if (!_catalog.TryFind(definitionId, out ConstructionDefinition definition))
            {
                reason = ConstructionOperationReason.UnknownDefinition;
                return false;
            }

            if (_sites.ContainsKey(instanceId) || _structures.ContainsKey(instanceId))
            {
                reason = ConstructionOperationReason.DuplicateInstance;
                return false;
            }

            if (space.Kind != definition.SpaceKind)
            {
                reason = ConstructionOperationReason.SpaceKindMismatch;
                return false;
            }

            if (!_occupancy.TryOccupy(space))
            {
                reason = ConstructionOperationReason.SpaceOccupied;
                return false;
            }

            site = new ConstructionSiteState(instanceId, definitionId, space);
            _sites.Add(instanceId, site);
            reason = ConstructionOperationReason.None;
            return true;
        }

        public ConstructionQuantityResult DepositMaterial(
            ConstructionInstanceId instanceId,
            ItemDefinitionId materialId,
            int requestedQuantity)
        {
            if (!instanceId.IsValid || !materialId.IsValid || requestedQuantity <= 0)
            {
                return new ConstructionQuantityResult(
                    requestedQuantity,
                    0,
                    ConstructionOperationReason.InvalidRequest);
            }

            if (!_sites.TryGetValue(instanceId, out ConstructionSiteState site))
            {
                return new ConstructionQuantityResult(
                    requestedQuantity,
                    0,
                    ConstructionOperationReason.SiteNotFound);
            }

            ConstructionDefinition definition = _catalog.Require(site.DefinitionId);
            if (!definition.TryFindMaterial(materialId, out ConstructionMaterialRequirement requirement))
            {
                return new ConstructionQuantityResult(
                    requestedQuantity,
                    0,
                    ConstructionOperationReason.MaterialNotRequired);
            }

            int deposited = site.GetDeposited(materialId);
            int remaining = requirement.Quantity - deposited;
            if (remaining <= 0)
            {
                return new ConstructionQuantityResult(
                    requestedQuantity,
                    0,
                    ConstructionOperationReason.MaterialRequirementSatisfied);
            }

            int accepted = Math.Min(requestedQuantity, remaining);
            site.AddDeposited(materialId, accepted);
            return new ConstructionQuantityResult(
                requestedQuantity,
                accepted,
                ConstructionOperationReason.None);
        }

        public ConstructionQuantityResult AddWork(
            ConstructionInstanceId instanceId,
            int requestedWorkUnits)
        {
            if (!instanceId.IsValid || requestedWorkUnits <= 0)
            {
                return new ConstructionQuantityResult(
                    requestedWorkUnits,
                    0,
                    ConstructionOperationReason.InvalidRequest);
            }

            if (!_sites.TryGetValue(instanceId, out ConstructionSiteState site))
            {
                return new ConstructionQuantityResult(
                    requestedWorkUnits,
                    0,
                    ConstructionOperationReason.SiteNotFound);
            }

            ConstructionDefinition definition = _catalog.Require(site.DefinitionId);
            int remaining = definition.RequiredWorkUnits - site.WorkCompletedUnits;
            if (remaining <= 0)
            {
                return new ConstructionQuantityResult(
                    requestedWorkUnits,
                    0,
                    ConstructionOperationReason.WorkRequirementSatisfied);
            }

            int accepted = Math.Min(requestedWorkUnits, remaining);
            site.WorkCompletedUnits = checked(site.WorkCompletedUnits + accepted);
            return new ConstructionQuantityResult(
                requestedWorkUnits,
                accepted,
                ConstructionOperationReason.None);
        }

        public bool IsReadyToComplete(ConstructionInstanceId instanceId)
        {
            if (!instanceId.IsValid || !_sites.TryGetValue(instanceId, out ConstructionSiteState site))
            {
                return false;
            }

            ConstructionDefinition definition = _catalog.Require(site.DefinitionId);
            if (site.WorkCompletedUnits != definition.RequiredWorkUnits)
            {
                return false;
            }

            for (int i = 0; i < definition.Materials.Count; i++)
            {
                ConstructionMaterialRequirement material = definition.Materials[i];
                if (site.GetDeposited(material.DefinitionId) != material.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryComplete(
            ConstructionInstanceId instanceId,
            out CompletedStructureState structure,
            out ConstructionOperationReason reason)
        {
            structure = null;
            reason = ConstructionOperationReason.InvalidRequest;

            if (!instanceId.IsValid)
            {
                return false;
            }

            if (!_sites.TryGetValue(instanceId, out ConstructionSiteState site))
            {
                reason = ConstructionOperationReason.SiteNotFound;
                return false;
            }

            if (!IsReadyToComplete(instanceId))
            {
                reason = ConstructionOperationReason.RequirementsIncomplete;
                return false;
            }

            if (!_occupancy.IsOccupied(site.Space))
            {
                throw new InvalidOperationException("A construction site lost its occupied space.");
            }

            structure = new CompletedStructureState(site.InstanceId, site.DefinitionId, site.Space);
            _structures.Add(instanceId, structure);
            if (!_sites.Remove(instanceId))
            {
                _structures.Remove(instanceId);
                throw new InvalidOperationException("A construction site disappeared during completion.");
            }

            reason = ConstructionOperationReason.None;
            return true;
        }

        public bool TryDismantleSite(
            ConstructionInstanceId instanceId,
            out ConstructionRecovery recovery,
            out ConstructionOperationReason reason)
        {
            recovery = null;
            reason = ConstructionOperationReason.InvalidRequest;

            if (!instanceId.IsValid)
            {
                return false;
            }

            if (!_sites.TryGetValue(instanceId, out ConstructionSiteState site))
            {
                reason = ConstructionOperationReason.SiteNotFound;
                return false;
            }

            ConstructionDefinition definition = _catalog.Require(site.DefinitionId);
            recovery = CaptureSiteRecovery(site, definition);
            RemoveSiteAndVacate(site);
            reason = ConstructionOperationReason.None;
            return true;
        }

        public bool TryDismantleStructure(
            ConstructionInstanceId instanceId,
            out ConstructionRecovery recovery,
            out ConstructionOperationReason reason)
        {
            recovery = null;
            reason = ConstructionOperationReason.InvalidRequest;

            if (!instanceId.IsValid)
            {
                return false;
            }

            if (!_structures.TryGetValue(instanceId, out CompletedStructureState structure))
            {
                reason = ConstructionOperationReason.StructureNotFound;
                return false;
            }

            ConstructionDefinition definition = _catalog.Require(structure.DefinitionId);
            recovery = CaptureCompletedRecovery(definition);
            RemoveStructureAndVacate(structure);
            reason = ConstructionOperationReason.None;
            return true;
        }

        public ConstructionMaterialQuantity[] CaptureDepositedMaterials(ConstructionInstanceId instanceId)
        {
            if (!TryFindSite(instanceId, out ConstructionSiteState site))
            {
                throw new KeyNotFoundException($"Unknown construction site '{instanceId}'.");
            }

            ConstructionDefinition definition = _catalog.Require(site.DefinitionId);
            var result = new List<ConstructionMaterialQuantity>();
            for (int i = 0; i < definition.Materials.Count; i++)
            {
                ItemDefinitionId materialId = definition.Materials[i].DefinitionId;
                int quantity = site.GetDeposited(materialId);
                if (quantity > 0)
                {
                    result.Add(new ConstructionMaterialQuantity(materialId, quantity));
                }
            }

            return result.ToArray();
        }

        public ConstructionSiteState[] CaptureCanonicalSites()
        {
            var result = new List<ConstructionSiteState>(_sites.Values);
            result.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return result.ToArray();
        }

        public CompletedStructureState[] CaptureCanonicalStructures()
        {
            var result = new List<CompletedStructureState>(_structures.Values);
            result.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return result.ToArray();
        }

        internal void RestoreSite(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space,
            IReadOnlyList<ConstructionMaterialQuantity> depositedMaterials,
            int workCompletedUnits)
        {
            if (depositedMaterials == null)
                throw new ArgumentNullException(nameof(depositedMaterials));
            if (!_catalog.TryFind(definitionId, out ConstructionDefinition definition))
                throw new InvalidOperationException($"Unknown saved construction definition '{definitionId}'.");
            if (!instanceId.IsValid || !space.IsValid || space.Kind != definition.SpaceKind)
                throw new InvalidOperationException("The saved construction site identity or topology is invalid.");
            if (_sites.ContainsKey(instanceId) || _structures.ContainsKey(instanceId))
                throw new InvalidOperationException("A saved construction instance identifier is duplicated.");
            if (workCompletedUnits < 0 || workCompletedUnits > definition.RequiredWorkUnits)
                throw new InvalidOperationException("Saved construction work is outside its requirements.");

            var quantities = new Dictionary<ItemDefinitionId, int>();
            for (int index = 0; index < depositedMaterials.Count; index++)
            {
                ConstructionMaterialQuantity material = depositedMaterials[index];
                if (!definition.TryFindMaterial(material.DefinitionId, out ConstructionMaterialRequirement requirement)
                    || material.Quantity > requirement.Quantity
                    || quantities.ContainsKey(material.DefinitionId))
                {
                    throw new InvalidOperationException("Saved construction materials violate the definition requirements.");
                }
                quantities.Add(material.DefinitionId, material.Quantity);
            }

            bool materialsComplete = true;
            for (int index = 0; index < definition.Materials.Count; index++)
            {
                ConstructionMaterialRequirement requirement = definition.Materials[index];
                if (!quantities.TryGetValue(requirement.DefinitionId, out int quantity)
                    || quantity != requirement.Quantity)
                {
                    materialsComplete = false;
                    break;
                }
            }
            if (materialsComplete && workCompletedUnits == definition.RequiredWorkUnits)
                throw new InvalidOperationException("A completed construction cannot be restored as a site.");
            if (!_occupancy.TryOccupy(space))
                throw new InvalidOperationException("Saved construction topology is occupied more than once.");

            var site = new ConstructionSiteState(instanceId, definitionId, space)
            {
                WorkCompletedUnits = workCompletedUnits
            };
            foreach (KeyValuePair<ItemDefinitionId, int> material in quantities)
                site.AddDeposited(material.Key, material.Value);
            _sites.Add(instanceId, site);
        }

        internal void RestoreCompletedStructure(
            ConstructionInstanceId instanceId,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            if (!_catalog.TryFind(definitionId, out ConstructionDefinition definition))
                throw new InvalidOperationException($"Unknown saved construction definition '{definitionId}'.");
            if (!instanceId.IsValid || !space.IsValid || space.Kind != definition.SpaceKind)
                throw new InvalidOperationException("The saved completed structure identity or topology is invalid.");
            if (_sites.ContainsKey(instanceId) || _structures.ContainsKey(instanceId))
                throw new InvalidOperationException("A saved construction instance identifier is duplicated.");
            if (!_occupancy.TryOccupy(space))
                throw new InvalidOperationException("Saved construction topology is occupied more than once.");
            _structures.Add(
                instanceId,
                new CompletedStructureState(instanceId, definitionId, space));
        }

        private static ConstructionRecovery CaptureSiteRecovery(
            ConstructionSiteState site,
            ConstructionDefinition definition)
        {
            var materials = new List<ConstructionMaterialQuantity>();
            for (int i = 0; i < definition.Materials.Count; i++)
            {
                ItemDefinitionId materialId = definition.Materials[i].DefinitionId;
                int deposited = site.GetDeposited(materialId);
                if (deposited > 0)
                {
                    materials.Add(new ConstructionMaterialQuantity(materialId, deposited));
                }
            }

            return new ConstructionRecovery(materials);
        }

        private static ConstructionRecovery CaptureCompletedRecovery(ConstructionDefinition definition)
        {
            var materials = new List<ConstructionMaterialQuantity>();
            for (int i = 0; i < definition.Materials.Count; i++)
            {
                ConstructionMaterialRequirement requirement = definition.Materials[i];
                int recovered = (int)(((long)requirement.Quantity * CompletedRecoveryPercent) / 100L);
                if (recovered > 0)
                {
                    materials.Add(new ConstructionMaterialQuantity(requirement.DefinitionId, recovered));
                }
            }

            return new ConstructionRecovery(materials);
        }

        private void RemoveSiteAndVacate(ConstructionSiteState site)
        {
            if (!_occupancy.IsOccupied(site.Space))
            {
                throw new InvalidOperationException("A construction site lost its occupied space.");
            }

            if (!_sites.Remove(site.InstanceId))
            {
                throw new InvalidOperationException("A construction site disappeared during dismantling.");
            }

            if (!_occupancy.TryVacate(site.Space))
            {
                throw new InvalidOperationException("A dismantled construction site could not vacate its space.");
            }
        }

        private void RemoveStructureAndVacate(CompletedStructureState structure)
        {
            if (!_occupancy.IsOccupied(structure.Space))
            {
                throw new InvalidOperationException("A completed structure lost its occupied space.");
            }

            if (!_structures.Remove(structure.InstanceId))
            {
                throw new InvalidOperationException("A completed structure disappeared during dismantling.");
            }

            if (!_occupancy.TryVacate(structure.Space))
            {
                throw new InvalidOperationException("A dismantled completed structure could not vacate its space.");
            }
        }
    }
}
