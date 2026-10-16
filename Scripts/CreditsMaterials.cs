/// <summary>Credit text kept separate from layout for later additions.</summary>
public static class CreditsMaterials
{
    public class Entry
    {
        public readonly string use, name, author, url;
        public Entry(string use, string name, string author, string url)
        { this.use = use; this.name = name; this.author = author; this.url = url; }
    }
    private const string Mixamo = "https://www.mixamo.com/";
    private static Entry Character(string use, string model, string death)
    {
        return new Entry(use + "のモデル・待機・死亡", model + "\nIdle / " + death, "Adobe Mixamo 様", Mixamo);
    }
    private static Entry Stage(string use, string name, string author, string id)
    {
        return new Entry(use, name, author + "\n提供：Poly Haven 様", "https://polyhaven.com/a/" + id);
    }
    private static readonly Entry[] Audio =
    {
        new Entry("タイトル・バトル以外のBGM", "母なる海へ", "BGMer 様", "https://bgmer.net/"),
        new Entry("通常戦・勝利後の報酬画面のBGM", "不安の種", "BGMer 様", "https://bgmer.net/"),
        new Entry("ボス戦のBGM", "偶発の讃歌", "BGMer 様", "https://bgmer.net/"),
        new Entry("ボタン操作時の決定音", "決定ボタンを押す9", "効果音ラボ 様", "https://soundeffect-lab.info/"),
        new Entry("敵・プレイヤーの被ダメージ／ブロック", "鞭で攻撃1", "効果音ラボ 様", "https://soundeffect-lab.info/"),
        new Entry("報酬でHP回復を選択したとき", "回復魔法1", "効果音ラボ 様", "https://soundeffect-lab.info/"),
    };
    private static readonly Entry[] Characters =
    {
        Character("電撃の勇者", "Maria WProp J J Ong", "Two Handed Sword Death"),
        Character("拳の勇者", "Ely By K.Atienza", "Standing React Death Backward"),
        Character("毒ネズミ", "Ch14_nonPBR", "Sword And Shield Death"),
        Character("狂戦士", "Ch10_nonPBR", "Dying"),
        Character("重装兵", "Warrok W Kurniawan", "Sword And Shield Death"),
        Character("双剣使い", "Vampire A Lusth", "Sword And Shield Death"),
        Character("呪術師", "Warzombie F Pedroso", "Sword And Shield Death"),
        Character("吸血鬼", "Paladin J Nordstrom", "Sword And Shield Death"),
        Character("毒獣", "Pumpkinhulk L Shaw", "Sword And Shield Death"),
        Character("城塞王", "Maw J Laygo", "Sword And Shield Death"),
        Character("魔人", "Nightshade J Friedrich", "Sword And Shield Death"),
    };
    private static readonly Entry[] Environment =
    {
        Stage("ステージの岩・大きな石", "Namaqualand Boulder 02", "Greg Zaal 様 / Rico Cilliers 様", "namaqualand_boulder_02"),
        Stage("ステージの苔のある岩", "Rock Moss Set 01", "Kless Gyzen 様", "rock_moss_set_01"),
        Stage("ステージの低木", "Shrub 02", "Rico Cilliers 様", "shrub_02"),
        Stage("ステージの草", "Weed Plant 02", "Rico Cilliers 様 / Rob Tuytel 様", "weed_plant_02"),
        Stage("ステージの地表の茂み", "Wild Rooibos Bush", "James Ray Cock 様 / Jenelle van Heerden 様", "wild_rooibos_bush"),
        Stage("遺跡の石材の色・凹凸（加工して使用）", "Cracked Concrete", "Dimitrios Savva 様", "cracked_concrete"),
        Stage("ステージの苔・土・地面", "Forest Ground 01", "Rob Tuytel 様", "forrest_ground_01"),
        Stage("地形の石材レイヤー", "Coral Stone Wall", "Dimitrios Savva 様", "coral_stone_wall"),
        Stage("石床のマテリアル", "Monastery Stone Floor", "Amal Kumar 様", "monastery_stone_floor"),
        Stage("ステージの空・環境光", "Citrus Orchard Road (Pure Sky)", "Dimitrios Savva 様 / Jarod Guest 様", "citrus_orchard_road_puresky"),
    };
    public static Entry[] ForCategory(int category)
    {
        return category == 0 ? Audio : category == 1 ? Characters : Environment;
    }
}
