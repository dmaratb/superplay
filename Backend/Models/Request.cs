namespace Backend.Models
{
    public static class RequestType
    {
        public const string Login = "Login";
        public const string Add = "Add";
        public const string Gift = "Gift";
    }

    public class Request
    {
        public string Type { get; set; }
        public string Token { get; set; }
        public decimal Amount { get; set; }
        public string RecipientId { get; set; }
    }
}
