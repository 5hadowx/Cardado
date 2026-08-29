using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Development-only fixes for cards stolen by Special Collector.
/// Keeps stolen-card selection reversible until the stolen card effect is actually committed.
/// Also adds hand-card counts to Executioner target screens.
/// </summary>
[DefaultExecutionOrder(-900)]
public class CardadoCollectorDevelopmentFix : MonoBehaviour
{
    CardadoGameManager gm;
    CardadoCardActionDevelopmentOverlayV2 overlay;

    FieldInfo stepField;
    FieldInfo visibleField;
    FieldInfo activeField;
    FieldInfo collectorPoolField;
    FieldInfo execEffectsField;
    MethodInfo routeStolenMethod;
    MethodInfo targetMethod;
    MethodInfo cancelEffectMethod;
    MethodInfo effectTextMethod;

    bool suppressOriginal;
    bool selectedFromCollector;
    CardInstance selectedStolenCard;
    readonly List<CardInstance> stolenSnapshot = new List<CardInstance>();

    GUIStyle box, title, button, small;

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
        activeField = t.GetField("active", BindingFlags.Instance | BindingFlags.NonPublic);
        collectorPoolField = t.GetField("collectorPool", BindingFlags.Instance | BindingFlags.NonPublic);
        execEffectsField = t.GetField("execEffects", BindingFlags.Instance | BindingFlags.NonPublic);
        routeStolenMethod = t.GetMethod("RouteStolen", BindingFlags.Instance | BindingFlags.NonPublic);
        targetMethod = t.GetMethod("Target", BindingFlags.Instance | BindingFlags.NonPublic);
        cancelEffectMethod = t.GetMethod("CancelEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        effectTextMethod = t.GetMethod("EffectText", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void Update()
    {
        if (gm == null || overlay == null || stepField == null || visibleField == null) return;

        string step = GetStepName();
        suppressOriginal = false;

        // The original overlay must not draw its collector-card screen because it
        // assumes the stolen cards are normal hand cards. We own this screen instead.
        if (step == "SpecialCollectorPlay")
        {
            CaptureCollectorPool();
            suppressOriginal = true;
            SetOverlayVisible(false);
            return;
        }

        // A stolen Special Executioner has its own effect: discard the target's
        // entire hand. A stolen regular Executioner must use the normal Executioner
        // rules instead, so only the special step gets the discard-hand UI.
        if (selectedFromCollector && step == "SpecialExecutionerTarget")
        {
            suppressOriginal = true;
            SetOverlayVisible(false);
            return;
        }

        // A stolen regular Executioner uses the normal two-purpose Executioner flow,
        // but its BACK path must return to the stolen-card list rather than the hand.
        if (selectedFromCollector && step == "ExecutionerTarget")
        {
            suppressOriginal = true;
            SetOverlayVisible(false);
            return;
        }

        // When several permanent effects exist, the original overlay enters
        // ExecutionerEffects. Keep ownership of that screen while the card came
        // from Collector so BACK can still return to the correct stolen-card state.
        if (selectedFromCollector && step == "ExecutionerEffects")
        {
            suppressOriginal = true;
            SetOverlayVisible(false);
            return;
        }

        // Once the underlying overlay reaches None, its After()/Commit() path has
        // actually resolved the stolen card. Only now discard the other stolen cards.
        if (selectedFromCollector && step == "None")
        {
            DiscardUnselectedStolenCards();
            selectedFromCollector = false;
            selectedStolenCard = null;
            stolenSnapshot.Clear();
            return;
        }

        // Any other transition means the stolen-card flow has finished or was reset.
        if (selectedFromCollector)
        {
            selectedFromCollector = false;
            selectedStolenCard = null;
            stolenSnapshot.Clear();
        }
    }

    void OnGUI()
    {
        if (gm == null || overlay == null || stepField == null) return;
        Styles();

        string step = GetStepName();
        if (step == "SpecialCollectorPlay")
        {
            DrawStolenCardSelection();
            return;
        }

        if (selectedFromCollector && step == "SpecialExecutionerTarget")
        {
            DrawExecutionerTarget(true);
            return;
        }

        if (selectedFromCollector && step == "ExecutionerTarget")
        {
            DrawExecutionerTarget(false);
            return;
        }

        if (selectedFromCollector && step == "ExecutionerEffects")
        {
            DrawExecutionerEffects();
            return;
        }
    }

    void CaptureCollectorPool()
    {
        if (collectorPoolField == null) return;
        object raw = collectorPoolField.GetValue(overlay);
        var pool = raw as List<CardInstance>;
        if (pool == null) return;

        if (stolenSnapshot.Count == 0 || !SamePool(pool, stolenSnapshot))
        {
            stolenSnapshot.Clear();
            stolenSnapshot.AddRange(pool);
        }
    }

    bool SamePool(List<CardInstance> a, List<CardInstance> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    void DrawStolenCardSelection()
    {
        Rect r = Box(900, 430);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 860, 45), "SPECIAL COLLECTOR — CHOOSE CARD TO PLAY", title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 860, 30), "You may go back until the stolen card effect is actually committed.", GUI.skin.label);

