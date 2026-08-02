using Verstack.Layer.Gateway;
using Verstack.Layer.Global;
using Verstack.Layer.Realm;
using Verstack.Lifecycle;
using Verstack.Shared.Assets;

AssetSource.PreloadTagBatch();

// 1. Создаем точку входа
var entryPoint = new EntryPoint();

// 2. Подписываемся на закрытие консоли (Ctrl + C или крестик)
Console.CancelKeyPress += (sender, e) =>
{
    // Отменяем стандартное принудительное завершение, чтобы дать серверу сохранить данные
    e.Cancel = true;
    Console.WriteLine("Получен сигнал остановки. Завершаем работу сервера...");
    entryPoint.Stop();
};

// Для тех, кто закрывает крестиком (когда CancelKeyPress может не сработать до конца)
AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
    entryPoint.Stop();
};

// 3. Запускаем сервер на порту 25565 (стандартный порт Minecraft)
Console.WriteLine("Запуск Verstack Server...");
entryPoint.Start(25565, new GlobalLayer(), new GatewayLayer(), new RealmLayer());

// После того как entryPoint.Start() завершится (когда _isRunning станет false),
// программа сама закроется.
Console.WriteLine("Сервер остановлен.");