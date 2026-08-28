using System;
using System.Collections.Generic;
using UnityEngine;

public class CardadoCardActionDevelopmentOverlayV2 : MonoBehaviour
{
    private enum Step { None, Cards, DieAfterSkip, ArtistDie, SoldierTarget, SoldierDie, CollectorTarget, CollectorCard, BodyguardDie, MirrorTarget, MirrorOwnDie, MirrorTargetDie, ModifierDirection, ModifierTarget, ExecutionerTarget, ExecutionerEffects, SpecialBodyguardChoice, SpecialBodyguardTarget }
    private enum EffectType { Modifier, BodyguardDie, SpecialBodyguardPlayer, SpecialBodyguardHand }
    private sealed class Effect { public EffectType type; public CardInstance card; public int owner = -1; public int target = -1; public int die = -1; public string key; public int originalValue; }

    private CardadoGameManager gm;
    private Step step;
    private bool visible;
    private int playerIndex = -1, targetIndex = -1, ownDieIndex = -1;
    private int modifierDirection;
    private int trackedHand = -1;
    private CardInstance activeCard;
    private string executionerMessage;

    private readonly HashSet<int> blockedThisHand = new HashSet<int>();
    private readonly HashSet<int> playedCardThisHand = new HashSet<int>();
    private readonly HashSet<int> protectedPlayers = new HashSet<int>();
    private readonly HashSet<string> protectedDice = new HashSet<string>();
    private readonly Dictionary<string, int> modifierOriginal = new Dictionary<string, int>();
    private readonly List<Effect> effects = new List<Effect>();
    private readonly List<Effect> executionerEffects = new List<Effect>();

    private GUIStyle panel, title, button, value, greenValue, redValue, small;

    private void Awake() { gm = GetComponent<CardadoGameManager>(); if (gm == null) gm = FindFirstObjectByType<CardadoGameManager>(); }

    private void OnEnable()
    {
        if (gm == null) gm = FindFirstObjectByType<CardadoGameManager>();
        if (gm == null) return;
        gm.CardActionRequested += OnCardActionRequested;
        gm.HandTurnStarted += OnHandTurnStarted;
        gm.DiePlayed += OnDiePlayed;
        gm.RoundResolutionCompleted += OnRoundResolutionCompleted;
    }

    private void OnDisable()
    {
        if (gm == null) return;
        gm.CardActionRequested -= OnCardActionRequested;
        gm.HandTurnStarted -= OnHandTurnStarted;
        gm.DiePlayed -= OnDiePlayed;
        gm.RoundResolutionCompleted -= OnRoundResolutionCompleted;
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType request)
    {
        playerIndex = IndexOf(player);
        if (playerIndex < 0) return;
        visible = true;
        targetIndex = ownDieIndex = -1;
        modifierDirection = 0;
        activeCard = null;
        executionerMessage = null;
        step = request == CardadoCardActionRequestType.ChooseArtistDie ? Step.ArtistDie : Step.Cards;
        if (step == Step.Cards && (player.hand.cardsInHand.Count == 0 || blockedThisHand.Contains(playerIndex)))
        {
            visible = false;
            step = Step.None;
            gm.TrySkipCardAction(playerIndex);
        }
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int hand, int starter)
    {
        if (hand != trackedHand)
        {
            trackedHand = hand;
            blockedThisHand.Clear();
            playedCardThisHand.Clear();
            ClearHandBodyguard();
        }
        visible = false;
        step = Step.None;
        playerIndex = targetIndex = ownDieIndex = -1;
        activeCard = null;
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int value)
    {
        int p = IndexOf(player);
        string key = Key(p, dieIndex);
        if (modifierOriginal.ContainsKey(key))
        {
            modifierOriginal.Remove(key);
            RemoveEffect(EffectType.Modifier, key, true);
        }
        if (protectedDice.Remove(key)) RemoveEffect(EffectType.BodyguardDie, key, true);

        // Special Bodyguard protects every die from the moment it is played.
        if (handBodyguardActive && p >= 0)
            protectedDice.Add(key);
    }

    private bool handBodyguardActive;
    private Effect handBodyguardEffect;

    private void ClearHandBodyguard()
    {
        if (handBodyguardEffect != null) RemoveEffectObj(handBodyguardEffect, true);
        handBodyguardEffect = null;
        handBodyguardActive = false;
        protectedDice.Clear();
    }

