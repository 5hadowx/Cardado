using UnityEngine;

/// <summary>
/// Development notification hook for War card resolution. War remains authoritative
/// in CardadoWarManager; this only provides a clear console trace after an interactive
/// War card choice has completed.
/// </summary>
public static class CardadoWarCardActionResolvedExtension
{
    public static void NotifyWarCardActionResolved(this CardadoGameManager gameManager,
        CardadoPlayerState player, CardInstance card)
    {
        if (player == null || card == null || card.data == null) return;
        Debug.Log($"[Cardado][War] {player.playerId} resolved War card: {card.data.id} [{card.data.cardType}]");
    }
}
