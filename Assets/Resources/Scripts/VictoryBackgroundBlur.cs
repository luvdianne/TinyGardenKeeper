using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captures the game screen once, blurs a downsampled copy, and displays it in a UI RawImage.
/// Put the RawImage as the FIRST child of the VictoryPanel (behind artwork and buttons).
/// The VictoryPanel's CanvasGroup must be invisible during capture.
/// </summary>
public class VictoryBackgroundBlur : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage blurredBackground;

    [Header("Blur Quality")]
    [Range(2, 12)] [SerializeField] private int downsampleFactor = 6;
    [Range(1, 10)] [SerializeField] private int blurRadius = 5;

    private Texture2D blurredTexture;

    public IEnumerator CaptureAndBlur()
    {
        if (blurredBackground == null)
        {
            Debug.LogWarning("VictoryBackgroundBlur: Assign the Blurred Background RawImage in the Inspector.");
            yield break;
        }

        // Unity requires the screen to finish rendering before taking a screenshot.
        // Keep VictoryPanel CanvasGroup.alpha = 0 until this coroutine completes.
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot == null)
        {
            Debug.LogWarning("VictoryBackgroundBlur: Screen capture failed.");
            yield break;
        }

        int width = Mathf.Max(1, screenshot.width / downsampleFactor);
        int height = Mathf.Max(1, screenshot.height / downsampleFactor);

        // Downsampling before blurring keeps the operation manageable on mobile.
        RenderTexture downsampled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        downsampled.filterMode = FilterMode.Bilinear;
        Graphics.Blit(screenshot, downsampled);

        RenderTexture oldActive = RenderTexture.active;
        RenderTexture.active = downsampled;
        Texture2D small = new Texture2D(width, height, TextureFormat.RGB24, false);
        small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        small.Apply();
        RenderTexture.active = oldActive;

        RenderTexture.ReleaseTemporary(downsampled);
        Destroy(screenshot);

        // Separable box blur (horizontal pass, then vertical pass).
        Color32[] source = small.GetPixels32();
        Color32[] horizontal = new Color32[source.Length];
        Color32[] result = new Color32[source.Length];
        int sampleCount = 2 * blurRadius + 1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int red = 0, green = 0, blue = 0;
                for (int d = -blurRadius; d <= blurRadius; d++)
                {
                    Color32 pixel = source[y * width + Mathf.Clamp(x + d, 0, width - 1)];
                    red += pixel.r;
                    green += pixel.g;
                    blue += pixel.b;
                }
                horizontal[y * width + x] = new Color32(
                    (byte)(red / sampleCount),
                    (byte)(green / sampleCount),
                    (byte)(blue / sampleCount),
                    255);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int red = 0, green = 0, blue = 0;
                for (int d = -blurRadius; d <= blurRadius; d++)
                {
                    Color32 pixel = horizontal[Mathf.Clamp(y + d, 0, height - 1) * width + x];
                    red += pixel.r;
                    green += pixel.g;
                    blue += pixel.b;
                }
                result[y * width + x] = new Color32(
                    (byte)(red / sampleCount),
                    (byte)(green / sampleCount),
                    (byte)(blue / sampleCount),
                    255);
            }
        }

        small.SetPixels32(result);
        small.Apply(false, false);
        small.filterMode = FilterMode.Bilinear;

        if (blurredTexture != null)
            Destroy(blurredTexture);

        blurredTexture = small;
        blurredBackground.texture = blurredTexture;
    }

    private void OnDestroy()
    {
        if (blurredTexture != null)
            Destroy(blurredTexture);
    }
}
