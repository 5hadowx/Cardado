using UnityEngine;

/// <summary>Development-only War presentation. WarManager remains the rules authority.</summary>
public sealed class CardadoWarDevelopmentOverlay : MonoBehaviour
{
    private enum PreWarStep { Claim, Target, Wager, Order }

    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;
    private PreWarStep preWarStep = PreWarStep.Claim;
    private int claimSearchStart = -1;
    private int activeClaimant = -1;
    private int selectedTarget = -1;
    private int lastWarChallenger = -1;
    private bool warWasStarted;
    private CardadoGamePhase lastPhase;

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

        if (gameManager == null) return;

        if (gameManager.Phase != CardadoGamePhase.WarResolution)
        {
            if (lastPhase == CardadoGamePhase.WarResolution)
                ResetWarPresentation();
        }
        else if (lastPhase != CardadoGamePhase.WarResolution)
        {
            ResetWarPresentation();
            claimSearchStart = gameManager.StartingPlayerIndex;
        }

        lastPhase = gameManager.Phase;
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution) return;

        GUILayout.BeginArea(new Rect(Screen.width - 540, 20, 520, Screen.height - 40), GUI.skin.box);
        GUILayout.Label("WAR");

        if (warWasStarted && warManager.Context == null)
            DrawWarComplete();
        else if (warManager.Context == null)
            DrawPreWar();
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
        switch (preWarStep)
        {
            case PreWarStep.Claim:
                DrawClaimStep();
                break;
            case PreWarStep.Target:
                DrawTargetStep();
                break;
            case PreWarStep.Wager:
                DrawWagerStep();
                break;
            case PreWarStep.Order:
                DrawOrderStep();
                break;
        }
    }

    private void DrawClaimStep()
    {
        int challenger = FindFirstEligibleClaimant(claimSearchStart);
        if (challenger < 0)
        {
            GUILayout.Label("No eligible War claimant.");
            if (GUILayout.Button("Finish War phase")) Act(() => warManager.TryFinishWarPhase());
            return;
        }

        activeClaimant = challenger;
        GUILayout.Label($"Player {challenger + 1}: claim or pass");
        if (warManager.CanClaimWar(challenger) && GUILayout.Button("Declare War"))
        {
            if (warManager.TryClaimWar(challenger))
            {
                lastWarChallenger = challenger;
                preWarStep = PreWarStep.Target;
            }
            Act(() => true);
        }

        if (GUILayout.Button("Pass"))
        {
            if (warManager.TryPassWar(challenger))
            {
                claimSearchStart = (challenger + 1) % gameManager.Players.Count;
                activeClaimant = -1;
            }
            Act(() => true);
        }
    }

    private void DrawTargetStep()
    {
        GUILayout.Label($"Player {lastWarChallenger + 1}: choose target");
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == lastWarChallenger || gameManager.Players[p].chips < 1) continue;
            int target = p;
            if (GUILayout.Button($"Target Player {target + 1}"))
            {
                if (warManager.TryChooseTarget(target))
                {
                    selectedTarget = target;
                    preWarStep = PreWarStep.Wager;
                }
                Act(() => true);
            }
        }
    }

    private void DrawWagerStep()
    {
        GUILayout.Label($"Target: Player {selectedTarget + 1}");
        GUILayout.Label("Choose War wager:");
        if (GUILayout.Button("Wager 1 chip"))
        {
            if (warManager.TryChooseWarWager(1))
                preWarStep = PreWarStep.Order;
            Act(() => true);
        }
    }

    private void DrawOrderStep()
    {
        GUILayout.Label("Choose who plays first:");
        if (GUILayout.Button("Challenger plays first"))
        {
            if (warManager.TryChooseWarOrder(true))
                warWasStarted = true;
            Act(() => true);
        }
        if (GUILayout.Button("Target plays first"))
        {
            if (warManager.TryChooseWarOrder(false))
                warWasStarted = true;
            Act(() => true);
        }
    }

    private int FindFirstEligibleClaimant(int startIndex)
    {
        if (gameManager == null || gameManager.Players.Count == 0) return -1;
        if (startIndex < 0) startIndex = gameManager.StartingPlayerIndex;
        if (startIndex < 0) startIndex = 0;

        for (int offset = 0; offset < gameManager.Players.Count; offset++)
        {
            int playerIndex = (startIndex + offset) % gameManager.Players.Count;
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
        if (lastWarChallenger >= 0 && warManager.CanClaimWar(lastWarChallenger) &&
            GUILayout.Button("Declare another War"))
        {
            if (warManager.TryDeclareAnotherWar(lastWarChallenger))
            {
                warWasStarted = false;
                preWarStep = PreWarStep.Claim;
                activeClaimant = lastWarChallenger;
                claimSearchStart = lastWarChallenger;
            }
            Act(() => true);
        }

        if (GUILayout.Button("Continue to next claimant"))
        {
            if (warManager.TryContinueWarPhase())
            {
                warWasStarted = false;
                preWarStep = PreWarStep.Claim;
                activeClaimant = -1;
                claimSearchStart = lastWarChallenger >= 0
                    ? (lastWarChallenger + 1) % gameManager.Players.Count
                    : gameManager.StartingPlayerIndex;
            }
            Act(() => true);
        }

        if (GUILayout.Button("Finish War phase")) Act(() => warManager.TryFinishWarPhase());
    }

    private void ResetWarPresentation()
    {
        preWarStep = PreWarStep.Claim;
        claimSearchStart = -1;
        activeClaimant = -1;
        selectedTarget = -1;
        lastWarChallenger = -1;
        warWasStarted = false;
    }

    private void Act(System.Func<bool> action)
    {
        action();
        GUIUtility.ExitGUI();
    }
}
