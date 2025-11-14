using Microsoft.AspNetCore.Mvc;
using IPT102monitoringAttendance.Models;
using IPT102monitoringAttendance.Services;
using MongoDB.Driver;

namespace IPT102monitoringAttendance.Controllers
{
    public class IPT102Controller : Controller
    {
        private readonly MongoDbService _mongoDbService;
        private readonly AuthService _authService;
        private readonly SemesterService _semesterService;
        private readonly ReportService _reportService;
        private readonly EmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IPT102Controller(MongoDbService mongoDbService, AuthService authService, SemesterService semesterService, ReportService reportService, EmailService emailService, IHttpContextAccessor httpContextAccessor)
        {
            _mongoDbService = mongoDbService;
            _authService = authService;
            _semesterService = semesterService;
            _reportService = reportService;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
        }

        public IActionResult LoginPage()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSectionReport(string section, string semester = null)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");
            if (string.IsNullOrWhiteSpace(section))
                return BadRequest("Section is required");

            semester ??= await _semesterService.GetCurrentSemesterAsync();
            var pdfBytes = await _reportService.GenerateSectionSemesterReportPdfAsync(semester, section);
            var fileName = $"AttendanceReport_{section}_{semester}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPage(LoginViewModel model)
        {
           if (ModelState.IsValid)
{
    var user = await _authService.AuthenticateUserAsync(model.Username, model.Password);
    if (user != null)
    {
        HttpContext.Session.SetString("UserId", user.Id);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("Role", user.Role);
        return RedirectToAction("ProfSchedule", new { day = "Monday" });
    }
    // Try as a student: username = Surname, password = Student Number
    var student = await _mongoDbService.Students
        .Find(s => s.LastName.ToLower() == model.Username.ToLower() && s.StudentNumber == model.Password)
        .FirstOrDefaultAsync();
    if (student != null)
    {
        HttpContext.Session.SetString("StudentId", student.Id);
        HttpContext.Session.SetString("StudentName", student.FullName);
        HttpContext.Session.SetString("StudentNumber", student.StudentNumber);
        HttpContext.Session.SetString("StudentSurname", student.LastName);
        return RedirectToAction("StudentDashboard");
    }
    ModelState.AddModelError("", "Invalid username or password.");
}
return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ProfSchedule(string day = "Monday")
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            var validDays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            if (!validDays.Contains(day, StringComparer.OrdinalIgnoreCase))
                day = "Monday";

            // Get the logged-in professor's username
            var professorUsername = HttpContext.Session.GetString("Username");
            
            // Filter schedules by day and professor's username
            var filter = Builders<ClassSchedule>.Filter.Eq(s => s.Day, day);
            if (!string.IsNullOrWhiteSpace(professorUsername))
            {
                filter = Builders<ClassSchedule>.Filter.And(
                    filter,
                    Builders<ClassSchedule>.Filter.Eq(s => s.ProfessorUsername, professorUsername)
                );
            }

            var daySchedules = await _mongoDbService.ClassSchedules
                .Find(filter)
                .ToListAsync();

            ViewBag.SelectedDay = day;
            var semCode = await _semesterService.GetCurrentSemesterAsync();
            string semLabel = semCode;
            var parts = semCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var semYear))
            {
                var termLabel = parts[1] == "1" ? "1st Semester" : parts[1] == "2" ? "2nd Semester" : $"Sem {parts[1]}";
                semLabel = $"SY.{semYear} {termLabel}";
            }
            ViewBag.SemesterLabel = semLabel;
            return View(daySchedules);
        }

        [HttpPost]
        public async Task<IActionResult> AddSection(string day, string startTime, string endTime, string section, string roomNumber, string subject)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            var validDays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            if (!validDays.Contains(day, StringComparer.OrdinalIgnoreCase))
                day = "Monday";

            // ✅ Validate fields
            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
            {
                TempData["Error"] = "Please provide section, start time, and end time.";
                return RedirectToAction("ProfSchedule", new { day });
            }

            // ✅ Parse times
            if (!DateTime.TryParse(startTime, out var newStartTime) || !DateTime.TryParse(endTime, out var newEndTime))
            {
                TempData["Error"] = "Invalid time format. Please use format like '7:00 AM'.";
                return RedirectToAction("ProfSchedule", new { day });
            }

            if (newEndTime <= newStartTime)
            {
                TempData["Error"] = "End time must be later than start time.";
                return RedirectToAction("ProfSchedule", new { day });
            }

            // ✅ Get all schedules for the same day & room
            var sameRoomSchedules = await _mongoDbService.ClassSchedules
                .Find(s => s.Day == day && s.RoomNumber == roomNumber)
                .ToListAsync();

            foreach (var existing in sameRoomSchedules)
            {
                if (string.IsNullOrWhiteSpace(existing.Time)) continue;

                var existingParts = existing.Time.Split('-', StringSplitOptions.TrimEntries);
                if (existingParts.Length != 2 ||
                    !DateTime.TryParse(existingParts[0], out var existingStart) ||
                    !DateTime.TryParse(existingParts[1], out var existingEnd))
                    continue;

                // ✅ Check overlap — any overlap at all should block
                bool overlap = newStartTime < existingEnd && newEndTime > existingStart;
                if (overlap)
                {
                    TempData["Error"] = $"Room conflict: {roomNumber} already has a schedule from {existing.Time} on {day}.";
                    return RedirectToAction("ProfSchedule", new { day });
                }
            }

            // ✅ Semester is automatically determined by GetCurrentSemesterAsync() based on current date
            // ✅ Save in the same "7:00 AM - 8:00 AM" format
            var formattedTime = $"{newStartTime:hh:mm tt} - {newEndTime:hh:mm tt}";
            
            // Get the professor's username from session
            var professorUsername = HttpContext.Session.GetString("Username") ?? string.Empty;

            var classSchedule = new ClassSchedule
            {
                Day = day,
                Time = formattedTime,
                Section = section,
                RoomNumber = roomNumber ?? string.Empty,
                Subject = subject ?? string.Empty,
                ProfessorUsername = professorUsername,
                CreatedAt = DateTime.UtcNow
            };

            await _mongoDbService.ClassSchedules.InsertOneAsync(classSchedule);
            TempData["Success"] = "Section added successfully!";

            return RedirectToAction("ProfSchedule", new { day });
        }



        [HttpPost]
        public async Task<IActionResult> DeleteSection(string id, string day)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            // Get the logged-in professor's username
            var professorUsername = HttpContext.Session.GetString("Username");
            
            // Only allow deletion of schedules belonging to the logged-in professor
            var filter = Builders<ClassSchedule>.Filter.Eq(s => s.Id, id);
            if (!string.IsNullOrWhiteSpace(professorUsername))
            {
                filter = Builders<ClassSchedule>.Filter.And(
                    filter,
                    Builders<ClassSchedule>.Filter.Eq(s => s.ProfessorUsername, professorUsername)
                );
            }
            
            await _mongoDbService.ClassSchedules.DeleteOneAsync(filter);
            return RedirectToAction("ProfSchedule", new { day });
        }

