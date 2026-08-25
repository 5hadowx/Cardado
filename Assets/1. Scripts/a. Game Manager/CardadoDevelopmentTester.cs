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
    private bool showDieChoice;
    private int bettingPlayerIndex = -1;
    private int handPlayerIndex = -1;
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
        gameManager.HandTurnStarted += OnHandTurnStarted;
        gameManager.DiePlayed += OnDiePlayed;
        gameManager.HandCompleted += OnHandCompleted;
        gameManager.RoundPlayingCompleted += OnRoundPlayingCompleted;

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
            Debug.Log($"[Cardado] {player.playerId}: bet {player.roundBet} chip(s), predicts {player.diceBid} dice.");

        try
        {
            gameManager.BeginPlayingHands();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int handNumber, int handStarterIndex)
    {
        handPlayerIndex = GetPlayerIndex(player);
        showDieChoice = true;
        Debug.Log($"[Cardado] HAND {handNumber}: {player.playerId} chooses a die. Hand starter: Player {handStarterIndex + 1}.");
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int dieValue)
    {
        Debug.Log($"[Cardado] {player.playerId} played die #{dieIndex + 1}: {dieValue}.");
    }

    private void OnHandCompleted(int winnerPlayerIndex, int winningValue)
    {
        showDieChoice = false;
        handPlayerIndex = -1;
        CardadoPlayerState winner = gameManager.Players[winnerPlayerIndex];
        Debug.Log($"[Cardado] HAND {gameManager.CurrentHandNumber} winner: {winner.playerId} with {winningValue}. Hands won: {winner.handsWon}.");
    }

    private void OnRoundPlayingCompleted()
    {
        showDieChoice = false;
        handPlayerIndex = -1;
        Debug.Log("[Cardado] All dice have been played. Round is ready for resolution.");

        foreach (CardadoPlayerState player in gameManager.Players)
            Debug.Log($"[Cardado] {player.playerId}: {player.handsWon} hand(s) won, prediction {player.diceBid}.");
    }

    private int GetPlayerIndex(CardadoPlayerState player)
    {
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (gameManager.Players[i] == player)
                return i;
        }

        return -1;
    }

    private void OnGUI()
    {
        if (showDealerChoice && gameManager != null && gameManager.PendingDealerDecision.HasValue)
        {
            DrawDealerChoicePanel();
            return;
        }

        if (showBettingChoice && gameManager != null && bettingPlayerIndex >= 0)
        {
            DrawBettingPanel();
            return;
        }

        if (showDieChoice && gameManager != null && handPlayerIndex >= 0)
            DrawDieChoicePanel();
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
            Rect buttonRect = new Rect(startX + (value - 1) * (buttonWidth + spacing), panel.y + 120f, buttonWidth, 60f);
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

        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, 455f, 45f),
            $"{player.playerId} — ROUND CALL", titleStyle);

        DrawRolledDiceSummary(player, panel);

        GUI.Label(new Rect(panel.x + 25f, panel.y + 120f, width - 50f, 30f),
            $"Choose chips to bet (minimum 1, maximum {maximumChipBet}).", GUI.skin.label);

        float buttonWidth = 70f;
        float spacing = 10f;
        float chipStartX = panel.x + 25f;
        float chipY = panel.y + 155f;

        for (int value = 1; value <= maximumChipBet; value++)
        {
            Rect buttonRect = new Rect(chipStartX + (value - 1) * (buttonWidth + spacing), chipY, buttonWidth, 55f);
            GUIStyle style = selectedChipBet == value ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(buttonRect, value.ToString(), style))
                selectedChipBet = value;
        }

        GUI.Label(new Rect(panel.x + 25f, panel.y + 230f, width - 50f, 30f),
            $"Predict dice won (0 to {gameManager.RoundDiceCount}).", GUI.skin.label);

        float diceStartX = panel.x + 25f;
        float diceY = panel.y + 267f;
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
        Rect confirmRect = new Rect(panel.x + 25f, panel.y + 345f, width - 50f, 50f);

        if (canConfirm && GUI.Button(confirmRect, "CONFIRM ROUND CALL", selectedButtonStyle))
            ResolveRoundCall();
        else if (!canConfirm)
            GUI.Label(confirmRect, "Select both the chip bet and dice prediction.", GUI.skin.label);
    }

    private void DrawRolledDiceSummary(CardadoPlayerState player, Rect panel)
    {
        GUI.Label(new Rect(panel.x + 500f, panel.y + 25f, 230f, 35f), "DICE", titleStyle);

        if (player.dice == null || player.dice.Count == 0)
        {
            GUI.Label(new Rect(panel.x + 500f, panel.y + 65f, 230f, 35f), "—", GUI.skin.label);
            return;
        }

        string diceValues = string.Join("   ", player.dice);
        GUI.Label(new Rect(panel.x + 500f, panel.y + 65f, 230f, 55f), diceValues, buttonStyle);
    }

    private void DrawDieChoicePanel()
    {
        EnsureStyles();

        CardadoPlayerState player = gameManager.Players[handPlayerIndex];
        int availableDice = gameManager.GetAvailableDieCount(handPlayerIndex);
        const float width = 760f;
        const float height = 300f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"{player.playerId} — CHOOSE A DIE", titleStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 68f, width - 50f, 30f),
            "Card effects are not implemented yet. Choose a die directly for this test.", GUI.skin.label);

        float buttonWidth = 90f;
        float spacing = 12f;
        float totalWidth = gameManager.RoundDiceCount * buttonWidth + (gameManager.RoundDiceCount - 1) * spacing;
        float startX = panel.x + (width - totalWidth) * 0.5f;

        for (int dieIndex = 0; dieIndex < player.dice.Count; dieIndex++)
        {
            if (!gameManager.IsDieAvailable(handPlayerIndex, dieIndex))
                continue;

            Rect buttonRect = new Rect(startX + dieIndex * (buttonWidth + spacing), panel.y + 125f, buttonWidth, 70f);
            if (GUI.Button(buttonRect, $"Die {dieIndex + 1}\n{player.dice[dieIndex]}", buttonStyle))
                ResolveDieChoice(dieIndex);
        }

        GUI.Label(new Rect(panel.x + 25f, panel.y + 215f, width - 50f, 30f),
            $"Available dice: {availableDice} / {gameManager.RoundDiceCount}", GUI.skin.label);
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

    private void ResolveDieChoice(int dieIndex)
    {
        try
        {
            if (!gameManager.TryPlayDie(handPlayerIndex, dieIndex))
                Debug.LogWarning($"[Cardado] Die choice rejected for Player {handPlayerIndex + 1}: die {dieIndex + 1}.");
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
        gameManager.HandTurnStarted -= OnHandTurnStarted;
        gameManager.DiePlayed -= OnDiePlayed;
        gameManager.HandCompleted -= OnHandCompleted;
        gameManager.RoundPlayingCompleted -= OnRoundPlayingCompleted;
    }
}
