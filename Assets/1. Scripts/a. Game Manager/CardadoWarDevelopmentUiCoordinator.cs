using System.Reflection;
using UnityEngine;

/// <summary>
/// Coordinates the temporary War IMGUI with the card-action development overlay.
/// The War manager owns claim/target/wager/order presentation; during actual War
/// hands the card-action overlay owns the interactive card/die presentation.
/// </summary>
public class CardadoWarDevelopmentUiCoordinator : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private CardadoWarManager warManager;
    private FieldInfo uiStepField;
    private FieldInfo showTemporaryUiField;
    private object lastUiStep;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<CardadoGameManager>();
        warManager = FindFirstObjectByType<CardadoWarManager>();
        if (warManager == null)
            return;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        uiStepField = typeof(CardadoWarManager).GetField("uiStep", flags);
        showTemporaryUiField = typeof(CardadoWarManager).GetField("showTemporaryUi", flags);
    }

    private void LateUpdate()
    {
        if (gameManager == null || warManager == null || showTemporaryUiField == null || uiStepField == null)
            return;

        if (gameManager.Phase != CardadoGamePhase.WarResolution)
        {
            SetWarUiVisible(true);
            return;
        }

        object currentUiStep = uiStepField.GetValue(warManager);
        if (!Equals(currentUiStep, lastUiStep))
        {
            lastUiStep = currentUiStep;
            bool isPlaying = currentUiStep != null && currentUiStep.ToString() == "Playing";
            SetWarUiVisible(!isPlaying);
        }
    }

    private void OnDisable()
    {
        SetWarUiVisible(true);
    }

    private void SetWarUiVisible(bool visible)
    {
        if (warManager != null && showTemporaryUiField != null)
            showTemporaryUiField.SetValue(warManager, visible);
    }
}