    private void OnRoundResolutionCompleted()
    {
        for (int i = effects.Count - 1; i >= 0; i--) RemoveEffectObj(effects[i], true);
        effects.Clear();
        blockedThisHand.Clear();
        playedCardThisHand.Clear();
        protectedPlayers.Clear();
        protectedDice.Clear();
        modifierOriginal.Clear();
        handBodyguardEffect = null;
        handBodyguardActive = false;
    }

    private void OnGUI()
    {
        if (gm == null) return;
        Styles();
        if (effects.Count > 0) DrawEffects();
        if (!visible || playerIndex < 0) return;

        switch (step)
        {
            case Step.Cards: DrawCards(); break;
            case Step.DieAfterSkip: DrawDice("CHOOSE DIE", "You skipped the card. You can still go back.", playerIndex, true); break;
            case Step.ArtistDie: DrawDice("ARTIST", "Choose one of your dice to reroll.", playerIndex, true); break;
            case Step.SoldierTarget: DrawTargets("SOLDIER", "Choose an opponent.", false, Step.Cards); break;
            case Step.SoldierDie: DrawDice("SOLDIER", "Choose a die from " + gm.Players[targetIndex].playerId + ".", targetIndex, true); break;
            case Step.CollectorTarget: DrawTargets("COLLECTOR", "Choose an opponent.", false, Step.Cards); break;
            case Step.CollectorCard: DrawCollectorCards(); break;
            case Step.BodyguardDie: DrawDice("BODYGUARD", "Choose one of your dice to protect.", playerIndex, true); break;
            case Step.MirrorTarget: DrawTargets("MIRROR", "Choose an opponent.", false, Step.Cards); break;
            case Step.MirrorOwnDie: DrawDice("MIRROR", "Choose your die.", playerIndex, true); break;
            case Step.MirrorTargetDie: DrawDice("MIRROR", "Choose " + gm.Players[targetIndex].playerId + "'s die.", targetIndex, true); break;
            case Step.ModifierDirection: DrawModifierDirection(); break;
            case Step.ModifierTarget: DrawModifierTarget(); break;
            case Step.ExecutionerTarget: DrawTargets("EXECUTIONER", executionerMessage ?? "Choose an opponent.", false, Step.Cards); break;
            case Step.ExecutionerEffects: DrawExecutionerEffects(); break;
            case Step.SpecialBodyguardChoice: DrawChoice("SPECIAL BODYGUARD", new[] { "PROTECT ALL DICE OF ONE PLAYER", "PROTECT ALL DICE PLAYED THIS HAND" }, ChooseSpecialBodyguard); break;
            case Step.SpecialBodyguardTarget: DrawTargets("SPECIAL BODYGUARD", "Choose the player to protect.", true, Step.SpecialBodyguardChoice); break;
        }
    }

