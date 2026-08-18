public class CardInstance
{
    public CardData data { get; private set; }
    public bool isPlayed;

    public CardInstance(CardData cardData)
    {
        data = cardData;
        isPlayed = false;
    }
}

