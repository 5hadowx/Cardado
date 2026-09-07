using UnityEngine;

/// <summary>
/// During War hands the dedicated War manager owns card/die presentation and
/// the normal development card-action overlay is disabled so it cannot mutate
/// match-wide state from inside the temporary 1v1 context.
/// </summary>
public class CardadoWarDevelopmentUiCoordinator : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;
    private CardadoCardActionDevelopmentOverlayV2 cardOverlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoWarDevelopmentUiCoordinator>() != null) return;
        GameObject host = new GameObject("Cardado War Development UI Coordinator");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoWarDevelopmentUiCoordinator>();
    }

    private void Awake()
    {
        RefreshReferences();
    }

    private void LateUpdate()
    {
        RefreshReferences();
        if (gameManager == null || warManager == null) return;

        bool warPlaying = gameManager.Phase == CardadoGamePhase.WarResolution && warManager.WarInProgress;
        if (cardOverlay != null) cardOverlay.enabled = !warPlaying;
    }

    private void OnDisable()
    {
        if (cardOverlay != null) cardOverlay.enabled = true;
    }

    private void RefreshReferences()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();
        if (cardOverlay == null) cardOverlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();
    }
}