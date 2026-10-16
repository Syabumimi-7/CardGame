using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>デッキ内のカード個体。性能抽選結果はこの個体だけが保持します。</summary>
[Serializable]
public class CardInstance
{
    public CardData Data { get; private set; }
    public CardPerformance CurrentPerformance { get; private set; }
    public bool IsPerformanceDetermined { get; private set; }
    public int TimesPlayed { get; private set; }
    public int CostReduction { get; set; }
    public int EffectiveCost => Mathf.Max(0, CurrentPerformance.cost - CostReduction);
    public bool RemainsInDiscard => IsPerformanceDetermined && CurrentPerformance.effects != null
        && CurrentPerformance.effects.Exists(effect => effect.effectType == BattleEffectType.RemainInDiscard);

    public CardInstance(CardData cardData) { Data = cardData; }

    public static CardInstance CreateCollectionPreview(CardData data, int pattern)
    {
        var card = new CardInstance(data);
        card.CurrentPerformance = pattern == 0 ? data.lowPerformance : pattern == 1 ? data.mediumPerformance : data.highPerformance;
        card.IsPerformanceDetermined = true;
        return card;
    }

    public void RollPerformance()
    {
        if (IsPerformanceDetermined || Data == null) return;
        int index = UnityEngine.Random.Range(0, 3);
        CurrentPerformance = index == 0 ? Data.lowPerformance : index == 1 ? Data.mediumPerformance : Data.highPerformance;
        IsPerformanceDetermined = true;
    }

    public void ResetPerformance()
    {
        ResetForBattle();
        IsPerformanceDetermined = false;
        CurrentPerformance = default(CardPerformance);
    }

    // 確定した性能は連戦で保持し、戦闘中の使用回数・コスト軽減だけを解除します。
    public void ResetForBattle()
    {
        TimesPlayed = 0;
        CostReduction = 0;
    }

    public void RecordPlayed() { TimesPlayed++; }

    public int GetBaseValue(BattleEffect effect)
    {
        return effect.effectType == BattleEffectType.GrowingDamage
            ? effect.value + TimesPlayed * effect.damageIncrease
            : effect.value;
    }

    public string GetDescription()
    {
        if (Data == null) return string.Empty;
        string description = Data.description ?? string.Empty;
        if (!IsPerformanceDetermined)
            return ReplaceIndexedPlaceholders(
                description.Replace("{value}", "?").Replace("{absValue}", "?").Replace("{duration}", "?"), null);
        if (CurrentPerformance.effects == null || CurrentPerformance.effects.Count == 0) return description;

        BattleEffect first = CurrentPerformance.effects[0];
        description = description
            .Replace("{value}", GetBaseValue(first).ToString())
            .Replace("{absValue}", Mathf.Abs(GetBaseValue(first)).ToString())
            .Replace("{duration}", first.duration.ToString());
        return ReplaceIndexedPlaceholders(description, CurrentPerformance.effects);
    }

    private string ReplaceIndexedPlaceholders(
        string description, System.Collections.Generic.IList<BattleEffect> effects)
    {
        return Regex.Replace(description, @"\{(value|absValue|duration|increment|min|max|decay|secondaryDuration|secondary|tertiary)(\d+)\}", match =>
        {
            if (effects == null) return "?";
            int index;
            if (!int.TryParse(match.Groups[2].Value, out index)) return "?";
            index--;
            if (index < 0 || index >= effects.Count) return "?";
            BattleEffect effect = effects[index];
            switch (match.Groups[1].Value)
            {
                case "value": return GetBaseValue(effect).ToString();
                case "absValue": return Mathf.Abs(GetBaseValue(effect)).ToString();
                case "increment": return effect.damageIncrease.ToString();
                case "decay": return effect.decayPerTrigger.ToString();
                case "secondary": return effect.secondaryValue.ToString();
                case "tertiary": return effect.tertiaryValue.ToString();
                case "secondaryDuration": return effect.secondaryDuration.ToString();
                case "min": return effect.minimumMultiplier.ToString();
                case "max": return effect.maximumMultiplier.ToString();
                default: return effect.duration.ToString();
            }
        });
    }
}
