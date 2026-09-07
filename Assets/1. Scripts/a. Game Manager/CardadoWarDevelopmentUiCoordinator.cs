using UnityEngine;

/// <summary>
/// Keeps the normal development card-action overlay disabled for the entire
/// WarResolution phase. War owns its own card/die presentation and the normal
/// overlay must not expose or mutate match-wide state while War is active or
/// showing its completion screen.
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

        bool warPhase = gameManager.Phase == CardadoGamePhase.WarResolution;
        if (cardOverlay != null) cardOverlay.enabled = !warPhase;
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
