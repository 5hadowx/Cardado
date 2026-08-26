using UnityEngine;

/// <summary>
/// Temporary development UI for the card-action step. The real UI will replace
/// this component later; it exists so card effects can be tested without editing
/// the existing hand/dice tester.
/// </summary>
public class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private bool visible;
    private CardadoCardActionRequestType requestType;
    private int playerIndex = -1;

    private GUIStyle panelStyle;
    private GUIStyle modalBackgroundStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null)
            gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager == null)
            gameManager = GetComponent<CardadoGameManager>();

        if (gameManager == null)
            return;

        gameManager.CardActionRequested += OnCardActionRequested;
        gameManager.HandTurnStarted += OnHandTurnStarted;
    }

    private void OnDisable()
    {
        if (gameManager == null)
            return;

        gameManager.CardActionRequested -= OnCardActionRequested;
        gameManager.HandTurnStarted -= OnHandTurnStarted;
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType type)
    {
        playerIndex = GetPlayerIndex(player);
        requestType = type;
        visible = playerIndex >= 0;

        if (visible)
        {
            string request = type == CardadoCardActionRequestType.ChooseCard
                ? "choose a card or skip"
                : "choose a die for Artist";
            Debug.Log($"[Cardado] CARD ACTION REQUIRED: {player.playerId} — {request}.");
        }
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int handNumber, int handStarterIndex)
    {
        visible = false;
        playerIndex = -1;
    }

    private void OnGUI()
    {
        if (!visible || gameManager == null || playerIndex < 0)
            return;

        EnsureStyles();

        if (requestType == CardadoCardActionRequestType.ChooseCard)
            DrawCardChoicePanel();
        else
            DrawArtistDiePanel();
    }

    private void DrawCardChoicePanel()
    {
        if (playerIndex >= gameManager.Players.Count)
        {
            visible = false;
            playerIndex = -1;
            return;
        }

        CardadoPlayerState player = gameManager.Players[playerIndex];
        if (player == null || player.hand == null || player.hand.cardsInHand == null)
        {
            visible = false;
            playerIndex = -1;
            return;
        }

        const float width = 820f;
        const float height = 390f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        // This is a modal development overlay. Cover the previous die-selection
        // tester so the old panel cannot remain visible behind the new card UI.
        DrawModalBackground();

        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"{player.playerId} — CARD ACTION", titleStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 68f, width - 50f, 30f),
            "Play a card before choosing a die, or skip the card action.", GUI.skin.label);

        float buttonWidth = 145f;
        float buttonHeight = 90f;
        float spacing = 12f;

        // Snapshot the hand before drawing buttons. Playing a card can remove it
        // from cardsInHand immediately, so iterating the live list can otherwise
        // produce an ArgumentOutOfRangeException during this same OnGUI pass.
        CardInstance[] cards = player.hand.cardsInHand.ToArray();
        int count = cards.Length;
        float totalWidth = count * buttonWidth + Mathf.Max(0, count - 1) * spacing;
        float startX = panel.x + (width - totalWidth) * 0.5f;
        float y = panel.y + 120f;

        for (int i = 0; i < count; i++)
        {
            CardInstance card = cards[i];
            if (card == null || card.data == null)
                continue;

            string label = card.data.id + "\n" + card.data.cardType +
                           (card.data.isBlankCard ? "\nBlank" : "");
            Rect buttonRect = new Rect(startX + i * (buttonWidth + spacing), y, buttonWidth, buttonHeight);

            if (GUI.Button(buttonRect, label, buttonStyle))
            {
                TryPlayCard(i);
                // GameManager may synchronously request the next player's card
                // action. Stop this GUI pass so the new state is drawn cleanly.
                return;
            }
        }

        Rect skipRect = new Rect(panel.x + 25f, panel.y + 290f, width - 50f, 55f);
        if (GUI.Button(skipRect, "SKIP CARD ACTION", buttonStyle))
        {
            TrySkipCardAction();
            return;
        }
    }

    private void DrawArtistDiePanel()
    {
        if (playerIndex >= gameManager.Players.Count)
        {
            visible = false;
            playerIndex = -1;
            return;
        }

        CardadoPlayerState player = gameManager.Players[playerIndex];
        const float width = 760f;
        const float height = 300f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        DrawModalBackground();
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 20f, width - 50f, 45f),
            $"{player.playerId} — ARTIST", titleStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 68f, width - 50f, 30f),
            "Choose one of your available dice to reroll.", GUI.skin.label);

        float buttonWidth = 90f;
        float spacing = 12f;
        float totalWidth = gameManager.RoundDiceCount * buttonWidth + (gameManager.RoundDiceCount - 1) * spacing;
        float startX = panel.x + (width - totalWidth) * 0.5f;

        for (int dieIndex = 0; dieIndex < player.dice.Count; dieIndex++)
        {
            if (!gameManager.IsDieAvailable(playerIndex, dieIndex))
                continue;

            Rect buttonRect = new Rect(startX + dieIndex * (buttonWidth + spacing), panel.y + 125f, buttonWidth, 70f);
            if (GUI.Button(buttonRect, $"Die {dieIndex + 1}\n{player.dice[dieIndex]}", buttonStyle))
            {
                TryResolveArtistDie(dieIndex);
                return;
            }
        }
    }

    private void DrawModalBackground()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0.035f, 0.06f, 0.10f, 1f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, modalBackgroundStyle);
        GUI.color = previousColor;
    }

    private void TryPlayCard(int cardIndex)
    {
        try
        {
            if (!gameManager.TryPlayCard(playerIndex, cardIndex))
                Debug.LogWarning($"[Cardado] Card choice rejected for Player {playerIndex + 1}: card {cardIndex + 1}.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void TrySkipCardAction()
    {
        try
        {
            if (!gameManager.TrySkipCardAction(playerIndex))
                Debug.LogWarning($"[Cardado] Card-action skip rejected for Player {playerIndex + 1}.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void TryResolveArtistDie(int dieIndex)
    {
        try
        {
            if (!gameManager.TryResolveArtistDie(playerIndex, dieIndex))
                Debug.LogWarning($"[Cardado] Artist target rejected for Player {playerIndex + 1}: die {dieIndex + 1}.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
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

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.padding = new RectOffset(20, 20, 20, 20);

        modalBackgroundStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = Texture2D.whiteTexture }
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
    }
}
