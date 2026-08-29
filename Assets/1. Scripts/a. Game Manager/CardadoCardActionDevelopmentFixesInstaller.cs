using UnityEngine;

public static class CardadoCardActionDevelopmentFixesInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        CardadoGameManager gm = Object.FindFirstObjectByType<CardadoGameManager>();
        if (gm == null) return;
        if (gm.GetComponent<CardadoCardActionDevelopmentFixes>() == null)
            gm.gameObject.AddComponent<CardadoCardActionDevelopmentFixes>();
    }
}
