using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Development-only fixes layered on top of CardadoCardActionDevelopmentOverlayV2.
///
/// This keeps the existing card-action overlay intact while fixing two UI flows that
/// need per-player/per-die state, and permanently removes dice once a hand is resolved.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class CardadoCardActionDevelopmentFixes : MonoBehaviour
{
    CardadoGameManager gm;
    CardadoCardActionDevelopmentOverlayV2 overlay;

    FieldInfo stepField;
    FieldInfo visibleField;
    FieldInfo modField;
    MethodInfo canAffectMethod;
    MethodInfo commitMethod;

    readonly HashSet<string> currentHandPlayed = new HashSet<string>();
    readonly List<int> specialSoldierOpponents = new List<int>();
    int specialSoldierPos;
    string lastStep;

    GUIStyle box, title, button, small, value;

    void Awake()
    {
        gm = GetComponent<CardadoGameManager>();
        if (gm == null) gm = FindFirstObjectByType<CardadoGameManager>();

        overlay = GetComponent<CardadoCardActionDevelopmentOverlayV2>();
        if (overlay == null) overlay = FindFirstObjectByType<CardadoCardActionDevelopmentOverlayV2>();

        if (overlay != null)
        {
            Type t = overlay.GetType();
            stepField = t.GetField("step", BindingFlags.Instance | BindingFlags.NonPublic);
            visibleField = t.GetField("visible", BindingFlags.Instance | BindingFlags.NonPublic);
            modField = t.GetField("mod", BindingFlags.Instance | BindingFlags.NonPublic);
            canAffectMethod = t.GetMethod("CanAffect", BindingFlags.Instance | BindingFlags.NonPublic);
            commitMethod = t.GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    void OnEnable()
    {
        if (gm == null) gm = FindFirstObjectByType<CardadoGameManager>();
        if (gm == null) return;
        gm.DiePlayed += OnDiePlayed;
        gm.HandCompleted += OnHandCompleted;
        gm.RoundResolutionCompleted += OnRoundEnded;
    }

    void OnDisable()
    {
        if (gm == null) return;
        gm.DiePlayed -= OnDiePlayed;
        gm.HandCompleted -= OnHandCompleted;
        gm.RoundResolutionCompleted -= OnRoundEnded;
    }

    void Update()
    {
        if (gm == null || overlay == null || stepField == null) return;

        string step = GetStepName();
        if (step != lastStep)
        {
            lastStep = step;
            if (step == "SpecialSoldierAllDie") BeginSpecialSoldierFlow();
            else if (step != "SpecialSoldierAllDie") specialSoldierOpponents.Clear();
        }

        if (step == "SpecialSoldierAllDie" || step == "ModifierTarget")
            SetOverlayVisible(false);
    }

    void OnGUI()
    {
        if (gm == null) return;
        Styles();
        DrawTurnStatus();

        if (overlay == null || stepField == null) return;

        string step = GetStepName();
        if (step == "SpecialSoldierAllDie")
            DrawSpecialSoldierAll();
        else if (step == "ModifierTarget")
            DrawModifierTarget();
    }

    void DrawTurnStatus()
    {
        if (gm.CurrentHandPlayerIndex < 0 || gm.CurrentHandPlayerIndex >= gm.Players.Count) return;
        if (gm.Phase != CardadoGamePhase.CardActionDecision && gm.Phase != CardadoGamePhase.PlayingHands) return;

        CardadoPlayerState p = gm.Players[gm.CurrentHandPlayerIndex];
        Rect r = new Rect(Screen.width - 245f, 15f, 230f, 105f);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 28f), p.playerId, title);
        GUI.Label(new Rect(r.x + 15f, r.y + 42f, r.width - 30f, 25f), "Prediction: " + p.diceBid, small);
        GUI.Label(new Rect(r.x + 15f, r.y + 68f, r.width - 30f, 25f), "Dice won: " + p.handsWon, small);
    }

    void DrawSpecialSoldierAll()
    {
        if (gm.CurrentHandPlayerIndex < 0) return;
        int actor = gm.CurrentHandPlayerIndex;
        int opponent = GetCurrentSpecialSoldierOpponent(actor);

        if (opponent < 0)
        {
            CommitOverlayCard();
            return;
        }

        Rect r = Box(900, 440);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20, r.y + 20, 860f, 45f), "SPECIAL SOLDIER — CHOOSE DIE", title);
        GUI.Label(new Rect(r.x + 20, r.y + 70, 860f, 35f),
            "Choose one die for " + gm.Players[opponent].playerId + " to reroll.", GUI.skin.label);

        int shown = 0;
        for (int d = 0; d < gm.Players[opponent].dice.Count; d++)
        {
            if (!CanAffect(opponent, d)) continue;

            Rect q = new Rect(r.x + 25f + shown * 125f, r.y + 135f, 110f, 95f);
            if (GUI.Button(q, "Die " + (d + 1) + "\nVALUE " + gm.Players[opponent].dice[d], button))
            {
                gm.Players[opponent].dice[d] = UnityEngine.Random.Range(1, 7);
                specialSoldierPos++;
                AdvanceSpecialSoldier();
            }
            shown++;
        }

        if (shown == 0)
        {
            GUI.Label(new Rect(r.x + 25f, r.y + 145f, 850f, 35f),
                "No targetable dice for this opponent.", GUI.skin.label);
            if (GUI.Button(new Rect(r.x + 25f, r.y + 205f, 850f, 55f), "CONTINUE", button))
            {
                specialSoldierPos++;
                AdvanceSpecialSoldier();
            }
        }

        if (GUI.Button(new Rect(r.x + 25f, r.y + 330f, 850f, 55f), "BACK", button))
            SetOverlayStep("SpecialSoldierChoice");
    }

    void BeginSpecialSoldierFlow()
    {
        specialSoldierOpponents.Clear();
        specialSoldierPos = 0;

        int actor = gm.CurrentHandPlayerIndex;
        for (int p = 0; p < gm.Players.Count; p++)
            if (p != actor) specialSoldierOpponents.Add(p);

        AdvanceSpecialSoldier();
    }

    int GetCurrentSpecialSoldierOpponent(int actor)
    {
        while (specialSoldierPos < specialSoldierOpponents.Count)
        {
            int p = specialSoldierOpponents[specialSoldierPos];
            for (int d = 0; d < gm.Players[p].dice.Count; d++)
                if (CanAffect(p, d)) return p;
            specialSoldierPos++;
        }
        return -1;
    }

    void AdvanceSpecialSoldier()
    {
        if (GetCurrentSpecialSoldierOpponent(gm.CurrentHandPlayerIndex) < 0)
            CommitOverlayCard();
    }

    void DrawModifierTarget()
    {
        int direction = GetModifierDirection();
        string directionText = direction > 0 ? "+1" : "-1";

        Rect r = Box(980, 560);
        GUI.Box(r, "", box);
        GUI.Label(new Rect(r.x + 20f, r.y + 20f, 940f, 45f), "MODIFIER " + directionText + " — CHOOSE DIE", title);
        GUI.Label(new Rect(r.x + 20f, r.y + 70f, 940f, 30f),
            "Only dice still in play can be affected.", GUI.skin.label);

        float y = r.y + 110f;
        for (int p = 0; p < gm.Players.Count; p++)
        {
            GUI.Label(new Rect(r.x + 20f, y, 140f, 30f), gm.Players[p].playerId, GUI.skin.label);
            float x = r.x + 170f;

            for (int d = 0; d < gm.Players[p].dice.Count; d++)
            {
                if (!CanAffect(p, d)) continue;

                int v = gm.Players[p].dice[d];
                bool validDirection = direction > 0 ? v < 6 : v > 1;
                if (!validDirection) continue;

                Rect q = new Rect(x, y - 5f, 100f, 72f);
                if (GUI.Button(q, "Die " + (d + 1) + "\n" + v, button))
                {
                    InvokePrivate("Modifier", p, d);
                    return;
                }
                x += 112f;
            }
            y += 82f;
        }

        if (GUI.Button(new Rect(r.x + 20f, r.y + 495f, 940f, 50f), "BACK", button))
            SetOverlayStep("ModifierDirection");
    }

    int GetModifierDirection()
    {
        if (modField == null) return 1;
        object value = modField.GetValue(overlay);
        return value is int i && i < 0 ? -1 : 1;
    }

    bool CanAffect(int playerIndex, int dieIndex)
    {
        if (canAffectMethod == null) return false;
        try
        {
            object result = canAffectMethod.Invoke(overlay, new object[] { playerIndex, dieIndex });
            return result is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    void InvokePrivate(string methodName, params object[] args)
    {
        if (overlay == null) return;
        MethodInfo method = overlay.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null) method.Invoke(overlay, args);
    }

    void CommitOverlayCard()
    {
        if (commitMethod != null)
        {
            commitMethod.Invoke(overlay, null);
            return;
        }
        gm.TrySkipCardAction(gm.CurrentHandPlayerIndex);
    }

    string GetStepName()
    {
        object value = stepField.GetValue(overlay);
        return value == null ? string.Empty : value.ToString();
    }

    void SetOverlayStep(string stepName)
    {
        if (stepField == null) return;
        Type enumType = stepField.FieldType;
        object value = Enum.Parse(enumType, stepName);
        stepField.SetValue(overlay, value);
        SetOverlayVisible(true);
    }

    void SetOverlayVisible(bool state)
    {
        if (visibleField != null) visibleField.SetValue(overlay, state);
    }

    void OnDiePlayed(CardadoPlayerState player, int dieIndex, int value)
    {
        int p = Index(player);
        if (p >= 0) currentHandPlayed.Add(Key(p, dieIndex));
    }

    void OnHandCompleted(int winnerPlayerIndex, int winningValue)
    {
        // A resolved hand is permanently out of play. Keep the values at 0 so all
        // existing card-targeting code naturally stops exposing these dice.
        foreach (string key in currentHandPlayed)
        {
            int[] parsed = Parse(key);
            if (parsed[0] < 0 || parsed[1] < 0) continue;
            if (parsed[0] >= gm.Players.Count) continue;
            if (parsed[1] >= gm.Players[parsed[0]].dice.Count) continue;
            gm.Players[parsed[0]].dice[parsed[1]] = 0;
        }
        currentHandPlayed.Clear();
    }

    void OnRoundEnded()
    {
        currentHandPlayed.Clear();
        specialSoldierOpponents.Clear();
        specialSoldierPos = 0;
    }

    int Index(CardadoPlayerState player)
    {
        for (int i = 0; i < gm.Players.Count; i++)
            if (gm.Players[i] == player) return i;
        return -1;
    }

    int[] Parse(string key)
    {
        string[] parts = key.Split(':');
        int p, d;
        if (parts.Length != 2 || !int.TryParse(parts[0], out p) || !int.TryParse(parts[1], out d))
            return new[] { -1, -1 };
        return new[] { p, d };
    }

    string Key(int p, int d) => p + ":" + d;

    Rect Box(float width, float height) => new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

    void Styles()
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        small = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        value = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }
}
