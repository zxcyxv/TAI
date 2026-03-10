using System.Collections.Generic;
using System.Text;

public class DecisionPacket
{
    // Episode tracking
    public int episodeId;
    public int stepId;
    public bool gameComplete;

    // Observation
    public int[,] boardVisible;    // [20,10]
    public int[,] boardHidden;     // [2,10]
    public string currentPiece;
    public string holdPiece;       // null if none
    public List<string> nextVisible;
    public bool canHold;
    public int combo;
    public int b2bChain;
    public uint incoming;

    // Search metadata
    public uint nodes;
    public uint depth;
    public string decisionMode;
    public int chosenIdx;
    public int? primaryCompareIdx;
    public string primaryCompareBasis;
    public int? bestValueAltIdx;
    public int? bestSurvivalAltIdx;
    public int? bestSpikeAltIdx;
    public int? marginValueVsPrimary;
    public int? marginSpikeVsPrimary;
    public bool firstSurvivalPass;
    public bool anySurvivalPass;
    public bool cotEligible;

    // Chosen action
    public bool actionHold;
    public string placementToken;
    public string actionToken;
    public byte[] cellsX, cellsY;

    // Chosen plan
    public List<PlanStepData> chosenPlan;

    // All candidates
    public List<CandidateData> candidates;

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.Append('{');

        // Episode
        AppendInt(sb, "episode_id", episodeId);
        AppendInt(sb, "step_id", stepId);
        AppendBool(sb, "game_complete", gameComplete);

        // Observation
        sb.Append("\"obs\":{");
        AppendMatrix(sb, "board_visible", boardVisible);
        AppendMatrix(sb, "board_hidden", boardHidden);
        AppendString(sb, "current_piece", currentPiece);
        AppendString(sb, "hold_piece", holdPiece);
        AppendStringList(sb, "next_visible", nextVisible);
        AppendBool(sb, "can_hold", canHold);
        AppendInt(sb, "combo", combo);
        AppendInt(sb, "b2b_chain", b2bChain);
        AppendUInt(sb, "incoming", incoming, false);
        sb.Append("},");

        // Search metadata
        sb.Append("\"search\":{");
        AppendUInt(sb, "nodes", nodes);
        AppendUInt(sb, "depth", depth);
        AppendString(sb, "decision_mode", decisionMode);
        AppendInt(sb, "chosen_idx", chosenIdx);
        AppendNullableInt(sb, "primary_compare_idx", primaryCompareIdx);
        AppendString(sb, "primary_compare_basis", primaryCompareBasis);
        AppendNullableInt(sb, "best_value_alt_idx", bestValueAltIdx);
        AppendNullableInt(sb, "best_survival_alt_idx", bestSurvivalAltIdx);
        AppendNullableInt(sb, "best_spike_alt_idx", bestSpikeAltIdx);
        AppendNullableInt(sb, "margin_value_vs_primary", marginValueVsPrimary);
        AppendNullableInt(sb, "margin_spike_vs_primary", marginSpikeVsPrimary);
        AppendBool(sb, "first_survival_pass", firstSurvivalPass);
        AppendBool(sb, "any_survival_pass", anySurvivalPass);
        AppendBool(sb, "cot_eligible", cotEligible, false);
        sb.Append("},");

        // Chosen action
        sb.Append("\"action\":{");
        AppendBool(sb, "hold", actionHold);
        AppendString(sb, "placement_token", placementToken);
        AppendString(sb, "action_token", actionToken);
        AppendByteArray(sb, "cells_x", cellsX);
        AppendByteArray(sb, "cells_y", cellsY, false);
        sb.Append("},");

        // Chosen plan
        AppendPlanSteps(sb, "chosen_plan", chosenPlan);

        // Candidates
        AppendCandidates(sb, "candidates", candidates, false);

