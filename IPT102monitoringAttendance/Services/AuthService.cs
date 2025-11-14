using BCrypt.Net;
using IPT102monitoringAttendance.Models;
using MongoDB.Driver;

namespace IPT102monitoringAttendance.Services
{
    public class AuthService
    {
        private readonly MongoDbService _mongoDbService;

        public AuthService(MongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
        }

        public async Task<User?> AuthenticateUserAsync(string username, string password)
        {
            var user = await _mongoDbService.Professors
                .Find(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                user.LastLogin = DateTime.UtcNow;
                await _mongoDbService.Professors.ReplaceOneAsync(u => u.Id == user.Id, user);
                return user;
            }

            return null;
        }

        public async Task<bool> CreateUserAsync(string username, string password, string email, string role = "Professor")
        {
            var existingUser = await _mongoDbService.Professors
                .Find(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (existingUser != null)
                return false;

            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Email = email,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            await _mongoDbService.Professors.InsertOneAsync(user);
            return true;
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}