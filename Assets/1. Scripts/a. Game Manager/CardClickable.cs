using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class CardClickable : MonoBehaviour, IPointerClickHandler
{
    private CardInstance card;
    private Action<CardInstance> onClicked;

    public void Init(CardInstance cardInstance, Action<CardInstance> clickCallback)
    {
        card = cardInstance;
        onClicked = clickCallback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClicked?.Invoke(card);
    }
}

