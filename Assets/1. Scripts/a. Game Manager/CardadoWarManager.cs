using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary/local rules layer for Cardado wars.
/// It owns war-specific state without making the normal round manager responsible
/// for the detailed 1v1 sequence. Card effects inside wars are intentionally left
/// for the card-effect layer; the current tester resolves the three war hands from
/// the rolled dice while still enforcing the real war claim and chip rules.
/// </summary>
public class CardadoWarManager : MonoBehaviour
{
    private enum WarUiStep
    {
        Claim,
        Target,
        Order,
        Playing,
        Complete
    }

    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField] private bool showTemporaryUi = true;

    private readonly List<int> claimOrder = new List<int>();
    private readonly HashSet<int> claimedPlayers = new HashSet<int>();

    private WarUiStep uiStep;
    private int challengerIndex = -1;
    private int targetIndex = -1;
    private bool challengerPlaysFirst;

    private readonly List<int> challengerDice = new List<int>();
    private readonly List<int> targetDice = new List<int>();
    private int challengerHandsWon;
    private int targetHandsWon;
    private int currentWarTurn;
    private int currentHandNumber;
    private int currentHandHighValue;
    private int currentHandWinner = -1;
    private int currentHandTurns;
    private bool warResolved;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;

    public bool WarInProgress => challengerIndex >= 0 && !warResolved;

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

    private void BeginWarPhase()
    {
        if (gameManager == null)
            return;

        claimOrder.Clear();
        claimedPlayers.Clear();
        challengerIndex = -1;
        targetIndex = -1;
        warResolved = false;
        uiStep = WarUiStep.Claim;

        // Until the round manager exposes the complete turn-order history, use the
        // final hand starter as the deterministic inspection order. The exact
        // previous-round order can later be supplied directly by RoundState.
        int start = gameManager.StartingPlayerIndex;
        if (start < 0)
            start = 0;

        for (int offset = 0; offset < gameManager.Players.Count; offset++)
        {
            int index = (start + offset) % gameManager.Players.Count;
            if (CanClaimWar(index))
                claimOrder.Add(index);
        }

        Debug.Log($"[Cardado] War phase started. Eligible war claimants: {claimOrder.Count}.");
        if (claimOrder.Count == 0)
            Debug.Log("[Cardado] No player currently has a valid war claim. War phase is complete for this test.");
    }

    public bool CanClaimWar(int playerIndex)
    {
        if (gameManager == null || playerIndex < 0 || playerIndex >= gameManager.Players.Count)
            return false;

        if (claimedPlayers.Contains(playerIndex))
            return false;

        return HasWarClaim(gameManager.Players[playerIndex].hand.cardsInHand);
    }

    public bool TryClaimWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Claim || !CanClaimWar(playerIndex))
            return false;

        challengerIndex = playerIndex;
        uiStep = WarUiStep.Target;
        Debug.Log($"[Cardado] WAR CLAIMED: {gameManager.Players[playerIndex].playerId}.");
        return true;
    }

    public bool TryChooseTarget(int playerIndex)
    {
        if (uiStep != WarUiStep.Target || challengerIndex < 0)
            return false;

        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count || playerIndex == challengerIndex)
            return false;

        targetIndex = playerIndex;
        uiStep = WarUiStep.Order;
        Debug.Log($"[Cardado] WAR TARGET: {gameManager.Players[targetIndex].playerId}. Target cannot decline.");
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
        challengerHandsWon = 0;
        targetHandsWon = 0;
        currentHandNumber = 1;
        currentHandTurns = 0;
        currentHandHighValue = 0;
        currentHandWinner = -1;
        warResolved = false;

        challengerDice.Clear();
        targetDice.Clear();

        for (int i = 0; i < 3; i++)
        {
            challengerDice.Add(UnityEngine.Random.Range(1, 7));
            targetDice.Add(UnityEngine.Random.Range(1, 7));
        }

        
        currentWarTurn = challengerPlaysFirst ? 0 : 1;
        uiStep = WarUiStep.Playing;

        Debug.Log($"[Cardado] WAR START: {gameManager.Players[challengerIndex].playerId} vs {gameManager.Players[targetIndex].playerId}. " +
                  $"Challenger plays {(challengerPlaysFirst ? "first" : "second")}.");
        Debug.Log($"[Cardado] War dice — {gameManager.Players[challengerIndex].playerId}: {string.Join(", ", challengerDice)}");
        Debug.Log($"[Cardado] War dice — {gameManager.Players[targetIndex].playerId}: {string.Join(", ", targetDice)}");
        Debug.Log("[Cardado] War cards are reserved at 3 vs 3 for this test. Card effects will plug into the war turn later.");
    }

    private bool TryPlayWarDieInternal(bool challenger, int dieIndex)
    {
        if (uiStep != WarUiStep.Playing || warResolved)
            return false;

        bool isChallengerTurn = currentWarTurn == 0;
        if (challenger != isChallengerTurn)
            return false;

        List<int> dice = challenger ? challengerDice : targetDice;
        if (dieIndex < 0 || dieIndex >= dice.Count || dice[dieIndex] <= 0)
            return false;

        int value = dice[dieIndex];
        dice[dieIndex] = 0;
        currentHandTurns++;

        if (currentHandTurns == 1 || value > currentHandHighValue)
        {
            currentHandHighValue = value;
            currentHandWinner = challenger ? 0 : 1;
        }

        Debug.Log($"[Cardado] War hand {currentHandNumber}: {(challenger ? gameManager.Players[challengerIndex].playerId : gameManager.Players[targetIndex].playerId)} played {value}.");

        if (currentHandTurns < 2)
        {
            currentWarTurn = 1 - currentWarTurn;
            return true;
        }

        if (currentHandWinner == 0)
            challengerHandsWon++;
        else
            targetHandsWon++;

        Debug.Log($"[Cardado] War hand {currentHandNumber} winner: {(currentHandWinner == 0 ? gameManager.Players[challengerIndex].playerId : gameManager.Players[targetIndex].playerId)}.");

        if (challengerHandsWon >= 2 || targetHandsWon >= 2)
        {
            ResolveWar(challengerHandsWon >= 2 ? challengerIndex : targetIndex);
            return true;
        }

        currentHandNumber++;
        currentHandTurns = 0;
        currentHandHighValue = 0;
        currentHandWinner = -1;
        currentWarTurn = challengerPlaysFirst ? 0 : 1;
        return true;
    }

    private void ResolveWar(int winnerIndex)
    {
        int loserIndex = winnerIndex == challengerIndex ? targetIndex : challengerIndex;
        int transfer = Math.Min(1, gameManager.Players[loserIndex].chips);

        if (transfer > 0)
        {
            gameManager.Players[loserIndex].chips -= transfer;
            gameManager.Players[winnerIndex].chips += transfer;
        }

        Debug.Log($"[Cardado] WAR RESOLVED: {gameManager.Players[winnerIndex].playerId} wins " +
                  $"{challengerHandsWon}-{targetHandsWon}. " +
                  $"Transferred {transfer} chip(s) from {gameManager.Players[loserIndex].playerId}.");

        claimedPlayers.Add(challengerIndex);
        warResolved = true;
        uiStep = WarUiStep.Complete;
    }

    private bool HasWarClaim(List<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0)
            return false;

        foreach (CardInstance card in cards)
        {
            if (card == null || card.data == null)
                continue;

            if (card.data.cardType == CardType.King ||
                card.data.cardType == CardType.Queen ||
                card.data.cardType == CardType.GordonRobleys)
                return true;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                if (cards[i]?.data == null || cards[j]?.data == null)
                    continue;

                if (IsSameWarSymbol(cards[i].data.cardType, cards[j].data.cardType) &&
                    (cards[i].data.rarity == CardRarity.Special || cards[j].data.rarity == CardRarity.Special))
                    return true;
            }
        }

        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                for (int k = j + 1; k < cards.Count; k++)
                {
                    if (IsValidThreeCardClaim(cards[i], cards[j], cards[k]))
                        return true;
                }
            }
        }

        return false;
    }

    private bool IsValidThreeCardClaim(CardInstance a, CardInstance b, CardInstance c)
    {
        if (a?.data == null || b?.data == null || c?.data == null)
            return false;

        CardType[] types = { a.data.cardType, b.data.cardType, c.data.cardType };
        CardType[] baseTypes = { GetWarSymbol(types[0]), GetWarSymbol(types[1]), GetWarSymbol(types[2]) };

        bool wildcardA = IsBlackWildcard(types[0]);
        bool wildcardB = IsBlackWildcard(types[1]);
        bool wildcardC = IsBlackWildcard(types[2]);
        int wildcards = (wildcardA ? 1 : 0) + (wildcardB ? 1 : 0) + (wildcardC ? 1 : 0);

        if (wildcards > 0)
        {
            List<CardType> concrete = new List<CardType>();
            if (!wildcardA) concrete.Add(baseTypes[0]);
            if (!wildcardB) concrete.Add(baseTypes[1]);
            if (!wildcardC) concrete.Add(baseTypes[2]);

            if (concrete.Count == 0 || AllEqual(concrete))
                return true;

            HashSet<CardType> distinct = new HashSet<CardType>(concrete);
            if (distinct.Count + wildcards >= 3)
                return true;
        }

        if (baseTypes[0] == baseTypes[1] && baseTypes[1] == baseTypes[2])
            return true;

        return baseTypes[0] != baseTypes[1] &&
               baseTypes[0] != baseTypes[2] &&
               baseTypes[1] != baseTypes[2];
    }

    private static bool IsSameWarSymbol(CardType a, CardType b)
    {
        return GetWarSymbol(a) == GetWarSymbol(b) && !IsBlackWildcard(a) && !IsBlackWildcard(b);
    }

    private static CardType GetWarSymbol(CardType type)
    {
        return type;
    }

    private static bool IsBlackWildcard(CardType type)
    {
        return type == CardType.Mirror || type == CardType.Executioner;
    }

    private static bool AllEqual(List<CardType> values)
    {
        if (values.Count < 2)
            return true;

        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] != values[0])
                return false;
        }

        return true;
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

        if (uiStep == WarUiStep.Claim)
            DrawClaimPanel(panel, width);
        else if (uiStep == WarUiStep.Target)
            DrawTargetPanel(panel, width);
        else if (uiStep == WarUiStep.Order)
            DrawOrderPanel(panel, width);
        else if (uiStep == WarUiStep.Playing)
            DrawPlayingPanel(panel, width);
        else
            DrawCompletePanel(panel, width);
    }

    private void DrawClaimPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CLAIM", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35), "Players with a valid 3-card claim may declare a war.", GUI.skin.label);

        float y = panel.y + 120;
        bool any = false;
        foreach (int playerIndex in claimOrder)
        {
            if (!CanClaimWar(playerIndex))
                continue;

            any = true;
            CardadoPlayerState player = gameManager.Players[playerIndex];
            if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), $"{player.playerId} — CLAIM WAR", buttonStyle))
                TryClaimWar(playerIndex);
            y += 65;
        }

        if (!any)
        {
            GUI.Label(new Rect(panel.x + 25, y, width - 50, 40), "No eligible wars remain.", GUI.skin.label);
            GUI.Label(new Rect(panel.x + 25, y + 50, width - 50, 40), "War phase complete for this test.", GUI.skin.label);
        }
    }

    private void DrawTargetPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE OPPONENT", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{gameManager.Players[challengerIndex].playerId} challenges any opponent. The opponent cannot decline.", GUI.skin.label);

        float y = panel.y + 125;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == challengerIndex)
                continue;

            if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), gameManager.Players[i].playerId, buttonStyle))
                TryChooseTarget(i);
            y += 65;
        }
    }

    private void DrawOrderPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE ORDER", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            "The challenger has seen their 3 dice and chooses who plays first.", GUI.skin.label);

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 130, width - 50, 65), "CHALLENGER PLAYS FIRST", buttonStyle))
            TryChooseWarOrder(true);

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 215, width - 50, 65), "CHALLENGER PLAYS SECOND", buttonStyle))
            TryChooseWarOrder(false);
    }

    private void DrawPlayingPanel(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        CardadoPlayerState target = gameManager.Players[targetIndex];
        string currentPlayer = currentWarTurn == 0 ? challenger.playerId : target.playerId;

        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — 3 HANDS", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 30),
            $"{challenger.playerId} {challengerHandsWon} — {targetHandsWon} {target.playerId}", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 30),
            $"Hand {currentHandNumber}: {currentPlayer} plays a die.", GUI.skin.label);

        DrawWarDice(panel, challengerDice, true, challenger.playerId);
        DrawWarDice(panel, targetDice, false, target.playerId);

        GUI.Label(new Rect(panel.x + 25, panel.y + 395, width - 50, 30),
            "Card effects are not implemented in wars yet; this test resolves the dice hands.", GUI.skin.label);
    }

    private void DrawWarDice(Rect panel, List<int> dice, bool challenger, string playerName)
    {
        float y = challenger ? panel.y + 150 : panel.y + 270;
        GUI.Label(new Rect(panel.x + 25, y, 200, 30), playerName, GUI.skin.label);

        float x = panel.x + 230;
        for (int i = 0; i < dice.Count; i++)
        {
            if (dice[i] <= 0)
                continue;

            if (GUI.Button(new Rect(x + i * 115, y - 5, 95, 60), dice[i].ToString(), buttonStyle))
                TryPlayWarDie(challenger, i);
        }
    }

    private void TryPlayWarDie(bool challenger, int dieIndex)
    {
        if (!TryPlayWarDieInternal(challenger, dieIndex))
            Debug.LogWarning("[Cardado] War die choice rejected.");
    }

    private void DrawCompletePanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 30, width - 50, 45), "WAR RESOLVED", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 95, width - 50, 35),
            "The chip transfer is complete. War/match continuation will be wired next.", GUI.skin.label);

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 155, width - 50, 60), "CONTINUE TEST", selectedButtonStyle))
            AdvanceToNextClaim();
    }

    private void AdvanceToNextClaim()
    {
        challengerIndex = -1;
        targetIndex = -1;
        warResolved = false;
        uiStep = WarUiStep.Claim;

        Debug.Log("[Cardado] Returning to war claim selection for the next possible war.");
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(20, 20, 20, 20)
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        selectedButtonStyle = new GUIStyle(buttonStyle)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }
}
