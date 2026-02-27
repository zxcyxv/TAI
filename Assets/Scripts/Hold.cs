using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Hold : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Board board;
    [SerializeField] private Tilemap tilemap;

    [Header("Layout (Top-Left Based)")]
    [Tooltip("홀드 칸의 왼쪽 위 좌표")]
    [SerializeField] private Vector2Int topLeft = new(0, 0);
    [Tooltip("홀드 칸의 가로/세로 셀 크기")]
    [SerializeField] private Vector2Int slotSize = new(4, 2);
    [Tooltip("홀드 위치 미세 조정")]
    [SerializeField] private Vector2Int drawOffset = new(0, 0);

    private readonly Dictionary<Vector3Int, TileBase> replacedTiles = new Dictionary<Vector3Int, TileBase>();
    private bool hasLoggedMissingRefs;
    private bool cachedHasHold;
    private bool hasCachedTetromino;
    private Tetromino cachedTetromino;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        Redraw(true);
    }

    private void LateUpdate()
    {
        if (board.CanHold) tilemap.color = new Color(255, 255, 255, 255);
        else tilemap.color = new Color(.4f, .4f, .4f, 255);

        Redraw(false);
    }

    private void OnDisable()
    {
        if (tilemap != null)
        {
            RestorePreviousTiles();
        }
    }

    private void Redraw(bool force)
    {
        ResolveReferences();

        if (board == null || tilemap == null)
        {
            if (!hasLoggedMissingRefs)
            {
                Debug.LogWarning("[Hold] Missing references. Assign Board and Hold Tilemap in inspector.");
                hasLoggedMissingRefs = true;
            }
            return;
        }

        hasLoggedMissingRefs = false;

        if (!board.TryGetHoldPiece(out TetrominoData holdData))
        {
            if (force || cachedHasHold)
            {
                RestorePreviousTiles();
                cachedHasHold = false;
                hasCachedTetromino = false;
            }
            return;
        }

        if (!force && cachedHasHold && hasCachedTetromino && cachedTetromino == holdData.tetromino)
        {
            return;
        }

        RestorePreviousTiles();
        DrawPiece(holdData);

        cachedHasHold = true;
        hasCachedTetromino = true;
        cachedTetromino = holdData.tetromino;
    }

    private void DrawPiece(TetrominoData data)
    {
        if (data.cells == null)
        {
            data.Initialize();
        }

        Vector2Int[] cells = data.cells;
        if (cells == null || cells.Length == 0 || data.tile == null)
        {
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int cell = cells[i];
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        int pieceWidth = maxX - minX + 1;
        int pieceHeight = maxY - minY + 1;

        int centerX = Mathf.Max(0, (slotSize.x - pieceWidth) / 2);
        int centerY = Mathf.Max(0, (slotSize.y - pieceHeight) / 2);

        int slotTopX = topLeft.x + drawOffset.x;
        int slotTopY = topLeft.y + drawOffset.y;

        for (int i = 0; i < cells.Length; i++)
        {
            int normalizedX = cells[i].x - minX;
            int normalizedYFromBottom = cells[i].y - minY;
            int normalizedYFromTop = (pieceHeight - 1) - normalizedYFromBottom;

            int x = slotTopX + centerX + normalizedX;
            int y = slotTopY - centerY - normalizedYFromTop;
            Vector3Int position = new Vector3Int(x, y, 0);

            if (!replacedTiles.ContainsKey(position))
            {
                replacedTiles[position] = tilemap.GetTile(position);
            }

            tilemap.SetTile(position, data.tile);
        }
    }

    private void ResolveReferences()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemap == null)
        {
            tilemap = GetComponentInChildren<Tilemap>();
        }

        if (board == null)
        {
#if UNITY_2023_1_OR_NEWER
            board = FindFirstObjectByType<Board>();
#else
            board = FindObjectOfType<Board>();
#endif
        }
    }

    private void RestorePreviousTiles()
    {
        foreach (var pair in replacedTiles)
        {
            tilemap.SetTile(pair.Key, pair.Value);
        }

        replacedTiles.Clear();
    }
}
