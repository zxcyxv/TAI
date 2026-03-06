using System.Collections.Generic;
using System.Text;

public class BoardData
{
    bool canHold;
    Tetromino holdedPiece;
    List<Tetromino> previewPiece;
    Tetromino currentPiece;
    int[,] boardData;
    string cotReason;
    bool actionHold;
    byte[] actionX;
    byte[] actionY;

    public BoardData(bool canHold, Tetromino holdedPiece, List<Tetromino> previewPiece, Tetromino currentPiece, int[,] boardData)
    {
        this.canHold = canHold;
        this.holdedPiece = holdedPiece;

        this.previewPiece = previewPiece != null ? new List<Tetromino>(previewPiece) : new List<Tetromino>();

        this.currentPiece = currentPiece;
        this.boardData = boardData;
        this.cotReason = "";
    }

    public void SetCoT(string reason)
    {
        cotReason = reason ?? "";
    }

    public void SetAction(bool hold, byte[] x, byte[] y)
    {
        actionHold = hold;
        actionX = x;
        actionY = y;
    }

    private string ToJsonCanHold()
    {
        return "\"canHold\":" + (canHold ? "true" : "false");
    }

    private string ToJsonHoldedPiece()
    {
        return "\"holdedPiece\":\"" + (holdedPiece == default ? "None" : holdedPiece.ToString()) + "\"";
    }

    private string ToJsonPreviewPiece()
    {
        StringBuilder sb = new StringBuilder(96);
        sb.Append("\"previewPiece\":[");
        for (int i = 0; i < previewPiece.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append('"').Append(previewPiece[i]).Append('"');
        }

        sb.Append(']');
        return sb.ToString();
    }

    private string ToJsonCurrentPiece()
    {
        return "\"currentPiece\":\"" + currentPiece + "\"";
    }

    private string ToJsonBoard()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append("\"board\":{");

        int rowCount = boardData != null ? boardData.GetLength(1) : 0;
        int colCount = boardData != null ? boardData.GetLength(0) : 0;

        for (int y = rowCount - 1; y >= 0; y--)
        {
            if (y != rowCount - 1)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(y + 1).Append("\":[");
            for (int x = 0; x < colCount; x++)
            {
                if (x > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(boardData[x, y]);
            }

            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private string ToJsonCoT()
    {
        string escaped = (cotReason ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "\"cotReason\":\"" + escaped + "\"";
    }

    private string ToJsonAction()
    {
        StringBuilder sb = new StringBuilder(128);
        sb.Append("\"action\":{");
        sb.Append("\"hold\":").Append(actionHold ? "true" : "false").Append(',');
        sb.Append("\"x\":[");
        if (actionX != null)
            for (int i = 0; i < actionX.Length; i++) { if (i > 0) sb.Append(','); sb.Append(actionX[i]); }
        sb.Append("],\"y\":[");
        if (actionY != null)
            for (int i = 0; i < actionY.Length; i++) { if (i > 0) sb.Append(','); sb.Append(actionY[i]); }
        sb.Append("]}");
        return sb.ToString();
    }

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append(ToJsonCanHold()); sb.Append(',');
        sb.Append(ToJsonHoldedPiece()); sb.Append(',');
        sb.Append(ToJsonPreviewPiece()); sb.Append(',');
        sb.Append(ToJsonCurrentPiece()); sb.Append(',');
        sb.Append(ToJsonBoard()); sb.Append(',');
        sb.Append(ToJsonAction()); sb.Append(',');
        sb.Append(ToJsonCoT());
        sb.Append('}');
        return sb.ToString();
    }

    public override string ToString()
    {
        return ToJson();
    }
}
