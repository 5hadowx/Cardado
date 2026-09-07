using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development presentation client for War actions. It never mutates War state directly;
/// every button calls an authoritative CardadoWarManager action request.
/// </summary>
public sealed class CardadoWarCardActionOverlayFix : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallButtonStyle;

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
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution || !warManager.IsWarPlaying)
            return;

        CardadoWarContext context = warManager.Context;
        if (context == null || context.CurrentPlayer == null) return;

        EnsureStyles();
        GUI.depth = -100;

        const float width = 900f;
        const float height = 620f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);

        DrawHeader(panel, context);

        if (warManager.PendingChoice != CardadoWarPendingChoice.None)
        {
            DrawPendingChoice(panel, width, context);
            return;
        }

        if (warManager.IsWarCardActionPending)
            DrawCardSelection(panel, width, context.CurrentPlayer);
        else
            DrawDieTurn(panel, width, context);
    }

    private void DrawHeader(Rect panel, CardadoWarContext context)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 18, panel.width - 50, 40), "WAR — TEMPORARY 1v1", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 60, panel.width - 50, 25),
            $"{context.Challenger.PlayerId} {context.ChallengerHandsWon} — {context.TargetHandsWon} {context.Target.PlayerId}", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 85, panel.width - 50, 25),
            $"Hand {context.HandNumber} — {context.CurrentPlayer.PlayerId} chooses", GUI.skin.label);
    }

    private void DrawCardSelection(Rect panel, float width, CardadoWarContext.Participant actor)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 120, width - 50, 30), "Choose a War card, or skip the card action.", GUI.skin.label);

        List<CardInstance> snapshot = new List<CardInstance>(actor.Cards);
        const float cardWidth = 160f;
        const float cardHeight = 72f;
        const float gap = 10f;
        float startX = panel.x + 25;
        float startY = panel.y + 160;
        int columns = 5;

        for (int i = 0; i < snapshot.Count; i++)
        {
            CardInstance capturedCard = snapshot[i];
            if (capturedCard == null || capturedCard.data == null) continue;
            int row = i / columns;
            int col = i % columns;
            Rect button = new Rect(startX + col * (cardWidth + gap), startY + row * (cardHeight + gap), cardWidth, cardHeight);
            if (GUI.Button(button, capturedCard.data.id + "\n" + capturedCard.data.cardType, buttonStyle))
            {
                int currentIndex = FindCardIndex(actor, capturedCard);
                if (currentIndex < 0) return;
                Debug.Log($"[Cardado][War] CARD SELECTED (UI): {actor.PlayerId} -> {capturedCard.data.id} [{capturedCard.data.cardType}].");
                if (!warManager.TryPlayWarCard(actor.PlayerIndex, currentIndex)) return;
                GUIUtility.ExitGUI();
            }
        }

        float skipY = panel.y + 540;
        if (GUI.Button(new Rect(panel.x + 25, skipY, 250, 50), "SKIP CARD ACTION", smallButtonStyle))
        {
            warManager.TrySkipCardAction(actor.PlayerIndex);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawDieTurn(Rect panel, float width, CardadoWarContext context)
    {
        CardadoWarContext.Participant current = context.CurrentPlayer;
        GUI.Label(new Rect(panel.x + 25, panel.y + 120, width - 50, 30), "Choose a die to play.", GUI.skin.label);
        DrawDiceRow(panel, context.Challenger, panel.y + 165, current.PlayerIndex);
        DrawDiceRow(panel, context.Target, panel.y + 270, current.PlayerIndex);
    }

    private void DrawDiceRow(Rect panel, CardadoWarContext.Participant participant, float y, int activePlayerIndex)
    {
        GUI.Label(new Rect(panel.x + 25, y, 190, 30), participant.PlayerId, GUI.skin.label);
        float x = panel.x + 220;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            if (!warManager.IsWarDieAvailable(participant.PlayerIndex, i)) continue;
            bool active = participant.PlayerIndex == activePlayerIndex;
            GUI.enabled = active;
            Rect button = new Rect(x, y - 5, 95, 55);
            if (GUI.Button(button, $"DIE {i + 1}\n{participant.Dice[i]}", smallButtonStyle))
            {
                warManager.TryPlayWarDieForPlayer(participant.PlayerIndex, i);
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;
            x += 105;
        }
    }

    private void DrawPendingChoice(Rect panel, float width, CardadoWarContext context)
    {
        CardadoWarContext.Participant actor = warManager.PendingChoiceActor;
        CardInstance card = warManager.PendingChoiceCard;
        if (actor == null || card == null) return;

        GUI.Label(new Rect(panel.x + 25, panel.y + 120, width - 50, 35), DescribePendingChoice(), titleStyle);

        switch (warManager.PendingChoice)
        {
            case CardadoWarPendingChoice.ModifierTarget:
                DrawModifierTarget(panel, actor);
                break;
            case CardadoWarPendingChoice.ModifierSign:
                DrawModifierSign(panel, card);
                break;
            case CardadoWarPendingChoice.ArtistDie:
                DrawSingleParticipantDieChoice(panel, actor, false, warManager.TryChooseArtistDie);
                break;
            case CardadoWarPendingChoice.KnightDie:
                DrawSingleParticipantDieChoice(panel, context.OpponentOf(actor), false, warManager.TryChooseKnightDie);
                break;
            case CardadoWarPendingChoice.CollectorOpponentCard:
                DrawOpponentCardSlots(panel, actor, "Choose one hidden opponent card", (index) => warManager.TryChooseCollectorCard(index));
                break;
            case CardadoWarPendingChoice.BodyguardDie:
                DrawSingleParticipantDieChoice(panel, actor, true, warManager.TryChooseWarBodyguardDie);
                break;
            case CardadoWarPendingChoice.MirrorOwnDie:
                DrawSingleParticipantDieChoice(panel, actor, true, warManager.TryChooseMirrorOwnDie);
                break;
            case CardadoWarPendingChoice.MirrorOpponentDie:
                DrawSingleParticipantDieChoice(panel, context.OpponentOf(actor), true, warManager.TryChooseMirrorOpponentDie);
                break;
            case CardadoWarPendingChoice.JokerPlayer:
                DrawJokerTarget(panel);
                break;
            case CardadoWarPendingChoice.JokerDie:
                DrawSingleParticipantDieChoice(panel, warManager.Context.OpponentOf(actor) == warManager.PendingChoiceActor ? actor : GetJokerTarget(actor), true, warManager.TryChooseJokerDie);
                break;
            case CardadoWarPendingChoice.SpecialArtistMode:
                DrawSpecialArtistMode(panel);
                break;
            case CardadoWarPendingChoice.SpecialArtistDie:
                DrawSingleParticipantDieChoice(panel, actor, true, warManager.TryChooseSpecialArtistDie);
                break;
            case CardadoWarPendingChoice.SpecialArtistResult:
                DrawArtistResults(panel);
                break;
            case CardadoWarPendingChoice.SpecialKnightMode:
                DrawSpecialKnightMode(panel);
                break;
            case CardadoWarPendingChoice.SpecialKnightDie:
                DrawSingleParticipantDieChoice(panel, context.OpponentOf(actor), true, warManager.TryChooseSpecialKnightDie);
                break;
            case CardadoWarPendingChoice.SpecialCollectorMode:
                DrawSpecialCollectorMode(panel);
                break;
            case CardadoWarPendingChoice.SpecialCollectorOwnCard:
                DrawOwnCardSlots(panel, actor, "Choose one of your War cards", (index) => warManager.TryChooseSpecialCollectorOwnCard(index));
                break;
            case CardadoWarPendingChoice.SpecialCollectorOpponentCard:
                DrawOpponentCardSlots(panel, actor, "Choose one hidden opponent card", (index) => warManager.TryChooseSpecialCollectorOpponentCard(index));
                break;
            case CardadoWarPendingChoice.SpecialCollectorPlayChoice:
                DrawSpecialCollectorPlayChoice(panel);
                break;
            case CardadoWarPendingChoice.SpecialBodyguardMode:
                DrawSpecialBodyguardMode(panel);
                break;
            case CardadoWarPendingChoice.SpecialMirrorMode:
                DrawSpecialMirrorMode(panel);
                break;
            case CardadoWarPendingChoice.SpecialMirrorOwnDie:
                DrawSingleParticipantDieChoice(panel, actor, true, warManager.TryChooseSpecialMirrorOwnDie);
                break;
            case CardadoWarPendingChoice.SpecialMirrorOpponentDie:
                DrawSingleParticipantDieChoice(panel, context.OpponentOf(actor), true, warManager.TryChooseSpecialMirrorOpponentDie);
                break;
            case CardadoWarPendingChoice.NoblemanEffect:
                DrawNoblemanChoice(panel);
                break;
        }
    }

    private CardadoWarContext.Participant GetJokerTarget(CardadoWarContext.Participant actor)
    {
        // The manager owns the selected target; the UI can infer it from the pending target by
        // checking which participant is currently targetable after the choice. This fallback keeps
        // the presentation side-effect free. The manager validates the final die request.
        return actor;
    }

    private string DescribePendingChoice()
    {
        switch (warManager.PendingChoice)
        {
            case CardadoWarPendingChoice.ModifierTarget: return "MODIFIER — CHOOSE DIE";
            case CardadoWarPendingChoice.ModifierSign: return "MODIFIER — CHOOSE +1 OR -1";
            case CardadoWarPendingChoice.ArtistDie: return "ARTIST — CHOOSE YOUR DIE";
            case CardadoWarPendingChoice.KnightDie: return "SOLDIER — CHOOSE OPPONENT DIE";
            case CardadoWarPendingChoice.CollectorOpponentCard: return "COLLECTOR — CHOOSE HIDDEN OPPONENT CARD";
            case CardadoWarPendingChoice.BodyguardDie: return "BODYGUARD — CHOOSE YOUR DIE";
            case CardadoWarPendingChoice.MirrorOwnDie: return "MIRROR — CHOOSE YOUR DIE";
            case CardadoWarPendingChoice.MirrorOpponentDie: return "MIRROR — CHOOSE OPPONENT DIE";
            case CardadoWarPendingChoice.JokerPlayer: return "JOKER — CHOOSE TARGET";
            case CardadoWarPendingChoice.JokerDie: return "JOKER — CHOOSE DIE";
            case CardadoWarPendingChoice.SpecialArtistMode: return "ARTIST SPECIAL — CHOOSE MODE";
            case CardadoWarPendingChoice.SpecialArtistDie: return "ARTIST SPECIAL — CHOOSE DIE";
            case CardadoWarPendingChoice.SpecialArtistResult: return "ARTIST SPECIAL — CHOOSE RESULT";
            case CardadoWarPendingChoice.SpecialKnightMode: return "SOLDIER SPECIAL — CHOOSE MODE";
            case CardadoWarPendingChoice.SpecialKnightDie: return "SOLDIER SPECIAL — CHOOSE OPPONENT DIE";
            case CardadoWarPendingChoice.SpecialCollectorMode: return "COLLECTOR SPECIAL — CHOOSE MODE";
            case CardadoWarPendingChoice.SpecialCollectorOwnCard: return "COLLECTOR SPECIAL — CHOOSE YOUR CARD";
            case CardadoWarPendingChoice.SpecialCollectorOpponentCard: return "COLLECTOR SPECIAL — CHOOSE OPPONENT CARD";
            case CardadoWarPendingChoice.SpecialCollectorPlayChoice: return "COLLECTOR SPECIAL — CHOOSE CARD TO PLAY";
            case CardadoWarPendingChoice.SpecialBodyguardMode: return "BODYGUARD SPECIAL — CHOOSE MODE";
            case CardadoWarPendingChoice.SpecialMirrorMode: return "MIRROR SPECIAL — CHOOSE MODE";
            case CardadoWarPendingChoice.SpecialMirrorOwnDie: return "MIRROR SPECIAL — CHOOSE YOUR DIE";
            case CardadoWarPendingChoice.SpecialMirrorOpponentDie: return "MIRROR SPECIAL — CHOOSE OPPONENT DIE";
            case CardadoWarPendingChoice.NoblemanEffect: return "NOBLEMAN — CHOOSE SPECIAL";
            default: return "CHOOSE";
        }
    }

    private void DrawModifierTarget(Rect panel, CardadoWarContext.Participant actor)
    {
        DrawSingleParticipantDieChoice(panel, actor, true, (index) => warManager.TryChooseModifierDie(actor.PlayerIndex, index));
        float y = panel.y + 360;
        DrawSingleParticipantDieChoiceAt(panel, contextOpponent(actor), y, true, (index) => warManager.TryChooseModifierDie(contextOpponent(actor).PlayerIndex, index));
    }

    private CardadoWarContext.Participant contextOpponent(CardadoWarContext.Participant actor) => warManager.Context.OpponentOf(actor);

    private void DrawModifierSign(Rect panel, CardData card)
    {
        float x = panel.x + 160;
        if (card.data.canAdd && GUI.Button(new Rect(x, panel.y + 210, 250, 70), "+1", buttonStyle))
        {
            warManager.TryChooseModifierValue(1);
            GUIUtility.ExitGUI();
        }
        if (card.data.canSubtract && GUI.Button(new Rect(x + 270, panel.y + 210, 250, 70), "-1", buttonStyle))
        {
            warManager.TryChooseModifierValue(-1);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSingleParticipantDieChoice(Rect panel, CardadoWarContext.Participant participant, bool targetable, System.Func<int, bool> action)
    {
        DrawSingleParticipantDieChoiceAt(panel, participant, panel.y + 205, targetable, action);
    }

    private void DrawSingleParticipantDieChoiceAt(Rect panel, CardadoWarContext.Participant participant, float y, bool targetable, System.Func<int, bool> action)
    {
        if (participant == null) return;
        float x = panel.x + 40;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            if (!warManager.IsWarDieAvailable(participant.PlayerIndex, i)) continue;
            if (targetable && !warManager.IsWarDieTargetable(participant.PlayerIndex, i)) continue;
            int capturedIndex = i;
            if (GUI.Button(new Rect(x, y, 120, 70), $"DIE {i + 1}\n{participant.Dice[i]}", buttonStyle))
            {
                if (action(capturedIndex)) GUIUtility.ExitGUI();
            }
            x += 135;
        }
    }

    private void DrawOpponentCardSlots(Rect panel, CardadoWarContext.Participant actor, string label, System.Func<int, bool> action)
    {
        CardadoWarContext.Participant opponent = warManager.Context.OpponentOf(actor);
        GUI.Label(new Rect(panel.x + 35, panel.y + 165, panel.width - 70, 30), label, GUI.skin.label);
        float x = panel.x + 35;
        for (int i = 0; i < opponent.Cards.Count; i++)
        {
            int capturedIndex = i;
            if (GUI.Button(new Rect(x, panel.y + 210, 145, 80), "CARD\nHIDDEN", buttonStyle))
            {
                if (action(capturedIndex)) GUIUtility.ExitGUI();
            }
            x += 155;
            if (x > panel.x + panel.width - 150) break;
        }
    }

    private void DrawOwnCardSlots(Rect panel, CardadoWarContext.Participant actor, string label, System.Func<int, bool> action)
    {
        GUI.Label(new Rect(panel.x + 35, panel.y + 165, panel.width - 70, 30), label, GUI.skin.label);
        float x = panel.x + 35;
        for (int i = 0; i < actor.Cards.Count; i++)
        {
            CardInstance card = actor.Cards[i];
            if (card == null || card.data == null) continue;
            int capturedIndex = i;
            if (GUI.Button(new Rect(x, panel.y + 210, 145, 80), card.data.id + "\n" + card.data.cardType, buttonStyle))
            {
                if (action(capturedIndex)) GUIUtility.ExitGUI();
            }
            x += 155;
            if (x > panel.x + panel.width - 150) break;
        }
    }

    private void DrawJokerTarget(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 100, panel.y + 210, 300, 75), "OWN DIE", buttonStyle))
        {
            warManager.TryChooseJokerTarget(false);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 500, panel.y + 210, 300, 75), "OPPONENT DIE", buttonStyle))
        {
            warManager.TryChooseJokerTarget(true);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSpecialArtistMode(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 75, panel.y + 210, 350, 80), "REROLL ALL OWN DICE ONCE", buttonStyle))
        {
            warManager.TryChooseSpecialArtistMode(true);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 475, panel.y + 210, 350, 80), "REROLL ONE DIE 3 TIMES", buttonStyle))
        {
            warManager.TryChooseSpecialArtistMode(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawArtistResults(Rect panel)
    {
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            if (GUI.Button(new Rect(panel.x + 100 + i * 230, panel.y + 220, 190, 90), "RESULT\n" + warManager.GetPendingArtistResult(captured), buttonStyle))
            {
                warManager.TryChooseSpecialArtistResult(captured);
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawSpecialKnightMode(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 75, panel.y + 210, 350, 80), "REROLL ALL OPPONENT DICE", buttonStyle))
        {
            warManager.TryChooseSpecialKnightMode(true);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 475, panel.y + 210, 350, 80), "REROLL ONE OPPONENT DIE", buttonStyle))
        {
            warManager.TryChooseSpecialKnightMode(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSpecialCollectorMode(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 75, panel.y + 200, 350, 90), "TAKE ONE FROM EACH\nPLAY ONE, DISCARD ONE", buttonStyle))
        {
            warManager.TryChooseSpecialCollectorMode(true);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 475, panel.y + 200, 350, 90), "DRAW 3\nDO NOT PLAY", buttonStyle))
        {
            warManager.TryChooseSpecialCollectorMode(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSpecialCollectorPlayChoice(Rect panel)
    {
        CardInstance own = warManager.PendingCollectorOwnCard;
        CardInstance opponent = warManager.PendingCollectorOpponentCard;
        if (own != null && GUI.Button(new Rect(panel.x + 75, panel.y + 210, 350, 90), "PLAY YOUR CARD\n" + own.data.cardType, buttonStyle))
        {
            warManager.TryChooseSpecialCollectorPlayedCard(true);
            GUIUtility.ExitGUI();
        }
        if (opponent != null && GUI.Button(new Rect(panel.x + 475, panel.y + 210, 350, 90), "PLAY OPPONENT CARD\nHIDDEN", buttonStyle))
        {
            warManager.TryChooseSpecialCollectorPlayedCard(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSpecialBodyguardMode(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 75, panel.y + 210, 350, 90), "PROTECT ALL OWN DICE", buttonStyle))
        {
            warManager.TryChooseSpecialBodyguardMode(true);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 475, panel.y + 210, 350, 90), "PROTECT ALL DICE FOR THE HAND", buttonStyle))
        {
            warManager.TryChooseSpecialBodyguardMode(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawSpecialMirrorMode(Rect panel)
    {
        if (GUI.Button(new Rect(panel.x + 75, panel.y + 210, 350, 90), "SWAP ONE OWN + ONE OPPONENT DIE", buttonStyle))
        {
            warManager.TryChooseSpecialMirrorMode(true);
            GUIUtility.ExitGUI();
        }
        if (GUI.Button(new Rect(panel.x + 475, panel.y + 210, 350, 90), "SWAP DICE BETWEEN RIVALS", buttonStyle))
        {
            warManager.TryChooseSpecialMirrorMode(false);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawNoblemanChoice(Rect panel)
    {
        CardType[] types = { CardType.Artist, CardType.Knight, CardType.Collector, CardType.Bodyguard };
        string[] labels = { "ARTIST SPECIAL", "SOLDIER SPECIAL", "COLLECTOR SPECIAL", "BODYGUARD SPECIAL" };
        for (int i = 0; i < types.Length; i++)
        {
            int captured = i;
            if (GUI.Button(new Rect(panel.x + 25 + captured * 215, panel.y + 210, 200, 90), labels[captured], buttonStyle))
            {
                warManager.TryChooseNoblemanEffect(types[captured]);
                GUIUtility.ExitGUI();
            }
        }
    }

    private int FindCardIndex(CardadoWarContext.Participant actor, CardInstance card)
    {
        for (int i = 0; i < actor.Cards.Count; i++)
            if (ReferenceEquals(actor.Cards[i], card)) return i;
        return -1;
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
        smallButtonStyle = new GUIStyle(buttonStyle) { fontSize = 15 };
    }
}
