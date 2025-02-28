using Backend.DAL;
using Backend.Services;

UserRepository userRepository = new();

Server server = new(userRepository);
await server.Start();
