using System;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Immutable editorial definition for one kind of inventory container.
    /// </summary>
    public sealed class ContainerDefinition
    {
        public ContainerDefinition(string key, string displayName, EncumbranceValue capacity)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A container definition requires a stable key.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A container definition requires a display name.", nameof(displayName));
            }

            Key = key;
            DisplayName = displayName;
            Capacity = capacity;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public EncumbranceValue Capacity { get; }
    }
}
