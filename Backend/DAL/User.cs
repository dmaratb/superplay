using Backend.Models;

namespace Backend.DAL
{
    public class UserRepository
    {
        private readonly Dictionary<string, User> users;

        public UserRepository()
        {
            // Simulating an in-memory user database
            users = new Dictionary<string, User>
            {
                { "user1", new User { PayerId = "user1", Token = "udid123" } },
                { "user2", new User { PayerId = "user2", Token = "udid456" } },
                { "user3", new User { PayerId = "user3", Token = "udid789" } }
            };
        }

        // Method to get user by token
        public User? GetUserByToken(string token)
        {
            return users.Values.FirstOrDefault(u => u.Token == token);
        }

        // Add more data access methods if needed
    }
}
