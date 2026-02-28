using System.Collections.Generic;
using UnityEngine;

public enum ControlCommand
{
    MoveLeft,
    MoveRight,
    SoftDrop,
    HardDrop,
    Hold,
    RotateLeft,
    RotateRight,
    Rotate180,
    None
}

public class ControlCommandManager : MonoBehaviour
{
    public Piece piece;
    private Queue<ControlCommand> commandQueue = new Queue<ControlCommand>();
    public float hardDropQueueDelay = 0.25f;

    private bool hardDropDelayArmed;
    private float hardDropReadyTime;

    public void EnqueueCommand(ControlCommand command)
    {
        commandQueue.Enqueue(command);
    }

    private void Update()
    {
        if (commandQueue.Count != 0)
        {
            ControlCommand nextCommand = commandQueue.Peek();

            if (nextCommand == ControlCommand.HardDrop)
            {
                if (!hardDropDelayArmed)
                {
                    hardDropDelayArmed = true;
                    hardDropReadyTime = Time.time + Mathf.Max(0f, hardDropQueueDelay);
                }

                if (Time.time < hardDropReadyTime)
                {
                    piece.UpdateAfter();
                    return;
                }
            }
            else
            {
                hardDropDelayArmed = false;
            }

            if (piece.EnqueueCommand(nextCommand))
            {
                commandQueue.Dequeue();
                hardDropDelayArmed = false;
            }
        }
        else
        {
            hardDropDelayArmed = false;
        }

        piece.UpdateAfter();
    } 
}
