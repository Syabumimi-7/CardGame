using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Uses the actual card-use, draw, enemy action and status code. Never opens/saves scenes.
[InitializeOnLoad]
public static class PoolBalancePlaytest
{
    [Serializable] public class Job { public int pool, runs = 100, seed = 1000; public string mode = "synergy", archetype = "shock"; }
    [Serializable] public class Plan { public Job[] jobs; public string output; public bool allowSynergyStop,riskAwareExpert; public int expertHealThreshold=42; }
    [Serializable] public class Result
    {
        public int pool, seed, reached, turns, hp, limitHits;
        public string mode, archetype, boss;
        public string chosenArchetype;
        public bool win;
        public string[] initial, deck, acquired, enemies;
        public int[] tiers;
        public int heals, removals;
        public string[] usedCards;
        public int[] uses;
        public int searchLimitHits,turnLimitHits;
        public int[] battleStartHP, battleEndHP, coreCounts;
        public int firstComboBattle;
        public string[] archetypeHistory;
        public int firstFunctionalBattle;
    }
    [Serializable] public class Report
    {
        public List<Result> results = new List<Result>(); public string error; public double seconds;
        public string startedUtc,cardHash,rulesHash,policyHash;
        public bool allowSynergyStop,riskAwareExpert;
        public int expertHealThreshold;
        public PackageDefinition[] packages;
        public bool recoveryPolicyVerified;
    }
    [Serializable] public class PackageDefinition { public string name; public string[] cards; }
    static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly MethodInfo Memberwise = typeof(object).GetMethod("MemberwiseClone", InstanceFlags);
    static readonly Dictionary<Type, FieldInfo[]> CopyFields = new Dictionary<Type, FieldInfo[]>();
    static readonly Dictionary<EnemyData,bool> PoisonPatterns = new Dictionary<EnemyData,bool>();
    static readonly MethodInfo DrawCards = typeof(BattleManager).GetMethod("DrawCards", InstanceFlags);
    static readonly MethodInfo ApplyEnemy = typeof(BattleManager).GetMethod("ApplyEnemyAction", InstanceFlags);
    static readonly PropertyInfo BattleState = typeof(BattleManager).GetProperty("State");
    static readonly PropertyInfo Performance = typeof(CardInstance).GetProperty("CurrentPerformance");
    static readonly PropertyInfo Determined = typeof(CardInstance).GetProperty("IsPerformanceDetermined");
    static readonly PropertyInfo TimesPlayed = typeof(CardInstance).GetProperty("TimesPlayed");
    static readonly FieldInfo[] EnemyFields = typeof(EnemyInstance).GetFields(InstanceFlags).Where(f => f.Name == "currentHP" || f.Name == "currentBlock" || f.Name == "poison" || f.Name == "bleeding" || f.Name == "electrocution" || f.Name == "modifiers" || f.Name == "actionIndex" || f.Name == "categoryCounts" || f.Name == "categoryLastUsed" || f.Name.Contains("PermanentAttackBonus") || f.Name.Contains("CardsPlayedThisTurn")).ToArray();
    static readonly FieldInfo[] PlayerFields = typeof(PlayerStatus).GetFields(InstanceFlags).Where(f => f.Name == "currentHP" || f.Name == "currentCost" || f.Name == "currentBlock" || f.Name == "poison" || f.Name == "bleeding" || f.Name == "electrocution" || f.Name == "modifiers" || f.Name == "cardStatus").ToArray();
    static readonly Dictionary<string, string[]> Packages = new Dictionary<string, string[]>
    {
        {"shock", new[]{"Kandenkougeki","Renzokugiri","Kanden","Kadenryu","Kandenzoufuku","Kafuka","Judenbougyo","Doden"}},
        {"chain", new[]{"Rensakougeki","Renkeibougyo","Junkanbougyo","Saikido","Rensakyouka","Keiryoka","Dorookyouka"}},
        {"cost", new[]{"Shugeki","Henkanbougyo","Kosutokyouka","Saikido","Kosutokangen","Keiryoka","Junkanbougyo"}},
        {"cycle", new[]{"Junkan","Keiryoka","Shuruirenkei","Rensakougeki","Dorookyouka","Junkanbougyo","Saikido"}},
        {"alternate", new[]{"Kougokougeki","Shuruirenkei","Judenbougyo","Renzokugiri","Kougekiryokuagenagarabougyo","Zeijaku"}},
        {"bleed", new[]{"Shukketsukougeki","Shukketsu","Ketsujin","Kutsuuzoufuku","Shukketsunotate","Fukade","Kinkyuubougyo"}},
        {"debuff", new[]{"Jakutaikougeki","Jakutainotate","Seisai","Yokuatsu","Jakushagari","Jubaku","Bougyosogai"}},
        {"revenge", new[]{"Houfukubougyo","Houfukutaisei","Kinkyuubougyo","Shokei","Hasaikougeki","Kougekiryokuappu"}},
        {"execute", new[]{"Shokei","Hasaikougeki","Jakutaikougeki","Suijaku","Kougekiryokuappu","Kinkyuubougyo"}},
        {"control", new[]{"Kougekifuuji","Jubaku","Fukade","Bougyosogai","Jakutainotate","Ketsujin","Shukketsunotate"}},
        {"poison", new[]{"Doku","Dokunokenn","Dokunotate","Dokuzoufuku","Bougyo","Jakutai","Chikusekibougyo"}},
        {"block", new[]{"Burokkudekougeki","Chikusekibougyo","Kosutoonzon","Bougyo","Tsuikaburokku","Bougyokyouka","Kosutokyouka","Burokkuenagarakougeki"}},
        {"growth", new[]{"Tukautabinidame-jiappukougeki","Dorookyouka","Kosutokyouka","Ikkaidakekougekinooowaza","Bougyo","Kaifuku"}},
        {"attack", new[]{"Kougeki","Kougekiryokuappu","Kougekiryokuagenagarabougyo","Bafutenkanbougyo","Zeijaku","Burokkuenagarakougeki"}},
        {"thorns", new[]{"Togeburokku","Chikusekibougyo","Tsuikaburokku","Bougyokyouka","Bougyo","Jakutai"}}
    };
    public static readonly string[][] Archetypes = { new[]{"shock","chain","cost","cycle","alternate"}, new[]{"bleed","debuff","revenge","execute","control"}, new[]{"poison","block","growth","attack","thorns"} };
    static Plan plan;
    static Report report;
    static int jobIndex, runIndex;
    static double started;
    static GameObject root;
    static PlayerStatus player;
    static EnemyInstance enemy;
    static EnemyManager enemies;
    static DeckManager deck;
    static BattleManager battle;
    static GameBalanceData balance;
    static List<CardData>[] pools;
    static List<CardInstance> hand, draw, discard;
    static bool priorLogging;
    static UnityEngine.Random.State priorRandom;
    static Job currentJob;
    static int limits;
    static int searchNodesThisTurn,searchNodeLimit=3200;
    const int SearchNodesPerDecision=3200;
    const int SearchNodesPerTurn=8000;
    // Batch execution uses the same Campaign method in an isolated test project.
    public static void RunBatch()
    {
        int exitCode=0;
        try
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-poolBalancePlan");
            if(index<0 || index+1>=args.Length)throw new Exception("Missing -poolBalancePlan");
            Initialize(JsonUtility.FromJson<Plan>(File.ReadAllText(args[index+1])));
            for(jobIndex=0;jobIndex<plan.jobs.Length;jobIndex++)
            {
                currentJob=plan.jobs[jobIndex];
                for(runIndex=0;runIndex<currentJob.runs;runIndex++)
                {
                    report.results.Add(Campaign(currentJob,currentJob.seed+runIndex));
                    File.WriteAllText("Temp/PoolBalance.progress",$"Job {jobIndex+1}/{plan.jobs.Length} pool {currentJob.pool+1} {currentJob.mode}/{currentJob.archetype}: {runIndex+1}/{currentJob.runs}; total {report.results.Count}; elapsed {EditorApplication.timeSinceStartup-started:F1}s");
                }
                Save();if(!string.IsNullOrEmpty(report.error))throw new Exception(report.error);
            }
        }
        catch(Exception e){exitCode=1;if(report!=null){report.error=e.ToString();Save();}Debug.LogException(e);}
        finally{Cleanup();EditorApplication.Exit(exitCode);}
    }
    static int searchLimits;
    static int turnLimits;
    static Dictionary<string,int> actualUses;
    static string[] cardPaths,rulePaths;
    static Snapshot expertRoot;
    static readonly List<CardInstance> searchPath=new List<CardInstance>();
    static string HashFiles(IEnumerable<string> paths)
    {
        string content=string.Join("\n",paths.OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+"\n"+File.ReadAllText(p)));
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-","").ToLowerInvariant();
    }

    static PoolBalancePlaytest()
    {
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += () => { if(plan!=null){report.error="Interrupted by assembly reload";Save();Cleanup();} };
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (plan == null)
        {
            if (!File.Exists("Temp/PoolBalance.request")) return;
            string path = File.ReadAllText("Temp/PoolBalance.request").Trim(); File.Delete("Temp/PoolBalance.request");
            try { Initialize(JsonUtility.FromJson<Plan>(File.ReadAllText(path))); }
            catch (Exception e) { File.WriteAllText("Temp/PoolBalance.error", e.ToString()); Cleanup(); return; }
        }
        double until = EditorApplication.timeSinceStartup + .65;
        try
        {
            do
            {
                currentJob = plan.jobs[jobIndex];
                report.results.Add(Campaign(currentJob, currentJob.seed + runIndex));
                runIndex++;
                File.WriteAllText("Temp/PoolBalance.progress", $"Job {jobIndex+1}/{plan.jobs.Length} pool {currentJob.pool+1} {currentJob.mode}/{currentJob.archetype}: {runIndex}/{currentJob.runs}; total {report.results.Count}; elapsed {EditorApplication.timeSinceStartup-started:F1}s");
                if (runIndex >= currentJob.runs)
                {
                    runIndex = 0; jobIndex++; Save();
                    if (jobIndex >= plan.jobs.Length) { Cleanup(); return; }
                }
            } while (EditorApplication.timeSinceStartup < until);
        }
        catch (Exception e) { report.error = e.ToString(); Save(); Cleanup(); }
    }
    static void Initialize(Plan input)
    {
        plan = input; report = new Report(); jobIndex = runIndex = 0; started = EditorApplication.timeSinceStartup;
        PoisonPatterns.Clear();
        report.packages=Packages.Select(p=>new PackageDefinition{name=p.Key,cards=p.Value}).ToArray();
        if(plan.expertHealThreshold<=0)plan.expertHealThreshold=42;
        report.allowSynergyStop=plan.allowSynergyStop;report.expertHealThreshold=plan.expertHealThreshold;report.riskAwareExpert=plan.riskAwareExpert;
        cardPaths=Directory.GetFiles("Assets/ScriptableObject/CardData","*.asset");
        rulePaths=new[]{"BattleManager","PlayerStatus","EnemyInstance","BattleStatus","PlayerCardStatus","CardInstance","DeckManager","EnemyManager","CardRarityLottery","EnemyIntentForecast"}.Select(n=>"Assets/Script/"+n+".cs")
            .Concat(Directory.GetFiles("Assets/ScriptableObject/EnemyData","*.asset"))
            .Concat(Directory.GetFiles("Assets/ScriptableObject/EnemyActionData","*.asset"))
            .Concat(new[]{"Assets/ScriptableObject/GameBalanceData/DefaultBalance.asset","Assets/Script/Data/CardData.cs","Assets/Script/Data/EnemyData.cs","Assets/Script/Data/GameBalanceData.cs"}).ToArray();
        report.startedUtc=DateTime.UtcNow.ToString("O");report.cardHash=HashFiles(cardPaths);report.rulesHash=HashFiles(rulePaths);report.policyHash=HashFiles(new[]{"Assets/Script/Editor/PoolBalancePlaytest.cs"});
        foreach(var old in Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.name=="PoolBalanceNative" && g.scene.IsValid()).ToArray())UnityEngine.Object.DestroyImmediate(old);
        priorLogging = Debug.unityLogger.logEnabled; priorRandom = UnityEngine.Random.state; Debug.unityLogger.logEnabled = false;
        root = new GameObject("PoolBalanceNative");root.hideFlags=HideFlags.HideAndDontSave; root.SetActive(false);
        player = root.AddComponent<PlayerStatus>(); enemy = root.AddComponent<EnemyInstance>();
        enemies = root.AddComponent<EnemyManager>(); deck = root.AddComponent<DeckManager>(); battle = root.AddComponent<BattleManager>();
        typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", InstanceFlags).SetValue(enemies, enemy);
        battle.playerStatus = player; battle.enemyManager = enemies; battle.deckManager = deck;
        balance = AssetDatabase.LoadAssetAtPath<GameBalanceData>("Assets/ScriptableObject/GameBalanceData/DefaultBalance.asset"); battle.balanceData = balance;
        Func<string,List<CardInstance>> pile = name => (List<CardInstance>)typeof(BattleManager).GetField(name,InstanceFlags).GetValue(battle);
        hand = pile("hand"); draw = pile("drawPile"); discard = pile("discardPile");
        pools = new List<CardData>[3]; var scene = File.ReadAllText("Assets/Scenes/RuinsStageRefined.unity");
        string[] poolNames = { "balancePool", "attackHeavyPool", "defenseHeavyPool" };
        for (int i=0;i<3;i++)
        {
            var block = Regex.Match(scene, @"(?m)^  "+poolNames[i]+@":\r?\n(?:  -[^\r\n]*\r?\n)+").Value;
            pools[i] = Regex.Matches(block,@"guid: (\w+)").Cast<Match>().Select(m=>AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value))).ToList();
            if (pools[i].Count != 30 || pools[i].Any(c=>c==null)) throw new Exception("Invalid pool "+i);
        }
        deck.balancePool=pools[0]; deck.attackHeavyPool=pools[1]; deck.defenseHeavyPool=pools[2];
        enemies.normalEnemies=Enumerable.Range(1,6).Select(i=>AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/ScriptableObject/EnemyData/Enemy{i}.asset")).ToList();
        enemies.bossEnemies=Enumerable.Range(1,2).Select(i=>AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/ScriptableObject/EnemyData/Boss{i}.asset")).ToList();
        enemy.Setup(enemies.normalEnemies[0],1);enemy.AddBleeding(5,1);enemy.AddElectrocution(6,2);
        var isolation=new Snapshot();enemy.TriggerBleeding();enemy.TriggerElectrocution();isolation.Restore();
        if(enemy.bleeding.TotalValue!=5 || enemy.electrocution.TotalValue!=6 || enemy.currentHP!=enemy.maxHP)throw new Exception("Probe state isolation failed");
        VerifyRecoveryPolicy();report.recoveryPolicyVerified=true;
    }
    static void Save()
    {
        if(report.cardHash!=HashFiles(cardPaths) || report.rulesHash!=HashFiles(rulePaths)
            || report.policyHash!=HashFiles(new[]{"Assets/Script/Editor/PoolBalancePlaytest.cs"}))
            report.error="Inputs changed during evaluation; discard mixed results.";
        report.seconds = EditorApplication.timeSinceStartup-started; File.WriteAllText(plan.output,JsonUtility.ToJson(report,true));
    }
    static void Cleanup()
    {
        if(root != null) UnityEngine.Object.DestroyImmediate(root);
        Debug.unityLogger.logEnabled=priorLogging; UnityEngine.Random.state=priorRandom; plan=null;
    }
    static object Copy(object value)
    {
        if(value==null || value is UnityEngine.Object || value is string || value.GetType().IsValueType) return value;
        if(value is PoisonStatus poison) { var result=new PoisonStatus();foreach(var stack in poison.Stacks)result.Add(stack.remainingTurns,stack.damagePerTurn);return result; }
        if(value is DecayingHitStatus hit) { var result=(DecayingHitStatus)Activator.CreateInstance(value.GetType());foreach(var stack in hit.Stacks)result.Add(stack.value,stack.decayPerTrigger);return result; }
        if(value is TimedModifier modifier) return new TimedModifier{effectType=modifier.effectType,value=modifier.value,remainingTurns=modifier.remainingTurns,secondaryValue=modifier.secondaryValue,secondaryDuration=modifier.secondaryDuration,decayPerTrigger=modifier.decayPerTrigger,skipNextTick=modifier.skipNextTick};
        if(value is BattleModifiers modifiers)
        {
            var result=new BattleModifiers();
            foreach(var m in modifiers.Active)
            {
                result.Add(new BattleEffect{effectType=m.effectType,value=m.value,duration=m.remainingTurns,secondaryValue=m.secondaryValue,secondaryDuration=m.secondaryDuration,decayPerTrigger=m.decayPerTrigger});
                if(result.Active.Count>0)result.Active[result.Active.Count-1].skipNextTick=m.skipNextTick;
            }
            return result;
        }
        if(value is Array array) { var result=(Array)array.Clone();if(array.GetType().GetElementType().IsValueType)return result;for(int i=0;i<array.Length;i++)result.SetValue(Copy(array.GetValue(i)),i); return result; }
        if(value is IList list) { var result=(IList)Activator.CreateInstance(value.GetType()); foreach(var item in list)result.Add(Copy(item));return result; }
        var clone=Memberwise.Invoke(value,null); var type=value.GetType();
        if(!CopyFields.TryGetValue(type,out var fields))
        {
            var all=new List<FieldInfo>();for(Type current=type;current!=null;current=current.BaseType)all.AddRange(current.GetFields(InstanceFlags|BindingFlags.DeclaredOnly));
            CopyFields[type]=fields=all.ToArray();
        }
        foreach(var field in fields)field.SetValue(clone,Copy(field.GetValue(value)));return clone;
    }
    sealed class Snapshot
    {
        readonly object[] p=PlayerFields.Select(f=>Copy(f.GetValue(player))).ToArray(), e=EnemyFields.Select(f=>Copy(f.GetValue(enemy))).ToArray();
        readonly CardInstance[] h=hand.ToArray(), d=draw.ToArray(), g=discard.ToArray();
        readonly CardSave[] cards=deck.currentDeck.Select(c=>new CardSave(c)).ToArray();
        readonly UnityEngine.Random.State random=UnityEngine.Random.state;
        public void Restore()
        {
            for(int i=0;i<PlayerFields.Length;i++)PlayerFields[i].SetValue(player,Copy(p[i]));
            for(int i=0;i<EnemyFields.Length;i++)EnemyFields[i].SetValue(enemy,Copy(e[i]));
            hand.Clear();hand.AddRange(h);draw.Clear();draw.AddRange(d);discard.Clear();discard.AddRange(g);
            foreach(var card in cards)card.Restore();
            UnityEngine.Random.state=random;BattleState.SetValue(battle,BattleTurnState.Player);
        }
    }
    struct CardSave
    {
        readonly CardInstance card;
        readonly CardPerformance performance;
        readonly bool determined;
        readonly int played,reduction;
        public CardSave(CardInstance instance){card=instance;performance=instance.CurrentPerformance;determined=instance.IsPerformanceDetermined;played=instance.TimesPlayed;reduction=instance.CostReduction;}
        public void Restore()
        {
            if(card.CurrentPerformance.cost!=performance.cost || !ReferenceEquals(card.CurrentPerformance.effects,performance.effects))Performance.SetValue(card,performance);
            if(card.IsPerformanceDetermined!=determined)Determined.SetValue(card,determined);
            if(card.TimesPlayed!=played)TimesPlayed.SetValue(card,played);
            card.CostReduction=reduction;
        }
    }
    static Result Campaign(Job job,int seed)
    {
        UnityEngine.Random.InitState(seed);limits=searchLimits=turnLimits=0;actualUses=new Dictionary<string,int>();deck.CreateInitialDeck(job.pool);player.currentHP=player.maxHP=50;
        ((IList)typeof(EnemyManager).GetField("normalEnemyBag",InstanceFlags).GetValue(enemies)).Clear();
        typeof(EnemyManager).GetField("lastNormalEnemy",InstanceFlags).SetValue(enemies,null);
        var result=new Result{pool=job.pool,seed=seed,mode=job.mode,archetype=job.archetype,initial=deck.currentDeck.Select(c=>c.Data.name).ToArray()};
        var acquired=new List<string>();var order=new List<string>();
        var startHP=new List<int>();var endHP=new List<int>();var cores=new List<int>();var history=new List<string>();
        string archetype=job.archetype;
        if(archetype=="adaptive")archetype=job.mode=="expert"?ChooseArchetype(job.pool):
            Archetypes[job.pool].OrderByDescending(a=>deck.currentDeck.Sum(c=>Affinity(c.Data,a))).First();
        result.chosenArchetype=archetype;
        for(int fight=1;fight<=10;fight++)
        {
            if(job.mode=="expert" && job.archetype=="adaptive")archetype=ChooseArchetype(job.pool);
            history.Add(archetype);result.chosenArchetype=archetype;
            var definition=enemies.SelectEnemyData(fight);order.Add(definition.name);enemy.Setup(definition,fight);
            player.ResetForBattle();deck.ResetCardsForBattle();hand.Clear();draw.Clear();discard.Clear();draw.AddRange(deck.currentDeck);
            startHP.Add(player.currentHP);cores.Add(deck.currentDeck.Count(c=>Affinity(c.Data,archetype)>0));
            if(result.firstComboBattle==0 && deck.currentDeck.Any(c=>c.Data.name==Packages[archetype][0])
                && deck.currentDeck.Where(c=>Affinity(c.Data,archetype)>0).Select(c=>c.Data.name).Distinct().Count()>=3)
                result.firstComboBattle=fight;
            if(result.firstFunctionalBattle==0 && FunctionalDeck(archetype))result.firstFunctionalBattle=fight;
            result.reached=fight;bool won=Fight(job.mode,archetype,ref result.turns);
            endHP.Add(player.currentHP);
            if(!won || fight==10) { result.win=won&&fight==10;break; }
            int threshold=job.mode=="beginner"?25:job.mode=="expert"?plan.expertHealThreshold:35;
            // Decide the reward type before opening the pack, as in the game UI.
            // An unfinished deck may accept a small HP risk to obtain cards.
            if(job.mode=="expert" && deck.currentDeck.Count(c=>Affinity(c.Data,archetype)>0)<5)threshold=Math.Min(threshold,35);
            if(job.mode=="expert" && deck.currentDeck.Count(c=>Affinity(c.Data,archetype)>0)>=5
                && deck.currentDeck.Where(c=>Affinity(c.Data,archetype)>0).Select(c=>c.Data.name).Distinct().Count()>=3)
                threshold=50;
            // Both final bosses are part of the known roster. Prepare HP before
            // the final fight without inspecting which boss will be drawn.
            if(job.mode=="expert" && fight==9)threshold=50;
            if(player.currentHP<threshold) { player.currentHP=Mathf.Min(50,player.currentHP+balance.hpRecoveryAmount);result.heals++;continue; }
            if(job.mode!="beginner" && deck.currentDeck.Count>(job.mode=="expert"?10:12) && deck.currentDeck.Count(c=>Affinity(c.Data,archetype)>0)>=5
                && (job.mode!="expert" || deck.currentDeck.Count(c=>c.Data.category==CardCategory.Attack)>=3))
            {
                var worst=deck.currentDeck.OrderBy(c=>job.mode=="expert"?ExistingScore(c,archetype,job.mode):DraftScore(c.Data,archetype,job.mode)-(c.IsPerformanceDetermined?c.CurrentPerformance.cost*.3f:0)).First();
                if(ExistingScore(worst,archetype,job.mode)<6) { deck.currentDeck.Remove(worst);result.removals++;continue; }
            }
            var candidates=new CardRarityLottery(pools[job.pool]);var rewards=new List<CardData>();
            // Reward type is chosen before generating candidates. No unseen cards or rolls inform it.
            for(int i=0;i<balance.rewardCardCandidateCount;i++)rewards.Add(candidates.Draw());
            for(int n=0;n<balance.maxRewardCardSelect && rewards.Count>0;n++)
            {
                var best=rewards.OrderByDescending(c=>DraftScore(c,archetype,job.mode)-deck.currentDeck.Count(x=>x.Data==c)*3).First();
                float score=DraftScore(best,archetype,job.mode)-deck.currentDeck.Count(x=>x.Data==best)*3;
                if(job.mode!="beginner" && score<6)break;
                if(deck.currentDeck.Count>=balance.maxDeckSize)
                {
                    var worst=deck.currentDeck.OrderBy(c=>ExistingScore(c,archetype,job.mode)).First();
                    if(DraftScore(best,archetype,job.mode)<=ExistingScore(worst,archetype,job.mode))break;
                    deck.currentDeck.Remove(worst);result.removals++;
                }
                deck.currentDeck.Add(new CardInstance(best));acquired.Add(best.name);rewards.Remove(best);
                if(job.mode=="expert" && job.archetype=="adaptive")archetype=ChooseArchetype(job.pool);
            }
        }
        result.hp=player.currentHP;result.deck=deck.currentDeck.Select(c=>c.Data.name).ToArray();
        result.tiers=deck.currentDeck.Select(c=>!c.IsPerformanceDetermined?-1:ReferenceEquals(c.CurrentPerformance.effects,c.Data.lowPerformance.effects)?0:ReferenceEquals(c.CurrentPerformance.effects,c.Data.mediumPerformance.effects)?1:2).ToArray();
        result.acquired=acquired.ToArray();result.enemies=order.ToArray();result.boss=order.Last();result.limitHits=limits;
        result.battleStartHP=startHP.ToArray();result.battleEndHP=endHP.ToArray();result.coreCounts=cores.ToArray();
        result.archetypeHistory=history.ToArray();
        result.usedCards=actualUses.Keys.ToArray();result.uses=result.usedCards.Select(k=>actualUses[k]).ToArray();result.searchLimitHits=searchLimits;result.turnLimitHits=turnLimits;return result;
    }
    static bool Fight(string mode,string archetype,ref int totalTurns)
    {
        for(int turn=0;turn<80 && !player.IsDead && !enemy.IsDead;turn++)
        {
            searchNodesThisTurn=0;
            totalTurns++;enemy.BeginPlayerTurn();player.cardStatus.BeginPlayerTurn();player.currentCost=balance.maxCost+player.cardStatus.nextTurnCost;player.cardStatus.nextTurnCost=0;
            DrawCards.Invoke(battle,new object[]{balance.handDrawCount+player.cardStatus.BeginTurnDrawBonus()});BattleState.SetValue(battle,BattleTurnState.Player);
            int plays=0;
            while(hand.Count>0 && !player.IsDead && !enemy.IsDead && plays<100)
            {
                CardInstance chosen=Choose(mode,archetype);if(chosen==null)break;
                BattleState.SetValue(battle,BattleTurnState.Player);if(!battle.TryPlayCard(chosen))throw new Exception("Illegal policy action");
                plays++;
                string name=chosen.Data.name;actualUses[name]=actualUses.TryGetValue(name,out int count)?count+1:1;
            }
            // This is a policy computation budget, not a game defeat condition.
            // End the turn normally when a draw/refund loop exceeds the budget.
            if(plays>=100)limits++;
            if(player.IsDead || enemy.IsDead)break;
            FinishTurn();
        }
        if(!player.IsDead && !enemy.IsDead)turnLimits++;
        return !player.IsDead&&enemy.IsDead;
    }
    static void FinishTurn()
    {
        discard.AddRange(hand);hand.Clear();player.cardStatus.EndPlayerTurn(player);player.TickModifiers();enemy.TickPoison();
        for(int i=0;i<enemy.ActionsPerTurn && !player.IsDead && !enemy.IsDead;i++)
        {
            var action=enemy.GetNextAction();if(action==null)break;ApplyEnemy.Invoke(battle,new object[]{action});enemy.AdvanceActionIndex();
        }
        if(player.IsDead || enemy.IsDead)return;
        player.TickPoison();player.cardStatus.EndEnemyTurn();enemy.TickModifiers();
    }
    static float Affinity(CardData data,string archetype)
    {
        int index=Array.IndexOf(Packages[archetype],data.name);return index<0?0:13-index*.5f;
    }
    static bool IsBlockAnswer(CardData data) => data.mediumPerformance.effects.Any(e=>
        e.effectType==BattleEffectType.GrowingDamage || e.effectType==BattleEffectType.BlockDamage
        || e.effectType==BattleEffectType.RemoveBlock || e.effectType==BattleEffectType.ReduceBlock
        || (e.effectType==BattleEffectType.ApplyPoison && e.value*e.duration>=8));
    static bool IsAttackBuff(CardData data) => data.mediumPerformance.effects.Any(e=>
        e.target==EffectTarget.Self && e.value>0 && (e.effectType==BattleEffectType.CardDamageBonus
        || e.effectType==BattleEffectType.OutgoingDamageModifier || e.effectType==BattleEffectType.PermanentAttackBonus
        || e.effectType==BattleEffectType.ChainAttackBonus));
    static bool IsShieldControl(CardData data) => data.mediumPerformance.effects.Any(e=>e.target==EffectTarget.Enemy
        && (e.effectType==BattleEffectType.RemoveBlock || e.effectType==BattleEffectType.ReduceBlock
        || (e.effectType==BattleEffectType.BlockGainModifier && e.value<0)));
    static bool FunctionalDeck(string archetype)
    {
        var core=deck.currentDeck.Where(c=>Affinity(c.Data,archetype)>0).ToArray();
        if(core.Length<5 || core.Select(c=>c.Data.name).Distinct().Count()<3)return false;
        Func<string[],bool> has=names=>deck.currentDeck.Any(c=>names.Contains(c.Data.name));
        int repeatable=deck.currentDeck.Count(c=>c.Data.category==CardCategory.Attack && !c.Data.mediumPerformance.effects.Any(e=>e.effectType==BattleEffectType.RemainInDiscard));
        switch(archetype)
        {
            case "debuff":return has(new[]{"Seisai","Jakushagari"}) && deck.currentDeck.Count(c=>c.Data.mediumPerformance.effects.Any(e=>
                e.target==EffectTarget.Enemy && BattleModifiers.IsDebuff(new TimedModifier{effectType=e.effectType,value=e.value})))>=2;
            case "bleed":return has(new[]{"Ketsujin","Kutsuuzoufuku"}) && has(new[]{"Shukketsu","Shukketsukougeki","Shukketsunotate","Fukade"});
            case "revenge":return has(new[]{"Houfukubougyo","Houfukutaisei"}) && repeatable>=3;
            case "control":return has(new[]{"Jubaku","Fukade"}) && has(new[]{"Ketsujin","Shukketsunotate","Jakutainotate"});
            case "block":return has(new[]{"Burokkudekougeki"}) && has(new[]{"Kosutoonzon","Kosutokyouka","Gyakkyou"});
            case "growth":return has(new[]{"Tukautabinidame-jiappukougeki"}) && has(new[]{"Dorookyouka","Kosutokyouka"});
            case "attack":return deck.currentDeck.Any(c=>IsAttackBuff(c.Data)) && repeatable>=3;
            case "poison":return has(new[]{"Doku","Dokunokenn","Dokunotate"}) && deck.currentDeck.Count(c=>c.Data.category==CardCategory.Defense)>=3;
            default:return has(new[]{Packages[archetype][0]});
        }
    }
    static string ChooseArchetype(int pool)
    {
        // Adapt to owned cards and known fixed rolls. Shared support and repeated
        // copies do not substitute for an absent payoff or missing setup source.
        return Archetypes[pool].OrderByDescending(a=>
        {
            var matching=deck.currentDeck.Where(c=>Affinity(c.Data,a)>0).GroupBy(c=>c.Data.name).ToArray();
            float score=matching.Sum(g=>Affinity(g.First().Data,a)*(1+.4f*Math.Min(2,g.Count()-1))
                *(pools.All(p=>p.Contains(g.First().Data)) ? .45f : 1f));
            score+=deck.currentDeck.Any(c=>c.Data.name==Packages[a][0])?12:-8;
            if(matching.Length>=3)score+=6;
            if(a=="debuff" && !deck.currentDeck.Any(c=>c.Data.mediumPerformance.effects.Any(e=>e.target==EffectTarget.Enemy
                && BattleModifiers.IsDebuff(new TimedModifier{effectType=e.effectType,value=e.value}))))score-=20;
            if(a=="block" && !deck.currentDeck.Any(c=>c.Data.name=="Kosutoonzon" || c.Data.name=="Kosutokyouka" || c.Data.name=="Gyakkyou"))score-=6;
            return score;
        }).First();
    }
    static float DraftScore(CardData data,string archetype,string mode)
    {
        if(mode=="beginner")return (PrintedScore(data.lowPerformance)+PrintedScore(data.mediumPerformance)+PrintedScore(data.highPerformance))/3;
        float score=Affinity(data,archetype);
        if(score==0)score=data.name=="Jakutai"?9:data.name=="Kougekiryokuagenagarabougyo"?8:data.category==CardCategory.Defense?7:data.name=="Kaifuku"?10:4;
        if(mode=="expert" && data.name=="Zeijaku")score=Mathf.Max(8,score);
        if(mode=="expert" && data.name=="Suijaku")score=Mathf.Max(7,score);
        if(mode=="expert" && data.name=="Hasaikougeki")score=Mathf.Max(8,score);
        if(mode=="expert" && (data.name=="Dorookyouka" || data.name=="Kosutokyouka"))score=Mathf.Max(9,score);
        if(mode=="expert")
        {
            // Fill visible deck coverage gaps, rather than follow one package
            // blindly. Enemy roster knowledge is public; upcoming enemy draws are not.
            if(data.category==CardCategory.Defense && deck.currentDeck.Count(c=>c.Data.category==CardCategory.Defense)<3)
                score=Mathf.Max(12,score);
            bool hasBlockAnswer=deck.currentDeck.Any(c=>IsBlockAnswer(c.Data));
            if(!hasBlockAnswer && (data.name=="Kuzushi" || data.name=="Hasaikougeki" || data.name=="Doku" || data.name=="Dokunokenn"))
                score=Mathf.Max(12,score);
            // Poison can bypass a shield but cannot reduce Fortress Collapse.
            // Preserve a physical shield counter as a separate roster requirement.
            if(IsShieldControl(data) && !deck.currentDeck.Any(c=>IsShieldControl(c.Data)))score=Mathf.Max(13,score);
            if(data.name=="Kaifuku" && !deck.currentDeck.Any(c=>c.Data.name=="Kaifuku"))score=Mathf.Max(12,score);
            if(IsAttackBuff(data) && deck.currentDeck.Count(c=>IsAttackBuff(c.Data))<2)score=Mathf.Max(12,score);
            float meanCost=deck.currentDeck.Average(c=>c.IsPerformanceDetermined?c.CurrentPerformance.cost:
                (c.Data.lowPerformance.cost+c.Data.mediumPerformance.cost+c.Data.highPerformance.cost)/3f);
            if(data.name=="Kosutokyouka" && meanCost>=1.4f)score=Mathf.Max(12,score);
            bool requiresDebuffs=deck.currentDeck.Any(c=>c.Data.mediumPerformance.effects.Any(e=>
                e.effectType==BattleEffectType.DebuffCountDamage || e.effectType==BattleEffectType.DebuffCountBlock
                || e.effectType==BattleEffectType.DebuffCountCardBonus));
            Func<CardData,bool> suppliesDebuff=d=>d.mediumPerformance.effects.Any(e=>e.target==EffectTarget.Enemy
                && BattleModifiers.IsDebuff(new TimedModifier{effectType=e.effectType,value=e.value}));
            if(requiresDebuffs && deck.currentDeck.Count(c=>suppliesDebuff(c.Data))<2 && suppliesDebuff(data))
                score=Mathf.Max(14,score);
        }
        return score;
    }
    static float ExistingScore(CardInstance card,string archetype,string mode)
    {
        float score=DraftScore(card.Data,archetype,mode);
        if(mode!="expert" || !card.IsPerformanceDetermined)return score;
        // A card already in the deck fills its own coverage gap. Its marginal
        // value cannot be computed as if another card had filled that gap.
        // Preserve scarce counters and attack buffs during compression/replacement.
        if(IsBlockAnswer(card.Data) && deck.currentDeck.Count(c=>IsBlockAnswer(c.Data))<=2)score=Mathf.Max(12,score);
        if(IsAttackBuff(card.Data) && deck.currentDeck.Count(c=>IsAttackBuff(c.Data))<=2)score=Mathf.Max(12,score);
        if(IsShieldControl(card.Data) && deck.currentDeck.Count(c=>IsShieldControl(c.Data))<=1)score=Mathf.Max(13,score);
        // A known fixed roll can be replaced with an unknown card of the same type.
        float average=(PrintedScore(card.Data.lowPerformance)+PrintedScore(card.Data.mediumPerformance)+PrintedScore(card.Data.highPerformance))/3;
        return score+Mathf.Clamp((PrintedScore(card.CurrentPerformance)-average)/Mathf.Max(1,average)*2,-2,2);
    }
    static float PrintedScore(CardPerformance tier)
    {
        float value=0;
        foreach(var effect in tier.effects)
        {
            int t=(int)effect.effectType;float v=Math.Abs(effect.value);
            // Evaluate printed standalone performance consistently. Random multipliers
            // and HP denominators must not be mistaken for flat damage values.
            if(t==12)value+=v*(effect.minimumMultiplier+effect.maximumMultiplier)*.5f;
            else if(t==13)value+=50f/Math.Max(1,effect.value);
            else if(t==0||t==11||t==39||t==40||t==54||t==55)value+=effect.target==EffectTarget.Self?-v:v;
            else if(t==1)value+=v*.45f;
            else if(t==2)value+=v*effect.duration*.4f;
            else if(t==35||t==36)value+=v*.5f;
            else if(t==37||t==52||t==53)value+=v*2;
            else value+=v*.3f;
        }
        return value/(.75f+tier.cost);
    }
    static void PlayExpected(CardInstance card)
    {
        PlayEstimated(card,false);
    }
    static void PlayEstimated(CardInstance card,bool minimum)
    {
        var tier=card.CurrentPerformance;
        if(tier.effects.Any(e=>e.effectType==BattleEffectType.RandomDamage))
        {
            tier.effects=tier.effects.Select(e=>{ if(e.effectType==BattleEffectType.RandomDamage){e.value=minimum?e.value*Math.Min(e.minimumMultiplier,e.maximumMultiplier):Mathf.RoundToInt(e.value*(e.minimumMultiplier+e.maximumMultiplier)*.5f);e.effectType=BattleEffectType.Damage;}return e; }).ToList();
            Performance.SetValue(card,tier);
        }
        BattleState.SetValue(battle,BattleTurnState.Player);if(!battle.TryPlayCard(card))throw new Exception("Illegal probe");
    }
    static CardInstance Choose(string mode,string archetype)
    {
        var available=hand.Where(c=>{c.CostReduction=player.CardCostReduction;return c.EffectiveCost<=player.currentCost;}).ToArray();
        if(available.Length==0)return null;
        if(mode=="beginner")return available.OrderByDescending(c=>PrintedScore(c.CurrentPerformance)).First();
        if(mode=="expert")
        {
            var recovery=SafeRecoveryBeforeLethal(available);
            if(recovery!=null)return recovery;
        }
        if(mode=="expert" && searchNodesThisTurn<SearchNodesPerTurn)
        {
            CardInstance result=null;float searchBest=float.MinValue;int nodes=0;
            int budget=Math.Min(SearchNodesPerDecision,SearchNodesPerTurn-searchNodesThisTurn);
            expertRoot=new Snapshot();searchPath.Clear();
            // An initially unaffordable visible card may become legal after a
            // discount or refund. Keep all visible cards in the search tree;
            // affordability is checked at each node. Hidden future draws stay out.
            var visible=hand.ToList();
            searchBest=EndValue(archetype);expertRoot.Restore();
            // Share the budget across legal first moves. A deep branch for the
            // first card drawn must not exclude every other possible first move.
            for(int i=0;i<available.Length;i++)
            {
                int branchBudget=Math.Max(1,(budget-nodes)/(available.Length-i));
                searchNodeLimit=branchBudget;int branchNodes=0;
                PlayExpected(available[i]);searchPath.Add(available[i]);
                SearchVisible(visible.Where(c=>c!=available[i]).ToList(),available[i],archetype,
                    ref result,ref searchBest,ref branchNodes);
                searchPath.Clear();expertRoot.Restore();
                nodes+=branchNodes;
                if(branchNodes>branchBudget)searchLimits++;
            }
            expertRoot=null;
            searchNodesThisTurn+=nodes;
            return result;
        }
        var baseline=new Snapshot();float origin=Value(archetype);FinishTurn();float stopValue=Value(archetype);baseline.Restore();
        CardInstance best=null;float bestScore=plan.allowSynergyStop?0:float.MinValue;
        foreach(var candidate in available)
        {
            int cost=candidate.EffectiveCost;PlayExpected(candidate);
            float immediate=Value(archetype)-origin;
            FinishTurn();float ended=Value(archetype);float score=(immediate*.6f+ended-stopValue)/(1+.25f*cost);
            if(score>bestScore){bestScore=score;best=candidate;}
            baseline.Restore();
        }
        return best;
    }
    static CardInstance SafeRecoveryBeforeLethal(IEnumerable<CardInstance> available)
    {
        if(player.currentHP>=player.maxHP || player.cardStatus.usageBuffs.Any(b=>
            b.effect.effectType==BattleEffectType.EveryCardsDraw && b.progress+1>=Math.Max(1,b.effect.value)))return null;
        var heals=available.Where(c=>c.CurrentPerformance.effects.Any(e=>e.effectType==BattleEffectType.Heal && e.value>0)).ToArray();
        if(heals.Length==0)return null;
        // Enemy actions here are deterministic. Do not inspect a hidden random
        // outcome when an enemy definition has a random damage action.
        if(enemy.Data.actionPattern.Concat(enemy.Data.categoryActions).Any(a=>a!=null && a.effects.Any(e=>e.effectType==BattleEffectType.RandomDamage)))return null;
        var original=new Snapshot();int hpBefore=player.currentHP;int bestHP=hpBefore+2;CardInstance best=null;
        foreach(var heal in heals)
        {
            PlayExpected(heal);FinishTurn();int projectedHP=player.currentHP;
            if(!player.IsDead && projectedHP>bestHP)
            {
                bool guaranteed=enemy.IsDead;
                if(!guaranteed)
                {
                    enemy.BeginPlayerTurn();player.cardStatus.BeginPlayerTurn();
                    player.currentCost=balance.maxCost+player.cardStatus.nextTurnCost;player.cardStatus.nextTurnCost=0;
                    int nextDraw=balance.handDrawCount+player.cardStatus.BeginTurnDrawBonus();
                    guaranteed=GuaranteedLethalDraw(nextDraw);
                }
                if(guaranteed){best=heal;bestHP=projectedHP;}
            }
            original.Restore();
        }
        return best;
    }
    static bool GuaranteedLethalDraw(int count)
    {
        // Card identities in the unordered piles are public. Enumerate every
        // possible next draw, never the RNG-selected identity or unknown roll.
        if(count<=0)return false;
        var remainingPile=draw.ToArray();
        if(remainingPile.Length>=count)return remainingPile.Count(c=>GuaranteedSingleCardLethal(c))>remainingPile.Length-count;
        if(remainingPile.Any(c=>GuaranteedSingleCardLethal(c)))return true;
        int remaining=count-remainingPile.Length;
        var recyclable=discard.Where(c=>!c.RemainsInDiscard).ToArray();
        remaining=Math.Min(remaining,recyclable.Length);
        return remaining>0 && recyclable.Count(c=>GuaranteedSingleCardLethal(c))>recyclable.Length-remaining;
    }
    static bool GuaranteedSingleCardLethal(CardInstance card)
    {
        var tiers=card.IsPerformanceDetermined?new[]{card.CurrentPerformance}:
            new[]{card.Data.lowPerformance,card.Data.mediumPerformance,card.Data.highPerformance};
        var nextTurn=new Snapshot();bool valid=true;
        foreach(var tier in tiers)
        {
            Performance.SetValue(card,tier);Determined.SetValue(card,true);card.CostReduction=player.CardCostReduction;
            if(card.EffectiveCost>player.currentCost)valid=false;
            else
            {
                hand.Add(card);PlayEstimated(card,true);
                valid=enemy.IsDead && !player.IsDead;
            }
            nextTurn.Restore();
            if(!valid)break;
        }
        return valid;
    }
    static void VerifyRecoveryPolicy()
    {
        var prior=UnityEngine.Random.state;var owned=new List<CardData>();
        Func<int,int,bool,CardData> definition=(damage,cost,heal)=>
        {
            var data=ScriptableObject.CreateInstance<CardData>();owned.Add(data);data.category=heal?CardCategory.Buff:CardCategory.Attack;
            data.lowPerformance=data.mediumPerformance=data.highPerformance=new CardPerformance{cost=cost,effects=new List<BattleEffect>
                {new BattleEffect{effectType=heal?BattleEffectType.Heal:BattleEffectType.Damage,target=heal?EffectTarget.Self:EffectTarget.Enemy,value=damage}}};return data;
        };
        try
        {
            var attack=definition(10,3,false);var healing=definition(15,2,true);var weak=definition(0,3,false);
            Action prepare=()=>
            {
                player.ResetForBattle();player.currentHP=20;player.currentCost=3;player.currentBlock=20;
                enemy.Setup(enemies.normalEnemies[5],1);enemy.currentHP=5;
                deck.currentDeck.Clear();hand.Clear();draw.Clear();discard.Clear();
            };
            Func<CardData,bool,CardInstance> card=(data,known)=>
            {
                var instance=new CardInstance(data);deck.currentDeck.Add(instance);
                if(known){Performance.SetValue(instance,data.highPerformance);Determined.SetValue(instance,true);}return instance;
            };
            prepare();var lethal=card(attack,true);var healCard=card(healing,true);hand.Add(lethal);hand.Add(healCard);
            draw.Add(card(attack,false));draw.Add(card(attack,false));
            var state=UnityEngine.Random.state;
            if(SafeRecoveryBeforeLethal(hand)!=healCard)throw new Exception("Safe recovery before a guaranteed public-pile lethal was missed");
            if(player.currentHP!=20 || player.currentCost!=3 || enemy.currentHP!=5 || hand.Count!=2 || draw.Count!=2
                || draw.Any(c=>c.IsPerformanceDetermined) || !state.Equals(UnityEngine.Random.state))throw new Exception("Recovery probe leaked state or unknown rolls");
            draw.Clear();for(int i=0;i<3;i++)draw.Add(card(attack,false));for(int i=0;i<5;i++)draw.Add(card(weak,false));
            if(SafeRecoveryBeforeLethal(hand)!=null)throw new Exception("A possible lethal was incorrectly treated as guaranteed");
            prepare();healCard=card(healing,true);hand.Add(healCard);
            var variable=definition(0,3,false);variable.highPerformance=new CardPerformance{cost=3,effects=attack.highPerformance.effects};
            var unknown=card(variable,false);draw.Add(unknown);
            if(SafeRecoveryBeforeLethal(hand)!=null || unknown.IsPerformanceDetermined)throw new Exception("Recovery used an unknown high roll");
            Performance.SetValue(unknown,variable.highPerformance);Determined.SetValue(unknown,true);
            if(SafeRecoveryBeforeLethal(hand)!=healCard)throw new Exception("Recovery ignored a known fixed lethal roll");
            player.currentHP=50;
            if(SafeRecoveryBeforeLethal(hand)!=null)throw new Exception("Recovery delayed a fight at full HP");
        }
        finally
        {
            deck.currentDeck.Clear();hand.Clear();draw.Clear();discard.Clear();player.ResetForBattle();player.currentHP=player.maxHP=50;
            enemy.Setup(enemies.normalEnemies[0],1);foreach(var data in owned)UnityEngine.Object.DestroyImmediate(data);UnityEngine.Random.state=prior;
        }
    }
    static void SearchVisible(List<CardInstance> available,CardInstance first,string archetype,ref CardInstance best,ref float bestValue,ref int nodes)
    {
        if(++nodes>searchNodeLimit)return;
        var snapshot=new Snapshot();float value=EndValue(archetype);snapshot.Restore();
        if(plan.riskAwareExpert && value>=100000 && searchPath.Any(c=>c.Data.lowPerformance.effects.Any(e=>e.effectType==BattleEffectType.RandomDamage)))
        {
            // A visible random attack is not a guaranteed lethal hit. Reevaluate
            // the same sequence at its printed minimum, without inspecting RNG.
            expertRoot.Restore();
            foreach(var card in searchPath)
            {
                if(player.IsDead || enemy.IsDead)break;
                PlayEstimated(card,true);
            }
            value=value*.75f+EndValue(archetype)*.25f;snapshot.Restore();
        }
        if(value>bestValue){bestValue=value;best=first;}
        if(player.IsDead || enemy.IsDead)return;
        var seen=new HashSet<string>();
        foreach(var card in available)
        {
            if(!hand.Contains(card))continue;card.CostReduction=player.CardCostReduction;if(card.EffectiveCost>player.currentCost)continue;
            string signature=card.Data.name+":"+card.CurrentPerformance.effects.GetHashCode()+":"+card.TimesPlayed;
            if(!seen.Add(signature))continue;
            PlayExpected(card);searchPath.Add(card);
            SearchVisible(available.Where(c=>c!=card).ToList(),first??card,archetype,ref best,ref bestValue,ref nodes);
            searchPath.RemoveAt(searchPath.Count-1);snapshot.Restore();
        }
    }
    static float EndValue(string archetype)
    {
        FinishTurn();float value=Value(archetype);
        if(!player.IsDead && !enemy.IsDead)
        {
            enemy.BeginPlayerTurn();player.cardStatus.BeginPlayerTurn();
            value-=EnemyIntentForecast.Create(enemy,player,true).hpDamage*.7f;
        }
        return value;
    }
    static float Value(string archetype)
    {
        if(player.IsDead)return -1000000;
        if(enemy.IsDead)return 100000+player.currentHP*20;
        bool skilled=currentJob.mode=="expert";
        bool poisonDeadline=false;
        if(skilled && !PoisonPatterns.TryGetValue(enemy.Data,out poisonDeadline))
        {
            poisonDeadline=enemy.Data.actionPattern.Concat(enemy.Data.categoryActions).Any(a=>a!=null && a.effects.Any(e=>e.effectType==BattleEffectType.ApplyPoison));
            PoisonPatterns[enemy.Data]=poisonDeadline;
        }
        // HP carries across ten fights. Against ordinary damage, bank protection
        // before accumulating a recovery deficit. Poison calls for faster lethal.
        float hpWeight=skilled?(poisonDeadline?1.9f:3f):1.7f;
        float blockWeight=skilled ? (poisonDeadline ? .25f : .45f) : .2f;
        float value=player.currentHP*hpWeight-Mathf.Max(0,20-player.currentHP)*1.8f-enemy.currentHP*1.2f-enemy.currentBlock*.65f
            +(archetype=="block"?player.currentBlock*.65f:Mathf.Min(60,player.currentBlock)*blockWeight);
        value+=enemy.poison.Stacks.Sum(s=>s.damagePerTurn*Mathf.Min(s.remainingTurns,3))*.85f;
        value-=player.poison.Stacks.Sum(s=>s.damagePerTurn*Mathf.Min(s.remainingTurns,3))*1.1f;
        value+=enemy.bleeding.TotalValue*1.7f+enemy.electrocution.TotalValue*1.4f;
        value-=player.bleeding.TotalValue*1.5f+player.electrocution.TotalValue*1.2f;
        value+=player.modifiers.Active.Sum(m=>!BattleModifiers.IsDebuff(m)?Math.Abs(m.value)*Mathf.Min(m.remainingTurns,3): -Math.Abs(m.value)*Mathf.Min(m.remainingTurns,3))*1.2f;
        value-=enemy.AttackBonus*2;
        value+=enemy.modifiers.DebuffCount*1.2f;
        value+=player.cardStatus.endTurnBlocks.Sum(m=>m.value*Mathf.Min(m.remainingTurns,3))*.9f;
        value+=player.cardStatus.blockBonuses.Sum(m=>m.value*Mathf.Min(m.remainingTurns,3))*.8f;
        value+=Mathf.Min(12,player.cardStatus.extraDraws.Sum(m=>m.value*Mathf.Min(m.remainingTurns,3)))*1.8f;
        if(currentJob.mode=="expert" && player.cardStatus.reactions.Count>0)
        {
            // HP-only retaliation cannot fire behind an intact shield. Ordinary
            // thorns and poison retaliation can, including against multi-hit enemies.
            var forecast=EnemyIntentForecast.Create(enemy,player,true);
            int shield=player.currentBlock+player.cardStatus.endTurnBlocks.Sum(m=>m.value);
            float hpTrigger=forecast.attackDamage>shield?1f:.25f;
            value+=player.cardStatus.reactions.Sum(r=>Math.Abs(r.damage)*Mathf.Min(r.turns,3)
                *(r.poisonTurns>0?Math.Min(r.poisonTurns,3):1)*enemy.ActionsPerTurn
                *(r.effectType==BattleEffectType.HPDamageRetaliation?hpTrigger:1f))*.8f;
        }
        else value+=player.cardStatus.reactions.Sum(r=>Math.Abs(r.damage)*Mathf.Min(r.turns,2)*(r.poisonTurns>0?Math.Min(r.poisonTurns,3):1))*.8f;
        // Additional setup has diminishing value once enough draw/cost is available.
        // This changes the test player's judgement, never the battle rules.
        value+=Mathf.Min(20,player.cardStatus.usageBuffs.Sum(b=>Mathf.Min(b.turns,3)*(b.effect.effectType==BattleEffectType.ChainAttackBonus?b.effect.value*2:b.effect.effectType==BattleEffectType.CategoryElectrocution?b.effect.value*.8f:4f/Math.Max(1,b.effect.value))));
        value+=Mathf.Min(8,player.cardStatus.lightweightRemaining.Sum(n=>Math.Min(n,4)))*1.4f;
        value+=Mathf.Min(20,player.cardStatus.ChainAttackBonus)*.8f+Mathf.Min(6,player.cardStatus.nextTurnCost)*2+Mathf.Min(6,player.currentCost)*.35f;
        value+=player.cardStatus.damageTriggeredBonuses.Sum(s=>Mathf.Min(s.remainingTurns,2)*3f/Math.Max(1,s.value));
        value+=deck.currentDeck.Sum(c=>c.Data.name=="Tukautabinidame-jiappukougeki"?c.TimesPlayed*1.5f:0);
        // Count new draws, but never score their identities or hidden performance results.
        value+=hand.Count*.45f;
        return value;
    }
}
