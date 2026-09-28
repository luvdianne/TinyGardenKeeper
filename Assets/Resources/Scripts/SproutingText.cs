using UnityEngine;
using TMPro;

public class SproutingText : MonoBehaviour
{
    private TMP_Text sproutingText;

    private float timer = 0f;
    private int dotCount = 0;

    void Start()
    {
        sproutingText = GetComponent<TMP_Text>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 0.4f)
        {
            timer = 0f;

            dotCount++;

            if (dotCount > 3)
            {
                dotCount = 0;
            }

            sproutingText.text = "Sprouting" + new string('.', dotCount);
        }
    }
}