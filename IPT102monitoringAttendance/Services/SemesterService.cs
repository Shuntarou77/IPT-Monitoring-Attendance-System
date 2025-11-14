using IPT102monitoringAttendance.Models;
using MongoDB.Driver;

namespace IPT102monitoringAttendance.Services
{
    public class SemesterService
    {
        private readonly MongoDbService _db;
        private const string CurrentSemesterKey = "currentSemester";

        public SemesterService(MongoDbService db)
        {
            _db = db;
        }

        public async Task<string> GetCurrentSemesterAsync()
        {
            // Always derive semester from current date (August-December = 1st, January-May = 2nd)
            var today = DateTime.UtcNow;
            int term;
            int year = today.Year;
            
            // August to December = 1st Semester of current academic year
            if (today.Month >= 8 && today.Month <= 12)
            {
                term = 1;
                year = today.Year; // e.g., Aug-Dec 2025 = 2025-1
            }
            // January to May = 2nd Semester of previous academic year
            else if (today.Month >= 1 && today.Month <= 5)
            {
                term = 2;
                year = today.Year - 1; // e.g., Jan-May 2025 = 2024-2 (AY 2024-2025)
            }
            // June/July = use previous semester (2nd semester of previous academic year)
            else
            {
                term = 2;
                year = today.Year - 1; // e.g., June/July 2025 = 2024-2
            }
            
            string semester = $"{year}-{term}";
            
            // Update database for consistency with other parts of the system
            await SetCurrentSemesterAsync(semester);
            
            return semester;
        }

        public async Task SetCurrentSemesterAsync(string semester)
        {
            var filter = Builders<Setting>.Filter.Eq(s => s.Key, CurrentSemesterKey);
            var update = Builders<Setting>.Update.Set(s => s.Value, semester);

            var options = new UpdateOptions { IsUpsert = true };
            await _db.Semesters.UpdateOneAsync(filter, update, options);
        }

        public static DateTime GetSemesterStartDate(int year, int term)
        {
            // University Calendar (A.Y. {year}-{year+1})
            // 1st Semester starts on August 11, {year}
            // 2nd Semester starts on January 12, {year+1}
            if (term == 1)
            {
                return new DateTime(year, 8, 11);
            }
            else
            {
                return new DateTime(year + 1, 1, 12);
            }
        }
    }
}


