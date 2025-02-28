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
    }
}