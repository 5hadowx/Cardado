using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporary development UI for the complete card-action pipeline.</summary>
public class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private enum Step
    {
        None, Cards, DieAfterSkip, ArtistDie, SoldierTarget, SoldierDie, CollectorTarget, CollectorCard,
        BodyguardDie, MirrorTarget, MirrorOwnDie, MirrorTargetDie, ModifierDirection, ModifierTarget,
        ExecutionerTarget, SpecialArtistDie, SpecialSoldierChoice, SpecialSoldierTarget, SpecialSoldierDie,
        SpecialSoldierAllDie, SpecialCollectorChoice, SpecialCollectorTake, SpecialCollectorPlay,
        SpecialBodyguardChoice, SpecialMirrorChoice, SpecialMirrorTarget, SpecialMirrorOwnDie,
        SpecialMirrorTargetDie, SpecialMirrorFirstOpponent, SpecialMirrorFirstDie, SpecialMirrorSecondOpponent,
        SpecialMirrorSecondDie, SpecialExecutionerTarget, JokerTarget, JokerDie, GordonChoice
    }

    private CardadoGameManager gameManager;
    private Step step;
    private bool visible;
    private int playerIndex = -1, targetIndex = -1, secondTargetIndex = -1, ownDieIndex = -1;
    private int modifierDirection;
    private int trackedHand = -1;
    private CardInstance activeCard;
    private readonly HashSet<string> bodyguards = new HashSet<string>();
    private readonly HashSet<int> protectedPlayersThisRound = new HashSet<int>();
    private readonly Dictionary<string, int> modifierOriginal = new Dictionary<string, int>();
    private readonly HashSet<int> cardBlockedThisHand = new HashSet<int>();
    private readonly List<int> collectorOpponents = new List<int>();
    private readonly List<CardInstance> collectorPool = new List<CardInstance>();
    private int collectorPosition;
    private GUIStyle panel, title, button;

    private void Awake()
    {
        gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager == null) return;
        gameManager.CardActionRequested += OnCardActionRequested;
        gameManager.HandTurnStarted += OnHandTurnStarted;
        gameManager.DiePlayed += OnDiePlayed;
        gameManager.RoundResolutionCompleted += ClearRoundEffects;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.CardActionRequested -= OnCardActionRequested;
        gameManager.HandTurnStarted -= OnHandTurnStarted;
        gameManager.DiePlayed -= OnDiePlayed;
        gameManager.RoundResolutionCompleted -= ClearRoundEffects;
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType request)
    {
        playerIndex = IndexOf(player);
        if (playerIndex < 0) return;
        visible = true;
        targetIndex = secondTargetIndex = ownDieIndex = -1;
        modifierDirection = 0;
        activeCard = null;
        step = request == CardadoCardActionRequestType.ChooseArtistDie ? Step.ArtistDie : Step.Cards;
        if (step == Step.Cards && cardBlockedThisHand.Contains(playerIndex))
        {
            visible = false;
            step = Step.None;
            gameManager.TrySkipCardAction(playerIndex);
            return;
        }
        Debug.Log($"[Cardado] CARD ACTION REQUIRED: {player.playerId}.");
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int hand, int starter)
    {
        if (hand != trackedHand) { trackedHand = hand; cardBlockedThisHand.Clear(); }
        visible = false; step = Step.None; playerIndex = targetIndex = secondTargetIndex = ownDieIndex = -1; activeCard = null;
        collectorPool.Clear(); collectorOpponents.Clear();
    }

    private void ClearRoundEffects()
    {
        bodyguards.Clear(); protectedPlayersThisRound.Clear(); modifierOriginal.Clear();
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int value)
    {
        string key = Key(IndexOf(player), dieIndex);
        bodyguards.Remove(key); modifierOriginal.Remove(key);
    }

    private void OnGUI()
    {
        if (!visible || gameManager == null || playerIndex < 0) return;
        Styles();
        switch (step)
        {
            case Step.Cards: DrawCards(); break;
            case Step.DieAfterSkip: DrawDice("CHOOSE DIE", "You skipped the card. You can still go back.", playerIndex, true); break;
            case Step.ArtistDie: DrawDice("ARTIST", "Choose one of your dice to reroll.", playerIndex, true); break;
            case Step.SoldierTarget: DrawTargets("SOLDIER", "Choose an opponent.", false, Step.Cards); break;
            case Step.SoldierDie: DrawDice("SOLDIER", $"Choose a die from {gameManager.Players[targetIndex].playerId}.", targetIndex, true); break;
            case Step.CollectorTarget: DrawTargets("COLLECTOR", "Choose an opponent.", false, Step.Cards); break;
            case Step.CollectorCard: DrawCollectorCards(); break;
            case Step.BodyguardDie: DrawDice("BODYGUARD", "Choose one of your dice to protect.", playerIndex, true); break;
            case Step.MirrorTarget: DrawTargets("MIRROR", "Choose an opponent.", false, Step.Cards); break;
            case Step.MirrorOwnDie: DrawDice("MIRROR", "Choose your die to exchange.", playerIndex, true); break;
            case Step.MirrorTargetDie: DrawDice("MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die.", targetIndex, true); break;
            case Step.ModifierDirection: DrawModifierDirection(); break;
            case Step.ModifierTarget: DrawModifierTarget(); break;
            case Step.ExecutionerTarget: DrawTargets("EXECUTIONER", "Choose an opponent to cancel.", false, Step.Cards); break;
            case Step.SpecialArtistDie: DrawDice("SPECIAL ARTIST", "This die will be rerolled three times.", playerIndex, true); break;
            case Step.SpecialSoldierChoice: DrawChoice("SPECIAL SOLDIER", new[] { "ONE OPPONENT REROLLS ALL DICE", "ALL OPPONENTS REROLL ONE DIE" }, ChooseSpecialSoldier, Step.Cards); break;
            case Step.SpecialSoldierTarget: DrawTargets("SPECIAL SOLDIER", "Choose the opponent.", false, Step.SpecialSoldierChoice); break;
            case Step.SpecialSoldierDie: DrawDice("SPECIAL SOLDIER", $"Choose a die from {gameManager.Players[targetIndex].playerId}.", targetIndex, true); break;
            case Step.SpecialSoldierAllDie: DrawAllOpponentDieChoice(); break;
            case Step.SpecialCollectorChoice: DrawChoice("SPECIAL COLLECTOR", new[] { "TAKE ONE CARD FROM EACH PLAYER", "DRAW 3 CARDS" }, ChooseSpecialCollector, Step.Cards); break;
            case Step.SpecialCollectorTake: DrawSpecialCollectorTake(); break;
            case Step.SpecialCollectorPlay: DrawSpecialCollectorPlay(); break;
            case Step.SpecialBodyguardChoice: DrawChoice("SPECIAL BODYGUARD", new[] { "PROTECT ALL YOUR DICE", "PROTECT YOURSELF FOR THE ROUND" }, ChooseSpecialBodyguard, Step.Cards); break;
            case Step.SpecialMirrorChoice: DrawChoice("SPECIAL MIRROR", new[] { "EXCHANGE YOUR DIE WITH AN OPPONENT", "EXCHANGE TWO OPPONENTS' DICE" }, ChooseSpecialMirror, Step.Cards); break;
            case Step.SpecialMirrorTarget: DrawTargets("SPECIAL MIRROR", "Choose an opponent.", false, Step.SpecialMirrorChoice); break;
            case Step.SpecialMirrorOwnDie: DrawDice("SPECIAL MIRROR", "Choose your die.", playerIndex, true); break;
            case Step.SpecialMirrorTargetDie: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die.", targetIndex, true); break;
            case Step.SpecialMirrorFirstOpponent: DrawOpponentPair("SPECIAL MIRROR", "Choose the first opponent.", true); break;
            case Step.SpecialMirrorFirstDie: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die.", targetIndex, true); break;
            case Step.SpecialMirrorSecondOpponent: DrawOpponentPair("SPECIAL MIRROR", "Choose the second opponent.", false); break;
            case Step.SpecialMirrorSecondDie: DrawDice("SPECIAL MIRROR", $"Choose {gameManager.Players[secondTargetIndex].playerId}'s die.", secondTargetIndex, true); break;
            case Step.SpecialExecutionerTarget: DrawTargets("SPECIAL EXECUTIONER", "Choose a player who must discard their entire hand.", false, Step.Cards); break;
            case Step.JokerTarget: DrawTargets("JOKER", "Choose whose die to flip.", true, Step.Cards); break;
            case Step.JokerDie: DrawDice("JOKER", "Choose the die to flip.", targetIndex, true); break;
            case Step.GordonChoice: DrawChoice("GORDON ROBLEYS", new[] { "SPECIAL ARTIST", "SPECIAL SOLDIER", "SPECIAL COLLECTOR", "SPECIAL BODYGUARD" }, ChooseGordon, Step.Cards); break;
        }
    }

    private void DrawCards()
    {
        CardadoPlayerState p = gameManager.Players[playerIndex];
        Rect r = Box(900, 430); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), $"{p.playerId} — CARD ACTION", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 850, 30), "Play a card before choosing a die, or skip.", GUI.skin.label);
        List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand);
        float bw = 145, gap = 12, total = cards.Count * bw + Mathf.Max(0, cards.Count - 1) * gap, x = r.x + (900 - total) * .5f;
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance c = cards[i]; if (c == null || c.data == null) continue;
            if (GUI.Button(new Rect(x + i * (bw + gap), r.y + 120, bw, 90), c.data.id + "\n" + c.data.cardType + (c.data.isBlankCard ? "\nBlank" : ""), button)) SelectCard(c);
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 320, 850, 55), "SKIP CARD ACTION", button)) SkipCardAction();
    }

    private void DrawDice(string heading, string text, int p, bool allowBack)
    {
        if (p < 0 || p >= gameManager.Players.Count) return;
        CardadoPlayerState state = gameManager.Players[p]; Rect r = Box(760, 390); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 710, 45), $"{state.playerId} — {heading}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 710, 55), text, GUI.skin.label);
        for (int d = 0; d < state.dice.Count; d++)
        {
            if (!gameManager.IsDieAvailable(p, d)) continue;
            GUI.enabled = CanAffect(p, d) || step == Step.ArtistDie || step == Step.SpecialArtistDie || step == Step.JokerDie || p == playerIndex;
            if (GUI.Button(new Rect(r.x + 25 + d * 105, r.y + 135, 90, 70), $"Die {d + 1}\n{state.dice[d]}", button)) DieChosen(d);
            GUI.enabled = true;
        }
        if (allowBack && GUI.Button(new Rect(r.x + 25, r.y + 285, 710, 55), "BACK", button)) BackFromDie();
    }

    private void DrawTargets(string heading, string text, bool self, Step backStep)
    {
        Rect r = Box(850, 370); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), heading, title); GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), text, GUI.skin.label);
        float x = r.x + 25;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (!self && i == playerIndex) continue;
            GUI.enabled = CanAffectPlayer(i) || (self && i == playerIndex);
            CardadoPlayerState p = gameManager.Players[i];
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), $"{p.playerId}\nChips: {p.chips}", button)) TargetChosen(i);
            GUI.enabled = true; x += 160;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 270, 800, 55), "BACK", button)) { step = backStep; targetIndex = secondTargetIndex = -1; }
    }

    private void DrawOpponentPair(string heading, string text, bool first)
    {
        Rect r = Box(850, 370); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), heading, title); GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), text, GUI.skin.label);
        float x = r.x + 25;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == playerIndex || (!first && i == targetIndex)) continue;
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), $"{gameManager.Players[i].playerId}\nChips: {gameManager.Players[i].chips}", button))
            {
                if (first) { targetIndex = i; step = Step.SpecialMirrorFirstDie; }
                else { secondTargetIndex = i; step = Step.SpecialMirrorSecondDie; }
            }
            x += 160;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 270, 800, 55), "BACK", button)) step = Step.SpecialMirrorChoice;
    }

    private void DrawCollectorCards()
    {
        CardadoPlayerState p = gameManager.Players[targetIndex]; Rect r = Box(820, 430); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), $"COLLECTOR — STEAL FROM {p.playerId}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 770, 35), "Choose a card. It is revealed only when selected.", GUI.skin.label);
        for (int i = 0; i < p.hand.cardsInHand.Count; i++) if (GUI.Button(new Rect(r.x + 25 + (i % 5) * 155, r.y + 125 + (i / 5) * 80, 140, 65), $"CARD {i + 1}", button)) CollectorCard(p.hand.cardsInHand[i]);
        if (GUI.Button(new Rect(r.x + 25, r.y + 345, 770, 55), "BACK", button)) { step = Step.CollectorTarget; targetIndex = -1; }
    }

    private void DrawModifierDirection()
    {
        Rect r = Box(700, 360); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 650, 45), "MODIFIER", title); GUI.Label(new Rect(r.x + 25, r.y + 70, 650, 30), "Choose +1 or -1. You can still go back.", GUI.skin.label);
        if (GUI.Button(new Rect(r.x + 50, r.y + 125, 280, 70), "+1", button)) { modifierDirection = 1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 370, r.y + 125, 280, 70), "-1", button)) { modifierDirection = -1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 50, r.y + 225, 600, 55), "BACK", button)) BackToCards();
    }

    private void DrawModifierTarget()
    {
        Rect r = Box(900, 520); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), $"MODIFIER {modifierDirection:+#;-#} — CHOOSE DIE", title);
        float y = r.y + 105;
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            GUI.Label(new Rect(r.x + 25, y, 140, 30), gameManager.Players[p].playerId, GUI.skin.label); float x = r.x + 170;
            for (int d = 0; d < gameManager.Players[p].dice.Count; d++)
            {
                if (!gameManager.IsDieAvailable(p, d)) continue;
                bool valid = (modifierDirection > 0 && gameManager.Players[p].dice[d] < 6) || (modifierDirection < 0 && gameManager.Players[p].dice[d] > 1);
                valid &= CanAffect(p, d);
                GUI.enabled = valid; if (GUI.Button(new Rect(x, y - 5, 90, 60), $"Die {d + 1}\n{gameManager.Players[p].dice[d]}", button)) ModifierChosen(p, d); GUI.enabled = true; x += 105;
            }
            y += 80;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 455, 850, 50), "BACK", button)) step = Step.ModifierDirection;
    }

    private void DrawChoice(string heading, string[] options, Action<int> choose, Step backStep)
    {
        Rect r = Box(820, 390); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), heading, title);
        for (int i = 0; i < options.Length; i++) if (GUI.Button(new Rect(r.x + 40, r.y + 90 + i * 75, 740, 60), options[i], button)) choose(i);
        if (GUI.Button(new Rect(r.x + 40, r.y + 250 + options.Length * 10, 740, 55), "BACK", button)) step = backStep;
    }

    private void DrawAllOpponentDieChoice()
    {
        Rect r = Box(760, 390); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 710, 45), "SPECIAL SOLDIER — CHOOSE DIE", title);
        for (int d = 0; d < gameManager.RoundDiceCount; d++)
        {
            bool available = false; for (int p = 0; p < gameManager.Players.Count; p++) if (p != playerIndex && gameManager.IsDieAvailable(p, d)) available = true;
            GUI.enabled = available; if (GUI.Button(new Rect(r.x + 25 + d * 105, r.y + 120, 90, 70), $"Die {d + 1}", button)) { for (int p = 0; p < gameManager.Players.Count; p++) if (p != playerIndex && CanAffect(p, d)) gameManager.Players[p].dice[d] = UnityEngine.Random.Range(1, 7); CommitAndFinish(); } GUI.enabled = true;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 285, 710, 55), "BACK", button)) step = Step.SpecialSoldierChoice;
    }

    private void DrawSpecialCollectorTake()
    {
        if (collectorPosition >= collectorOpponents.Count) { step = Step.SpecialCollectorPlay; return; }
        int target = collectorOpponents[collectorPosition]; CardadoPlayerState p = gameManager.Players[target]; Rect r = Box(820, 400); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), $"SPECIAL COLLECTOR — TAKE FROM {p.playerId}", title);
        for (int i = 0; i < p.hand.cardsInHand.Count; i++) if (GUI.Button(new Rect(r.x + 25 + i * 155, r.y + 125, 140, 65), $"CARD {i + 1}", button)) TakeCollectorCard(i);
        if (GUI.Button(new Rect(r.x + 25, r.y + 285, 770, 55), "BACK", button)) step = Step.SpecialCollectorChoice;
    }

    private void DrawSpecialCollectorPlay()
    {
        Rect r = Box(850, 430); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), "SPECIAL COLLECTOR — CHOOSE CARD TO PLAY", title);
        for (int i = 0; i < collectorPool.Count; i++) if (GUI.Button(new Rect(r.x + 25 + i * 165, r.y + 125, 150, 80), collectorPool[i].data.id + "\n" + collectorPool[i].data.cardType, button)) PlayCollectorCard(i);
        if (GUI.Button(new Rect(r.x + 25, r.y + 330, 800, 55), "BACK", button)) step = Step.SpecialCollectorTake;
    }

    private void SelectCard(CardInstance card)
    {
        if (card == null || card.data == null) return;
        activeCard = card; targetIndex = secondTargetIndex = ownDieIndex = -1; modifierDirection = 0;
        if (card.data.isBlankCard) { CommitAndFinish(); return; }
        if (card.data.isModifier) { step = Step.ModifierDirection; return; }
        if (card.data.rarity == CardRarity.Special) { RouteSpecial(card.data.cardType); return; }
        if (card.data.rarity == CardRarity.Royalty) { RouteRoyalty(card.data.cardType); return; }
        switch (card.data.cardType)
        {
            case CardType.Artist: step = Step.ArtistDie; break;
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Collector: step = Step.CollectorTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: CommitAndFinish(); break;
        }
    }

    private void RouteSpecial(CardType type)
    {
        switch (type)
        {
            case CardType.Artist: step = Step.SpecialArtistDie; break;
            case CardType.Knight: step = Step.SpecialSoldierChoice; break;
            case CardType.Collector: step = Step.SpecialCollectorChoice; break;
            case CardType.Bodyguard: step = Step.SpecialBodyguardChoice; break;
            case CardType.Mirror: step = Step.SpecialMirrorChoice; break;
            case CardType.Executioner: step = Step.SpecialExecutionerTarget; break;
            default: CommitAndFinish(); break;
        }
    }

    private void RouteRoyalty(CardType type)
    {
        switch (type)
        {
            case CardType.Joker: step = Step.JokerTarget; break;
            case CardType.Queen: ResolveQueen(); break;
            case CardType.King: ResolveKing(); break;
            case CardType.GordonRobleys: step = Step.GordonChoice; break;
            default: CommitAndFinish(); break;
        }
    }

    private void SkipCardAction() { activeCard = null; step = Step.DieAfterSkip; targetIndex = secondTargetIndex = ownDieIndex = -1; }
    private void BackToCards() { activeCard = null; targetIndex = secondTargetIndex = ownDieIndex = -1; modifierDirection = 0; collectorPool.Clear(); collectorOpponents.Clear(); step = Step.Cards; }

    private void BackFromDie()
    {
        switch (step)
        {
            case Step.DieAfterSkip: case Step.ArtistDie: case Step.BodyguardDie: case Step.SpecialArtistDie: case Step.SpecialSoldierDie: case Step.SpecialMirrorOwnDie: case Step.SpecialMirrorTargetDie: case Step.SpecialMirrorFirstDie: case Step.SpecialMirrorSecondDie: case Step.JokerDie: BackToCards(); break;
            case Step.SoldierDie: targetIndex = -1; step = Step.SoldierTarget; break;
            case Step.MirrorOwnDie: targetIndex = -1; step = Step.MirrorTarget; break;
            case Step.MirrorTargetDie: ownDieIndex = -1; step = Step.MirrorOwnDie; break;
            case Step.SpecialSoldierAllDie: step = Step.SpecialSoldierChoice; break;
        }
    }

    private void TargetChosen(int target)
    {
        targetIndex = target;
        switch (step)
        {
            case Step.SoldierTarget: step = Step.SoldierDie; break;
            case Step.CollectorTarget: step = Step.CollectorCard; break;
            case Step.MirrorTarget: step = Step.MirrorOwnDie; break;
            case Step.ExecutionerTarget: ResolveExecutioner(target); break;
            case Step.SpecialSoldierTarget: ResolveSpecialSoldier(target); break;
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
                int skipped = playerIndex; if (gameManager.TrySkipCardAction(skipped)) gameManager.TryPlayDie(skipped, die); break;
            case Step.ArtistDie: Reroll(playerIndex, die, 1); CommitAndFinish(); break;
            case Step.SoldierDie: if (CanAffect(targetIndex, die)) { Reroll(targetIndex, die, 1); CommitAndFinish(); } break;
            case Step.BodyguardDie: bodyguards.Add(Key(playerIndex, die)); CommitAndFinish(); break;
            case Step.MirrorOwnDie: ownDieIndex = die; step = Step.MirrorTargetDie; break;
            case Step.MirrorTargetDie: if (CanAffect(targetIndex, die)) { Exchange(playerIndex, ownDieIndex, targetIndex, die); CommitAndFinish(); } break;
            case Step.SpecialArtistDie: Reroll(playerIndex, die, 3); CommitAndFinish(); break;
            case Step.SpecialSoldierDie: if (CanAffect(targetIndex, die)) { Reroll(targetIndex, die, 1); CommitAndFinish(); } break;
            case Step.SpecialMirrorOwnDie: ownDieIndex = die; step = Step.SpecialMirrorTargetDie; break;
            case Step.SpecialMirrorTargetDie: if (CanAffect(targetIndex, die)) { Exchange(playerIndex, ownDieIndex, targetIndex, die); CommitAndFinish(); } break;
            case Step.SpecialMirrorFirstDie: ownDieIndex = die; step = Step.SpecialMirrorSecondOpponent; break;
            case Step.SpecialMirrorSecondDie: if (CanAffect(secondTargetIndex, die)) { Exchange(targetIndex, ownDieIndex, secondTargetIndex, die); CommitAndFinish(); } break;
            case Step.JokerDie: if (CanAffect(targetIndex, die)) { int v = gameManager.Players[targetIndex].dice[die]; gameManager.Players[targetIndex].dice[die] = 7 - v; CommitAndFinish(); } break;
        }
    }

    private void CollectorCard(CardInstance stolen)
    {
        if (stolen == null) return;
        gameManager.Players[targetIndex].hand.RemoveCard(stolen);
        CommitCardOnly();
        activeCard = stolen; RouteStolen(stolen);
    }

    private void ModifierChosen(int p, int d)
    {
        if (!gameManager.IsDieAvailable(p, d) || !CanAffect(p, d)) return;
        int v = gameManager.Players[p].dice[d]; if (modifierDirection > 0 && v >= 6 || modifierDirection < 0 && v <= 1) return;
        string key = Key(p, d); if (modifierOriginal.ContainsKey(key)) return;
        modifierOriginal[key] = v; gameManager.Players[p].dice[d] = v + modifierDirection; CommitAndFinish();
    }

    private void ResolveExecutioner(int target)
    {
        bool cancelled = false; CardadoPlayerState p = gameManager.Players[target];
        for (int d = 0; d < p.dice.Count; d++)
        {
            string key = Key(target, d);
            if (modifierOriginal.ContainsKey(key)) { p.dice[d] = modifierOriginal[key]; modifierOriginal.Remove(key); cancelled = true; }
            if (bodyguards.Remove(key)) cancelled = true;
        }
        if (!cancelled) cardBlockedThisHand.Add(target); CommitAndFinish();
    }

    private void ChooseSpecialSoldier(int option) { step = option == 0 ? Step.SpecialSoldierTarget : Step.SpecialSoldierAllDie; }
    private void ResolveSpecialSoldier(int target) { if (!CanAffectPlayer(target)) return; for (int d = 0; d < gameManager.Players[target].dice.Count; d++) if (CanAffect(target, d)) gameManager.Players[target].dice[d] = UnityEngine.Random.Range(1, 7); CommitAndFinish(); }

    private void ChooseSpecialCollector(int option)
    {
        if (option == 1)
        {
            CommitCardOnly();
            for (int i = 0; i < 3; i++) { CardInstance c = gameManager.RoundDeck.Draw(); if (c == null) break; gameManager.Players[playerIndex].hand.AddCard(c); }
            FinishAfterCardEffect(); return;
        }
        collectorPool.Clear(); collectorOpponents.Clear(); collectorPosition = 0;
        for (int p = 0; p < gameManager.Players.Count; p++) if (p != playerIndex && gameManager.Players[p].hand.cardsInHand.Count > 0 && CanAffectPlayer(p)) collectorOpponents.Add(p);
        step = collectorOpponents.Count == 0 ? Step.SpecialCollectorPlay : Step.SpecialCollectorTake;
    }

    private void TakeCollectorCard(int index)
    {
        int target = collectorOpponents[collectorPosition]; CardadoPlayerState p = gameManager.Players[target];
        if (index < 0 || index >= p.hand.cardsInHand.Count) return;
        CardInstance c = p.hand.cardsInHand[index]; p.hand.RemoveCard(c); collectorPool.Add(c); collectorPosition++;
        if (collectorPosition >= collectorOpponents.Count) step = Step.SpecialCollectorPlay;
    }

    private void PlayCollectorCard(int index)
    {
        CardInstance chosen = collectorPool[index];
        for (int i = 0; i < collectorPool.Count; i++) if (i != index) gameManager.DiscardResolvedCard(collectorPool[i]);
        collectorPool.Clear(); CommitCardOnly(); activeCard = chosen; RouteStolen(chosen);
    }

    private void RouteStolen(CardInstance card)
    {
        if (card.data.isBlankCard) { gameManager.DiscardResolvedCard(card); FinishAfterCardEffect(); return; }
        if (card.data.isModifier) { step = Step.ModifierDirection; return; }
        if (card.data.rarity == CardRarity.Special) { RouteSpecial(card.data.cardType); return; }
        if (card.data.rarity == CardRarity.Royalty) { RouteRoyalty(card.data.cardType); return; }
        switch (card.data.cardType)
        {
            case CardType.Artist: step = Step.ArtistDie; break;
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Collector: step = Step.CollectorTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: gameManager.DiscardResolvedCard(card); FinishAfterCardEffect(); break;
        }
    }

    private void ChooseSpecialBodyguard(int option)
    {
        if (option == 0) for (int d = 0; d < gameManager.Players[playerIndex].dice.Count; d++) if (gameManager.IsDieAvailable(playerIndex, d)) bodyguards.Add(Key(playerIndex, d));
        else protectedPlayersThisRound.Add(playerIndex);
        CommitAndFinish();
    }

    private void ChooseSpecialMirror(int option) { step = option == 0 ? Step.SpecialMirrorTarget : Step.SpecialMirrorFirstOpponent; }
    private void ResolveSpecialExecutioner(int target) { if (!CanAffectPlayer(target)) return; CardadoPlayerState p = gameManager.Players[target]; List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand); p.hand.cardsInHand.Clear(); foreach (CardInstance c in cards) gameManager.DiscardResolvedCard(c); CommitAndFinish(); }

    private void ChooseGordon(int option)
    {
        switch (option)
        {
            case 0: step = Step.SpecialArtistDie; break;
            case 1: step = Step.SpecialSoldierChoice; break;
            case 2: step = Step.SpecialCollectorChoice; break;
            default: step = Step.SpecialBodyguardChoice; break;
        }
    }

    private void ResolveQueen()
    {
        CommitCardOnly();
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            List<CardInstance> cards = new List<CardInstance>(gameManager.Players[p].hand.cardsInHand); gameManager.Players[p].hand.cardsInHand.Clear(); foreach (CardInstance c in cards) gameManager.DiscardResolvedCard(c);
        }
        for (int p = 0; p < gameManager.Players.Count; p++) for (int i = 0; i < 3; i++) { CardInstance c = gameManager.RoundDeck.Draw(); if (c == null) break; gameManager.Players[p].hand.AddCard(c); }
        BeginAnotherCardOrDie();
    }

    private void ResolveKing()
    {
        CommitCardOnly();
        for (int p = 0; p < gameManager.Players.Count; p++) for (int d = 0; d < gameManager.Players[p].dice.Count; d++) if (CanAffect(p, d)) gameManager.Players[p].dice[d] = UnityEngine.Random.Range(1, 7);
        BeginAnotherCardOrDie();
    }

    private void Reroll(int p, int d, int times) { for (int i = 0; i < times; i++) gameManager.Players[p].dice[d] = UnityEngine.Random.Range(1, 7); }
    private void Exchange(int a, int ad, int b, int bd) { int v = gameManager.Players[a].dice[ad]; gameManager.Players[a].dice[ad] = gameManager.Players[b].dice[bd]; gameManager.Players[b].dice[bd] = v; }

    private bool CanAffect(int p, int d) => gameManager.IsDieAvailable(p, d) && CanAffectPlayer(p) && !bodyguards.Contains(Key(p, d));
    private bool CanAffectPlayer(int p) => p >= 0 && p < gameManager.Players.Count && (p == playerIndex || !protectedPlayersThisRound.Contains(p));
    private string Key(int p, int d) => p + ":" + d;

    private void CommitCardOnly()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true; gameManager.Players[playerIndex].hand.RemoveCard(activeCard); gameManager.DiscardResolvedCard(activeCard);
    }

    private void CommitAndFinish() { CommitCardOnly(); FinishAfterCardEffect(); }

    private void FinishAfterCardEffect()
    {
        activeCard = null; visible = false; step = Step.None; targetIndex = secondTargetIndex = ownDieIndex = -1; modifierDirection = 0; collectorPool.Clear(); collectorOpponents.Clear();
        gameManager.TrySkipCardAction(playerIndex);
    }

    private void BeginAnotherCardOrDie()
    {
        activeCard = null; targetIndex = secondTargetIndex = ownDieIndex = -1;
        if (gameManager.Players[playerIndex].hand.cardsInHand.Count > 0) { visible = true; step = Step.Cards; }
        else { visible = false; gameManager.TrySkipCardAction(playerIndex); }
    }

    private int IndexOf(CardadoPlayerState p) { for (int i = 0; i < gameManager.Players.Count; i++) if (gameManager.Players[i] == p) return i; return -1; }
    private Rect Box(float w, float h) => new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
    private void Styles()
    {
        if (panel != null) return;
        panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
    }
}
