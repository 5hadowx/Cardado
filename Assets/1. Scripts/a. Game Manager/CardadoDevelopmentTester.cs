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
    private int selectedChipBet = 1;
    private int selectedDiceBid = -1;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;

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
        gameManager.PlayerDiceRolled += OnPlayerDiceRolled;
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

    private void OnPlayerDiceRolled(CardadoPlayerState player)
    {
        Debug.Log($"[Cardado] {player.playerId} rolled: {string.Join(", ", player.dice)}");
    }

    private void OnBettingTurnStarted(CardadoPlayerState player, int playerIndex)
    {
        bettingPlayerIndex = playerIndex;
        selectedChipBet = 1;
        selectedDiceBid = -1;
        showBettingChoice = true;

        Debug.Log($"[Cardado] ROUND CALL REQUIRED: {player.playerId}. Choose chip bet and predicted dice wins.");
    }

    private void OnBettingCompleted()
    {
        bettingPlayerIndex = -1;
        showBettingChoice = false;
        Debug.Log("[Cardado] All players have placed their round calls.");

        foreach (CardadoPlayerState player in gameManager.Players)
        {
            Debug.Log($"[Cardado] {player.playerId}: bet {player.roundBet} chip(s), predicts {player.diceBid} dice win(s).");
        }

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

        int maximumChipBet = gameManager.GetMaximumRoundBetForPlayer(bettingPlayerIndex);
        CardadoPlayerState player = gameManager.Players[bettingPlayerIndex];

        const float width = 760f;
        const float height = 390f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, GUIContent.none, panelStyle);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"{player.playerId} — ROUND CALL", titleStyle);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 68f, width - 50f, 30f),
            $"Choose chips to bet (minimum 1, maximum {maximumChipBet}).", GUI.skin.label);

        float buttonWidth = 70f;
        float spacing = 10f;
        float chipStartX = panel.x + 25f;
        float chipY = panel.y + 105f;

        for (int value = 1; value <= maximumChipBet; value++)
        {
            Rect buttonRect = new Rect(chipStartX + (value - 1) * (buttonWidth + spacing), chipY, buttonWidth, 55f);
            GUIStyle style = selectedChipBet == value ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(buttonRect, value.ToString(), style))
                selectedChipBet = value;
        }

        GUI.Label(new Rect(panel.x + 25f, panel.y + 180f, width - 50f, 30f),
            $"Predict dice won (0 to {gameManager.RoundDiceCount}).", GUI.skin.label);

        float diceStartX = panel.x + 25f;
        float diceY = panel.y + 217f;
        int minimumDiceBid = gameManager.GetMinimumDicePredictionForPlayer(bettingPlayerIndex);

        for (int value = 0; value <= gameManager.RoundDiceCount; value++)
        {
            if (value < minimumDiceBid || !gameManager.IsValidDicePrediction(bettingPlayerIndex, value))
                continue;

            Rect buttonRect = new Rect(diceStartX + value * (buttonWidth + spacing), diceY, buttonWidth, 55f);
            GUIStyle style = selectedDiceBid == value ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(buttonRect, value.ToString(), style))
                selectedDiceBid = value;
        }

        bool canConfirm = selectedDiceBid >= 0;
        Rect confirmRect = new Rect(panel.x + 25f, panel.y + 315f, width - 50f, 50f);

        if (canConfirm && GUI.Button(confirmRect, "CONFIRM ROUND CALL", selectedButtonStyle))
            ResolveRoundCall();
        else if (!canConfirm)
            GUI.Label(confirmRect, "Select both the chip bet and dice prediction.", GUI.skin.label);
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

    private void ResolveRoundCall()
    {
        try
        {
            if (!gameManager.TryPlaceRoundCall(bettingPlayerIndex, selectedChipBet, selectedDiceBid))
                Debug.LogWarning($"[Cardado] Round call rejected for Player {bettingPlayerIndex + 1}: {selectedChipBet} chip(s), {selectedDiceBid} dice.");
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

        selectedButtonStyle = new GUIStyle(buttonStyle)
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
        gameManager.PlayerDiceRolled -= OnPlayerDiceRolled;
        gameManager.BettingTurnStarted -= OnBettingTurnStarted;
        gameManager.BettingCompleted -= OnBettingCompleted;
    }
}
