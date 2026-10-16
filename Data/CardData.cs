using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("カード基本情報")]
    public string cardName;
    public Sprite cardImage;
    [Tooltip("効果ごとに {value1}/{duration1}、{value2}/{duration2} を使用。負の値を正数で表示する場合は {absValue1}（先頭の効果は {absValue}）を使用。成長攻撃のvalueは強化済みの値。増加量は {increment1}、ランダム倍率は {min1}/{max1}。詳しくは Assets/Script/CardEffects.md")]
    [TextArea] public string description;
    public CardCategory category;
    public CardRarity rarity = CardRarity.Common;

    [Header("性能バリエーション（低・中・高）")]
    public CardPerformance lowPerformance;
    public CardPerformance mediumPerformance;
    public CardPerformance highPerformance;
}

public enum CardCategory { Attack, Defense, Buff, Debuff }

public enum CardRarity
{
    [InspectorName("コモン")] Common = 0,
    [InspectorName("アンコモン")] Uncommon = 1,
    [InspectorName("レア")] Rare = 2,
    [InspectorName("スーパーレア")] SuperRare = 3
}

public enum EffectTarget { [InspectorName("自分")] Self, [InspectorName("敵")] Enemy }

/// <summary>カードと敵行動の双方から利用できる汎用効果。</summary>
public enum BattleEffectType
{
    [InspectorName("ダメージを与える")] Damage,
    [InspectorName("ブロックを得る")] GainBlock,
    [InspectorName("毒を付与する")] ApplyPoison,
    [InspectorName("攻撃力アップ／ダウン（プレイヤー）")] CardDamageBonus,
    [InspectorName("カードの毒ダメージ補正")] CardPoisonDamageBonus,
    [InspectorName("カードの毒の継続ターン補正")] CardPoisonDurationBonus,
    [InspectorName("攻撃力アップ／ダウン")] OutgoingDamageModifier,
    [InspectorName("受けるダメージアップ／ダウン")] IncomingDamageModifier,
    [InspectorName("付与する毒の継続ターンアップ／ダウン")] OutgoingPoisonDurationModifier,
    [InspectorName("このカードはずっと墓地に置かれる")] RemainInDiscard,
    [InspectorName("ブロックで攻撃する")] BlockDamage,
    [InspectorName("使うたびにダメージがアップする")] GrowingDamage,
    [InspectorName("ダメージをランダムに変動する")] RandomDamage,
    [InspectorName("相手の残りHPの割合で攻撃する")] RemainingHPDamage,
    [InspectorName("カードのブロック獲得量アップ")] CardBlockBonus,
    [InspectorName("攻撃への固定反撃")] Thorns,
    [InspectorName("攻撃への毒反撃の継続期間")] PoisonShield,
    [InspectorName("毒反撃の毒ダメージと期間（直前の毒反撃用）")] PoisonShieldPayload,
    [InspectorName("攻撃力バフ分のブロック")] BuffToBlock,
    [InspectorName("HPが最大HPの1/value以下なら追加ブロック")] LowHPBlock,
    [InspectorName("条件付き追加ブロック量（直前のHP条件用）")] ConditionalBlockPayload,
    [InspectorName("ターン終了時にブロック")] EndTurnBlock,
    [InspectorName("次ターンのコスト追加")] NextTurnCost,
    [InspectorName("次ターンからのドロー追加")] ExtraDraw,
    [InspectorName("現在の毒のダメージ増幅")] AmplifyPoison,
    [InspectorName("残りコストを次ターンへ温存")] PreserveCost,
    [InspectorName("HPがvalue以下の間カードコスト軽減")] Adversity,
    [InspectorName("HP回復")] Heal,
    [InspectorName("カードから受けるダメージ追加")] IncomingCardDamageBonus,
    [InspectorName("ブロックを全て除去")] RemoveBlock,
    [InspectorName("ブロックを指定量削る")] ReduceBlock,
    [InspectorName("戦闘中ずっと攻撃力上昇")] PermanentAttackBonus,
    [InspectorName("ブロックを全て消費して攻撃")] ConsumeBlockDamage,
    [InspectorName("与えたHPダメージの割合を吸血（durationは回復率%）")] LifestealDamage,
    [InspectorName("カードの使用コスト補正")] CardCostModifier,
    [InspectorName("出血を付与する")] ApplyBleeding,
    [InspectorName("感電を付与する")] ApplyElectrocution,
    [InspectorName("デバフ数に応じたダメージ")] DebuffCountDamage,
    [InspectorName("デバフ数に応じた防御")] DebuffCountBlock,
    [InspectorName("出血値に応じた追加ダメージ")] BleedingBonusDamage,
    [InspectorName("敵HP条件で追加ダメージ")] ExecutionDamage,
    [InspectorName("各出血スタックの増幅")] AmplifyBleeding,
    [InspectorName("使用時のデバフ数でカード攻撃強化")] DebuffCountCardBonus,
    [InspectorName("次の1ヒットの攻撃を減らす")] NextAttackReduction,
    [InspectorName("ブロック獲得量アップ／ダウン")] BlockGainModifier,
    [InspectorName("敵の全バフを一定量減らす")] ReduceBuffs,
    [InspectorName("攻撃への出血反撃")] BleedingShield,
    [InspectorName("攻撃への弱体反撃")] WeakeningShield,
    [InspectorName("HPダメージ時のみ固定反撃")] HPDamageRetaliation,
    [InspectorName("受けたHPダメージでカード攻撃強化")] DamageTriggeredCardBonus,
    [InspectorName("攻撃後に出血を得る")] DeepWound,
    [InspectorName("攻撃後に攻撃力低下を得る")] CursedAttacks,
    [InspectorName("使用済み枚数に応じた攻撃")] PlayedCardsDamage,
    [InspectorName("支払い済みコストに応じた攻撃")] SpentCostDamage,
    [InspectorName("直前が攻撃以外なら追加ダメージ")] AlternatingDamage,
    [InspectorName("感電値に応じた追加ダメージ")] ElectrocutionBonusDamage,
    [InspectorName("使用済み枚数条件でコスト回復")] PlayedCardsCostRecovery,
    [InspectorName("使用済み枚数に応じた防御")] PlayedCardsBlock,
    [InspectorName("全感電スタックを増幅")] AmplifyElectrocution,
    [InspectorName("使用済み枚数条件で追加防御")] PlayedCardsConditionalBlock,
    [InspectorName("支払い済みコストに応じた防御")] SpentCostBlock,
    [InspectorName("ターン中の使用枚数に連動する攻撃強化")] ChainAttackBonus,
    [InspectorName("カードで与える感電値を強化")] CardElectrocutionBonus,
    [InspectorName("次の指定枚数のコスト軽減")] LightweightCards,
    [InspectorName("指定枚数ごとにドロー")] EveryCardsDraw,
    [InspectorName("指定枚数ごとにコスト回復")] EveryCardsCostRecovery,
    [InspectorName("三種類のカテゴリーで感電付与")] CategoryElectrocution,
    [InspectorName("感電の発動後の減少量を減らす")] ReduceElectrocutionDecay
}

