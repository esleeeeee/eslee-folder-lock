using System.Text;
using System.Text.Json;
using FolderGate.Core.Formatting;

namespace FolderGate.Core.Storage;

public static class OperationLogDisplayFormatter
{
    public static string FormatJsonLinesForDisplay(string jsonLines)
    {
        return FormatJsonLinesForDisplay(jsonLines, TimeZoneInfo.Local, "Local");
    }

    public static string FormatJsonLinesForDisplay(string jsonLines, TimeZoneInfo timeZone, string timeZoneLabel)
    {
        if (string.IsNullOrWhiteSpace(jsonLines))
        {
            return "아직 기록된 로그가 없습니다.";
        }

        StringBuilder builder = new();
        using StringReader reader = new(jsonLines);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            OperationLogRecord? record = TryReadRecord(line);
            builder.AppendLine(record is null
                ? "[읽을 수 없는 로그 항목]"
                : FormatRecord(record, timeZone, timeZoneLabel));
        }

        return builder.Length == 0
            ? "아직 기록된 로그가 없습니다."
            : builder.ToString();
    }

    private static OperationLogRecord? TryReadRecord(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OperationLogRecord>(json, JsonOptionsFactory.Create(indented: false));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatRecord(OperationLogRecord record, TimeZoneInfo timeZone, string timeZoneLabel)
    {
        StringBuilder builder = new();
        builder.Append(LocalTimeFormatter.FormatLocal(record.TimestampUtc, timeZone, timeZoneLabel));
        AppendPart(builder, record.Status);
        AppendPart(builder, record.Operation);
        AppendPart(builder, record.Path);
        AppendPart(builder, record.Message);
        AppendPart(builder, record.ExceptionType);
        return builder.ToString();
    }

    private static void AppendPart(StringBuilder builder, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append(" | ");
            builder.Append(value);
        }
    }
}
