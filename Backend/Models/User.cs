using SQLite;

namespace Backend.Models
{
    [Table("Users")]
    public class User
    {
        [PrimaryKey, AutoIncrement] public int PayerId { get; set; }
        public int Coins { get; set; }
        public int Rolls { get; set; }
        public string? UDID { get; set; }
    }
}
