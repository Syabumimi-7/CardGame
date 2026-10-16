using UnityEngine;

public class EnemyInstance : MonoBehaviour
{
    public EnemyData Data { get; private set; }
    [Header("現在のステータス")]
    public int currentHP;
    public int maxHP;
    [Min(0)] public int currentBlock;
    public PoisonStatus poison = new PoisonStatus();
    public BleedingStatus bleeding = new BleedingStatus();
    public ElectrocutionStatus electrocution = new ElectrocutionStatus();
    public BattleModifiers modifiers = new BattleModifiers();
    private int actionIndex;
    private EnemyAnimationPlayer animationPlayer;
    private readonly int[] categoryCounts = new int[4];
    private readonly int[] categoryLastUsed = new int[4];
    public int CardsPlayedThisTurn { get; private set; }
    public int ActionsPerTurn => Data == null ? 1 : Mathf.Max(1, Data.actionsPerTurn);
    public bool IsAnimating => animationPlayer != null && animationPlayer.IsBusy;
    public bool IsDead => currentHP <= 0;
    public int PermanentAttackBonus { get; private set; }
    public int AttackBonus => modifiers.outgoingDamageModifier + PermanentAttackBonus;
    public float BattleScalingMultiplier { get; private set; } = 1f;

    public void Setup(EnemyData data, int battleCount)
    {
        Data = data;
        actionIndex = 0;
        BattleScalingMultiplier = Mathf.Pow(data.scalingFactor, battleCount - 1);
        maxHP = Mathf.RoundToInt(data.baseMaxHP * BattleScalingMultiplier);
        currentHP = maxHP;
        currentBlock = 0;
        PermanentAttackBonus = 0;
        BeginPlayerTurn();
        poison = new PoisonStatus();
        bleeding = new BleedingStatus();
        electrocution = new ElectrocutionStatus();
        modifiers.Reset();
        animationPlayer = GetComponentInChildren<EnemyAnimationPlayer>();
        if (animationPlayer != null) animationPlayer.ResetForBattle();
    }

    public void BeginPlayerTurn()
    {
        System.Array.Clear(categoryCounts, 0, categoryCounts.Length);
        System.Array.Clear(categoryLastUsed, 0, categoryLastUsed.Length);
        CardsPlayedThisTurn = 0;
    }

    public void RecordPlayerCard(CardCategory category)
    {
        categoryCounts[(int)category]++;
        categoryLastUsed[(int)category] = ++CardsPlayedThisTurn;
    }

    public int CategoryCount(CardCategory category) => categoryCounts[(int)category];
    public CardCategory DominantCategory
    {
        get
        {
            int best = 0;
            for (int i = 1; i < categoryCounts.Length; i++)
                if (categoryCounts[i] > categoryCounts[best] || (categoryCounts[i] == categoryCounts[best]
                    && categoryLastUsed[i] > categoryLastUsed[best])) best = i;
            return (CardCategory)best;
        }
    }

    public EnemyActionData GetNextAction(int offset = 0)
    {
        if (Data == null) return null;
        if (Data.reactsToPlayerCards && CardsPlayedThisTurn > 0 && Data.categoryActions.Count == 4)
            return Data.categoryActions[(int)DominantCategory];
        return Data.actionPattern.Count == 0 ? null : Data.actionPattern[(actionIndex + offset) % Data.actionPattern.Count];
    }

    public System.Collections.Generic.List<BattleEffect> ResolveEffects(EnemyActionData action)
    {
        var effects = new System.Collections.Generic.List<BattleEffect>();
        if (action == null || action.effects == null) return effects;
        foreach (BattleEffect original in action.effects)
        {
            BattleEffect effect = original;
            if (Data != null && Data.reactsToPlayerCards)
                effect.value += effect.valuePerExtraCard * Mathf.Max(0, CardsPlayedThisTurn - 1);
            effects.Add(effect);
        }
        return effects;
    }

    public void AdvanceActionIndex()
    {
        if (Data != null && Data.actionPattern.Count > 0) actionIndex = (actionIndex + 1) % Data.actionPattern.Count;
    }

    public int TakeDamage(int rawDamage, bool isPoison = false, bool isCard = false, bool isStatusDamage = false, DamagePopupKind popupKind = DamagePopupKind.Attack)
    {
        if (IsDead) return 0;
        int damage = Mathf.Max(0, rawDamage + (isPoison || isStatusDamage ? 0 : modifiers.incomingDamageModifier)
            + (isCard && !isPoison && !isStatusDamage ? modifiers.incomingCardDamageBonus : 0));
        int absorbed = isPoison ? 0 : Mathf.Min(currentBlock, damage);
        if (damage > 0) GameAudio.PlayDamage();
        currentBlock -= absorbed;
        int hpDamage = Mathf.Min(currentHP, damage - absorbed);
        currentHP = Mathf.Max(0, currentHP - hpDamage);
        DamagePopupUI.ShowDamage(this, damage - absorbed, absorbed, absorbed > 0 && currentBlock == 0,
            isPoison ? DamagePopupKind.Poison : popupKind);
        if (hpDamage > 0 && animationPlayer != null)
        {
            if (IsDead) animationPlayer.PlayDeath();
        }
        Debug.Log($"{Data.enemyName} は {hpDamage} ダメージ（ブロック {absorbed}） 残りHP: {currentHP}");
        return hpDamage;
    }

    public int Heal(int amount)
    {
        if (IsDead) return 0;
        int recovered = Mathf.Min(Mathf.Max(0, maxHP - currentHP), Mathf.Max(0, amount));
        currentHP += recovered;
        DamagePopupUI.Show(this, recovered, DamagePopupKind.Heal);
        return recovered;
    }

    public void GainBlock(int amount) { currentBlock += Mathf.Max(0, amount + modifiers.blockGainModifier); }
    public void ReduceBuffs(int amount)
    {
        modifiers.ReduceBuffs(amount);
        PermanentAttackBonus = Mathf.Max(0, PermanentAttackBonus - Mathf.Max(0, amount));
    }
    public void OnAttackCompleted()
    {
        if (IsDead) return;
        foreach (var modifier in new System.Collections.Generic.List<TimedModifier>(modifiers.Active))
        {
            if (modifier.remainingTurns <= 0) continue;
            if (modifier.effectType == BattleEffectType.DeepWound) AddBleeding(modifier.value, modifier.decayPerTrigger);
            else if (modifier.effectType == BattleEffectType.CursedAttacks)
                modifiers.Add(BattleEffectType.OutgoingDamageModifier, -Mathf.Max(0, modifier.value), modifier.secondaryDuration, true);
        }
    }
    public void GainPermanentAttack(int amount) { PermanentAttackBonus += Mathf.Max(0, amount); }
    public void AddPoison(int turns, int damage) { poison.Add(turns, damage); }
    public void AddBleeding(int value, int decayPerTrigger) { bleeding.Add(value, decayPerTrigger); }
    public void AddElectrocution(int value, int decayPerTrigger) { electrocution.Add(value, decayPerTrigger); }
    public int TriggerBleeding() => IsDead ? 0 : TakeDamage(bleeding.ConsumeTrigger(), isStatusDamage: true, popupKind: DamagePopupKind.Bleeding);
    public int TriggerElectrocution() => IsDead ? 0 : TakeDamage(electrocution.ConsumeTrigger(), isStatusDamage: true, popupKind: DamagePopupKind.Electrocution);
    public int TickPoison()
    {
        int damage = poison.ConsumeTick();
        return damage > 0 ? TakeDamage(damage, isPoison: true) : 0;
    }
    public void TickModifiers() { modifiers.Tick(); }
}
