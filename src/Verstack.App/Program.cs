using System.Net;
using Verstack.Minecraft.Status;
using Verstack.Network;

Console.WriteLine("[Verstack] Server is starting...");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;  // не убиваем процесс, даём graceful shutdown
    cts.Cancel();
};

// Конфигурация сервера — здесь, в точке входа. Handler — чистая логика диспетчеризации,
// данные задаёт App. Позже вынесется в config-файл.
var status = new ServerStatusResponse(
    new ServerVersion("1.22.11", 774),
    new ServerCapacity(max: 99, online: 0),
    "A Minecraft Server");

var handler = new ServerStatusHandler(status);

var endPoint = new IPEndPoint(IPAddress.Any, 25565);
using var server = new TcpServer(endPoint, handler);
server.Start();

Console.WriteLine("[Verstack] Press Ctrl+C to stop.");
await server.RunAsync(cts.Token);

Console.WriteLine("[Verstack] Server stopped.");