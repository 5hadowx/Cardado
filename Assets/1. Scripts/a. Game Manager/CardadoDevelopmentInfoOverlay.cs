using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only presentation overlay for the live player state.
/// It reads authoritative state from CardadoGameManager and never mutates gameplay state.
/// </summary>
public sealed class CardadoDevelopmentInfoOverlay : MonoBehaviour
{
    private const int MaxDisplayedPlayers = 4;

    private CardadoGameManager gameManager;
    private readonly List<int> lastPlayedDieIndex = new List<int>();
    private readonly List<int> lastPlayedDieValue = new List<int>();
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle playerStyle;
    private GUIStyle activePlayerStyle;
    private GUIStyle dividerStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CardadoGameManager manager = FindFirstObjectByType<CardadoGameManager>();
        if (manager == null) return;
        if (manager.GetComponent<CardadoDevelopmentInfoOverlay>() != null) return;
        manager.gameObject.AddComponent<CardadoDevelopmentInfoOverlay>();
    }

    private void OnEnable()
    {
        gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null)
            gameManager = FindFirstObjectByType<CardadoGameManager>();

        if (gameManager == null) return;

        gameManager.RoundSetupCompleted += OnRoundSetupCompleted;
        gameManager.DiePlayed += OnDiePlayed;
        ResetPlayedDice();
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.RoundSetupCompleted -= OnRoundSetupCompleted;
        gameManager.DiePlayed -= OnDiePlayed;
    }

    private void OnRoundSetupCompleted(int diceCount, int cardCount)
    {
        ResetPlayedDice();
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int dieValue)
    {
        if (gameManager == null || player == null) return;
        int playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0) return;
        EnsurePlayedDiceStorage();
        lastPlayedDieIndex[playerIndex] = dieIndex;
        lastPlayedDieValue[playerIndex] = dieValue;
    }

    private void ResetPlayedDice()
    {
        lastPlayedDieIndex.Clear();
        lastPlayedDieValue.Clear();
        int count = gameManager == null ? MaxDisplayedPlayers : gameManager.Players.Count;
        for (int i = 0; i < count; i++)
        {
            lastPlayedDieIndex.Add(-1);
            lastPlayedDieValue.Add(0);
        }
    }

    private void EnsurePlayedDiceStorage()
    {
        if (gameManager == null) return;
        while (lastPlayedDieIndex.Count < gameManager.Players.Count)
        {
            lastPlayedDieIndex.Add(-1);
            lastPlayedDieValue.Add(0);
        }
    }

    private int GetPlayerIndex(CardadoPlayerState player)
    {
        if (gameManager == null) return -1;
        for (int i = 0; i < gameManager.Players.Count; i++)
            if (gameManager.Players[i] == player) return i;
        return -1;
    }

    private void OnGUI()
    {
        if (gameManager == null || gameManager.Players == null || gameManager.Players.Count == 0) return;
        EnsureStyles();
        EnsurePlayedDiceStorage();
        DrawScorePanel();
        DrawPlayerInfoPanel();
    }

    private void DrawScorePanel()
    {
        float width = Mathf.Clamp(Screen.width * 0.39f, 520f, 760f);
        float height = 145f;
        float x = Screen.width * 0.337f;
        float y = 20f;
        Rect panel = new Rect(x, y, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 8f, panel.width - 36f, 30f), "PLAYER SCORE", headerStyle);

        int count = Mathf.Min(MaxDisplayedPlayers, gameManager.Players.Count);
        float rowTop = panel.y + 48f;
        float rowHeight = 23f;
        float nameWidth = panel.width * 0.30f;
        float predictionWidth = panel.width * 0.33f;
        float wonWidth = panel.width * 0.30f;

        GUI.Label(new Rect(panel.x + 18f, rowTop, nameWidth, rowHeight), "PLAYER", labelStyle);
        GUI.Label(new Rect(panel.x + nameWidth, rowTop, predictionWidth, rowHeight), "PREDICTED", labelStyle);
        GUI.Label(new Rect(panel.x + nameWidth + predictionWidth, rowTop, wonWidth, rowHeight), "WON", labelStyle);

        for (int i = 0; i < count; i++)
        {
            CardadoPlayerState player = gameManager.Players[i];
            float rowY = rowTop + (i + 1) * rowHeight;
            GUIStyle nameStyle = gameManager.CurrentHandPlayerIndex == i ? activePlayerStyle : playerStyle;
            string prediction = player.hasPlacedBid ? player.diceBid.ToString() : "—";
            GUI.Label(new Rect(panel.x + 18f, rowY, nameWidth, rowHeight), player.playerId, nameStyle);
            GUI.Label(new Rect(panel.x + nameWidth, rowY, predictionWidth, rowHeight), prediction, valueStyle);
            GUI.Label(new Rect(panel.x + nameWidth + predictionWidth, rowY, wonWidth, rowHeight), player.handsWon.ToString(), valueStyle);
        }
    }

    private void DrawPlayerInfoPanel()
    {
        float width = Mathf.Clamp(Screen.width * 0.20f, 330f, 400f);
        float x = Screen.width * 0.76f;
        float y = 34f;
        float height = Mathf.Min(Screen.height - y - 24f, 690f);
        Rect panel = new Rect(x, y, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 32f), "PLAYER DETAILS", headerStyle);

        int count = Mathf.Min(MaxDisplayedPlayers, gameManager.Players.Count);
        float contentY = panel.y + 48f;
        float playerBlockHeight = (panel.height - 58f) / Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            CardadoPlayerState player = gameManager.Players[i];
            DrawPlayerDetails(player, i, new Rect(panel.x + 14f, contentY, panel.width - 28f, playerBlockHeight));
            contentY += playerBlockHeight;
        }
    }

    private void DrawPlayerDetails(CardadoPlayerState player, int playerIndex, Rect rect)
    {
        GUIStyle nameStyle = gameManager.CurrentHandPlayerIndex == playerIndex ? activePlayerStyle : playerStyle;
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 28f), player.playerId, nameStyle);

        int availableCount = 0;
        List<int> availableValues = new List<int>();
        if (player.dice != null)
        {
            for (int dieIndex = 0; dieIndex < player.dice.Count; dieIndex++)
            {
                if (!gameManager.IsDieAvailable(playerIndex, dieIndex)) continue;
                availableCount++;
                availableValues.Add(player.dice[dieIndex]);
            }
        }

        string diceText = availableCount == 0 ? "—" : string.Join("  ", availableValues);
        GUI.Label(new Rect(rect.x, rect.y + 28f, rect.width, 25f), $"Dice: {diceText}", labelStyle);

        string playedText = "—";
        if (playerIndex < lastPlayedDieIndex.Count && lastPlayedDieIndex[playerIndex] >= 0)
            playedText = $"{lastPlayedDieValue[playerIndex]} (Die {lastPlayedDieIndex[playerIndex] + 1})";
        GUI.Label(new Rect(rect.x, rect.y + 52f, rect.width, 25f), $"Played die: {playedText}", labelStyle);

        int cardsInHand = player.hand == null || player.hand.cardsInHand == null ? 0 : player.hand.cardsInHand.Count;
        GUI.Label(new Rect(rect.x, rect.y + 76f, rect.width, 25f), $"Cards in hand: {cardsInHand}", labelStyle);
        GUI.Label(new Rect(rect.x, rect.y + 100f, rect.width, 25f), $"Total chips: {player.chips}", labelStyle);

        GUI.Label(new Rect(rect.x, rect.y + 128f, rect.width, 1f), GUIContent.none, dividerStyle);
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };
        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft
        };
        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        playerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        activePlayerStyle = new GUIStyle(playerStyle)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold
        };
        dividerStyle = new GUIStyle(GUI.skin.box);
    }
}
