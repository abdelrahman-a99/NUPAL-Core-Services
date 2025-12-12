using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nupal.Domain.Entities
{
    public class ContactMessage
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("studentName")]
        public string StudentName { get; set; }

        [BsonElement("studentEmail")]
        public string StudentEmail { get; set; }

        [BsonElement("message")]
        public string Message { get; set; }

        [BsonElement("submittedAt")]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
