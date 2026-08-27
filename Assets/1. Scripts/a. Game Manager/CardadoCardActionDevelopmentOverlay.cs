using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporary development UI for the normal card effects and modifier.</summary>
public class CardadoCardActionDevelopmentOverlay : MonoBehaviour
{
    private enum Step { None, Cards, ArtistDie, SoldierTarget, SoldierDie, CollectorTarget, CollectorCard, BodyguardDie, MirrorTarget, MirrorOwnDie, MirrorTargetDie, ModifierDirection, ModifierTarget, ExecutionerTarget }
    private CardadoGameManager gameManager;
    private Step step;
    private bool visible;
    private int playerIndex = -1, targetIndex = -1, ownDieIndex = -1, modifierDirection;
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
        visible = true; targetIndex = -1; ownDieIndex = -1;
        step = request == CardadoCardActionRequestType.ChooseArtistDie ? Step.ArtistDie : Step.Cards;
        if (step == Step.Cards && cardBlockedThisHand.Contains(playerIndex))
        {
            visible = false; step = Step.None;
            Debug.Log($"[Cardado] {player.playerId} is blocked from card play this hand; proceeding to die selection.");
            gameManager.TrySkipCardAction(playerIndex);
            return;
        }
        Debug.Log($"[Cardado] CARD ACTION REQUIRED: {player.playerId} — {(step == Step.Cards ? "choose a card or skip" : "choose a die for Artist")}.");
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int hand, int starter)
    {
        visible = false; step = Step.None; cardBlockedThisHand.Clear();
        playerIndex = -1; targetIndex = -1; ownDieIndex = -1; activeCard = null;
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
            case Step.ArtistDie: DrawDice("ARTIST", "Choose one of your available dice to reroll.", playerIndex); break;
            case Step.SoldierTarget: DrawTargets("SOLDIER", "Choose an opponent.", false); break;
            case Step.SoldierDie: DrawDice("SOLDIER", $"Choose a die from {gameManager.Players[targetIndex].playerId} to reroll.", targetIndex); break;
            case Step.CollectorTarget: DrawTargets("COLLECTOR", "Choose an opponent to steal a card from.", false); break;
            case Step.CollectorCard: DrawCollectorCards(); break;
            case Step.BodyguardDie: DrawDice("BODYGUARD", "Choose one of your dice to protect.", playerIndex); break;
            case Step.MirrorTarget: DrawTargets("MIRROR", "Choose an opponent.", false); break;
            case Step.MirrorOwnDie: DrawDice("MIRROR", "Choose your die to exchange.", playerIndex); break;
            case Step.MirrorTargetDie: DrawDice("MIRROR", $"Choose {gameManager.Players[targetIndex].playerId}'s die to exchange.", targetIndex); break;
            case Step.ModifierDirection: DrawModifierDirection(); break;
            case Step.ModifierTarget: DrawModifierTarget(); break;
            case Step.ExecutionerTarget: DrawTargets("EXECUTIONER", "Choose an opponent to cancel.", false); break;
        }
    }

    private void DrawCards()
    {
        CardadoPlayerState p = gameManager.Players[playerIndex];
        Rect r = Box(900, 430); GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x+25,r.y+20,850,45), $"{p.playerId} — CARD ACTION", title);
        GUI.Label(new Rect(r.x+25,r.y+70,850,30), "Play a card before choosing a die, or skip.", GUI.skin.label);
        List<CardInstance> cards = new List<CardInstance>(p.hand.cardsInHand);
        float bw=145, gap=12, total=cards.Count*bw+Mathf.Max(0,cards.Count-1)*gap, x=r.x+(900-total)*.5f;
        for(int i=0;i<cards.Count;i++)
        {
            CardInstance c=cards[i]; if(c==null||c.data==null) continue;
            if(GUI.Button(new Rect(x+i*(bw+gap),r.y+120,bw,90),c.data.id+"\n"+c.data.cardType+(c.data.isBlankCard?"\nBlank":""),button)) PlayCard(c);
        }
        if(GUI.Button(new Rect(r.x+25,r.y+320,850,55),"SKIP CARD ACTION",button)) Finish(false);
    }

    private void DrawDice(string heading,string text,int p)
    {
        CardadoPlayerState state=gameManager.Players[p]; Rect r=Box(760,310); GUI.Box(r,GUIContent.none,panel);
        GUI.Label(new Rect(r.x+25,r.y+20,710,45),$"{state.playerId} — {heading}",title);
        GUI.Label(new Rect(r.x+25,r.y+70,710,40),text,GUI.skin.label);
        for(int d=0;d<state.dice.Count;d++) if(gameManager.IsDieAvailable(p,d))
            if(GUI.Button(new Rect(r.x+25+d*105,r.y+125,90,70),$"Die {d+1}\n{state.dice[d]}",button)) DieChosen(d);
    }

    private void DrawTargets(string heading,string text,bool self)
    {
        Rect r=Box(800,330); GUI.Box(r,GUIContent.none,panel); GUI.Label(new Rect(r.x+25,r.y+20,750,45),heading,title); GUI.Label(new Rect(r.x+25,r.y+70,750,35),text,GUI.skin.label);
        float x=r.x+25; for(int i=0;i<gameManager.Players.Count;i++) { if(!self&&i==playerIndex) continue; CardadoPlayerState p=gameManager.Players[i]; if(GUI.Button(new Rect(x,r.y+125,145,75),$"{p.playerId}\nChips: {p.chips}",button)) TargetChosen(i); x+=160; }
    }

    private void DrawCollectorCards()
    {
        CardadoPlayerState p=gameManager.Players[targetIndex]; Rect r=Box(820,350); GUI.Box(r,GUIContent.none,panel); GUI.Label(new Rect(r.x+25,r.y+20,770,45),$"COLLECTOR — STEAL FROM {p.playerId}",title); GUI.Label(new Rect(r.x+25,r.y+70,770,35),"Choose a card. Only card positions are shown in this development UI.",GUI.skin.label);
        List<CardInstance> cards=new List<CardInstance>(p.hand.cardsInHand); float x=r.x+25,y=r.y+125; for(int i=0;i<cards.Count;i++){ if(GUI.Button(new Rect(x,y,145,70),$"CARD {i+1}",button)) CollectorCard(cards[i]); x+=160; if(x>r.x+650){x=r.x+25;y+=85;} }
    }

    private void DrawModifierDirection()
    {
        Rect r=Box(700,300); GUI.Box(r,GUIContent.none,panel); GUI.Label(new Rect(r.x+25,r.y+20,650,45),"MODIFIER",title); GUI.Label(new Rect(r.x+25,r.y+70,650,30),"Choose +1 or -1 first.",GUI.skin.label);
        if(GUI.Button(new Rect(r.x+50,r.y+125,280,70),"+1",button)) { modifierDirection=1; step=Step.ModifierTarget; }
        if(GUI.Button(new Rect(r.x+370,r.y+125,280,70),"-1",button)) { modifierDirection=-1; step=Step.ModifierTarget; }
    }

    private void DrawModifierTarget()
    {
        Rect r=Box(900,500); GUI.Box(r,GUIContent.none,panel); GUI.Label(new Rect(r.x+25,r.y+20,850,45),$"MODIFIER {modifierDirection:+#;-#} — CHOOSE DIE",title); float y=r.y+85;
        for(int p=0;p<gameManager.Players.Count;p++){ CardadoPlayerState state=gameManager.Players[p]; GUI.Label(new Rect(r.x+25,y,140,30),state.playerId,GUI.skin.label); float x=r.x+170; for(int d=0;d<state.dice.Count;d++) if(gameManager.IsDieAvailable(p,d)){ if(GUI.Button(new Rect(x,y-5,90,60),$"Die {d+1}\n{state.dice[d]}",button)) ModifierChosen(p,d); x+=105; } y+=80; }
    }

    private void PlayCard(CardInstance card)
    {
        if(card==null||card.data==null)return;
        if(card.data.isBlankCard)
        {
            int i=gameManager.Players[playerIndex].hand.cardsInHand.IndexOf(card); if(i>=0) gameManager.TryPlayCard(playerIndex,i); return;
        }
        if(card.data.isModifier)
        {
            gameManager.Players[playerIndex].hand.RemoveCard(card); card.isPlayed=true; activeCard=card; step=Step.ModifierDirection;
            Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} played modifier {card.data.id}. Choose +1 or -1."); return;
        }
        if(card.data.rarity==CardRarity.Normal && card.data.cardType==CardType.Artist)
        {
            int i=gameManager.Players[playerIndex].hand.cardsInHand.IndexOf(card); if(i>=0) gameManager.TryPlayCard(playerIndex,i); return;
        }
        if(card.data.rarity!=CardRarity.Normal)
        {
            Debug.Log($"[Cardado] {card.data.id} is {card.data.rarity}; special/royalty effects are the next implementation pass.");
            return;
        }
        gameManager.Players[playerIndex].hand.RemoveCard(card); card.isPlayed=true; activeCard=card;
        switch(card.data.cardType)
        {
            case CardType.Knight: step=Step.SoldierTarget; break;
            case CardType.Collector: step=Step.CollectorTarget; break;
            case CardType.Bodyguard: step=Step.BodyguardDie; break;
            case CardType.Mirror: step=Step.MirrorTarget; break;
            case CardType.Executioner: step=Step.ExecutionerTarget; break;
            default: Finish(true); break;
        }
        Debug.Log($"[Cardado] {gameManager.Players[playerIndex].playerId} played {card.data.id}.");
    }

    private void TargetChosen(int target)
    {
        targetIndex=target;
        if(step==Step.SoldierTarget)step=Step.SoldierDie;
        else if(step==Step.CollectorTarget)step=Step.CollectorCard;
        else if(step==Step.MirrorTarget)step=Step.MirrorOwnDie;
        else if(step==Step.ExecutionerTarget)Executioner(target);
    }

    private void DieChosen(int die)
    {
        if(step==Step.ArtistDie)
        {
            if(gameManager.PendingCardActionCard!=null){gameManager.TryResolveArtistDie(playerIndex,die);return;}
            int old=gameManager.Players[playerIndex].dice[die]; gameManager.Players[playerIndex].dice[die]=UnityEngine.Random.Range(1,7); Debug.Log($"[Cardado] Stolen Artist rerolled die #{die+1}: {old} -> {gameManager.Players[playerIndex].dice[die]}."); Finish(true); return;
        }
        if(step==Step.SoldierDie){ if(IsProtected(targetIndex,die)){Debug.LogWarning("[Cardado] Soldier target is protected.");return;} CardadoPlayerState p=gameManager.Players[targetIndex];int old=p.dice[die];p.dice[die]=UnityEngine.Random.Range(1,7);Debug.Log($"[Cardado] Soldier rerolled {p.playerId} die #{die+1}: {old} -> {p.dice[die]}.");Finish(true);return; }
        if(step==Step.BodyguardDie){bodyguards.Add(Key(playerIndex,die));Debug.Log($"[Cardado] Bodyguard protected {gameManager.Players[playerIndex].playerId} die #{die+1}.");Finish(false);return;}
        if(step==Step.MirrorOwnDie){ownDieIndex=die;step=Step.MirrorTargetDie;return;}
        if(step==Step.MirrorTargetDie){if(IsProtected(targetIndex,die)){Debug.LogWarning("[Cardado] Mirror target is protected.");return;}CardadoPlayerState a=gameManager.Players[playerIndex],b=gameManager.Players[targetIndex];int v=a.dice[ownDieIndex];a.dice[ownDieIndex]=b.dice[die];b.dice[die]=v;Debug.Log($"[Cardado] Mirror exchanged {a.playerId} die #{ownDieIndex+1} with {b.playerId} die #{die+1}.");Finish(true);}
    }

    private void CollectorCard(CardInstance stolen)
    {
        if(stolen==null)return; CardadoPlayerState target=gameManager.Players[targetIndex];target.hand.RemoveCard(stolen);stolen.isPlayed=true; if(activeCard!=null)gameManager.DiscardResolvedCard(activeCard); activeCard=stolen;
        if(stolen.data.isBlankCard){Debug.Log($"[Cardado] Collector immediately played blank {stolen.data.id}.");Finish(true);return;}
        if(stolen.data.isModifier){step=Step.ModifierDirection;return;}
        if(stolen.data.rarity!=CardRarity.Normal){Debug.Log($"[Cardado] Stolen {stolen.data.id} is {stolen.data.rarity}; special/royalty effects are next pass.");Finish(true);return;}
        switch(stolen.data.cardType){case CardType.Artist:step=Step.ArtistDie;break;case CardType.Knight:step=Step.SoldierTarget;break;case CardType.Bodyguard:step=Step.BodyguardDie;break;case CardType.Mirror:step=Step.MirrorTarget;break;case CardType.Executioner:step=Step.ExecutionerTarget;break;default:Finish(true);break;}
    }

    private void ModifierChosen(int p,int d)
    {
        if(IsProtected(p,d)&&p!=playerIndex){Debug.LogWarning("[Cardado] Modifier target is protected.");return;}
        string key=Key(p,d); if(modifierOriginal.ContainsKey(key)){Debug.LogWarning("[Cardado] Die already has a modifier.");return;}
        modifierOriginal[key]=gameManager.Players[p].dice[d]; gameManager.Players[p].dice[d]+=modifierDirection; Debug.Log($"[Cardado] Modifier {modifierDirection:+#;-#} applied to {gameManager.Players[p].playerId} die #{d+1}: {gameManager.Players[p].dice[d]}."); Finish(false);
    }

    private void Executioner(int target)
    {
        if(target==playerIndex)return; bool cancelled=false; CardadoPlayerState p=gameManager.Players[target];
        for(int d=0;d<p.dice.Count;d++){string key=Key(target,d);if(modifierOriginal.ContainsKey(key)){p.dice[d]=modifierOriginal[key];modifierOriginal.Remove(key);cancelled=true;Debug.Log($"[Cardado] Executioner cancelled modifier on {p.playerId} die #{d+1}.");}if(bodyguards.Remove(key)){cancelled=true;Debug.Log($"[Cardado] Executioner cancelled Bodyguard on {p.playerId} die #{d+1}.");}}
        if(!cancelled){cardBlockedThisHand.Add(target);Debug.Log($"[Cardado] Executioner blocked {p.playerId} from playing a card for the rest of this hand.");}
        Finish(true);
    }

    private void Finish(bool discard)
    {
        CardInstance card=activeCard; if(card!=null&&discard)gameManager.DiscardResolvedCard(card); activeCard=null; visible=false;step=Step.None;targetIndex=-1;ownDieIndex=-1;modifierDirection=0; gameManager.TrySkipCardAction(playerIndex);
    }

    private bool IsProtected(int p,int d)=>bodyguards.Contains(Key(p,d));
    private string Key(int p,int d)=>p+":"+d;
    private int IndexOf(CardadoPlayerState p){for(int i=0;i<gameManager.Players.Count;i++)if(gameManager.Players[i]==p)return i;return -1;}
    private Rect Box(float w,float h)=>new Rect((Screen.width-w)*.5f,(Screen.height-h)*.5f,w,h);
    private void Styles(){if(panel!=null)return;panel=new GUIStyle(GUI.skin.box){padding=new RectOffset(20,20,20,20)};title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};button=new GUIStyle(GUI.skin.button){fontSize=18,fontStyle=FontStyle.Bold};}
}
