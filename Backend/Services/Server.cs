using Backend.Models;
using Backend.DAL;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Backend.Services
{
    public class Server(UserRepository userRepository)
    {
        private readonly ConcurrentDictionary<int, WebSocket> socketsMap = new();
        private readonly UserRepository userRepository = userRepository;

        public void Start(WebApplication app)
        {
            app.UseWebSockets();
            app.Map("/ws", HandleWebSocket);
            app.MapGet("/", () => "WebSocket Server is running!");

            Log.Information("WebSocket server started.");
        }

        private async Task HandleWebSocket(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                Log.Information("WebSocket connection established.");
                await ProcessRequest(webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task ProcessRequest(WebSocket webSocket)
        {
            try
            {
                byte[] buffer = new byte[1024 * 4];

                WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Request? loginRequest = JsonSerializer.Deserialize<Request>(message);
                if (loginRequest == null || loginRequest.Message != RequestMessage.Login) throw new Exception("Not logged in.");

                int? payerId = await HandleLogin(loginRequest, webSocket);
                if (payerId != null)
                {
                    await ProcessWebSocketMessages((int)payerId, webSocket);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing WebSocket request.");
                await NotifySocket(webSocket, $"Error: {ex.Message}");
            }
        }
        private async Task ProcessWebSocketMessages(int payerId, WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[1024 * 4];

                WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Request request = JsonSerializer.Deserialize<Request>(message) ?? throw new Exception("Bad request format.");

                switch (request.Message)
                {
                    case RequestMessage.Login:
                        await NotifySocket(webSocket, "Already logged in.");
                        break;
                    case RequestMessage.Gift:
                        await HandleGift(payerId, request);
                        break;
                    case RequestMessage.Update:
                        await HandleUpdate(payerId, request);
                        break;
                    default:
                        Log.Warning("Unknown request type: {Message}", request.Message);
                        await NotifySocket(webSocket, "Unknown request type");
                        break;
                }
            }
        }
        private async Task<int?> HandleLogin(Request request, WebSocket webSocket)
        {
            if (request.UDID == null) throw new Exception("UDID is mandatory.");

            User user = userRepository.GetUserByUDID(request.UDID) ?? throw new Exception("User not found.");

            if (socketsMap.ContainsKey(user.PayerId)) throw new Exception("User is already connected.");

            socketsMap[user.PayerId] = webSocket;

            Log.Information("User {PayerId} logged in successfully.", user.PayerId);
            await NotifySocket(webSocket, "Login successful.");

            return user.PayerId;
        }
        private async Task HandleUpdate(int payerId, Request request)
        {
            if (request.Resource == null || request.Resource.Amount <= 0) throw new Exception("Amount must be positive.");

            User user = userRepository.UpdateBalance(payerId, request.Resource.Amount, request.Resource.Type);
            await NotifyPayer(payerId, $"New balance: rolls {user.Rolls}, coins {user.Coins}.");
        }

        private async Task HandleGift(int payerId, Request request)
        {
            if (request.RecipientId == null) throw new Exception("RecipientId is required for gifting.");
            if (request.Resource == null) throw new Exception("Resource is mandatory.");
            if (request.Resource.Amount <= 0) throw new Exception("Gift amount must be positive.");

            userRepository.TransferResource(payerId, (int)request.RecipientId, request.Resource.Amount, request.Resource.Type);

            await NotifyPayer(payerId, $"{request.Resource.Amount} {request.Resource.Type} were sent to user {request.RecipientId}.");
            await NotifyPayer((int)request.RecipientId, $"{payerId} sent you {request.Resource.Amount} {request.Resource.Type}.");
        }

        private static async Task NotifySocket(WebSocket webSocket, string message)
        {
            byte[] responseMessage = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task NotifyPayer(int payerId, string message)
        {
            if (socketsMap.TryGetValue((int)payerId, out WebSocket? webSocket))
            {
                byte[] responseMessage = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
