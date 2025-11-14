using IPT102monitoringAttendance.Models;
using IPT102monitoringAttendance.Services;

namespace IPT102monitoringAttendance.Services
{
    public class DatabaseSeeder
    {
        private readonly MongoDbService _mongoDbService;
        private readonly AuthService _authService;

        public DatabaseSeeder(MongoDbService mongoDbService, AuthService authService)
        {
            _mongoDbService = mongoDbService;
            _authService = authService;
        }

        public async Task SeedDatabaseAsync()
        {
            await _authService.CreateUserAsync("professor", "password123", "professor@university.edu", "Professor");

            var sampleStudents = new List<Student>
            {
        
            };

        }
    }
}