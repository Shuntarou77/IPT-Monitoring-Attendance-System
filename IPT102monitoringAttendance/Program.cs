using IPT102monitoringAttendance.Services;
using QuestPDF; // for QuestPDF.Settings
using QuestPDF.Infrastructure; // for LicenseType

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SemesterService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<EmailService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<DatabaseSeeder>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure QuestPDF license (Community)
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedDatabaseAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=IPT102}/{action=LoginPage}/{id?}");

app.Run();