using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary development UI for the normal card effects and modifier.
/// Decision selections are reversible until the final action is committed.
/// </summary>
public class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private enum Step
    {
        None, Cards, ArtistDie, DieAfterSkip, SoldierTarget, SoldierDie,
        CollectorTarget, CollectorCard, BodyguardDie, MirrorTarget,
        MirrorOwnDie, MirrorTargetDie, ModifierDirection, ModifierTarget,
        ExecutionerTarget
    }

    private CardadoGameManager gameManager;
    private Step step;
    private bool visible;
    private int playerIndex = -1, targetIndex = -1, ownDieIndex = -1, modifierDirection;
    private int trackedHandNumber = -1;
    private CardInstance activeCard;
    private readonly HashSet<string> bodyguards = new HashSet<string>();
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
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.CardActionRequested -= OnCardActionRequested;
        gameManager.HandTurnStarted -= OnHandTurnStarted;
        gameManager.DiePlayed -= OnDiePlayed;
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType request)
    {
        playerIndex = IndexOf(player);
        if (playerIndex < 0) return;

        visible = true;
        targetIndex = -1;
        ownDieIndex = -1;
        modifierDirection = 0;
        activeCard = null;
        step = request == CardadoCardActionRequestType.ChooseArtistDie ? Step.ArtistDie : Step.Cards;

        if (step == Step.Cards && cardBlockedThisHand.Contains(playerIndex))
        {
            visible = false;
            step = Step.None;
            Debug.Log($"[Cardado] {player.playerId} is blocked from card play this hand; proceeding to die selection.");
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
        ownDieIndex = -1;
        activeCard = null;
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
            case Step.ArtistDie: DrawDice("ARTIST", "Choose one of your available dice to reroll.", playerIndex, true); break;
            case Step.DieAfterSkip: DrawDice("CHOOSE DIE", "You skipped the card. You can still go back before playing the die.", playerIndex, true); break;
            case Step.SoldierTarget: DrawTargets("SOLDIER", "Choose an opponent.", false); break;
            case Step.SoldierDie: DrawDice("SOLDIER", $"Choose a die from {gameManager.Players[targetIndex].playerId} to reroll.", targetIndex, true); break;
            case Step.CollectorTarget: DrawTargets("COLLECTOR", "Choose an opponent to steal a card from.", false); break;
            case Step.CollectorCard: DrawCollectorCards(); break;
            case Step.BodyguardDie: DrawDice("BODYGUARD", "Choose one of your dice to protect.", playerIndex, true); break;
            case Step.MirrorTarget: DrawTargets("MIRROR", "Choose an opponent.", false); break;
            case Step.MirrorOwnDie: DrawDice("MIRROR", "Choose your die to exchange.", playerIndex, true); break;
            case Step.MirrorTargetDie: DrawDice("MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die to exchange.", targetIndex, true); break;
            case Step.ModifierDirection: DrawModifierDirection(); break;
            case Step.ModifierTarget: DrawModifierTarget(); break;
            case Step.ExecutionerTarget: DrawTargets("EXECUTIONER", "Choose an opponent to cancel.", false); break;
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

    private void DrawTargets(string heading, string text, bool self)
    {
        Rect r = Box(800, 330);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 750, 45), heading, title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 750, 35), text, GUI.skin.label);

        float x = r.x + 25;
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (!self && i == playerIndex) continue;
            CardadoPlayerState p = gameManager.Players[i];
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), $"{p.playerId}\nChips: {p.chips}", button))
                TargetChosen(i);
            x += 160;
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 245, 750, 55), "BACK", button))
            BackFromTargetSelection();
    }

    private void DrawCollectorCards()
    {
        CardadoPlayerState p = gameManager.Players[targetIndex];
        Rect r = Box(820, 430);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), $"COLLECTOR — STEAL FROM {p.playerId}", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 770, 35), "Choose a card. Selecting it commits the Collector action.", GUI.skin.label);

        List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand);
        float x = r.x + 25, y = r.y + 125;
        for (int i = 0; i < cards.Count; i++)
        {
            if (GUI.Button(new Rect(x, y, 145, 70), $"CARD {i + 1}", button))
                CollectorCard(cards[i]);
            x += 160;
            if (x > r.x + 650) { x = r.x + 25; y += 85; }
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 345, 770, 55), "BACK", button))
            BackFromTargetSelection();
    }

    private void DrawModifierDirection()
    {
        Rect r = Box(700, 360);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 650, 45), "MODIFIER", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 650, 30), "Choose +1 or -1 first. You can still go back.", GUI.skin.label);

        if (GUI.Button(new Rect(r.x + 50, r.y + 125, 280, 70), "+1", button))
        {
            modifierDirection = 1;
            step = Step.ModifierTarget;
        }
        if (GUI.Button(new Rect(r.x + 370, r.y + 125, 280, 70), "-1", button))
        {
            modifierDirection = -1;
            step = Step.ModifierTarget;
        }
        if (GUI.Button(new Rect(r.x + 50, r.y + 225, 600, 55), "BACK", button))
            BackToCards();
    }

    private void DrawModifierTarget()
    {
        // Five player rows plus a visible Back button fit inside the development window.
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

                bool valid = (modifierDirection == 1 && state.dice[d] < 6) ||
                             (modifierDirection == -1 && state.dice[d] > 1);

                GUI.enabled = valid;
                if (GUI.Button(new Rect(x, y - 5, 90, 60), $"Die {d + 1}\n{state.dice[d]}", button))
                    ModifierChosen(p, d);
                GUI.enabled = true;

                x += 105;
            }
            y += 80;
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 455, 850, 50), "BACK", button))
            step = Step.ModifierDirection;
    }

    private void SelectCard(CardInstance card)
    {
        if (card == null || card.data == null) return;

        activeCard = card;
        targetIndex = -1;
        ownDieIndex = -1;
        modifierDirection = 0;

        if (card.data.isBlankCard)
        {
            CommitSimpleCard();
            return;
        }

        if (card.data.isModifier)
        {
            step = Step.ModifierDirection;
            Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} selected modifier {card.data.id}; card remains in hand until committed.");
            return;
        }

        if (card.data.rarity == CardRarity.Normal && card.data.cardType == CardType.Artist)
        {
            step = Step.ArtistDie;
            Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} selected Artist {card.data.id}; choose a die or go back.");
            return;
        }

        if (card.data.rarity != CardRarity.Normal)
        {
            Debug.Log($"[Cardado] {card.data.id} is {card.data.rarity}; special/royalty effects are the next implementation pass.");
            activeCard = null;
            return;
        }

        switch (card.data.cardType)
        {
            case CardType.Knight: step = Step.SoldierTarget; break;
            case CardType.Collector: step = Step.CollectorTarget; break;
            case CardType.Bodyguard: step = Step.BodyguardDie; break;
            case CardType.Mirror: step = Step.MirrorTarget; break;
            case CardType.Executioner: step = Step.ExecutionerTarget; break;
            default: CommitSimpleCard(); return;
        }

        Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} selected {card.data.id}; selection remains reversible until the effect is committed.");
    }

    private void SkipCardAction()
    {
        activeCard = null;
        targetIndex = -1;
        ownDieIndex = -1;
        modifierDirection = 0;
        step = Step.DieAfterSkip;
        Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} skipped card action; die selection remains reversible.");
    }

    private void BackToCards()
    {
        activeCard = null;
        targetIndex = -1;
        ownDieIndex = -1;
        modifierDirection = 0;
        step = Step.Cards;
        Debug.Log("[Cardado] Back to card selection.");
    }

    private void BackFromTargetSelection()
    {
        if (step == Step.SoldierTarget || step == Step.CollectorTarget || step == Step.MirrorTarget || step == Step.ExecutionerTarget)
        {
            BackToCards();
            return;
        }
        if (step == Step.SoldierDie)
        {
            targetIndex = -1;
            step = Step.SoldierTarget;
            return;
        }
        if (step == Step.CollectorCard)
        {
            targetIndex = -1;
            step = Step.CollectorTarget;
            return;
        }
        if (step == Step.MirrorOwnDie)
        {
            ownDieIndex = -1;
            targetIndex = -1;
            step = Step.MirrorTarget;
        }
    }

    private void BackFromDieSelection()
    {
        if (step == Step.DieAfterSkip || step == Step.ArtistDie || step == Step.BodyguardDie)
        {
            BackToCards();
            return;
        }
        if (step == Step.SoldierDie)
        {
            targetIndex = -1;
            step = Step.SoldierTarget;
            return;
        }
        if (step == Step.MirrorOwnDie)
        {
            targetIndex = -1;
            step = Step.MirrorTarget;
            return;
        }
        if (step == Step.MirrorTargetDie)
        {
            ownDieIndex = -1;
            step = Step.MirrorOwnDie;
        }
    }

    private void TargetChosen(int target)
    {
        targetIndex = target;
        if (step == Step.SoldierTarget) step = Step.SoldierDie;
        else if (step == Step.CollectorTarget) step = Step.CollectorCard;
        else if (step == Step.MirrorTarget) step = Step.MirrorOwnDie;
        else if (step == Step.ExecutionerTarget) Executioner(target);
    }

    private void DieChosen(int die)
    {
        if (step == Step.DieAfterSkip)
        {
            if (!gameManager.TrySkipCardAction(playerIndex)) return;
            gameManager.TryPlayDie(playerIndex, die);
            return;
        }
        if (step == Step.ArtistDie)
        {
            CommitArtistDie(die);
            return;
        }
        if (step == Step.SoldierDie)
        {
            if (IsProtected(targetIndex, die)) { Debug.LogWarning("[Cardado] Soldier target is protected."); return; }
            CardadoPlayerState target = gameManager.Players[targetIndex];
            int old = target.dice[die];
            target.dice[die] = UnityEngine.Random.Range(1, 7);
            Debug.Log($"[Cardado] Soldier rerolled {target.playerId} die #{die + 1}: {old} -> {target.dice[die]}.");
            CommitActiveCardAndContinue();
            return;
        }
        if (step == Step.BodyguardDie)
        {
            bodyguards.Add(Key(playerIndex, die));
            Debug.Log($"[Cardado] Bodyguard protected {gameManager.Players[playerIndex].playerId} die #{die + 1}.");
            CommitActiveCardAndContinue();
            return;
        }
        if (step == Step.MirrorOwnDie)
        {
            ownDieIndex = die;
            step = Step.MirrorTargetDie;
            return;
        }
        if (step == Step.MirrorTargetDie)
        {
            if (IsProtected(targetIndex, die)) { Debug.LogWarning("[Cardado] Mirror target is protected."); return; }
            CardadoPlayerState a = gameManager.Players[playerIndex];
            CardadoPlayerState b = gameManager.Players[targetIndex];
            int v = a.dice[ownDieIndex];
            a.dice[ownDieIndex] = b.dice[die];
            b.dice[die] = v;
            Debug.Log($"[Cardado] Mirror exchanged {a.playerId} die #{ownDieIndex + 1} with {b.playerId} die #{die + 1}.");
            CommitActiveCardAndContinue();
        }
    }

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

        if (stolen.data.isBlankCard)
        {
            Debug.Log($"[Cardado] Collector immediately played blank {stolen.data.id}.");
            gameManager.DiscardResolvedCard(stolen);
            FinishCommittedCard();
            return;
        }
        if (stolen.data.isModifier)
        {
            step = Step.ModifierDirection;
            Debug.Log($"[Cardado] Collector committed; stolen modifier {stolen.data.id} now awaits its own direction choice.");
            return;
        }
        if (stolen.data.rarity != CardRarity.Normal)
        {
            Debug.Log($"[Cardado] Stolen {stolen.data.id} is {stolen.data.rarity}; special/royalty effects are next pass.");
            FinishCommittedCard();
            return;
        }

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

        int currentValue = gameManager.Players[p].dice[d];
        if (modifierDirection == 1 && currentValue >= 6)
        {
            Debug.LogWarning($"[Cardado] Cannot apply +1 to {gameManager.Players[p].playerId} die #{d + 1}: die is already 6.");
            return;
        }
        if (modifierDirection == -1 && currentValue <= 1)
        {
            Debug.LogWarning($"[Cardado] Cannot apply -1 to {gameManager.Players[p].playerId} die #{d + 1}: die is already 1.");
            return;
        }
        if (IsProtected(p, d) && p != playerIndex)
        {
            Debug.LogWarning("[Cardado] Modifier target is protected.");
            return;
        }

        string key = Key(p, d);
        if (modifierOriginal.ContainsKey(key))
        {
            Debug.LogWarning("[Cardado] Die already has a modifier.");
            return;
        }

        modifierOriginal[key] = currentValue;
        gameManager.Players[p].dice[d] = currentValue + modifierDirection;
        Debug.Log($"[Cardado] Modifier {modifierDirection:+#;-#} applied to {gameManager.Players[p].playerId} die #{d + 1}: {gameManager.Players[p].dice[d]}.");
        CommitActiveCardAndContinue();
    }

    private void Executioner(int target)
    {
        if (target == playerIndex) return;

        bool cancelled = false;
        CardadoPlayerState p = gameManager.Players[target];
        for (int d = 0; d < p.dice.Count; d++)
        {
            string key = Key(target, d);
            if (modifierOriginal.ContainsKey(key))
            {
                p.dice[d] = modifierOriginal[key];
                modifierOriginal.Remove(key);
                cancelled = true;
                Debug.Log($"[Cardado] Executioner cancelled modifier on {p.playerId} die #{d + 1}.");
            }
            if (bodyguards.Remove(key))
            {
                cancelled = true;
                Debug.Log($"[Cardado] Executioner cancelled Bodyguard on {p.playerId} die #{d + 1}.");
            }
        }

        if (!cancelled)
        {
            cardBlockedThisHand.Add(target);
            Debug.Log($"[Cardado] Executioner blocked {p.playerId} from playing a card for the rest of this hand.");
        }
        CommitActiveCardAndContinue();
    }

    private void CommitArtistDie(int die)
    {
        if (activeCard == null || !gameManager.IsDieAvailable(playerIndex, die)) return;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        int old = player.dice[die];
        player.dice[die] = UnityEngine.Random.Range(1, 7);
        Debug.Log($"[Cardado] {player.playerId} rerolled die #{die + 1}: {old} -> {player.dice[die]} using {activeCard.data.id}.");
        CommitActiveCardAndContinue();
    }

    private void CommitSimpleCard()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true;
        gameManager.Players[playerIndex].hand.RemoveCard(activeCard);
        gameManager.DiscardResolvedCard(activeCard);
        Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} played {activeCard.data.id}.");
        FinishCommittedCard();
    }

    private void CommitActiveCardAndContinue()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true;
        gameManager.Players[playerIndex].hand.RemoveCard(activeCard);
        gameManager.DiscardResolvedCard(activeCard);
        Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} committed {activeCard.data.id}. No further back navigation is allowed.");
        FinishCommittedCard();
    }

    private void FinishCommittedCard()
    {
        activeCard = null;
        visible = false;
        step = Step.None;
        targetIndex = -1;
        ownDieIndex = -1;
        modifierDirection = 0;
        gameManager.TrySkipCardAction(playerIndex);
    }

    private bool IsProtected(int p, int d) => bodyguards.Contains(Key(p, d));
    private string Key(int p, int d) => p + ":" + d;

    private int IndexOf(CardadoPlayerState p)
    {
        for (int i = 0; i < gameManager.Players.Count; i++)
            if (gameManager.Players[i] == p) return i;
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