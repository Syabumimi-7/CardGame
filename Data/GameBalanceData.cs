using UnityEngine;

[CreateAssetMenu(fileName = "GameBalanceData", menuName = "Game/Game Balance Data")]
public class GameBalanceData : ScriptableObject
{
    [Header("デッキ・手札設定")]
    [Tooltip("初期デッキの枚数")]
    public int initialDeckSize = 10;

    [Tooltip("毎ターン引く手札の枚数")]
    public int handDrawCount = 5;

    [Tooltip("デッキの最大枚数")]
    public int maxDeckSize = 20;

    [Header("戦闘・コスト設定")]
    [Tooltip("1ターンに使用できる最大コスト")]
    public int maxCost = 3;

    [Header("報酬設定")]
    [Tooltip("報酬で提示されるカード候補の枚数")]
    public int rewardCardCandidateCount = 10;

    [Tooltip("報酬で最大何枚までカードを獲得できるか")]
    public int maxRewardCardSelect = 3;

    [Tooltip("報酬で回復するHPの量")]
    public int hpRecoveryAmount = 20;
}
