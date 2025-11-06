using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CSVProssessor.Domain.Entities
{
    public class BaseEntity
    {
        [Key]
        [JsonPropertyName("id")] // Serialize as 'id' (lowercase) for consistency with Cosmos DB
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Guid? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}