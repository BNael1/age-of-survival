using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Crafting
{
    public readonly struct CraftRecipeId : IEquatable<CraftRecipeId>, IComparable<CraftRecipeId>
    {
        public CraftRecipeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A craft recipe identifier must not be empty.", nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public int CompareTo(CraftRecipeId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(CraftRecipeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CraftRecipeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CraftRecipeId left, CraftRecipeId right) => left.Equals(right);
        public static bool operator !=(CraftRecipeId left, CraftRecipeId right) => !left.Equals(right);
    }

    public readonly struct CraftMethodId : IEquatable<CraftMethodId>, IComparable<CraftMethodId>
    {
        public CraftMethodId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A craft method identifier must not be empty.", nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public int CompareTo(CraftMethodId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(CraftMethodId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CraftMethodId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public static class CraftMethodIds
    {
        public static readonly CraftMethodId Hand = new CraftMethodId("hand");
    }

    public sealed class CraftIngredientDefinition
    {
        public CraftIngredientDefinition(ItemDefinitionId definitionId, int quantity)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A craft ingredient requires a valid item identifier.", nameof(definitionId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A craft ingredient quantity must be positive.");
            DefinitionId = definitionId;
            Quantity = quantity;
        }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
    }

    public sealed class CraftOutputDefinition
    {
        public CraftOutputDefinition(ItemDefinitionId definitionId, int quantity)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A craft output requires a valid item identifier.", nameof(definitionId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A craft output quantity must be positive.");
            DefinitionId = definitionId;
            Quantity = quantity;
        }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
    }

    public sealed class CraftRecipeDefinition
    {
        private readonly IReadOnlyList<CraftIngredientDefinition> _ingredients;
        private readonly IReadOnlyList<CraftOutputDefinition> _outputs;

        public CraftRecipeDefinition(CraftRecipeId id, string displayName, CraftMethodId methodId, int durationTicks,
            IEnumerable<CraftIngredientDefinition> ingredients, IEnumerable<CraftOutputDefinition> outputs)
        {
            if (!id.IsValid) throw new ArgumentException("A craft recipe requires a valid identifier.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A craft recipe requires a display name.", nameof(displayName));
            if (!methodId.IsValid) throw new ArgumentException("A craft recipe requires a valid method identifier.", nameof(methodId));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks), durationTicks, "A craft duration must be positive.");
            _ingredients = CanonicalIngredients(ingredients);
            _outputs = CanonicalOutputs(outputs);
            Id = id;
            DisplayName = displayName;
            MethodId = methodId;
            DurationTicks = durationTicks;
        }

        public CraftRecipeId Id { get; }
        public string DisplayName { get; }
        public CraftMethodId MethodId { get; }
        public int DurationTicks { get; }
        public IReadOnlyList<CraftIngredientDefinition> Ingredients => _ingredients;
        public IReadOnlyList<CraftOutputDefinition> Outputs => _outputs;

        private static IReadOnlyList<CraftIngredientDefinition> CanonicalIngredients(IEnumerable<CraftIngredientDefinition> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = new List<CraftIngredientDefinition>();
            var ids = new HashSet<ItemDefinitionId>();
            foreach (CraftIngredientDefinition value in values)
            {
                if (value == null) throw new ArgumentException("Craft ingredients must not contain null entries.", nameof(values));
                if (!ids.Add(value.DefinitionId)) throw new ArgumentException("A craft recipe cannot declare the same ingredient twice.", nameof(values));
                result.Add(value);
            }
            if (result.Count == 0) throw new ArgumentException("A craft recipe requires at least one ingredient.", nameof(values));
            return result.AsReadOnly();
        }

        private static IReadOnlyList<CraftOutputDefinition> CanonicalOutputs(IEnumerable<CraftOutputDefinition> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = new List<CraftOutputDefinition>();
            var ids = new HashSet<ItemDefinitionId>();
            foreach (CraftOutputDefinition value in values)
            {
                if (value == null) throw new ArgumentException("Craft outputs must not contain null entries.", nameof(values));
                if (!ids.Add(value.DefinitionId)) throw new ArgumentException("A craft recipe cannot declare the same output twice.", nameof(values));
                result.Add(value);
            }
            if (result.Count == 0) throw new ArgumentException("A craft recipe requires at least one output.", nameof(values));
            return result.AsReadOnly();
        }
    }

    public sealed class CraftRecipeCatalog
    {
        private readonly Dictionary<CraftRecipeId, CraftRecipeDefinition> _byId;
        private readonly IReadOnlyList<CraftRecipeDefinition> _recipes;

        public CraftRecipeCatalog(IEnumerable<CraftRecipeDefinition> recipes)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            _byId = new Dictionary<CraftRecipeId, CraftRecipeDefinition>();
            var result = new List<CraftRecipeDefinition>();
            foreach (CraftRecipeDefinition recipe in recipes)
            {
                if (recipe == null) throw new ArgumentException("Craft recipes must not contain null entries.", nameof(recipes));
                if (_byId.ContainsKey(recipe.Id)) throw new ArgumentException("Craft recipe identifiers must be unique.", nameof(recipes));
                _byId.Add(recipe.Id, recipe);
                result.Add(recipe);
            }
            if (result.Count == 0) throw new ArgumentException("A craft catalog requires at least one recipe.", nameof(recipes));
            result.Sort((left, right) => left.Id.CompareTo(right.Id));
            _recipes = result.AsReadOnly();
        }

        public IReadOnlyList<CraftRecipeDefinition> Recipes => _recipes;
        public bool TryFind(CraftRecipeId id, out CraftRecipeDefinition recipe)
        {
            if (!id.IsValid) { recipe = null; return false; }
            return _byId.TryGetValue(id, out recipe);
        }
        public CraftRecipeDefinition Require(CraftRecipeId id)
        {
            if (!TryFind(id, out CraftRecipeDefinition recipe)) throw new KeyNotFoundException($"Unknown craft recipe '{id}'.");
            return recipe;
        }
    }
}
