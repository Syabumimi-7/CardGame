using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>報酬開封から獲得・次戦までを実際のUIで確認。シーンは保存しません。</summary>
[InitializeOnLoad]
public static class RewardOpeningVerification
{
    private const string Key = "RewardOpeningVerification";
    private const string Report = "StageReview/reward-opening-verification.txt";
    static RewardOpeningVerification() { EditorApplication.update += Update; }

    private static void Update()
    {
        const string request = "Temp/RewardOpening.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(request))
        {
            File.Delete(request);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.WriteAllText(Report, "Reward opening verification " + DateTime.Now.ToString("s") + "\n");
            SessionState.SetInt(Key + "Pack", 0);
            SessionState.SetInt(Key, 1);
            SessionState.SetFloat(Key + "Due", 0);
            SessionState.SetFloat(Key + "Deadline", float.MaxValue);
            EditorApplication.EnterPlaymode();
        }
        int step = SessionState.GetInt(Key, 0);
        if (step == 0 || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.realtimeSinceStartup < SessionState.GetFloat(Key + "Due", 0)) return;
        try
        {
            if (Time.realtimeSinceStartup > SessionState.GetFloat(Key + "Deadline", float.MaxValue))
                throw new InvalidOperationException("Timed out at step " + step);
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            int packIndex = SessionState.GetInt(Key + "Pack", 0);
            var opening = game.GetComponent<PackOpeningPresentation>();
            if (step == 1)
            {
                Application.runInBackground = true;
                game.StartGame(packIndex);
                Next(2, 4);
            }
            else if (step == 2) { game.OnClickStartBattle(); Next(3, 5); }
            else if (step == 3)
            {
                Require(game.battleManager.CanPlayerAct, "Battle ready");
                game.battleManager.StartCoroutine("FinishBattle", true);
                Next(4, 1);
            }
            else if (step == 4)
            {
                Click("次のバトルへ");
                Next(41, .3f);
            }
            else if (step == 41)
            {
                Click("新たなカードを獲得");
                Require(opening.IsPlaying, "Reward starts opening");
                var clone = (RectTransform)GameObject.Find("OpeningPack").transform;
                Require(clone.anchoredPosition == Vector2.zero, "Reward pack centered from start");
                Require(clone.Find("Body").GetComponent<PackWrapperGraphic>().palette == packIndex, "Uses starting pack appearance");
                Require(clone.GetComponent<CanvasGroup>().alpha == 0, "Pack waits for other UI to fade");
                Click("新たなカードを獲得", true);
                Require(UnityEngine.Object.FindObjectsOfType<Canvas>().Count(c => c.name == "PackOpeningOverlay") == 1, "Duplicate click blocked");
                Next(5, .7f);
            }
            else if (step == 5)
            {
                Require(game.battleUIRoot.GetComponent<CanvasGroup>().alpha < .01f, "Battle and reward UI faded");
                Require(GameObject.Find("RewardGrid") == null, "Cards wait for whiteout");
                ScreenCapture.CaptureScreenshot("StageReview/reward-pack-" + packIndex + ".png");
                Next(6, 3);
            }
            else if (step == 6)
            {
                if (opening.IsPlaying) return;
                var panel = GameObject.Find("CardRewardPanel");
                Require(panel != null && panel.transform.Find("CardAcquisitionBackground") != null, "Shared acquisition background shown");
                if (!panel.GetComponent<CanvasGroup>().interactable) return;
                var cards = panel.GetComponentsInChildren<RewardCardClickHandler>();
                Require(cards.Length == game.battleManager.balanceData.rewardCardCandidateCount, "All reward candidates displayed");
                Require(cards.All(c => Vector3.Distance(c.transform.localScale, Vector3.one) < .01f), "Card entry completed");
                Require(game.deckManager.currentDeck.Count == 10, "Opening does not acquire cards automatically");
                ScreenCapture.CaptureScreenshot("StageReview/reward-cards-" + packIndex + ".png");
                Next(7, .3f);
            }
            else if (step == 7)
            {
                var card = GameObject.Find("CardRewardPanel").GetComponentInChildren<RewardCardClickHandler>();
                Require(Hits(card.transform), "Reward card receives pointer input");
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Next(71, .3f);
            }
            else if (step == 71)
            {
                Click("獲得");
                Require(game.deckManager.currentDeck.Count == 11, "Selected reward added exactly once");
                Next(70, .3f);
            }
            else if (step == 70)
            {
                Click("現在のデッキ");
                Require(GameObject.Find("CurrentDeckPanel").GetComponentsInChildren<CardUIView>().Length == 11, "Deck inspection works");
                Next(72, .3f);
            }
            else if (step == 72)
            {
                Click("もどる");
                Next(8, .3f);
            }
            else if (step == 8) { Click("次のバトルへ"); Next(9, 5); }
            else if (step == 9)
            {
                Require(game.battleManager.currentBattleCount == 2 && game.battleManager.CanPlayerAct, "Next battle starts");
                Require(game.battleUIRoot.GetComponent<CanvasGroup>().alpha == 1, "Battle UI restored");
                File.AppendAllText(Report, "PASS pack" + (packIndex + 1) + ": central pack, UI fade, shared opening/background, candidate reveal, acquisition, deck inspection, next battle.\n");
                if (packIndex < 2)
                {
                    game.ShowTitle();
                    SessionState.SetInt(Key + "Pack", packIndex + 1);
                    Next(1, 1);
                }
                else
                {
                    game.battleManager.StartCoroutine("FinishBattle", true);
                    Next(10, 1);
                }
            }
            else if (step == 10)
            {
                Click("次のバトルへ"); Next(101, .3f);
            }
            else if (step == 101)
            {
                Click("新たなカードを獲得");
                Next(11, .9f);
            }
            else if (step == 11) { game.ShowTitle(); Next(12, 1); }
            else if (step == 12)
            {
                Require(!opening.IsPlaying && GameObject.Find("PackOpeningOverlay") == null, "Cancellation cleans opening");
                Require(game.startScreenRoot.activeInHierarchy && game.battleUIRoot.GetComponent<CanvasGroup>().alpha == 1, "Cancellation restores UI");
                File.AppendAllText(Report, "PASS repeated reward opening and cancellation.\n");
                SessionState.SetInt(Key, 0);
                EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e + "\n");
            SessionState.SetInt(Key, 0);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Click(string name, bool allowBlocked = false)
    {
        Button button = UnityEngine.Object.FindObjectsOfType<Button>().LastOrDefault(b => b.name == name);
        Require(button != null, "Button present: " + name);
        if (!allowBlocked) Require(button.IsInteractable() && Hits(button.transform), "Button receives pointer input: " + name);
        button.onClick.Invoke();
    }
    private static bool Hits(Transform target)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)target;
        Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        if (hits.Count == 0 || !(hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target)))
            File.AppendAllText(Report, "Raycast " + target.name + ": " + string.Join(", ", hits.Take(5).Select(h => h.gameObject.name)) + "\n");
        return hits.Count > 0 && (hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Next(int step, float delay)
    {
        SessionState.SetInt(Key, step);
        SessionState.SetFloat(Key + "Due", Time.realtimeSinceStartup + delay);
        SessionState.SetFloat(Key + "Deadline", Time.realtimeSinceStartup + delay + 15);
    }
}
