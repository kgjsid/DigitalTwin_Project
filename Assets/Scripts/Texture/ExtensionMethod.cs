using UnityEngine;

public static class ExtensionMethod
{
    /// <summary>
    /// 텍스쳐를 저장하기 위한 DeComprees 과정
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static Texture2D DeCompress(this Texture2D source)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTexture);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D readableTexture = new Texture2D(source.width, source.height);
        readableTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        readableTexture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readableTexture;
    }
}