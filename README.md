# IPT102 Attendance Monitoring System

A comprehensive web-based attendance monitoring system built with ASP.NET Core MVC for tracking student attendance in the IPT102 course. This system provides separate interfaces for professors and students, enabling efficient attendance management and tracking.

## 🎯 Features

### For Professors
- **Secure Authentication** - Login system with password reset via email
- **Class Schedule Management** - Add, edit, and delete class schedules organized by day
- **Room Conflict Detection** - Automatic validation to prevent overlapping schedules in the same room
- **Attendance Tracking** - Mark attendance with statuses: Present, Absent, Late
- **Bulk Student Registration** - Import multiple students via Excel file upload
- **PDF Report Generation** - Generate comprehensive attendance reports by section and semester
- **Semester Management** - Automatic semester detection with manual override capability
- **Quick Attendance Marking** - Fast attendance entry by student number

### For Students
- **Simple Login** - Access using surname and student number
- **Attendance Viewing** - View attendance records filtered by subject
- **Weekly Overview** - Track attendance status across 18 weeks per semester
- **Professor Information** - View assigned professor for each subject

### System Features
- **MongoDB Database** - NoSQL database for flexible data storage
- **Session-Based Authentication** - Secure session management
- **Email Notifications** - Password reset functionality via email
- **PDF Report Generation** - Professional attendance reports using QuestPDF
- **Excel File Processing** - Bulk operations using EPPlus
- **Responsive Design** - Modern web interface with Bootstrap

## 🛠️ Technology Stack

- **Framework:** ASP.NET Core MVC (.NET 8.0)
- **Database:** MongoDB
- **Authentication:** BCrypt password hashing, session management
- **Email Service:** MailKit (SMTP)
- **PDF Generation:** QuestPDF
- **Excel Processing:** EPPlus
- **Frontend:** Bootstrap, jQuery, jQuery Validation

## 📋 Prerequisites

