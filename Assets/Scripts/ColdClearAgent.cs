using System;
using System.Collections.Generic;
using UnityEngine;

public class ColdClearAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Board board;
    [SerializeField] private ControlCommandManager commandManager;

    [Header("Runtime")]
    public bool enableBot { get; private set; } = false;
    [SerializeField] private uint incomingGarbage;
    [SerializeField] private bool verboseLogging;

    private readonly List<ControlCommand> commandBuffer = new List<ControlCommand>(40);
    private IntPtr bot = IntPtr.Zero;
    private Board subscribedBoard;
    private bool waitingForMove;
    private bool hasLoggedMissingRefs;

    // singleton
    public static ColdClearAgent Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else
        {
            Destroy(gameObject);
        }
    }


    public void SetActive(bool activate)
    {
        if (enableBot == activate)
        {
            return;
        }

        enableBot = activate;
        if (activate)
        {
            board.RestartGame();
            TryLaunchFromBoardState();
        }
        else
        {
            board.RestartGame();
            ShutdownBot();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        AttachBoardEvents();
    }

    private void Start()
    {
        TryLaunchFromBoardState();
    }

    private void Update()
    {
        if (!enableBot)
        {
            return;
        }

        ResolveReferences();
        AttachBoardEvents();

        if (board == null || commandManager == null)
        {
            if (!hasLoggedMissingRefs)
            {
                Debug.LogWarning("[ColdClearAgent] Missing references. Assign Board and ControlCommandManager.");
                hasLoggedMissingRefs = true;
            }

            return;
        }

        hasLoggedMissingRefs = false;

        if (bot == IntPtr.Zero)
        {
            TryLaunchFromBoardState();
            return;
        }

        if (waitingForMove)
        {
            PollNextMove();
        }
    }

    private void OnDisable()
    {
        DetachBoardEvents();
        ShutdownBot();
    }

    private void ResolveReferences()
    {
        if (board == null)
        {
#if UNITY_2023_1_OR_NEWER
            board = FindFirstObjectByType<Board>();
#else
            board = FindObjectOfType<Board>();
#endif
        }

        if (commandManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            commandManager = FindFirstObjectByType<ControlCommandManager>();
#else
            commandManager = FindObjectOfType<ControlCommandManager>();
#endif
        }
    }

    private void AttachBoardEvents()
    {
        if (board == null || subscribedBoard == board)
        {
            return;
        }

        DetachBoardEvents();

        board.PieceSpawned += HandlePieceSpawned;
        board.QueuePieceEnqueued += HandleQueuePieceEnqueued;
        board.GameRestarted += HandleGameRestarted;
        subscribedBoard = board;
    }

    private void DetachBoardEvents()
    {
        if (subscribedBoard == null)
        {
            return;
        }

        subscribedBoard.PieceSpawned -= HandlePieceSpawned;
        subscribedBoard.QueuePieceEnqueued -= HandleQueuePieceEnqueued;
        subscribedBoard.GameRestarted -= HandleGameRestarted;
        subscribedBoard = null;
    }

    private void HandlePieceSpawned(TetrominoData pieceData)
    {
        if (!enableBot)
        {
            return;
        }

        if (bot == IntPtr.Zero)
        {
            TryLaunchFromBoardState();
            return;
        }

        // Hold 처리로 인한 스폰은 같은 턴이므로 새 요청을 보내지 않는다.
        if (board != null && !board.CanHold)
        {
            return;
        }

        RequestNextMove();
    }

    private void HandleQueuePieceEnqueued(TetrominoData queuedData)
    {
        if (!enableBot || bot == IntPtr.Zero)
        {
            return;
        }

        if (!ColdClearAdapter.TryToNativePiece(queuedData, out ColdClearNative.CCPiece piece))
        {
            Debug.LogWarning($"[ColdClearAgent] Unsupported tetromino for queue sync: {queuedData.tetromino}");
            return;
        }

        ColdClearNative.cc_add_next_piece_async(bot, piece);
    }

    private void HandleGameRestarted()
    {
        if (!enableBot)
        {
            return;
        }

        RelaunchBot();
    }

    public void RelaunchBot()
    {
        ShutdownBot();
        TryLaunchFromBoardState();
    }

    private bool TryLaunchFromBoardState()
    {
        if (!enableBot || bot != IntPtr.Zero || board == null || commandManager == null)
        {
            return false;
        }

        if (!board.TryGetActivePieceData(out TetrominoData activeData))
        {
            return false;
        }

        List<ColdClearNative.CCPiece> initialQueue = new List<ColdClearNative.CCPiece>(8);
        if (!TryAddNativePiece(activeData, initialQueue))
        {
            return false;
        }

        List<TetrominoData> queued = board.GetQueueSnapshot();
        for (int i = 0; i < queued.Count; i++)
        {
            if (!TryAddNativePiece(queued[i], initialQueue))
            {
                return false;
            }
        }

        ColdClearNative.cc_default_options(out ColdClearNative.CCOptions options);
        ColdClearNative.cc_default_weights(out ColdClearNative.CCWeights weights);
        EnsureWeightsLayout(ref weights);

        bot = ColdClearNative.cc_launch_async(
            ref options,
            ref weights,
            IntPtr.Zero,
            initialQueue.ToArray(),
            (uint)initialQueue.Count);

        if (bot == IntPtr.Zero)
        {
            Debug.LogError("[ColdClearAgent] Failed to launch Cold Clear bot.");
            return false;
        }

        waitingForMove = false;

        if (verboseLogging)
        {
            Debug.Log($"[ColdClearAgent] Bot launched with queue length {initialQueue.Count}.");
        }

        RequestNextMove();
        return true;
    }

    private bool TryAddNativePiece(TetrominoData data, List<ColdClearNative.CCPiece> list)
    {
        if (!ColdClearAdapter.TryToNativePiece(data, out ColdClearNative.CCPiece piece))
        {
            Debug.LogWarning($"[ColdClearAgent] Unsupported tetromino for launch: {data.tetromino}");
            return false;
        }

        list.Add(piece);
        return true;
    }

    private static void EnsureWeightsLayout(ref ColdClearNative.CCWeights weights)
    {
        if (weights.tslot == null || weights.tslot.Length != 4)
        {
            weights.tslot = new int[4];
        }

        if (weights.well_column == null || weights.well_column.Length != 10)
        {
            weights.well_column = new int[10];
        }
    }

    private void RequestNextMove()
    {
        if (bot == IntPtr.Zero || waitingForMove)
        {
            return;
        }

        ColdClearNative.cc_request_next_move(bot, incomingGarbage);
        waitingForMove = true;
    }

    private void PollNextMove()
    {
        ColdClearNative.CCMove move = ColdClearNative.CCMove.CreateBuffer();
        ColdClearNative.CCBotPollStatus status = ColdClearNative.cc_poll_next_move(
            bot,
            ref move,
            IntPtr.Zero,
            IntPtr.Zero);

        if (status == ColdClearNative.CCBotPollStatus.CC_WAITING)
        {
            return;
        }

        waitingForMove = false;

        if (status == ColdClearNative.CCBotPollStatus.CC_BOT_DEAD)
        {
            Debug.LogWarning("[ColdClearAgent] Bot died. Relaunching from current board state.");
            RelaunchBot();
            return;
        }

        if (!ColdClearAdapter.TryBuildCommandSequence(move, commandBuffer))
        {
            Debug.LogWarning("[ColdClearAgent] Cold Clear returned an empty or unsupported move.");
            return;
        }

        for (int i = 0; i < commandBuffer.Count; i++)
        {
            commandManager.EnqueueCommand(commandBuffer[i]);
        }
    }

    private void ShutdownBot()
    {
        waitingForMove = false;
        commandBuffer.Clear();

        if (bot == IntPtr.Zero)
        {
            return;
        }

        ColdClearNative.cc_destroy_async(bot);
        bot = IntPtr.Zero;
    }
}
