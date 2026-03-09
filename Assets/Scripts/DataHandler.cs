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

    private readonly List<DecisionPacket> currentGameSteps = new List<DecisionPacket>(100);
    private DecisionPacket pendingStep;
    private bool updated = true;
    private StreamWriter writer;
    private string datasetsDir;
    private int currentFileIndex;
    private int currentEpisodeId;
    private Board board;
    private bool pendingRestartAfterLock;

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

    public void UpdateBoard()
    {
        updated = false;
    }

    public void SaveData()
    {
        if (collectionDone || updated || DataManager.Instance == null)
        {
            return;
        }

        updated = true;
        pendingStep = new DecisionPacket
        {
            boardVisible = DataManager.Instance.GetBoardVisible(),
            boardHidden = DataManager.Instance.GetBoardHidden(),
            currentPiece = DecisionPacket.PieceToString(DataManager.Instance.GetCurrentDataMino()),
            holdPiece = DecisionPacket.PieceToString(DataManager.Instance.GetHoldDataMino()),
            nextVisible = DecisionPacket.ConvertPieces(DataManager.Instance.GetPreviewDataMino()),
            canHold = DataManager.Instance.GetCanHold(),
            combo = DataManager.Instance.GetCombo(),
            b2bChain = DataManager.Instance.GetB2BChain(),
            candidates = new List<CandidateData>()
        };
    }

    public void AttachDecision(
        List<CandidateData> candidates,
        ColdClearNative.CCDecisionInfo decisionInfo,
        ColdClearNative.CCMove move)
    {
        if (pendingStep == null)
        {
            return;
        }

        pendingStep.nodes = move.nodes;
        pendingStep.depth = move.depth;
        pendingStep.decisionMode = decisionInfo.decision_mode;
        pendingStep.chosenIdx = (int)decisionInfo.chosen_idx;
        pendingStep.candidates = candidates ?? new List<CandidateData>();
        pendingStep.actionHold = move.hold;
        pendingStep.actionX = DecisionPacket.CopyBytes(move.expected_x);
        pendingStep.actionY = DecisionPacket.CopyBytes(move.expected_y);
        pendingStep.actionToken = PlacementToken.FromNative(
            move.hold,
            pendingStep.currentPiece,
            move.expected_x,
            move.expected_y);

        currentGameSteps.Add(pendingStep);
        currentStepCount = currentGameSteps.Count;
        pendingStep = null;

        if (currentGameSteps.Count >= stepsPerGame)
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

        FinishCurrentGame(false);
    }

    public bool ConsumePendingRestartAfterLock()
    {
        if (!pendingRestartAfterLock)
        {
            return false;
        }

        pendingRestartAfterLock = false;
        FinishCurrentGame(true);
        return true;
    }

    private void FinishCurrentGame(bool gameComplete)
    {
        if (currentGameSteps.Count == 0)
        {
            pendingStep = null;
            return;
        }

        currentEpisodeId++;
        WriteGameToFile(currentEpisodeId, currentGameSteps, gameComplete);

        completedGames++;
        currentGameSteps.Clear();
        currentStepCount = 0;
        pendingStep = null;
        pendingRestartAfterLock = false;

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

        if (gameComplete)
        {
            board?.RestartGame();
        }
    }

    private void WriteGameToFile(int gameIndex, List<DecisionPacket> steps, bool gameComplete)
    {
        if (writer == null)
        {
            return;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].episodeId = gameIndex;
            steps[i].stepId = i;
            steps[i].gameComplete = gameComplete;
            writer.WriteLine(steps[i].ToJson());
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
        currentGameSteps.Clear();
        currentStepCount = 0;
        pendingStep = null;
        pendingRestartAfterLock = false;
        updated = true;
    }
}
