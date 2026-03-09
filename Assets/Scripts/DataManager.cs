using System.Collections.Generic;
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
        int hiddenHeight = board != null ? Mathf.Max(0, board.additionalHeight) : 0;
        return CaptureBoardRows(20 + hiddenHeight, 0);
    }

    public int[,] GetBoardVisible()
    {
        return CaptureBoardRows(20, 0);
    }

    public int[,] GetBoardHidden()
    {
        int hiddenHeight = board != null ? Mathf.Max(0, board.additionalHeight) : 0;
        return CaptureBoardRows(hiddenHeight, 20);
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

    public int GetCombo()
    {
        return board != null ? board.GetCombo() : 0;
    }

    public int GetB2BChain()
    {
        return board != null ? board.GetB2BChain() : 0;
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

    private int[,] CaptureBoardRows(int rows, int rowOffset)
    {
        int[,] data = new int[Mathf.Max(0, rows), 10];
        if (rows <= 0 || board == null || board.tilemap == null)
        {
            return data;
        }

        RectInt bounds = board.Bounds;
        Tilemap boardTilemap = board.tilemap;
        int maxRows = Mathf.Min(rows, Mathf.Max(0, bounds.height - rowOffset));
        int maxCols = Mathf.Min(10, bounds.width);

        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < maxCols; col++)
            {
                int worldX = bounds.xMin + col;
                int worldY = bounds.yMin + rowOffset + row;
                data[row, col] = boardTilemap.HasTile(new Vector3Int(worldX, worldY, 0)) ? 1 : 0;
            }
        }

        return data;
    }
}
