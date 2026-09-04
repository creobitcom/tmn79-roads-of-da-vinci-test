using UnityEngine;

public static class WideAspectEdgeFillCache
{
    private const int CapturePass = 1;

    public static void Render(ref RenderTexture cache, Texture source, Material material)
    {
        int divisor = Application.isMobilePlatform ? 2 : 1;
        int width = Mathf.Max(1, Screen.width / divisor);
        int height = Mathf.Max(1, Screen.height / divisor);

        if (cache != null && (cache.width != width || cache.height != height))
        {
            Release(ref cache);
        }

        if (cache == null)
        {
            cache = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "WideAspectEdgeFillCache",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, cache, material, CapturePass);
        RenderTexture.active = previous;
    }

    public static void Release(ref RenderTexture cache)
    {
        if (cache == null)
        {
            return;
        }

        cache.Release();

        if (Application.isPlaying)
        {
            Object.Destroy(cache);
        }
        else
        {
            Object.DestroyImmediate(cache);
        }

        cache = null;
    }
}
