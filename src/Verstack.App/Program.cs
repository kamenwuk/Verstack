using System.Net;
using Verstack.Network;

Console.WriteLine("[Verstack] Server is starting...");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;  // не убиваем процесс, даём graceful shutdown
    cts.Cancel();
};

var endPoint = new IPEndPoint(IPAddress.Any, 25565);
using var server = new TcpServer(endPoint);
server.Start();

Console.WriteLine("[Verstack] Press Ctrl+C to stop.");
await server.RunAsync(cts.Token);

Console.WriteLine("[Verstack] Server stopped.");