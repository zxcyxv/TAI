using System.IO;
using System.Text;
using UnityEngine;

public class DataHandler : MonoBehaviour
{
    public static DataHandler Instance;

    [Header("Collection Settings")]
    [SerializeField] private int targetGames = 5000;
    [SerializeField] private int stepsPerGame = 100;
    [SerializeField] private int maxLinesPerFile = 10000;
    [SerializeField] private int collectorSeed = 1337;
    [SerializeField] private string engineCommit = "";

    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int completedGames;
    [SerializeField] private int currentStepCount;
    [SerializeField] private bool collectionDone;

    private DecisionPacket pendingSpawnStep;
    private DecisionPacket stagedStep;
    private StreamWriter writer;
    private string datasetsDir;
    private int currentFileIndex;
    private int currentEpisodeId = 1;
    private int nextStepId;
    private int writtenLinesInFile;
    private string runId;
    private Board board;
    private bool pendingRestartAfterLock;
    private bool updated = true;
    private CollectorMeta collectorMeta;

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
        runId = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        collectorMeta = new CollectorMeta
        {
            runId = runId,
            collectorSeed = collectorSeed,
            engineCommit = string.IsNullOrWhiteSpace(engineCommit) ? "unknown" : engineCommit,
            weightsHash = "",
            optionsHash = "",
            bookEnabled = false,
        };
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

    public void ConfigureCollectorMeta(string weightsHash, string optionsHash, bool bookEnabled)
    {
        collectorMeta.weightsHash = weightsHash ?? string.Empty;
        collectorMeta.optionsHash = optionsHash ?? string.Empty;
        collectorMeta.bookEnabled = bookEnabled;
    }

    private void OpenNextFile()
    {
        FlushAndClose();
        string path = Path.Combine(datasetsDir, $"dataset_{currentFileIndex:D4}.jsonl");
        writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8);
        writtenLinesInFile = 0;
        Debug.Log($"[DataHandler] Writing to: {path}");
    }

    private void FlushAndClose()
    {
        if (writer != null && stagedStep != null)
        {
            stagedStep.gameComplete = false;
            writer.WriteLine(stagedStep.ToJsonLine(collectorMeta));
            writer.Flush();
            stagedStep = null;
        }
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }

    public void UpdateBoard()
    {
        updated = false;
    }

    public void SaveData()
    {
        if (collectionDone || updated)
        {
            return;
        }

        updated = true;
        pendingSpawnStep = new DecisionPacket();
    }

    public void AttachEngineJson(string engineJson)
    {
        if (pendingSpawnStep == null || collectionDone)
        {
            return;
        }

        if (stagedStep != null)
        {
            DecisionPacket previousStep = stagedStep;
            stagedStep = null;
            WriteStep(previousStep, false);
        }

        pendingSpawnStep.episodeId = currentEpisodeId;
        pendingSpawnStep.stepId = nextStepId;
        pendingSpawnStep.engineJson = engineJson;
        stagedStep = pendingSpawnStep;
        pendingSpawnStep = null;
        nextStepId++;
        currentStepCount = nextStepId;

        if (currentStepCount >= stepsPerGame)
        {
            pendingRestartAfterLock = true;
        }
    }

    public void OnGameOver()
    {
        if (collectionDone)
        {
            return;
        }

        FinalizeEpisode();
    }

    public bool ConsumePendingRestartAfterLock()
    {
        if (!pendingRestartAfterLock)
        {
            return false;
        }

        pendingRestartAfterLock = false;
        FinalizeEpisode();
        if (!collectionDone)
        {
            board?.RestartGame();
        }
        return true;
    }

    private void FinalizeEpisode()
    {
        if (stagedStep == null)
        {
            pendingSpawnStep = null;
            return;
        }

        DecisionPacket terminalStep = stagedStep;
        stagedStep = null;
        WriteStep(terminalStep, true);
        pendingSpawnStep = null;
        completedGames++;
        currentStepCount = 0;
        nextStepId = 0;
        currentEpisodeId++;
        pendingRestartAfterLock = false;

        Debug.Log($"[DataHandler] Game {completedGames}/{targetGames} collected.");

        if (completedGames >= targetGames)
        {
            FlushAndClose();
            collectionDone = true;
            Debug.Log($"[DataHandler] Collection complete! Dir: {datasetsDir}");
            ColdClearAgent.Instance?.SetActive(false);
        }
    }

    private void WriteStep(DecisionPacket packet, bool gameComplete)
    {
        if (writer == null || packet == null)
        {
            return;
        }

        packet.gameComplete = gameComplete;
        writer.WriteLine(packet.ToJsonLine(collectorMeta));
        writer.Flush();
        writtenLinesInFile++;

        if (writtenLinesInFile >= maxLinesPerFile)
        {
            currentFileIndex++;
            OpenNextFile();
        }
    }

    public void SetTargetGames(int count)
    {
        if (count <= 0)
        {
            return;
        }

        targetGames = count;
        Debug.Log($"[DataHandler] targetGames set to {targetGames}");
    }

    public void ClearData()
    {
        pendingSpawnStep = null;
        stagedStep = null;
        pendingRestartAfterLock = false;
        updated = true;
        nextStepId = 0;
        currentStepCount = 0;
    }
}
