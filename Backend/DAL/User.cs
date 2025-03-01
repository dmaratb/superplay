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
        private User? GetUserByID(int payerId)
        {
            return connection.Table<User>().FirstOrDefault(u => u.PayerId == payerId);
        }
        public User? GetUserByUDID(string UDID)
        {
            return connection.Table<User>().FirstOrDefault(u => u.UDID == UDID);
        }
        public User TransferResource(int fromId, int toId, int amount, string? type)
        {
            User? fromUser = GetUserByID(fromId) ?? throw new Exception("Sender not found.");
            User? toUser = GetUserByID(toId) ?? throw new Exception("Recipient not found.");

            switch (type)
            {
                case ResourceType.Coins:
                    if (fromUser.Coins < amount) throw new Exception("Not enough coins.");

                    fromUser.Coins -= amount;
                    toUser.Coins += amount;
                    break;
                case ResourceType.Rolls:
                    if (fromUser.Rolls < amount) throw new Exception("Not enough rolls.");

                    fromUser.Rolls -= amount;
                    toUser.Rolls += amount;
                    break;
                default:
                    throw new Exception("Unsupported type.");
            }

            connection.Update(fromUser);
            connection.Update(toUser);

            return fromUser;
        }
        public User UpdateBalance(int payerId, int amount, string? type)
        {
            User user = GetUserByID(payerId) ?? throw new Exception("User not found.");

            switch (type)
            {
                case ResourceType.Coins:
                    user.Coins += amount;
                    break;
                case ResourceType.Rolls:
                    user.Rolls += amount;
                    break;
                default:
                    throw new Exception("Unsupported type.");
            }

            connection.Update(user);
            return user;
        }
    }
}