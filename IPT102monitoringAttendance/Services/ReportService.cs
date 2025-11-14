using IPT102monitoringAttendance.Models;
using MongoDB.Driver;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IPT102monitoringAttendance.Services
{
    public class ReportService
    {
        private readonly MongoDbService _db;

        public ReportService(MongoDbService db)
        {
            _db = db;
        }

        public async Task<byte[]> GenerateSectionSemesterReportPdfAsync(string semester, string section)
        {
            // Load students for section
            var students = await _db.Students.Find(s => s.Section == section).ToListAsync();

            // Load attendance for semester & section
            var attendance = await _db.AttendanceRecords
                .Find(r => r.Section == section && r.Semester == semester)
                .ToListAsync();

            // Aggregate per student
            var rows = students
                .Select(s =>
                {
                    var sRecs = attendance.Where(a => a.StudentId == s.Id).ToList();
                    int present = sRecs.Count(a => a.Status == "Present");
                    int absent = sRecs.Count(a => a.Status == "Absent");
                    int late = sRecs.Count(a => a.Status == "Late");
                    int total = present + absent + late;
                    double rate = total == 0 ? 0 : (double)present / total * 100.0;
                    return new
                    {
                        StudentNumber = s.StudentNumber,
                        Name = s.FullName,
                        Present = present,
                        Absent = absent,
                        Late = late,
                        Rate = rate
                    };
                })
                .OrderBy(r => r.Name)
                .ToList();

            byte[] pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));
                    page.Header()
                        .Text($"Attendance Report - {section} - {semester}")
                        .SemiBold().FontSize(16).AlignCenter();

                    page.Content().Element(e =>
                    {
                        e.Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2); // Student Number
                                cols.RelativeColumn(4); // Name
                                cols.RelativeColumn(1); // Present
                                cols.RelativeColumn(1); // Absent
                                cols.RelativeColumn(1); // Late
                                cols.RelativeColumn(2); // Rate
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeader).Text("Student No.");
                                header.Cell().Element(CellHeader).Text("Name");
                                header.Cell().Element(CellHeader).Text("Present");
                                header.Cell().Element(CellHeader).Text("Absent");
                                header.Cell().Element(CellHeader).Text("Late");
                                header.Cell().Element(CellHeader).Text("Rate %");
                            });

                            foreach (var r in rows)
                            {
                                table.Cell().Element(CellContent).Text(r.StudentNumber);
                                table.Cell().Element(CellContent).Text(r.Name);
                                table.Cell().Element(CellContent).Text(r.Present.ToString());
                                table.Cell().Element(CellContent).Text(r.Absent.ToString());
                                table.Cell().Element(CellContent).Text(r.Late.ToString());
                                table.Cell().Element(CellContent).Text(r.Rate.ToString("0.0"));
                            }
                        });

                        static IContainer CellHeader(IContainer c) => c.Background(Colors.Grey.Lighten3).Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                        static IContainer CellContent(IContainer c) => c.Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Generated ");
                        txt.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    });
                });
            }).GeneratePdf();

            return pdf;
        }
    }
}


