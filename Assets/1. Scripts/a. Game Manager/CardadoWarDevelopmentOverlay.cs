using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Temporary development UI for the War pre-play decisions.
/// It sits in front of CardadoWarManager's existing test UI and adds the
/// challenger wager while exposing current chip counts when selecting a target.
/// The underlying War manager remains responsible for the actual 3-hand War.
/// </summary>
public class CardadoWarDevelopmentOverlay : MonoBehaviour
{
    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField] private CardadoWarManager warManager;
    [SerializeField] private bool showTemporaryUi = true;

    private enum OverlayStep
    {
        None,
        Claim,
        Target,
        Wager,
        Order,
        Playing,
        Complete
    }

    private OverlayStep step;
    private int challengerIndex = -1;
    private int targetIndex = -1;
    private int selectedWager;
    private bool wagerApplied;
    private int wagerWinner = -1;
    private int wagerLoser = -1;
    private int wagerAmount;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

    private FieldInfo warUiVisibilityField;
    private FieldInfo challengerHandsWonField;
    private FieldInfo targetHandsWonField;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<CardadoGameManager>();

        if (warManager == null)
            warManager = FindFirstObjectByType<CardadoWarManager>();

        if (warManager != null)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            warUiVisibilityField = typeof(CardadoWarManager).GetField("showTemporaryUi", flags);
            challengerHandsWonField = typeof(CardadoWarManager).GetField("challengerHandsWon", flags);
            targetHandsWonField = typeof(CardadoWarManager).GetField("targetHandsWon", flags);
        }
    }

    private void OnEnable()
    {
        if (gameManager != null)
            gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase == CardadoGamePhase.WarResolution)
            BeginOverlay();
        else if (phase != CardadoGamePhase.WarResolution)
            SetWarManagerUi(true);
    }

    private void Start()
    {
        if (gameManager != null && gameManager.Phase == CardadoGamePhase.WarResolution)
            BeginOverlay();
    }

    private void Update()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution)
            return;

        if (step == OverlayStep.Playing && !warManager.WarInProgress)
        {
            ResolveWagerWinner();
            ApplyAdditionalWagerTransferIfNeeded();
            step = OverlayStep.Complete;
            SetWarManagerUi(true);
        }
    }

    private void BeginOverlay()
    {
        challengerIndex = -1;
        targetIndex = -1;
        selectedWager = 0;
        wagerApplied = false;
        wagerWinner = -1;
        wagerLoser = -1;
        wagerAmount = 0;
        step = OverlayStep.Claim;
        SetWarManagerUi(false);
    }

    private void SetWarManagerUi(bool visible)
    {
        if (warUiVisibilityField == null || warManager == null)
            return;

        warUiVisibilityField.SetValue(warManager, visible);
    }

    private void ChooseClaim(int playerIndex)
    {
        if (!warManager.TryClaimWar(playerIndex))
            return;

        challengerIndex = playerIndex;
        step = OverlayStep.Target;
    }

    private void ChooseTarget(int playerIndex)
    {
        if (!warManager.TryChooseTarget(playerIndex))
            return;

        targetIndex = playerIndex;
        selectedWager = 0;
        step = OverlayStep.Wager;
    }

    private void ChooseWager(int amount)
    {
        if (challengerIndex < 0 || challengerIndex >= gameManager.Players.Count)
            return;

        if (amount < 1 || amount > 2)
            return;

        selectedWager = amount;
        step = OverlayStep.Order;
        Debug.Log($"[Cardado] WAR WAGER: {gameManager.Players[challengerIndex].playerId} bets {amount} chip(s) against {gameManager.Players[targetIndex].playerId}. Target cannot decline.");
    }

    private void ChooseOrder(bool challengerFirst)
    {
        if (selectedWager < 1)
            return;

        wagerAmount = selectedWager;
        wagerApplied = false;
        wagerWinner = -1;
        wagerLoser = -1;
        SetWarManagerUi(true);

        if (!warManager.TryChooseWarOrder(challengerFirst))
        {
            SetWarManagerUi(false);
            return;
        }

        step = OverlayStep.Playing;
    }

    private void ResolveWagerWinner()
    {
        if (challengerHandsWonField == null || targetHandsWonField == null)
            return;

        int challengerHands = (int)challengerHandsWonField.GetValue(warManager);
        int targetHands = (int)targetHandsWonField.GetValue(warManager);

        if (challengerHands == targetHands)
            return;

        if (challengerHands > targetHands)
        {
            wagerWinner = challengerIndex;
            wagerLoser = targetIndex;
        }
        else
        {
            wagerWinner = targetIndex;
            wagerLoser = challengerIndex;
        }
    }

    private void ApplyAdditionalWagerTransferIfNeeded()
    {
        if (wagerApplied || wagerWinner < 0 || wagerLoser < 0)
            return;

        // CardadoWarManager already transfers one chip when the War resolves.
        // This overlay adds the second chip when the challenger selected a 2-chip wager.
        int remaining = Mathf.Max(0, wagerAmount - 1);
        int transfer = Mathf.Min(remaining, gameManager.Players[wagerLoser].chips);

        if (transfer > 0)
        {
            gameManager.Players[wagerLoser].chips -= transfer;
            gameManager.Players[wagerWinner].chips += transfer;
        }

        Debug.Log($"[Cardado] WAR WAGER SETTLED: wager {wagerAmount}, additional transfer {transfer}. " +
                  $"Winner: {gameManager.Players[wagerWinner].playerId} ({gameManager.Players[wagerWinner].chips} chips), " +
                  $"Loser: {gameManager.Players[wagerLoser].playerId} ({gameManager.Players[wagerLoser].chips} chips).");

        wagerApplied = true;
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box);
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18
        };
    }

    private void OnGUI()
    {
        if (!showTemporaryUi || gameManager == null || gameManager.Phase != CardadoGamePhase.WarResolution)
            return;

        if (step == OverlayStep.Playing)
            return;

        EnsureStyles();

        const float width = 760f;
        const float height = 500f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);

        switch (step)
        {
            case OverlayStep.Claim:
                DrawClaim(panel, width);
                break;
            case OverlayStep.Target:
                DrawTarget(panel, width);
                break;
            case OverlayStep.Wager:
                DrawWager(panel, width);
                break;
            case OverlayStep.Order:
                DrawOrder(panel, width);
                break;
            case OverlayStep.Complete:
                DrawComplete(panel, width);
                break;
        }
    }

    private void DrawClaim(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 40), "WAR — CLAIM", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35), "Eligible players can declare a War.", GUI.skin.label);

        float y = panel.y + 120;
        bool any = false;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (!warManager.CanClaimWar(i))
                continue;

            any = true;
            CardadoPlayerState player = gameManager.Players[i];
            string label = $"{player.playerId} — {player.chips} chips — CLAIM WAR";
            if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), label, buttonStyle))
                ChooseClaim(i);
            y += 65;
        }

        if (!any)
        {
            GUI.Label(new Rect(panel.x + 25, y, width - 50, 40), "No eligible wars remain.", GUI.skin.label);
            GUI.Label(new Rect(panel.x + 25, y + 45, width - 50, 40), "War phase complete for this test.", GUI.skin.label);
        }
    }

    private void DrawTarget(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 40), "WAR — CHOOSE OPPONENT", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{gameManager.Players[challengerIndex].playerId} chooses the target. The target cannot decline.", GUI.skin.label);

        float y = panel.y + 120;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == challengerIndex)
                continue;

            CardadoPlayerState player = gameManager.Players[i];
            string label = $"{player.playerId} — {player.chips} chips — SELECT TARGET";
            if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), label, buttonStyle))
                ChooseTarget(i);
            y += 65;
        }
    }

    private void DrawWager(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        CardadoPlayerState target = gameManager.Players[targetIndex];

        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 40), "WAR — CHOOSE WAGER", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"Challenger: {challenger.playerId} — {challenger.chips} chips", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 35),
            $"Target: {target.playerId} — {target.chips} chips", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 150, width - 50, 35),
            "Choose the War wager. The target cannot decline.", GUI.skin.label);

        float y = panel.y + 205;
        if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), "BET 1 CHIP", buttonStyle))
            ChooseWager(1);
        y += 65;

        if (GUI.Button(new Rect(panel.x + 25, y, width - 50, 55), "BET 2 CHIPS", buttonStyle))
            ChooseWager(2);
    }

    private void DrawOrder(Rect panel, float width)
    {
        CardadoPlayerState challenger = gameManager.Players[challengerIndex];
        CardadoPlayerState target = gameManager.Players[targetIndex];

        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 40), "WAR — CHOOSE ORDER", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 35),
            $"{challenger.playerId} wagered {selectedWager} chip(s).", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 35),
            $"Target: {target.playerId} — {target.chips} chips. Target cannot decline.", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 150, width - 50, 35),
            "Choose who plays first in the War.", GUI.skin.label);

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 205, width - 50, 55),
                $"{challenger.playerId} PLAYS FIRST", buttonStyle))
            ChooseOrder(true);

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 270, width - 50, 55),
                $"{target.playerId} PLAYS FIRST", buttonStyle))
            ChooseOrder(false);
    }

    private void DrawComplete(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 40), "WAR — COMPLETE", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 80, width - 50, 40),
            "War resolved. Further War chaining remains available through the War manager.", GUI.skin.label);
    }
}
