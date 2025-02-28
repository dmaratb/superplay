using Backend.Models;
using Backend.DAL;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Backend.Services
{
    public class Server
    {
        private readonly WebApplication app;
        private readonly ConcurrentDictionary<int, WebSocket> socketsMap = new();
        private readonly UserRepository userRepository;

        public Server(UserRepository userRepository)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            app = builder.Build();
            this.userRepository = userRepository;
        }

        public async Task Start()
        {
            app.UseWebSockets();
            app.Map("/ws", HandleWebSocket);
            app.MapGet("/", () => "WebSocket Server is running!");
            await app.RunAsync();
        }

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

        private async Task ProcessLoginRequest(WebSocket webSocket)
        {
            try
            {
                byte[] buffer = new byte[1024 * 4];

                WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Request? loginRequest = JsonSerializer.Deserialize<Request>(message);
                if (loginRequest == null || loginRequest.Message != RequestMessage.Login)
                {
                    await NotifySocket(webSocket, "Invalid login request.");
                    return;
                }

                int? payerId = await HandleLogin(loginRequest, webSocket);
                if (payerId != null)
                {
                    await ProcessWebSocketMessages((int)payerId, webSocket);
                }
            }
            catch (Exception ex)
            {
                await NotifySocket(webSocket, $"Error: {ex.Message}");
            }
        }

        private async Task<int?> HandleLogin(Request request, WebSocket webSocket)
        {
            if (request.UDID == null)
            {
                await NotifySocket(webSocket, "Missing UDID.");
                return null;
            }

            User? user = userRepository.GetUserByUDID(request.UDID);
            if (user == null)
            {
                await NotifySocket(webSocket, "User not found.");
                return null;
            }

            if (socketsMap.ContainsKey(user.PayerId))
            {
                await NotifySocket(webSocket, "User is already connected.");
                return null;
            }

            socketsMap[user.PayerId] = webSocket;
            await NotifySocket(webSocket, "Login successful.");
            return user.PayerId;
        }

        private async Task ProcessWebSocketMessages(int payerId, WebSocket webSocket)
        {
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    byte[] buffer = new byte[1024 * 4];

                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Request? request = JsonSerializer.Deserialize<Request>(message);

                    if (request == null)
                    {
                        await NotifySocket(webSocket, "Unknown request type");
                        continue;
                    }

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
                            await NotifySocket(webSocket, "Unknown request type");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await NotifySocket(webSocket, $"Error: {ex.Message}");
            }
            finally
            {
                socketsMap.TryRemove(payerId, out _);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }

        private async Task HandleUpdate(int payerId, Request request)
        {
            if (request.Resource != null && request.Resource.Amount > 0)
            {
                User? user = userRepository.UpdateBalance(payerId, request.Resource.Amount, request.Resource.Type);
                if (user == null)
                {
                    await NotifyPayer(payerId, "User not found.");
                    return;
                }

                await NotifyPayer(payerId, $"New balance: rolls {user.Rolls}, coins {user.Coins}.");
            }
            else
            {
                await NotifyPayer(payerId, "Amount to add must be positive.");
            }
        }

        private async Task HandleGift(int payerId, Request request)
        {
            if (string.IsNullOrEmpty(request.RecipientId))
            {
                await NotifyPayer(payerId, "RecipientId is required for gifting.");
            }
            else if (request.Amount <= 0)
            {
                await NotifyPayer(payerId, "Gift amount must be positive.");
            }
            else
            {
                await NotifyPayer(payerId, $"Gifted {request.Amount} to user {request.RecipientId}.");
            }
        }

        private async Task NotifySocket(WebSocket webSocket, string message)
        {
            byte[] responseMessage = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task NotifyPayer(int payerId, string message)
        {
            if (socketsMap.TryGetValue(payerId, out WebSocket? webSocket))
            {
                byte[] responseMessage = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
