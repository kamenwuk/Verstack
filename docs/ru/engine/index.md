# Engine

Движок Verstack. Четыре проекта под капотом сервера: ECS, сетевой транспорт, развязка Network↔ECS и композиция сервера.
Ни один из них не знает про Minecraft-фазы — это забота [слоёв](../layers/index.md).

```text
Verstack.Engine.Ecs       ← вендор Leopotam.EcsProto
Verstack.Engine.Network   ← транспорт (сокеты, фрейминг, компрессия)   → см. network.md
Verstack.Engine.Bridge    ← развязка Network↔ECS (роутинг каналов)      → см. bridge.md
Verstack.Engine.Lifecycle ← композиция сервера, тик-луп (этот файл)
```

## Engine.Ecs

Завендоренный `Leopotam.EcsProto` + QoL (`src/engine/Verstack.Engine.Ecs/`). DOD, GC-free, `ref struct`-итераторы без
`IEnumerable`, аспекты (`ProtoAspectInject`), пулы (`ProtoPool<T>`), DI (`AutoInjectModule`, `[DI]`). Не потокобезопасен —
синхронизация между потоками делается системами и очередями. 0 NuGet, только BCL. Лицензия MIT-ZARYA (см. README).

Вендор, а не NuGet — чтобы держать единый стиль и конвенции проекта; у EcsProto нет официального пакета. Код вендора
правится минимально; конвенции EcsProto (русские тексты исключений, `#if DEBUG`) сохраняются.

## Engine.Network

Транспорт: TCP/сокеты, фрейминг, компрессия. Не знает про Minecraft-фазы — только байты и кадры. Полный разбор — в
[network.md](network.md). Для этого файла важна одна точка стыка с Bridge (ниже).

## Engine.Bridge

Развязка асинхронного сетевого слоя и синхронного ECS-тика: маршрутизация каналов между слоями, конечный автомат игрока
(`Pending → Connected → Disconnected`). Полный разбор — в [bridge.md](bridge.md).

## Engine.Lifecycle

Композиция сервера и главный цикл. Содержит:

- `ServerFeatureLayer` — абстрактная точка расширения; каждый слой (Global/Gateway/Realm) — её наследник.
- `ServerComposer` — собирает из `ServerFeatureLayer`'ов массив `ProtoSystems` (по одному на мир).
- `EntryPoint` — главный тик-луп (20 TPS).
- `ServerTime` — `DeltaTime`/`TotalTime` на `Stopwatch`, без накопления дрейфа.
- `ServerConstants` — TPS, длительность тика, порог компрессии.
- `ServerWorldScopes` — строковые константы имён миров (`GLOBAL`/`GATEWAY`/`REALM`).

### ServerFeatureLayer

Абстрактный класс, который реализует каждый слой. Через него Composer получает всё, что нужно для сборки мира:

| Член | Назначение |
|------|------------|
| `Scope` | Имя ECS-мира слоя (из `ServerWorldScopes`) |
| `Init(IProtoSystems)` | Финальная инициализация после сборки мира |
| `GetCacheStores()` | Аспекты слоя (`ProtoAspectInject[]`) — кэши с пулами компонентов и фильтрами |
| `GetVisibleScopes(...)` | Скоупы чужих миров, которые слой хочет видеть (для чтения их данных) |
| `GetNextScope()` | Скоуп слоя, куда этот передаёт игрока по хэндоффу (или пусто, если некуда) |
| `GetHandoffPolicy()` | Политика перехода для Bridge (или `null`, если слой не передаёт дальше) |

Как автор слоя реализует этот класс (свои бандлы, системы, кэши) — в [layers/index.md](../layers/index.md). Здесь важен
механизм: Composer опрашивает эти методы при сборке.

### ServerComposer.Compose

Сборка идёт в три фазы. Порядок слоёв фиксируется на входе: Global всегда первый, далее Gateway, Realm.

**Фаза 1 — создание мира Global.** Global строится отдельно: ему в `BuildSystems` передаётся `NetworkHubModule` —
модуль, который регистрирует `TcpNetworkService` (`internal sealed`, спрятан внутри модуля) и компрессоры
(`IPacketCompressor`/`IPacketDecompressor`) как сервисы. После сборки эти сервисы попадают в общий список и через
`[DI(ServerWorldScopes.GLOBAL)]` становятся видны системам всех последующих миров. Global — единственный слой, через
который Network заходит в композицию: он выступает точкой регистрации сервисов, а не «владельцем сокетов».

**Фаза 2 — создание миров Gateway и Realm.** Каждому вместо `NetworkHubModule` передаётся `BridgeLayerModule(scope,
nextScope, handoffRouter, handoffPolicy)` — модуль Bridge, ставящий в мир четыре системы в фиксированном порядке (см.
[bridge.md](bridge.md)). Так слой получает доступ к Bridge, но не к `TcpNetworkService` напрямую: единственное, что
доходит до игровых систем от Network, — сервисы компрессии.

**Фаза 3 — настройка видимости.** Для каждого слоя Composer опрашивает `GetVisibleScopes` и регистрирует запрошенные
чужие миры через `sys.AddWorld(foreignWorld, scope)`. Свой мир уже зарегистрирован под именем `Scope`. Если слой просит
мир, которого нет среди зарегистрированных, — исключение. Этим и обеспечивается плоская видимость: Gateway и Realm
никогда не просят миры друг друга, только `GLOBAL`.

**Фаза 4 — инициализация.** На каждом собранном `ProtoSystems` вызывается `layer.Init(systems)` — финальная доводка
слоя после того, как миры и видимость готовы.

### Точка стыка Network ↔ Bridge

Network и Bridge развязаны через абстракцию `ClientLifecycleHandler` (в `Engine.Network`): два метода —
`HandleConnect(channel)` и `HandleDisconnect(channel)`. `TcpNetworkService` дёргает их из своих фоновых потоков при
accept'е сокета и при обрыве.

Единственная реализация — `BridgeHandoffRouter` (в `Engine.Bridge`). Один экземпляр на сервер, передаётся и в
`NetworkHubModule` (как handler), и в каждый `BridgeLayerModule` (как роутер). Так Network не знает про Bridge и ECS —
он знает только интерфейс handler'а; а Bridge получает события жизненного цикла канала без прямого доступа к
`TcpNetworkService`. Детали роутинга — в [bridge.md](bridge.md).
