using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

WebSocketServer server = new();
await server.Start();

public class WebSocketServer
{
    private readonly WebApplication app;
    private readonly ConcurrentDictionary<string, WebSocket> connections = new();

    public WebSocketServer()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        this.app = builder.Build();
    }

    // Start the WebSocket server and configure message handling
    public async Task Start()
    {
        app.UseWebSockets();
        app.Map("/ws", HandleWebSocket);
        app.MapGet("/", () => "WebSocket Server is running!");
        await app.RunAsync();
    }

    // Handle WebSocket connections
    private async Task HandleWebSocket(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

            await ProcessLoginRequest(webSocket);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    // Process the login request to authenticate the user
    private async Task ProcessLoginRequest(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4]; // Buffer to receive WebSocket data
        try
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // Deserialize the request
            Request? loginRequest = JsonSerializer.Deserialize<Request>(message);
            if (loginRequest == null || loginRequest.Type != RequestType.Login)
            {
                await SendResponse(webSocket, "Invalid login request.");
                return;
            }

            // Process the login logic
            string? userId = await HandleLogin(loginRequest, webSocket);
            if (userId != null)
            {
                // If login was successful, start handling WebSocket messages
                await ProcessWebSocketMessages(userId, webSocket);
            }
        }
        catch (Exception ex)
        {
            await SendResponse(webSocket, $"Error: {ex.Message}");
        }
    }

    // Handle login request (authenticate or create a session)
    private async Task<string?> HandleLogin(Request request, WebSocket webSocket)
    {
        // Simulating a database of users (replace with actual database checks)
        var users = new Dictionary<string, string>
        {
            { "user1", "udid123" },
            { "user2", "udid456" },
            { "user3", "udid789" }
        };

        // 1. Check if the user exists in the database
        var user = users.FirstOrDefault(u => u.Value == request.Token);
        if (user.Key == null)
        {
            // User not found
            await SendResponse(webSocket, "User not found.");
            return null;
        }

        // 2. Check if the user is already connected
        if (connections.ContainsKey(user.Key))
        {
            // User is already connected
            await SendResponse(webSocket, "User is already connected.");
            return null;
        }

        // 3. User is valid and not connected, so we accept the WebSocket connection
        connections[user.Key] = webSocket; // Store the connection

        // Inform the user that login was successful
        await SendResponse(webSocket, "Login successful.");
        return user.Key; // Return the userId (username or identifier)
    }

    // Process messages received from WebSocket clients
    private async Task ProcessWebSocketMessages(string userId, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4]; // Buffer to receive WebSocket data

        try
        {
            // While the WebSocket connection is open, keep reading messages
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                // Handle the message
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // Deserialize the incoming message
                    Request? incomingRequest = JsonSerializer.Deserialize<Request>(message);
                    if (incomingRequest == null)
                    {
                        await SendResponse(userId, "Unknown request type");
                        continue;
                    }

                    // Handle different types of requests
                    switch (incomingRequest.Type)
                    {
                        case RequestType.Login:
                            await SendResponse(userId, "Already logged in.");
                            break;
                        case RequestType.Add:
                            await HandleAdd(userId, incomingRequest);
                            break;
                        case RequestType.Gift:
                            await HandleGift(userId, incomingRequest);
                            break;
                        default:
                            await SendResponse(userId, "Unknown request type");
                            break;
                    }
                }
                else
                {
                    await SendResponse(userId, "Received non-text message.");
                }
            }
        }
        catch (Exception ex)
        {
            await SendResponse(userId, $"Error: {ex.Message}");
        }
        finally
        {
            // Close the WebSocket connection if something goes wrong
            connections.TryRemove(userId, out _);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        }
    }

    // Handle add funds request
    private async Task HandleAdd(string userId, Request request)
    {
        if (request.Amount <= 0)
        {
            await SendResponse(userId, "Amount to add must be positive.");
        }
        else
        {
            await SendResponse(userId, $"Added {request.Amount} to your account.");
        }
    }

    // Handle gift request (transfer funds to another user)
    private async Task HandleGift(string userId, Request request)
    {
        if (string.IsNullOrEmpty(request.RecipientId))
        {
            await SendResponse(userId, "RecipientId is required for gifting.");
        }
        else if (request.Amount <= 0)
        {
            await SendResponse(userId, "Gift amount must be positive.");
        }
        else
        {
            await SendResponse(userId, $"Gifted {request.Amount} to user {request.RecipientId}.");
        }
    }

    // Send a message back to the WebSocket client
    private async Task SendResponse(WebSocket webSocket, string message)
    {
        var responseMessage = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    // Send a message back to the WebSocket client using userId
    private async Task SendResponse(string userId, string message)
    {
        if (connections.TryGetValue(userId, out var webSocket))
        {
            var responseMessage = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

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
    public decimal Amount { get; set; }  // Amount to add or gift
    public string RecipientId { get; set; }
}
