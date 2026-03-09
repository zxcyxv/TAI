using System.Collections.Generic;
using UnityEngine;

public static class PlacementToken
{
    private static readonly Dictionary<Tetromino, Vector2Int[][]> RotationShapes = BuildRotationShapes();

    public static string FromNative(bool hold, string pieceName, byte[] cellsX, byte[] cellsY)
    {
        Tetromino piece = ParsePiece(pieceName);
        return FromCells(hold, piece, cellsX, cellsY);
    }

    public static string FromNative(bool hold, ColdClearNative.CCPiece piece, byte[] cellsX, byte[] cellsY)
    {
        return FromCells(hold, ToTetromino(piece), cellsX, cellsY);
    }

    private static string FromCells(bool hold, Tetromino piece, byte[] cellsX, byte[] cellsY)
    {
        if (piece == Tetromino.None || cellsX == null || cellsY == null || cellsX.Length < 4 || cellsY.Length < 4)
        {
            return string.Empty;
        }

        Vector2Int[] normalized = Normalize(cellsX, cellsY, out int minX);
        int rotation = InferRotation(piece, normalized);
        string rotationToken = rotation switch
        {
            0 => "0",
            1 => "R",
            2 => "2",
            3 => "L",
            _ => "0"
        };

        return $"{(hold ? "H" : string.Empty)}{piece}{rotationToken}_{minX}";
    }

    private static Vector2Int[] Normalize(byte[] cellsX, byte[] cellsY, out int minX)
    {
        minX = int.MaxValue;
        int minY = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            minX = Mathf.Min(minX, cellsX[i]);
            minY = Mathf.Min(minY, cellsY[i]);
        }

        Vector2Int[] normalized = new Vector2Int[4];
        for (int i = 0; i < 4; i++)
        {
            normalized[i] = new Vector2Int(cellsX[i] - minX, cellsY[i] - minY);
        }

        System.Array.Sort(normalized, CompareVec);
        return normalized;
    }

    private static int InferRotation(Tetromino piece, Vector2Int[] normalized)
    {
        if (!RotationShapes.TryGetValue(piece, out Vector2Int[][] rotations))
        {
            return 0;
        }

        for (int i = 0; i < rotations.Length; i++)
        {
            if (Matches(rotations[i], normalized))
            {
                return i;
            }
        }

        return 0;
    }

    private static bool Matches(Vector2Int[] lhs, Vector2Int[] rhs)
    {
        if (lhs.Length != rhs.Length)
        {
            return false;
        }

        for (int i = 0; i < lhs.Length; i++)
        {
            if (lhs[i] != rhs[i])
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<Tetromino, Vector2Int[][]> BuildRotationShapes()
    {
        Dictionary<Tetromino, Vector2Int[][]> shapes = new Dictionary<Tetromino, Vector2Int[][]>();
        Tetromino[] pieces =
        {
            Tetromino.I, Tetromino.O, Tetromino.T, Tetromino.L, Tetromino.J, Tetromino.S, Tetromino.Z
        };

        foreach (Tetromino piece in pieces)
        {
            Vector2Int[] baseCells = Data.Cells[piece];
            Vector2Int[][] rotations = new Vector2Int[4][];
            rotations[0] = Normalize(baseCells);
            rotations[1] = Normalize(Rotate(baseCells, piece, 1));
            rotations[2] = Normalize(Rotate(baseCells, piece, 2));
            rotations[3] = Normalize(Rotate(baseCells, piece, 3));
            shapes[piece] = rotations;
        }

        return shapes;
    }

    private static Vector2Int[] Rotate(Vector2Int[] cells, Tetromino piece, int times)
    {
        Vector2Int[] rotated = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int cell = cells[i];
            for (int j = 0; j < times; j++)
            {
                if (piece == Tetromino.I || piece == Tetromino.O)
                {
                    Vector2 shifted = new Vector2(cell.x - 0.5f, cell.y - 0.5f);
                    cell = new Vector2Int(Mathf.CeilToInt(shifted.y), Mathf.CeilToInt(-shifted.x));
                }
                else
                {
                    cell = new Vector2Int(cell.y, -cell.x);
                }
            }
            rotated[i] = cell;
        }
        return rotated;
    }

    private static Vector2Int[] Normalize(Vector2Int[] cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            minX = Mathf.Min(minX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
        }

        Vector2Int[] normalized = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            normalized[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
        }

        System.Array.Sort(normalized, CompareVec);
        return normalized;
    }

    private static int CompareVec(Vector2Int a, Vector2Int b)
    {
        int xCompare = a.x.CompareTo(b.x);
        return xCompare != 0 ? xCompare : a.y.CompareTo(b.y);
    }

    private static Tetromino ParsePiece(string pieceName)
    {
        if (string.IsNullOrEmpty(pieceName) || pieceName == "None")
        {
            return Tetromino.None;
        }

        return System.Enum.TryParse(pieceName, out Tetromino piece) ? piece : Tetromino.None;
    }

    private static Tetromino ToTetromino(ColdClearNative.CCPiece piece)
    {
        return piece switch
        {
            ColdClearNative.CCPiece.CC_I => Tetromino.I,
            ColdClearNative.CCPiece.CC_O => Tetromino.O,
            ColdClearNative.CCPiece.CC_T => Tetromino.T,
            ColdClearNative.CCPiece.CC_L => Tetromino.L,
            ColdClearNative.CCPiece.CC_J => Tetromino.J,
            ColdClearNative.CCPiece.CC_S => Tetromino.S,
            ColdClearNative.CCPiece.CC_Z => Tetromino.Z,
            _ => Tetromino.None
        };
    }
}
