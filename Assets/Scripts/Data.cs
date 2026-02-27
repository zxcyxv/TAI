using System.Collections.Generic;
using UnityEngine;

public static class Data
{
    public static readonly float cos = Mathf.Cos(Mathf.PI / 2f);
    public static readonly float sin = Mathf.Sin(Mathf.PI / 2f);
    public static readonly float[] RotationMatrix = new float[] { cos, sin, -sin, cos };

    public static readonly Dictionary<Tetromino, Vector2Int[]> Cells = new Dictionary<Tetromino, Vector2Int[]>()
    {
        { Tetromino.I, new Vector2Int[] { new Vector2Int(-1, 1), new Vector2Int( 0, 1), new Vector2Int( 1, 1), new Vector2Int( 2, 1) } },
        { Tetromino.J, new Vector2Int[] { new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int( 0, 0), new Vector2Int( 1, 0) } },
        { Tetromino.L, new Vector2Int[] { new Vector2Int( 1, 1), new Vector2Int(-1, 0), new Vector2Int( 0, 0), new Vector2Int( 1, 0) } },
        { Tetromino.O, new Vector2Int[] { new Vector2Int( 0, 1), new Vector2Int( 1, 1), new Vector2Int( 0, 0), new Vector2Int( 1, 0) } },
        { Tetromino.S, new Vector2Int[] { new Vector2Int( 0, 1), new Vector2Int( 1, 1), new Vector2Int(-1, 0), new Vector2Int( 0, 0) } },
        { Tetromino.T, new Vector2Int[] { new Vector2Int( 0, 1), new Vector2Int(-1, 0), new Vector2Int( 0, 0), new Vector2Int( 1, 0) } },
        { Tetromino.Z, new Vector2Int[] { new Vector2Int(-1, 1), new Vector2Int( 0, 1), new Vector2Int( 0, 0), new Vector2Int( 1, 0) } },
    };

    private static readonly Vector2Int[,] WallKicksI = new Vector2Int[,] {
        { new Vector2Int(0, 0), new Vector2Int(-2, 0), new Vector2Int( 1, 0), new Vector2Int(-2,-1), new Vector2Int( 1, 2) },
        { new Vector2Int(0, 0), new Vector2Int( 2, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 1), new Vector2Int(-1,-2) },
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-1, 2), new Vector2Int( 2,-1) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int(-2, 0), new Vector2Int( 1,-2), new Vector2Int(-2, 1) },
        { new Vector2Int(0, 0), new Vector2Int( 2, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 1), new Vector2Int(-1,-2) },
        { new Vector2Int(0, 0), new Vector2Int(-2, 0), new Vector2Int( 1, 0), new Vector2Int(-2,-1), new Vector2Int( 1, 2) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int(-2, 0), new Vector2Int( 1,-2), new Vector2Int(-2, 1) },
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-1, 2), new Vector2Int( 2,-1) },
    };

    private static readonly Vector2Int[,] WallKicksO = new Vector2Int[,] {
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
        { new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0) },
    };

    private static readonly Vector2Int[,] WallKicksJLOSTZ = new Vector2Int[,] {
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0,-2), new Vector2Int(-1,-2) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int(0, 2), new Vector2Int( 1, 2) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int(0, 2), new Vector2Int( 1, 2) },
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0,-2), new Vector2Int(-1,-2) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int( 1, 1), new Vector2Int(0,-2), new Vector2Int( 1,-2) },
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1,-1), new Vector2Int(0, 2), new Vector2Int(-1, 2) },
        { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(-1,-1), new Vector2Int(0, 2), new Vector2Int(-1, 2) },
        { new Vector2Int(0, 0), new Vector2Int( 1, 0), new Vector2Int( 1, 1), new Vector2Int(0,-2), new Vector2Int( 1,-2) },
    };

    // 180도 회전용 킥 테이블 (가이드라인에서 널리 쓰이는 보수적인 순서)
    private static readonly Vector2Int[,] WallKicks180JLOSTZ = new Vector2Int[,] {
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 1, 1), new Vector2Int(-1, 1), new Vector2Int( 1,-1), new Vector2Int(-1,-1), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 1, 1), new Vector2Int(-1, 1), new Vector2Int( 1,-1), new Vector2Int(-1,-1), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 1, 1), new Vector2Int(-1, 1), new Vector2Int( 1,-1), new Vector2Int(-1,-1), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 1, 1), new Vector2Int(-1, 1), new Vector2Int( 1,-1), new Vector2Int(-1,-1), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
    };

    private static readonly Vector2Int[,] WallKicks180I = new Vector2Int[,] {
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
        { new Vector2Int(0, 0), new Vector2Int( 0, 1), new Vector2Int( 0,-1), new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int( 2, 0), new Vector2Int(-2, 0) },
    };

    private static readonly Vector2Int[,] WallKicks180O = new Vector2Int[,] {
        { new Vector2Int(0, 0) },
        { new Vector2Int(0, 0) },
        { new Vector2Int(0, 0) },
        { new Vector2Int(0, 0) },
    };

    public static readonly Dictionary<Tetromino, Vector2Int[,]> WallKicks = new Dictionary<Tetromino, Vector2Int[,]>()
    {
        { Tetromino.I, WallKicksI },
        { Tetromino.J, WallKicksJLOSTZ },
        { Tetromino.L, WallKicksJLOSTZ },
        { Tetromino.O, WallKicksO },
        { Tetromino.S, WallKicksJLOSTZ },
        { Tetromino.T, WallKicksJLOSTZ },
        { Tetromino.Z, WallKicksJLOSTZ },
    };

    public static readonly Dictionary<Tetromino, Vector2Int[,]> WallKicks180 = new Dictionary<Tetromino, Vector2Int[,]>()
    {
        { Tetromino.I, WallKicks180I },
        { Tetromino.J, WallKicks180JLOSTZ },
        { Tetromino.L, WallKicks180JLOSTZ },
        { Tetromino.O, WallKicks180O },
        { Tetromino.S, WallKicks180JLOSTZ },
        { Tetromino.T, WallKicks180JLOSTZ },
        { Tetromino.Z, WallKicks180JLOSTZ },
    };

}