[HttpGet]
public async Task<IActionResult> Attendance(string section, string subject = null, string date = null)
{
    if (!IsUserLoggedIn())
        return RedirectToAction("LoginPage");

    if (string.IsNullOrWhiteSpace(section))
        return RedirectToAction("ProfSchedule");

    // Get the logged-in professor's username
    var professorUsername = HttpContext.Session.GetString("Username");
    
    // If no subject provided, try to get it from the first schedule entry for this section and professor
    if (string.IsNullOrWhiteSpace(subject))
    {
        var scheduleFilter = Builders<ClassSchedule>.Filter.Eq(s => s.Section, section);
        if (!string.IsNullOrWhiteSpace(professorUsername))
        {
            scheduleFilter = Builders<ClassSchedule>.Filter.And(
                scheduleFilter,
                Builders<ClassSchedule>.Filter.Eq(s => s.ProfessorUsername, professorUsername)
            );
        }
        
        var schedule = await _mongoDbService.ClassSchedules
            .Find(scheduleFilter)
            .FirstOrDefaultAsync();
        if (schedule != null)
            subject = schedule.Subject;
    }

    if (string.IsNullOrWhiteSpace(subject))
    {
        TempData["Error"] = "Subject is required for attendance tracking.";
        return RedirectToAction("ProfSchedule");
    }

    DateTime selectedDate;
    if (!DateTime.TryParse(date, out selectedDate))
        selectedDate = DateTime.Today;

    var students = await _mongoDbService.Students
        .Find(s => s.Section == section)
        .ToListAsync();

    var currentSemester = await _semesterService.GetCurrentSemesterAsync();
    // Filter attendance records by section, subject, semester, and date
    var existingRecords = await _mongoDbService.AttendanceRecords
        .Find(r => r.Section == section && r.Subject == subject && r.Semester == currentSemester && r.Date >= selectedDate.Date && r.Date < selectedDate.Date.AddDays(1))
        .ToListAsync();

    var existingRecordsDict = existingRecords.ToDictionary(r => r.StudentId, r => r.Status);

    var viewModel = new AttendanceViewModel
    {
        Section = section,
        Subject = subject,
        Date = DateOnly.FromDateTime(selectedDate),
        Students = students.Select(s => new StudentAttendanceDto
        {
            StudentId = s.Id,
            StudentNumber = s.StudentNumber,
            Name = s.FullName,
            Status = existingRecordsDict.GetValueOrDefault(s.Id, "")
        }).ToList()
    };

    var semCode = await _semesterService.GetCurrentSemesterAsync();
    string semLabel = semCode;
    var parts = semCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 2 && int.TryParse(parts[0], out var semYear))
    {
        var termLabel = parts[1] == "1" ? "1st Semester" : parts[1] == "2" ? "2nd Semester" : $"Sem {parts[1]}";
        semLabel = $"SY.{semYear} {termLabel}";
    }
    ViewBag.SemesterLabel = semLabel;
    return View(viewModel);
}
        [HttpPost]
        public async Task<IActionResult> SaveAttendance(string section, string subject, DateOnly date, List<StudentAttendanceDto> students)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            if (string.IsNullOrWhiteSpace(subject))
            {
                TempData["Error"] = "Subject is required.";
                return RedirectToAction("Attendance", new { section, date = date.ToString("yyyy-MM-dd") });
            }

            var currentSemester = await _semesterService.GetCurrentSemesterAsync();
            // Delete existing records for this section, subject, semester, and date
            await _mongoDbService.AttendanceRecords.DeleteManyAsync(
                r => r.Section == section && r.Subject == subject && r.Semester == currentSemester && r.Date >= date.ToDateTime(TimeOnly.MinValue) && r.Date < date.ToDateTime(TimeOnly.MinValue).AddDays(1));

            var attendanceRecords = students.Select(dto => new AttendanceRecord
            {
                StudentId = dto.StudentId, 
                Section = section,
                Subject = subject,
                Date = date.ToDateTime(TimeOnly.MinValue),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Absent" : dto.Status,
                Semester = currentSemester,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (attendanceRecords.Any())
            {
                await _mongoDbService.AttendanceRecords.InsertManyAsync(attendanceRecords);
            }

            return RedirectToAction("Attendance", new { section, subject, date = date.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public async Task<IActionResult> Semester()
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");
            ViewBag.CurrentSemester = await _semesterService.GetCurrentSemesterAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Semester(string semester)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");
            if (!string.IsNullOrWhiteSpace(semester))
            {
                await _semesterService.SetCurrentSemesterAsync(semester.Trim());
            }
            return RedirectToAction("Semester");
        }

        [HttpPost]
        public async Task<IActionResult> AddStudentToSection(string section, string studentNumber, string firstName, string middleName, string lastName)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            if (!string.IsNullOrWhiteSpace(section) && !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
            {
                var student = new Student
                {
                    StudentNumber = studentNumber ?? string.Empty,
                    FirstName = firstName,
                    MiddleName = middleName ?? string.Empty,
                    Section = section,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow
                };

                await _mongoDbService.Students.InsertOneAsync(student);
            }
            return RedirectToAction("Attendance", new { section, date = DateOnly.FromDateTime(DateTime.Today) });
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("LoginPage");
        }

        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }
        public IActionResult PasswordHashGenerator()
        {
            return View();
        }

        [HttpPost]
        public IActionResult PasswordHashGenerator(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter a password.";
                return View();
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            ViewBag.OriginalPassword = password;
            ViewBag.HashedPassword = hashedPassword;

            return View();
        }
        public IActionResult StudentRegistration()
        {
            return View();
        }

        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> StudentRegistration(StudentRegistrationViewModel model)
{
    if (ModelState.IsValid)
    {
        var existingStudent = await _mongoDbService.Students
            .Find(s => s.StudentNumber == model.StudentNumber)
            .FirstOrDefaultAsync();

        if (existingStudent != null)
        {
            existingStudent.FirstName = model.FirstName;
            existingStudent.MiddleName = model.MiddleName;
            existingStudent.LastName = model.LastName;
            existingStudent.Section = model.Section;

            await _mongoDbService.Students.ReplaceOneAsync(s => s.Id == existingStudent.Id, existingStudent);
            ViewBag.Success = "Student information updated successfully!";
        }
        else
        {
            var student = new Student
            {
                StudentNumber = model.StudentNumber,
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                Section = model.Section,
                CreatedAt = DateTime.UtcNow
            };

            await _mongoDbService.Students.InsertOneAsync(student);
            ViewBag.Success = "Student registered successfully!";
        }

        return View(new StudentRegistrationViewModel());
    }

    return View(model);
}
        [HttpGet]
        public IActionResult ProfessorRegistration()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfessorRegistration(ProfessorRegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if username already exists
                var existingUser = await _mongoDbService.Professors
                    .Find(u => u.Username == model.Username)
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");
                    return View(model);
                }

                // Hash the password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var professor = new User
                {
                    Name = model.Name,
                    Username = model.Username,
                    PasswordHash = hashedPassword,
                    Email = model.Email,
                    Role = "Professor",
                    CreatedAt = DateTime.UtcNow
                };

                await _mongoDbService.Professors.InsertOneAsync(professor);
                ViewBag.Success = "Professor registered successfully! You can now log in.";
                return View(new ProfessorRegistrationViewModel());
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                var testEmail = "loreno.jhonray.monterde7@gmail.com";
                var testToken = "test-token-123";
                var testUrl = "http://localhost:5095/IPT102/ResetPassword?token=test&email=test@test.com";
                
                await _emailService.SendPasswordResetEmailAsync(testEmail, testToken, testUrl);
                return Content($"✅ Test email sent successfully to {testEmail}. Check your inbox!");
            }
            catch (Exception ex)
            {
                return Content($"❌ Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckProfessors()
        {
            try
            {
                var professors = await _mongoDbService.Professors
                    .Find(_ => true)
                    .ToListAsync();
                
                var info = new System.Text.StringBuilder();
                info.AppendLine($"Total Professors: {professors.Count}\n");
                
                foreach (var prof in professors)
                {
                    info.AppendLine($"Username: {prof.Username}");
                    info.AppendLine($"Name: {prof.Name}");
                    info.AppendLine($"Email: {prof.Email ?? "(no email)"}");
                    info.AppendLine($"---");
                }
                
                return Content(info.ToString());
            }
            catch (Exception ex)
            {
                return Content($"❌ Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Try case-insensitive email lookup
                var allProfessors = await _mongoDbService.Professors
                    .Find(_ => true)
                    .ToListAsync();
                
                var professor = allProfessors
                    .FirstOrDefault(u => u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));

                if (professor == null)
                {
                    // Log for debugging
                    System.Diagnostics.Debug.WriteLine($"ForgotPassword: No professor found with email: {model.Email}");
                    System.Diagnostics.Debug.WriteLine($"Available emails in database: {string.Join(", ", allProfessors.Select(p => p.Email))}");
                    
                    // Don't reveal if email exists for security
                    ViewBag.Success = "If an account with that email exists, a password reset link has been sent.";
                    return View(new ForgotPasswordViewModel());
                }

                System.Diagnostics.Debug.WriteLine($"ForgotPassword: Found professor with email: {professor.Email}, Username: {professor.Username}");

                // Generate reset token
                var resetToken = Guid.NewGuid().ToString() + DateTime.UtcNow.Ticks.ToString();
                var tokenExpiry = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

                // Save token to database
                var update = Builders<User>.Update
                    .Set(u => u.PasswordResetToken, resetToken)
                    .Set(u => u.PasswordResetTokenExpiry, tokenExpiry);

                await _mongoDbService.Professors.UpdateOneAsync(
                    u => u.Id == professor.Id,
                    update
                );

                // Generate reset URL
                var request = _httpContextAccessor.HttpContext?.Request;
                var baseUrl = $"{request?.Scheme}://{request?.Host}";
                var resetUrl = $"{baseUrl}/IPT102/ResetPassword?token={resetToken}&email={Uri.EscapeDataString(model.Email)}";

                try
                {
                    // Send email
                    System.Diagnostics.Debug.WriteLine($"ForgotPassword: Attempting to send email to: {professor.Email}");
                    await _emailService.SendPasswordResetEmailAsync(professor.Email, resetToken, resetUrl);
                    System.Diagnostics.Debug.WriteLine($"ForgotPassword: Email sent successfully to: {professor.Email}");
                    ViewBag.Success = "If an account with that email exists, a password reset link has been sent to your email.";
                }
                catch (Exception ex)
                {
                    // Log the full error for debugging
                    System.Diagnostics.Debug.WriteLine($"Email Error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                    
                    ViewBag.Error = $"Failed to send email: {ex.Message}. Please check your email settings in appsettings.json and ensure you're using an App Password for Gmail.";
                }

                return View(new ForgotPasswordViewModel());
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token, string email)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Invalid reset link. Please request a new password reset.";
                return View(new ResetPasswordViewModel());
            }

            var professor = await _mongoDbService.Professors
                .Find(u => u.Email == email && u.PasswordResetToken == token)
                .FirstOrDefaultAsync();

            if (professor == null || professor.PasswordResetTokenExpiry == null || professor.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "Invalid or expired reset token. Please request a new password reset.";
                return View(new ResetPasswordViewModel { Token = token, Email = email });
            }

            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var professor = await _mongoDbService.Professors
                    .Find(u => u.Email == model.Email && u.PasswordResetToken == model.Token)
                    .FirstOrDefaultAsync();

                if (professor == null || professor.PasswordResetTokenExpiry == null || professor.PasswordResetTokenExpiry < DateTime.UtcNow)
                {
                    ViewBag.Error = "Invalid or expired reset token. Please request a new password reset.";
                    return View(model);
                }

                // Update password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                var update = Builders<User>.Update
                    .Set(u => u.PasswordHash, hashedPassword)
                    .Set(u => u.PasswordResetToken, (string?)null)
                    .Set(u => u.PasswordResetTokenExpiry, (DateTime?)null);

                await _mongoDbService.Professors.UpdateOneAsync(
                    u => u.Id == professor.Id,
                    update
                );

                ViewBag.Success = "Your password has been reset successfully. You can now log in with your new password.";
                return View(new ResetPasswordViewModel());
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult TakeAttendance(string section, string date = null, string message = null, string error = null)
        {
            if (!IsUserLoggedIn()) return RedirectToAction("LoginPage");
            if (string.IsNullOrWhiteSpace(section)) return RedirectToAction("ProfSchedule");

            DateOnly selected = DateOnly.FromDateTime(DateTime.Today);
            if (DateTime.TryParse(date, out var dt)) selected = DateOnly.FromDateTime(dt);

            ViewBag.Section = section;
            ViewBag.Date = selected.ToString("yyyy-MM-dd");
            ViewBag.Message = message;
            ViewBag.Error = error;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeAttendancePost(string section, string date, string studentNumber, string subject = null)
        {
            if (!IsUserLoggedIn()) return RedirectToAction("LoginPage");
            if (string.IsNullOrWhiteSpace(section)) return RedirectToAction("ProfSchedule");

            if (!DateTime.TryParse(date, out var dt))
                dt = DateTime.Today;
            var selectedDate = DateOnly.FromDateTime(dt);

            // Get the logged-in professor's username
            var professorUsername = HttpContext.Session.GetString("Username");
            
            // Get subject from schedule if not provided
            if (string.IsNullOrWhiteSpace(subject))
            {
                var scheduleFilter = Builders<ClassSchedule>.Filter.Eq(s => s.Section, section);
                if (!string.IsNullOrWhiteSpace(professorUsername))
                {
                    scheduleFilter = Builders<ClassSchedule>.Filter.And(
                        scheduleFilter,
                        Builders<ClassSchedule>.Filter.Eq(s => s.ProfessorUsername, professorUsername)
                    );
                }
                
                var schedule = await _mongoDbService.ClassSchedules
                    .Find(scheduleFilter)
                    .FirstOrDefaultAsync();
                if (schedule != null)
                    subject = schedule.Subject;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                return RedirectToAction("TakeAttendance", new
                {
                    section,
                    date = selectedDate.ToString("yyyy-MM-dd"),
                    error = "Subject is required for attendance tracking."
                });
            }

            var student = await _mongoDbService.Students
                .Find(s => s.Section == section && s.StudentNumber == studentNumber)
                .FirstOrDefaultAsync();

            if (student == null)
            {
                return RedirectToAction("TakeAttendance", new
                {
                    section,
                    date = selectedDate.ToString("yyyy-MM-dd"),
                    error = "Student number not found for this section."
                });
            }

            var currentSemester = await _semesterService.GetCurrentSemesterAsync();
            var filter = Builders<AttendanceRecord>.Filter.Where(r =>
                r.Section == section &&
                r.Subject == subject &&
                r.StudentId == student.Id &&
                r.Semester == currentSemester &&
                r.Date == selectedDate.ToDateTime(TimeOnly.MinValue));

            var update = Builders<AttendanceRecord>.Update
                .Set(r => r.StudentId, student.Id)
                .Set(r => r.Section, section)
                .Set(r => r.Subject, subject)
                .Set(r => r.Semester, currentSemester)
                .Set(r => r.Date, selectedDate.ToDateTime(TimeOnly.MinValue))
                .Set(r => r.Status, "Present")
                .SetOnInsert(r => r.CreatedAt, DateTime.UtcNow);

            await _mongoDbService.AttendanceRecords.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

            return RedirectToAction("TakeAttendance", new
            {
                section,
                date = selectedDate.ToString("yyyy-MM-dd"),
                message = $"Marked Present: {student.StudentNumber} - {student.FullName}"
            });
        }
        [HttpGet]
public IActionResult StudentRegistrationWithSections()
{
    return View("StudentRegistration");
}
[HttpGet]
public async Task<IActionResult> GetSections()
{
    var sections = await _mongoDbService.ClassSchedules
        .Find(_ => true)
        .Project(s => s.Section)
        .ToListAsync();
    
    var uniqueSections = sections.Distinct().OrderBy(s => s).ToList();
    
    return Json(uniqueSections);
}
        [HttpPost]
        public async Task<IActionResult> UploadMasterList(string section, string subject, IFormFile excelFile)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("LoginPage");

            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel file.";
                return RedirectToAction("Attendance", new { section, subject });
            }

            var studentsToAdd = new List<Student>();
            try
            {
                using (var stream = new MemoryStream())
                {
                     await excelFile.CopyToAsync(stream);
                    OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

    using (var package = new OfficeOpenXml.ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var studNo = worksheet.Cells[row, 1]?.Text?.Trim() ?? string.Empty;
                            var lName = worksheet.Cells[row, 2]?.Text?.Trim() ?? string.Empty;
                            var fName = worksheet.Cells[row, 3]?.Text?.Trim() ?? string.Empty;
                            var mName = worksheet.Cells[row, 4]?.Text?.Trim() ?? string.Empty;
                            if (string.IsNullOrEmpty(studNo) || string.IsNullOrEmpty(lName) || string.IsNullOrEmpty(fName))
                                continue;
                            studentsToAdd.Add(new Student
                            {
                                StudentNumber = studNo,
                                Section = section,
                                LastName = lName,
                                FirstName = fName,
                                MiddleName = mName,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                if (studentsToAdd.Count > 0)
                {
                    // Optionally, check for duplicate StudentNumbers here before inserting
                    await _mongoDbService.Students.InsertManyAsync(studentsToAdd);
                    TempData["Success"] = $"Uploaded {studentsToAdd.Count} students.";
                }
                else {
                    TempData["Error"] = "No valid students found in the file.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error reading Excel file: " + ex.Message;
            }
            return RedirectToAction("Attendance", new { section, subject });
        }
        [HttpGet]
public async Task<IActionResult> StudentDashboard(string subject = null)
{
    var studentId = HttpContext.Session.GetString("StudentId");
    if (string.IsNullOrEmpty(studentId))
        return RedirectToAction("LoginPage");
    var student = await _mongoDbService.Students.Find(s => s.Id == studentId).FirstOrDefaultAsync();
    if (student == null)
        return RedirectToAction("LoginPage");
    // Get unique subjects filtered by student's section
    var sectionFilter = Builders<ClassSchedule>.Filter.Eq(cs => cs.Section, student.Section);
    var subjects = await _mongoDbService.ClassSchedules.Distinct<string>("Subject", sectionFilter).ToListAsync();
    ViewBag.Subjects = subjects;
    ViewBag.Student = student;
    ViewBag.StudentSection = student.Section;
    
    // If no subject selected and subjects exist, use the first one
    if (string.IsNullOrWhiteSpace(subject) && subjects.Any())
    {
        subject = subjects.First();
    }
    ViewBag.SelectedSubject = subject;
    
    // Get professor name for the selected subject
    string professorName = "N/A";
    if (!string.IsNullOrWhiteSpace(subject))
    {
        var schedule = await _mongoDbService.ClassSchedules
            .Find(cs => cs.Section == student.Section && cs.Subject == subject)
            .FirstOrDefaultAsync();
        
        if (schedule != null && !string.IsNullOrWhiteSpace(schedule.ProfessorUsername))
        {
            var professor = await _mongoDbService.Professors
                .Find(u => u.Username == schedule.ProfessorUsername)
                .FirstOrDefaultAsync();
            
            professorName = professor != null && !string.IsNullOrWhiteSpace(professor.Name) 
                ? professor.Name 
                : schedule.ProfessorUsername;
        }
    }
    ViewBag.ProfessorName = professorName;
    
    // Semester label (SY.YYYY 1st/2nd Semester)
        var semCode = await _semesterService.GetCurrentSemesterAsync();
        string semLabel = semCode;
        var parts = semCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
        int semYear = DateTime.Today.Year;
        if (parts.Length == 2 && int.TryParse(parts[0], out var parsedYear))
        {
            semYear = parsedYear;
            var termLabel = parts[1] == "1" ? "1st Semester" : parts[1] == "2" ? "2nd Semester" : $"Sem {parts[1]}";
            semLabel = $"SY.{semYear} {termLabel}";
        }
    ViewBag.SemesterLabel = semLabel;

        // Build week statuses (1..18) for the student's attendance in the current semester, filtered by subject
        var weekStatuses = Enumerable.Repeat("-", 18).ToArray();
        if (parts.Length == 2 && int.TryParse(parts[1], out var term) && !string.IsNullOrWhiteSpace(subject))
        {
            var startDate = SemesterService.GetSemesterStartDate(semYear, term);
            // Handle potential duplicate student entries sharing the same student number
            var relatedStudentIds = await _mongoDbService.Students
                .Find(s => s.StudentNumber == student.StudentNumber)
                .Project(s => s.Id)
                .ToListAsync();

            // Filter attendance records by subject, semester, and student IDs
            var records = await _mongoDbService.AttendanceRecords
                .Find(r => relatedStudentIds.Contains(r.StudentId) && r.Semester == semCode && r.Subject == subject)
                .ToListAsync();

            var startDateOnly = DateOnly.FromDateTime(startDate);
            foreach (var rec in records)
            {
                var recLocal = rec.Date.Kind == DateTimeKind.Utc ? rec.Date.ToLocalTime() : rec.Date;
                var recDateOnly = DateOnly.FromDateTime(recLocal);
                int diffDays = recDateOnly.DayNumber - startDateOnly.DayNumber;
                if (diffDays < 0) continue;
                int weekIndex = (diffDays / 7) + 1; // integer division, week 1-based
                if (weekIndex >= 1 && weekIndex <= 18)
                {
                    weekStatuses[weekIndex - 1] = string.IsNullOrWhiteSpace(rec.Status) ? "-" : rec.Status;
                }
            }
        }
        ViewBag.WeekStatuses = weekStatuses;
    return View();
}
    }
}