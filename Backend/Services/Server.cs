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
        private readonly ConcurrentDictionary<string, WebSocket> connections = new();
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

                if (loginRequest == null || loginRequest.Type != RequestType.Login)
                {
                    await SendResponse(webSocket, "Invalid login request.");
                    return;
                }

                string? payerId = await HandleLogin(loginRequest, webSocket);
                if (payerId != null)
                {
                    await ProcessWebSocketMessages(payerId, webSocket);
                }
            }
            catch (Exception ex)
            {
                await SendResponse(webSocket, $"Error: {ex.Message}");
            }
        }

        private async Task<string?> HandleLogin(Request request, WebSocket webSocket)
        {
            if (request.Token == null)
            {
                await SendResponse(webSocket, "Missing token.");
                return null;
            }

            User? user = userRepository.GetUserByToken(request.Token);
            if (user == null)
            {
                await SendResponse(webSocket, "User not found.");
                return null;
            }

            if (connections.ContainsKey(user.PayerId))
            {
                await SendResponse(webSocket, "User is already connected.");
                return null;
            }

            connections[user.PayerId] = webSocket;
            await SendResponse(webSocket, "Login successful.");
            return user.PayerId;
        }

        private async Task ProcessWebSocketMessages(string payerId, WebSocket webSocket)
        {
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    byte[] buffer = new byte[1024 * 4];

                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Request? incomingRequest = JsonSerializer.Deserialize<Request>(message);

                    if (incomingRequest == null)
                    {
                        await SendResponse(payerId, "Unknown request type");
                        continue;
                    }

                    switch (incomingRequest.Type)
                    {
                        case RequestType.Login:
                            await SendResponse(payerId, "Already logged in.");
                            break;
                        case RequestType.Add:
                            await HandleAdd(payerId, incomingRequest);
                            break;
                        case RequestType.Gift:
                            await HandleGift(payerId, incomingRequest);
                            break;
                        default:
                            await SendResponse(payerId, "Unknown request type");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await SendResponse(payerId, $"Error: {ex.Message}");
            }
            finally
            {
                connections.TryRemove(payerId, out _);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }

        private async Task HandleAdd(string payerId, Request request)
        {
            if (request.Amount <= 0)
            {
                await SendResponse(payerId, "Amount to add must be positive.");
            }
            else
            {
                await SendResponse(payerId, $"Added {request.Amount} to your account.");
            }
        }

        private async Task HandleGift(string payerId, Request request)
        {
            if (string.IsNullOrEmpty(request.RecipientId))
            {
                await SendResponse(payerId, "RecipientId is required for gifting.");
            }
            else if (request.Amount <= 0)
            {
                await SendResponse(payerId, "Gift amount must be positive.");
            }
            else
            {
                await SendResponse(payerId, $"Gifted {request.Amount} to user {request.RecipientId}.");
            }
        }

        private async Task SendResponse(WebSocket webSocket, string message)
        {
            byte[] responseMessage = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendResponse(string payerId, string message)
        {
            if (connections.TryGetValue(payerId, out WebSocket? webSocket))
            {
                byte[] responseMessage = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(responseMessage, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
