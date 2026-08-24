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

    private bool showDealerChoice;
    private bool showBettingChoice;
    private int bettingPlayerIndex = -1;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

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
        gameManager.BettingTurnStarted += OnBettingTurnStarted;
        gameManager.BettingCompleted += OnBettingCompleted;

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
        showDealerChoice = true;
        Debug.LogWarning($"[Cardado] DEALER DECISION REQUIRED: {decision}. Use the temporary choice panel.");
    }

    private void OnRoundSetupCompleted(int diceCount, int cardCount)
    {
        showDealerChoice = false;
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

    private void OnBettingTurnStarted(CardadoPlayerState player, int playerIndex)
    {
        bettingPlayerIndex = playerIndex;
        showBettingChoice = true;
        Debug.Log($"[Cardado] BET REQUIRED: {player.playerId}. Maximum bid: {gameManager.GetMaximumBidForPlayer(playerIndex)}");
    }

    private void OnBettingCompleted()
    {
        bettingPlayerIndex = -1;
        showBettingChoice = false;
        Debug.Log("[Cardado] All players have placed their bids.");

        try
        {
            gameManager.BeginPlayingHands();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnGUI()
    {
        if (showDealerChoice && gameManager != null && gameManager.PendingDealerDecision.HasValue)
        {
            DrawDealerChoicePanel();
            return;
        }

        if (showBettingChoice && gameManager != null && bettingPlayerIndex >= 0)
            DrawBettingPanel();
    }

    private void DrawDealerChoicePanel()
    {
        EnsureStyles();

        const float width = 520f;
        const float height = 230f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, GUIContent.none, panelStyle);

        string choiceLabel = gameManager.PendingDealerDecision.Value == RoundSetupDecisionType.ChooseDiceCount
            ? "DICE"
            : "CARDS";

        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"DEALER — CHOOSE {choiceLabel}", titleStyle);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 65f, width - 50f, 35f),
            "Select a value from 1 to 5.", GUI.skin.label);

        float buttonWidth = 78f;
        float spacing = 10f;
        float totalWidth = buttonWidth * 5f + spacing * 4f;
        float startX = panel.x + (width - totalWidth) * 0.5f;

        for (int value = 1; value <= 5; value++)
        {
            Rect buttonRect = new Rect(
                startX + (value - 1) * (buttonWidth + spacing),
                panel.y + 120f,
                buttonWidth,
                60f);

            if (GUI.Button(buttonRect, value.ToString(), buttonStyle))
                ResolveDealerChoice(value);
        }
    }

    private void DrawBettingPanel()
    {
        EnsureStyles();

        int maximumBid = gameManager.GetMaximumBidForPlayer(bettingPlayerIndex);
        CardadoPlayerState player = gameManager.Players[bettingPlayerIndex];

        const float width = 620f;
        const float height = 260f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, GUIContent.none, panelStyle);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"{player.playerId} — PLACE BET", titleStyle);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 68f, width - 50f, 35f),
            $"Choose 0 to {maximumBid} chips. You have {player.chips} chips.", GUI.skin.label);

        float buttonWidth = 78f;
        float spacing = 10f;
        int buttonCount = maximumBid + 1;
        float totalWidth = buttonWidth * buttonCount + spacing * (buttonCount - 1);
        float startX = panel.x + (width - totalWidth) * 0.5f;

        for (int value = 0; value <= maximumBid; value++)
        {
            Rect buttonRect = new Rect(
                startX + value * (buttonWidth + spacing),
                panel.y + 125f,
                buttonWidth,
                60f);

            if (GUI.Button(buttonRect, value.ToString(), buttonStyle))
                ResolveBet(value);
        }
    }

    private void ResolveDealerChoice(int value)
    {
        try
        {
            gameManager.ResolveDealerChoice(value);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void ResolveBet(int value)
    {
        try
        {
            if (!gameManager.TryPlaceBid(bettingPlayerIndex, value))
                Debug.LogWarning($"[Cardado] Bid rejected for Player {bettingPlayerIndex + 1}: {value}");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.padding = new RectOffset(20, 20, 20, 20);

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
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
        gameManager.BettingTurnStarted -= OnBettingTurnStarted;
        gameManager.BettingCompleted -= OnBettingCompleted;
    }
}
