using UnityEngine;

/// <summary>
/// Temporary development harness for exercising the Cardado match flow
/// before the real UI is wired to the game manager.
/// </summary>
public class CardadoDevelopmentTester : MonoBehaviour
{
    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField, Min(0)] private int dealerPlayerIndex = 0;
    [SerializeField] private bool startOnPlay = true;

    private void Start()
    {
        if (startOnPlay)
            StartTestMatch();
    }

    [ContextMenu("Start Test Match")]
    public void StartTestMatch()
    {
        if (gameManager == null)
        {
            Debug.LogError("CardadoDevelopmentTester: Game Manager reference is missing.");
            return;
        }

        gameManager.PhaseChanged += OnPhaseChanged;
        gameManager.SetupDiceRolled += OnSetupDiceRolled;
        gameManager.DealerDecisionRequested += OnDealerDecisionRequested;
        gameManager.RoundSetupCompleted += OnRoundSetupCompleted;
        gameManager.PlayerHandDealt += OnPlayerHandDealt;

        Debug.Log("=== CARDADO DEVELOPMENT TEST ===");
        Debug.Log($"Dealer: Player {dealerPlayerIndex + 1}");

        try
        {
            gameManager.SetDealer(dealerPlayerIndex);
            gameManager.RollRoundSetupDice();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        Debug.Log($"[Cardado] Phase: {phase}");
    }

    private void OnSetupDiceRolled(RoundSetupRoll roll)
    {
        Debug.Log($"[Cardado] Setup dice: {roll.diceCountDie} dice / {roll.cardCountDie} cards");
    }

    private void OnDealerDecisionRequested(RoundSetupDecisionType decision)
    {
        Debug.LogWarning($"[Cardado] DEALER DECISION REQUIRED: {decision}. Test paused here.");
    }

    private void OnRoundSetupCompleted(int diceCount, int cardCount)
    {
        Debug.Log($"[Cardado] Round setup complete: {diceCount} dice, {cardCount} cards per player.");

        try
        {
            gameManager.BeginBetting();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnPlayerHandDealt(CardadoPlayerState player)
    {
        Debug.Log($"[Cardado] {player.playerId} dealt their initial hand.");
    }

    private void OnDestroy()
    {
        if (gameManager == null)
            return;

        gameManager.PhaseChanged -= OnPhaseChanged;
        gameManager.SetupDiceRolled -= OnSetupDiceRolled;
        gameManager.DealerDecisionRequested -= OnDealerDecisionRequested;
        gameManager.RoundSetupCompleted -= OnRoundSetupCompleted;
        gameManager.PlayerHandDealt -= OnPlayerHandDealt;
    }
}
