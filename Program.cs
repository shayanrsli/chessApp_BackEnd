// Program.cs
using ChessServer.Hubs;
using ChessServer.Services;
using Microsoft.AspNetCore.Cors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ اضافه کردن GameManager به عنوان Singleton
builder.Services.AddSingleton<GameManager>();

// ✅ اضافه کردن SignalR
builder.Services.AddSignalR();

// ✅ **CORS تنظیمات کامل**
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .WithOrigins(
                    "http://localhost:5173",  // Vite dev server
                    "http://localhost:3000",  // React dev server
                    "http://localhost:8080"   // Vue dev server
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowed(_ => true); // برای تست، همه origins را allow کن
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // ✅ برای توسعه، اجازه دهید همه چیز رد شود
    app.UseCors("AllowAll");
}
else
{
    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
}

app.UseAuthorization();

// ✅ **اول CORS، بعد MapHub**
app.MapControllers();
app.MapHub<ChessHub>("/chessHub").RequireCors("AllowAll"); // این خط مهم است!

app.Run();