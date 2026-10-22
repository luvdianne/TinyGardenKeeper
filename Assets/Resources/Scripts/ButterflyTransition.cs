using UnityEngine;
using System.Collections;

public class ButterflyTransition : MonoBehaviour
{
    public float scaleUpDuration = 1.0f;   
    public float stayDuration = 11.0f;
    public float scaleDownDuration = 0.4f; 

    void Start()
    {
        StartCoroutine(ButterflyRoutine());
    }

    IEnumerator ButterflyRoutine()
    {
        Vector3 finalScale = transform.localScale; 
        transform.localScale = Vector3.zero;     

        float time = 0f;
        while (time < scaleUpDuration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, time / scaleUpDuration);
            yield return null;
        }
        transform.localScale = finalScale;

        yield return new WaitForSeconds(stayDuration);

        time = 0f;
        while (time < scaleDownDuration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(finalScale, Vector3.zero, time / scaleDownDuration);
            yield return null;
        }
        transform.localScale = Vector3.zero;

        gameObject.SetActive(false); 
    }
}