        sb.Append('}');
        return sb.ToString();
    }

    public static List<string> ConvertPieces(List<Tetromino> pieces)
    {
        List<string> result = new List<string>(pieces != null ? pieces.Count : 0);
        if (pieces == null) return result;
        for (int i = 0; i < pieces.Count; i++)
            result.Add(PieceToString(pieces[i]));
        return result;
    }

    public static string PieceToString(Tetromino piece)
    {
        return piece == Tetromino.None ? "None" : piece.ToString();
    }

    public static string CCPieceToString(ColdClearNative.CCPiece piece)
    {
        return piece switch
        {
            ColdClearNative.CCPiece.CC_I => "I",
            ColdClearNative.CCPiece.CC_O => "O",
            ColdClearNative.CCPiece.CC_T => "T",
            ColdClearNative.CCPiece.CC_L => "L",
            ColdClearNative.CCPiece.CC_J => "J",
            ColdClearNative.CCPiece.CC_S => "S",
            ColdClearNative.CCPiece.CC_Z => "Z",
            _ => "None"
        };
    }

    public static byte[] CopyBytes(byte[] values)
    {
        if (values == null) return null;
        byte[] copy = new byte[values.Length];
        values.CopyTo(copy, 0);
        return copy;
    }

    public static string DecisionModeToString(byte mode)
    {
        return mode switch
        {
            0 => "Book",
            1 => "Normal",
            2 => "SurviveFilter",
            3 => "SpikeBackup",
            _ => "Normal"
        };
    }

    public static string CompareBasisToString(byte basis)
    {
        return basis switch
        {
            0 => "NONE",
            1 => "BEST_OTHER_BY_RANK",
            2 => "BEST_FILTERED_OUT_BY_SURVIVAL",
            3 => "SECOND_BEST_SPIKE",
            4 => "BOOK",
            _ => "NONE"
        };
    }

    // --- JSON helpers ---

    private static void AppendInt(StringBuilder sb, string name, int value)
    {
        sb.Append('"').Append(name).Append("\":").Append(value).Append(',');
    }

    private static void AppendUInt(StringBuilder sb, string name, uint value, bool comma = true)
    {
        sb.Append('"').Append(name).Append("\":").Append(value);
        if (comma) sb.Append(',');
    }

    private static void AppendBool(StringBuilder sb, string name, bool value, bool comma = true)
    {
        sb.Append('"').Append(name).Append("\":").Append(value ? "true" : "false");
        if (comma) sb.Append(',');
    }

    private static void AppendString(StringBuilder sb, string name, string value, bool comma = true)
    {
        if (value == null)
            sb.Append('"').Append(name).Append("\":null");
        else
            sb.Append('"').Append(name).Append("\":\"").Append(Escape(value)).Append('"');
        if (comma) sb.Append(',');
    }

    private static void AppendNullableInt(StringBuilder sb, string name, int? value, bool comma = true)
    {
        sb.Append('"').Append(name).Append("\":");
        if (value.HasValue)
            sb.Append(value.Value);
        else
            sb.Append("null");
        if (comma) sb.Append(',');
    }

    private static void AppendByteArray(StringBuilder sb, string name, byte[] values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendStringList(StringBuilder sb, string name, List<string> values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendMatrix(StringBuilder sb, string name, int[,] matrix, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendPlanSteps(StringBuilder sb, string name, List<PlanStepData> steps, bool comma = true)
    {
        sb.Append('"').Append(name).Append("\":[");
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(steps[i].ToJson());
            }
        }
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendCandidates(StringBuilder sb, string name, List<CandidateData> values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public class PlanStepData
{
    public string piece;
    public int placementKind;
    public int linesCleared;
    public int garbageSent;
    public bool b2bAfter;
    public int comboAfter;
    public byte[] cellsX, cellsY;

    public static PlanStepData FromPlanPlacement(ColdClearNative.CCPlanPlacement p)
    {
        int lines = 0;
        if (p.cleared_lines != null)
            for (int i = 0; i < p.cleared_lines.Length; i++)
                if (p.cleared_lines[i] >= 0) lines++;

        return new PlanStepData
        {
            piece = DecisionPacket.CCPieceToString(p.piece),
            placementKind = p.placement_kind,
            linesCleared = lines,
            garbageSent = (int)p.garbage_sent,
            b2bAfter = p.b2b,
            comboAfter = p.combo,
            cellsX = DecisionPacket.CopyBytes(p.expected_x),
            cellsY = DecisionPacket.CopyBytes(p.expected_y),
        };
    }

    public static PlanStepData FromPVStep(ColdClearNative.CCPVStep s)
    {
        return new PlanStepData
        {
            piece = DecisionPacket.CCPieceToString(s.piece),
            placementKind = s.placement_kind,
            linesCleared = s.lines_cleared,
            garbageSent = (int)s.garbage_sent,
            b2bAfter = s.b2b,
            comboAfter = s.combo,
            cellsX = DecisionPacket.CopyBytes(s.expected_x),
            cellsY = DecisionPacket.CopyBytes(s.expected_y),
        };
    }

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"piece\":\"").Append(piece ?? "").Append("\",");
        sb.Append("\"placement_kind\":").Append(placementKind).Append(',');
        sb.Append("\"lines_cleared\":").Append(linesCleared).Append(',');
        sb.Append("\"garbage_sent\":").Append(garbageSent).Append(',');
        sb.Append("\"b2b_after\":").Append(b2bAfter ? "true" : "false").Append(',');
        sb.Append("\"combo_after\":").Append(comboAfter).Append(',');
        AppendByteArray(sb, "cells_x", cellsX);
        AppendByteArray(sb, "cells_y", cellsY, false);
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendByteArray(StringBuilder sb, string name, byte[] values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }
}

public class TraceData
{
    public int clearScore, tspinScore, pcScore, b2bScore, comboScore;
    public int wastedT, heightPenalty, jeopardyPenalty, wellScore;
    public int[] tslotScore;
    public int bumpinessPenalty, holePenalty, coveredPenalty, rowTransitions;

    public static TraceData FromNative(ColdClearNative.CCEvalTrace t)
    {
        return new TraceData
        {
            clearScore = t.clear_score,
            tspinScore = t.tspin_score,
            pcScore = t.pc_score,
            b2bScore = t.b2b_score,
            comboScore = t.combo_score,
            wastedT = t.wasted_t,
            heightPenalty = t.height_penalty,
            jeopardyPenalty = t.jeopardy_penalty,
            wellScore = t.well_score,
            tslotScore = t.tslot_score != null ? (int[])t.tslot_score.Clone() : new int[4],
            bumpinessPenalty = t.bumpiness_penalty,
            holePenalty = t.hole_penalty,
            coveredPenalty = t.covered_penalty,
            rowTransitions = t.row_transitions,
        };
    }

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"clear_score\":").Append(clearScore).Append(',');
        sb.Append("\"tspin_score\":").Append(tspinScore).Append(',');
        sb.Append("\"pc_score\":").Append(pcScore).Append(',');
        sb.Append("\"b2b_score\":").Append(b2bScore).Append(',');
        sb.Append("\"combo_score\":").Append(comboScore).Append(',');
        sb.Append("\"wasted_t\":").Append(wastedT).Append(',');
        sb.Append("\"height_penalty\":").Append(heightPenalty).Append(',');
        sb.Append("\"jeopardy_penalty\":").Append(jeopardyPenalty).Append(',');
        sb.Append("\"well_score\":").Append(wellScore).Append(',');
        sb.Append("\"tslot_score\":[");
        if (tslotScore != null)
            for (int i = 0; i < tslotScore.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(tslotScore[i]);
            }
        sb.Append("],");
        sb.Append("\"bumpiness_penalty\":").Append(bumpinessPenalty).Append(',');
        sb.Append("\"hole_penalty\":").Append(holePenalty).Append(',');
        sb.Append("\"covered_penalty\":").Append(coveredPenalty).Append(',');
        sb.Append("\"row_transitions\":").Append(rowTransitions);
        sb.Append('}');
        return sb.ToString();
    }
}

