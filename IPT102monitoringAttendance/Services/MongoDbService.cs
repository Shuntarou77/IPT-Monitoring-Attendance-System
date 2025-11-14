using MongoDB.Driver;
using IPT102monitoringAttendance.Models;

namespace IPT102monitoringAttendance.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase("IPT102Attendance");
        }

        public IMongoCollection<User> Professors => _database.GetCollection<User>("professors");
        public IMongoCollection<Student> Students => _database.GetCollection<Student>("students");
        public IMongoCollection<AttendanceRecord> AttendanceRecords => _database.GetCollection<AttendanceRecord>("attendanceRecords");
        public IMongoCollection<ClassSchedule> ClassSchedules => _database.GetCollection<ClassSchedule>("classSchedules");
        public IMongoCollection<Setting> Semesters => _database.GetCollection<Setting>("semesters");
    }
}