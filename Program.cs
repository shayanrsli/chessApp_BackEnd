using ChessServer.Hubs;
using ChessServer.Services;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.UseUrls("http://localhost:5131");

// 1. SignalR با حداقل تنظیمات
builder.Services.AddSignalR();

// 2. CORS خیلی ساده
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. سایر سرویس‌ها
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<GameManager>();

var app = builder.Build();

// 4. لاگ ساده
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
    await next();
});

// 5. CORS
app.UseCors("AllowLocalhost");

// 6. ❌❌❌ خیلی مهم: کامنت کردن UseHttpsRedirection ❌❌❌
// app.UseHttpsRedirection(); // این خط رو کامنت کن یا پاک کن

app.UseAuthorization();

// 7. Swagger فقط در development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 8. Map controllers
app.MapControllers();

// 9. ❗ مهم: MapHub فقط یک بار و با مسیر درست
app.MapHub<ChessHub>("/chessHub");

// 10. endpointهای تست
app.MapGet("/", () => "Chess Server is running!");
app.MapGet("/test", () => "Test OK!");
app.MapGet("/api/ping", () => new { message = "Pong", time = DateTime.UtcNow });

// 11. ❌ حذف endpoint دستی negotiate (بذار SignalR خودش مدیریت کنه)

Console.WriteLine("========================================");
Console.WriteLine("🚀 Chess Server Started!");
Console.WriteLine("🔗 SignalR Hub: http://localhost:5131/chessHub");
Console.WriteLine("🌐 WebSocket: ws://localhost:5131/chessHub");
Console.WriteLine("📡 Test: http://localhost:5131/test");
Console.WriteLine("========================================");

app.Run();