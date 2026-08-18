using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardInstance card;
    private System.Action<CardInstance> onHoverEnter;
    private System.Action<CardInstance> onHoverExit;

    public void Init(CardInstance cardData, System.Action<CardInstance> onEnter, System.Action<CardInstance> onExit)
    {
        card = cardData;
        onHoverEnter = onEnter;
        onHoverExit = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onHoverEnter?.Invoke(card);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke(card);
    }
}


