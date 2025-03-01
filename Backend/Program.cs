using Backend.DAL;
using Backend.Services;
using Serilog;
using SQLite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("logs/server.txt", rollingInterval: RollingInterval.Day).CreateLogger();
builder.Host.UseSerilog();

// initializations
string connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString") ?? "main.db";
SQLiteConnection sqlConnection = new(connectionString);

UserRepository userRepository = new(sqlConnection);

Server server = new(userRepository);

WebApplication app = builder.Build();

// start server
try
{
    Log.Information("Starting server...");
    server.Start(app);
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred while starting the server");
}
finally
{
    Log.CloseAndFlush();
}

app.Run();