using System.Text;
using UnityEngine;

/// <summary>
/// Runtime-only development logger that prints the exact cards dealt to each player
/// and the centralized War eligibility result during WarResolution.
/// It creates itself automatically after the scene loads, so no scene setup is needed.
/// </summary>
public class CardadoDevelopmentCardLogger : MonoBehaviour
{
    private CardadoGameManager gameManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeLogger()
    {
        if (FindFirstObjectByType<CardadoDevelopmentCardLogger>() != null)
            return;

        GameObject loggerObject = new GameObject("CardadoDevelopmentCardLogger");
        DontDestroyOnLoad(loggerObject);
        loggerObject.AddComponent<CardadoDevelopmentCardLogger>();
    }

    private void Awake()
    {
        // Subscribe in Awake, not Start. The development tester can begin the
        // match from Start, so waiting until Start could miss PlayerHandDealt.
        gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager == null)
            return;

        gameManager.PlayerHandDealt += OnPlayerHandDealt;
        gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void Start()
    {
        // Safety net: if the logger was created after the hands were already dealt,
        // print the current hands once anyway.
        if (gameManager == null)
            gameManager = FindFirstObjectByType<CardadoGameManager>();

        if (gameManager == null)
            return;

        foreach (CardadoPlayerState player in gameManager.Players)
        {
            if (player != null && player.hand != null && player.hand.cardsInHand != null && player.hand.cardsInHand.Count > 0)
                LogPlayerHand(player);
        }
    }

    private void OnDestroy()
    {
        if (gameManager == null)
            return;

        gameManager.PlayerHandDealt -= OnPlayerHandDealt;
        gameManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPlayerHandDealt(CardadoPlayerState player)
    {
        LogPlayerHand(player);
    }

    private void LogPlayerHand(CardadoPlayerState player)
    {
        if (player == null || player.hand == null || player.hand.cardsInHand == null)
        {
            Debug.Log("[Cardado] Player hand dealt: no readable cards.");
            return;
        }

        StringBuilder details = new StringBuilder();

        foreach (CardInstance card in player.hand.cardsInHand)
        {
            if (details.Length > 0)
                details.Append(" | ");

            if (card == null || card.data == null)
            {
                details.Append("NULL CARD");
                continue;
            }

            details.Append(card.data.id);
            details.Append(" [");
            details.Append(card.data.cardType);
            details.Append(", ");
            details.Append(card.data.rarity);
            details.Append("]");
        }

        Debug.Log($"[Cardado] {player.playerId} dealt their initial hand: {details}");
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase != CardadoGamePhase.WarResolution || gameManager == null)
            return;

        Debug.Log("[Cardado] Centralized War eligibility check:");

        foreach (CardadoPlayerState player in gameManager.Players)
        {
            bool eligible = player != null &&
                            player.hand != null &&
                            CardadoWarRules.HasWarClaim(player.hand.cardsInHand);

            Debug.Log($"[Cardado] {player.playerId}: War eligible = {eligible}.");
        }
    }
}
