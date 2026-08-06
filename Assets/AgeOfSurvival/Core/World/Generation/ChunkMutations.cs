using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;

namespace AgeOfSurvival.Core.World.Generation
{
    public static class StableIdentifierValidation
    {
        public const int MaximumLength = 256;

        public static void Validate(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A stable identifier must not be empty.", parameterName);
            }

            if (value.Length > MaximumLength)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value.Length,
                    $"A stable identifier must contain at most {MaximumLength} characters.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    throw new ArgumentException(
                        "A stable identifier must not contain control characters.",
                        parameterName);
                }
            }
        }
    }

    public static class ChunkPositioning
    {
        private const double LongUpperExclusive = 9223372036854775808.0;

        public static ChunkCoordinate Locate(WorldPosition position, ChunkLayout layout)
        {
            double floorX = Math.Floor(position.X);
            double floorY = Math.Floor(position.Y);
            if (floorX < long.MinValue
                || floorX >= LongUpperExclusive
                || floorY < long.MinValue
                || floorY >= LongUpperExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    position,
                    "The world position cannot be represented by signed 64-bit cells.");
            }

            return ChunkAddressing.Locate(
                new WorldCellCoordinate((long)floorX, (long)floorY),
                layout).Chunk;
        }

        public static bool BelongsTo(
            WorldPosition position,
            ChunkCoordinate coordinate,
            ChunkLayout layout)
        {
            return Locate(position, layout).Equals(coordinate);
        }
    }

    public readonly struct HarvestedResourceMutation : IEquatable<HarvestedResourceMutation>
    {
        public HarvestedResourceMutation(ResourceId id, WorldPosition position)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A harvested resource requires a valid identifier.", nameof(id));
            }

            StableIdentifierValidation.Validate(id.Value, nameof(id));
            Id = id;
            Position = position;
        }

        public ResourceId Id { get; }
        public WorldPosition Position { get; }

        public bool Equals(HarvestedResourceMutation other)
        {
            return Id.Equals(other.Id) && Position.Equals(other.Position);
        }

        public override bool Equals(object obj)
        {
            return obj is HarvestedResourceMutation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }
    }

    public sealed class StoredItemMutation
    {
        public StoredItemMutation(
            ItemDefinitionId definitionId,
            ItemStateKind kind,
            EncumbranceValue unitEncumbrance,
            int quantity,
            ItemInstanceId instanceId = default,
            ContainerId containedContainerId = default)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException("A stored item requires a definition identifier.", nameof(definitionId));
            }

            StableIdentifierValidation.Validate(definitionId.Value, nameof(definitionId));
            if (!Enum.IsDefined(typeof(ItemStateKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown item state kind.");
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A stored quantity must be positive.");
            }

            if (kind == ItemStateKind.Unique)
            {
                if (quantity != 1 || !instanceId.IsValid)
                {
                    throw new ArgumentException(
                        "A unique stored item requires exactly one valid instance.",
                        nameof(instanceId));
                }

                StableIdentifierValidation.Validate(instanceId.Value, nameof(instanceId));
            }
            else if (instanceId.IsValid || containedContainerId.IsValid)
            {
                throw new ArgumentException(
                    "A stackable stored item cannot carry unique-instance data.",
                    nameof(instanceId));
            }

            if (containedContainerId.IsValid)
            {
                StableIdentifierValidation.Validate(containedContainerId.Value, nameof(containedContainerId));
            }

            DefinitionId = definitionId;
            Kind = kind;
            UnitEncumbrance = unitEncumbrance;
            Quantity = quantity;
            InstanceId = instanceId;
            ContainedContainerId = containedContainerId;
        }

        public ItemDefinitionId DefinitionId { get; }
        public ItemStateKind Kind { get; }
        public EncumbranceValue UnitEncumbrance { get; }
        public int Quantity { get; }
        public ItemInstanceId InstanceId { get; }
        public ContainerId ContainedContainerId { get; }

        internal static StoredItemMutation Capture(InventoryEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.Kind == ItemStateKind.Stackable
                ? new StoredItemMutation(
                    entry.DefinitionId,
                    entry.Kind,
                    entry.UnitEncumbrance,
                    entry.Quantity)
                : new StoredItemMutation(
                    entry.DefinitionId,
                    entry.Kind,
                    entry.UnitEncumbrance,
                    1,
                    entry.UniqueItem.InstanceId,
                    entry.UniqueItem.ContainedContainerId);
        }
    }

    public sealed class GroundContainerMutation
    {
        private readonly StoredItemMutation[] _items;
        private readonly IReadOnlyList<StoredItemMutation> _readOnlyItems;

        public GroundContainerMutation(
            GroundContainerId id,
            ContainerId containerId,
            WorldPosition position,
            string definitionKey,
            string displayName,
            EncumbranceValue capacity,
            IEnumerable<StoredItemMutation> items)
        {
            if (!id.IsValid || !containerId.IsValid)
            {
                throw new ArgumentException("A stored ground container requires valid identifiers.");
            }

            StableIdentifierValidation.Validate(id.Value, nameof(id));
            StableIdentifierValidation.Validate(containerId.Value, nameof(containerId));
            if (string.IsNullOrWhiteSpace(definitionKey))
            {
                throw new ArgumentException("A stored container requires a definition key.", nameof(definitionKey));
            }

            StableIdentifierValidation.Validate(definitionKey, nameof(definitionKey));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A stored container requires a display name.", nameof(displayName));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var canonical = new List<StoredItemMutation>();
            foreach (StoredItemMutation item in items)
            {
                canonical.Add(item ?? throw new ArgumentException("Stored items must not contain null values.", nameof(items)));
            }

            if (canonical.Count == 0)
            {
                throw new ArgumentException("Empty ground containers must not be persisted.", nameof(items));
            }

            canonical.Sort(CompareItems);
            for (int index = 1; index < canonical.Count; index++)
            {
                if (CompareItems(canonical[index - 1], canonical[index]) == 0)
                {
                    throw new ArgumentException("Stored item identities must be unique.", nameof(items));
                }
            }

            Id = id;
            ContainerId = containerId;
            Position = position;
            DefinitionKey = definitionKey;
            DisplayName = displayName;
            Capacity = capacity;
            _items = canonical.ToArray();
            _readOnlyItems = Array.AsReadOnly(_items);
        }

        public GroundContainerId Id { get; }
        public ContainerId ContainerId { get; }
        public WorldPosition Position { get; }
        public string DefinitionKey { get; }
        public string DisplayName { get; }
        public EncumbranceValue Capacity { get; }
        public IReadOnlyList<StoredItemMutation> Items => _readOnlyItems;

        public static GroundContainerMutation Capture(GroundContainerState ground)
        {
            if (ground == null)
            {
                throw new ArgumentNullException(nameof(ground));
            }

            var items = new List<StoredItemMutation>(ground.Container.Entries.Count);
            for (int index = 0; index < ground.Container.Entries.Count; index++)
            {
                items.Add(StoredItemMutation.Capture(ground.Container.Entries[index]));
            }

            return new GroundContainerMutation(
                ground.Id,
                ground.Container.Id,
                ground.Position,
                ground.Container.Definition.Key,
                ground.Container.Definition.DisplayName,
                ground.Container.Definition.Capacity,
                items);
        }

        private static int CompareItems(StoredItemMutation left, StoredItemMutation right)
        {
            int result = left.DefinitionId.CompareTo(right.DefinitionId);
            if (result != 0) return result;
            result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0) return result;
            return left.InstanceId.CompareTo(right.InstanceId);
        }
    }

    public sealed class ChunkMutationState
    {
        private readonly HarvestedResourceMutation[] _harvestedResources;
        private readonly GroundContainerMutation[] _groundContainers;
        private readonly IReadOnlyList<HarvestedResourceMutation> _readOnlyHarvestedResources;
        private readonly IReadOnlyList<GroundContainerMutation> _readOnlyGroundContainers;

        public ChunkMutationState(
            ChunkCoordinate coordinate,
            ChunkLayout layout,
            IEnumerable<HarvestedResourceMutation> harvestedResources,
            IEnumerable<GroundContainerMutation> groundContainers)
        {
            if (!layout.IsValid) throw new ArgumentException("A valid chunk layout is required.", nameof(layout));
            if (harvestedResources == null) throw new ArgumentNullException(nameof(harvestedResources));
            if (groundContainers == null) throw new ArgumentNullException(nameof(groundContainers));

            var resources = new List<HarvestedResourceMutation>(harvestedResources);
            resources.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 0; index < resources.Count; index++)
            {
                if (!ChunkPositioning.BelongsTo(resources[index].Position, coordinate, layout))
                {
                    throw new ArgumentException("A harvested resource is outside its owning chunk.", nameof(harvestedResources));
                }

                if (index > 0 && resources[index - 1].Id.Equals(resources[index].Id))
                {
                    throw new ArgumentException("Harvested resource identifiers must be unique.", nameof(harvestedResources));
                }
            }

            var containers = new List<GroundContainerMutation>();
            foreach (GroundContainerMutation container in groundContainers)
            {
                containers.Add(container ?? throw new ArgumentException("Ground mutations must not contain null values.", nameof(groundContainers)));
            }

            containers.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 0; index < containers.Count; index++)
            {
                if (!ChunkPositioning.BelongsTo(containers[index].Position, coordinate, layout))
                {
                    throw new ArgumentException("A ground container is outside its owning chunk.", nameof(groundContainers));
                }

                if (index > 0 && containers[index - 1].Id.Equals(containers[index].Id))
                {
                    throw new ArgumentException("Ground container identifiers must be unique.", nameof(groundContainers));
                }
            }

            Coordinate = coordinate;
            Layout = layout;
            _harvestedResources = resources.ToArray();
            _groundContainers = containers.ToArray();
            _readOnlyHarvestedResources = Array.AsReadOnly(_harvestedResources);
            _readOnlyGroundContainers = Array.AsReadOnly(_groundContainers);
        }

        public ChunkCoordinate Coordinate { get; }
        public ChunkLayout Layout { get; }
        public IReadOnlyList<HarvestedResourceMutation> HarvestedResources => _readOnlyHarvestedResources;
        public IReadOnlyList<GroundContainerMutation> GroundContainers => _readOnlyGroundContainers;
        public bool IsEmpty => _harvestedResources.Length == 0 && _groundContainers.Length == 0;
    }

    public sealed class ChunkMutationStore
    {
        private readonly Dictionary<ChunkCoordinate, ChunkMutationState> _states =
            new Dictionary<ChunkCoordinate, ChunkMutationState>();

        public int Count => _states.Count;

        public void Put(ChunkMutationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsEmpty)
            {
                _states.Remove(state.Coordinate);
                return;
            }

            _states[state.Coordinate] = state;
        }

        public bool Contains(ChunkCoordinate coordinate) => _states.ContainsKey(coordinate);

        public bool TryPeek(ChunkCoordinate coordinate, out ChunkMutationState state)
        {
            return _states.TryGetValue(coordinate, out state);
        }

        public bool TryTake(ChunkCoordinate coordinate, out ChunkMutationState state)
        {
            if (!_states.TryGetValue(coordinate, out state)) return false;
            _states.Remove(coordinate);
            return true;
        }

        public ChunkMutationState[] CaptureCanonicalStates()
        {
            var states = new List<ChunkMutationState>(_states.Values);
            states.Sort((left, right) =>
                left.Coordinate.CompareTo(right.Coordinate));
            return states.ToArray();
        }
    }

    public sealed class ActiveChunkState
    {
        private readonly List<ResourceState> _resources;
        private readonly List<GroundContainerState> _groundContainers;
        private readonly IReadOnlyList<ResourceState> _readOnlyResources;
        private readonly IReadOnlyList<GroundContainerState> _readOnlyGroundContainers;

        internal ActiveChunkState(
            PopulatedChunk baseline,
            List<ResourceState> resources,
            List<GroundContainerState> groundContainers)
        {
            Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _groundContainers = groundContainers ?? throw new ArgumentNullException(nameof(groundContainers));
            _readOnlyResources = _resources.AsReadOnly();
            _readOnlyGroundContainers = _groundContainers.AsReadOnly();
        }

        public PopulatedChunk Baseline { get; }
        public ChunkCoordinate Coordinate => Baseline.Coordinate;
        public IReadOnlyList<ResourceState> Resources => _readOnlyResources;
        public IReadOnlyList<GroundContainerState> GroundContainers => _readOnlyGroundContainers;

        public ResourceState FindResource(ResourceId id)
        {
            for (int index = 0; index < _resources.Count; index++)
            {
                if (_resources[index].Id.Equals(id)) return _resources[index];
            }

            return null;
        }

        public void AddGroundContainer(GroundContainerState ground)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (!ChunkPositioning.BelongsTo(ground.Position, Coordinate, Baseline.Layout))
            {
                throw new ArgumentException("The ground container is outside this active chunk.", nameof(ground));
            }

            for (int index = 0; index < _groundContainers.Count; index++)
            {
                if (_groundContainers[index].Id.Equals(ground.Id)
                    || _groundContainers[index].Container.Id.Equals(ground.Container.Id))
                {
                    throw new InvalidOperationException("The ground container is already owned by this chunk.");
                }
            }

            _groundContainers.Add(ground);
        }
    }

    public static class ChunkStateTransfer
    {
        public static ActiveChunkState CreateActive(PopulatedChunk baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            var resources = new List<ResourceState>(baseline.Resources.Count);
            for (int index = 0; index < baseline.Resources.Count; index++)
            {
                GeneratedResourcePlacement placement = baseline.Resources[index];
                resources.Add(new ResourceState(
                    placement.Id,
                    new WorldPosition(placement.Cell.X, placement.Cell.Y)));
            }

            return new ActiveChunkState(
                baseline,
                resources,
                new List<GroundContainerState>());
        }

        public static ChunkMutationState Extract(ActiveChunkState active)
        {
            if (active == null) throw new ArgumentNullException(nameof(active));
            var harvested = new List<HarvestedResourceMutation>();
            var knownIds = new HashSet<ResourceId>();
            for (int index = 0; index < active.Baseline.Resources.Count; index++)
            {
                knownIds.Add(active.Baseline.Resources[index].Id);
            }

            for (int index = 0; index < active.Resources.Count; index++)
            {
                ResourceState resource = active.Resources[index];
                if (resource == null || !knownIds.Contains(resource.Id))
                {
                    throw new InvalidOperationException("An active chunk contains an unknown resource identifier.");
                }

                GeneratedResourcePlacement placement = FindPlacement(active.Baseline, resource.Id);
                var expected = new WorldPosition(placement.Cell.X, placement.Cell.Y);
                if (!resource.Position.Equals(expected))
                {
                    throw new InvalidOperationException("An active resource moved away from its generated position.");
                }

                if (resource.Availability == ResourceAvailability.Harvested)
                {
                    harvested.Add(new HarvestedResourceMutation(resource.Id, resource.Position));
                }
            }

            var ground = new List<GroundContainerMutation>();
            for (int index = 0; index < active.GroundContainers.Count; index++)
            {
                GroundContainerState container = active.GroundContainers[index];
                if (container != null && !container.IsEmpty)
                {
                    ground.Add(GroundContainerMutation.Capture(container));
                }
            }

            return new ChunkMutationState(
                active.Coordinate,
                active.Baseline.Layout,
                harvested,
                ground);
        }

        public static ActiveChunkState Restore(
            PopulatedChunk baseline,
            ChunkMutationState mutation)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (mutation == null) return CreateActive(baseline);
            if (!baseline.Coordinate.Equals(mutation.Coordinate)
                || !baseline.Layout.Equals(mutation.Layout))
            {
                throw new ArgumentException("The mutation does not belong to the generated chunk.", nameof(mutation));
            }

            ActiveChunkState restored = CreateActive(baseline);
            for (int index = 0; index < mutation.HarvestedResources.Count; index++)
            {
                HarvestedResourceMutation harvested = mutation.HarvestedResources[index];
                GeneratedResourcePlacement placement = FindPlacement(baseline, harvested.Id);
                var expected = new WorldPosition(placement.Cell.X, placement.Cell.Y);
                if (!expected.Equals(harvested.Position))
                {
                    throw new ArgumentException("A harvested resource position contradicts generation.", nameof(mutation));
                }

                ResourceState resource = restored.FindResource(harvested.Id);
                if (!ResourceInteraction.Apply(
                    default,
                    restored.Resources,
                    expected,
                    0d).Succeeded
                    || resource.Availability != ResourceAvailability.Harvested)
                {
                    throw new InvalidOperationException("The harvested resource mutation could not be restored.");
                }
            }

            var groundIds = new HashSet<GroundContainerId>();
            var containerIds = new HashSet<ContainerId>();
            for (int index = 0; index < mutation.GroundContainers.Count; index++)
            {
                GroundContainerMutation stored = mutation.GroundContainers[index];
                if (!groundIds.Add(stored.Id) || !containerIds.Add(stored.ContainerId))
                {
                    throw new ArgumentException("Stored ground ownership is duplicated.", nameof(mutation));
                }

                restored.AddGroundContainer(RestoreGround(stored));
            }

            return restored;
        }

        public static GroundContainerState RestoreGround(GroundContainerMutation stored)
        {
            var container = new ContainerState(
                stored.ContainerId,
                new ContainerDefinition(
                    stored.DefinitionKey,
                    stored.DisplayName,
                    stored.Capacity));
            for (int index = 0; index < stored.Items.Count; index++)
            {
                StoredItemMutation item = stored.Items[index];
                var definition = new ItemDefinition(
                    item.DefinitionId,
                    item.DefinitionId.Value,
                    item.Kind,
                    item.UnitEncumbrance);
                AddItemResult result;
                if (item.Kind == ItemStateKind.Stackable)
                {
                    result = InventoryOperations.AddStack(container, definition, item.Quantity);
                }
                else
                {
                    result = InventoryOperations.AddUnique(
                        container,
                        definition,
                        new UniqueItemState(
                            item.DefinitionId,
                            item.InstanceId,
                            item.ContainedContainerId));
                }

                if (result.Accepted != item.Quantity)
                {
                    throw new ArgumentException("Stored ground contents exceed their container capacity.", nameof(stored));
                }
            }

            return new GroundContainerState(stored.Id, stored.Position, container);
        }

        private static GeneratedResourcePlacement FindPlacement(
            PopulatedChunk baseline,
            ResourceId id)
        {
            for (int index = 0; index < baseline.Resources.Count; index++)
            {
                if (baseline.Resources[index].Id.Equals(id)) return baseline.Resources[index];
            }

            throw new ArgumentException($"Unknown generated resource identifier {id}.", nameof(id));
        }
    }

    public static class ChunkEvictionPlanner
    {
        public static IReadOnlyList<ChunkCoordinate> Create(
            IEnumerable<ChunkCoordinate> loaded,
            ChunkCoordinate center,
            int retentionRadius)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            if (retentionRadius < 0 || retentionRadius > ChunkStreamingWindowSettings.MaximumRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(retentionRadius));
            }

            var result = new List<ChunkCoordinate>();
            var unique = new HashSet<ChunkCoordinate>();
            foreach (ChunkCoordinate coordinate in loaded)
            {
                if (!unique.Add(coordinate))
                {
                    throw new ArgumentException("Loaded chunk coordinates must be unique.", nameof(loaded));
                }

                if (AbsoluteDifference(coordinate.X, center.X) > (ulong)retentionRadius
                    || AbsoluteDifference(coordinate.Y, center.Y) > (ulong)retentionRadius)
                {
                    result.Add(coordinate);
                }
            }

            result.Sort();
            return result.AsReadOnly();
        }

        private static ulong AbsoluteDifference(long left, long right)
        {
            return left >= right
                ? unchecked((ulong)left - (ulong)right)
                : unchecked((ulong)right - (ulong)left);
        }
    }

    public sealed class ChunkStateLifecycle
    {
        private readonly IWorldPopulationGenerator _generator;
        private readonly Dictionary<ChunkCoordinate, ActiveChunkState> _active =
            new Dictionary<ChunkCoordinate, ActiveChunkState>();

        public ChunkStateLifecycle(
            IWorldPopulationGenerator generator,
            ChunkMutationStore store = null)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Store = store ?? new ChunkMutationStore();
        }

        public ChunkMutationStore Store { get; }
        public int ActiveCount => _active.Count;

        public ActiveChunkState Activate(ChunkCoordinate coordinate)
        {
            if (_active.ContainsKey(coordinate))
            {
                throw new InvalidOperationException("The chunk already has an active owner.");
            }

            PopulatedChunk baseline = _generator.Generate(coordinate);
            bool hadMutation = Store.TryTake(coordinate, out ChunkMutationState mutation);
            try
            {
                ActiveChunkState active = ChunkStateTransfer.Restore(baseline, mutation);
                _active.Add(coordinate, active);
                return active;
            }
            catch
            {
                if (hadMutation) Store.Put(mutation);
                throw;
            }
        }

        public ActiveChunkState GetOrActivate(ChunkCoordinate coordinate)
        {
            return _active.TryGetValue(coordinate, out ActiveChunkState active)
                ? active
                : Activate(coordinate);
        }

        public bool TryGetActive(ChunkCoordinate coordinate, out ActiveChunkState active)
        {
            return _active.TryGetValue(coordinate, out active);
        }

        public bool Evict(ChunkCoordinate coordinate)
        {
            if (!_active.TryGetValue(coordinate, out ActiveChunkState active)) return false;
            ChunkMutationState mutation = ChunkStateTransfer.Extract(active);
            _active.Remove(coordinate);
            Store.Put(mutation);
            return true;
        }

        public int EvictOutside(ChunkCoordinate center, int retentionRadius)
        {
            IReadOnlyList<ChunkCoordinate> plan = ChunkEvictionPlanner.Create(
                _active.Keys,
                center,
                retentionRadius);
            for (int index = 0; index < plan.Count; index++) Evict(plan[index]);
            return plan.Count;
        }

        internal IReadOnlyList<ChunkMutationState> CaptureCanonicalMutations()
        {
            var canonical = new List<ChunkMutationState>();
            var knownCoordinates = new HashSet<ChunkCoordinate>();

            ChunkMutationState[] stored = Store.CaptureCanonicalStates();
            for (int index = 0; index < stored.Length; index++)
            {
                ChunkMutationState mutation = stored[index];
                if (mutation == null || mutation.IsEmpty)
                {
                    throw new InvalidOperationException(
                        "The mutation store contains an invalid save-facing state.");
                }

                if (!knownCoordinates.Add(mutation.Coordinate))
                {
                    throw new InvalidOperationException(
                        "A chunk mutation coordinate is duplicated.");
                }

                canonical.Add(mutation);
            }

            foreach (KeyValuePair<ChunkCoordinate, ActiveChunkState> pair in _active)
            {
                ChunkMutationState mutation = ChunkStateTransfer.Extract(pair.Value);
                if (mutation.IsEmpty)
                {
                    continue;
                }

                if (!knownCoordinates.Add(mutation.Coordinate))
                {
                    throw new InvalidOperationException(
                        "A chunk cannot be active and stored at the same time.");
                }

                canonical.Add(mutation);
            }

            canonical.Sort((left, right) =>
                left.Coordinate.CompareTo(right.Coordinate));
            return Array.AsReadOnly(canonical.ToArray());
        }

        public IReadOnlyList<ChunkCoordinate> CopyActiveCoordinates()
        {
            var coordinates = new List<ChunkCoordinate>(_active.Keys);
            coordinates.Sort();
            return coordinates.AsReadOnly();
        }
    }
}
