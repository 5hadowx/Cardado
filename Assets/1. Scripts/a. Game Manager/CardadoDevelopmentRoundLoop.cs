using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary development-only loop helper.
/// When the development match returns to RoundSetupRoll after a completed round,
/// automatically rolls the setup dice so the temporary tester can continue the
/// match without requiring another manual trigger.
/// </summary>
public class CardadoDevelopmentRoundLoop : MonoBehaviour
{
    private bool rollScheduled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CardadoGameManager gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager == null)
            return;

        if (gameManager.GetComponent<CardadoDevelopmentRoundLoop>() == null)
            gameManager.gameObject.AddComponent<CardadoDevelopmentRoundLoop>();
    }

    private void OnEnable()
    {
        CardadoGameManager gameManager = GetComponent<CardadoGameManager>();
        if (gameManager != null)
            gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        CardadoGameManager gameManager = GetComponent<CardadoGameManager>();
        if (gameManager != null)
            gameManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase != CardadoGamePhase.RoundSetupRoll || rollScheduled)
            return;

        rollScheduled = true;
        StartCoroutine(RollSetupOnNextFrame());
    }

    private IEnumerator RollSetupOnNextFrame()
    {
        yield return null;
        rollScheduled = false;

        CardadoGameManager gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null || gameManager.Phase != CardadoGamePhase.RoundSetupRoll)
            yield break;

        try
        {
            Debug.Log("[Cardado] Development loop: automatically rolling setup dice for the next round.");
            gameManager.RollRoundSetupDice();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
