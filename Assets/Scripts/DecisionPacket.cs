using System.Text;

public class DecisionPacket
{
    public int episodeId;
    public int stepId;
    public bool gameComplete;
    public string engineJson;

    public string ToJsonLine(CollectorMeta meta)
    {
        StringBuilder sb = new StringBuilder((engineJson?.Length ?? 4) + 512);
        sb.Append("{\"schema_version\":\"cot_raw_v3\",\"engine\":");
        sb.Append(engineJson ?? "null");
        sb.Append(",\"collector_meta\":{");
        sb.Append("\"run_id\":\"").Append(Escape(meta.runId)).Append("\",");
        sb.Append("\"episode_id\":").Append(episodeId).Append(',');
        sb.Append("\"step_id\":").Append(stepId).Append(',');
        sb.Append("\"game_complete\":").Append(gameComplete ? "true" : "false").Append(',');
        sb.Append("\"collector_seed\":").Append(meta.collectorSeed).Append(',');
        sb.Append("\"engine_commit\":\"").Append(Escape(meta.engineCommit)).Append("\",");
        sb.Append("\"weights_hash\":\"").Append(Escape(meta.weightsHash)).Append("\",");
        sb.Append("\"options_hash\":\"").Append(Escape(meta.optionsHash)).Append("\",");
        sb.Append("\"book_enabled\":").Append(meta.bookEnabled ? "true" : "false");
        sb.Append("}}");
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public struct CollectorMeta
{
    public string runId;
    public int collectorSeed;
    public string engineCommit;
    public string weightsHash;
    public string optionsHash;
    public bool bookEnabled;
}
