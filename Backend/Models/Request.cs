namespace Backend.Models
{
    public static class RequestMessage
    {
        public const string Login = "Login";
        public const string Update = "Update";
        public const string Gift = "Gift";
    }

    public static class ResourceType
    {
        public const string Coins = "Coins";
        public const string Rolls = "Rolls";
    }
    public class Resource
    {
        public int Amount { get; set; }
        public string? Type { get; set; }
    }
    public class Request
    {
        public int Amount { get; set; }
        public string? Message { get; set; }
        public string? RecipientId { get; set; }
        public Resource? Resource { get; set; }
        public string? UDID { get; set; }
    }
}
