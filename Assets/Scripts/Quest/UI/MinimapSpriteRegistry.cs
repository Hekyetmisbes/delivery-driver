using UnityEngine;

namespace DeliveryDriver.Quest.UI
{
    [CreateAssetMenu(fileName = "MinimapSpriteRegistry", menuName = "Delivery Driver/UI/Minimap Sprite Registry")]
    public class MinimapSpriteRegistry : ScriptableObject
    {
        [SerializeField] private bool useAtlasPlayerArrow;
        [SerializeField] private Texture2D atlasTexture;
        [SerializeField] private Rect playerArrowRect = new Rect(288f, 162f, 16f, 16f);
        [SerializeField] private Vector2 playerArrowPivot = new Vector2(0.5f, 0.5f);
        [SerializeField] private float pixelsPerUnit = 100f;

        private Sprite cachedPlayerArrowSprite;
        private bool loggedMissingAtlas;
        private static Sprite fallbackPlayerArrowSprite;

        public Sprite GetPlayerArrowSprite()
        {
            if (cachedPlayerArrowSprite != null)
            {
                return cachedPlayerArrowSprite;
            }

            if (!useAtlasPlayerArrow)
            {
                cachedPlayerArrowSprite = GetFallbackPlayerArrowSprite();
                return cachedPlayerArrowSprite;
            }

            if (atlasTexture == null)
            {
                if (!loggedMissingAtlas)
                {
                    Debug.LogError("[MinimapSpriteRegistry] atlasTexture is not assigned. Falling back to a runtime-generated player arrow sprite.");
                    loggedMissingAtlas = true;
                }

                return GetFallbackPlayerArrowSprite();
            }

            if (!IsValidSpriteRect(atlasTexture, playerArrowRect))
            {
                Debug.LogError($"[MinimapSpriteRegistry] playerArrowRect {playerArrowRect} is outside atlasTexture bounds ({atlasTexture.width}x{atlasTexture.height}). Falling back to a runtime-generated player arrow sprite.");
                return GetFallbackPlayerArrowSprite();
            }

            cachedPlayerArrowSprite = Sprite.Create(
                atlasTexture,
                playerArrowRect,
                playerArrowPivot,
                pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            cachedPlayerArrowSprite.name = "MinimapPlayerArrow";
            return cachedPlayerArrowSprite;
        }

        public static Sprite GetFallbackPlayerArrowSprite()
        {
            if (fallbackPlayerArrowSprite != null)
            {
                return fallbackPlayerArrowSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false, true);
            texture.name = "RuntimeMinimapPlayerArrow";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 fill = new Color32(255, 255, 255, 255);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX = (x + 0.5f) / size;
                    float normalizedY = (y + 0.5f) / size;
                    float centeredX = Mathf.Abs(normalizedX - 0.5f);
                    bool isArrowHead =
                        normalizedY >= 0.52f &&
                        centeredX <= (normalizedY - 0.52f) * 0.95f;
                    bool isShoulder =
                        normalizedY >= 0.44f &&
                        normalizedY < 0.52f &&
                        centeredX <= Mathf.Lerp(0.07f, 0.16f, (normalizedY - 0.44f) / 0.08f);
                    bool isStem =
                        normalizedY >= 0.16f &&
                        normalizedY < 0.44f &&
                        centeredX <= 0.07f;
                    bool isTailCut =
                        normalizedY < 0.28f &&
                        centeredX < 0.03f;

                    if ((isArrowHead || isShoulder || isStem) && !isTailCut)
                    {
                        pixels[(y * size) + x] = fill;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            fallbackPlayerArrowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            fallbackPlayerArrowSprite.name = "RuntimeMinimapPlayerArrow";

            return fallbackPlayerArrowSprite;
        }

        private static bool IsValidSpriteRect(Texture2D atlas, Rect rect)
        {
            return atlas != null &&
                   rect.width > 0f &&
                   rect.height > 0f &&
                   rect.xMin >= 0f &&
                   rect.yMin >= 0f &&
                   rect.xMax <= atlas.width &&
                   rect.yMax <= atlas.height;
        }
    }
}
