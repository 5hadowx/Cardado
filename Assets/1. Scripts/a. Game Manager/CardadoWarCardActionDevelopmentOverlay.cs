using UnityEngine;

/// <summary>Development-only War card presentation. It requests actions from WarManager.</summary>
public sealed class CardadoWarCardActionDevelopmentOverlay : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoWarCardActionDevelopmentOverlay>() != null) return;
        var host = new GameObject("CardadoWarCardActionDevelopmentOverlay");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoWarCardActionDevelopmentOverlay>();
    }

    private void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager != null && warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || !warManager.WarInProgress || !warManager.IsWarCardActionPending) return;
        var actor = warManager.PendingChoiceActor;
        if (actor == null) return;
        GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40), GUI.skin.box);
        GUILayout.Label($"War Player {actor.PlayerIndex + 1} — {warManager.PendingChoice}");
        DrawPending(actor);
        GUILayout.EndArea();
    }

    private void DrawPending(CardadoWarContext.Participant actor)
    {
        switch (warManager.PendingChoice)
        {
            case CardadoWarPendingChoice.ModifierTarget: DrawModifierTargets(actor); break;
            case CardadoWarPendingChoice.ModifierSign:
                if (GUILayout.Button("+1")) Act(() => warManager.TryChooseModifierValue(1));
                if (GUILayout.Button("-1")) Act(() => warManager.TryChooseModifierValue(-1));
                break;
            case CardadoWarPendingChoice.ArtistDie: DrawWarDice(actor, warManager.TryChooseArtistDie); break;
            case CardadoWarPendingChoice.KnightDie: DrawWarDice(warManager.Context.OpponentOf(actor), warManager.TryChooseKnightDie); break;
            case CardadoWarPendingChoice.CollectorOpponentCard: DrawHiddenCards(warManager.Context.OpponentOf(actor)); break;
            case CardadoWarPendingChoice.BodyguardDie: DrawWarDice(actor, warManager.TryChooseWarBodyguardDie); break;
            case CardadoWarPendingChoice.MirrorOwnDie: DrawWarDice(actor, warManager.TryChooseMirrorOwnDie); break;
            case CardadoWarPendingChoice.MirrorOpponentDie: DrawWarDice(warManager.Context.OpponentOf(actor), warManager.TryChooseMirrorOpponentDie); break;
            case CardadoWarPendingChoice.JokerPlayer:
                if (GUILayout.Button("Flip own die")) Act(() => warManager.TryChooseJokerTarget(false));
                if (GUILayout.Button("Flip opponent die")) Act(() => warManager.TryChooseJokerTarget(true));
                break;
            case CardadoWarPendingChoice.JokerDie: DrawJokerDice(actor); break;
            case CardadoWarPendingChoice.SpecialArtistMode:
                if (GUILayout.Button("Reroll all own dice")) Act(() => warManager.TryChooseSpecialArtistMode(true));
                if (GUILayout.Button("Reroll one die 3x and choose")) Act(() => warManager.TryChooseSpecialArtistMode(false));
                break;
            case CardadoWarPendingChoice.SpecialArtistDie: DrawWarDice(actor, warManager.TryChooseSpecialArtistDie); break;
            case CardadoWarPendingChoice.SpecialArtistResult:
                for (int i = 0; i < 3; i++) { int n = i; if (GUILayout.Button($"Keep {warManager.GetPendingArtistResult(n)}")) Act(() => warManager.TryChooseSpecialArtistResult(n)); }
                break;
            case CardadoWarPendingChoice.SpecialKnightMode:
                if (GUILayout.Button("Reroll all opponent dice")) Act(() => warManager.TryChooseSpecialKnightMode(true));
                if (GUILayout.Button("Reroll one opponent die")) Act(() => warManager.TryChooseSpecialKnightMode(false));
                break;
            case CardadoWarPendingChoice.SpecialKnightDie: DrawWarDice(warManager.Context.OpponentOf(actor), warManager.TryChooseSpecialKnightDie); break;
            case CardadoWarPendingChoice.SpecialCollectorMode:
                if (GUILayout.Button("Take one own + one opponent card, play one")) Act(() => warManager.TryChooseSpecialCollectorMode(true));
                if (GUILayout.Button("Draw 3 and do not play")) Act(() => warManager.TryChooseSpecialCollectorMode(false));
                break;
            case CardadoWarPendingChoice.SpecialCollectorOwnCard: DrawHiddenCards(actor); break;
            case CardadoWarPendingChoice.SpecialCollectorOpponentCard: DrawHiddenCards(warManager.Context.OpponentOf(actor)); break;
            case CardadoWarPendingChoice.SpecialCollectorPlayChoice:
                if (GUILayout.Button("Play selected own card")) Act(() => warManager.TryChooseSpecialCollectorPlayedCard(true));
                if (GUILayout.Button("Play selected opponent card")) Act(() => warManager.TryChooseSpecialCollectorPlayedCard(false));
                break;
            case CardadoWarPendingChoice.SpecialBodyguardMode:
                if (GUILayout.Button("Protect own dice")) Act(() => warManager.TryChooseSpecialBodyguardMode(true));
                if (GUILayout.Button("Protect all dice for the hand")) Act(() => warManager.TryChooseSpecialBodyguardMode(false));
                break;
            case CardadoWarPendingChoice.SpecialMirrorMode:
                if (GUILayout.Button("Swap one die each")) Act(() => warManager.TryChooseSpecialMirrorMode(true));
                if (GUILayout.Button("Swap the alternate Mirror mode")) Act(() => warManager.TryChooseSpecialMirrorMode(false));
                break;
            case CardadoWarPendingChoice.SpecialMirrorOwnDie: DrawWarDice(actor, warManager.TryChooseSpecialMirrorOwnDie); break;
            case CardadoWarPendingChoice.SpecialMirrorOpponentDie: DrawWarDice(warManager.Context.OpponentOf(actor), warManager.TryChooseSpecialMirrorOpponentDie); break;
            case CardadoWarPendingChoice.NoblemanEffect:
                DrawNobleman(CardType.Artist, "Artist"); DrawNobleman(CardType.Knight, "Soldier"); DrawNobleman(CardType.Collector, "Collector"); DrawNobleman(CardType.Bodyguard, "Bodyguard");
                break;
            default: DrawCards(actor); break;
        }
    }

    private void DrawCards(CardadoWarContext.Participant actor)
    {
        for (int i = 0; i < actor.Cards.Count; i++)
        {
            int index = i; var card = actor.Cards[index];
            if (card == null || card.data == null) continue;
            if (GUILayout.Button($"Play {card.data.cardType} [{card.data.rarity}]")) Act(() => warManager.TryPlayWarCard(actor.PlayerIndex, index));
        }
        if (GUILayout.Button("Skip card action")) Act(() => warManager.TrySkipCardAction(actor.PlayerIndex));
    }

    private void DrawModifierTargets(CardadoWarContext.Participant actor)
    {
        DrawModifierTargetGroup(actor);
        DrawModifierTargetGroup(warManager.Context.OpponentOf(actor));
    }

    private void DrawModifierTargetGroup(CardadoWarContext.Participant target)
    {
        for (int i = 0; i < target.Dice.Count; i++)
        {
            int die = i;
            if (!warManager.Context.IsDieTargetable(target, die)) continue;
            if (GUILayout.Button($"P{target.PlayerIndex + 1} die {die + 1}: {target.Dice[die]}")) Act(() => warManager.TryChooseModifierDie(target.PlayerIndex, die));
        }
    }

    private void DrawWarDice(CardadoWarContext.Participant participant, System.Func<int, bool> action)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            int die = i;
            if (!warManager.Context.IsDieTargetable(participant, die)) continue;
            if (GUILayout.Button($"Die {die + 1}: {participant.Dice[die]}")) Act(() => action(die));
        }
    }

    private void DrawJokerDice(CardadoWarContext.Participant actor)
    {
        for (int i = 0; i < actor.Dice.Count; i++)
        {
            int die = i;
            if (GUILayout.Button($"Flip die {die + 1}")) Act(() => warManager.TryChooseJokerDie(die));
        }
    }

    private void DrawHiddenCards(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.Cards.Count; i++)
        {
            int slot = i;
            if (GUILayout.Button($"Card slot {slot + 1}"))
            {
                bool ok = warManager.PendingChoice == CardadoWarPendingChoice.CollectorOpponentCard
                    ? warManager.TryChooseCollectorCard(slot)
                    : warManager.PendingChoice == CardadoWarPendingChoice.SpecialCollectorOwnCard
                        ? warManager.TryChooseSpecialCollectorOwnCard(slot)
                        : warManager.TryChooseSpecialCollectorOpponentCard(slot);
                if (ok) GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawNobleman(CardType type, string label)
    {
        if (GUILayout.Button(label + " special")) Act(() => warManager.TryChooseNoblemanEffect(type));
    }

    private void Act(System.Func<bool> action)
    {
        action();
        GUIUtility.ExitGUI();
    }
}
