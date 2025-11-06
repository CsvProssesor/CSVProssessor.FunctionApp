using Newtonsoft.Json;

namespace CSVProssessor.Domain.Entities
{
    public class CsvRecord : BaseEntity
    {
        /// <summary>
        /// Override Id property to map to 'id' for Cosmos SDK (Newtonsoft.Json)
        /// BaseEntity already has [JsonPropertyName("id")] for System.Text.Json responses
        /// </summary>
        [JsonProperty("id")] // Newtonsoft.Json - for Cosmos storage
        public new Guid Id
        {
            get => base.Id;
            set => base.Id = value;
        }

        public Guid JobId { get; set; }
        public string? FileName { get; set; }
        public DateTime ImportedAt { get; set; }
        
        /// <summary>
        /// CSV row data stored as JSON string
        /// </summary>
        public string? Data { get; set; }
    }
}