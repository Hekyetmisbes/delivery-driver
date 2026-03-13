using UnityEngine;

/// <summary>
/// Shared helper for creating minimap marker materials.
/// Works around shader stripping in builds by falling back to the primitive's own shader
/// when Shader.Find() returns null (unreferenced shaders get stripped).
/// </summary>
public static class MinimapShaderHelper
{
    private static Shader cachedUnlitShader;

    /// <summary>
    /// Create a flat-color material suitable for minimap markers.
    /// Pass the primitive's MeshRenderer as fallback so its built-in shader can be reused
    /// when URP/Unlit shaders are stripped from the build.
    /// </summary>
    public static Material CreateColorMaterial(Color color, MeshRenderer fallbackRenderer = null)
    {
        Shader shader = ResolveShader(fallbackRenderer);
        if (shader == null) return null;
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    private static Shader ResolveShader(MeshRenderer fallbackRenderer)
    {
        if (cachedUnlitShader != null) return cachedUnlitShader;

        cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (cachedUnlitShader != null) return cachedUnlitShader;

        cachedUnlitShader = Shader.Find("Unlit/Color");
        if (cachedUnlitShader != null) return cachedUnlitShader;

        // Fallback: use the primitive's own shader (guaranteed to exist in builds)
        if (fallbackRenderer != null && fallbackRenderer.sharedMaterial != null)
        {
            cachedUnlitShader = fallbackRenderer.sharedMaterial.shader;
        }

        return cachedUnlitShader;
    }
}
