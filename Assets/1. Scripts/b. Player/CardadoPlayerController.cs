using System;
using UnityEngine;

/// <summary>
/// Base controller for a Cardado player.
/// Controllers choose/request actions; CardadoGameManager remains authoritative for rules and state mutation.
/// </summary>
public abstract class CardadoPlayerController : MonoBehaviour
{
    private CardadoGameManager gameManager;
    private int playerIndex = -1;

    public CardadoGameManager GameManager => gameManager;
    public int PlayerIndex => playerIndex;
    public bool IsBound => gameManager != null && playerIndex >= 0;
    public bool IsCurrentPlayer => IsBound && gameManager.CurrentHandPlayerIndex == playerIndex;
    public CardadoPlayerState PlayerState => IsBound ? gameManager.Players[playerIndex] : null;

    /// <summary>
    /// Associates this controller with one player in the current game.
    /// Only the game setup/ownership layer should perform this binding.
    /// </summary>
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

    /// <summary>
    /// Called once after the controller has been associated with its player.
    /// </summary>
    protected virtual void OnBound()
    {
    }

    /// <summary>
    /// Clears the association so the controller can be reused by a future match setup.
    /// </summary>
    public void Unbind()
    {
        gameManager = null;
        playerIndex = -1;
        OnUnbound();
    }

    protected virtual void OnUnbound()
    {
    }

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

    protected virtual void Update()
    {
    }
}
