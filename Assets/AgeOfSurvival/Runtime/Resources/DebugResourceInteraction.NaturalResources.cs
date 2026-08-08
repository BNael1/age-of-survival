using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Resources
{
    public sealed partial class DebugResourceInteraction
    {
        private readonly Dictionary<ResourceDefinitionId, Sprite>
            _naturalResourceSprites =
                new Dictionary<ResourceDefinitionId, Sprite>();
        private readonly Dictionary<ItemDefinitionId, Sprite>
            _naturalGroundSprites =
                new Dictionary<ItemDefinitionId, Sprite>();

        private bool TryCreateNaturalResourceVisualAssets()
        {
            _naturalResourceSprites.Clear();
            _naturalGroundSprites.Clear();

            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Shrub,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.ResourceShrub,
                    new Vector2(
                        ResourceGroundPivotX,
                        ResourceGroundPivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Resource Shrub"));
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.LooseStone,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.ResourceStone,
                    new Vector2(
                        ResourceGroundPivotX,
                        ResourceGroundPivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Resource Loose Stone"));
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Deadwood,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.ResourceDeadwood,
                    new Vector2(
                        ResourceGroundPivotX,
                        ResourceGroundPivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Resource Deadwood"));
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Tree,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.ResourceTree,
                    new Vector2(
                        ResourceGroundPivotX,
                        ResourceGroundPivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Resource Tree"));

            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Branches.Id,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.GroundBranches,
                    new Vector2(
                        ResourceGroundPivotX,
                        GroundPilePivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Ground Branches"));
            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Stones.Id,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.GroundStones,
                    new Vector2(
                        ResourceGroundPivotX,
                        GroundPilePivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Ground Stones"));
            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Wood.Id,
                PrototypeVisualAssets.CreateSprite(
                    PrototypeVisualAssets.GroundWood,
                    new Vector2(
                        ResourceGroundPivotX,
                        GroundPilePivotY),
                    PrototypeVisualAssets.PixelsPerUnit,
                    "Prototype Ground Wood"));

            _resourceSprite =
                _naturalResourceSprites[ResourceDefinitionIds.Shrub];
            _groundSprite =
                _naturalGroundSprites[
                    InventoryPrototypeCatalog.Branches.Id];

            foreach (Sprite sprite in _naturalResourceSprites.Values)
            {
                if (sprite == null)
                {
                    return false;
                }
            }

            foreach (Sprite sprite in _naturalGroundSprites.Values)
            {
                if (sprite == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void BindFallbackNaturalResourceSprites()
        {
            _naturalResourceSprites.Clear();
            _naturalGroundSprites.Clear();

            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Shrub,
                _resourceSprite);
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.LooseStone,
                _resourceSprite);
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Deadwood,
                _resourceSprite);
            _naturalResourceSprites.Add(
                ResourceDefinitionIds.Tree,
                _resourceSprite);

            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Branches.Id,
                _groundSprite);
            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Stones.Id,
                _groundSprite);
            _naturalGroundSprites.Add(
                InventoryPrototypeCatalog.Wood.Id,
                _groundSprite);
        }

        private Sprite ResourceSpriteFor(
            ResourceDefinitionId definitionId)
        {
            return _naturalResourceSprites.TryGetValue(
                definitionId,
                out Sprite sprite)
                ? sprite
                : _resourceSprite;
        }

        private Sprite GroundSpriteFor(
            ItemDefinitionId definitionId)
        {
            return _naturalGroundSprites.TryGetValue(
                definitionId,
                out Sprite sprite)
                ? sprite
                : _groundSprite;
        }

        public bool TryGetBodySprite(
            ResourceDefinitionId definitionId,
            out Sprite sprite)
        {
            return _naturalResourceSprites.TryGetValue(
                definitionId,
                out sprite)
                && sprite != null;
        }

        private int GroundQuantityFor(
            ResourceState resource,
            GroundContainerState ground,
            out ItemDefinitionId displayedDefinitionId)
        {
            displayedDefinitionId = default;
            if (resource == null || ground == null)
            {
                return 0;
            }

            ResourceDefinition definition =
                ResourcePrototypeCatalog.Definitions.Require(
                    resource.DefinitionId);
            int total = 0;
            for (int index = 0;
                 index < definition.Yields.Count;
                 index++)
            {
                ItemDefinitionId itemId =
                    definition.Yields[index].Definition.Id;
                int quantity = InventoryOperations.Count(
                    ground.Container,
                    itemId);
                if (quantity > 0
                    && !displayedDefinitionId.IsValid)
                {
                    displayedDefinitionId = itemId;
                }

                total = checked(total + quantity);
            }

            return total;
        }

        private void DestroyNaturalResourcePrototypeSprites()
        {
            var unique = new HashSet<Sprite>();
            foreach (Sprite sprite in _naturalResourceSprites.Values)
            {
                if (sprite != null)
                {
                    unique.Add(sprite);
                }
            }

            foreach (Sprite sprite in _naturalGroundSprites.Values)
            {
                if (sprite != null)
                {
                    unique.Add(sprite);
                }
            }

            foreach (Sprite sprite in unique)
            {
                PrototypeVisualAssets.DestroyRuntimeSprite(sprite);
            }

            _naturalResourceSprites.Clear();
            _naturalGroundSprites.Clear();
            _resourceSprite = null;
            _groundSprite = null;
        }

        private void ClearNaturalResourceSpriteMaps()
        {
            _naturalResourceSprites.Clear();
            _naturalGroundSprites.Clear();
        }
    }
}
