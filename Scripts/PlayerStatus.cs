using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    [Header("現在のステータス")]
    public int currentHP = 50;
    public int maxHP = 50;
    public int currentCost = 3;
    [Min(0)] public int currentBlock;
    public PoisonStatus poison = new PoisonStatus();
    public BleedingStatus bleeding = new BleedingStatus();
    public ElectrocutionStatus electrocution = new ElectrocutionStatus();
    public BattleModifiers modifiers = new BattleModifiers();
    public PlayerCardStatus cardStatus = new PlayerCardStatus();

    public bool IsDead => currentHP <= 0;
    public int CardCostReduction => cardStatus.CostReduction(currentHP) - modifiers.cardCostModifier + cardStatus.LightweightReduction;
    public event System.Action Died;

    public int TakeDamage(int rawDamage, bool isPoison = false, bool isStatusDamage = false, DamagePopupKind popupKind = DamagePopupKind.Attack)
    {
        if (IsDead) return 0;
        int damage = Mathf.Max(0, rawDamage + (isPoison || isStatusDamage ? 0 : modifiers.incomingDamageModifier));
        int absorbed = isPoison ? 0 : Mathf.Min(currentBlock, damage);
        if (damage > 0) GameAudio.PlayDamage();
        currentBlock -= absorbed;
        int hpDamage = Mathf.Min(currentHP, damage - absorbed);
        currentHP = Mathf.Max(0, currentHP - hpDamage);
        DamagePopupUI.ShowDamage(this, damage - absorbed, absorbed, absorbed > 0 && currentBlock == 0,
            isPoison ? DamagePopupKind.Poison : popupKind);
        if (IsDead) Died?.Invoke();
        Debug.Log($"プレイヤーは {hpDamage} ダメージ（ブロック {absorbed}） 残りHP: {currentHP}");
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

    public void GainBlock(int amount) { currentBlock += Mathf.Max(0, amount); }
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

    public void ResetForBattle()
    {
        currentBlock = 0;
        poison = new PoisonStatus();
        bleeding = new BleedingStatus();
        electrocution = new ElectrocutionStatus();
        modifiers.Reset();
        cardStatus = new PlayerCardStatus();
    }
}
