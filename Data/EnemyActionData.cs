using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyActionData", menuName = "Game/Enemy Action Data")]
public class EnemyActionData : ScriptableObject
{
    public string actionName;
    [TextArea] public string description;
    [Tooltip("上から順にすべて適用されます。Self は敵、Enemy はプレイヤーを指します")]
    public List<BattleEffect> effects = new List<BattleEffect>();
}
