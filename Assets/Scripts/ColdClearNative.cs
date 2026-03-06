using System;
using System.Runtime.InteropServices;

public static class ColdClearNative
{
    private const string Lib = "cold_clear";

    // Opaque pointers
    public static class CCAsyncBot { }
    public static class CCBook { }

    public enum CCPiece : int { CC_I, CC_O, CC_T, CC_L, CC_J, CC_S, CC_Z }
    public enum CCMovement : int { CC_LEFT, CC_RIGHT, CC_CW, CC_CCW, CC_DROP }
    public enum CCMovementMode : int { CC_0G, CC_20G, CC_HARD_DROP_ONLY }
    public enum CCSpawnRule : int { CC_ROW_19_OR_20, CC_ROW_21_AND_FALL }
    public enum CCPcPriority : int { CC_PC_OFF, CC_PC_FASTEST, CC_PC_ATTACK }
    public enum CCBotPollStatus : int { CC_MOVE_PROVIDED, CC_WAITING, CC_BOT_DEAD }

    [StructLayout(LayoutKind.Sequential)]
    public struct CCOptions
    {
        public CCMovementMode mode;
        public CCSpawnRule spawn_rule;
        public CCPcPriority pcloop;
        public uint min_nodes;
        public uint max_nodes;
        public uint threads;

        [MarshalAs(UnmanagedType.I1)] public bool use_hold;
        [MarshalAs(UnmanagedType.I1)] public bool speculate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CCWeights
    {
        public int back_to_back, bumpiness, bumpiness_sq, row_transitions, height, top_half, top_quarter, jeopardy;
        public int cavity_cells, cavity_cells_sq, overhang_cells, overhang_cells_sq, covered_cells, covered_cells_sq;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public int[] tslot;
        public int well_depth, max_well_depth;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] public int[] well_column;

        public int b2b_clear, clear1, clear2, clear3, clear4;
        public int tspin1, tspin2, tspin3, mini_tspin1, mini_tspin2;
        public int perfect_clear, combo_garbage, move_time, wasted_t;

        [MarshalAs(UnmanagedType.I1)] public bool use_bag;
        [MarshalAs(UnmanagedType.I1)] public bool timed_jeopardy;
        [MarshalAs(UnmanagedType.I1)] public bool stack_pc_damage;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CCMove
    {
        [MarshalAs(UnmanagedType.I1)] public bool hold;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] expected_x;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] expected_y;

        public byte movement_count;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public CCMovement[] movements;

        public uint nodes;
        public uint depth;
        public uint original_rank;

        public static CCMove CreateBuffer() => new CCMove
        {
            expected_x = new byte[4],
            expected_y = new byte[4],
            movements = new CCMovement[32],
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CCEvalTrace
    {
        public int clear_score;
        public int tspin_score;
        public int pc_score;
        public int b2b_score;
        public int combo_score;
        public int wasted_t;
        public int height_penalty;
        public int jeopardy_penalty;
        public int well_score;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public int[] tslot_score;
        public int bumpiness_penalty;
        public int hole_penalty;
        public int covered_penalty;
        public int row_transitions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CCCandidate
    {
        public CCPiece piece;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] expected_x;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] expected_y;
        [MarshalAs(UnmanagedType.I1)] public bool hold;
        public int eval_score;
        [MarshalAs(UnmanagedType.I1)] public bool has_trace;
        public CCEvalTrace trace;
    }

    // defaults
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void cc_default_options(out CCOptions options);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void cc_default_weights(out CCWeights weights);

    // lifecycle
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr cc_launch_async(
        ref CCOptions options,
        ref CCWeights weights,
        IntPtr book,
        [In] CCPiece[] queue,
        uint count);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void cc_destroy_async(IntPtr bot);

    // thinking
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void cc_request_next_move(IntPtr bot, uint incoming);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void cc_add_next_piece_async(IntPtr bot, CCPiece piece);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CCBotPollStatus cc_poll_next_move(
        IntPtr bot,
        ref CCMove move,
        IntPtr plan,
        IntPtr plan_length,
        IntPtr candidates,
        ref uint candidate_count);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CCBotPollStatus cc_block_next_move(
        IntPtr bot,
        ref CCMove move,
        IntPtr plan,
        IntPtr plan_length,
        IntPtr candidates,
        ref uint candidate_count);
}
