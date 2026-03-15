using UnityEngine;

namespace DeliveryDriver.Quest.UI
{
    [CreateAssetMenu(fileName = "MinimapSpriteRegistry", menuName = "Delivery Driver/UI/Minimap Sprite Registry")]
    public class MinimapSpriteRegistry : ScriptableObject
    {
        [SerializeField] private GameObject playerArrowPrefab;
        [SerializeField] private bool useAtlasPlayerArrow;
        [SerializeField] private Texture2D atlasTexture;
        [SerializeField] private Rect playerArrowRect = new Rect(288f, 162f, 16f, 16f);
        [SerializeField] private Vector2 playerArrowPivot = new Vector2(0.5f, 0.5f);
        [SerializeField] private float pixelsPerUnit = 100f;

        private Sprite cachedPlayerArrowSprite;
        private bool loggedMissingAtlas;
        private bool loggedMissingPrefab;
        private static Sprite fallbackPlayerArrowSprite;
        private static bool loggedFallbackUsage;

        public Sprite GetPlayerArrowSprite()
        {
            if (cachedPlayerArrowSprite != null)
            {
                return cachedPlayerArrowSprite;
            }

            if (TryCreateSpriteFromPrefab(out Sprite prefabSprite))
            {
                cachedPlayerArrowSprite = prefabSprite;
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

            const int size = 32;
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
                    bool isArrowHead =
                        normalizedY >= 0.42f &&
                        Mathf.Abs(normalizedX - 0.5f) <= (normalizedY - 0.42f) * 1.12f;
                    bool isArrowStem =
                        normalizedY >= 0.12f &&
                        normalizedY < 0.50f &&
                        Mathf.Abs(normalizedX - 0.5f) <= 0.09f;

                    if (isArrowHead || isArrowStem)
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

            if (!loggedFallbackUsage)
            {
                Debug.LogWarning("[MinimapSpriteRegistry] Using runtime-generated fallback player arrow sprite.");
                loggedFallbackUsage = true;
            }

            return fallbackPlayerArrowSprite;
        }

        private bool TryCreateSpriteFromPrefab(out Sprite sprite)
        {
            sprite = null;
            GameObject prefab = playerArrowPrefab;
            if (ReferenceEquals(prefab, null))
            {
                return false;
            }

            try
            {
                if (!prefab)
                {
                    return false;
                }

                MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>(true);
                Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
                Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                Texture2D texture = renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.mainTexture as Texture2D
                    : null;
                if (mesh == null || texture == null)
                {
                    return false;
                }

                Vector2[] uv = mesh.uv;
                if (uv == null || uv.Length == 0)
                {
                    return false;
                }

                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < uv.Length; i++)
                {
                    min = Vector2.Min(min, uv[i]);
                    max = Vector2.Max(max, uv[i]);
                }

                min = Vector2.Max(Vector2.zero, min);
                max = Vector2.Min(Vector2.one, max);
                float width = (max.x - min.x) * texture.width;
                float height = (max.y - min.y) * texture.height;
                if (width <= 1f || height <= 1f)
                {
                    return false;
                }

                Rect rect = new Rect(
                    min.x * texture.width,
                    min.y * texture.height,
                    width,
                    height);
                sprite = Sprite.Create(texture, rect, playerArrowPivot, pixelsPerUnit, 0u, SpriteMeshType.FullRect);
                if (sprite != null)
                {
                    sprite.name = $"{prefab.name}_Sprite";
                }

                loggedMissingPrefab = false;
                return sprite != null;
            }
            catch (MissingReferenceException)
            {
                if (!loggedMissingPrefab)
                {
                    Debug.LogWarning("[MinimapSpriteRegistry] playerArrowPrefab reference is missing. Falling back to atlas/runtime arrow sprite.");
                    loggedMissingPrefab = true;
                }

                playerArrowPrefab = null;
                return false;
            }
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
