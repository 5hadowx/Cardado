using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    public HandUI handUI;
    public CardDisplay bigCardDisplay; // prefab with artwork+name+desc
    public CanvasGroup bigCardCanvasGroup; // attach a CanvasGroup to big card

    [Header("Animation")]
    public float fadeDuration = 0.2f;

    private void Start()
    {
        // Ensure hidden at start
        bigCardCanvasGroup.alpha = 0f;
        bigCardDisplay.gameObject.SetActive(false);

        // Example usage: later you'll pass actual cards
        // handUI.ShowHand(testCards, OnCardHoverEnter, OnCardHoverExit);
    }

    private void OnCardHoverEnter(CardInstance card)
    {
        bigCardDisplay.ShowCard(card.data);
        bigCardDisplay.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(bigCardCanvasGroup, bigCardCanvasGroup.alpha, 1f));
    }

    private void OnCardHoverExit()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutAndHide());
    }

    private System.Collections.IEnumerator FadeOutAndHide()
    {
        yield return FadeCanvasGroup(bigCardCanvasGroup, bigCardCanvasGroup.alpha, 0f);
        bigCardDisplay.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }
}

