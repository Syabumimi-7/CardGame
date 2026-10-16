using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RuinsStagePlayVerification
{
    private const string Key="RuinsStageVerification";
    private const string Report="StageReview/play-verification.txt";
    static RuinsStagePlayVerification()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=OnLog;
    }
    [MenuItem("Tools/Stage/Verify Refined Stage In Play Mode")]
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene first.");
        string path=SceneManager.GetActiveScene().path;
        if(path!=RuinsStageBuilder.ScenePath && path!="Assets/TitleScene.unity")throw new InvalidOperationException("Open refined stage scene first.");
        File.WriteAllText(Report,"Scene: "+path+"\n");
        SessionState.SetInt(Key,1);SessionState.SetFloat(Key+"Time",-1);
        SessionState.SetInt(Key+"Errors",0);
        EditorApplication.EnterPlaymode();
    }
    private static void OnLog(string condition,string stack,LogType type)
    {
        if(SessionState.GetInt(Key,0)==0 || type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
        SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);
        File.AppendAllText(Report,"ERROR: "+condition+"\n");
    }
    private static void Update()
    {
        int step=SessionState.GetInt(Key,0);
        if(step==0 || !EditorApplication.isPlaying)return;
        if(Time.realtimeSinceStartup<SessionState.GetFloat(Key+"Time",-1))return;
        try
        {
            var game=Object.FindObjectOfType<GameManager>();
            if(step==1)
            {
                Require(game!=null && game.deckManager!=null && game.battleManager!=null,"Game managers present");
                Require(GameObject.Find("Ruins Stage")!=null,"Scenery present in Play mode");
                game.StartGame(0);Next(2,5);return;
            }
            if(step==2)
            {
                Require(game.deckPreviewRoot.activeInHierarchy,"Deck preview opens");
                game.OnClickStartBattle();Next(3,6);return;
            }
            if(step==3)
            {
                var battle=game.battleManager;
                Require(game.battleUIRoot.activeInHierarchy && battle.CanPlayerAct,"Battle opens and player can act");
                Require(battle.enemyManager.CurrentEnemy!=null,"Enemy spawned");
                Require(battle.Hand.Count>0,"Hand populated");
                var root=GameObject.Find("Ruins Stage");
                Require(root.GetComponentsInChildren<Collider>().Length==0,"Scenery introduces no gameplay colliders");
                Require(root.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null && m.shader!=null && m.shader.isSupported)),"All scenery materials supported");
                ScreenCapture.CaptureScreenshot("StageReview/ruins-battle.png");
                SessionState.SetInt(Key+"Turn",battle.Hand.Count);
                Next(4,2);return;
            }
            if(step==4)
            {
                Require(File.Exists("StageReview/ruins-battle.png"),"Battle screenshot saved");
                game.battleManager.EndPlayerTurn();Next(5,8);return;
            }
            if(step==5)
            {
                Require(game.battleManager.CanPlayerAct,"Enemy turn completes and control returns");
                Require(SessionState.GetInt(Key+"Errors",0)==0,"No runtime errors during verification");
                File.AppendAllText(Report,"PASS: title/deck/battle, enemy, cards, materials, end turn, no runtime errors.\n");
                SessionState.SetInt(Key,0);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception e)
        {
            File.AppendAllText(Report,"FAIL: "+e+"\n");SessionState.SetInt(Key,0);EditorApplication.ExitPlaymode();
        }
    }
    private static void Next(int step,float delay) {SessionState.SetInt(Key,step);SessionState.SetFloat(Key+"Time",Time.realtimeSinceStartup+delay);}
    private static void Require(bool condition,string description)
    {
        if(!condition)throw new InvalidOperationException(description);
        File.AppendAllText(Report,"PASS: "+description+"\n");
    }
}
