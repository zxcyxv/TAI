using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum SpinKind
{
    None,
    TSpin,
    AllSpin
}

[System.Serializable]
public struct ScoreResult
{
    public int linesCleared;
    public Spin spin;
    public bool allClear;
    public int allClearScoreBonus;
    public int allClearAttackBonus;
    public bool backToBackActive;
    public int backToBackChain;
    public int combo;
    public int attack;
    public int scoreGain;
    public int totalScore;
    public int totalAttack;
}

[System.Serializable]
public struct LineClearReward
{
    public int score;
    public int attack;
}

[System.Serializable]
public class LineClearRewardTable
{
    public LineClearReward single;
    public LineClearReward doubleLine;
    public LineClearReward triple;
    public LineClearReward quad;

    public LineClearReward GetReward(int linesCleared)
    {
        switch (linesCleared)
        {
            case 1: return single;
            case 2: return doubleLine;
            case 3: return triple;
            case 4: return quad;
            default: return default;
        }
    }
}

public class Board : MonoBehaviour
{
    public Tilemap tilemap;
    public Piece activePiece;
    public TetrominoData[] tetrominos;
    public Vector3Int spawnPosition;
    public Vector2Int boradSize = new(10, 20);
    public int additionalHeight = 2;

    [Tooltip("미리보기로 유지할 개수")]
    [SerializeField] private int previewSize = 5;

    [Header("Scoring (TETR.IO Style)")]
    [SerializeField] private int score;
    [SerializeField] private int totalAttack;
    [SerializeField] private int combo = -1; // -1 means combo is inactive
    [SerializeField] private int backToBackChain;
    [SerializeField] private int comboScoreStep = 50;

