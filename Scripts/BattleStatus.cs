using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PoisonStatus
{
    [Serializable] public class Stack
    {
        [Min(0)] public int remainingTurns;
        [Min(0)] public int damagePerTurn;
    }
    [SerializeField] private List<Stack> stacks = new List<Stack>();
    public IReadOnlyList<Stack> Stacks => stacks;
    public bool IsActive => stacks.Count > 0;

    public void Add(int turns, int damage)
    {
        if (turns > 0 && damage > 0)
            stacks.Add(new Stack { remainingTurns = turns, damagePerTurn = damage });
    }

    public int ConsumeTick()
    {
        int total = 0;
        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            Stack stack = stacks[i];
            if (stack.remainingTurns <= 0 || stack.damagePerTurn <= 0) { stacks.RemoveAt(i); continue; }
            total += stack.damagePerTurn;
            if (--stack.remainingTurns <= 0) stacks.RemoveAt(i);
        }
        return total;
    }

    public void Amplify(int amount)
    {
        foreach (Stack stack in stacks)
            stack.damagePerTurn = Mathf.Max(0, stack.damagePerTurn + amount);
    }
}

/// <summary>各スタックが発動後に減衰する状態。ターン経過だけでは減りません。</summary>
[Serializable]
public class DecayingHitStatus
{
    [Serializable]
    public class Stack
    {
        [Min(0)] public int value;
        [Min(1)] public int decayPerTrigger;
    }
    [SerializeField] private List<Stack> stacks = new List<Stack>();
    public IReadOnlyList<Stack> Stacks => stacks;
    public bool IsActive => stacks.Count > 0;
    public void Add(int value, int decayPerTrigger)
    {
        if (value > 0 && decayPerTrigger > 0)
            stacks.Add(new Stack { value = value, decayPerTrigger = decayPerTrigger });
    }
    public int TotalValue
    {
        get { int total = 0; foreach (var stack in stacks) total += stack.value; return total; }
    }
    public void Amplify(int amount)
    {
        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            stacks[i].value = Mathf.Max(0, stacks[i].value + amount);
            if (stacks[i].value == 0) stacks.RemoveAt(i);
        }
    }
    public int ConsumeTrigger()
    {
        int damage = 0;
        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            var stack = stacks[i];
            damage += Mathf.Max(0, stack.value);
            stack.value = Mathf.Max(0, stack.value - Mathf.Max(1, stack.decayPerTrigger));
            if (stack.value == 0) stacks.RemoveAt(i);
        }
        return damage;
    }
    public void ReduceDecay(int amount)
    {
        foreach (var stack in stacks) stack.decayPerTrigger = Mathf.Max(1, stack.decayPerTrigger - Mathf.Max(0, amount));
    }
}

[Serializable] public class BleedingStatus : DecayingHitStatus { }
[Serializable] public class ElectrocutionStatus : DecayingHitStatus { }

[Serializable]
public class TimedModifier
{
    public BattleEffectType effectType;
    public int value;
    [Min(0)] public int remainingTurns;
    public int secondaryValue, secondaryDuration, decayPerTrigger;
    public bool skipNextTick;
}

[Serializable]
public class BattleModifiers
{
    [SerializeField] private List<TimedModifier> active = new List<TimedModifier>();
    public IReadOnlyList<TimedModifier> Active => active;
    public int cardDamageBonus => Total(BattleEffectType.CardDamageBonus);
    public int cardPoisonDamageBonus => Total(BattleEffectType.CardPoisonDamageBonus);
    public int cardPoisonDurationBonus => Total(BattleEffectType.CardPoisonDurationBonus);
    public int outgoingDamageModifier => Total(BattleEffectType.OutgoingDamageModifier);
    public int incomingDamageModifier => Total(BattleEffectType.IncomingDamageModifier);
    public int incomingCardDamageBonus => Total(BattleEffectType.IncomingCardDamageBonus);
    public int cardCostModifier => Total(BattleEffectType.CardCostModifier);
    public int cardElectrocutionBonus => Total(BattleEffectType.CardElectrocutionBonus);
    public int blockGainModifier => Total(BattleEffectType.BlockGainModifier);
    public int NextAttackReduction => Total(BattleEffectType.NextAttackReduction);
    public int DebuffCount
    {
        get { int count = 0; foreach (var modifier in active) if (modifier.remainingTurns > 0 && IsDebuff(modifier)) count++; return count; }
    }
    public static bool IsDebuff(TimedModifier modifier)
    {
        switch (modifier.effectType)
        {
            case BattleEffectType.IncomingDamageModifier:
            case BattleEffectType.IncomingCardDamageBonus:
            case BattleEffectType.CardCostModifier:
            case BattleEffectType.NextAttackReduction:
            case BattleEffectType.DeepWound:
            case BattleEffectType.CursedAttacks: return modifier.value > 0;
            default: return modifier.value < 0;
        }
    }
    public void ReduceBuffs(int amount)
    {
        if (amount <= 0) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var modifier = active[i];
            if (modifier.remainingTurns <= 0 || IsDebuff(modifier)) continue;
            modifier.value = modifier.value > 0 ? Mathf.Max(0, modifier.value - amount) : Mathf.Min(0, modifier.value + amount);
            if (modifier.value == 0) active.RemoveAt(i);
        }
    }
    public int ConsumeNextAttackReduction()
    {
        int amount = NextAttackReduction;
        active.RemoveAll(m => m.effectType == BattleEffectType.NextAttackReduction);
        return amount;
    }
    public int PositiveAttackBonus
    {
        get
        {
            int total = 0;
            foreach (var modifier in active)
                if (modifier.remainingTurns > 0 && modifier.value > 0 &&
                    (modifier.effectType == BattleEffectType.CardDamageBonus || modifier.effectType == BattleEffectType.OutgoingDamageModifier))
                    total += modifier.value;
            return total;
        }
    }
    public int outgoingPoisonDurationModifier => Total(BattleEffectType.OutgoingPoisonDurationModifier);

    public void Add(BattleEffectType type, int value, int duration, bool skipNextTick = false)
    {
        if (duration > 0 && value != 0)
            active.Add(new TimedModifier { effectType = type, value = value, remainingTurns = duration, skipNextTick = skipNextTick });
    }
    public void Add(BattleEffect effect)
    {
        if (effect.duration > 0 && effect.value != 0)
            active.Add(new TimedModifier { effectType = effect.effectType, value = effect.value, remainingTurns = effect.duration,
                secondaryValue = effect.secondaryValue, secondaryDuration = effect.secondaryDuration, decayPerTrigger = effect.decayPerTrigger });
    }
    public void Tick()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].skipNextTick) { active[i].skipNextTick = false; continue; }
            if (active[i].effectType != BattleEffectType.NextAttackReduction && --active[i].remainingTurns <= 0) active.RemoveAt(i);
        }
    }
    public void Reset() => active.Clear();
    private int Total(BattleEffectType type)
    {
        int total = 0;
        foreach (TimedModifier modifier in active)
            if (modifier.effectType == type && modifier.remainingTurns > 0) total += modifier.value;
        return total;
    }
}
