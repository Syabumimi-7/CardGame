using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("敵の基本情報")]
    public string enemyName;
    public EnemyType enemyType;
    public GameObject enemyPrefab;
    [Header("敵図鑑")]
    public Sprite helpPortrait;
    [Header("基礎ステータス")]
    public int baseMaxHP;
    [Header("行動パターン（上から順に循環）")]
    public List<EnemyActionData> actionPattern = new List<EnemyActionData>();
    [Min(1)] public int actionsPerTurn = 1;
    [Header("使用カテゴリーに反応する敵")]
    public bool reactsToPlayerCards;
    [Tooltip("攻撃・防御・バフ・デバフの順。カード未使用時は通常の行動パターンを使います")]
    public List<EnemyActionData> categoryActions = new List<EnemyActionData>();
    [Header("連戦ごとのHP・行動ダメージ補正")]
    public float scalingFactor = 1.1f;
}

public enum EnemyType { Normal, Boss }