public class CandidateData
{
    public int idx;
    public string piece;
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
    public int[] clearedLines;
    public bool hasTrace;
    public TraceData localTrace;
    public string placementToken;

    // Board after
    public int[,] boardAfterVisible;  // [20,10]
    public int[,] boardAfterHidden;   // [2,10]

    // PV
    public int pvLen;
    public List<PlanStepData> pvSteps;

    public static CandidateData FromNative(int idx, ColdClearNative.CCCandidate n)
    {
        // Board snapshot: reshape [u8; 220] → [20,10] visible + [2,10] hidden
        int[,] boardVis = new int[20, 10];
        int[,] boardHid = new int[2, 10];
        if (n.board_after != null)
        {
            for (int row = 0; row < 20; row++)
                for (int col = 0; col < 10; col++)
                    boardVis[row, col] = n.board_after[row * 10 + col];
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 10; col++)
                    boardHid[row, col] = n.board_after[(20 + row) * 10 + col];
        }

        // PV steps
        int pvLen = n.pv_len;
        List<PlanStepData> pvSteps = new List<PlanStepData>(pvLen);
        if (n.pv_steps != null)
        {
            for (int i = 0; i < pvLen && i < n.pv_steps.Length; i++)
                pvSteps.Add(PlanStepData.FromPVStep(n.pv_steps[i]));
        }