    private void DrawCards()
    {
        CardadoPlayerState p = gm.Players[playerIndex];
        Rect r = Box(900, 430); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), p.playerId + " — CARD ACTION", title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 850, 30), "Play a card before choosing a die, or skip.", GUI.skin.label);
        float bw = 145, gap = 12, total = p.hand.cardsInHand.Count * bw + Mathf.Max(0, p.hand.cardsInHand.Count - 1) * gap;
        float x = r.x + (900 - total) * .5f;
        for (int i = 0; i < p.hand.cardsInHand.Count; i++)
        {
            CardInstance c = p.hand.cardsInHand[i]; if (c == null || c.data == null) continue;
            if (GUI.Button(new Rect(x + i * (bw + gap), r.y + 120, bw, 90), c.data.id + "\n" + c.data.cardType, button)) SelectCard(c);
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 320, 850, 55), "SKIP CARD ACTION", button)) SkipCardAction();
    }

    private void DrawDice(string heading, string text, int p, bool allowBack)
    {
        if (p < 0 || p >= gm.Players.Count) return;
        CardadoPlayerState state = gm.Players[p];
        Rect r = Box(760, 390); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 710, 45), state.playerId + " — " + heading, title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 710, 55), text, GUI.skin.label);
        for (int d = 0; d < state.dice.Count; d++)
        {
            if (!gm.IsDieAvailable(p, d)) continue;
            Rect q = new Rect(r.x + 25 + d * 105, r.y + 135, 90, 70);
            GUI.enabled = CanChooseDie(p, d);
            if (GUI.Button(q, "Die " + (d + 1), button)) DieChosen(d);
            GUI.enabled = true;
            DrawModifierValue(q, p, d);
        }
        if (allowBack && GUI.Button(new Rect(r.x + 25, r.y + 285, 710, 55), "BACK", button)) BackFromDie();
    }

    private void DrawModifierValue(Rect q, int p, int d)
    {
        string key = Key(p, d); GUIStyle style = value;
        if (modifierOriginal.ContainsKey(key)) style = gm.Players[p].dice[d] > modifierOriginal[key] ? greenValue : redValue;
        GUI.Label(new Rect(q.x, q.y + 35, q.width, 30), gm.Players[p].dice[d].ToString(), style);
    }

    private void DrawTargets(string heading, string text, bool self, Step backStep)
    {
        Rect r = Box(850, 370); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 800, 45), heading, title);
        GUI.Label(new Rect(r.x + 25, r.y + 70, 800, 35), text, GUI.skin.label);
        float x = r.x + 25;
        for (int i = 0; i < gm.Players.Count; i++)
        {
            if (!self && i == playerIndex) continue;
            GUI.enabled = CanPlayer(i);
            if (GUI.Button(new Rect(x, r.y + 125, 145, 75), gm.Players[i].playerId + "\nChips: " + gm.Players[i].chips, button)) TargetChosen(i);
            GUI.enabled = true; x += 160;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 270, 800, 55), "BACK", button)) { step = backStep; targetIndex = -1; executionerMessage = null; }
    }

    private void DrawCollectorCards()
    {
        CardadoPlayerState p = gm.Players[targetIndex]; Rect r = Box(820, 430); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), "COLLECTOR — STEAL FROM " + p.playerId, title);
        for (int i = 0; i < p.hand.cardsInHand.Count; i++)
            if (GUI.Button(new Rect(r.x + 25 + (i % 5) * 155, r.y + 110 + (i / 5) * 80, 140, 65), "CARD " + (i + 1), button)) CollectorCard(p.hand.cardsInHand[i]);
        if (GUI.Button(new Rect(r.x + 25, r.y + 345, 770, 55), "BACK", button)) { step = Step.CollectorTarget; targetIndex = -1; }
    }

    private void DrawModifierDirection()
    {
        Rect r = Box(700, 360); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 650, 45), "MODIFIER", title);
        if (GUI.Button(new Rect(r.x + 50, r.y + 125, 280, 70), "+1", button)) { modifierDirection = 1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 370, r.y + 125, 280, 70), "-1", button)) { modifierDirection = -1; step = Step.ModifierTarget; }
        if (GUI.Button(new Rect(r.x + 50, r.y + 225, 600, 55), "BACK", button)) BackToCards();
    }

    private void DrawModifierTarget()
    {
        Rect r = Box(900, 520); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 25, r.y + 20, 850, 45), "MODIFIER " + (modifierDirection > 0 ? "+1" : "-1") + " — CHOOSE DIE", title);
        float y = r.y + 105;
        for (int p = 0; p < gm.Players.Count; p++)
        {
            GUI.Label(new Rect(r.x + 25, y, 140, 30), gm.Players[p].playerId, GUI.skin.label);
            float x = r.x + 170;
            for (int d = 0; d < gm.Players[p].dice.Count; d++)
            {
                if (!gm.IsDieAvailable(p, d)) continue;
                int current = gm.Players[p].dice[d];
                bool valid = (modifierDirection > 0 && current < 6) || (modifierDirection < 0 && current > 1);
                valid &= CanAffect(p, d);
                GUI.enabled = valid;
                Rect q = new Rect(x, y - 5, 90, 60);
                if (GUI.Button(q, "Die " + (d + 1), button)) ModifierChosen(p, d);
                GUI.enabled = true; DrawModifierValue(q, p, d); x += 105;
            }
            y += 80;
        }
        if (GUI.Button(new Rect(r.x + 25, r.y + 455, 850, 50), "BACK", button)) step = Step.ModifierDirection;
    }

    private void DrawChoice(string heading, string[] options, Action<int> choose)
    {
        Rect r = Box(820, 350); GUI.Box(r, GUIContent.none, panel); GUI.Label(new Rect(r.x + 25, r.y + 20, 770, 45), heading, title);
        for (int i = 0; i < options.Length; i++) if (GUI.Button(new Rect(r.x + 40, r.y + 90 + i * 75, 740, 60), options[i], button)) choose(i);
        if (GUI.Button(new Rect(r.x + 40, r.y + 250, 740, 55), "BACK", button)) BackToCards();
    }

    private void DrawExecutionerEffects()
    {
        Rect r = Box(820, 420); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 780, 45), "EXECUTIONER — " + gm.Players[targetIndex].playerId, title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 780, 35), "Choose the permanent effect to cancel.", GUI.skin.label);
        for (int i = 0; i < executionerEffects.Count; i++)
            if (GUI.Button(new Rect(r.x + 25, r.y + 120 + i * 70, 760, 55), EffectText(executionerEffects[i]), button)) CancelEffect(executionerEffects[i]);
        if (GUI.Button(new Rect(r.x + 25, r.y + 300, 760, 55), "BACK", button)) { executionerMessage = null; step = Step.ExecutionerTarget; }
    }

    private void DrawEffects()
    {
        Rect r = new Rect(15, 15, 430, 70 + effects.Count * 42); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 10, r.y + 8, r.width - 20, 28), "ACTIVE CARD EFFECTS", small);
        for (int i = 0; i < effects.Count; i++) GUI.Label(new Rect(r.x + 10, r.y + 40 + i * 42, r.width - 20, 40), EffectText(effects[i]), small);
    }

    private void SelectCard(CardInstance card)
    {
        if (card == null || card.data == null) return;
        activeCard = card; targetIndex = ownDieIndex = -1; modifierDirection = 0; executionerMessage = null;
        if (card.data.isBlankCard) { CommitAndFinish(); return; }
        if (card.data.isModifier) { step = Step.ModifierDirection; return; }
        if (card.data.rarity == CardRarity.Special)
        {
            switch (card.data.cardType)
            {
                case CardType.Bodyguard: step = Step.SpecialBodyguardChoice; return;
                default: CommitAndFinish(); return;
            }
        }
        if (card.data.rarity == CardRarity.Royalty) { CommitAndFinish(); return; }
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

    private void SkipCardAction() { activeCard = null; step = Step.DieAfterSkip; targetIndex = ownDieIndex = -1; }
    private void BackToCards() { activeCard = null; targetIndex = ownDieIndex = -1; modifierDirection = 0; step = Step.Cards; }

    private void BackFromDie()
    {
        switch (step)
        {
            case Step.DieAfterSkip:
            case Step.ArtistDie:
            case Step.BodyguardDie: BackToCards(); break;
            case Step.SoldierDie: targetIndex = -1; step = Step.SoldierTarget; break;
            case Step.MirrorOwnDie: targetIndex = -1; step = Step.MirrorTarget; break;
            case Step.MirrorTargetDie: ownDieIndex = -1; step = Step.MirrorOwnDie; break;
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
            case Step.SpecialBodyguardTarget: ResolveSpecialBodyguardPlayer(target); break;
        }
    }

    private void DieChosen(int die)
    {
        switch (step)
        {
            case Step.DieAfterSkip: if (gm.TrySkipCardAction(playerIndex)) gm.TryPlayDie(playerIndex, die); break;
            case Step.ArtistDie: if (CanChooseDie(playerIndex, die)) { Reroll(playerIndex, die, 1); CommitAndFinish(); } break;
            case Step.SoldierDie: if (CanAffect(targetIndex, die)) { Reroll(targetIndex, die, 1); CommitAndFinish(); } break;
            case Step.BodyguardDie: AddBodyguardDie(die); break;
            case Step.MirrorOwnDie: if (CanChooseDie(playerIndex, die)) { ownDieIndex = die; step = Step.MirrorTargetDie; } break;
            case Step.MirrorTargetDie: if (CanAffect(targetIndex, die)) { Exchange(playerIndex, ownDieIndex, targetIndex, die); CommitAndFinish(); } break;
        }
    }

    private void AddBodyguardDie(int die)
    {
        if (!CanChooseDie(playerIndex, die)) return;
        string key = Key(playerIndex, die); if (protectedDice.Contains(key)) return;
        protectedDice.Add(key);
        Persistent(new Effect { type = EffectType.BodyguardDie, card = activeCard, owner = playerIndex, target = playerIndex, die = die, key = key });
    }

    private void CollectorCard(CardInstance stolen)
    {
        if (stolen == null) return;
        gm.Players[targetIndex].hand.RemoveCard(stolen);
        CommitCardOnly();
        if (stolen.data != null && stolen.data.isModifier) { activeCard = stolen; step = Step.ModifierDirection; }
        else { gm.DiscardResolvedCard(stolen); FinishAfterCardEffect(); }
    }

    private void ModifierChosen(int p, int d)
    {
        if (!CanAffect(p, d)) return;
        int current = gm.Players[p].dice[d];
        if ((modifierDirection > 0 && current >= 6) || (modifierDirection < 0 && current <= 1)) return;
        string key = Key(p, d); if (modifierOriginal.ContainsKey(key)) return;
        modifierOriginal[key] = current;
        gm.Players[p].dice[d] = current + modifierDirection;
        Persistent(new Effect { type = EffectType.Modifier, card = activeCard, owner = playerIndex, target = p, die = d, key = key, originalValue = current });
    }

    private void ResolveExecutioner(int target)
    {
        if (!playedCardThisHand.Contains(target))
        {
            blockedThisHand.Add(target);
            CommitAndFinish();
            return;
        }

        executionerEffects.Clear();
        foreach (Effect e in effects)
            if (e.owner == target && (e.type == EffectType.Modifier || e.type == EffectType.BodyguardDie || e.type == EffectType.SpecialBodyguardPlayer || e.type == EffectType.SpecialBodyguardHand)) executionerEffects.Add(e);

        if (executionerEffects.Count == 0)
        {
            executionerMessage = gm.Players[target].playerId + " already played a card and has no cancelable permanent effect.";
            step = Step.ExecutionerTarget;
        }
        else if (executionerEffects.Count == 1) CancelEffect(executionerEffects[0]);
        else step = Step.ExecutionerEffects;
    }

    private void CancelEffect(Effect effect)
    {
        if (effect == null) return;
        switch (effect.type)
        {
            case EffectType.Modifier:
                if (modifierOriginal.ContainsKey(effect.key))
                {
                    int[] parsed = ParseKey(effect.key);
                    if (parsed[0] >= 0 && gm.IsDieAvailable(parsed[0], parsed[1])) gm.Players[parsed[0]].dice[parsed[1]] = modifierOriginal[effect.key];
                    modifierOriginal.Remove(effect.key);
                }
                break;
            case EffectType.BodyguardDie: protectedDice.Remove(effect.key); break;
            case EffectType.SpecialBodyguardPlayer: protectedPlayers.Remove(effect.target); break;
            case EffectType.SpecialBodyguardHand: handBodyguardActive = false; handBodyguardEffect = null; break;
        }
        RemoveEffectObj(effect, true);
        CommitAndFinish();
    }

    private void ChooseSpecialBodyguard(int option)
    {
        if (option == 0) { step = Step.SpecialBodyguardTarget; return; }
        handBodyguardActive = true;
        handBodyguardEffect = new Effect { type = EffectType.SpecialBodyguardHand, card = activeCard, owner = playerIndex, key = "special_bodyguard_hand" };
        effects.Add(handBodyguardEffect);
        CommitPersistentCard();
        FinishAfterCardEffect();
    }

    private void ResolveSpecialBodyguardPlayer(int target)
    {
        if (target < 0 || target >= gm.Players.Count) return;
        protectedPlayers.Add(target);
        effects.Add(new Effect { type = EffectType.SpecialBodyguardPlayer, card = activeCard, owner = playerIndex, target = target, key = "special_bodyguard_player:" + target });
        CommitPersistentCard();
        FinishAfterCardEffect();
    }

    private void Reroll(int p, int d, int times)
    {
        if (!gm.IsDieAvailable(p, d)) return;
        for (int i = 0; i < times; i++) gm.Players[p].dice[d] = UnityEngine.Random.Range(1, 7);
    }

    private void Exchange(int a, int ad, int b, int bd)
    {
        if (!gm.IsDieAvailable(a, ad) || !gm.IsDieAvailable(b, bd)) return;
        int value = gm.Players[a].dice[ad];
        gm.Players[a].dice[ad] = gm.Players[b].dice[bd];
        gm.Players[b].dice[bd] = value;
    }

    private bool CanChooseDie(int p, int d)
    {
        if (!gm.IsDieAvailable(p, d)) return false;
        if (protectedPlayers.Contains(p)) return false;
        return p == playerIndex;
    }

    private bool CanAffect(int p, int d) { return gm.IsDieAvailable(p, d) && CanAffectPlayer(p) && !protectedDice.Contains(Key(p, d)); }
    private bool CanAffectPlayer(int p) { return p >= 0 && p < gm.Players.Count && !protectedPlayers.Contains(p); }
    private bool CanPlayer(int p) { return p >= 0 && p < gm.Players.Count && !protectedPlayers.Contains(p); }

    private string EffectText(Effect e)
    {
        string owner = e.owner >= 0 && e.owner < gm.Players.Count ? gm.Players[e.owner].playerId : "?";
        if (e.type == EffectType.Modifier) return (gm.Players[e.target].dice[e.die] > e.originalValue ? "+1" : "-1") + " — " + owner + " → " + gm.Players[e.target].playerId + " Die " + (e.die + 1);
        if (e.type == EffectType.BodyguardDie) return "BODYGUARD — " + owner + " protects Die " + (e.die + 1);
        if (e.type == EffectType.SpecialBodyguardPlayer) return "SPECIAL BODYGUARD — " + owner + " protects " + gm.Players[e.target].playerId;
        return "SPECIAL BODYGUARD — " + owner + " protects dice played this hand";
    }

    private void Persistent(Effect effect)
    {
        if (effect == null || activeCard == null) return;
        effect.card.isPlayed = true;
        gm.Players[playerIndex].hand.RemoveCard(effect.card);
        effects.Add(effect);
        playedCardThisHand.Add(playerIndex);
        activeCard = null;
        FinishAfterCardEffect();
    }

    private void CommitPersistentCard()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true;
        gm.Players[playerIndex].hand.RemoveCard(activeCard);
        playedCardThisHand.Add(playerIndex);
        activeCard = null;
    }

    private void RemoveEffect(EffectType type, string key, bool discard)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
            if (effects[i].type == type && effects[i].key == key) { RemoveEffectObj(effects[i], discard); return; }
    }

    private void RemoveEffectObj(Effect effect, bool discard)
    {
        if (effect == null) return;
        effects.Remove(effect);
        if (effect.type == EffectType.SpecialBodyguardHand && handBodyguardEffect == effect) { handBodyguardActive = false; handBodyguardEffect = null; }
        if (discard && effect.card != null) gm.DiscardResolvedCard(effect.card);
    }

    private void CommitCardOnly()
    {
        if (activeCard == null) return;
        activeCard.isPlayed = true;
        gm.Players[playerIndex].hand.RemoveCard(activeCard);
        gm.DiscardResolvedCard(activeCard);
        playedCardThisHand.Add(playerIndex);
    }

    private void CommitAndFinish() { CommitCardOnly(); FinishAfterCardEffect(); }

    private void FinishAfterCardEffect()
    {
        activeCard = null; visible = false; step = Step.None; targetIndex = ownDieIndex = -1; modifierDirection = 0; executionerMessage = null;
        gm.TrySkipCardAction(playerIndex);
    }

    private int IndexOf(CardadoPlayerState player) { for (int i = 0; i < gm.Players.Count; i++) if (gm.Players[i] == player) return i; return -1; }
    private string Key(int p, int d) { return p + ":" + d; }
    private int[] ParseKey(string key) { string[] a = key.Split(':'); int p, d; if (a.Length != 2 || !int.TryParse(a[0], out p) || !int.TryParse(a[1], out d)) return new[] { -1, -1 }; return new[] { p, d }; }
    private Rect Box(float w, float h) { return new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h); }

    private void Styles()
    {
        if (panel != null) return;
        panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        value = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        greenValue = new GUIStyle(value); greenValue.normal.textColor = Color.green;
        redValue = new GUIStyle(value); redValue.normal.textColor = Color.red;
        small = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
    }
}