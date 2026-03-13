using UnityEngine;

/// <summary>
/// Shared fallback sprite utility for UI components that need a plain white sprite
/// when no designer sprite is assigned.
/// </summary>
public static class DeliveryUiSpriteHelper
{
    private static Sprite cachedFallbackSprite;

    public static Sprite GetFallbackSprite()
    {
        if (cachedFallbackSprite != null) return cachedFallbackSprite;

        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        cachedFallbackSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return cachedFallbackSprite;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadCleanup()
    {
        cachedFallbackSprite = null;
    }
#endif
}
