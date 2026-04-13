using UnityEngine;

/// <summary>
/// Shared helper for creating flat-color materials at runtime.
/// Works around shader stripping in builds by falling back to the primitive's own shader
/// when Shader.Find() returns null.
/// </summary>
public static class RuntimeColorMaterialHelper
{
    private static Shader cachedUnlitShader;

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

        if (fallbackRenderer != null && fallbackRenderer.sharedMaterial != null)
        {
            cachedUnlitShader = fallbackRenderer.sharedMaterial.shader;
        }

        return cachedUnlitShader;
    }
}
