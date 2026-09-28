using UnityEngine;
using UnityEngine.UI;

public class PlantAnimation : MonoBehaviour
{
    public Image plantImage;
    public Sprite[] frames;
    public float frameRate = 0.1f;

    private int currentFrame = 0;
    private float timer = 0f;

    void Update()
    {
        if (frames.Length == 0)
            return;

        timer += Time.deltaTime;

        if (timer >= frameRate)
        {
            timer = 0f;

            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                currentFrame = 0;
            }

            plantImage.sprite = frames[currentFrame];
        }
    }
}