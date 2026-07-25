using System.Net;
using Verstack.Minecraft.Session;
using Verstack.Minecraft.Status;
using Verstack.Network;
using Verstack.Protocol;

Console.WriteLine("[Verstack] Server is starting...");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;  // не убиваем процесс, даём graceful shutdown
    cts.Cancel();
};

// Конфигурация сервера — здесь, в точке входа. Фабрика — чистая логика создания
// диспетчеров на каждое соединение, данные задаёт App. Позже вынесется в config-файл.
var status = new ServerStatusResponse(
    new ServerVersion("1.22.11", 774),
    new ServerCapacity(max: 99, online: 0),
    "A Minecraft Server");

// Настройка сжатия (один инстанс на весь сервер, внутри используется ArrayPool)
IPacketCompressor compressor = new ZLibPacketCompressor();
IPacketDecompressor decompressor = new ZLibPacketDecompressor();
int compressionThreshold = 256;

var factory = new PacketDispatcherFactory(status, compressor, compressionThreshold);

var endPoint = new IPEndPoint(IPAddress.Any, 25565);
// ВАЖНО: TcpServer должен уметь принимать декомпрессор.
// Если в конструкторе TcpServer сейчас нет параметра для декомпрессора, 
// скинь мне его код (TcpServer.cs), и мы добавим этот параметр!
using var server = new TcpServer(endPoint, factory, decompressor); 
server.Start();

Console.WriteLine("[Verstack] Press Ctrl+C to stop.");
await server.AcceptConnectionsAsync(cts.Token);

Console.WriteLine("[Verstack] Server stopped.");