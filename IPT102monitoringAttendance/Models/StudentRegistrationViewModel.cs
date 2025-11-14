using System.ComponentModel.DataAnnotations;

namespace IPT102monitoringAttendance.Models
{
    public class StudentRegistrationViewModel
    {
        [Required(ErrorMessage = "Student number is required.")]
        [RegularExpression(@"^\d{2}-\d{4}$", ErrorMessage = "Student number must be in format XX-XXXX (e.g., 21-0001)")]
        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Middle name is required.")]
        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Section is required.")]
        [Display(Name = "Section")]
        public string Section { get; set; } = string.Empty;
    }
}