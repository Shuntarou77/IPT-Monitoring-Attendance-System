using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IPT102monitoringAttendance.Models
{
    public class ClassSchedule
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("day")]
        public string Day { get; set; } = string.Empty;

        [BsonElement("time")]
        public string Time { get; set; } = string.Empty;

        [BsonElement("section")]
        public string Section { get; set; } = string.Empty;

        [BsonElement("roomNumber")]
        public string RoomNumber { get; set; } = string.Empty;

        [BsonElement("subject")]
        public string Subject { get; set; } = string.Empty;

        [BsonElement("professorUsername")]
        public string ProfessorUsername { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}