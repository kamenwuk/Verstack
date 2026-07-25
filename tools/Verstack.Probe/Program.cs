using System.Net.Sockets;
using System.Text;

// Probe: имитирует N Minecraft-клиентов, которые держат соединения одновременно,
// чтобы проверить, что TcpServer обслуживает их параллельно (а не по одному за раз).
// Сервер должен быть уже запущен в другом терминале:
//   dotnet run --project src/Verstack.App

const string DEFAULT_HOST = "127.0.0.1";
const int DEFAULT_PORT = 25565;
const int DEFAULT_CLIENT_COUNT = 2;
const int PROTOCOL_VERSION = 772;     // Minecraft 1.21.6
const int HOLD_OPEN_MS = 1500;        // держим сокеты открытыми, чтобы в логе сервера увидеть все "Accepted"
const int READ_TIMEOUT_MS = 5000;

string host = args.Length > 0 && args[0] is { } hostArg ? hostArg : DEFAULT_HOST;
int port = args.Length > 1 && int.TryParse(args[1], out int parsedPort) ? parsedPort : DEFAULT_PORT;
int clientCount = args.Length > 2 && int.TryParse(args[2], out int parsedCount) ? parsedCount : DEFAULT_CLIENT_COUNT;

Console.WriteLine($"[Probe] Connecting {clientCount} client(s) to {host}:{port}...");

byte[] probe = BuildStatusProbe(PROTOCOL_VERSION, host, port);

// Открываем клиентов последовательно, НО не закрываем — каждый остаётся живым,
// пока открывается следующий. В блокирующем TcpServer второй connect завис бы,
// потому что accept-цикл ждал бы SessionLifetime первого до конца.
var clients = new TcpClient?[clientCount];
try
{
    for (int i = 0; i < clientCount; i++)
    {
        TcpClient c = new(host, port);
        clients[i] = c;
        c.GetStream().Write(probe);
        Console.WriteLine($"  [#{i}] connected, sent Handshake + Status Request.");
    }

    // Теперь — когда ВСЕ открыты — читаем ответы. Без конкурентности второй клиент
    // не получил бы ответа, пока не закрыт первый.
    bool allOk = true;
    for (int i = 0; i < clientCount; i++)
    {
        try
        {
            string json = ReadStatusResponse(clients[i]!.GetStream());
            string preview = json.Length > 80 ? json[..80] + "..." : json;
            Console.WriteLine($"  [#{i}] response: {preview}");
        }
        catch (Exception ex)
        {
            allOk = false;
            Console.WriteLine($"  [#{i}] FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    Console.WriteLine(allOk
        ? $"[Probe] OK: all {clientCount} client(s) answered — concurrency works."
        : "[Probe] FAIL: at least one client did not answer (check server log).");

    Console.WriteLine($"[Probe] Holding connections open for {HOLD_OPEN_MS}ms (watch server log for 'Accepted' lines)...");
    Thread.Sleep(HOLD_OPEN_MS);
}
finally
{
    foreach (TcpClient? c in clients)
    {
        c?.Dispose();
    }
}

// --- Minecraft Status probe: Handshake (0x00) + Status Request (0x00) ---
// Не зависит от Verstack.Protocol — честная имитация внешнего клиента.
static byte[] BuildStatusProbe(int protocolVersion, string host, int port)
{
    // Handshake payload: VarInt(protocol), string(host), ushort(port), VarInt(nextState=1).
    using var payload = new MemoryStream();
    WriteVarInt(payload, protocolVersion);
    WriteString(payload, host);
    WriteUShort(payload, (ushort)port);
    WriteVarInt(payload, 1);

    // Handshake packet: VarInt(packetId=0x00) + payload.
    using var handshake = new MemoryStream();
    WriteVarInt(handshake, 0x00);
    handshake.Write(payload.ToArray());

    // Status Request packet: VarInt(packetId=0x00), пустой payload.
    using var status = new MemoryStream();
    WriteVarInt(status, 0x00);

    // Оба пакета, каждый со своим VarInt-length-prefix (фрейминг Minecraft).
    using var output = new MemoryStream();
    byte[] h = handshake.ToArray();
    WriteVarInt(output, h.Length);
    output.Write(h);

    byte[] s = status.ToArray();
    WriteVarInt(output, s.Length);
    output.Write(s);

    return output.ToArray();
}

// Status Response packet: VarInt(frameLen), VarInt(packetId=0x00), VarInt(stringLen), UTF-8 JSON.
static string ReadStatusResponse(NetworkStream stream)
{
    stream.ReadTimeout = READ_TIMEOUT_MS;
    ReadVarInt(stream);                              // frameLen — не нужен, поля читаются дальше
    int packetId = ReadVarInt(stream);
    if (packetId != 0x00)
        throw new IOException($"[Probe] Unexpected Status Response packetId: 0x{packetId:X2} (expected 0x00).");
    int strLen = ReadVarInt(stream);

    byte[] buf = new byte[strLen];
    int read = 0;
    while (read < strLen)
    {
        int n = stream.Read(buf, read, strLen - read);
        if (n <= 0) throw new IOException("[Probe] Unexpected EOF reading Status Response JSON.");
        read += n;
    }
    return Encoding.UTF8.GetString(buf);
}

static void WriteVarInt(Stream s, int value)
{
    // LEB128, беззнаковая трактовка битов — как в VarInt.cs сервера.
    uint v = (uint)value;
    while (v >= 0x80)
    {
        s.WriteByte((byte)(v | 0x80));
        v >>= 7;
    }
    s.WriteByte((byte)v);
}

static void WriteString(Stream s, string str)
{
    byte[] utf8 = Encoding.UTF8.GetBytes(str);
    WriteVarInt(s, utf8.Length);
    s.Write(utf8);
}

static void WriteUShort(Stream s, ushort value)
{
    // Big-endian, как в Minecraft wire-формате.
    s.WriteByte((byte)(value >> 8));
    s.WriteByte((byte)(value & 0xFF));
}

static int ReadVarInt(Stream s)
{
    int result = 0, shift = 0;
    int b;
    do
    {
        b = s.ReadByte();
        if (b < 0) throw new IOException("[Probe] Unexpected EOF reading VarInt.");
        result |= (b & 0x7F) << shift;
        shift += 7;
    }
    while ((b & 0x80) != 0);
    return result;
}
