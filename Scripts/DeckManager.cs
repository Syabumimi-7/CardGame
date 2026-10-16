using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("3種類のカードプール")]
    public List<CardData> balancePool = new List<CardData>();
    public List<CardData> attackHeavyPool = new List<CardData>();
    public List<CardData> defenseHeavyPool = new List<CardData>();
    public List<CardInstance> currentDeck = new List<CardInstance>();

    public List<CardData> GetPool(int poolType)
    {
        return poolType == 1 ? attackHeavyPool : poolType == 2 ? defenseHeavyPool : balancePool;
    }

    public void CreateInitialDeck(int poolType)
    {
        currentDeck.Clear();
        List<CardData> pool = GetPool(poolType);
        AddRandomCards(pool, CardCategory.Attack, 5);
        AddRandomCards(pool, CardCategory.Defense, 2);
        AddRandomCards(pool, CardCategory.Buff, 2);
        AddRandomCards(pool, CardCategory.Debuff, 1);
    }

    private void AddRandomCards(List<CardData> pool, CardCategory category, int count)
    {
        var lottery = new CardRarityLottery(pool, category);
        for (int i = 0; i < count; i++)
        {
            CardData card = lottery.Draw();
            if (card == null) break;
            currentDeck.Add(new CardInstance(card));
        }
    }

    public void ResetAllCardPerformances()
    {
        foreach (CardInstance card in currentDeck) card.ResetPerformance();
    }

    public void ResetCardsForBattle()
    {
        foreach (CardInstance card in currentDeck) card.ResetForBattle();
    }
}
