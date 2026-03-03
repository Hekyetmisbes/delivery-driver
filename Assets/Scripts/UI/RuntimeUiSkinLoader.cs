using UnityEngine;
using System.Collections.Generic;

public static class RuntimeUiSkinLoader
{
    private static readonly Dictionary<string, Sprite> RuntimeSpriteCache = new Dictionary<string, Sprite>();

    public static Sprite LoadSprite(string resourcesPath, string assetPath)
    {
        string cacheKey = $"{resourcesPath}|{assetPath}";
        if (RuntimeSpriteCache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
        {
            RuntimeSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        Texture2D textureFromResources = Resources.Load<Texture2D>(resourcesPath);
        if (textureFromResources != null)
        {
            Sprite generated = CreateSprite(textureFromResources);
            RuntimeSpriteCache[cacheKey] = generated;
            return generated;
        }

#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (editorSprite != null)
        {
            RuntimeSpriteCache[cacheKey] = editorSprite;
            return editorSprite;
        }

        Texture2D editorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (editorTexture != null)
        {
            Sprite generated = CreateSprite(editorTexture);
            RuntimeSpriteCache[cacheKey] = generated;
            return generated;
        }
#else
        Texture2D textureFromAsset = Resources.Load<Texture2D>(assetPath);
        if (textureFromAsset != null)
        {
            Sprite generated = CreateSprite(textureFromAsset);
            RuntimeSpriteCache[cacheKey] = generated;
            return generated;
        }
#endif

        return null;
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
    }
}
