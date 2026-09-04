using UnityEngine;

/// <summary>
/// Temporary development-only deck lifecycle helper.
/// Normal-round cards must remain in player hands through War Resolution because
/// those cards determine War eligibility. CardadoGameManager handles hand cleanup
/// when the next round begins (or when the game ends).
/// </summary>
public class CardadoDevelopmentDeckLifecycle : MonoBehaviour
{
    private CardadoGameManager gameManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CardadoGameManager manager = FindFirstObjectByType<CardadoGameManager>();
        if (manager == null)
            return;

        if (manager.GetComponent<CardadoDevelopmentDeckLifecycle>() == null)
            manager.gameObject.AddComponent<CardadoDevelopmentDeckLifecycle>();
    }

    private void Awake()
    {
        gameManager = GetComponent<CardadoGameManager>();
    }
}
