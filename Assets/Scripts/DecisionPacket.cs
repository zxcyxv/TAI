using System.Collections.Generic;
using System.Text;

public class DecisionPacket
{
    public int episodeId;
    public int stepId;
    public bool gameComplete;
    public int[,] boardVisible;
    public int[,] boardHidden;
    public string currentPiece;
    public string holdPiece;
    public List<string> nextVisible;
    public bool canHold;
    public int combo;
    public int b2bChain;
    public uint nodes;
    public uint depth;
    public int decisionMode;
    public int chosenIdx;
    public List<CandidateData> candidates;
    public bool actionHold;
    public byte[] actionX;
    public byte[] actionY;
    public string actionToken;

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.Append('{');
        AppendInt(sb, "episode_id", episodeId);
        AppendInt(sb, "step_id", stepId);
        AppendBool(sb, "game_complete", gameComplete);
        AppendMatrix(sb, "board_visible", boardVisible);
        AppendMatrix(sb, "board_hidden", boardHidden);
        AppendString(sb, "current_piece", currentPiece);
        AppendString(sb, "hold_piece", holdPiece);
        AppendStringList(sb, "next_visible", nextVisible);
        AppendBool(sb, "can_hold", canHold);
        AppendInt(sb, "combo", combo);
        AppendInt(sb, "b2b_chain", b2bChain);
        AppendUInt(sb, "nodes", nodes);
        AppendUInt(sb, "depth", depth);
        AppendInt(sb, "decision_mode", decisionMode);
        AppendInt(sb, "chosen_idx", chosenIdx);
        AppendCandidates(sb, "candidates", candidates);
        AppendBool(sb, "action_hold", actionHold);
        AppendByteArray(sb, "action_x", actionX);
        AppendByteArray(sb, "action_y", actionY);
        AppendString(sb, "action_token", actionToken, false);
        sb.Append('}');
        return sb.ToString();
    }

    public static List<string> ConvertPieces(List<Tetromino> pieces)
    {
        List<string> result = new List<string>(pieces != null ? pieces.Count : 0);
        if (pieces == null)
        {
            return result;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            result.Add(PieceToString(pieces[i]));
        }

        return result;
    }

    public static string PieceToString(Tetromino piece)
    {
        return piece == Tetromino.None ? "None" : piece.ToString();
    }

    public static byte[] CopyBytes(byte[] values)
    {
        if (values == null)
        {
            return null;
        }

        byte[] copy = new byte[values.Length];
        values.CopyTo(copy, 0);
        return copy;
    }

    private static void AppendInt(StringBuilder sb, string name, int value)
    {
        sb.Append('"').Append(name).Append("\":").Append(value).Append(',');
    }

    private static void AppendUInt(StringBuilder sb, string name, uint value)
    {
        sb.Append('"').Append(name).Append("\":").Append(value).Append(',');
    }

    private static void AppendBool(StringBuilder sb, string name, bool value)
    {
        sb.Append('"').Append(name).Append("\":").Append(value ? "true" : "false").Append(',');
    }

    private static void AppendString(StringBuilder sb, string name, string value, bool withComma = true)
    {
        sb.Append('"').Append(name).Append("\":\"").Append(Escape(value)).Append('"');
        if (withComma)
        {
            sb.Append(',');
        }
    }

    private static void AppendByteArray(StringBuilder sb, string name, byte[] values)
    {
        sb.Append('"').Append(name).Append("\":[");
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i]);
            }
        }
        sb.Append("],");
    }

    private static void AppendStringList(StringBuilder sb, string name, List<string> values)
    {
        sb.Append('"').Append(name).Append("\":[");
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(values[i])).Append('"');
            }
        }
        sb.Append("],");
    }

    private static void AppendMatrix(StringBuilder sb, string name, int[,] matrix)
    {
        sb.Append('"').Append(name).Append("\":[");
        int rows = matrix != null ? matrix.GetLength(0) : 0;
        int cols = matrix != null ? matrix.GetLength(1) : 0;
        for (int row = 0; row < rows; row++)
        {
            if (row > 0) sb.Append(',');
            sb.Append('[');
            for (int col = 0; col < cols; col++)
            {
                if (col > 0) sb.Append(',');
                sb.Append(matrix[row, col]);
            }
            sb.Append(']');
        }
        sb.Append("],");
    }

    private static void AppendCandidates(StringBuilder sb, string name, List<CandidateData> values)
    {
        sb.Append('"').Append(name).Append("\":[");
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i].ToJson());
            }
        }
        sb.Append("],");
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public class CandidateData
{
    public int idx;
    public bool hold;
    public byte[] cellsX;
    public byte[] cellsY;
    public int originalRank;
    public bool survivalPass;
    public int backedUpValue;
    public int backedUpSpike;
    public int linesCleared;
    public int placementKind;
    public bool b2b;
    public bool perfectClear;
    public int comboAfter;
    public int garbageSent;
    public bool hasTrace;
    public ColdClearNative.CCEvalTrace localTrace;
    public string placementToken;

    public static CandidateData FromNative(int idx, ColdClearNative.CCCandidate nativeCandidate)
    {
        return new CandidateData
        {
            idx = idx,
            hold = nativeCandidate.hold,
            cellsX = DecisionPacket.CopyBytes(nativeCandidate.expected_x),
            cellsY = DecisionPacket.CopyBytes(nativeCandidate.expected_y),
            originalRank = (int)nativeCandidate.original_rank,
            survivalPass = nativeCandidate.survival_pass,
            backedUpValue = nativeCandidate.eval_score,
            backedUpSpike = nativeCandidate.spike_score,
            linesCleared = CountClearedLines(nativeCandidate.cleared_lines),
            placementKind = nativeCandidate.placement_kind,
            b2b = nativeCandidate.b2b,
            perfectClear = nativeCandidate.perfect_clear,
            comboAfter = nativeCandidate.combo,
            garbageSent = (int)nativeCandidate.garbage_sent,
            hasTrace = nativeCandidate.has_trace,
            localTrace = nativeCandidate.trace,
            placementToken = PlacementToken.FromNative(
                nativeCandidate.hold,
                nativeCandidate.piece,
                nativeCandidate.expected_x,
                nativeCandidate.expected_y)
        };
    }

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"idx\":").Append(idx).Append(',');
        sb.Append("\"hold\":").Append(hold ? "true" : "false").Append(',');
        AppendByteArray(sb, "cells_x", cellsX);
        AppendByteArray(sb, "cells_y", cellsY);
        sb.Append("\"original_rank\":").Append(originalRank).Append(',');
        sb.Append("\"survival_pass\":").Append(survivalPass ? "true" : "false").Append(',');
        sb.Append("\"backed_up_value\":").Append(backedUpValue).Append(',');
        sb.Append("\"backed_up_spike\":").Append(backedUpSpike).Append(',');
        sb.Append("\"lines_cleared\":").Append(linesCleared).Append(',');
        sb.Append("\"placement_kind\":").Append(placementKind).Append(',');
        sb.Append("\"b2b\":").Append(b2b ? "true" : "false").Append(',');
        sb.Append("\"perfect_clear\":").Append(perfectClear ? "true" : "false").Append(',');
        sb.Append("\"combo_after\":").Append(comboAfter).Append(',');
        sb.Append("\"garbage_sent\":").Append(garbageSent).Append(',');
        sb.Append("\"has_trace\":").Append(hasTrace ? "true" : "false").Append(',');
        AppendTrace(sb, localTrace);
        sb.Append("\"placement_token\":\"").Append(Escape(placementToken)).Append("\"");
        sb.Append('}');
        return sb.ToString();
    }

    private static int CountClearedLines(int[] clearedLines)
    {
        int count = 0;
        if (clearedLines == null)
        {
            return count;
        }

        for (int i = 0; i < clearedLines.Length; i++)
        {
            if (clearedLines[i] >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendByteArray(StringBuilder sb, string name, byte[] values)
    {
        sb.Append('"').Append(name).Append("\":[");
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i]);
            }
        }
        sb.Append("],");
    }

    private static void AppendTrace(StringBuilder sb, ColdClearNative.CCEvalTrace trace)
    {
        sb.Append("\"local_trace\":{");
        sb.Append("\"clear_score\":").Append(trace.clear_score).Append(',');
        sb.Append("\"tspin_score\":").Append(trace.tspin_score).Append(',');
        sb.Append("\"pc_score\":").Append(trace.pc_score).Append(',');
        sb.Append("\"b2b_score\":").Append(trace.b2b_score).Append(',');
        sb.Append("\"combo_score\":").Append(trace.combo_score).Append(',');
        sb.Append("\"wasted_t\":").Append(trace.wasted_t).Append(',');
        sb.Append("\"height_penalty\":").Append(trace.height_penalty).Append(',');
        sb.Append("\"jeopardy_penalty\":").Append(trace.jeopardy_penalty).Append(',');
        sb.Append("\"well_score\":").Append(trace.well_score).Append(',');
        sb.Append("\"tslot_score\":[");
        if (trace.tslot_score != null)
        {
            for (int i = 0; i < trace.tslot_score.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(trace.tslot_score[i]);
            }
        }
        sb.Append("],");
        sb.Append("\"bumpiness_penalty\":").Append(trace.bumpiness_penalty).Append(',');
        sb.Append("\"hole_penalty\":").Append(trace.hole_penalty).Append(',');
        sb.Append("\"covered_penalty\":").Append(trace.covered_penalty).Append(',');
        sb.Append("\"row_transitions\":").Append(trace.row_transitions).Append("},");
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
