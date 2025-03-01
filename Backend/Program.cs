using Backend.DAL;
using Backend.Services;
using SQLite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// initializations
string connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString") ?? "main.db";
SQLiteConnection sqlConnection = new(connectionString);

UserRepository userRepository = new(sqlConnection);

Server server = new(userRepository);

WebApplication app = builder.Build();

// start server
try
{
    server.Start(app);
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while starting the server: {ex.Message}");
}

app.Run();