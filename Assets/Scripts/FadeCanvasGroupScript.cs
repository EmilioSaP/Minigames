using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class FadeCanvasGroupScript : MonoBehaviour
{
    [Tooltip("Duration of fade in seconds.")]
    public float fadeTime = 0.5f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void FadeIn()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(canvasGroup, 0f, 1f));
    }
    public void FadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(canvasGroup, 1f, 0f, deactivateAfter: true));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, bool deactivateAfter = false)
    {
        float elapsed = 0f;

        cg.alpha = start;
        cg.blocksRaycasts = true; // Allow interaction during fade if needed

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / fadeTime);
            yield return null;
        }

        cg.alpha = end;

        // Disable interaction if fully transparent
        cg.blocksRaycasts = end > 0f;

        if (deactivateAfter)
        {
            gameObject.SetActive(false);
        }
    }
}
