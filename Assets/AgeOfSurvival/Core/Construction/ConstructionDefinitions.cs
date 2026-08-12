using System;
using System.Collections.Generic;
using System.Globalization;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Construction
{
    public readonly struct ConstructionDefinitionId :
        IEquatable<ConstructionDefinitionId>,
        IComparable<ConstructionDefinitionId>
    {
        public ConstructionDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A construction definition identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ConstructionDefinitionId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(ConstructionDefinitionId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ConstructionDefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ConstructionDefinitionId left, ConstructionDefinitionId right) => left.Equals(right);
        public static bool operator !=(ConstructionDefinitionId left, ConstructionDefinitionId right) => !left.Equals(right);
    }

    public readonly struct ConstructionInstanceId :
        IEquatable<ConstructionInstanceId>,
        IComparable<ConstructionInstanceId>
    {
        public ConstructionInstanceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A construction instance identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ConstructionInstanceId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(ConstructionInstanceId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ConstructionInstanceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ConstructionInstanceId left, ConstructionInstanceId right) => left.Equals(right);
        public static bool operator !=(ConstructionInstanceId left, ConstructionInstanceId right) => !left.Equals(right);
    }

    /// <summary>
    /// Shared durable sequence policy for construction instance identifiers.
    /// The terminal sequence is an explicit exhausted sentinel: it is persisted,
    /// but it never yields a candidate and therefore cannot overflow.
    /// </summary>
    public static class ConstructionInstanceIdSequencePolicy
    {
        public const int MaximumCollisionAttempts = 1024;
        public const long InitialSequence = 1L;
        public const long ExhaustedSequence = long.MaxValue;

        public static bool TryFindCommittableCandidate(
            string instanceNamespace,
            long nextSequence,
            Func<ConstructionInstanceId, bool> isAvailable,
            out ConstructionInstanceId candidate)
        {
            if (string.IsNullOrWhiteSpace(instanceNamespace))
                throw new ArgumentException("An instance namespace is required.", nameof(instanceNamespace));
            if (nextSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            if (isAvailable == null) throw new ArgumentNullException(nameof(isAvailable));

            if (nextSequence == ExhaustedSequence)
            {
                candidate = default;
                return false;
            }

            string normalizedNamespace = instanceNamespace.Trim();
            long sequence = nextSequence;
            for (int attempts = 0;
                 attempts < MaximumCollisionAttempts
                 && sequence < ExhaustedSequence;
                 attempts++)
            {
                candidate = Create(normalizedNamespace, sequence);
                if (isAvailable(candidate)) return true;
                sequence++;
            }

            candidate = default;
            return false;
        }

        public static bool TryGetCommittedNextSequence(
            string instanceNamespace,
            long currentNextSequence,
            ConstructionInstanceId candidate,
            out long committedNextSequence)
        {
            committedNextSequence = default;
            if (currentNextSequence <= 0L
                || currentNextSequence == ExhaustedSequence
                || !TryParseOwnedSequence(
                    candidate,
                    instanceNamespace,
                    out long committed)
                || committed < currentNextSequence
                || committed == ExhaustedSequence)
            {
                return false;
            }

            committedNextSequence = committed + 1L;
            return true;
        }

        public static bool TryParseOwnedSequence(
            ConstructionInstanceId instanceId,
            string instanceNamespace,
            out long sequence)
        {
            sequence = default;
            if (!instanceId.IsValid || string.IsNullOrWhiteSpace(instanceNamespace))
                return false;

            string prefix = instanceNamespace.Trim() + ":";
            if (!instanceId.Value.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string suffix = instanceId.Value.Substring(prefix.Length);
            return long.TryParse(
                    suffix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out sequence)
                && sequence > 0L
                && string.Equals(
                    suffix,
                    sequence.ToString("D10", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
        }

        public static bool IsOwnedByNamespace(
            ConstructionInstanceId instanceId,
            string instanceNamespace)
        {
            if (!instanceId.IsValid || string.IsNullOrWhiteSpace(instanceNamespace))
                return false;
            return instanceId.Value.StartsWith(
                instanceNamespace.Trim() + ":",
                StringComparison.Ordinal);
        }

        private static ConstructionInstanceId Create(
            string instanceNamespace,
            long sequence)
        {
            return new ConstructionInstanceId(
                instanceNamespace + ":" + sequence.ToString(
                    "D10",
                    CultureInfo.InvariantCulture));
        }
    }

    public sealed class ConstructionMaterialRequirement
    {
        public ConstructionMaterialRequirement(ItemDefinitionId definitionId, int quantity)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException("A construction material requires a valid item definition identifier.", nameof(definitionId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A construction material quantity must be positive.");
            }

            DefinitionId = definitionId;
            Quantity = quantity;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
    }

    public readonly struct ConstructionMaterialQuantity : IEquatable<ConstructionMaterialQuantity>
    {
        public ConstructionMaterialQuantity(ItemDefinitionId definitionId, int quantity)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException("A construction material quantity requires a valid item definition identifier.", nameof(definitionId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A construction material quantity must be positive.");
            }

            DefinitionId = definitionId;
            Quantity = quantity;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }

        public bool Equals(ConstructionMaterialQuantity other) =>
            DefinitionId.Equals(other.DefinitionId) && Quantity == other.Quantity;

        public override bool Equals(object obj) => obj is ConstructionMaterialQuantity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (DefinitionId.GetHashCode() * 397) ^ Quantity;
            }
        }

        public override string ToString() => $"{DefinitionId} x{Quantity}";
    }

    public sealed class ConstructionDefinition
    {
        private readonly IReadOnlyList<ConstructionMaterialRequirement> _materials;

        public ConstructionDefinition(
            ConstructionDefinitionId id,
            ConstructionSpaceKind spaceKind,
            int requiredWorkUnits,
            IEnumerable<ConstructionMaterialRequirement> materials)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A construction definition requires a valid identifier.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(ConstructionSpaceKind), spaceKind))
            {
                throw new ArgumentOutOfRangeException(nameof(spaceKind), spaceKind, "Unknown construction space kind.");
            }

            if (requiredWorkUnits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredWorkUnits), requiredWorkUnits, "Required construction work must be positive.");
            }

            Id = id;
            SpaceKind = spaceKind;
            RequiredWorkUnits = requiredWorkUnits;
            _materials = CanonicalMaterials(materials);
        }

        public ConstructionDefinitionId Id { get; }
        public ConstructionSpaceKind SpaceKind { get; }
        public int RequiredWorkUnits { get; }
        public IReadOnlyList<ConstructionMaterialRequirement> Materials => _materials;

        public bool TryFindMaterial(ItemDefinitionId definitionId, out ConstructionMaterialRequirement material)
        {
            if (!definitionId.IsValid)
            {
                material = null;
                return false;
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i].DefinitionId.Equals(definitionId))
                {
                    material = _materials[i];
                    return true;
                }
            }

            material = null;
            return false;
        }

        private static IReadOnlyList<ConstructionMaterialRequirement> CanonicalMaterials(
            IEnumerable<ConstructionMaterialRequirement> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var result = new List<ConstructionMaterialRequirement>();
            var ids = new HashSet<ItemDefinitionId>();
            foreach (ConstructionMaterialRequirement material in materials)
            {
                if (material == null)
                {
                    throw new ArgumentException("Construction materials must not contain null entries.", nameof(materials));
                }

                if (!ids.Add(material.DefinitionId))
                {
                    throw new ArgumentException("A construction definition cannot declare the same material twice.", nameof(materials));
                }

                result.Add(material);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("A construction definition requires at least one material.", nameof(materials));
            }

            result.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));
            return result.AsReadOnly();
        }
    }

    public sealed class ConstructionDefinitionCatalog
    {
        private readonly Dictionary<ConstructionDefinitionId, ConstructionDefinition> _byId;
        private readonly IReadOnlyList<ConstructionDefinition> _definitions;

        public ConstructionDefinitionCatalog(IEnumerable<ConstructionDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _byId = new Dictionary<ConstructionDefinitionId, ConstructionDefinition>();
            var result = new List<ConstructionDefinition>();
            foreach (ConstructionDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Construction definitions must not contain null entries.", nameof(definitions));
                }

                if (_byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException("Construction definition identifiers must be unique.", nameof(definitions));
                }

                _byId.Add(definition.Id, definition);
                result.Add(definition);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("A construction catalog requires at least one definition.", nameof(definitions));
            }

            result.Sort((left, right) => left.Id.CompareTo(right.Id));
            _definitions = result.AsReadOnly();
        }

        public IReadOnlyList<ConstructionDefinition> Definitions => _definitions;

        public bool TryFind(ConstructionDefinitionId id, out ConstructionDefinition definition)
        {
            if (!id.IsValid)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }

        public ConstructionDefinition Require(ConstructionDefinitionId id)
        {
            if (!TryFind(id, out ConstructionDefinition definition))
            {
                throw new KeyNotFoundException($"Unknown construction definition '{id}'.");
            }

            return definition;
        }
    }
}
