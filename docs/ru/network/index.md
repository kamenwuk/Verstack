# Слой Network

Network — это насос байт: он принимает TCP-соединения, превращает входящий поток байт в обрамлённые payload'ы Minecraft, передаёт каждый payload в вышележащий слой и пишет обрамлённые ответы обратно. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`).

Работу делают четыре типа. `TcpServer` владеет слушающим сокетом и accept-циклом. `SessionLifetime` ведёт одно соединение через цикл чтения, фрейминг, диспетчеризацию и flush. `IPacketHandler` — контракт, по которому разобранный кадр уходит в прикладной слой. `IPacketHandlerFactory` создаёт свежий handler на каждое соединение — чтобы у каждого было своё per-connection состояние. Оба контракта определены здесь, реализованы в Minecraft, связаны в App.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

→ [Жизнь соединения](server-lifetime.md) — accept-цикл TcpServer, read-цикл SessionLifetime, backpressure, точка flush'а.
→ [Packet handler](packet-handler.md) — шов IPacketHandler.