        for (int i = 0; i < stolenSnapshot.Count; i++)
        {
            CardInstance c = stolenSnapshot[i];
            if (c == null || c.data == null) continue;
            string label = c.data.id + "\n" + c.data.cardType + (c.data.isBlankCard ? "\nBlank" : "");
            if (GUI.Button(new Rect(r.x + 25 + i * 165, r.y + 125, 150, 80), label, button))
                SelectStolen(i);
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 330, 850, 55), "BACK", button))
        {
            selectedFromCollector = false;
            selectedStolenCard = null;
            stolenSnapshot.Clear();
            SetOverlayStep("SpecialCollectorTake");
            SetOverlayVisible(true);
        }
    }

    void SelectStolen(int index)
    {
        if (index < 0 || index >= stolenSnapshot.Count) return;
        CardInstance c = stolenSnapshot[index];
        if (c == null || c.data == null) return;

        selectedFromCollector = true;
        selectedStolenCard = c;
        SetOverlayActive(c);
        SetOverlayVisible(true);

        // Do not call PlayCollector(): it clears collectorPool immediately.
        // RouteStolen only chooses the appropriate effect state.
        if (routeStolenMethod != null)
            routeStolenMethod.Invoke(overlay, new object[] { c });
    }

    void DrawExecutionerTarget(bool special)
    {
        Rect r = Box(920, 450);
        GUI.Box(r, "", box);

        string head = special
            ? "SPECIAL EXECUTIONER — CHOOSE OPPONENT"
            : "EXECUTIONER — CHOOSE TARGET";
        GUI.Label(new Rect(r.x + 20, r.y + 20, 880, 45), head, title);

        string help = special
            ? "Choose a player. Their entire hand will be discarded."
            : "Choose a player to block this turn, or target a player who already played to cancel their permanent effect.";
        GUI.Label(new Rect(r.x + 20, r.y + 70, 880, 45), help, GUI.skin.label);

        float x = r.x + 25;
        for (int p = 0; p < gm.Players.Count; p++)
        {
            if (p == gm.CurrentHandPlayerIndex) continue;

            string label = gm.Players[p].playerId
                + "\nCards: " + gm.Players[p].hand.cardsInHand.Count
                + "\nChips: " + gm.Players[p].chips;

            GUI.enabled = special || CanBeRegularExecutionerTarget(p);
            if (GUI.Button(new Rect(x, r.y + 140, 175, 115), label, button))
            {
                // Do not discard the other stolen cards yet. The target selection
                // can still lead to the regular ExecutionerEffects screen, where
                // the player may press BACK.
                InvokeTarget(p);
            }
            GUI.enabled = true;
            x += 190;
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 335, 870, 55), "BACK", button))
        {
            RestoreCollectorPool();
            SetOverlayStep("SpecialCollectorPlay");
            SetOverlayVisible(true);
        }
    }

    bool CanBeRegularExecutionerTarget(int p)
    {
        // Preserve the normal Executioner semantics: a player who has not yet
        // played can be blocked. A player who already played is still a valid
        // target only when they have a permanent effect that can be cancelled.
        // The underlying Executioner() method remains authoritative for the exact
        // resolution; this check only prevents selecting the current player.
        return p >= 0 && p < gm.Players.Count && p != gm.CurrentHandPlayerIndex;
    }

    void DrawExecutionerEffects()
    {
        Rect r = Box(900, 430);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 860, 45), "EXECUTIONER — CANCEL PERMANENT EFFECT", title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 860, 35),
            "Choose an already played permanent effect to cancel.", GUI.skin.label);

        IList list = execEffectsField == null ? null : execEffectsField.GetValue(overlay) as IList;
        int count = list == null ? 0 : list.Count;

        for (int i = 0; i < count; i++)
        {
            object effect = list[i];
            string label = EffectDisplayText(effect);
            if (GUI.Button(new Rect(r.x + 25, r.y + 120 + i * 70, 850, 55), label, button))
            {
                if (cancelEffectMethod != null)
                    cancelEffectMethod.Invoke(overlay, new object[] { effect });
            }
        }

        if (count == 0)
            GUI.Label(new Rect(r.x + 25, r.y + 120, 850, 35), "No cancelable permanent effect.", GUI.skin.label);

        if (GUI.Button(new Rect(r.x + 25, r.y + 330, 850, 55), "BACK", button))
        {
            SetOverlayStep("ExecutionerTarget");
            SetOverlayVisible(true);
        }
    }

    string EffectDisplayText(object effect)
    {
        if (effectTextMethod != null && effect != null)
        {
            object result = effectTextMethod.Invoke(overlay, new[] { effect });
            if (result is string text && !string.IsNullOrEmpty(text)) return text;
        }
        return "Permanent effect";
    }

    void RestoreCollectorPool()
    {
        if (collectorPoolField == null) return;
        var pool = collectorPoolField.GetValue(overlay) as List<CardInstance>;
        if (pool == null) return;
        pool.Clear();
        pool.AddRange(stolenSnapshot);
        SetOverlayActive(null);
    }

    void DiscardUnselectedStolenCards()
    {
        for (int i = 0; i < stolenSnapshot.Count; i++)
        {
            CardInstance c = stolenSnapshot[i];
            if (c == null || c == selectedStolenCard) continue;
            gm.DiscardResolvedCard(c);
        }
    }

    void InvokeTarget(int playerIndex)
    {
        if (targetMethod != null)
            targetMethod.Invoke(overlay, new object[] { playerIndex });
    }

    string GetStepName()
    {
        object value = stepField.GetValue(overlay);
        return value == null ? string.Empty : value.ToString();
    }

    void SetOverlayStep(string name)
    {
        object value = Enum.Parse(stepField.FieldType, name);
        stepField.SetValue(overlay, value);
    }

    void SetOverlayVisible(bool state)
    {
        visibleField.SetValue(overlay, state);
    }

    void SetOverlayActive(CardInstance card)
    {
        if (activeField != null) activeField.SetValue(overlay, card);
    }

    Rect Box(float width, float height) => new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

    void Styles()
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        small = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }
}
