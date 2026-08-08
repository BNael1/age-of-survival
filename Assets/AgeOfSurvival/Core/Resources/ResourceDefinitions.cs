using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Resources
{
    public readonly struct ResourceDefinitionId :
        IEquatable<ResourceDefinitionId>,
        IComparable<ResourceDefinitionId>
    {
        public ResourceDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A resource definition identifier must not be empty.",
                    nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ResourceDefinitionId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(ResourceDefinitionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceDefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(
            ResourceDefinitionId left,
            ResourceDefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ResourceDefinitionId left,
            ResourceDefinitionId right)
        {
            return !left.Equals(right);
        }
    }

    public static class ResourceDefinitionIds
    {
        public static readonly ResourceDefinitionId Shrub =
            new ResourceDefinitionId("shrub");
        public static readonly ResourceDefinitionId LooseStone =
            new ResourceDefinitionId("loose-stone");
        public static readonly ResourceDefinitionId Deadwood =
            new ResourceDefinitionId("deadwood");
        public static readonly ResourceDefinitionId Tree =
            new ResourceDefinitionId("tree");
    }

    public sealed class ResourceYieldDefinition
    {
        public ResourceYieldDefinition(
            ItemDefinition definition,
            int quantity)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.StateKind != ItemStateKind.Stackable)
            {
                throw new ArgumentException(
                    "Natural resource yields must be stackable.",
                    nameof(definition));
            }

            if (definition.Perishable != null)
            {
                throw new ArgumentException(
                    "Ground perishable persistence is outside this resource slice.",
                    nameof(definition));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "A resource yield quantity must be positive.");
            }

            Definition = definition;
            Quantity = quantity;
        }

        public ItemDefinition Definition { get; }
        public int Quantity { get; }
    }

    public sealed class ResourceDefinition
    {
        private readonly ResourceYieldDefinition[] _yields;
        private readonly IReadOnlyList<ResourceYieldDefinition> _readOnlyYields;

        public ResourceDefinition(
            ResourceDefinitionId id,
            string displayName,
            IEnumerable<ResourceYieldDefinition> yields)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A valid resource definition identifier is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A resource definition display name is required.",
                    nameof(displayName));
            }

            if (yields == null)
            {
                throw new ArgumentNullException(nameof(yields));
            }

            var canonical = new List<ResourceYieldDefinition>();
            var itemIds = new HashSet<ItemDefinitionId>();
            foreach (ResourceYieldDefinition yield in yields)
            {
                if (yield == null)
                {
                    throw new ArgumentException(
                        "Resource yields must not contain null entries.",
                        nameof(yields));
                }

                if (!itemIds.Add(yield.Definition.Id))
                {
                    throw new ArgumentException(
                        "A resource cannot declare the same yield definition twice.",
                        nameof(yields));
                }

                canonical.Add(yield);
            }

            if (canonical.Count == 0)
            {
                throw new ArgumentException(
                    "A harvestable resource requires at least one yield.",
                    nameof(yields));
            }

            Id = id;
            DisplayName = displayName;
            _yields = canonical.ToArray();
            _readOnlyYields = Array.AsReadOnly(_yields);
        }

        public ResourceDefinitionId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceYieldDefinition> Yields => _readOnlyYields;
        public ResourceYieldDefinition PrimaryYield => _yields[0];
    }

    public sealed class ResourceDefinitionCatalog
    {
        private readonly Dictionary<ResourceDefinitionId, ResourceDefinition> _byId;
        private readonly ResourceDefinition[] _definitions;
        private readonly IReadOnlyList<ResourceDefinition> _readOnlyDefinitions;

        public ResourceDefinitionCatalog(IEnumerable<ResourceDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _byId = new Dictionary<ResourceDefinitionId, ResourceDefinition>();
            var canonical = new List<ResourceDefinition>();
            foreach (ResourceDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Resource definitions must not contain null entries.",
                        nameof(definitions));
                }

                if (_byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Resource definition identifiers must be unique.",
                        nameof(definitions));
                }

                _byId.Add(definition.Id, definition);
                canonical.Add(definition);
            }

            if (canonical.Count == 0)
            {
                throw new ArgumentException(
                    "A resource catalog requires at least one definition.",
                    nameof(definitions));
            }

            canonical.Sort((left, right) => left.Id.CompareTo(right.Id));
            _definitions = canonical.ToArray();
            _readOnlyDefinitions = Array.AsReadOnly(_definitions);
        }

        public IReadOnlyList<ResourceDefinition> Definitions => _readOnlyDefinitions;

        public bool TryFind(
            ResourceDefinitionId id,
            out ResourceDefinition definition)
        {
            if (!id.IsValid)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }

        public ResourceDefinition Require(ResourceDefinitionId id)
        {
            if (!TryFind(id, out ResourceDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"Unknown resource definition '{id}'.");
            }

            return definition;
        }
    }
}