[Serializable]
public struct BattleEffect
{
    public BattleEffectType effectType;
    public EffectTarget target;
    [Tooltip("ダメージ・ブロック・補正量。ブロック攻撃は加算量、成長攻撃は初期ダメージ、ランダム攻撃は倍率を掛ける元の値、HP割合攻撃は分母（1以上）。墓地に残る効果では未使用")]
    public int value;
    [Tooltip("毒・バフ・デバフの継続ターン数。Damage と GainBlock では使用しません")]
    public int duration;
    [InspectorName("使用ごとのダメージ増加量")]
    [Tooltip("使うたびにダメージがアップする効果専用。説明文では {increment1}（1つ目の効果）などで表示します")]
    [Min(0)] public int damageIncrease;
    [InspectorName("ランダム倍率の最小値")]
    [Tooltip("ランダム攻撃専用。上下限を含む整数を使用時に等確率で抽選。説明文は {min1} など")]
    [Min(0)] public int minimumMultiplier;
    [InspectorName("ランダム倍率の最大値")]
    [Tooltip("ランダム攻撃専用。説明文は {max1} など")]
    [Min(0)] public int maximumMultiplier;
    [Tooltip("敵の反応行動専用。プレイヤーがこのターンに使用した2枚目以降のカード1枚につきvalueへ加算")]
    public int valuePerExtraCard;
    [Min(1), Tooltip("出血・感電専用。発動後に各スタックから減らす値。valueが初期値で、durationは使用しません。")]
    public int decayPerTrigger;
    [Tooltip("血刃の出血倍率、処刑のHP分母など。説明文では {secondary1} など")]
    public int secondaryValue;
    [Tooltip("処刑の追加ダメージ。説明文では {tertiary1} など")]
    public int tertiaryValue;
    [Min(0), Tooltip("反撃で与えるデバフ、報復体制で得るバフ、呪縛で得るデバフの継続時間。{secondaryDuration1} など")]
    public int secondaryDuration;
}

[Serializable]
public struct CardPerformance
{
    [Min(0)] public int cost;
    [Tooltip("上から順にすべて適用されます")]
    public List<BattleEffect> effects;
}
