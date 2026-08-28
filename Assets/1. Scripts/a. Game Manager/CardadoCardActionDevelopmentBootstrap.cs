using UnityEngine;

public static class CardadoCardActionDevelopmentBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        CardadoGameManager manager = Object.FindFirstObjectByType<CardadoGameManager>();
        if (manager == null) return;
        CardadoCardActionDevelopmentOverlay legacy = manager.GetComponent<CardadoCardActionDevelopmentOverlay>();
        if (legacy != null) Object.Destroy(legacy);
        if (manager.GetComponent<CardadoCardActionDevelopmentOverlayV2>() == null)
            manager.gameObject.AddComponent<CardadoCardActionDevelopmentOverlayV2>();
    }
}