Before running this application, ensure you have:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [MongoDB](https://www.mongodb.com/try/download/community) (local or remote instance)
- SMTP email account (Gmail recommended) for password reset functionality

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/IPT102monitoringAttendance.git
cd IPT102monitoringAttendance/IPT102monitoringAttendance
```

### 2. Configure the Application

1. Copy `appsettings.example.json` to `appsettings.json`:
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. Update `appsettings.json` with your configuration:
   - **MongoDB Connection String**: Update the MongoDB connection string if not using default localhost
   - **Email Settings**: Configure your SMTP settings for password reset functionality
     - For Gmail, you'll need to use an [App Password](https://support.google.com/accounts/answer/185833) instead of your regular password

### 3. Run MongoDB

Make sure MongoDB is running on your system:
```bash
# Windows (if installed as service, it should start automatically)
# Or start manually:
mongod

# Linux/Mac
sudo systemctl start mongod
# or
mongod
```

### 4. Run the Application

```bash
dotnet restore
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000` (check the console output for the exact URL).

The database will be automatically seeded on first startup.

### 5. Access the Application

- **Professor Login**: Use registered username and password
- **Student Login**: Use surname as username and student number as password

## 📁 Project Structure

```
IPT102monitoringAttendance/
├── Controllers/
│   └── IPT102Controller.cs          # Main controller handling all routes
├── Models/                          # Data models
│   ├── Student.cs                   # Student entity
│   ├── AttendanceRecord.cs          # Attendance tracking records
│   ├── ClassSchedule.cs             # Class schedule information
│   ├── User.cs                      # Professor/User entity
│   └── ...                          # View models and other models
├── Services/                        # Business logic services
│   ├── MongoDbService.cs            # MongoDB connection and collections
│   ├── AuthService.cs               # Authentication logic
│   ├── EmailService.cs              # Email sending functionality
│   ├── ReportService.cs             # PDF report generation
│   ├── SemesterService.cs           # Semester management
│   └── DatabaseSeeder.cs            # Database initialization
├── Views/                           # Razor views
│   └── IPT102/
│       ├── LoginPage.cshtml
│       ├── ProfSchedule.cshtml
│       ├── Attendance.cshtml
│       ├── StudentDashboard.cshtml
│       └── ...
├── wwwroot/                         # Static files (CSS, JS, images)
└── Program.cs                       # Application entry point
```

## 🗄️ Database Collections

The application uses the following MongoDB collections:

- **`professors`** - Professor user accounts with authentication information
- **`students`** - Student information (name, student number, section)
- **`attendanceRecords`** - Attendance tracking records (date, status, subject, semester)
- **`classSchedules`** - Class schedule information (day, time, section, room, subject)
- **`semesters`** - Semester settings and current semester configuration

## 🔑 Key Functionalities

### Semester Management
- Automatic semester detection based on current date
- Manual semester override capability
- Semester format: `YYYY-T` (e.g., `2025-1` for 2025 1st Semester)

### Attendance Tracking
- Per-subject, per-semester attendance tracking
- Date-based filtering and viewing
- Status options: Present, Absent, Late
- Automatic semester association

### Report Generation
- PDF reports showing attendance statistics per section
- Includes student numbers, names, and attendance rates
- Filterable by semester and section

### Bulk Operations
- Excel file upload for importing multiple students
- Expected format: Student Number, Last Name, First Name, Middle Name (in columns A-D)

### Security Features
- BCrypt password hashing
- Session-based authentication
- Secure password reset flow with email verification
- Token-based password reset (expires in 1 hour)

## 📝 Usage Examples

### Adding a Class Schedule
1. Login as a professor
2. Navigate to the schedule page
3. Select a day (Monday-Saturday)
4. Click "Add Section" and fill in:
   - Section name
   - Start time and end time
   - Room number
   - Subject name
5. The system will validate for room conflicts

### Taking Attendance
1. From the schedule page, click on a section
2. Select the date
3. Mark each student as Present, Absent, or Late
4. Click "Save Attendance"

### Generating Reports
1. Navigate to the attendance page for a section
2. Click "Download Section Report"
3. A PDF will be generated with attendance statistics

### Student View
1. Login using surname and student number
2. View attendance by subject
3. See weekly attendance status (18 weeks per semester)

## 🔒 Security Notes

- **Never commit `appsettings.json`** - It contains sensitive information
- Use environment variables or secure configuration management in production
- Ensure MongoDB is properly secured in production environments
- Use strong passwords and enable MongoDB authentication in production

## 📦 Dependencies

- **EPPlus** (7.7.3) - Excel file processing
- **MailKit** (4.14.1) - Email functionality
- **MongoDB.Driver** (2.28.0) - MongoDB database access
- **BCrypt.Net-Next** (4.0.3) - Password hashing
- **QuestPDF** (2025.7.3) - PDF generation

## 🤝 Contributing

This is a course project for IPT102. Contributions and improvements are welcome!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is created for educational/academic purposes as part of the IPT102 course.

## 👨‍💻 Author

Created for IPT102 course attendance monitoring.

## ⚠️ Important Notes

- Ensure MongoDB is running before starting the application
- Configure email settings properly for password reset functionality
- The database will be automatically seeded on first startup
- Default MongoDB connection is `mongodb://localhost:27017`
- For Gmail, use App Passwords instead of regular passwords

## 🐛 Troubleshooting

### MongoDB Connection Issues
- Ensure MongoDB service is running
- Check the connection string in `appsettings.json`
- Verify MongoDB is accessible on the specified port (default: 27017)

### Email Not Sending
- Verify SMTP settings in `appsettings.json`
- For Gmail, ensure you're using an App Password, not your regular password
- Check firewall settings for SMTP port 587

### Database Not Seeding
- Check MongoDB connection
- Review application logs for errors
- Ensure MongoDB has write permissions

---

**Note:** This is a course project for IPT102 attendance monitoring. Make sure to configure MongoDB and email settings before deployment.