        return new CandidateData
        {
            idx = idx,
            piece = DecisionPacket.CCPieceToString(n.piece),
            hold = n.hold,
            cellsX = DecisionPacket.CopyBytes(n.expected_x),
            cellsY = DecisionPacket.CopyBytes(n.expected_y),
            originalRank = (int)n.original_rank,
            survivalPass = n.survival_pass,
            backedUpValue = n.eval_score,
            backedUpSpike = n.spike_score,
            linesCleared = CountClearedLines(n.cleared_lines),
            placementKind = n.placement_kind,
            b2b = n.b2b,
            perfectClear = n.perfect_clear,
            comboAfter = n.combo,
            garbageSent = (int)n.garbage_sent,
            clearedLines = n.cleared_lines != null ? (int[])n.cleared_lines.Clone() : new int[] { -1, -1, -1, -1 },
            hasTrace = n.has_trace,
            localTrace = TraceData.FromNative(n.trace),
            placementToken = PlacementToken.FromNative(
                n.hold,
                n.piece,
                n.expected_x,
                n.expected_y),
            boardAfterVisible = boardVis,
            boardAfterHidden = boardHid,
            pvLen = pvLen,
            pvSteps = pvSteps,
        };
    }

    public string ToJson()
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append('{');
        sb.Append("\"idx\":").Append(idx).Append(',');
        sb.Append("\"piece\":\"").Append(piece ?? "").Append("\",");
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
        AppendIntArray(sb, "cleared_lines", clearedLines);
        sb.Append("\"has_trace\":").Append(hasTrace ? "true" : "false").Append(',');
        sb.Append("\"local_trace\":").Append(localTrace != null ? localTrace.ToJson() : "null").Append(',');
        sb.Append("\"placement_token\":\"").Append(Escape(placementToken)).Append("\",");

        // Board after
        AppendMatrix(sb, "board_after_visible", boardAfterVisible);
        AppendMatrix(sb, "board_after_hidden", boardAfterHidden);

        // PV
        sb.Append("\"pv_len\":").Append(pvLen).Append(',');
        sb.Append("\"pv_steps\":[");
        if (pvSteps != null)
        {
            for (int i = 0; i < pvSteps.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(pvSteps[i].ToJson());
            }
        }
        sb.Append(']');

        sb.Append('}');
        return sb.ToString();
    }

    private static int CountClearedLines(int[] clearedLines)
    {
        int count = 0;
        if (clearedLines == null) return count;
        for (int i = 0; i < clearedLines.Length; i++)
            if (clearedLines[i] >= 0) count++;
        return count;
    }

    private static void AppendByteArray(StringBuilder sb, string name, byte[] values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendIntArray(StringBuilder sb, string name, int[] values, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static void AppendMatrix(StringBuilder sb, string name, int[,] matrix, bool comma = true)
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
        sb.Append(']');
        if (comma) sb.Append(',');
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
