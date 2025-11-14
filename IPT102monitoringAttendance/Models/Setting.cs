using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IPT102monitoringAttendance.Models
{
    public class Setting
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("key")]
        public string Key { get; set; } = string.Empty;

        [BsonElement("value")]
        public string Value { get; set; } = string.Empty;
    }
}


