using ChessServer.Hubs;
using ChessServer.Services;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<GameManager>();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true) // برای dev
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = config.GetConnectionString("MySql");

        using var conn = new MySqlConnection(cs);
        conn.Open();

        Console.WriteLine("✅ MySQL connection SUCCESS");
        Console.WriteLine($"📌 Server: {conn.ServerVersion}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ MySQL connection FAILED");
        Console.WriteLine(ex.Message);
    }
}



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChessHub>("/chessHub").RequireCors("CorsPolicy");

app.Run();
