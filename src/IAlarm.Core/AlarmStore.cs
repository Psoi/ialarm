using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAlarm.Core;

/// <summary>All alarms in one small JSON file. Writes go through a temp file so a crash never leaves half a file.</summary>
public sealed class AlarmStore(string path)
{
    public string Path => path;

    public AlarmData Load()
    {
        if (!File.Exists(path))
            return new AlarmData();
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), AlarmJson.Default.AlarmData) ?? new AlarmData();
        }
        catch (JsonException)
        {
            // Keep the unreadable file for recovery instead of overwriting it on the next save.
            File.Copy(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), overwrite: true);
            return new AlarmData();
        }
    }

    public void Save(IEnumerable<Alarm> alarms)
    {
        var data = new AlarmData { Format = AlarmData.FormatName, Alarms = alarms.ToList() };
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(data, AlarmJson.Default.AlarmData));
        File.Move(temp, path, overwrite: true);
    }
}

[JsonSerializable(typeof(AlarmData))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
internal sealed partial class AlarmJson : JsonSerializerContext;
