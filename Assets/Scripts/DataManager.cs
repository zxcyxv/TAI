using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DataManager : MonoBehaviour
{
    private static DataManager instance;

    public static DataManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<DataManager>();

            return instance;
        }
    }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private ControlCommandManager ccm;
    [SerializeField] private Board board;
    [SerializeField] private DataHandler dataHandler;
    [SerializeField] private List<ControlCommand> commands = new List<ControlCommand>();

    private int[,] boardData;
    
    private List<ControlCommand> sampleCommands = new List<ControlCommand>()
    {
        ControlCommand.MoveLeft,
        ControlCommand.RotateRight,
        ControlCommand.SoftDrop,
        ControlCommand.MoveLeft,
        ControlCommand.MoveLeft,
        ControlCommand.MoveLeft,
        ControlCommand.HardDrop
    };

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (ccm == null) return;
        if (commands == null || commands.Count == 0) return;

        foreach (var command in commands)
        {
            ccm.EnqueueCommand(command);
        }

        commands.Clear();
    }

    public void SampleCommand()
    {
        if (commands == null)
        {
            commands = new List<ControlCommand>();
        }

        commands.AddRange(sampleCommands);
    }

    public int[,] GetBoardData()
    {

        int width = 10;
        int height = 20 + Mathf.Max(0, board != null ? board.additionalHeight : 0);

        boardData = new int[width, height];

        if (board == null || board.tilemap == null)
        {
            return boardData;
        }

        RectInt bounds = board.Bounds;
        Tilemap boardTilemap = board.tilemap;

        int maxWidth = Mathf.Min(width, bounds.width);
        int maxHeight = Mathf.Min(height, bounds.height);

        for (int x = 0; x < maxWidth; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                int worldX = bounds.xMin + x;
                int worldY = bounds.yMin + y;
                Vector3Int tilePosition = new Vector3Int(worldX, worldY, 0);

                boardData[x, y] = boardTilemap.HasTile(tilePosition) ? 1 : 0;
            }
        }
        
        return boardData;
    }

    public string GetHoldData()
    {
        
        Debug.Log("Hold Data: " + board.holdPiece.tetromino.ToString());
        return board.holdPiece.tetromino.ToString();
    }

    public Tetromino GetHoldDataMino()
    {
        if (board == null || !board.TryGetHoldPiece(out TetrominoData holdData))
        {
            return Tetromino.None;
        }

        return holdData.tetromino;
    }

    public Tetromino GetCurrentDataMino()
    {
        if (board == null || !board.TryGetActivePieceData(out TetrominoData activeData))
        {
            return Tetromino.None;
        }

        return activeData.tetromino;
    }

    public bool GetCanHold()
    {
        return board.CanHold;

    }

    public string[] GetPreviewData()
    {
        List<TetrominoData> preview = board.GetPreviewPieces();
        string[] previewStrings = new string[preview.Count];

        for (int i = 0; i < preview.Count; i++)
        {
            TetrominoData data = preview[i];
            previewStrings[i] = data.tetromino.ToString();
        }


        string row = "";
        for (int i = 0; i < previewStrings.Length; i++)
        {
            row += previewStrings[i] + " ";
        }
        Debug.Log("Preview Data:" + row); 

        return previewStrings; 
    }

    public List<Tetromino> GetPreviewDataMino()
    {
        List<Tetromino> minos = new();
        if (board == null)
        {
            return minos;
        }

        foreach (var data in board.GetPreviewPieces())
        {
            minos.Add(data.tetromino);
        }
        return minos;
    }
}
