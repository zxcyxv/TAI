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
    [SerializeField] private int gamesPerFile = 100;

    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int completedGames;
    [SerializeField] private int currentStepCount;
    [SerializeField] private bool collectionDone;

    private readonly List<BoardData> currentGameSteps = new List<BoardData>(100);
    private BoardData pendingStep;
    private bool updated = true;
    private StreamWriter writer;
    private string datasetsDir;
    private int currentFileIndex;
    private Board board;

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

        datasetsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "datasets"));
        Directory.CreateDirectory(datasetsDir);
        currentFileIndex = 1;
        OpenNextFile();
        board = FindFirstObjectByType<Board>();
    }

    void OnDestroy()
    {
        FlushAndClose();
    }

    void OnApplicationQuit()
    {
        FlushAndClose();
    }

    private void OpenNextFile()
    {
        FlushAndClose();
        string path = Path.Combine(datasetsDir, $"dataset_{currentFileIndex:D4}.jsonl");
        writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8);
        Debug.Log($"[DataHandler] Writing to: {path}");
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
            Debug.Log($"[DataHandler] Collection complete! Dir: {datasetsDir}");
            ColdClearAgent.Instance?.SetActive(false);
            return;
        }

        if (completedGames % gamesPerFile == 0)
        {
            currentFileIndex++;
            OpenNextFile();
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

    public void SetTargetGames(int count)
    {
        if (count <= 0) return;
        targetGames = count;
        Debug.Log($"[DataHandler] targetGames set to {targetGames}");
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
