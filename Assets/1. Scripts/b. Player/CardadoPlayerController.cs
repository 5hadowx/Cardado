using System;
using UnityEngine;

/// <summary>
/// Base controller for a Cardado player.
/// Controllers choose/request actions; CardadoGameManager remains authoritative for rules and state mutation.
/// </summary>
public abstract class CardadoPlayerController : MonoBehaviour
{
    [SerializeField, Min(0)] private int playerIndex;

    private CardadoGameManager gameManager;

    public CardadoGameManager GameManager => gameManager;
    public int PlayerIndex => playerIndex;
    public bool IsBound => gameManager != null && playerIndex >= 0;
    public bool IsCurrentPlayer => IsBound && gameManager.CurrentHandPlayerIndex == playerIndex;
    public CardadoPlayerState PlayerState => IsBound ? gameManager.Players[playerIndex] : null;

    public void Bind(CardadoGameManager manager, int index)
    {
        if (manager == null)
            throw new ArgumentNullException(nameof(manager));
        if (index < 0 || index >= manager.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (gameManager != null && gameManager != manager)
            throw new InvalidOperationException("This controller is already bound to a different Cardado game.");

        gameManager = manager;
        playerIndex = index;
        OnBound();
    }

    protected virtual void OnBound() { }

    public void Unbind()
    {
        gameManager = null;
        playerIndex = -1;
        OnUnbound();
    }

    protected virtual void OnUnbound() { }

    protected bool RequestPrediction(int dicePrediction)
    {
        return IsBound && gameManager.TryPlaceDicePrediction(playerIndex, dicePrediction);
    }

    protected bool RequestPlayCard(int cardIndex)
    {
        return IsBound && gameManager.TryPlayCard(playerIndex, cardIndex);
    }

    protected bool RequestSkipCardAction()
    {
        return IsBound && gameManager.TrySkipCardAction(playerIndex);
    }

    protected bool RequestPlayDie(int dieIndex)
    {
        return IsBound && gameManager.TryPlayDie(playerIndex, dieIndex);
    }

    protected bool RequestResolveArtistDie(int dieIndex)
    {
        return IsBound && gameManager.TryResolveArtistDie(playerIndex, dieIndex);
    }

    protected virtual void Update() { }
}