    [Header("Base Line Clear Rewards")]
    [SerializeField] private LineClearRewardTable normalRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 100, attack = 0 },
        doubleLine = new LineClearReward { score = 300, attack = 1 },
        triple = new LineClearReward { score = 500, attack = 2 },
        quad = new LineClearReward { score = 800, attack = 4 }
    };

    [Header("Spin Rewards")]
    [SerializeField] private LineClearRewardTable tSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 800, attack = 2 },
        doubleLine = new LineClearReward { score = 1200, attack = 4 },
        triple = new LineClearReward { score = 1600, attack = 6 },
        quad = new LineClearReward()
    };
    [SerializeField] private LineClearRewardTable iSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 600, attack = 1 },
        doubleLine = new LineClearReward { score = 900, attack = 2 },
        triple = new LineClearReward { score = 1200, attack = 4 },
        quad = new LineClearReward()
    };
    [SerializeField] private LineClearRewardTable jSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 600, attack = 1 },
        doubleLine = new LineClearReward { score = 900, attack = 2 },
        triple = new LineClearReward { score = 1200, attack = 4 },
        quad = new LineClearReward()
    };
    [SerializeField] private LineClearRewardTable lSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 600, attack = 1 },
        doubleLine = new LineClearReward { score = 900, attack = 2 },
        triple = new LineClearReward { score = 1200, attack = 4 },
        quad = new LineClearReward()
    };
    [SerializeField] private LineClearRewardTable sSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 600, attack = 1 },
        doubleLine = new LineClearReward { score = 900, attack = 2 },
        triple = new LineClearReward { score = 1200, attack = 4 },
        quad = new LineClearReward()
    };
    [SerializeField] private LineClearRewardTable zSpinRewards = new LineClearRewardTable
    {
        single = new LineClearReward { score = 600, attack = 1 },
        doubleLine = new LineClearReward { score = 900, attack = 2 },
        triple = new LineClearReward { score = 1200, attack = 4 },
        quad = new LineClearReward()
    };

    [Header("All Clear Bonus")]
    [SerializeField] private LineClearReward allClearBonus = new LineClearReward
    {
        score = 3500,
        attack = 4
    };

    private readonly List<TetrominoData> bag = new List<TetrominoData>();
    private readonly Queue<TetrominoData> queue = new Queue<TetrominoData>();
    private bool hasLoggedMissingTetrominos;
    private bool canHold = true;
    private bool hasHoldPiece;
    public TetrominoData holdPiece { get; private set; }
    private bool isRestarting;

    public bool CanHold => canHold;
    public bool HasHoldPiece => hasHoldPiece;
    public int Score => score;
    public int TotalAttack => totalAttack;
    public int Combo => Mathf.Max(0, combo);
    public int BackToBackChain => backToBackChain;
    public int GetCombo() => Mathf.Max(0, combo);
    public int GetB2BChain() => backToBackChain;
    public ScoreResult LastScoreResult { get; private set; }
    public Spin LastLockSpin { get; private set; } = Spin.None;
    public event Action<TetrominoData> PieceSpawned;
    public event Action<TetrominoData> QueuePieceEnqueued;
    public event Action GameRestarted;

    private Vector3Int SpawnPositionWithAdditionalHeight
    {
        get
        {
            int extraRows = Mathf.Max(0, additionalHeight);
            return spawnPosition + new Vector3Int(0, extraRows, 0);
        }
    }

    public RectInt Bounds
    {
        get
        {
            int extendedHeight = boradSize.y + Mathf.Max(0, additionalHeight);
            Vector2Int size = new(boradSize.x, extendedHeight);
            Vector2Int position = new(-size.x/2, -boradSize.y/2);
            return new RectInt(position, size);
        }
    }

    private void Awake()
    {
        tilemap = GetComponentInChildren<Tilemap>();
        activePiece = GetComponent<Piece>();

        foreach(var tetromino in tetrominos)
        {
            tetromino.Initialize();
        } 
    }

    private void Start()
    {
        RestartGame();
    }

    public void RestartGame()
    {
        ResetRuntimeState();
        FillQueueIfNeeded();
        DataHandler.Instance?.UpdateBoard();
        SpawnPiece();
        GameRestarted?.Invoke();
    }

    public void SpawnPiece()
    {
        if (tetrominos == null || tetrominos.Length == 0)
        {
            Debug.LogError("[Board] Cannot spawn piece because tetrominos is empty.");
            return;
        }

        TetrominoData data = GetNextPiece();
        if (data.tile == null)
        {
            Debug.LogError("[Board] Next piece data is invalid (tile is null).");
            return;
        }

        Debug.Log("[Board] Loaded Piece: " + data.tetromino.ToString());
        SpawnPiece(data);
    }

    public TetrominoData GetNextPiece()
    {
        FillQueueIfNeeded();
        if (queue.Count == 0)
        {
            Debug.LogError("[Board] Next queue is empty. Cannot provide next piece.");
            return default;
        }

        TetrominoData next = queue.Dequeue();

        // 다음 미리보기를 항상 유지하도록 리필
        FillQueueIfNeeded();

        return next;
    }

    public List<TetrominoData> GetPreviewPieces()
    {
        FillQueueIfNeeded();

        List<TetrominoData> preview = new List<TetrominoData>(previewSize);
        int count = 0;
        foreach (var item in queue)
        {
            preview.Add(item);
            count++;
            if (count >= previewSize) break;
        }

        return preview;
    }

    public List<TetrominoData> GetQueueSnapshot()
    {
        FillQueueIfNeeded();
        return new List<TetrominoData>(queue);
    }

    public bool TryGetActivePieceData(out TetrominoData data)
    {
        if (activePiece == null || activePiece.data.tile == null)
        {
            data = default;
            return false;
        }

        data = activePiece.data;
        return true;
    }

    public bool TryGetHoldPiece(out TetrominoData data)
    {
        data = holdPiece;
        return hasHoldPiece;
    }

    public bool TryHoldPiece()
    {
        if (!canHold || activePiece == null)
        {
            return false;
        }

        TetrominoData current = activePiece.data;
        if (current.tile == null)
        {
            return false;
        }

        Clear(activePiece);

        if (!hasHoldPiece)
        {
            holdPiece = current;
            hasHoldPiece = true;
            canHold = false;
            SpawnPiece();
            return true;
        }

        TetrominoData swap = holdPiece;
        holdPiece = current;
        canHold = false;
        SpawnPiece(swap);
        return true;
    }

    public void NotifyPieceLocked()
    {
        canHold = true;
        DataHandler.Instance?.UpdateBoard();
    }

    private void SpawnPiece(TetrominoData data)
    {
        if (data.cells == null)
        {
            data.Initialize();
        }

        Vector3Int runtimeSpawnPosition = SpawnPositionWithAdditionalHeight;
        activePiece.Initialize(this, runtimeSpawnPosition, data);
        if (IsVaildPosition(activePiece, runtimeSpawnPosition))
        {
            DataHandler.Instance?.SaveData();
            Set(activePiece);
            PieceSpawned?.Invoke(data);
        }
        else
        {
            GameOver();
        }
    }

    private void FillQueueIfNeeded()
    {
        if (tetrominos == null || tetrominos.Length == 0)
        {
            if (!hasLoggedMissingTetrominos)
            {
                Debug.LogWarning("[Board] Tetrominos is empty. Assign tetromino data in inspector.");
                hasLoggedMissingTetrominos = true;
            }
            return;
        }

        while (queue.Count < previewSize + 1) // +1 은 다음 활성 블록까지 확보
        {
            if (bag.Count == 0)
            {
                RefillBag();
                if (bag.Count == 0)
                {
                    return;
                }
            }

            TetrominoData draw = bag[0];
            bag.RemoveAt(0);
            queue.Enqueue(draw);
            QueuePieceEnqueued?.Invoke(draw);
        }
    }

    private void RefillBag()
    {
        List<TetrominoData> tempBag = new List<TetrominoData>(tetrominos);

        while (tempBag.Count > 0)
        {
            int random = UnityEngine.Random.Range(0, tempBag.Count);
            bag.Add(tempBag[random]);
            tempBag.RemoveAt(random);
        }
    }

    public void GameOver()
    {
        Debug.Log("[Board] Game Over");

        if (isRestarting)
        {
            return;
        }

        isRestarting = true;
        DataHandler.Instance?.OnGameOver();
        RestartGame();
        isRestarting = false;
    }

    private void ResetRuntimeState()
    {
        tilemap.ClearAllTiles();
        bag.Clear();
        queue.Clear();

        canHold = true;
        hasHoldPiece = false;
        holdPiece = default;
        hasLoggedMissingTetrominos = false;

        score = 0;
        totalAttack = 0;
        combo = -1;
        backToBackChain = 0;
        LastScoreResult = default;
        LastLockSpin = Spin.None;
    }

    public void RecordLastLockSpin(Spin spin)
    {
        LastLockSpin = spin;
    }

    public void Set(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            tilemap.SetTile(tilePosition, piece.data.tile);
        }
    }

    public void Clear(Piece piece)
    {
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + piece.position;
            tilemap.SetTile(tilePosition, null);
        }
    }

    public bool IsVaildPosition(Piece piece, Vector3Int position)
    {
        RectInt bounds = Bounds;
        for (int i = 0; i < piece.cells.Length; i++)
        {
            Vector3Int tilePosition = piece.cells[i] + position;
            if (!bounds.Contains((Vector2Int)tilePosition)) return false;
            if (tilemap.HasTile(tilePosition)) return false;
        }
        return true;
    }

    public int ClearLines()
    {
        RectInt bounds = Bounds;
        int row = bounds.yMin;
        int clearedLines = 0;

        while (row < bounds.yMax)
        {
            if (IsLineFull(row))
            {
                LineClear(row);
                clearedLines++;
                // 클리어 한 행 재확인
            } 
            else
            {
                row++;
            }
        }

        return clearedLines;
    }

    public ScoreResult ResolveScore(int linesCleared, Spin spin)
    {
        bool hasLineClear = linesCleared > 0;
        bool allClear = hasLineClear && IsAllClear();
        bool difficultClear = IsDifficultClear(linesCleared, spin);

        if (hasLineClear)
        {
            combo++;
        }
        else
        {
            combo = -1;
        }

        bool backToBackActive = false;
        if (difficultClear)
        {
            backToBackChain++;
            backToBackActive = backToBackChain > 1;
        }
        else if (hasLineClear)
        {
            backToBackChain = 0;
        }

        LineClearReward reward = GetBaseReward(linesCleared, spin);
        int attack = reward.attack;
        int scoreGain = reward.score;

        if (allClear)
        {
            attack += allClearBonus.attack;
            scoreGain += allClearBonus.score;
        }

        if (hasLineClear && combo > 0)
        {
            attack += GetComboAttack(combo);
            scoreGain += combo * comboScoreStep;
        }

        if (backToBackActive)
        {
            attack += GetBackToBackAttackBonus(backToBackChain);
            scoreGain = Mathf.RoundToInt(scoreGain * 1.5f);
        }

        attack = Mathf.Max(0, attack);
        scoreGain = Mathf.Max(0, scoreGain);

        score += scoreGain;
        totalAttack += attack;

        ScoreResult result = new ScoreResult
        {
            linesCleared = linesCleared,
            spin = spin,
            allClear = allClear,
            allClearScoreBonus = allClear ? allClearBonus.score : 0,
            allClearAttackBonus = allClear ? allClearBonus.attack : 0,
            backToBackActive = backToBackActive,
            backToBackChain = backToBackChain,
            combo = Mathf.Max(0, combo),
            attack = attack,
            scoreGain = scoreGain,
            totalScore = score,
            totalAttack = totalAttack
        };

        LastScoreResult = result;
        return result;
    }

    private bool IsDifficultClear(int linesCleared, Spin spin)
    {
        if (linesCleared <= 0)
        {
            return false;
        }

        if (linesCleared == 4)
        {
            return true;
        }

        return spin != Spin.None;
    }

    private LineClearReward GetBaseReward(int linesCleared, Spin spin)
    {
        // Requested behavior: no score/attack for spin-only locks without a line clear.
        if (linesCleared <= 0)
        {
            return default;
        }

        return spin switch
        {
            Spin.TSpin => tSpinRewards.GetReward(linesCleared),
            Spin.ISpin => iSpinRewards.GetReward(linesCleared),
            Spin.JSpin => jSpinRewards.GetReward(linesCleared),
            Spin.LSpin => lSpinRewards.GetReward(linesCleared),
            Spin.SSpin => sSpinRewards.GetReward(linesCleared),
            Spin.ZSpin => zSpinRewards.GetReward(linesCleared),
            _ => normalRewards.GetReward(linesCleared),
        };
    }

    private int GetComboAttack(int comboCount)
    {
        // comboCount is 1 for the second consecutive clear.
        return Mathf.Min(4, (comboCount + 1) / 2);
    }

    private int GetBackToBackAttackBonus(int chainCount)
    {
        if (chainCount <= 1)
        {
            return 0;
        }

        float scaled = Mathf.Log(1f + (chainCount - 1) * 0.8f);
        return 1 + Mathf.FloorToInt(scaled);
    }

    private bool IsAllClear()
    {
        RectInt bounds = Bounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                if (tilemap.HasTile(new Vector3Int(x, y, 0)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsLineFull(int row)
    {
        RectInt bounds = Bounds;

        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new(col, row, 0);
            if (!tilemap.HasTile(position)) return false;
        }

        return true;
    }

    private void LineClear(int row)
    {
        RectInt bounds = Bounds;

        for (int col = bounds.xMin; col < bounds.xMax; col++)
        {
            Vector3Int position = new(col, row, 0);
            tilemap.SetTile(position, null);
        }

        while (row < bounds.yMax)
        {
            for (int col = bounds.xMin; col < bounds.xMax; col++)
            {
                Vector3Int position = new(col, row + 1, 0);

                TileBase above = tilemap.GetTile(position);
                position = new(col, row, 0);

                tilemap.SetTile(position, above);
            }
            row++;
        }
    }
}
