using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Development-only flow fixes for Collector cards and skipped die selection.
/// Collector steals are blind: the source card identities are hidden until stolen.
/// Regular Collector also remains reversible until the stolen card effect commits.
/// </summary>
[DefaultExecutionOrder(-800)]
public class CardadoCollectorAndSkipDevelopmentFix : MonoBehaviour
{
    CardadoGameManager gm;
    CardadoCardActionDevelopmentOverlayV2 overlay;

    FieldInfo stepField, visibleField, piField, tiField, activeField, collectorOppField, collectorPosField, selectedFromCollectorField;
    MethodInfo collectorMethod, takeCollectorMethod, targetMethod;

    bool regularStolen;
    int regularStolenFrom = -1;
    CardInstance regularStolenCard;

    GUIStyle box, title, button;

    void Awake()
    {
        gm = GetComponent<CardadoGameManager>();
        if (gm == null) gm = FindFirstObjectByType<CardadoGameManager>();

        overlay = GetComponent<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) overlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) return;

        Type t = overlay.GetType();
        stepField = t.GetField("step", BindingFlags.Instance | BindingFlags.NonPublic);
        visibleField = t.GetField("visible", BindingFlags.Instance | BindingFlags.NonPublic);
        piField = t.GetField("pi", BindingFlags.Instance | BindingFlags.NonPublic);
        tiField = t.GetField("ti", BindingFlags.Instance | BindingFlags.NonPublic);
        activeField = t.GetField("active", BindingFlags.Instance | BindingFlags.NonPublic);
        collectorOppField = t.GetField("collectorOpp", BindingFlags.Instance | BindingFlags.NonPublic);
        collectorPosField = t.GetField("collectorPos", BindingFlags.Instance | BindingFlags.NonPublic);
        selectedFromCollectorField = t.GetField("selectedFromCollector", BindingFlags.Instance | BindingFlags.NonPublic);
        collectorMethod = t.GetMethod("Collector", BindingFlags.Instance | BindingFlags.NonPublic);
        takeCollectorMethod = t.GetMethod("TakeCollector", BindingFlags.Instance | BindingFlags.NonPublic);
        targetMethod = t.GetMethod("Target", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void Update()
    {
        if (gm == null || overlay == null || stepField == null || visibleField == null) return;

        string step = StepName();

        // Skip must actually advance the GameManager out of CardActionDecision.
        // The overlay's Skip() only changes its local UI state.
        if (step == "DieAfterSkip" && gm.Phase == CardadoGamePhase.CardActionDecision)
        {
            int player = GetInt(piField, -1);
            if (player >= 0) gm.TrySkipCardAction(player);
            SetVisible(true);
            return;
        }

        // Replace the source-card selection screens with blind selection.
        if (step == "CollectorCard" || step == "SpecialCollectorTake")
        {
            SetVisible(false);
            return;
        }

        // The existing Collector fix owns executioner screens for cards stolen
        // by Special Collector. Leave those screens alone; this component owns
        // the normal turn and regular-Collector cases.
        if ((step == "ExecutionerTarget" || step == "SpecialExecutionerTarget") && !IsSpecialCollectorStolen())
        {
            SetVisible(false);
            return;
        }

        // If the player backed out of an uncommitted stolen-card effect, the
        // overlay returns to Cards. Put the stolen card back into its owner and
        // return to the appropriate blind Collector selection screen instead.
        if (regularStolen && step == "Cards")
        {
            int source = regularStolenFrom;
            RestoreRegularStolenCard();
            SetInt(tiField, source);
            SetStep("CollectorCard");
            SetVisible(true);
            return;
        }

        // A committed stolen card leaves the turn flow normally.
        if (regularStolen && step == "None" && GetActive() == null)
        {
            regularStolen = false;
            regularStolenFrom = -1;
            regularStolenCard = null;
        }
    }

    void OnGUI()
    {
        if (gm == null || overlay == null || stepField == null) return;
        Styles();
        string step = StepName();

        if (step == "CollectorCard")
        {
            DrawRegularCollectorCards();
            return;
        }

        if (step == "SpecialCollectorTake")
        {
            DrawSpecialCollectorTake();
            return;
        }

        if ((step == "ExecutionerTarget" || step == "SpecialExecutionerTarget") && !IsSpecialCollectorStolen())
        {
            DrawExecutionerTarget(step == "SpecialExecutionerTarget");
            return;
        }
    }

    void DrawRegularCollectorCards()
    {
        int target = GetInt(tiField, -1);
        if (target < 0 || target >= gm.Players.Count) return;
        var hand = gm.Players[target].hand.cardsInHand;

        Rect r = Box(820, 430);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 780, 45), "COLLECTOR — STEAL FROM " + gm.Players[target].playerId, title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 780, 30), "Blind steal: choose a card position without seeing its identity.", GUI.skin.label);

        for (int i = 0; i < hand.Count; i++)
        {
            if (GUI.Button(new Rect(r.x + 25 + (i % 5) * 155, r.y + 115 + (i / 5) * 80, 140, 65), "CARD " + (i + 1), button))
                SelectRegularCollectorCard(i, target);
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 345, 770, 55), "BACK", button))
        {
            SetStep("CollectorTarget");
            SetInt(tiField, -1);
        }
    }

    void DrawSpecialCollectorTake()
    {
        var opponents = collectorOppField == null ? null : collectorOppField.GetValue(overlay) as IList;
        int pos = GetInt(collectorPosField, 0);
        if (opponents == null || pos < 0 || pos >= opponents.Count) return;
        int target = Convert.ToInt32(opponents[pos]);
        if (target < 0 || target >= gm.Players.Count) return;
        var hand = gm.Players[target].hand.cardsInHand;

        Rect r = Box(820, 400);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 780, 45), "SPECIAL COLLECTOR — TAKE FROM " + gm.Players[target].playerId, title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 780, 30), "Blind steal: choose a card position without seeing its identity.", GUI.skin.label);

        for (int i = 0; i < hand.Count; i++)
        {
            if (GUI.Button(new Rect(r.x + 25 + i * 155, r.y + 125, 140, 65), "CARD " + (i + 1), button))
                SelectSpecialCollectorCard(i);
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 285, 770, 55), "BACK", button))
            SetStep("SpecialCollectorChoice");
    }

    void DrawExecutionerTarget(bool special)
    {
        Rect r = Box(920, 450);
        GUI.Box(r, "", box);
        string head = special ? "SPECIAL EXECUTIONER — CHOOSE OPPONENT" : "EXECUTIONER — CHOOSE TARGET";
        string help = special
            ? "Choose a player. Their entire hand will be discarded."
            : "Choose a player. If they have not played yet, they are blocked this turn; otherwise choose a permanent effect to cancel.";
        GUI.Label(new Rect(r.x + 20, r.y + 20, 880, 45), head, title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 880, 45), help, GUI.skin.label);

        float x = r.x + 25;
        int current = GetInt(piField, -1);
        for (int p = 0; p < gm.Players.Count; p++)
        {
            if (p == current) continue;
            string text = gm.Players[p].playerId
                + "\nCards: " + gm.Players[p].hand.cardsInHand.Count
                + "\nChips: " + gm.Players[p].chips;
            if (GUI.Button(new Rect(x, r.y + 140, 175, 115), text, button))
                if (targetMethod != null) targetMethod.Invoke(overlay, new object[] { p });
            x += 190;
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 335, 870, 55), "BACK", button))
        {
            SetStep("Cards");
            SetInt(tiField, -1);
        }
    }

    void SelectRegularCollectorCard(int index, int target)
    {
        if (collectorMethod == null) return;
        var hand = gm.Players[target].hand.cardsInHand;
        if (index < 0 || index >= hand.Count) return;

        regularStolen = true;
        regularStolenFrom = target;
        regularStolenCard = hand[index];
        collectorMethod.Invoke(overlay, new object[] { regularStolenCard });
    }

    void SelectSpecialCollectorCard(int index)
    {
        if (takeCollectorMethod == null) return;
        takeCollectorMethod.Invoke(overlay, new object[] { index });
    }

    bool IsSpecialCollectorStolen()
    {
        return selectedFromCollectorField != null
            && Convert.ToBoolean(selectedFromCollectorField.GetValue(overlay));
    }

    void RestoreRegularStolenCard()
    {
        if (!regularStolen || regularStolenCard == null) return;
        if (regularStolenFrom >= 0 && regularStolenFrom < gm.Players.Count)
        {
            if (!gm.Players[regularStolenFrom].hand.cardsInHand.Contains(regularStolenCard))
                gm.Players[regularStolenFrom].hand.AddCard(regularStolenCard);
        }

        SetActive(null);
        regularStolen = false;
        regularStolenFrom = -1;
        regularStolenCard = null;
    }

    object GetActive() => activeField == null ? null : activeField.GetValue(overlay);

    string StepName()
    {
        object value = stepField.GetValue(overlay);
        return value == null ? string.Empty : value.ToString();
    }

    void SetStep(string name)
    {
        stepField.SetValue(overlay, Enum.Parse(stepField.FieldType, name));
    }

    void SetVisible(bool value) => visibleField.SetValue(overlay, value);
    void SetActive(CardInstance card) { if (activeField != null) activeField.SetValue(overlay, card); }
    int GetInt(FieldInfo field, int fallback) => field == null ? fallback : Convert.ToInt32(field.GetValue(overlay));
    void SetInt(FieldInfo field, int value) { if (field != null) field.SetValue(overlay, value); }

    Rect Box(float w, float h) => new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

    void Styles()
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
    }
}
