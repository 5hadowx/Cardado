using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// War phase controller. War declaration consumes only the optimal claim cards;
/// all other cards remain with their owner and survive the War. Each player may
/// declare multiple wars in sequence while they remain eligible and have chips.
/// Once a War starts, its three cards per player use the normal card-effect flow.
/// </summary>
public class CardadoWarManager : MonoBehaviour
{
    private enum WarUiStep { Claim, Target, Wager, Order, Playing, Complete }

    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField] private bool showTemporaryUi = true;
    [SerializeField, Min(1)] private int warCardCount = 3;
    [SerializeField, Min(1)] private int warDiceCount = 3;

    private readonly List<int> claimOrder = new List<int>();
    private WarUiStep uiStep;
    private int currentClaimPosition;
    private int challengerIndex = -1;
    private int targetIndex = -1;
    private int warWager;
    private bool challengerPlaysFirst;

    private int challengerHandsWon;
    private int targetHandsWon;
    private int currentWarTurn;
    private int currentHandNumber;
    private int currentHandTurns;
    private int challengerCurrentDieIndex = -1;
    private int targetCurrentDieIndex = -1;
    private bool warResolved;
    private bool warCardActionPending;
    private bool warCardPlayedThisTurn;
    private readonly List<CardInstance> turnStartingCards = new List<CardInstance>();

    // Cards held before a War starts. The claim cards are consumed, but every
    // other pre-War card must be restored after the War.
    private readonly List<CardInstance> preservedChallengerCards = new List<CardInstance>();
    private readonly List<CardInstance> preservedTargetCards = new List<CardInstance>();

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;

    public bool WarInProgress => challengerIndex >= 0 && !warResolved;
    public bool IsWarCardActionPending => warCardActionPending;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager != null)
            gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void Start()
    {
        if (gameManager != null && gameManager.Phase == CardadoGamePhase.WarResolution)
            BeginWarPhase();
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase == CardadoGamePhase.WarResolution)
            BeginWarPhase();
    }

    private void LateUpdate()
    {
        if (!WarInProgress || !warCardActionPending)
            return;

        if (HasWarCardBeenPlayedThisTurn())
        {
            warCardPlayedThisTurn = true;
            warCardActionPending = false;
            ForceDevelopmentOverlayToDieSelection(GetCurrentWarPlayerIndex());
        }
    }

    private void BeginWarPhase()
    {
        if (gameManager == null)
            return;

        claimOrder.Clear();
        challengerIndex = -1;
        targetIndex = -1;
        warWager = 0;
        warResolved = false;
        currentClaimPosition = 0;
        warCardActionPending = false;
        warCardPlayedThisTurn = false;
        turnStartingCards.Clear();
        preservedChallengerCards.Clear();
        preservedTargetCards.Clear();
        uiStep = WarUiStep.Claim;

        int start = gameManager.StartingPlayerIndex;
        if (start < 0)
            start = 0;

        for (int offset = 0; offset < gameManager.Players.Count; offset++)
            claimOrder.Add((start + offset) % gameManager.Players.Count);

        Debug.Log($"[Cardado] War phase started. Claim order: {BuildClaimOrderLabel()}.");
        AdvanceToCurrentClaimant();
    }

    private string BuildClaimOrderLabel()
    {
        List<string> names = new List<string>();
        foreach (int index in claimOrder)
        {
            if (index >= 0 && index < gameManager.Players.Count)
                names.Add(gameManager.Players[index].playerId);
        }
        return string.Join(" -> ", names);
    }

    private void AdvanceToCurrentClaimant()
    {
        challengerIndex = -1;
        targetIndex = -1;
        warWager = 0;
        warResolved = false;
        warCardActionPending = false;
        warCardPlayedThisTurn = false;
        turnStartingCards.Clear();
        uiStep = WarUiStep.Claim;

        while (currentClaimPosition < claimOrder.Count)
        {
            int playerIndex = claimOrder[currentClaimPosition];
            if (CanClaimWar(playerIndex))
            {
                Debug.Log($"[Cardado] WAR CLAIM TURN: {gameManager.Players[playerIndex].playerId}. " +
                          $"Optimal claim: {DescribeOptimalClaim(playerIndex)}.");
                return;
            }

            Debug.Log($"[Cardado] WAR PASS: {gameManager.Players[playerIndex].playerId} has no valid war claim or cannot afford another war.");
            currentClaimPosition++;
        }

        uiStep = WarUiStep.Complete;
        Debug.Log("[Cardado] War claim sequence complete. No further player may declare a war.");
    }

    public bool CanClaimWar(int playerIndex)
    {
        if (gameManager == null || playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return false;
        if (gameManager.Players[playerIndex].chips <= 0)
            return false;
        return CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand) != null;
    }

    private string DescribeOptimalClaim(int playerIndex)
    {
        List<CardInstance> claim = CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand);
        if (claim == null)
            return "none";

        List<string> names = new List<string>();
        foreach (CardInstance card in claim)
            names.Add(GetCardLabel(card));
        return string.Join(" + ", names);
    }

    private string GetCardLabel(CardInstance card)
    {
        if (card == null || card.data == null)
            return "?";
        return card.data.id + " [" + card.data.cardType + ", " + card.data.rarity + "]";
    }

    public bool TryClaimWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Claim || currentClaimPosition >= claimOrder.Count)
            return false;
        if (playerIndex != claimOrder[currentClaimPosition] || !CanClaimWar(playerIndex))
            return false;

        List<CardInstance> optimalClaim = CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand);
        if (optimalClaim == null || optimalClaim.Count == 0)
            return false;

        // Consume only the cards used for this declaration. Every other card stays in hand.
        foreach (CardInstance card in optimalClaim)
        {
            if (!gameManager.Players[playerIndex].hand.cardsInHand.Remove(card))
                return false;
            card.isPlayed = true;
            gameManager.DiscardResolvedCard(card);
        }

        challengerIndex = playerIndex;
        uiStep = WarUiStep.Target;
        Debug.Log($"[Cardado] WAR CLAIMED: {gameManager.Players[playerIndex].playerId} using {DescribeClaim(optimalClaim)}. " +
                  $"Remaining cards: {gameManager.Players[playerIndex].hand.cardsInHand.Count}.");
        return true;
    }

    private string DescribeClaim(List<CardInstance> claim)
    {
        List<string> labels = new List<string>();
        foreach (CardInstance card in claim)
            labels.Add(GetCardLabel(card));
        return string.Join(" + ", labels);
    }

    public bool TryPassWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Claim || currentClaimPosition >= claimOrder.Count)
            return false;
        if (playerIndex != claimOrder[currentClaimPosition])
            return false;

        currentClaimPosition++;
        Debug.Log($"[Cardado] WAR PASS: {gameManager.Players[playerIndex].playerId} chose not to declare a war.");
        AdvanceToCurrentClaimant();
        return true;
    }

    public bool TryChooseTarget(int playerIndex)
    {
        if (uiStep != WarUiStep.Target || challengerIndex < 0)
            return false;
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count || playerIndex == challengerIndex)
            return false;
        targetIndex = playerIndex;
        uiStep = WarUiStep.Wager;
        return true;
    }

    public bool TryChooseWarWager(int wager)
    {
        if (uiStep != WarUiStep.Wager || challengerIndex < 0 || targetIndex < 0)
            return false;
        if (wager < 1 || wager > 2)
            return false;
        if (gameManager.Players[challengerIndex].chips < wager || gameManager.Players[targetIndex].chips < wager)
            return false;
        warWager = wager;
        uiStep = WarUiStep.Order;
        return true;
    }

    public bool TryChooseWarOrder(bool challengerFirst)
    {
        if (uiStep != WarUiStep.Order || challengerIndex < 0 || targetIndex < 0)
            return false;
        challengerPlaysFirst = challengerFirst;
        StartWar();
        return true;
    }

    private void StartWar()
    {
        if (gameManager.RoundDeck == null)
            throw new InvalidOperationException("War cannot start because the round deck is not initialized.");

        challengerHandsWon = 0;
        targetHandsWon = 0;
        currentHandNumber = 1;
        currentHandTurns = 0;
        challengerCurrentDieIndex = -1;
        targetCurrentDieIndex = -1;
        currentWarTurn = challengerPlaysFirst ? 0 : 1;
        warResolved = false;
        warCardActionPending = false;
        warCardPlayedThisTurn = false;

        PreserveAndClearWarHands(challengerIndex, preservedChallengerCards);
        PreserveAndClearWarHands(targetIndex, preservedTargetCards);

        DealWarCards(challengerIndex, warCardCount);
        DealWarCards(targetIndex, warCardCount);
        RollWarDice(challengerIndex, warDiceCount);
        RollWarDice(targetIndex, warDiceCount);

        uiStep = WarUiStep.Playing;
        Debug.Log($"[Cardado] WAR START: {gameManager.Players[challengerIndex].playerId} vs {gameManager.Players[targetIndex].playerId}. " +
                  $"Challenger plays {(challengerPlaysFirst ? "first" : "second")}.");
        Debug.Log("[Cardado] WAR CARDS: 3 vs 3 from the existing RoundDeck.");
        BeginWarHand();
    }

    private void PreserveAndClearWarHands(int playerIndex, List<CardInstance> preserved)
    {
        preserved.Clear();
        CardadoPlayerState player = gameManager.Players[playerIndex];
        preserved.AddRange(player.hand.cardsInHand);
        player.hand.cardsInHand.Clear();
        player.dice.Clear();
        player.playedDice.Clear();
    }

    private void RestorePreservedWarCards()
    {
        RestorePlayerCards(challengerIndex, preservedChallengerCards);
        RestorePlayerCards(targetIndex, preservedTargetCards);
        preservedChallengerCards.Clear();
        preservedTargetCards.Clear();
    }

    private void RestorePlayerCards(int playerIndex, List<CardInstance> cards)
    {
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        foreach (CardInstance card in cards)
        {
            if (card == null) continue;
            card.isPlayed = false;
            player.hand.AddCard(card);
        }
    }

    private void DealWarCards(int playerIndex, int count)
    {
        CardadoPlayerState player = gameManager.Players[playerIndex];
        for (int i = 0; i < count; i++)
        {
            CardInstance card = gameManager.RoundDeck.Draw();
            if (card == null)
                throw new InvalidOperationException("No cards are available to complete the War deal.");
            card.isPlayed = false;
            player.hand.AddCard(card);
        }
    }

    private void RollWarDice(int playerIndex, int count)
    {
        CardadoPlayerState player = gameManager.Players[playerIndex];
        for (int i = 0; i < count; i++)
        {
            player.dice.Add(UnityEngine.Random.Range(1, 7));
            player.playedDice.Add(false);
        }
    }

    private void BeginWarHand()
    {
        if (warResolved)
            return;

        currentHandTurns = 0;
        challengerCurrentDieIndex = -1;
        targetCurrentDieIndex = -1;
        warCardActionPending = false;
        warCardPlayedThisTurn = false;
        turnStartingCards.Clear();
        uiStep = WarUiStep.Playing;

        CardadoPlayerState current = GetCurrentWarPlayer();
        if (current == null)
            return;

        gameManager.NotifyWarHandTurnStarted(current, currentHandNumber, GetCurrentWarPlayerIndex());
        turnStartingCards.AddRange(current.hand.cardsInHand);
        if (current.hand.cardsInHand.Count > 0)
        {
            warCardActionPending = true;
            gameManager.RequestWarCardAction(current, CardadoCardActionRequestType.ChooseCard);
        }
    }

    public bool TrySkipCardAction(int playerIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || playerIndex != GetCurrentWarPlayerIndex())
            return false;
        warCardPlayedThisTurn = HasWarCardBeenPlayedThisTurn();
        warCardActionPending = false;
        if (warCardPlayedThisTurn)
            ForceDevelopmentOverlayToDieSelection(playerIndex);
        return true;
    }

    private bool HasWarCardBeenPlayedThisTurn()
    {
        for (int i = 0; i < turnStartingCards.Count; i++)
            if (turnStartingCards[i] != null && turnStartingCards[i].isPlayed)
                return true;
        return false;
    }

    public bool TryPlayWarDieForPlayer(int playerIndex, int dieIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || warCardActionPending)
            return false;
        if (playerIndex != GetCurrentWarPlayerIndex() || !IsWarDieAvailable(playerIndex, dieIndex))
            return false;

        CardadoPlayerState player = gameManager.Players[playerIndex];
        int value = player.dice[dieIndex];
        player.playedDice[dieIndex] = true;
        if (playerIndex == challengerIndex) challengerCurrentDieIndex = dieIndex;
        else targetCurrentDieIndex = dieIndex;
        currentHandTurns++;
        warCardActionPending = false;
        gameManager.NotifyWarDiePlayed(player, dieIndex, value);
        Debug.Log($"[Cardado] War hand {currentHandNumber}: {player.playerId} played {value}.");

        if (currentHandTurns < 2)
        {
            currentWarTurn = 1 - currentWarTurn;
            BeginWarTurnAfterFirstDie();
            return true;
        }

        ResolveCurrentWarHand();
        return true;
    }

    public bool IsWarDieAvailable(int playerIndex, int dieIndex)
    {
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return false;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        return dieIndex >= 0 && dieIndex < player.dice.Count &&
               dieIndex < player.playedDice.Count && !player.playedDice[dieIndex] && player.dice[dieIndex] > 0;
    }

    public bool IsWarDieTargetable(int playerIndex, int dieIndex)
    {
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return false;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        return dieIndex >= 0 && dieIndex < player.dice.Count && player.dice[dieIndex] > 0;
    }

    private void BeginWarTurnAfterFirstDie()
    {
        CardadoPlayerState current = GetCurrentWarPlayer();
        if (current == null)
            return;
        warCardActionPending = false;
        warCardPlayedThisTurn = false;
        turnStartingCards.Clear();
        gameManager.NotifyWarHandTurnStarted(current, currentHandNumber, GetCurrentWarPlayerIndex());
        turnStartingCards.AddRange(current.hand.cardsInHand);
        if (current.hand.cardsInHand.Count > 0)
        {
            warCardActionPending = true;
            gameManager.RequestWarCardAction(current, CardadoCardActionRequestType.ChooseCard);
        }
    }

    private void ResolveCurrentWarHand()
    {
        int challengerValue = GetCurrentHandDieValue(challengerIndex, challengerCurrentDieIndex);
        int targetValue = GetCurrentHandDieValue(targetIndex, targetCurrentDieIndex);
        if (challengerValue > targetValue) challengerHandsWon++;
        else if (targetValue > challengerValue) targetHandsWon++;
        else Debug.Log($"[Cardado] War hand {currentHandNumber} tied at {challengerValue}.");

        CleanupWarHandEffects();
        Debug.Log($"[Cardado] WAR SCORE: {gameManager.Players[challengerIndex].playerId} {challengerHandsWon} — " +
                  $"{targetHandsWon} {gameManager.Players[targetIndex].playerId}.");

        if (challengerHandsWon >= 2 || targetHandsWon >= 2 || currentHandNumber >= warDiceCount)
        {
            int winner = challengerHandsWon > targetHandsWon ? challengerIndex : targetIndex;
            if (challengerHandsWon == targetHandsWon)
                winner = challengerIndex;
            ResolveWar(winner);
            return;
        }

        currentHandNumber++;
        currentWarTurn = challengerPlaysFirst ? 0 : 1;
        BeginWarHand();
    }

    private int GetCurrentHandDieValue(int playerIndex, int dieIndex)
    {
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return 0;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        return dieIndex >= 0 && dieIndex < player.dice.Count ? player.dice[dieIndex] : 0;
    }

    private void ResolveWar(int winnerIndex)
    {
        int loserIndex = winnerIndex == challengerIndex ? targetIndex : challengerIndex;
        int transfer = Math.Min(warWager, gameManager.Players[loserIndex].chips);
        if (transfer > 0)
        {
            gameManager.Players[loserIndex].chips -= transfer;
            gameManager.Players[winnerIndex].chips += transfer;
        }

        ResetDevelopmentOverlayForWarEnd();
        DiscardWarHands();
        RestorePreservedWarCards();
        warResolved = true;
        warCardActionPending = false;
        uiStep = WarUiStep.Complete;

        Debug.Log($"[Cardado] WAR RESOLVED: {gameManager.Players[winnerIndex].playerId} wins " +
                  $"{challengerHandsWon}-{targetHandsWon}. Transferred {transfer} chip(s). Wager was {warWager}.");
    }

    private void CleanupWarHandEffects()
    {
        Component overlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) return;
        Type type = overlay.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo effectsField = type.GetField("effects", flags);
        MethodInfo removeMethod = type.GetMethod("RemoveEffectObj", flags);
        if (effectsField == null || removeMethod == null) return;
        var effects = effectsField.GetValue(overlay) as System.Collections.IList;
        if (effects == null) return;

        List<object> remove = new List<object>();
        foreach (object effect in effects)
        {
            if (effect == null) continue;
            Type effectType = effect.GetType();
            FieldInfo typeField = effectType.GetField("type", flags);
            FieldInfo keyField = effectType.GetField("key", flags);
            if (typeField == null || keyField == null) continue;
            string effectTypeName = typeField.GetValue(effect)?.ToString();
            string key = keyField.GetValue(effect) as string;
            bool removeEffect = effectTypeName == "SpecialBodyguardHand";
            if (!removeEffect && !string.IsNullOrEmpty(key))
            {
                string[] parts = key.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int p) && int.TryParse(parts[1], out int d))
                {
                    bool wasPlayedThisHand = (p == challengerIndex && d == challengerCurrentDieIndex) ||
                                              (p == targetIndex && d == targetCurrentDieIndex);
                    removeEffect = wasPlayedThisHand && (effectTypeName == "Modifier" || effectTypeName == "BodyguardDie");
                }
            }
            if (removeEffect) remove.Add(effect);
        }
        foreach (object effect in remove)
            removeMethod.Invoke(overlay, new[] { effect, true });
    }

    private void ResetDevelopmentOverlayForWarEnd()
    {
        Component overlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) return;
        MethodInfo method = overlay.GetType().GetMethod("RoundEnded", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(overlay, null);
    }

    private void DiscardWarHands()
    {
        DiscardPlayerHand(challengerIndex);
        DiscardPlayerHand(targetIndex);
    }

    private void DiscardPlayerHand(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count) return;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        List<CardInstance> cards = new List<CardInstance>(player.hand.cardsInHand);
        player.hand.cardsInHand.Clear();
        foreach (CardInstance card in cards)
            if (card != null) gameManager.DiscardResolvedCard(card);
    }

    private CardadoPlayerState GetCurrentWarPlayer()
    {
        int index = GetCurrentWarPlayerIndex();
        return index >= 0 && index < gameManager.Players.Count ? gameManager.Players[index] : null;
    }

    private int GetCurrentWarPlayerIndex() => currentWarTurn == 0 ? challengerIndex : targetIndex;

    private void ForceDevelopmentOverlayToDieSelection(int playerIndex)
    {
        Component overlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) return;
        Type type = overlay.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo stepField = type.GetField("step", flags);
        FieldInfo visibleField = type.GetField("visible", flags);
        FieldInfo piField = type.GetField("pi", flags);
        FieldInfo tiField = type.GetField("ti", flags);
        FieldInfo siField = type.GetField("si", flags);
        FieldInfo diField = type.GetField("di", flags);
        FieldInfo activeField = type.GetField("active", flags);
        if (stepField == null || visibleField == null || piField == null) return;
        object dieAfterSkip = Enum.Parse(stepField.FieldType, "DieAfterSkip");
        stepField.SetValue(overlay, dieAfterSkip);
        visibleField.SetValue(overlay, true);
        piField.SetValue(overlay, playerIndex);
        tiField?.SetValue(overlay, -1);
        siField?.SetValue(overlay, -1);
        diField?.SetValue(overlay, -1);
        activeField?.SetValue(overlay, null);
    }

    private void OnGUI()
    {
        if (!showTemporaryUi || gameManager == null || gameManager.Phase != CardadoGamePhase.WarResolution)
            return;
        EnsureStyles();
        const float width = 760f;
        const float height = 500f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);
        switch (uiStep)
        {
            case WarUiStep.Claim: DrawClaimPanel(panel, width); break;
            case WarUiStep.Target: DrawTargetPanel(panel, width); break;
            case WarUiStep.Wager: DrawWagerPanel(panel, width); break;
            case WarUiStep.Order: DrawOrderPanel(panel, width); break;
            case WarUiStep.Playing: DrawPlayingPanel(panel, width); break;
            default: DrawCompletePanel(panel, width); break;
        }
    }

    private void DrawClaimPanel(Rect panel, float width)
    {
        if (currentClaimPosition >= claimOrder.Count)
        {
            DrawCompletePanel(panel, width);
            return;
        }
        CardadoPlayerState player = gameManager.Players[claimOrder[currentClaimPosition]];
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — DECLARE OR PASS", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{player.playerId} — your turn to declare a war.", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 30),
            $"Chips: {player.chips}    Optimal claim: {DescribeOptimalClaim(claimOrder[currentClaimPosition])}", GUI.skin.label);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 155, width - 50, 60), "DECLARE WAR", buttonStyle))
            TryClaimWar(claimOrder[currentClaimPosition]);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 230, width - 50, 60), "PASS", buttonStyle))
            TryPassWar(claimOrder[currentClaimPosition]);
        GUI.Label(new Rect(panel.x + 25, panel.y + 320, width - 50, 30), $"War order: {BuildClaimOrderLabel()}", GUI.skin.label);
    }

    private void DrawTargetPanel(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE OPPONENT", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{challenger.playerId} challenges any opponent. The opponent cannot decline.", GUI.skin.label);
        float y = panel.y + 125;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == challengerIndex) continue;
            CardadoPlayerState target = gameManager.Players[i];
            bool canBeTarget = target.chips >= 1;
            GUI.enabled = canBeTarget;
            if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), $"{target.playerId} — {target.chips} chip(s)", buttonStyle))
                TryChooseTarget(i);
            GUI.enabled = true;
            y += 65;
        }
    }

    private void DrawWagerPanel(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        CardadoPlayerState target = gameManager.Players[targetIndex];
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE WAGER", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{challenger.playerId}: {challenger.chips} chips    vs    {target.playerId}: {target.chips} chips", GUI.skin.label);
        for (int wager = 1; wager <= 2; wager++)
        {
            bool canWager = challenger.chips >= wager && target.chips >= wager;
            GUI.enabled = canWager;
            if (GUI.Button(new Rect(panel.x + 25, panel.y + 145 + (wager - 1) * 80, width - 50, 60),
                $"{wager} CHIP{(wager == 1 ? "" : "S")}", buttonStyle))
                TryChooseWarWager(wager);
            GUI.enabled = true;
        }
    }

    private void DrawOrderPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE ORDER", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 45),
            "The challenger sees their 3 dice and chooses who plays first.", GUI.skin.label);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 135, width - 50, 65), "CHALLENGER PLAYS FIRST", buttonStyle))
            TryChooseWarOrder(true);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 220, width - 50, 65), "CHALLENGER PLAYS SECOND", buttonStyle))
            TryChooseWarOrder(false);
    }

    private void DrawPlayingPanel(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        CardadoPlayerState target = gameManager.Players[targetIndex];
        string currentPlayer = GetCurrentWarPlayer()?.playerId ?? "?";
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — 3 HANDS", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 30),
            $"{challenger.playerId} {challengerHandsWon} — {targetHandsWon} {target.playerId}", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 30),
            $"Hand {currentHandNumber}: {currentPlayer} {(warCardActionPending ? "chooses a card." : "plays a die.")}", GUI.skin.label);
        DrawWarDice(panel, challengerIndex, challenger.playerId, panel.y + 150);
        DrawWarDice(panel, targetIndex, target.playerId, panel.y + 270);
    }

    private void DrawWarDice(Rect panel, int playerIndex, string playerName, float y)
    {
        GUI.Label(new Rect(panel.x + 25, y, 200, 30), playerName, GUI.skin.label);
        float x = panel.x + 230;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        for (int i = 0; i < player.dice.Count; i++)
        {
            if (!IsWarDieAvailable(playerIndex, i)) continue;
            GUI.enabled = !warCardActionPending && playerIndex == GetCurrentWarPlayerIndex();
            if (GUI.Button(new Rect(x + i * 115, y - 5, 95, 60), player.dice[i].ToString(), buttonStyle))
                gameManager.TryPlayDie(playerIndex, i);
            GUI.enabled = true;
        }
    }

    private void DrawCompletePanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 30, width - 50, 45), "WAR PHASE COMPLETE", titleStyle);
        if (warResolved && challengerIndex >= 0)
        {
            CardadoPlayerState player = gameManager.Players[challengerIndex];
            bool canAgain = CanClaimWar(challengerIndex);
            GUI.Label(new Rect(panel.x + 25, panel.y + 95, width - 50, 35),
                canAgain
                    ? $"War resolved. {player.playerId} still has a valid War and {player.chips} chip(s)."
                    : $"War resolved. {player.playerId} cannot declare another War.", GUI.skin.label);

            if (canAgain)
            {
                if (GUI.Button(new Rect(panel.x + 25, panel.y + 155, width - 50, 60), "DECLARE ANOTHER WAR", selectedButtonStyle))
                {
                    warResolved = false;
                    challengerIndex = -1;
                    targetIndex = -1;
                    warWager = 0;
                    AdvanceToCurrentClaimant();
                }

                if (GUI.Button(new Rect(panel.x + 25, panel.y + 230, width - 50, 60), "CONTINUE TO NEXT PLAYER", buttonStyle))
                {
                    currentClaimPosition++;
                    AdvanceToCurrentClaimant();
                }
                return;
            }

            if (GUI.Button(new Rect(panel.x + 25, panel.y + 155, width - 50, 60), "CONTINUE TO NEXT PLAYER", selectedButtonStyle))
            {
                currentClaimPosition++;
                AdvanceToCurrentClaimant();
            }
            return;
        }

        GUI.Label(new Rect(panel.x + 25, panel.y + 95, width - 50, 35),
            "All players have had their opportunity to declare a war.", GUI.skin.label);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 195, width - 50, 60), "FINISH WAR PHASE", selectedButtonStyle))
            gameManager.CompleteWarPhase();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        selectedButtonStyle = new GUIStyle(buttonStyle);
    }
}
