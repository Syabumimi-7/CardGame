using System.Collections.Generic;
using UnityEngine;

/// <summary>カード由来の予約効果と反撃。戦闘ごとにリセットします。</summary>
public class PlayerCardStatus
{
    public class CardUsageBuff
    {
        public BattleEffect effect;
        public int turns, progress, categoryMask, createdTurn;
    }
    public readonly List<CardUsageBuff> usageBuffs = new List<CardUsageBuff>();
    public readonly List<int> lightweightRemaining = new List<int>();
    public int CardsPlayedThisTurn { get; private set; }
    public int CostSpentThisTurn { get; private set; }
    public CardCategory? PreviousCategory { get; private set; }
    public int TurnSerial { get; private set; }
    public int ChainAttackBonus { get; private set; }
    public int LightweightReduction => lightweightRemaining.Count;

    public void BeginPlayerTurn()
    {
        TurnSerial++; CardsPlayedThisTurn = 0; CostSpentThisTurn = 0; PreviousCategory = null;
        foreach (var buff in usageBuffs) buff.categoryMask = 0;
        UpdateChainBonus(0);
    }
    public void AddUsageBuff(BattleEffect effect)
    {
        if (effect.duration <= 0) return;
        usageBuffs.Add(new CardUsageBuff { effect = effect, turns = effect.duration, createdTurn = TurnSerial });
        UpdateChainBonus(CardsPlayedThisTurn + 1);
    }
    public List<CardUsageBuff> BeginCardUse()
    {
        UpdateChainBonus(CardsPlayedThisTurn + 1);
        return new List<CardUsageBuff>(usageBuffs);
    }
    public int CompleteCardUse(CardCategory category, int paidCost, List<CardUsageBuff> existingBuffs, PlayerStatus player, EnemyInstance enemy)
    {
        CardsPlayedThisTurn++; CostSpentThisTurn += Mathf.Max(0, paidCost); PreviousCategory = category;
        UpdateChainBonus(CardsPlayedThisTurn);
        int draws = 0;
        foreach (var buff in existingBuffs)
        {
            if (buff.turns <= 0 || player.IsDead || enemy.IsDead) continue;
            if (buff.effect.effectType == BattleEffectType.CategoryElectrocution)
            {
                buff.categoryMask |= 1 << (int)category;
                int count = 0; for (int bits = buff.categoryMask; bits != 0; bits >>= 1) count += bits & 1;
                if (count >= 3)
                {
                    enemy.AddElectrocution(Mathf.Max(0, buff.effect.value + player.modifiers.cardElectrocutionBonus), buff.effect.decayPerTrigger);
                    buff.categoryMask = 0;
                }
            }
            else if (buff.effect.effectType == BattleEffectType.EveryCardsDraw || buff.effect.effectType == BattleEffectType.EveryCardsCostRecovery)
            {
                if (++buff.progress >= Mathf.Max(1, buff.effect.value))
                {
                    buff.progress = 0;
                    if (buff.effect.effectType == BattleEffectType.EveryCardsDraw) draws++;
                    else player.currentCost++;
                }
            }
        }
        return draws;
    }
    public void ConsumeLightweight()
    {
        for (int i = lightweightRemaining.Count - 1; i >= 0; i--)
            if (--lightweightRemaining[i] <= 0) lightweightRemaining.RemoveAt(i);
    }
    private void UpdateChainBonus(int count)
    {
        ChainAttackBonus = 0;
        foreach (var buff in usageBuffs)
            if (buff.turns > 0 && buff.effect.effectType == BattleEffectType.ChainAttackBonus)
                ChainAttackBonus += Mathf.Max(0, count - (buff.createdTurn == TurnSerial ? 1 : 0)) * buff.effect.value;
    }
    public class Reaction
    {
        public int turns, damage, poisonTurns;
        public BattleEffectType effectType;
        public int decayPerTrigger, debuffDuration;
    }
    public readonly List<Reaction> reactions = new List<Reaction>();
    public readonly List<TimedModifier> blockBonuses = new List<TimedModifier>();
    public readonly List<TimedModifier> endTurnBlocks = new List<TimedModifier>();
    public readonly List<TimedModifier> extraDraws = new List<TimedModifier>();
    public readonly List<int> adversityThresholds = new List<int>();
    public readonly List<TimedModifier> damageTriggeredBonuses = new List<TimedModifier>();
    public int nextTurnCost;
    public int preserveCostTurns;
    public bool preserveCost
    {
        get => preserveCostTurns > 0;
        set => preserveCostTurns = value ? Mathf.Max(1, preserveCostTurns) : 0;
    }
    public void PreserveCostFor(int turns)
    {
        // 複数枚でも温存量は倍増せず、残り期間の長い効果を維持します。
        preserveCostTurns = Mathf.Max(preserveCostTurns, Mathf.Max(1, turns));
    }

    public int CostReduction(int hp) => adversityThresholds.FindAll(threshold => hp <= threshold).Count;
    public int BlockBonus
    {
        get { int total = 0; foreach (var buff in blockBonuses) total += buff.value; return total; }
    }
    public void AddTimed(List<TimedModifier> list, BattleEffect effect)
    {
        if (effect.duration > 0)
            list.Add(new TimedModifier { effectType = effect.effectType, value = effect.value, remainingTurns = effect.duration,
                secondaryValue = effect.secondaryValue, secondaryDuration = effect.secondaryDuration, decayPerTrigger = effect.decayPerTrigger });
    }
    public int BeginTurnDrawBonus()
    {
        int total = 0;
        foreach (var buff in extraDraws) total += buff.value;
        Tick(extraDraws);
        return total;
    }
    public void EndPlayerTurn(PlayerStatus player)
    {
        foreach (var buff in endTurnBlocks) player.GainBlock(buff.value);
        Tick(endTurnBlocks);
        Tick(blockBonuses);
        for (int i = usageBuffs.Count - 1; i >= 0; i--)
            if (--usageBuffs[i].turns <= 0) usageBuffs.RemoveAt(i);
        UpdateChainBonus(CardsPlayedThisTurn);
        if (preserveCost)
        {
            nextTurnCost += Mathf.Max(0, player.currentCost);
            preserveCostTurns--;
        }
    }
    public void EndEnemyTurn()
    {
        for (int i = reactions.Count - 1; i >= 0; i--)
            if (--reactions[i].turns <= 0) reactions.RemoveAt(i);
        Tick(damageTriggeredBonuses);
    }
    public void OnEnemyHPDamage(PlayerStatus player, int damage)
    {
        if (damage <= 0 || player.IsDead) return;
        foreach (var stance in damageTriggeredBonuses)
            if (stance.remainingTurns > 0 && stance.value > 0)
                player.modifiers.Add(BattleEffectType.CardDamageBonus, (damage - 1) / stance.value + 1, stance.secondaryDuration);
    }
    private static void Tick(List<TimedModifier> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (--list[i].remainingTurns <= 0) list.RemoveAt(i);
    }
}
