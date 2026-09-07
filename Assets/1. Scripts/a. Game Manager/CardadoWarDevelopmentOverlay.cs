using System;
using UnityEngine;

/// <summary>Development-only War presentation. WarManager remains the rules authority.</summary>
public sealed class CardadoWarDevelopmentOverlay : MonoBehaviour
{
    private enum PreWarStep { Claim, Target, Wager, Order }

    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;
    private PreWarStep preWarStep = PreWarStep.Claim;
    private int claimSearchStart = -1;
    private int activeClaimant = -1;
    private int selectedTarget = -1;
    private int lastWarChallenger = -1;
    private bool warWasStarted;
    private CardadoGamePhase lastPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<CardadoWarDevelopmentOverlay>() != null) return;
        var host = new GameObject("CardadoWarDevelopmentOverlay");
        DontDestroyOnLoad(host);
        host.AddComponent<CardadoWarDevelopmentOverlay>();
    }

    private void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager != null && warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();

        if (gameManager == null) return;

        if (gameManager.Phase != CardadoGamePhase.WarResolution)
        {
            if (lastPhase == CardadoGamePhase.WarResolution)
                ResetWarPresentation();
        }
        else if (lastPhase != CardadoGamePhase.WarResolution)
        {
            ResetWarPresentation();
            claimSearchStart = gameManager.StartingPlayerIndex;
        }

        lastPhase = gameManager.Phase;
    }

    private void OnGUI()
    {
        if (gameManager == null || warManager == null || gameManager.Phase != CardadoGamePhase.WarResolution) return;

        GUILayout.BeginArea(new Rect(Screen.width - 540, 20, 520, Screen.height - 40), GUI.skin.box);
        GUILayout.Label("WAR");

        if (warWasStarted && warManager.Context == null)
            DrawWarComplete();
        else if (warManager.Context == null)
            DrawPreWar();
        else if (warManager.IsWarPlaying)
        {
            if (warManager.PendingChoice != CardadoWarPendingChoice.None)
                DrawPendingWarChoice();
            else if (warManager.IsWarCardActionPending)
                DrawWarCardAction();
            else
                DrawWarDice();
        }
        else
        {
            DrawWarComplete();
        }

        GUILayout.EndArea();
    }

    private void DrawPreWar()
    {
        switch (preWarStep)
        {
            case PreWarStep.Claim: DrawClaimStep(); break;
            case PreWarStep.Target: DrawTargetStep(); break;
            case PreWarStep.Wager: DrawWagerStep(); break;
            case PreWarStep.Order: DrawOrderStep(); break;
        }
    }

    private void DrawClaimStep()
    {
        int challenger = FindFirstEligibleClaimant(claimSearchStart);
        if (challenger < 0)
        {
            Act(() => warManager.TryFinishWarPhase());
            return;
        }

        activeClaimant = challenger;
        GUILayout.Label($"Player {challenger + 1}: claim or pass");
        if (warManager.CanClaimWar(challenger) && GUILayout.Button("Declare War"))
        {
            if (warManager.TryClaimWar(challenger))
            {
                lastWarChallenger = challenger;
                preWarStep = PreWarStep.Target;
            }
            Act(() => true);
        }

        if (GUILayout.Button("Pass"))
        {
            if (warManager.TryPassWar(challenger))
            {
                claimSearchStart = (challenger + 1) % gameManager.Players.Count;
                activeClaimant = -1;
            }
            Act(() => true);
        }
    }

    private void DrawTargetStep()
    {
        GUILayout.Label($"Player {lastWarChallenger + 1}: choose target");
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == lastWarChallenger || gameManager.Players[p].chips < 1) continue;
            int target = p;
            if (GUILayout.Button($"Target Player {target + 1}"))
            {
                if (warManager.TryChooseTarget(target))
                {
                    selectedTarget = target;
                    preWarStep = PreWarStep.Wager;
                }
                Act(() => true);
            }
        }
    }

    private void DrawWagerStep()
    {
        GUILayout.Label($"Target: Player {selectedTarget + 1}");
        GUILayout.Label("Choose War wager:");
        if (GUILayout.Button("Wager 1 chip"))
        {
            if (warManager.TryChooseWarWager(1)) preWarStep = PreWarStep.Order;
            Act(() => true);
        }
    }

    private void DrawOrderStep()
    {
        GUILayout.Label("Choose who plays first:");
        if (GUILayout.Button("Challenger plays first"))
        {
            if (warManager.TryChooseWarOrder(true)) warWasStarted = true;
            Act(() => true);
        }
        if (GUILayout.Button("Target plays first"))
        {
            if (warManager.TryChooseWarOrder(false)) warWasStarted = true;
            Act(() => true);
        }
    }

    private int FindFirstEligibleClaimant(int startIndex)
    {
        if (gameManager == null || gameManager.Players.Count == 0) return -1;
        if (startIndex < 0) startIndex = gameManager.StartingPlayerIndex;
        if (startIndex < 0) startIndex = 0;

        for (int offset = 0; offset < gameManager.Players.Count; offset++)
        {
            int playerIndex = (startIndex + offset) % gameManager.Players.Count;
            if (warManager.CanClaimWar(playerIndex)) return playerIndex;
        }
        return -1;
    }

    private void DrawWarCardAction()
    {
        var current = warManager.Context.CurrentPlayer;
        if (current == null) return;

        GUILayout.Label($"Player {current.PlayerIndex + 1}: choose a War card or skip");
        for (int i = 0; i < current.Cards.Count; i++)
        {
            CardInstance card = current.Cards[i];
            if (card == null || card.data == null) continue;
            int index = i;
            if (GUILayout.Button($"Play {card.data.cardType} [{card.data.rarity}]"))
                Act(() => warManager.TryPlayWarCard(current.PlayerIndex, index));
        }

        if (GUILayout.Button("Skip card action"))
            Act(() => warManager.TrySkipCardAction(current.PlayerIndex));
    }

    private void DrawPendingWarChoice()
    {
        switch (warManager.PendingChoice)
        {
            case CardadoWarPendingChoice.ModifierTarget:
                GUILayout.Label("Choose a die to modify:");
                DrawWarTargetableDice(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.ModifierSign:
                GUILayout.Label("Choose modifier:");
                if (GUILayout.Button("+1")) Act(() => warManager.TryChooseModifierValue(1));
                if (GUILayout.Button("-1")) Act(() => warManager.TryChooseModifierValue(-1));
                break;
            case CardadoWarPendingChoice.ArtistDie:
                GUILayout.Label("Choose your die to reroll:");
                DrawWarTargetableDice(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.KnightDie:
                GUILayout.Label("Choose opponent die to reroll:");
                DrawWarTargetableDice(warManager.Context.OpponentOf(warManager.PendingChoiceActor));
                break;
            case CardadoWarPendingChoice.CollectorOpponentCard:
                DrawWarOpponentCardSlots();
                break;
            case CardadoWarPendingChoice.BodyguardDie:
                GUILayout.Label("Choose your die to protect:");
                DrawWarAvailableDice(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.MirrorOwnDie:
            case CardadoWarPendingChoice.SpecialMirrorOwnDie:
                GUILayout.Label("Choose your die:");
                DrawWarTargetableDice(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.MirrorOpponentDie:
            case CardadoWarPendingChoice.SpecialMirrorOpponentDie:
                GUILayout.Label("Choose opponent die:");
                DrawWarTargetableDice(warManager.Context.OpponentOf(warManager.PendingChoiceActor));
                break;
            case CardadoWarPendingChoice.JokerPlayer:
                GUILayout.Label("Choose whose die to flip:");
                if (GUILayout.Button("Own die")) Act(() => warManager.TryChooseJokerTarget(false));
                if (GUILayout.Button("Opponent die")) Act(() => warManager.TryChooseJokerTarget(true));
                break;
            case CardadoWarPendingChoice.JokerDie:
                GUILayout.Label("Choose die to flip:");
                DrawWarTargetableDice(warManager.GetJokerTargetForDevelopment());
                break;
            case CardadoWarPendingChoice.SpecialArtistMode:
                if (GUILayout.Button("Reroll all own dice")) Act(() => warManager.TryChooseSpecialArtistMode(true));
                if (GUILayout.Button("Reroll one die 3x and choose")) Act(() => warManager.TryChooseSpecialArtistMode(false));
                break;
            case CardadoWarPendingChoice.SpecialArtistDie:
                GUILayout.Label("Choose die:");
                DrawWarTargetableDice(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.SpecialArtistResult:
                for (int i = 0; i < 3; i++)
                {
                    int result = i;
                    if (GUILayout.Button($"Keep {warManager.GetPendingArtistResult(result)}"))
                        Act(() => warManager.TryChooseSpecialArtistResult(result));
                }
                break;
            case CardadoWarPendingChoice.SpecialKnightMode:
                if (GUILayout.Button("Reroll all opponent dice")) Act(() => warManager.TryChooseSpecialKnightMode(true));
                if (GUILayout.Button("Reroll one opponent die")) Act(() => warManager.TryChooseSpecialKnightMode(false));
                break;
            case CardadoWarPendingChoice.SpecialKnightDie:
                GUILayout.Label("Choose opponent die:");
                DrawWarTargetableDice(warManager.Context.OpponentOf(warManager.PendingChoiceActor));
                break;
            case CardadoWarPendingChoice.SpecialCollectorMode:
                if (GUILayout.Button("Take one hidden card from each side, then play one")) Act(() => warManager.TryChooseSpecialCollectorMode(true));
                if (GUILayout.Button("Draw 3 cards and do not play")) Act(() => warManager.TryChooseSpecialCollectorMode(false));
                break;
            case CardadoWarPendingChoice.SpecialCollectorOwnCard:
                GUILayout.Label("Choose one own card:");
                DrawWarCardSlots(warManager.PendingChoiceActor);
                break;
            case CardadoWarPendingChoice.SpecialCollectorOpponentCard:
                GUILayout.Label("Choose one opponent card:");
                DrawWarOpponentCardSlots();
                break;
            case CardadoWarPendingChoice.SpecialCollectorPlayChoice:
                if (GUILayout.Button("Play selected own card")) Act(() => warManager.TryChooseSpecialCollectorPlayedCard(true));
                if (GUILayout.Button("Play selected opponent card")) Act(() => warManager.TryChooseSpecialCollectorPlayedCard(false));
                break;
            case CardadoWarPendingChoice.SpecialBodyguardMode:
                if (GUILayout.Button("Protect all own dice")) Act(() => warManager.TryChooseSpecialBodyguardMode(true));
                if (GUILayout.Button("Protect all dice for the hand")) Act(() => warManager.TryChooseSpecialBodyguardMode(false));
                break;
            case CardadoWarPendingChoice.SpecialMirrorMode:
                if (GUILayout.Button("Swap one own die with an opponent die")) Act(() => warManager.TryChooseSpecialMirrorMode(true));
                if (GUILayout.Button("Swap one die between the two participants")) Act(() => warManager.TryChooseSpecialMirrorMode(false));
                break;
            case CardadoWarPendingChoice.NoblemanEffect:
                GUILayout.Label("Choose the Nobleman special effect:");
                if (GUILayout.Button("Artist special")) Act(() => warManager.TryChooseNoblemanEffect(CardType.Artist));
                if (GUILayout.Button("Soldier/Knight special")) Act(() => warManager.TryChooseNoblemanEffect(CardType.Knight));
                if (GUILayout.Button("Collector special")) Act(() => warManager.TryChooseNoblemanEffect(CardType.Collector));
                if (GUILayout.Button("Bodyguard special")) Act(() => warManager.TryChooseNoblemanEffect(CardType.Bodyguard));
                break;
        }
    }

    private void DrawWarCardSlots(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.Cards.Count; i++)
        {
            int cardIndex = i;
            CardInstance card = participant.Cards[i];
            string label = card != null && card.data != null
                ? $"Card {cardIndex + 1}: {card.data.cardType} [{card.data.rarity}]"
                : $"Card slot {cardIndex + 1}";
            if (GUILayout.Button(label) && warManager.PendingChoice == CardadoWarPendingChoice.SpecialCollectorOwnCard)
                Act(() => warManager.TryChooseSpecialCollectorOwnCard(cardIndex));
        }
    }

    private void DrawWarOpponentCardSlots()
    {
        var actor = warManager.PendingChoiceActor;
        if (actor == null) return;
        var opponent = warManager.Context.OpponentOf(actor);
        if (opponent == null) return;
        for (int i = 0; i < opponent.Cards.Count; i++)
        {
            int cardIndex = i;
            if (GUILayout.Button($"Opponent card slot {cardIndex + 1}"))
            {
                if (warManager.PendingChoice == CardadoWarPendingChoice.CollectorOpponentCard)
                    Act(() => warManager.TryChooseCollectorCard(cardIndex));
                else if (warManager.PendingChoice == CardadoWarPendingChoice.SpecialCollectorOpponentCard)
                    Act(() => warManager.TryChooseSpecialCollectorOpponentCard(cardIndex));
            }
        }
    }

    private void DrawWarTargetableDice(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            int dieIndex = i;
            if (!warManager.IsWarDieTargetable(participant.PlayerIndex, dieIndex)) continue;
            if (GUILayout.Button($"Player {participant.PlayerIndex + 1} die {dieIndex + 1}: {participant.Dice[dieIndex]}"))
            {
                switch (warManager.PendingChoice)
                {
                    case CardadoWarPendingChoice.ModifierTarget: Act(() => warManager.TryChooseModifierDie(participant.PlayerIndex, dieIndex)); break;
                    case CardadoWarPendingChoice.ArtistDie: Act(() => warManager.TryChooseArtistDie(dieIndex)); break;
                    case CardadoWarPendingChoice.KnightDie: Act(() => warManager.TryChooseKnightDie(dieIndex)); break;
                    case CardadoWarPendingChoice.MirrorOwnDie: Act(() => warManager.TryChooseMirrorOwnDie(dieIndex)); break;
                    case CardadoWarPendingChoice.MirrorOpponentDie: Act(() => warManager.TryChooseMirrorOpponentDie(dieIndex)); break;
                    case CardadoWarPendingChoice.SpecialArtistDie: Act(() => warManager.TryChooseSpecialArtistDie(dieIndex)); break;
                    case CardadoWarPendingChoice.SpecialKnightDie: Act(() => warManager.TryChooseSpecialKnightDie(dieIndex)); break;
                    case CardadoWarPendingChoice.SpecialMirrorOwnDie: Act(() => warManager.TryChooseSpecialMirrorOwnDie(dieIndex)); break;
                    case CardadoWarPendingChoice.SpecialMirrorOpponentDie: Act(() => warManager.TryChooseSpecialMirrorOpponentDie(dieIndex)); break;
                    case CardadoWarPendingChoice.JokerDie: Act(() => warManager.TryChooseJokerDie(dieIndex)); break;
                }
            }
        }
    }

    private void DrawWarAvailableDice(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            int dieIndex = i;
            if (!warManager.IsWarDieAvailable(participant.PlayerIndex, dieIndex)) continue;
            if (GUILayout.Button($"Die {dieIndex + 1}: {participant.Dice[dieIndex]}"))
                Act(() => warManager.TryChooseWarBodyguardDie(dieIndex));
        }
    }

    private void DrawWarDice()
    {
        var current = warManager.Context.CurrentPlayer;
        if (current == null) return;
        GUILayout.Label($"Player {current.PlayerIndex + 1}: choose a War die");
        for (int i = 0; i < current.Dice.Count; i++)
        {
            if (!warManager.Context.IsDieAvailable(current, i)) continue;
            int die = i;
            if (GUILayout.Button($"Die {die + 1}: {current.Dice[die]}")) Act(() => gameManager.TryPlayDie(current.PlayerIndex, die));
        }
    }

    private void DrawWarComplete()
    {
        int start = lastWarChallenger >= 0
            ? (lastWarChallenger + 1) % gameManager.Players.Count
            : gameManager.StartingPlayerIndex;
        int nextClaimant = FindFirstEligibleClaimant(start);

        if (nextClaimant < 0)
        {
            Act(() => warManager.TryFinishWarPhase());
            return;
        }

        GUILayout.Label($"Player {nextClaimant + 1}: declare War or pass");

        if (GUILayout.Button("Declare War"))
        {
            bool advanced = warManager.TryContinueWarPhase();
            bool claimed = advanced && warManager.TryClaimWar(nextClaimant);
            if (claimed)
            {
                lastWarChallenger = nextClaimant;
                warWasStarted = false;
                preWarStep = PreWarStep.Target;
                activeClaimant = nextClaimant;
            }
            Act(() => true);
        }

        if (GUILayout.Button("Pass"))
        {
            if (warManager.TryContinueWarPhase())
            {
                warWasStarted = false;
                preWarStep = PreWarStep.Claim;
                activeClaimant = -1;
                claimSearchStart = (nextClaimant + 1) % gameManager.Players.Count;

                if (FindFirstEligibleClaimant(claimSearchStart) < 0)
                {
                    Act(() => warManager.TryFinishWarPhase());
                    return;
                }
            }
            Act(() => true);
        }
    }

    private void ResetWarPresentation()
    {
        preWarStep = PreWarStep.Claim;
        claimSearchStart = -1;
        activeClaimant = -1;
        selectedTarget = -1;
        lastWarChallenger = -1;
        warWasStarted = false;
    }

    private void Act(Func<bool> action)
    {
        action();
        GUIUtility.ExitGUI();
    }
}
