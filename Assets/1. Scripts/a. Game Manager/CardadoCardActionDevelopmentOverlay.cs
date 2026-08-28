using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary development UI for Cardado card actions.
/// Selections remain reversible until the final action commits the card.
/// Implements normal, modifier, special and royalty card behavior for the
/// current local development rules layer.
/// </summary>
public class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private enum Step
    {
        None, Cards, DieAfterSkip, ArtistDie,
        SoldierTarget, SoldierDie, CollectorTarget, CollectorCard,
        BodyguardDie, MirrorTarget, MirrorOwnDie, MirrorTargetDie,
        ModifierDirection, ModifierTarget, ExecutionerTarget,
        SpecialArtistChoice, SpecialArtistDie,
        SpecialSoldierChoice, SpecialSoldierTarget, SpecialSoldierDie, SpecialSoldierAllDie,
        SpecialCollectorChoice, SpecialCollectorTakeCard, SpecialCollectorPlayCard,
        SpecialBodyguardChoice, SpecialMirrorChoice, SpecialMirrorTarget,
        SpecialMirrorOwnDie, SpecialMirrorTargetDie, SpecialMirrorOpponents,
        SpecialMirrorOpponentDieA, SpecialMirrorOpponentDieB,
        SpecialExecutionerTarget, JokerTarget, JokerDie, GordonChoice
    }

    private CardadoGameManager gameManager;
    private Step step;
    private bool visible;
    private int playerIndex = -1, targetIndex = -1, ownDieIndex = -1;
    private int secondTargetIndex = -1, specialDieIndex = -1;
    private int modifierDirection;
    private int trackedHandNumber = -1;
    private CardInstance activeCard;
    private CardType gordonSpecialType;
    private bool hasGordonSpecialType;
    private int collectorTakePosition;
    private readonly List<int> collectorOpponents = new List<int>();
    private readonly List<CardInstance> collectorPool = new List<CardInstance>();
    private readonly HashSet<string> bodyguards = new HashSet<string>();
    private readonly HashSet<int> protectedPlayersThisRound = new HashSet<int>();
    private readonly Dictionary<string, int> modifierOriginal = new Dictionary<string, int>();
    private readonly HashSet<int> cardBlockedThisHand = new HashSet<int>();
    private GUIStyle panel, title, button;

    private void Awake()
    {
        gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null) return;
        gameManager.CardActionRequested += OnCardActionRequested;
        gameManager.HandTurnStarted += OnHandTurnStarted;
        gameManager.DiePlayed += OnDiePlayed;
        gameManager.RoundResolutionCompleted += OnRoundResolutionCompleted;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.CardActionRequested -= OnCardActionRequested;
        gameManager.HandTurnStarted -= OnHandTurnStarted;
        gameManager.DiePlayed -= OnDiePlayed;
        gameManager.RoundResolutionCompleted -= OnRoundResolutionCompleted;
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType request)
    {
        playerIndex = IndexOf(player);
        if (playerIndex < 0) return;

        visible = true;
        targetIndex = -1;
        secondTargetIndex = -1;
        ownDieIndex = -1;
        specialDieIndex = -1;
        modifierDirection = 0;
        activeCard = null;
        hasGordonSpecialType = false;
        step = request == CardadoCardActionRequestType.ChooseArtistDie ? Step.ArtistDie : Step.Cards;

        if (step == Step.Cards && cardBlockedThisHand.Contains(playerIndex))
        {
            visible = false;
            step = Step.None;
            gameManager.TrySkipCardAction(playerIndex);
            return;
        }

        Debug.Log($"[Cardado] CARD ACTION REQUIRED: {player.playerId} — {(step == Step.Cards ? "choose a card or skip" : "choose a die for Artist")}.");
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int hand, int starter)
    {
        if (hand != trackedHandNumber)
        {
            trackedHandNumber = hand;
            cardBlockedThisHand.Clear();
        }

        visible = false;
        step = Step.None;
        playerIndex = -1;
        targetIndex = -1;
        secondTargetIndex = -1;
        ownDieIndex = -1;
        specialDieIndex = -1;
        activeCard = null;
        collectorPool.Clear();
        collectorOpponents.Clear();
        hasGordonSpecialType = false;
    }

    private void OnRoundResolutionCompleted()
    {
        bodyguards.Clear();
        protectedPlayersThisRound.Clear();
        modifierOriginal.Clear();
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int value)
    {
        string key = Key(IndexOf(player), dieIndex);
        bodyguards.Remove(key);
        modifierOriginal.Remove(key);
    }

    private void OnGUI()
    {
        if (!visible || gameManager == null || playerIndex < 0) return;
        Styles();

        switch (step)
        {
            case Step.Cards: DrawCards(); break;
            case Step.DieAfterSkip: DrawDice("CHOOSE DIE", "You skipped the card. You can still go back before playing the die.", playerIndex, true); break;
            case Step.ArtistDie: DrawDice("ARTIST", "Choose one of your available dice to reroll.", playerIndex, true); break;
            case Step.SoldierTarget: DrawTargets("SOLDIER", "Choose an opponent.", false, true); break;
            case Step.SoldierDie: DrawDice("SOLDIER", $"Choose a die from {gameManager.Players[targetIndex].playerId} to reroll.", targetIndex, true); break;
            case Step.CollectorTarget: DrawTargets("COLLECTOR", "Choose an opponent to steal a card from.", false, true); break;
            case Step.CollectorCard: DrawCollectorCards(); break;
            case Step.BodyguardDie: DrawDice("BODYGUARD", "Choose one of your dice to protect.", playerIndex, true); break;
            case Step.MirrorTarget: DrawTargets("MIRROR", "Choose an opponent.", false, true); break;
            case Step.MirrorOwnDie: DrawDice("MIRROR", "Choose your die to exchange.", playerIndex, true); break;
            case Step.MirrorTargetDie: DrawDice("MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die to exchange.", targetIndex, true); break;
            case Step.ModifierDirection: DrawModifierDirection(); break;
            case Step.ModifierTarget: DrawModifierTarget(); break;
            case Step.ExecutionerTarget: DrawTargets("EXECUTIONER", "Choose an opponent to cancel.", false, true); break;
            case Step.SpecialArtistChoice: DrawChoice("SPECIAL ARTIST", "Choose an effect.", new[] { "REROLL ONE DIE 3 TIMES", "REROLL ALL DICE ONCE" }, ChooseSpecialArtist); break;
            case Step.SpecialArtistDie: DrawDice("SPECIAL ARTIST", "Choose one of your dice. It will be rerolled three times.", playerIndex, true); break;
            case Step.SpecialSoldierChoice: DrawChoice("SPECIAL SOLDIER", "Choose an effect.", new[] { "ONE OPPONENT REROLLS ALL DICE", "ALL OPPONENTS REROLL ONE DIE" }, ChooseSpecialSoldier); break;
            case Step.SpecialSoldierTarget: DrawTargets("SPECIAL SOLDIER", "Choose the opponent who rerolls all dice.", false, true); break;
            case Step.SpecialSoldierDie: DrawDice("SPECIAL SOLDIER", "Choose one die for the opponent to reroll.", targetIndex, true); break;
            case Step.SpecialSoldierAllDie: DrawDieIndexForAllOpponents(); break;
            case Step.SpecialCollectorChoice: DrawChoice("SPECIAL COLLECTOR", "Choose an effect.", new[] { "TAKE ONE CARD FROM EACH PLAYER", "DRAW 3 CARDS" }, ChooseSpecialCollector); break;
            case Step.SpecialCollectorTakeCard: DrawSpecialCollectorTakeCard(); break;
            case Step.SpecialCollectorPlayCard: DrawSpecialCollectorPlayCard(); break;
            case Step.SpecialBodyguardChoice: DrawChoice("SPECIAL BODYGUARD", "Choose an effect.", new[] { "PROTECT ALL YOUR DICE", "PROTECT YOURSELF FOR THE ROUND" }, ChooseSpecialBodyguard); break;
            case Step.SpecialMirrorChoice: DrawChoice("SPECIAL MIRROR", "Choose an effect.", new[] { "EXCHANGE YOUR DIE WITH AN OPPONENT", "EXCHANGE DICE BETWEEN TWO OPPONENTS" }, ChooseSpecialMirror); break;
            case Step.SpecialMirrorTarget: DrawTargets("SPECIAL MIRROR", "Choose the opponent whose die you want to exchange with yours.", false, true); break;
            case Step.SpecialMirrorOwnDie: DrawDice("SPECIAL MIRROR", "Choose your die to exchange.", playerIndex, true); break;
            case Step.SpecialMirrorTargetDie: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die.", targetIndex, true); break;
            case Step.SpecialMirrorOpponents: DrawTwoOpponentTargets(); break;
            case Step.SpecialMirrorOpponentDieA: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die.", targetIndex, true); break;
            case Step.SpecialMirrorOpponentDieB: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[secondTargetIndex].playerId}'s die.", secondTargetIndex, true); break;
            case Step.SpecialExecutionerTarget: DrawTargets("SPECIAL EXECUTIONER", "Choose a player who must discard their entire hand.", false, true); break;
            case Step.JokerTarget: DrawTargets("JOKER", "Choose whose die to flip.", true, true); break;
            case Step.JokerDie: DrawDice("JOKER", "Choose the die to flip to its opposite face.", targetIndex, true); break;
            case Step.GordonChoice: DrawChoice("GORDON ROBLEYS", "Choose which Special card this acts as.", new[] { "SPECIAL ARTIST", "SPECIAL SOLDIER", "SPECIAL COLLECTOR", "SPECIAL BODYGUARD" }, ChooseGordon); break;
        }
    }

    private void DrawCards()
    {
        CardadoPlayerState p = gameManager.Players[playerIndex];
        Rect r = Box(900, 430);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), $"{p.playerId} — CARD ACTION", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 850, 30), "Play a card before choosing a die, or skip.", GUI.skin.label);

        List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand);
        float bw = 145, gap = 12;
        float total = cards.Count * bw + Mathf.Max(0, cards.Count - 1) * gap;
        float x = r.x + (900 - total) * .5f;
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance c = cards[i];
            if (c == null || c.data == null) continue;
            if (GUI.Button(new Rect(x + i * (bw + gap), r.y + 120, bw, 90),
                c.data.id + "\n" + c.data.cardType + (c.data.isBlankCard ? "\nBlank" : ""), button))
                SelectCard(c);
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 320, 850, 55), "SKIP CARD ACTION", button))
            SkipCardAction();
    }

    private void DrawDice(string heading, string text, int p, bool allowBack)
    {
        if (p < 0 || p >= gameManager.Players.Count) return;
        CardadoPlayerState state = gameManager.Players[p];
        Rect r = Box(760, allowBack ? 390 : 310);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 710, 45), $"{state.playerId} — {heading}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 710, 55), text, GUI.skin.label);

        for (int d = 0; d < state.dice.Count; d++)
        {
            if (!gameManager.IsDieAvailable(p, d)) continue;
            if (GUI.Button(new Rect(r.x + 25 + d * 105, r.y + 135, 90, 70), $"Die {d + 1}\n{state.dice[d]}", button))
                DieChosen(d);
        }

        if (allowBack && GUI.Button(new Rect(r.x + 25, r.y + 285, 710, 55), "BACK", button))
            BackFromDieSelection();
    }

    private void DrawTargets(string heading, string text, bool self, bool showBack)
    {
        Rect r = Box(850, 370);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), heading, title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), text, GUI.skin.label);
        float x = r.x + 25;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (!self && i == playerIndex) continue;
            CardadoPlayerState p = gameManager.Players[i];
            GUI.enabled = i != playerIndex && !protectedPlayersThisRound.Contains(i);
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), $"{p.playerId}\nChips: {p.chips}", button))
                TargetChosen(i);
            GUI.enabled = true;
            x += 160;
        }
        if (showBack && GUI.Button(new Rect(r.x + 25, r.y + 270, 800, 55), "BACK", button))
            BackFromTargetSelection();
    }

    private void DrawCollectorCards()
    {
        CardadoPlayerState p = gameManager.Players[targetIndex];
        Rect r = Box(820, 430);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), $"COLLECTOR — STEAL FROM {p.playerId}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 770, 35), "Choose a card. The card is revealed only when selected.", GUI.skin.label);
        List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand);
        float x = r.x + 25, y = r.y + 125;
        for (int i = 0; i < cards.Count; i++)
        {
            if (GUI.Button(new Rect(x, y, 145, 70), $"CARD {i + 1}", button)) CollectorCard(cards[i]);
            x += 160;
            if (x > r.x + 650) { x = r.x + 25; y += 85; }
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 345, 770, 55), "BACK", button)) BackFromTargetSelection();
    }

    private void DrawModifierDirection()
    {
        Rect r = Box(700, 360);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 650, 45), "MODIFIER", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 650, 30), "Choose +1 or -1 first. You can still go back.", GUI.skin.label);
        if (GUI.Button(new Rect(r.x + 50, r.y + 125, 280, 70), "+1", button)) { modifierDirection = 1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 370, r.y + 125, 280, 70), "-1", button)) { modifierDirection = -1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 50, r.y + 225, 600, 55), "BACK", button)) BackToCards();
    }

    private void DrawModifierTarget()
    {
        Rect r = Box(900, 520);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), $"MODIFIER {modifierDirection:+#;-#} — CHOOSE DIE", title);
        GUI.Label(new Rect(r.x + 25, r.y + 65, 850, 30), "Selecting the die applies the modifier and commits the card.", GUI.skin.label);
        float y = r.y + 105;
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            CardadoPlayerState state = gameManager.Players[p];
            GUI.Label(new Rect(r.x + 25, y, 140, 30), state.playerId, GUI.skin.label);
            float x = r.x + 170;
            for (int d = 0; d < state.dice.Count; d++)
            {
                if (!gameManager.IsDieAvailable(p, d)) continue;
                bool valid = (modifierDirection == 1 && state.dice[d] < 6) || (modifierDirection == -1 && state.dice[d] > 1);
                valid &= !protectedPlayersThisRound.Contains(p) || p == playerIndex;
                valid &= !IsProtected(p, d) || p == playerIndex;
                GUI.enabled = valid;
                if (GUI.Button(new Rect(x, y - 5, 90, 60), $"Die {d + 1}\n{state.dice[d]}", button)) ModifierChosen(p, d);
                GUI.enabled = true;
                x += 105;
            }
            y += 80;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 455, 850, 50), "BACK", button)) step = Step.ModifierDirection;
    }

    private void DrawChoice(string heading, string text, string[] options, Action<int> choose)
    {
        Rect r = Box(820, 390);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), heading, title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 770, 40), text, GUI.skin.label);
        for (int i = 0; i < options.Length; i++)
            if (GUI.Button(new Rect(r.x + 40, r.y + 125 + i * 75, 740, 60), options[i], button)) choose(i);
        if (GUI.Button(new Rect(r.x + 40, r.y + 285, 740, 55), "BACK", button)) BackToCards();
    }

    private void DrawDieIndexForAllOpponents()
    {
        Rect r = Box(760, 390);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 710, 45), "SPECIAL SOLDIER — CHOOSE DIE", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 710, 35), "That die index will be rerolled for every opponent who still has it.", GUI.skin.label);
        int maxDice = gameManager.RoundDiceCount;
        for (int d = 0; d < maxDice; d++)
        {
            bool available = false;
            for (int p = 0; p < gameManager.Players.Count; p++) if (p != playerIndex && gameManager.IsDieAvailable(p, d)) available = true;
            GUI.enabled = available;
            if (GUI.Button(new Rect(r.x + 25 + d * 105, r.y + 135, 90, 70), $"Die {d + 1}", button)) SpecialSoldierAllDieChosen(d);
            GUI.enabled = true;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 285, 710, 55), "BACK", button)) step = Step.SpecialSoldierChoice;
    }

    private void DrawSpecialCollectorTakeCard()
    {
        if (collectorTakePosition >= collectorOpponents.Count) { step = Step.SpecialCollectorPlayCard; return; }
        int target = collectorOpponents[collectorTakePosition];
        CardadoPlayerState p = gameManager.Players[target];
        Rect r = Box(820, 400);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), $"SPECIAL COLLECTOR — TAKE FROM {p.playerId}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 770, 45), "Cards stay hidden until taken. Choose a card position.", GUI.skin.label);
        int count = p.hand.cardsInHand.Count;
        for (int i = 0; i < count; i++)
            if (GUI.Button(new Rect(r.x + 25 + i * 160, r.y + 135, 145, 70), $"CARD {i + 1}", button)) TakeSpecialCollectorCard(i);
        if (count == 0) { GUI.Label(new Rect(r.x + 25, r.y + 150, 700, 35), "No cards to take from this player.", GUI.skin.label); }
        if (GUI.Button(new Rect(r.x + 25, r.y + 285, 770, 55), "BACK", button)) step = Step.SpecialCollectorChoice;
    }

    private void DrawSpecialCollectorPlayCard()
    {
        Rect r = Box(850, 430);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), "SPECIAL COLLECTOR — CHOOSE CARD TO PLAY", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), "Choose one stolen card to play immediately. The others are discarded.", GUI.skin.label);
        for (int i = 0; i < collectorPool.Count; i++)
        {
            CardInstance c = collectorPool[i];
            if (GUI.Button(new Rect(r.x + 25 + i * 165, r.y + 125, 150, 80), c.data.id + "\n" + c.data.cardType, button)) PlaySpecialCollectorCard(i);
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 330, 800, 55), "BACK", button)) step = Step.SpecialCollectorTakeCard;
    }

    private void DrawTwoOpponentTargets()
    {
        Rect r = Box(850, 380);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), "SPECIAL MIRROR — TWO OPPONENTS", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), "Choose the first opponent.", GUI.skin.label);
        float x = r.x + 25;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == playerIndex) continue;
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), $"{gameManager.Players[i].playerId}\nChips: {gameManager.Players[i].chips}", button)) { targetIndex = i; step = Step.SpecialMirrorOpponentDieA; }
            x += 160;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 270, 800, 55), "BACK", button)) step = Step.SpecialMirrorChoice;
    }

    private void SelectCard(CardInstance card)
    {
        if (card == null || card.data == null) return;
        activeCard = card;
        targetIndex = -1;
        secondTargetIndex = -1;
        ownDieIndex = -1;
        specialDieIndex = -1;
        modifierDirection = 0;
        hasGordonSpecialType = false;

        if (card.data.isBlankCard) { CommitSimpleCard(); return; }
        if (card.data.isModifier) { step = Step.ModifierDirection; return; }

        if (card.data.rarity == CardRarity.Special) { RouteSpecialCard(card.data.cardType); return; }
        if (card.data.rarity == CardRarity.Royalty) { RouteRoyaltyCard(card.data.cardType); return; }

        if (card.data.cardType == CardType.Artist) { step = Step.ArtistDie; return; }
        switch (card.data.cardType)
        {
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Collector: step = Step.CollectorTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: CommitSimpleCard(); break;
        }
    }

    private void RouteSpecialCard(CardType type)
    {
        switch (type)
        {
            case CardType.Artist: step = Step.SpecialArtistChoice; break;
            case CardType.Knight: step = Step.SpecialSoldierChoice; break;
            case CardType.Collector: step = Step.SpecialCollectorChoice; break;
            case CardType.Bodyguard: step = Step.SpecialBodyguardChoice; break;
            case CardType.Mirror: step = Step.SpecialMirrorChoice; break;
            case CardType.Executioner: step = Step.SpecialExecutionerTarget; break;
            default: CommitSimpleCard(); break;
        }
    }

    private void RouteRoyaltyCard(CardType type)
    {
        switch (type)
        {
            case CardType.Joker: step = Step.JokerTarget; break;
            case CardType.Queen: ResolveQueen(); break;
            case CardType.King: ResolveKing(); break;
            case CardType.GordonRobleys: step = Step.GordonChoice; break;
            default: CommitSimpleCard(); break;
        }
    }

    private void SkipCardAction()
    {
        activeCard = null; targetIndex = -1; secondTargetIndex = -1; ownDieIndex = -1; modifierDirection = 0; step = Step.DieAfterSkip;
    }

    private void BackToCards()
    {
        activeCard = null; targetIndex = -1; secondTargetIndex = -1; ownDieIndex = -1; specialDieIndex = -1; modifierDirection = 0; hasGordonSpecialType = false; collectorPool.Clear(); collectorOpponents.Clear(); step = Step.Cards;
    }

    private void BackFromTargetSelection()
    {
        if (step == Step.SoldierTarget || step == Step.CollectorTarget || step == Step.MirrorTarget || step == Step.ExecutionerTarget || step == Step.SpecialSoldierTarget || step == Step.SpecialMirrorTarget || step == Step.SpecialExecutionerTarget || step == Step.JokerTarget) { BackToCards(); return; }
        if (step == Step.SoldierDie) { targetIndex = -1; step = Step.SoldierTarget; return; }
        if (step == Step.CollectorCard) { targetIndex = -1; step = Step.CollectorTarget; return; }
        if (step == Step.MirrorOwnDie) { targetIndex = -1; step = Step.MirrorTarget; return; }
        if (step == Step.SpecialSoldierTarget) { step = Step.SpecialSoldierChoice; return; }
        if (step == Step.SpecialMirrorTarget) { step = Step.SpecialMirrorChoice; return; }
        if (step == Step.SpecialExecutionerTarget) { BackToCards(); return; }
        if (step == Step.JokerTarget) { BackToCards(); return; }
    }

    private void BackFromDieSelection()
    {
        if (step == Step.DieAfterSkip || step == Step.ArtistDie || step == Step.BodyguardDie || step == Step.SpecialArtistDie || step == Step.SpecialSoldierDie || step == Step.SpecialMirrorOwnDie || step == Step.SpecialMirrorTargetDie || step == Step.SpecialMirrorOpponentDieA || step == Step.SpecialMirrorOpponentDieB || step == Step.JokerDie) { BackToCards(); return; }
        if (step == Step.SoldierDie) { targetIndex = -1; step = Step.SoldierTarget; return; }
        if (step == Step.MirrorOwnDie) { targetIndex = -1; step = Step.MirrorTarget; return; }
        if (step == Step.MirrorTargetDie) { ownDieIndex = -1; step = Step.MirrorOwnDie; return; }
        if (step == Step.SpecialSoldierAllDie) { step = Step.SpecialSoldierChoice; return; }
    }

    private void TargetChosen(int target)
    {
        targetIndex = target;
        switch (step)
        {
            case Step.SoldierTarget: step = Step.SoldierDie; break;
            case Step.CollectorTarget: step = Step.CollectorCard; break;
            case Step.MirrorTarget: step = Step.MirrorOwnDie; break;
            case Step.ExecutionerTarget: Executioner(target); break;
            case Step.SpecialSoldierTarget: ResolveSpecialSoldierAll(target); break;
            case Step.SpecialMirrorTarget: step = Step.SpecialMirrorOwnDie; break;
            case Step.SpecialExecutionerTarget: ResolveSpecialExecutioner(target); break;
            case Step.JokerTarget: step = Step.JokerDie; break;
        }
    }

    private void DieChosen(int die)
    {
        switch (step)
        {
            case Step.DieAfterSkip:
                int skippedPlayerIndex = playerIndex;
                if (!gameManager.TrySkipCardAction(skippedPlayerIndex)) return;
                gameManager.TryPlayDie(skippedPlayerIndex, die);
                break;
            case Step.ArtistDie: CommitReroll(playerIndex, die, 1); break;
            case Step.SoldierDie: if (!CanAffect(targetIndex, die)) return; CommitReroll(targetIndex, die, 1); CommitActiveCardAndContinue(); break;
            case Step.BodyguardDie: bodyguards.Add(Key(playerIndex, die)); CommitActiveCardAndContinue(); break;
            case Step.MirrorOwnDie: ownDieIndex = die; step = Step.MirrorTargetDie; break;
            case Step.MirrorTargetDie: ExchangeDice(playerIndex, ownDieIndex, targetIndex, die); CommitActiveCardAndContinue(); break;
            case Step.SpecialArtistDie: CommitReroll(playerIndex, die, 3); break;
            case Step.SpecialSoldierDie: if (!CanAffect(targetIndex, die)) return; CommitReroll(targetIndex, die, 1); CommitActiveCardAndContinue(); break;
            case Step.SpecialMirrorOwnDie: ownDieIndex = die; step = Step.SpecialMirrorTargetDie; break;
            case Step.SpecialMirrorTargetDie: ExchangeDice(playerIndex, ownDieIndex, targetIndex, die); CommitActiveCardAndContinue(); break;
            case Step.SpecialMirrorOpponentDieA: specialDieIndex = die; secondTargetIndex = secondTargetIndex < 0 ? secondTargetIndex : secondTargetIndex; step = Step.SpecialMirrorOpponentDieB; break;
            case Step.SpecialMirrorOpponentDieB: ExchangeDice(targetIndex, specialDieIndex, secondTargetIndex, die); CommitActiveCardAndContinue(); break;
            case Step.JokerDie: ResolveJokerDie(die); break;
        }
    }

    private void DrawSpecialMirrorOpponentsAfterFirstDie(int firstDie)
    {
        specialDieIndex = firstDie;
        step = Step.SpecialMirrorOpponentDieB;
    }

    private void DrawSpecialMirrorOpponentDieB() { }

    private void CollectorCard(CardInstance stolen)
    {
        if (stolen == null || activeCard == null) return;
        CardadoPlayerState target = gameManager.Players[targetIndex];
        target.hand.RemoveCard(stolen);
        stolen.isPlayed = true;
        CardInstance collector = activeCard;
        collector.isPlayed = true;
        gameManager.Players[playerIndex].hand.RemoveCard(collector);
        gameManager.DiscardResolvedCard(collector);
        activeCard = stolen;
        if (stolen.data.isBlankCard) { gameManager.DiscardResolvedCard(stolen); FinishCommittedCard(); return; }
        if (stolen.data.isModifier) { step = Step.ModifierDirection; return; }
        if (stolen.data.rarity == CardRarity.Special) { RouteSpecialCard(stolen.data.cardType); return; }
        if (stolen.data.rarity == CardRarity.Royalty) { RouteRoyaltyCard(stolen.data.cardType); return; }
        switch (stolen.data.cardType)
        {
            case CardType.Artist: step = Step.ArtistDie; break;
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: FinishCommittedCard(); break;
        }
    }

    private void ModifierChosen(int p, int d)
    {
        if (!gameManager.IsDieAvailable(p, d)) return;
        int current = gameManager.Players[p].dice[d];
        if (modifierDirection == 1 && current >= 6) return;
        if (modifierDirection == -1 && current <= 1) return;
        if (!CanAffect(p, d)) return;
        string key = Key(p, d);
        if (modifierOriginal.ContainsKey(key)) return;
        modifierOriginal[key] = current;
        gameManager.Players[p].dice[d] = current + modifierDirection;
        CommitActiveCardAndContinue();
    }

    private void Executioner(int target)
    {
        bool cancelled = false;
        CardadoPlayerState p = gameManager.Players[target];
        for (int d = 0; d < p.dice.Count; d++)
        {
            string key = Key(target, d);
            if (modifierOriginal.ContainsKey(key)) { p.dice[d] = modifierOriginal[key]; modifierOriginal.Remove(key); cancelled = true; }
            if (bodyguards.Remove(key)) cancelled = true;
        }
        if (!cancelled) cardBlockedThisHand.Add(target);
        CommitActiveCardAndContinue();
    }

    private void ChooseSpecialArtist(int option) { step = option == 0 ? Step.SpecialArtistDie : Step.SpecialArtistAllDice; }

    private void DrawSpecialArtistAllDice() { }

    private void ResolveSpecialArtistAllDice()
    {
        CardadoPlayerState p = gameManager.Players[playerIndex];
        for (int d = 0; d < p.dice.Count; d++) if (gameManager.IsDieAvailable(playerIndex, d)) p.dice[d] = UnityEngine.Random.Range(1, 7);
        CommitActiveCardAndContinue();
    }

    private void ChooseSpecialSoldier(int option)
    {
        step = option == 0 ? Step.SpecialSoldierTarget : Step.SpecialSoldierAllDie;
    }

    private void ResolveSpecialSoldierAll(int target)
    {
        if (!CanAffectPlayer(target)) return;
        CardadoPlayerState p = gameManager.Players[target];
        for (int d = 0; d < p.dice.Count; d++) if (gameManager.IsDieAvailable(target, d) && !IsProtected(target, d)) p.dice[d] = UnityEngine.Random.Range(1, 7);
        CommitActiveCardAndContinue();
    }

    private void SpecialSoldierAllDieChosen(int dieIndex)
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == playerIndex || !CanAffectPlayer(p)) continue;
            if (gameManager.IsDieAvailable(p, dieIndex) && !IsProtected(p, dieIndex)) gameManager.Players[p].dice[dieIndex] = UnityEngine.Random.Range(1, 7);
        }
        CommitActiveCardAndContinue();
    }

    private void ChooseSpecialCollector(int option)
    {
        if (option == 1)
        {
            CommitCardOnly();
            for (int i = 0; i < 3; i++)
            {
                CardInstance drawn = gameManager.RoundDeck.Draw();
                if (drawn == null) break;
                gameManager.Players[playerIndex].hand.AddCard(drawn);
            }
            FinishCommittedCard();
            return;
        }

        collectorPool.Clear(); collectorOpponents.Clear(); collectorTakePosition = 0;
        for (int p = 0; p < gameManager.Players.Count; p++) if (p != playerIndex && !protectedPlayersThisRound.Contains(p) && gameManager.Players[p].hand.cardsInHand.Count > 0) collectorOpponents.Add(p);
        step = collectorOpponents.Count == 0 ? Step.SpecialCollectorPlayCard : Step.SpecialCollectorTakeCard;
    }

    private void TakeSpecialCollectorCard(int cardIndex)
    {
        int target = collectorOpponents[collectorTakePosition];
        CardadoPlayerState p = gameManager.Players[target];
        if (cardIndex < 0 || cardIndex >= p.hand.cardsInHand.Count) return;
        CardInstance card = p.hand.cardsInHand[cardIndex];
        p.hand.RemoveCard(card);
        collectorPool.Add(card);
        collectorTakePosition++;
        if (collectorTakePosition >= collectorOpponents.Count) step = Step.SpecialCollectorPlayCard;
    }

    private void PlaySpecialCollectorCard(int index)
    {
        if (index < 0 || index >= collectorPool.Count) return;
        CardInstance chosen = collectorPool[index];
        for (int i = collectorPool.Count - 1; i >= 0; i--)
        {
            if (i == index) continue;
            gameManager.DiscardResolvedCard(collectorPool[i]);
        }
        collectorPool.Clear();
        CommitCardOnly();
        activeCard = chosen;
        chosen.isPlayed = true;
        RouteStolenCard(chosen);
    }

    private void RouteStolenCard(CardInstance card)
    {
        if (card.data.isBlankCard) { gameManager.DiscardResolvedCard(card); FinishCommittedCard(); return; }
        if (card.data.isModifier) { step = Step.ModifierDirection; return; }
        if (card.data.rarity == CardRarity.Special) { RouteSpecialCard(card.data.cardType); return; }
        if (card.data.rarity == CardRarity.Royalty) { RouteRoyaltyCard(card.data.cardType); return; }
        switch (card.data.cardType)
        {
            case CardType.Artist: step = Step.ArtistDie; break;
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Collector: step = Step.CollectorTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: gameManager.DiscardResolvedCard(card); FinishCommittedCard(); break;
        }
    }

    private void ChooseSpecialBodyguard(int option)
    {
        if (option == 0)
        {
            for (int d = 0; d < gameManager.Players[playerIndex].dice.Count; d++) if (gameManager.IsDieAvailable(playerIndex, d)) bodyguards.Add(Key(playerIndex, d));
        }
        else protectedPlayersThisRound.Add(playerIndex);
        CommitActiveCardAndContinue();
    }

    private void ChooseSpecialMirror(int option)
    {
        step = option == 0 ? Step.SpecialMirrorTarget : Step.SpecialMirrorOpponents;
    }

    private void DrawSpecialMirrorOpponentsUnused() { }

    private void ResolveSpecialExecutioner(int target)
    {
        if (!CanAffectPlayer(target)) return;
        CardadoPlayerState p = gameManager.Players[target];
        List<CardInstance> discarded = new List<CardInstance>(p.hand.cardsInHand);
        p.hand.cardsInHand.Clear();
        foreach (CardInstance c in discarded) gameManager.DiscardResolvedCard(c);
        Debug.Log($"[Cardado] Special Executioner: {p.playerId} discarded {discarded.Count} card(s).");
        CommitActiveCardAndContinue();
    }

    private void ResolveJokerDie(int die)
    {
        if (!CanAffect(targetIndex, die)) return;
        int v = gameManager.Players[targetIndex].dice[die];
        gameManager.Players[targetIndex].dice[die] = v switch { 1 => 6, 2 => 5, 3 => 4, 4 => 3, 5 => 2, 6 => 1, _ => v };
        CommitActiveCardAndContinue();
    }

    private void ChooseGordon(int option)
    {
        hasGordonSpecialType = true;
        gordonSpecialType = option switch { 0 => CardType.Artist, 1 => CardType.Knight, 2 => CardType.Collector, _ => CardType.Bodyguard };
        RouteSpecialCard(gordonSpecialType);
    }

    private void ResolveQueen()
    {
        CommitCardOnly();
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            List<CardInstance> old = new List<CardInstance>(gameManager.Players[p].hand.cardsInHand);
            gameManager.Players[p].hand.cardsInHand.Clear();
            foreach (CardInstance c in old) gameManager.DiscardResolvedCard(c);
        }
        for (int p = 0; p < gameManager.Players.Count; p++)
            for (int i = 0; i < 3; i++)
            {
                CardInstance c = gameManager.RoundDeck.Draw();
                if (c == null) break;
                gameManager.Players[p].hand.AddCard(c);
            }
        BeginAnotherCardOrDie();
    }

    private void ResolveKing()
    {
        CommitCardOnly();
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (protectedPlayersThisRound.Contains(p) && p != playerIndex) continue;
            for (int d = 0; d < gameManager.Players[p].dice.Count; d++)
                if (gameManager.IsDieAvailable(p, d) && !IsProtected(p, d)) gameManager.Players[p].dice[d] = UnityEngine.Random.Range(1, 7);
        }
        BeginAnotherCardOrDie();
    }

    private void CommitReroll(int p, int die, int times)
    {
        if (!CanAffect(p, die)) return;
        for (int i = 0; i < times; i++) gameManager.Players[p].dice[die] = UnityEngine.Random.Range(1, 7);
        CommitActiveCardAndContinue();
    }

    private void ExchangeDice(int a, int ad, int b, int bd)
    {
        if (!CanAffect(b, bd)) return;
        int v = gameManager.Players[a].dice[ad];
        gameManager.Players[a].dice[ad] = gameManager.Players[b].dice[bd];
        gameManager.Players[b].dice[bd] = v;
    }

    private bool CanAffect(int p, int d)
    {
        return gameManager.IsDieAvailable(p, d) && CanAffectPlayer(p) && !IsProtected(p, d);
    }

    private bool CanAffectPlayer(int p) => p >= 0 && p < gameManager.Players.Count && (p == playerIndex || !protectedPlayersThisRound.Contains(p));
    private bool IsProtected(int p, int d) => bodyguards.Contains(Key(p, d));
    private string Key(int p, int d) => p + ":" + d;

    private void CommitSimpleCard()
    {
        CommitCardOnly();
        FinishCommittedCard();
    }

    private void CommitCardOnly()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true;
        gameManager.Players[playerIndex].hand.RemoveCard(activeCard);
        gameManager.DiscardResolvedCard(activeCard);
    }

    private void CommitActiveCardAndContinue()
    {
        CommitCardOnly();
        FinishCommittedCard();
    }

    private void FinishCommittedCard()
    {
        activeCard = null;
        visible = false;
        step = Step.None;
        targetIndex = -1;
        secondTargetIndex = -1;
        ownDieIndex = -1;
        specialDieIndex = -1;
        modifierDirection = 0;
        hasGordonSpecialType = false;
        gameManager.TrySkipCardAction(playerIndex);
    }

    private void BeginAnotherCardOrDie()
    {
        activeCard = null;
        targetIndex = -1;
        secondTargetIndex = -1;
        ownDieIndex = -1;
        specialDieIndex = -1;
        hasGordonSpecialType = false;
        if (gameManager.Players[playerIndex].hand.cardsInHand.Count > 0)
        {
            visible = true;
            step = Step.Cards;
            Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} may play another card after the royalty effect.");
        }
        else
        {
            visible = false;
            gameManager.TrySkipCardAction(playerIndex);
        }
    }

    private int IndexOf(CardadoPlayerState p)
    {
        for (int i = 0; i < gameManager.Players.Count; i++) if (gameManager.Players[i] == p) return i;
        return -1;
    }

    private Rect Box(float w, float h) => new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);

    private void Styles()
    {
        if (panel != null) return;
        panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
    }
}
