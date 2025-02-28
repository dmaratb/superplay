namespace Backend.Models
{
    public class User
    {
        public int Coins { get; set; }
        public required string PayerId { get; set; }
        public int Rolls { get; set; }
        public required string Token { get; set; }
    }
}
