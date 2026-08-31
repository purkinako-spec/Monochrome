using MiniGame.Fish;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class FishingMinigameManager : MonoBehaviour
{
    public enum FishingMiniGameStates
    {
        Neutral,            
        WatingForBite,      // 魚がかかるのを待つ
        Hooked,             // 魚がかかった
        Reeling,            // 釣り上げ中
        Finished            // 終了
    }

    private FishingMiniGameStates fishingMiniGameState = FishingMiniGameStates.Neutral;

    private void Update()
    {
        switch(fishingMiniGameState) 
        {
            case FishingMiniGameStates.Neutral:
                StartFishingMiniGame();
                break;

            case FishingMiniGameStates.WatingForBite:
                ChackPoint();
                break;

            case FishingMiniGameStates.Hooked:
                ChangeMoveObjectsState(false);
                fishingMiniGameState = FishingMiniGameStates.Reeling;
                break;

            case FishingMiniGameStates.Reeling:

                break;

            case FishingMiniGameStates.Finished:
                break;
        }
    }

    
    // 魚釣り開始-------------------------------------------------------------
    public void StartFishingMiniGame()
    {
        // 釣り開始
        // 魚出現、WaitingForBiteに
        InitFish();
        InitLure();
        fishingMiniGameState = FishingMiniGameStates.WatingForBite;
    }
    
    [SerializeField] private Collider2D moveArea;
    [SerializeField] private GameObject fishGameObject;
    private FishStatus fishStatus;

    private void InitFish()
    {
        // 画像などの情報を確認し、魚の情報を初期化
        fishStatus = GetFishData();

        fishGameObject.GetComponent<FishMovement>().SetStatus(fishStatus);
        fishGameObject.GetComponent<FishMovement>().ChangeWait(true);
        
    }

    private Vector2 GetMovePosition()
    {
        Bounds bounds = moveArea.bounds;

        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);

        return new Vector2(x, y);
    }

    [SerializeField] List<MiniGame.Fish.FishStatus> FishStatusList;
    private FishStatus GetFishData()
    {
        int statusCount = FishStatusList.Count;
        Debug.Log(statusCount);
        float totalRate = 0;
        float tmpTotalRate = 0;

        foreach (FishStatus status in FishStatusList) totalRate += status.rate;

        float targetRate = Random.Range(0, totalRate);

        foreach (FishStatus status in FishStatusList)
        {
            tmpTotalRate += status.rate;
            
            if(tmpTotalRate >= targetRate)
            {
                return status;
            }
        }
        return new FishStatus();
    }

    [SerializeField] private GameObject LureGameObject;
    private void InitLure()
    {
        // 中心にルアーを移動状態の初期化を
        LureGameObject.SetActive(true);
        LureGameObject.transform.position = Vector2.zero;
        LureGameObject.GetComponent<LureMovement>().ChantaCanInput(true);
    }

    //-----------------------------------------------------------

    [SerializeField] private GameObject fishObject;
    [SerializeField] private GameObject lureObject;
    [SerializeField] private float inlineDiatance;
    [SerializeField] private float hituyouPoint;
    private float currentPoint = 0;
    private void ChackPoint()
    {
        if(Vector2.Distance(fishObject.transform.position,lureObject.transform.position) <= inlineDiatance)
        {
            currentPoint += Time.deltaTime;
        }

        if(currentPoint >= hituyouPoint)
        {
            currentPoint = 0;
            fishingMiniGameState = FishingMiniGameStates.Hooked;
        }
    }



    //-----------------------------------------------------------

    private void ChangeMoveObjectsState(bool state)
    {
        fishGameObject.GetComponent<FishMovement>().ChangeWait(state);
        LureGameObject.GetComponent<LureMovement>().ChantaCanInput(state);
    }


    //-----------------------------------------------------------
    [SerializeField] private GameObject reedingGameObject;
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] private GameObject centerGameObject;
    [SerializeField] private float distance;

    private void Reeding()
    {

    }


    //-----------------------------------------------------------
    public void EndFishingMiniGame()
    {
        // 釣り上げ演出で終了
        
    }

}
