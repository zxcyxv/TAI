using System.Collections.Generic;
using UnityEngine;

public class DataHandler : MonoBehaviour
{
    public static DataHandler Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private List<BoardData> boardDataList = new List<BoardData>();
    private bool updated = true;

    public void ClearData()
    {
        boardDataList.Clear();
        updated = true;
    }

    public void SaveData()
    {
        if (updated) return;
        if (DataManager.Instance == null) return;
        if (boardDataList == null)
        {
            boardDataList = new List<BoardData>();
        }

        updated = true;

        BoardData data = new BoardData
        (
            DataManager.Instance.GetCanHold(),
            DataManager.Instance.GetHoldDataMino(),
            DataManager.Instance.GetPreviewDataMino(),
            DataManager.Instance.GetCurrentDataMino(),
            DataManager.Instance.GetBoardData()
        );

        boardDataList.Add(data);
        Debug.Log(data);
    }

    public void UpdateBoard()
    {
        updated = false;
        Debug.Log("Board Updated");
    }
}
