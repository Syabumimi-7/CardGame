using System.Collections.Generic;
using UnityEngine;

/// <summary>状態を変更せず、ターン終了処理と行動順を含めて次の敵行動を予告します。</summary>
public static class EnemyIntentForecast
{
    public sealed class Forecast
    {
        public string text;
        public int attackDamage;
        public int hpDamage;
    }

    public static string CategoryName(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Defense: return "防御";
            case CardCategory.Buff: return "バフ";
            case CardCategory.Debuff: return "デバフ";
            default: return "攻撃";
        }
    }

    public static Forecast Create(EnemyInstance enemy, PlayerStatus player, bool beforeTurnEnd = true,
        int actionsToPredict = -1, int firstActionNumber = 1)
    {
        var result = new Forecast { text = "行動なし" };
        if (enemy == null || enemy.Data == null || player == null || enemy.IsDead) return result;
        int enemyBlock = enemy.currentBlock, enemyHP = enemy.currentHP;
        int playerBlock = player.currentBlock, playerHP = player.currentHP;
        int attackBonus = enemy.AttackBonus, incoming = 0;
        int nextAttackReduction = enemy.modifiers.NextAttackReduction;
        int blockGainModifier = enemy.modifiers.blockGainModifier;
        int enemyIncoming = enemy.modifiers.incomingDamageModifier;
        var bleeding = new BleedingStatus();
        foreach (var stack in enemy.bleeding.Stacks) bleeding.Add(stack.value, stack.decayPerTrigger);
        var shock = new ElectrocutionStatus();
        foreach (var stack in player.electrocution.Stacks) shock.Add(stack.value, stack.decayPerTrigger);
        foreach (var modifier in player.modifiers.Active)
            if (modifier.effectType == BattleEffectType.IncomingDamageModifier
                && modifier.remainingTurns > (beforeTurnEnd ? 1 : 0)) incoming += modifier.value;
        if (beforeTurnEnd)
        {
            foreach (var buff in player.cardStatus.endTurnBlocks) playerBlock += Mathf.Max(0, buff.value);
            int poisonDamage = 0;
            foreach (var poison in enemy.poison.Stacks)
                if (poison.remainingTurns > 0) poisonDamage += poison.damagePerTurn;
            enemyHP -= poisonDamage;
        }
        if (enemyHP <= 0) { result.text = "ターン終了時の毒で撃破見込み"; return result; }

        var lines = new List<string>();
        if (enemy.Data.reactsToPlayerCards)
            lines.Add(enemy.CardsPlayedThisTurn == 0 ? "カード未使用"
                : $"行動の基準：{CategoryName(enemy.DominantCategory)}／使用枚数{enemy.CardsPlayedThisTurn}枚");
        int count = actionsToPredict < 0 ? enemy.ActionsPerTurn : Mathf.Min(enemy.ActionsPerTurn, actionsToPredict);
        for (int i = 0; i < count && enemyHP > 0 && playerHP > 0; i++)
        {
            EnemyActionData action = enemy.GetNextAction(i);
            if (action == null) continue;
            var details = new List<string>();
            foreach (BattleEffect effect in enemy.ResolveEffects(action))
            {
                if (enemyHP <= 0 || playerHP <= 0) break;
                bool targetPlayer = effect.target == EffectTarget.Enemy;
                string target = targetPlayer ? "プレイヤー" : "敵自身";
                switch (effect.effectType)
                {
                    case BattleEffectType.Damage:
                    case BattleEffectType.LifestealDamage:
                    case BattleEffectType.BlockDamage:
                    case BattleEffectType.ConsumeBlockDamage:
                        int baseDamage = effect.effectType == BattleEffectType.BlockDamage || effect.effectType == BattleEffectType.ConsumeBlockDamage
                            ? enemyBlock + effect.value : Mathf.RoundToInt(effect.value * enemy.BattleScalingMultiplier);
                        if (effect.effectType == BattleEffectType.ConsumeBlockDamage) enemyBlock = 0;
                        int outgoing = Mathf.Max(0, baseDamage + attackBonus - (targetPlayer ? nextAttackReduction : 0));
                        if (targetPlayer) nextAttackReduction = 0;
                        int damage = Mathf.Max(0, outgoing + (targetPlayer ? incoming : enemyIncoming));
                        details.Add($"{target}に{damage}ダメージ" + (effect.effectType == BattleEffectType.ConsumeBlockDamage ? "（自身の全ブロックを消費）" : ""));
                        if (targetPlayer)
                        {
                            result.attackDamage += damage;
                            int blocked = Mathf.Min(playerBlock, damage); playerBlock -= blocked;
                            int hpDamage = Mathf.Min(playerHP, damage - blocked);
                            playerHP -= hpDamage; result.hpDamage += hpDamage;
                            if (effect.effectType == BattleEffectType.LifestealDamage)
                            {
                                int healing = hpDamage * Mathf.Clamp(effect.duration, 0, 100) / 100;
                                enemyHP = Mathf.Min(enemy.maxHP, enemyHP + healing);
                                details.Add($"敵自身のHPを与えたHPダメージの{effect.duration}％回復（{healing}回復）");
                            }
                            if (playerHP > 0)
                            {
                                int shockDamage = shock.ConsumeTrigger();
                                int shockBlocked = Mathf.Min(playerBlock, shockDamage); playerBlock -= shockBlocked;
                                int shockHP = Mathf.Min(playerHP, shockDamage - shockBlocked);
                                playerHP -= shockHP; result.hpDamage += shockHP;
                                if (shockDamage > 0) details.Add($"プレイヤーに感電で{shockDamage}ダメージ");
                            }
                            if (enemyHP > 0)
                            {
                                int bleedingDamage = bleeding.ConsumeTrigger();
                                int bleedingBlocked = Mathf.Min(enemyBlock, bleedingDamage); enemyBlock -= bleedingBlocked;
                                enemyHP -= bleedingDamage - bleedingBlocked;
                                if (bleedingDamage > 0) details.Add($"敵自身に出血で{bleedingDamage}ダメージ");
                            }
                            if (enemyHP > 0)
                                foreach (var modifier in enemy.modifiers.Active)
                                {
                                    if (modifier.remainingTurns <= 0) continue;
                                    if (modifier.effectType == BattleEffectType.DeepWound)
                                        bleeding.Add(modifier.value, modifier.decayPerTrigger);
                                    else if (modifier.effectType == BattleEffectType.CursedAttacks && modifier.secondaryDuration > 0)
                                        attackBonus -= Mathf.Max(0, modifier.value);
                                }
                            foreach (var reaction in player.cardStatus.reactions)
                            {
                                if (enemyHP <= 0) break;
                                if (reaction.turns <= 0 || reaction.poisonTurns > 0) continue;
                                if (reaction.effectType == BattleEffectType.BleedingShield)
                                {
                                    bleeding.Add(reaction.damage, reaction.decayPerTrigger);
                                    continue;
                                }
                                if (reaction.effectType == BattleEffectType.WeakeningShield)
                                {
                                    if (reaction.debuffDuration > 0) attackBonus += reaction.damage;
                                    continue;
                                }
                                if (reaction.effectType == BattleEffectType.HPDamageRetaliation && hpDamage <= 0) continue;
                                int retaliation = Mathf.Max(0, reaction.damage + enemyIncoming);
                                int absorbed = Mathf.Min(enemyBlock, retaliation); enemyBlock -= absorbed;
                                enemyHP -= retaliation - absorbed;
                            }
                        }
                        break;
                    case BattleEffectType.GainBlock:
                        int gainedBlock = Mathf.Max(0, effect.value + (targetPlayer ? 0 : blockGainModifier));
                        if (!targetPlayer) enemyBlock += gainedBlock;
                        details.Add($"{target}が{gainedBlock}ブロックを得る"); break;
                    case BattleEffectType.PermanentAttackBonus:
                        attackBonus += Mathf.Max(0, effect.value);
                        details.Add($"{target}に攻撃力{effect.value}アップを付与（戦闘中）"); break;
                    case BattleEffectType.ApplyPoison:
                        details.Add($"{target}に毒{effect.value}を付与（{effect.duration}ターン）"); break;
                    case BattleEffectType.ApplyBleeding:
                        if (!targetPlayer) bleeding.Add(effect.value, effect.decayPerTrigger);
                        details.Add($"{target}に出血{effect.value}を付与（発動ごと−{effect.decayPerTrigger}）"); break;
                    case BattleEffectType.ApplyElectrocution:
                        if (targetPlayer) shock.Add(effect.value, effect.decayPerTrigger);
                        details.Add($"{target}に感電{effect.value}を付与（発動ごと−{effect.decayPerTrigger}）"); break;
                    case BattleEffectType.OutgoingDamageModifier:
                        if (!targetPlayer) attackBonus += effect.value;
                        details.Add(ModifierText(target, "攻撃力", effect)); break;
                    case BattleEffectType.IncomingDamageModifier:
                        if (targetPlayer) incoming += effect.value;
                        else enemyIncoming += effect.value;
                        details.Add(ModifierText(target, "受けるダメージ", effect)); break;
                    case BattleEffectType.CardBlockBonus:
                        details.Add(ModifierText(target, "カードのブロック獲得量", effect)); break;
                    case BattleEffectType.OutgoingPoisonDurationModifier:
                        details.Add(ModifierText(target, "付与する毒の継続ターン", effect)); break;
                    case BattleEffectType.CardCostModifier:
                        details.Add(ModifierText(target, "カードの使用コスト", effect)); break;
                }
            }
            string prefix = enemy.ActionsPerTurn > 1 ? $"行動{i + firstActionNumber:00}：" : "";
            lines.Add(prefix + action.actionName + "：" + string.Join(" → ", details));
        }
        result.text = lines.Count == 0 ? "行動なし" : string.Join("\n", lines);
        return result;
    }

    private static string ModifierText(string target, string name, BattleEffect effect)
    {
        string direction = effect.value >= 0 ? "アップ" : "ダウン";
        return $"{target}に{name}{Mathf.Abs(effect.value)}{direction}を付与（{effect.duration}ターン）";
    }
}
