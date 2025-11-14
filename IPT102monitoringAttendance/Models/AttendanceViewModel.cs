using System.ComponentModel.DataAnnotations;

namespace IPT102monitoringAttendance.Models
{
    public class AttendanceViewModel
    {
        public string Section { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public List<StudentAttendanceDto> Students { get; set; } = new();
    }

    public class StudentAttendanceDto
    {
        public string StudentId { get; set; } = string.Empty; 
        public string StudentNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = ""; 
    }
}