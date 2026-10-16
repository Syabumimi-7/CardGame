using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>レアリティを抽選し、そのレアリティの候補から等確率で選びます。</summary>
public sealed class CardRarityLottery
{
    private readonly List<CardData>[] candidates =
    {
        new List<CardData>(), new List<CardData>(), new List<CardData>(), new List<CardData>()
    };

    public CardRarityLottery(IEnumerable<CardData> pool, CardCategory? category = null)
    {
        if (pool == null) return;
        foreach (CardData card in pool)
        {
            if (card == null || (category.HasValue && card.category != category.Value)) continue;
            int rarity = (int)card.rarity;
            if (rarity >= 0 && rarity < candidates.Length && !candidates[rarity].Contains(card))
                candidates[rarity].Add(card);
        }
    }

    // 0～99の各整数は1％。存在しない上位レアリティの枠はコモンに移します。
    public static CardRarity ResolveRarity(int roll, bool hasUncommon, bool hasRare, bool hasSuperRare)
    {
        if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        if (roll < 50) return CardRarity.Common;
        if (roll < 85) return hasUncommon ? CardRarity.Uncommon : CardRarity.Common;
        if (roll < 95) return hasRare ? CardRarity.Rare : CardRarity.Common;
        return hasSuperRare ? CardRarity.SuperRare : CardRarity.Common;
    }

    public CardData Draw()
    {
        // コモンは基本50％および欠けたレアリティの受け皿として必須です。
        if (candidates[0].Count == 0)
        {
            Debug.LogError("カード抽選の対象にコモンがありません。パック／カテゴリーにコモンを登録してください。");
            return null;
        }
        CardRarity rarity = ResolveRarity(UnityEngine.Random.Range(0, 100),
            candidates[1].Count > 0, candidates[2].Count > 0, candidates[3].Count > 0);
        List<CardData> bucket = candidates[(int)rarity];
        return bucket[UnityEngine.Random.Range(0, bucket.Count)];
    }
}
