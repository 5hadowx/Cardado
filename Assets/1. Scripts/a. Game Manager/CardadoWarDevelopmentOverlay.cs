using UnityEngine;

/// <summary>Development-only War presentation. WarManager remains the rules authority.</summary>
public sealed class CardadoWarDevelopmentOverlay : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoWarDevelopmentOverlay>() != null) return;
        var host = new GameObject("CardadoWarDevelopmentOverlay");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoWarDevelopmentOverlay>();
    }

    private void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager != null && warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution) return;
        GUILayout.BeginArea(new Rect(Screen.width - 540, 20, 520, Screen.height - 40), GUI.skin.box);
        GUILayout.Label("WAR");

        if (warManager.Context == null)
        {
            DrawPreWar();
        }
        else if (warManager.IsWarPlaying)
        {
            if (!warManager.IsWarCardActionPending) DrawWarDice();
        }
        else
        {
            DrawWarComplete();
        }

        GUILayout.EndArea();
    }

    private void DrawPreWar()
    {
        int challenger = warManager.CurrentWarClaimantIndex;
        if (challenger < 0) challenger = FindFirstEligibleClaimant();

        if (challenger < 0)
        {
            GUILayout.Label("No eligible War claimant.");
            if (GUILayout.Button("Finish War phase")) Act(() => warManager.TryFinishWarPhase());
            return;
        }

        GUILayout.Label($"Player {challenger + 1}: claim or pass");
        if (warManager.CanClaimWar(challenger) && GUILayout.Button("Declare War")) Act(() => warManager.TryClaimWar(challenger));
        if (GUILayout.Button("Pass")) Act(() => warManager.TryPassWar(challenger));

        GUILayout.Label("Choose target:");
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == challenger || gameManager.Players[p].chips < 1) continue;
            int target = p;
            if (GUILayout.Button($"Target Player {target + 1}")) Act(() => warManager.TryChooseTarget(target));
        }

        if (GUILayout.Button("Wager 1 chip")) Act(() => warManager.TryChooseWarWager(1));
        if (GUILayout.Button("Challenger plays first")) Act(() => warManager.TryChooseWarOrder(true));
        if (GUILayout.Button("Target plays first")) Act(() => warManager.TryChooseWarOrder(false));
    }

    private int FindFirstEligibleClaimant()
    {
        if (gameManager == null || gameManager.Players.Count == 0) return -1;
        int start = gameManager.StartingPlayerIndex;
        if (start < 0) start = 0;
        for (int offset = 0; offset < gameManager.Players.Count; offset++)
        {
            int playerIndex = (start + offset) % gameManager.Players.Count;
            if (warManager.CanClaimWar(playerIndex)) return playerIndex;
        }
        return -1;
    }

    private void DrawWarDice()
    {
        var current = warManager.Context.CurrentPlayer;
        if (current == null) return;
        GUILayout.Label($"Player {current.PlayerIndex + 1}: choose a War die");
        for (int i = 0; i < current.Dice.Count; i++)
        {
            if (!warManager.Context.IsDieAvailable(current, i)) continue;
            int die = i;
            if (GUILayout.Button($"Die {die + 1}: {current.Dice[die]}")) Act(() => gameManager.TryPlayDie(current.PlayerIndex, die));
        }
    }

    private void DrawWarComplete()
    {
        if (warManager.Context != null && warManager.Context.Challenger != null && warManager.CanClaimWar(warManager.Context.Challenger.PlayerIndex) &&
            GUILayout.Button("Declare another War"))
            Act(() => warManager.TryDeclareAnotherWar(warManager.Context.Challenger.PlayerIndex));
        if (GUILayout.Button("Continue to next claimant")) Act(() => warManager.TryContinueWarPhase());
        if (GUILayout.Button("Finish War phase")) Act(() => warManager.TryFinishWarPhase());
    }

    private void Act(System.Func<bool> action)
    {
        action();
        GUIUtility.ExitGUI();
    }
}
