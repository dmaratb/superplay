using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
Server server = new(app);
await server.Start();

class Server(WebApplication webApp)
{
    private readonly WebApplication webApp = webApp;

    internal async Task Start()
    {
        this.webApp.UseWebSockets();

        this.webApp.MapGet("/", () => "WebSocket Server is running!");

        this.webApp.Use(ProcessRequest);

        await this.webApp.RunAsync();
    }



    private async Task ProcessRequest(HttpContext context, RequestDelegate next)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync(); while (webSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[1024 * 4];
                var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine(message);
                }
                else
                {
                    Console.WriteLine("not text");
                }
            }
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}