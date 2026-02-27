using System.Collections.Generic;
using UnityEngine;

public static class ColdClearAdapter
{
    public static bool TryToNativePiece(Tetromino tetromino, out ColdClearNative.CCPiece piece)
    {
        switch (tetromino)
        {
            case Tetromino.I:
                piece = ColdClearNative.CCPiece.CC_I;
                return true;
            case Tetromino.O:
                piece = ColdClearNative.CCPiece.CC_O;
                return true;
            case Tetromino.T:
                piece = ColdClearNative.CCPiece.CC_T;
                return true;
            case Tetromino.J:
                piece = ColdClearNative.CCPiece.CC_J;
                return true;
            case Tetromino.L:
                piece = ColdClearNative.CCPiece.CC_L;
                return true;
            case Tetromino.S:
                piece = ColdClearNative.CCPiece.CC_S;
                return true;
            case Tetromino.Z:
                piece = ColdClearNative.CCPiece.CC_Z;
                return true;
            default:
                piece = default;
                return false;
        }
    }

    public static bool TryToNativePiece(TetrominoData data, out ColdClearNative.CCPiece piece)
    {
        return TryToNativePiece(data.tetromino, out piece);
    }

    public static bool TryToControlCommand(ColdClearNative.CCMovement movement, out ControlCommand command)
    {
        switch (movement)
        {
            case ColdClearNative.CCMovement.CC_LEFT:
                command = ControlCommand.MoveLeft;
                return true;
            case ColdClearNative.CCMovement.CC_RIGHT:
                command = ControlCommand.MoveRight;
                return true;
            case ColdClearNative.CCMovement.CC_CW:
                command = ControlCommand.RotateRight;
                return true;
            case ColdClearNative.CCMovement.CC_CCW:
                command = ControlCommand.RotateLeft;
                return true;
            case ColdClearNative.CCMovement.CC_DROP:
                command = ControlCommand.SoftDrop;
                return true;
            default:
                command = ControlCommand.None;
                return false;
        }
    }

    public static bool TryBuildCommandSequence(ColdClearNative.CCMove move, List<ControlCommand> commands)
    {
        if (commands == null)
        {
            return false;
        }

        commands.Clear();

        if (move.hold)
        {
            commands.Add(ControlCommand.Hold);
        }

        int movementCount = move.movements == null ? 0 : Mathf.Min(move.movement_count, move.movements.Length);
        for (int i = 0; i < movementCount; i++)
        {
            if (!TryToControlCommand(move.movements[i], out ControlCommand command))
            {
                return false;
            }

            commands.Add(command);
        }

        // Cold Clear 응답을 실제 락인으로 마무리하도록 마지막에 하드드롭을 붙인다.
        commands.Add(ControlCommand.HardDrop);

        return commands.Count > 0;
    }
}
