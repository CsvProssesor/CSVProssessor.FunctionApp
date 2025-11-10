using CSVProssessor.Domain.Enums;
using Newtonsoft.Json;

namespace CSVProssessor.Domain.Entities;

public class CsvJob : BaseEntity
{
    /// <summary>
    ///     Override Id property to map to 'id' for Cosmos SDK (Newtonsoft.Json)
    ///     BaseEntity already has [JsonPropertyName("id")] for System.Text.Json responses
    /// </summary>
    [JsonProperty("id")]
    public new Guid Id
    {
        get => base.Id;
        set => base.Id = value;
    }

    /// <summary>
    ///     Unique file name stored in MinIO (with timestamp and GUID)
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Original file name uploaded by user
    /// </summary>
    public string? OriginalFileName { get; set; }

    public CsvJobType Type { get; set; }
    public CsvJobStatus Status { get; set; }
}