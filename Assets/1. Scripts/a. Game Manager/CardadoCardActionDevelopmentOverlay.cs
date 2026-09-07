using System;
using UnityEngine;

/// <summary>Development presentation only. Gameplay state is changed through the rules API.</summary>
public sealed class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoCardActionManager actions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoCardActionDevelopmentOverlay>() != null) return;
        var host = new GameObject("CardadoCardActionDevelopmentOverlay");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoCardActionDevelopmentOverlay>();
    }

    private void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager != null && actions == null) actions = gameManager.CardActionManager;
    }

    private void OnGUI()
    {
        if (gameManager == null || actions == null) return;
        if (gameManager.Phase == CardadoGamePhase.WarResolution) return;
        if (gameManager.Phase != CardadoGamePhase.CardActionDecision && gameManager.Phase != CardadoGamePhase.PlayingHands) return;
        int player = gameManager.CurrentHandPlayerIndex;
        if (player < 0 || player >= gameManager.Players.Count) return;

        GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40), GUI.skin.box);
        GUILayout.Label($"Player {player + 1} — {gameManager.Phase}");
        if (gameManager.Phase == CardadoGamePhase.CardActionDecision) DrawCardAction(player);
        else DrawDieAction(player);
        GUILayout.EndArea();
    }

    private void DrawCardAction(int player)
    {
        switch (actions.PendingChoice)
        {
            case CardadoCardActionPendingChoice.None: DrawHand(player); break;
            case CardadoCardActionPendingChoice.ModifierDirection:
                if (actions.PendingCardActionCard.data.canAdd && GUILayout.Button("+1")) Act(() => actions.TryChooseModifierDirection(1));
                if (actions.PendingCardActionCard.data.canSubtract && GUILayout.Button("-1")) Act(() => actions.TryChooseModifierDirection(-1));
                break;
            case CardadoCardActionPendingChoice.ModifierTarget: DrawModifierTargets(); break;
            case CardadoCardActionPendingChoice.ArtistDie: DrawDice(player, actions.TryChooseArtistDie); break;
            case CardadoCardActionPendingChoice.KnightTarget: DrawOpponents(actions.TryChooseKnightTarget); break;
            case CardadoCardActionPendingChoice.KnightDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseKnightDie); break;
            case CardadoCardActionPendingChoice.CollectorTarget: DrawCollectorTargets(); break;
            case CardadoCardActionPendingChoice.CollectorCard: DrawHiddenCards(actions.PendingTargetIndex, actions.TryChooseCollectorCard); break;
            case CardadoCardActionPendingChoice.BodyguardDie: DrawDice(player, actions.TryChooseBodyguardDie); break;
            case CardadoCardActionPendingChoice.MirrorTarget: DrawOpponents(actions.TryChooseMirrorTarget); break;
            case CardadoCardActionPendingChoice.MirrorOwnDie: DrawDice(player, actions.TryChooseMirrorOwnDie); break;
            case CardadoCardActionPendingChoice.MirrorOpponentDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseMirrorOpponentDie); break;
            case CardadoCardActionPendingChoice.ExecutionerTarget: DrawOpponents(actions.TryChooseExecutionerTarget); break;
            case CardadoCardActionPendingChoice.SpecialArtistMode:
                if (GUILayout.Button("Reroll all own dice")) Act(() => actions.TryChooseSpecialArtistMode(true));
                if (GUILayout.Button("Reroll one die 3x and choose")) Act(() => actions.TryChooseSpecialArtistMode(false));
                break;
            case CardadoCardActionPendingChoice.SpecialArtistDie: DrawDice(player, actions.TryChooseSpecialArtistDie); break;
            case CardadoCardActionPendingChoice.SpecialArtistResult:
                for (int i = 0; i < 3; i++) { int n = i; if (GUILayout.Button($"Keep {actions.GetSpecialArtistResult(n)}")) Act(() => actions.TryChooseSpecialArtistResult(n)); }
                break;
            case CardadoCardActionPendingChoice.SpecialKnightMode:
                if (GUILayout.Button("Reroll all opponents' dice")) Act(() => actions.TryChooseSpecialKnightMode(true));
                if (GUILayout.Button("All opponents reroll one chosen die")) Act(() => actions.TryChooseSpecialKnightMode(false));
                break;
            case CardadoCardActionPendingChoice.SpecialKnightDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseSpecialKnightDie); break;
            case CardadoCardActionPendingChoice.SpecialKnightAllDie: DrawSharedOpponentDie(); break;
            case CardadoCardActionPendingChoice.SpecialCollectorMode:
                if (GUILayout.Button("Take one hidden card from each opponent, then play one")) Act(() => actions.TryChooseSpecialCollectorMode(true));
                if (GUILayout.Button("Draw 3 and do not play")) Act(() => actions.TryChooseSpecialCollectorMode(false));
                break;
            case CardadoCardActionPendingChoice.SpecialCollectorTake: DrawSpecialCollectorTake(); break;
            case CardadoCardActionPendingChoice.SpecialCollectorPlay:
                for (int i = 0; i < actions.SpecialCollectorPool.Count; i++) { int n = i; var card = actions.SpecialCollectorPool[n]; if (GUILayout.Button($"Play {card.data.cardType} [{card.data.rarity}]")) Act(() => actions.TryChooseSpecialCollectorPlay(n)); }
                break;
            case CardadoCardActionPendingChoice.SpecialBodyguardMode:
                if (GUILayout.Button("Protect all own dice")) Act(() => actions.TryChooseSpecialBodyguardMode(true));
                if (GUILayout.Button("Protect all dice for the round")) Act(() => actions.TryChooseSpecialBodyguardMode(false));
                break;
            case CardadoCardActionPendingChoice.SpecialMirrorMode:
                if (GUILayout.Button("Swap one own die with an opponent die")) Act(() => actions.TryChooseSpecialMirrorMode(true));
                if (gameManager.Players.Count >= 3 && GUILayout.Button("Swap one die between two opponents")) Act(() => actions.TryChooseSpecialMirrorMode(false));
                break;
            case CardadoCardActionPendingChoice.SpecialMirrorTarget: DrawOpponents(actions.TryChooseSpecialMirrorTarget); break;
            case CardadoCardActionPendingChoice.SpecialMirrorOwnDie: DrawDice(player, actions.TryChooseSpecialMirrorOwnDie); break;
            case CardadoCardActionPendingChoice.SpecialMirrorOpponentDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseSpecialMirrorOpponentDie); break;
            case CardadoCardActionPendingChoice.SpecialMirrorFirstOpponent: DrawOpponents(actions.TryChooseSpecialMirrorFirstOpponent); break;
            case CardadoCardActionPendingChoice.SpecialMirrorFirstDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseSpecialMirrorFirstDie); break;
            case CardadoCardActionPendingChoice.SpecialMirrorSecondOpponent: DrawOpponents(actions.TryChooseSpecialMirrorSecondOpponent, actions.PendingTargetIndex); break;
            case CardadoCardActionPendingChoice.SpecialMirrorSecondDie: DrawDice(actions.PendingSecondTargetIndex, actions.TryChooseSpecialMirrorSecondDie); break;
            case CardadoCardActionPendingChoice.SpecialExecutionerTarget: DrawOpponents(actions.TryChooseSpecialExecutionerTarget); break;
            case CardadoCardActionPendingChoice.JokerTarget: DrawAllPlayers(); break;
            case CardadoCardActionPendingChoice.JokerDie: DrawDice(actions.PendingTargetIndex, actions.TryChooseJokerDie); break;
            case CardadoCardActionPendingChoice.NoblemanEffect:
                DrawNobleman(CardType.Artist, "Artist"); DrawNobleman(CardType.Knight, "Soldier"); DrawNobleman(CardType.Collector, "Collector"); DrawNobleman(CardType.Bodyguard, "Bodyguard");
                break;
        }
    }

    private void DrawHand(int player)
    {
        GUILayout.Label("Choose a card or skip.");
        for (int i = 0; i < gameManager.Players[player].hand.cardsInHand.Count; i++)
        {
            CardInstance card = gameManager.Players[player].hand.cardsInHand[i];
            if (card == null || card.data == null) continue;
            int index = i;
            if (GUILayout.Button($"Play {card.data.cardType} [{card.data.rarity}]")) Act(() => gameManager.TryPlayCard(player, index));
        }
        if (GUILayout.Button("Skip card action")) Act(() => gameManager.TrySkipCardAction(player));
    }

    private void DrawDieAction(int player)
    {
        GUILayout.Label("Choose an available die.");
        for (int i = 0; i < gameManager.Players[player].dice.Count; i++)
        {
            if (!gameManager.IsDieAvailable(player, i)) continue;
            int index = i;
            if (GUILayout.Button($"Die {index + 1}: {gameManager.Players[player].dice[index]}")) Act(() => gameManager.TryPlayDie(player, index));
        }
    }

    private void DrawModifierTargets()
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
            for (int d = 0; d < gameManager.Players[p].dice.Count; d++)
            {
                int player = p, die = d;
                if (!gameManager.IsDieTargetable(player, die)) continue;
                if (GUILayout.Button($"P{player + 1} die {die + 1}: {gameManager.Players[player].dice[die]}")) Act(() => actions.TryChooseModifierTarget(player, die));
            }
    }

    private void DrawDice(int player, Func<int, bool> action)
    {
        if (player < 0 || player >= gameManager.Players.Count) return;
        for (int d = 0; d < gameManager.Players[player].dice.Count; d++)
        {
            int die = d;
            if (!gameManager.IsDieTargetable(player, die)) continue;
            if (GUILayout.Button($"Die {die + 1}: {gameManager.Players[player].dice[die]}")) Act(() => action(die));
        }
    }

    private void DrawOpponents(Func<int, bool> action, int excluded = -1)
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == actions.PendingActorIndex || p == excluded) continue;
            int target = p;
            if (GUILayout.Button($"Player {target + 1}")) Act(() => action(target));
        }
    }

    private void DrawCollectorTargets()
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == actions.PendingActorIndex || gameManager.Players[p].hand.cardsInHand.Count == 0) continue;
            int target = p;
            if (GUILayout.Button($"Take a hidden card from Player {target + 1}")) Act(() => actions.TryChooseCollectorTarget(target));
        }
    }

    private void DrawHiddenCards(int player, Func<int, bool> action)
    {
        if (player < 0 || player >= gameManager.Players.Count) return;
        for (int i = 0; i < gameManager.Players[player].hand.cardsInHand.Count; i++)
        {
            int slot = i;
            if (GUILayout.Button($"Card slot {slot + 1}")) Act(() => action(slot));
        }
    }

    private void DrawSpecialCollectorTake()
    {
        int seen = 0;
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == actions.PendingActorIndex || gameManager.Players[p].hand.cardsInHand.Count == 0) continue;
            if (seen++ == actions.SpecialCollectorPosition) { DrawHiddenCards(p, actions.TryChooseSpecialCollectorCard); return; }
        }
    }

    private void DrawSharedOpponentDie()
    {
        if (actions.PendingActorIndex < 0) return;
        for (int d = 0; d < gameManager.Players[actions.PendingActorIndex].dice.Count; d++)
        {
            int die = d;
            if (GUILayout.Button($"Reroll die {die + 1} on all opponents")) Act(() => actions.TryChooseSpecialKnightAllDie(die));
        }
    }

    private void DrawAllPlayers()
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            int target = p;
            if (GUILayout.Button(target == actions.PendingActorIndex ? "Own die" : $"Player {target + 1} die")) Act(() => actions.TryChooseJokerTarget(target));
        }
    }

    private void DrawNobleman(CardType type, string label)
    {
        if (GUILayout.Button(label + " special")) Act(() => actions.TryChooseNoblemanEffect(type));
    }

    private void Act(Func<bool> action)
    {
        action();
        GUIUtility.ExitGUI();
    }
}
