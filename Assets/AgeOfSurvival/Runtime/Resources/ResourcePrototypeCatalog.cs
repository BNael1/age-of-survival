using System;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Inventory;

namespace AgeOfSurvival.Runtime.Resources
{
    /// <summary>
    /// Provisional editorial data for the first complete natural-resource slice.
    /// Core owns the rules; these values remain replaceable tuning/content.
    /// </summary>
    public static class ResourcePrototypeCatalog
    {
        public static readonly ResourceDefinition Shrub =
            new ResourceDefinition(
                ResourceDefinitionIds.Shrub,
                "Shrub",
                new[]
                {
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Branches,
                        6)
                });

        public static readonly ResourceDefinition LooseStone =
            new ResourceDefinition(
                ResourceDefinitionIds.LooseStone,
                "Loose stone",
                new[]
                {
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Stones,
                        3)
                });

        public static readonly ResourceDefinition Deadwood =
            new ResourceDefinition(
                ResourceDefinitionIds.Deadwood,
                "Deadwood",
                new[]
                {
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Wood,
                        2),
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Branches,
                        2)
                });

        public static readonly ResourceDefinition Tree =
            new ResourceDefinition(
                ResourceDefinitionIds.Tree,
                "Tree",
                new[]
                {
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Wood,
                        6),
                    new ResourceYieldDefinition(
                        InventoryPrototypeCatalog.Branches,
                        3)
                });

        public static readonly ResourceDefinitionCatalog Definitions =
            new ResourceDefinitionCatalog(
                new[]
                {
                    Shrub,
                    LooseStone,
                    Deadwood,
                    Tree
                });

        public static ResourceDefinitionId DefinitionIdFor(
            GeneratedResourceKind kind)
        {
            switch (kind)
            {
                case GeneratedResourceKind.Shrub:
                    return ResourceDefinitionIds.Shrub;
                case GeneratedResourceKind.LooseStone:
                    return ResourceDefinitionIds.LooseStone;
                case GeneratedResourceKind.Deadwood:
                    return ResourceDefinitionIds.Deadwood;
                case GeneratedResourceKind.Tree:
                    return ResourceDefinitionIds.Tree;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unknown generated natural-resource kind.");
            }
        }

        public static ResourceDefinition DefinitionFor(
            GeneratedResourceKind kind)
        {
            return Definitions.Require(DefinitionIdFor(kind));
        }
    }
}
