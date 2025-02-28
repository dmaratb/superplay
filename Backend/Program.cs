using Backend.DAL;
using Backend.Services;

UserRepository userRepository = new("main.db");

Server server = new(userRepository);
await server.Start();