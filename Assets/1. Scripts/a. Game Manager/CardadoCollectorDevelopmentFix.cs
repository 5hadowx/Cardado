using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Development-only fixes for cards stolen by Special Collector.
/// Keeps a stolen card selection reversible until the stolen card effect is actually used.
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
    MethodInfo routeStolenMethod;
    MethodInfo targetMethod;

    bool selectedFromCollector;
    CardInstance selectedStolenCard;
    readonly List<CardInstance> stolenSnapshot = new List<CardInstance>();

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
        activeField = t.GetField("active", BindingFlags.Instance | BindingFlags.NonPublic);
        collectorPoolField = t.GetField("collectorPool", BindingFlags.Instance | BindingFlags.NonPublic);
        routeStolenMethod = t.GetMethod("RouteStolen", BindingFlags.Instance | BindingFlags.NonPublic);
        targetMethod = t.GetMethod("Target", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void Update()
    {
        if (gm == null || overlay == null || stepField == null || visibleField == null) return;

        string step = GetStepName();

        if (step == "SpecialCollectorPlay")
        {
            CaptureCollectorPool();
            SetOverlayVisible(false);
        }
        else if (step == "SpecialExecutionerTarget" && selectedFromCollector)
        {
            SetOverlayVisible(false);
        }
        else if (step == "ExecutionerTarget")
        {
            SetOverlayVisible(false);
        }
        else if (selectedFromCollector && step != "SpecialExecutionerTarget")
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

        if (step == "SpecialExecutionerTarget" && selectedFromCollector)
        {
            DrawExecutionerTarget(true);
            return;
        }

        if (step == "ExecutionerTarget")
            DrawExecutionerTarget(false);
    }

    void CaptureCollectorPool()
    {
        if (collectorPoolField == null) return;
        var pool = collectorPoolField.GetValue(overlay) as List<CardInstance>;
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
        GUI.Label(new Rect(r.x + 20, r.y + 70, 860, 30), "You may go back until the stolen card effect is actually used.", GUI.skin.label);

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

        // Route the stolen card without calling PlayCollector(). That method clears the
        // stolen-card pool immediately, which made Back impossible. Unselected cards stay
        // in the pool until the chosen stolen-card effect is actually used.
        if (routeStolenMethod != null)
            routeStolenMethod.Invoke(overlay, new object[] { c });
    }

    void DrawExecutionerTarget(bool stolen)
    {
        Rect r = Box(900, 420);
        GUI.Box(r, "", box);
        string head = stolen ? "SPECIAL EXECUTIONER — CHOOSE OPPONENT" : "EXECUTIONER — CHOOSE OPPONENT";
        GUI.Label(new Rect(r.x + 20, r.y + 20, 860, 45), head, title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 860, 35),
            "Choose a player to discard their entire hand. Card count is shown for each player.", GUI.skin.label);

        float x = r.x + 25;
        for (int p = 0; p < gm.Players.Count; p++)
        {
            if (p == gm.CurrentHandPlayerIndex) continue;
            string label = gm.Players[p].playerId + "\nCards: " + gm.Players[p].hand.cardsInHand.Count + "\nChips: " + gm.Players[p].chips;
            if (GUI.Button(new Rect(x, r.y + 135, 160, 100), label, button))
            {
                if (stolen) DiscardUnselectedStolenCards();
                selectedFromCollector = false;
                selectedStolenCard = null;
                stolenSnapshot.Clear();
                InvokeTarget(p);
            }
            x += 175;
        }

        if (GUI.Button(new Rect(r.x + 25, r.y + 320, 850, 55), "BACK", button))
        {
            if (stolen)
            {
                RestoreCollectorPool();
                SetOverlayStep("SpecialCollectorPlay");
                SetOverlayVisible(true);
            }
            else
            {
                SetOverlayStep("Cards");
                SetOverlayVisible(true);
            }
        }
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

    void RestoreCollectorPool()
    {
        if (collectorPoolField == null) return;
        var pool = collectorPoolField.GetValue(overlay) as List<CardInstance>;
        if (pool == null) return;
        pool.Clear();
        pool.AddRange(stolenSnapshot);
        SetOverlayActive(null);
        selectedFromCollector = false;
        selectedStolenCard = null;
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
    }
}
