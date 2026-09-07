using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only presentation fix for War card selection.
/// It binds buttons to captured CardInstance references, logs the selected card,
/// and exits the IMGUI pass immediately after an action to prevent list mutation
/// from changing the card represented by a button.
///
/// The WarManager remains authoritative; this component only submits War actions.
/// </summary>
public sealed class CardadoWarCardActionOverlayFix : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;
    private CardInstance selectedCard;
    private CardadoWarContext.Participant selectedActor;
    private int choiceStage;
    private bool choiceOverlay;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoWarCardActionOverlayFix>() != null) return;
        GameObject host = new GameObject("Cardado War Card Action Overlay Fix");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoWarCardActionOverlayFix>();
    }

    private void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();

        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution)
        {
            selectedCard = null;
            selectedActor = null;
            choiceStage = 0;
            choiceOverlay = false;
            return;
        }

        if (!warManager.WarInProgress)
        {
            selectedCard = null;
            selectedActor = null;
            choiceStage = 0;
            choiceOverlay = false;
        }
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution || !warManager.WarInProgress)
            return;

        CardadoWarContext context = warManager.Context;
        if (context == null || context.CurrentPlayer == null) return;

        EnsureStyles();
        GUI.depth = -100;

        const float width = 820f;
        const float height = 560f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        if (!choiceOverlay && warManager.IsWarCardActionPending)
            DrawCardSelection(panel, context.CurrentPlayer, width);
        else if (choiceOverlay)
            DrawChoice(panel, width);
    }

    private void DrawCardSelection(Rect panel, CardadoWarContext.Participant actor, float width)
    {
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE CARD", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 30),
            $"{actor.PlayerId} — select a War card", GUI.skin.label);

        List<CardInstance> snapshot = new List<CardInstance>(actor.Cards);
        float x = panel.x + 25;
        for (int i = 0; i < snapshot.Count; i++)
        {
            CardInstance capturedCard = snapshot[i];
            if (capturedCard == null || capturedCard.data == null) continue;

            if (GUI.Button(new Rect(x, panel.y + 125, 150, 80),
                capturedCard.data.id + "\n" + capturedCard.data.cardType, buttonStyle))
            {
                int currentIndex = FindCardIndex(actor, capturedCard);
                if (currentIndex < 0) return;

                selectedCard = capturedCard;
                selectedActor = actor;
                Debug.Log($"[Cardado][War] CARD SELECTED (UI): {actor.PlayerId} -> {capturedCard.data.id} [{capturedCard.data.cardType}].");
                bool accepted = warManager.TryPlayWarCard(actor.PlayerIndex, currentIndex);
                if (!accepted)
                {
                    selectedCard = null;
                    selectedActor = null;
                    return;
                }

                if (capturedCard.data.cardType == CardType.GordonRobleys)
                {
                    choiceStage = 1;
                    choiceOverlay = true;
                }
                else if (capturedCard.data.cardType == CardType.Bodyguard && capturedCard.data.rarity == CardRarity.Normal)
                {
                    choiceStage = 2;
                    choiceOverlay = true;
                }
                else if (capturedCard.data.cardType == CardType.Artist && capturedCard.data.rarity == CardRarity.Normal)
                {
                    choiceStage = 3;
                    choiceOverlay = true;
                }
                else
                {
                    selectedCard = null;
                    selectedActor = null;
                }

                GUIUtility.ExitGUI();
            }

            x += 165f;
            if (x > panel.x + width - 170f) break;
        }
    }

    private int FindCardIndex(CardadoWarContext.Participant actor, CardInstance card)
    {
        for (int i = 0; i < actor.Cards.Count; i++)
            if (ReferenceEquals(actor.Cards[i], card)) return i;
        return -1;
    }

    private void DrawChoice(Rect panel, float width)
    {
        GUI.Box(panel, GUIContent.none, panelStyle);
        string title = choiceStage == 1 ? "NOBLEMAN — CHOOSE SPECIAL" :
            choiceStage == 2 ? "BODYGUARD — CHOOSE DIE" : "ARTIST — CHOOSE DIE";
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), title, titleStyle);
        if (selectedActor == null || selectedCard == null)
        {
            choiceOverlay = false;
            return;
        }

        if (choiceStage == 1)
        {
            DrawChoiceButton(panel, 25, 120, 175, 70, "ARTIST SPECIAL", () => CompleteChoice(warManager.TryChooseNoblemanEffect(CardType.Artist)));
            DrawChoiceButton(panel, 215, 120, 175, 70, "SOLDIER SPECIAL", () => CompleteChoice(warManager.TryChooseNoblemanEffect(CardType.Soldier)));
            DrawChoiceButton(panel, 405, 120, 175, 70, "COLLECTOR SPECIAL", () => CompleteChoice(warManager.TryChooseNoblemanEffect(CardType.Collector)));
            DrawChoiceButton(panel, 595, 120, 175, 70, "BODYGUARD SPECIAL", () => CompleteChoice(warManager.TryChooseNoblemanEffect(CardType.Bodyguard)));
            return;
        }

        for (int i = 0; i < selectedActor.Dice.Count; i++)
        {
            if (!warManager.IsWarDieAvailable(selectedActor.PlayerIndex, i)) continue;
            int capturedIndex = i;
            string label = $"DIE {capturedIndex + 1}\n{selectedActor.Dice[capturedIndex]}";
            if (GUI.Button(new Rect(panel.x + 30 + capturedIndex * 125, panel.y + 120, 110, 70), label, buttonStyle))
            {
                bool accepted = choiceStage == 2
                    ? warManager.TryChooseWarBodyguardDie(capturedIndex)
                    : warManager.TryChooseWarArtistDie(capturedIndex);
                if (accepted)
                {
                    Debug.Log($"[Cardado][War] CARD RESOLVED (UI): {selectedActor.PlayerId} -> {selectedCard.data.id} [{selectedCard.data.cardType}].");
                    selectedCard = null;
                    selectedActor = null;
                    choiceStage = 0;
                    choiceOverlay = false;
                }
                GUIUtility.ExitGUI();
            }
        }
    }

    private void CompleteChoice(bool accepted)
    {
        if (accepted)
        {
            Debug.Log($"[Cardado][War] CARD CHOICE ACCEPTED (UI): {selectedActor.PlayerId} -> {selectedCard.data.id} [{selectedCard.data.cardType}].");
            selectedCard = null;
            selectedActor = null;
            choiceStage = 0;
            choiceOverlay = false;
        }
    }

    private void DrawChoiceButton(Rect panel, float x, float y, float width, float height, string label, System.Action action)
    {
        if (GUI.Button(new Rect(panel.x + x, panel.y + y, width, height), label, buttonStyle))
        {
            action();
            GUIUtility.ExitGUI();
        }
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
    }
}
