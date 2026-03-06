using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class DataHandler : MonoBehaviour
{
    public static DataHandler Instance;

    [Header("Collection Settings")]
    [SerializeField] private int targetGames = 5000;
    [SerializeField] private int stepsPerGame = 100;
    [SerializeField] private Board board;

    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int completedGames;
    [SerializeField] private int currentStepCount;
    [SerializeField] private bool collectionDone;

    private readonly List<BoardData> currentGameSteps = new List<BoardData>(100);
    private BoardData pendingStep;
    private bool updated = true;
    private StreamWriter writer;
    private string outputPath;

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
            return;
        }

        outputPath = Path.Combine(Application.persistentDataPath, "dataset.jsonl");
        writer = new StreamWriter(outputPath, append: false, encoding: Encoding.UTF8);
        Debug.Log($"[DataHandler] Dataset will be saved to: {outputPath}");
    }

    void OnDestroy()
    {
        FlushAndClose();
    }

    void OnApplicationQuit()
    {
        FlushAndClose();
    }

    private void FlushAndClose()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }

    // Called by Board every time the board state changes
    public void UpdateBoard()
    {
        updated = false;
    }

    // Called by Board at piece spawn — captures one step
    public void SaveData()
    {
        if (collectionDone) return;
        if (updated) return;
        if (DataManager.Instance == null) return;

        updated = true;

        pendingStep = new BoardData(
            DataManager.Instance.GetCanHold(),
            DataManager.Instance.GetHoldDataMino(),
            DataManager.Instance.GetPreviewDataMino(),
            DataManager.Instance.GetCurrentDataMino(),
            DataManager.Instance.GetBoardData()
        );

        currentGameSteps.Add(pendingStep);
        currentStepCount = currentGameSteps.Count;

        if (currentGameSteps.Count >= stepsPerGame)
        {
            FinishCurrentGame();
        }
    }

    // Called by ColdClearAgent after CoT is generated — attaches reasoning to latest step
    public void AttachCoT(string cotReason)
    {
        pendingStep?.SetCoT(cotReason);
    }

    // Called by ColdClearAgent with the chosen move — attaches action label to latest step
    public void AttachAction(bool hold, byte[] expectedX, byte[] expectedY)
    {
        pendingStep?.SetAction(hold, expectedX, expectedY);
    }

    // Called by Board on GameOver — discards incomplete games
    public void OnGameOver()
    {
        if (collectionDone) return;

        // Game ended before reaching stepsPerGame — discard partial data
        currentGameSteps.Clear();
        currentStepCount = 0;
        pendingStep = null;
    }

    private void FinishCurrentGame()
    {
        WriteGameToFile(completedGames + 1, currentGameSteps);

        completedGames++;
        currentGameSteps.Clear();
        currentStepCount = 0;
        pendingStep = null;

        Debug.Log($"[DataHandler] Game {completedGames}/{targetGames} collected.");

        if (completedGames >= targetGames)
        {
            FlushAndClose();
            collectionDone = true;
            Debug.Log($"[DataHandler] Collection complete! File: {outputPath}");
            ColdClearAgent.Instance?.SetActive(false);
            return;
        }

        // Force-restart the board to start the next game cleanly
        board?.RestartGame();
    }

    private void WriteGameToFile(int gameIndex, List<BoardData> steps)
    {
        if (writer == null) return;

        StringBuilder sb = new StringBuilder(steps.Count * 512);
        sb.Append("{\"game\":");
        sb.Append(gameIndex);
        sb.Append(",\"steps\":[");

        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(steps[i].ToJson());
        }

        sb.Append("]}");
        writer.WriteLine(sb.ToString());
    }

    // Legacy helper — kept for compatibility
    public void ClearData()
    {
        currentGameSteps.Clear();
        currentStepCount = 0;
        pendingStep = null;
        updated = true;
    }
}
