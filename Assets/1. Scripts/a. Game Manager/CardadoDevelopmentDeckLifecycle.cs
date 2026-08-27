using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary development-only deck lifecycle helper.
/// At the end of each round, cards that were not played are still in player hands.
/// Move those cards into the deck discard pile so the discard can be recycled and
/// shuffled when the next round needs cards.
/// </summary>
public class CardadoDevelopmentDeckLifecycle : MonoBehaviour
{
    private CardadoGameManager gameManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CardadoGameManager manager = FindFirstObjectByType<CardadoGameManager>();
        if (manager == null)
            return;

        if (manager.GetComponent<CardadoDevelopmentDeckLifecycle>() == null)
            manager.gameObject.AddComponent<CardadoDevelopmentDeckLifecycle>();
    }

    private void Awake()
    {
        gameManager = GetComponent<CardadoGameManager>();
        if (gameManager != null)
            gameManager.RoundResolutionCompleted += OnRoundResolutionCompleted;
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.RoundResolutionCompleted -= OnRoundResolutionCompleted;
    }

    private void OnRoundResolutionCompleted()
    {
        if (gameManager == null || gameManager.RoundDeck == null)
            return;

        int returnedCards = 0;

        foreach (CardadoPlayerState player in gameManager.Players)
        {
            if (player == null || player.hand == null || player.hand.cardsInHand == null)
                continue;

            List<CardInstance> remainingCards = new List<CardInstance>(player.hand.cardsInHand);
            player.hand.cardsInHand.Clear();

            foreach (CardInstance card in remainingCards)
            {
                if (card == null)
                    continue;

                // A card that survived the round is becoming available again.
                card.isPlayed = false;
                gameManager.DiscardResolvedCard(card);
                returnedCards++;
            }
        }

        Debug.Log($"[Cardado] End of round: returned {returnedCards} unplayed card(s) from player hands to the discard pile. " +
                  $"Draw pile before next deal: {gameManager.RoundDeck.Count}, discard pile: {gameManager.RoundDeck.DiscardCount}.");
    }
}
