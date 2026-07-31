using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Centralized loader for the temporary prototype readability sprites.
    /// The imported textures are project-owned and replaceable; callers own only
    /// the runtime Sprite wrappers returned by this class.
    /// </summary>
    public static class PrototypeVisualAssets
    {
        public const float PixelsPerUnit = 64f;
        public const string GroundGrass = "ground_grass";
        public const string GroundDirt = "ground_dirt";
        public const string GroundWater = "ground_water";
        public const string PlayerSurvivor = "player_survivor";
        public const string ResourceShrub = "resource_shrub";
        public const string GroundBranches = "ground_branches";
        public const string TargetRing = "target_ring";
        public const string InteractionRadius = "interaction_radius";
        public const string UiPixel = "ui_pixel";

        private const string ResourceRoot = "PrototypeVisuals/";

        public static Sprite CreateSprite(
            string resourceName,
            Vector2 pivot,
            float pixelsPerUnit,
            string spriteName)
        {
            Texture2D texture = UnityEngine.Resources.Load<Texture2D>(
                ResourceRoot + resourceName);
            if (texture == null)
            {
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            return sprite;
        }

        public static bool TextureExists(string resourceName)
        {
            return UnityEngine.Resources.Load<Texture2D>(
                ResourceRoot + resourceName) != null;
        }

        public static bool AllRequiredTexturesExist()
        {
            return TextureExists(GroundGrass)
                && TextureExists(GroundDirt)
                && TextureExists(GroundWater)
                && TextureExists(PlayerSurvivor)
                && TextureExists(ResourceShrub)
                && TextureExists(GroundBranches)
                && TextureExists(TargetRing)
                && TextureExists(InteractionRadius)
                && TextureExists(UiPixel);
        }

        public static void DestroyRuntimeSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(sprite);
            }
            else
            {
                Object.DestroyImmediate(sprite);
            }
        }
    }
}
