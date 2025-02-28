using Backend.Models;
using SQLite;

namespace Backend.DAL
{
    public class UserRepository
    {
        private readonly SQLiteConnection connection; public UserRepository(string dbPath)
        {
            connection = new(dbPath);
            connection.CreateTable<User>();
        }
        public User? GetUserByUDID(string UDID)
        {
            return connection.Table<User>().FirstOrDefault(u => u.UDID == UDID);
        }

        public User? UpdateBalance(int payerId, int amount, string? type)
        {
            User? user = GetUserByID(payerId);
            if (user == null) return null;

            switch (type)
            {
                case ResourceType.Coins:
                    user.Coins = amount;
                    break;
                case ResourceType.Rolls:
                    user.Rolls = amount;
                    break;
                default:
                    // not supported value, ignore
                    break;
            }

            connection.Update(user);
            return user;
        }

        private User? GetUserByID(int payerId)
        {
            return connection.Table<User>().FirstOrDefault(u => u.PayerId == payerId);
        }
    }
}