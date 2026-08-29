using System.Reflection;
using UnityEngine;

/// <summary>
/// Development-only bridge for the V2 card-action overlay.
///
/// The overlay deliberately keeps the skipped-card screen visible so the player
/// can still press BACK. The game manager, however, remains in CardActionDecision
/// until the die is actually committed. This bridge completes that transition at
/// the moment the player clicks a die, then delegates the real die play to the
/// game manager.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class CardadoCardActionDevelopmentSkipBridge : MonoBehaviour
{
    CardadoGameManager gm;
    Component overlay;
    FieldInfo stepField;
    FieldInfo visibleField;
    FieldInfo playerIndexField;

    void Awake()
    {
        gm = FindFirstObjectByType<CardadoGameManager>();
        if (gm == null) return;

        overlay = gm.GetComponent("CardadoCardActionDevelopmentOverlayV2");
        if (overlay == null) return;

        System.Type type = overlay.GetType();
        stepField = type.GetField("step", BindingFlags.Instance | BindingFlags.NonPublic);
        visibleField = type.GetField("visible", BindingFlags.Instance | BindingFlags.NonPublic);
        playerIndexField = type.GetField("pi", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void OnGUI()
    {
        if (gm == null || overlay == null || stepField == null || visibleField == null || playerIndexField == null)
            return;

        if (!(visibleField.GetValue(overlay) is bool visible) || !visible)
            return;

        object step = stepField.GetValue(overlay);
        if (step == null || step.ToString() != "DieAfterSkip")
            return;

        if (!(playerIndexField.GetValue(overlay) is int playerIndex))
            return;

        if (playerIndex < 0 || playerIndex >= gm.Players.Count)
            return;

        var player = gm.Players[playerIndex];
        float panelWidth = 760f;
        float panelHeight = 410f;
        float panelX = (Screen.width - panelWidth) / 2f;
        float panelY = (Screen.height - panelHeight) / 2f;
        float startX = panelX + 25f;
        float y = panelY + 135f;

        int shown = 0;
        for (int dieIndex = 0; dieIndex < player.dice.Count; dieIndex++)
        {
            if (!gm.IsDieAvailable(playerIndex, dieIndex))
                continue;

            Rect buttonRect = new Rect(startX + shown * 105f, y, 90f, 80f);
            shown++;

            // Invisible overlay button: the existing V2 UI remains responsible
            // for rendering the die. This only repairs the state transition.
            if (!GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                continue;

            // Move the manager into PlayingHands, then use its authoritative
            // die-play method. The V2 overlay will receive the normal turn
            // events and advance to the next player.
            if (!gm.TrySkipCardAction(playerIndex))
                return;

            gm.TryPlayDie(playerIndex, dieIndex);
            return;
        }
    }
}
