# Gateway

GATEWAY-мир — входной слой. Обрабатывает Handshake и проводит подключение через Status, Login и Configuration до точки
передачи игрока в [Realm](realm.md). Не видит Realm напрямую — хэндофф идёт через [Bridge](../engine/bridge.md).

Реализация — `GatewayLayer : ServerFeatureLayer` (`Scope = GATEWAY`): видит `GLOBAL`, следующий слой — `REALM`,
политика — `GatewayHandoffPolicy`. Две игровые системы: `GuestScreeningSystem` (приём Handshake) и
`PacketDispatchSystem` (конвейер бандлов). Кэш-стор — `GatewayCacheStore`.

## Сценарий: от сокета до Realm

Сценарий линейный — `SequentialPacketPipeline` из 7 бандлов. Состояние хранится в `PacketFlowState` (в
`GatewayCacheStore`), позиция инициализируется в `GuestScreeningSystem` после разбора Handshake.

```text
accept → [Bridge: Pending → Connected]
   ↓
GuestScreeningSystem: первый пакет (Handshake 0x00)
   ├─ nextState=1 → Status   (BundleIndex=0)
   └─ nextState=2 → Login    (BundleIndex=2)
   ↓
PacketDispatchSystem → SequentialPacketPipeline:

  [0] StatusExchangeBundle   Status Request 0x00  → Status Response (JSON из ServerInfo)
  [1] PingPongBundle         Ping 0x01            → Pong (тот же timestamp)
        ── Status-сценарий заканчивается, клиент уходит. ──
  [2] LoginStartBundle       Login Start 0x00     → Set Compression + Login Success
  [3] LoginAcknowledgedBundle Login Ack 0x03      → (переход в Configuration)
  [4] ClientInformationBundle Client Info 0x00    → Known Packs (minecraft:core@26.2)
  [5] KnownPacksBundle       Known Packs 0x07     → Registry Data + Update Tags + Feature Flags + Finish
  [6] ConfigurationFinishBundle Ack 0x03          → (плейсхолдер)
        ── конец конвейера → Transfer ──
   ↓
GatewayHandoffPolicy.TryTransfer: BundleIndex ≥ 6 → EnterRealmHandoffData → Bridge → Realm
```

`PacketDispatchSystem.Run` по `ConnectedFilter` прогоняет `ProcessSession`. `PipelineSessionStatus.Transfer` (конец
массива бандлов) — слой ничего не делает, дальше работает `GatewayHandoffPolicy` в `BridgeTransferSystem` следующего
тика. `Kick` — `channel.Disconnect()`, дальше Bridge снимет сущность.

## Бандлы

| Bundle | Шагов | Пакет клиента | Что делает |
|--------|-------|---------------|------------|
| `StatusExchangeBundle` | 1 | Status Request `0x00` | Отдаёт Status Response с JSON из `ServerInfoCacheStore` (Global) |
| `PingPongBundle` | 1 | Ping `0x01` | Эхо timestamp обратно |
| `LoginStartBundle` | 1 | Login Start `0x00` | Читает имя, генерирует offline-UUID (MD5, version 3), кладёт `UserProfile`, шлёт `Set Compression` + `Login Success` |
| `LoginAcknowledgedBundle` | 1 | Login Ack `0x03` | Логирует, переводит в Configuration |
| `ClientInformationBundle` | 1 | Client Info `0x00` | Читает locale в `UserProfile`, шлёт Known Packs (`minecraft:core@26.2`). `minecraft:brand` (`0x02`) — `Ignored` |
| `KnownPacksBundle` | 3 | Known Packs `0x07` | Шаг 0: Registry Data (`0x07`) по каталогу из `SyncedRegistryCatalog`. Шаг 1: Update Tags (`0x0D`) из ассетов. Шаг 2: Feature Flags (`0x0C`) + Finish Configuration (`0x03`). Использует `Continue` между шагами |
| `ConfigurationFinishBundle` | 1 | Ack `0x03` | Текущая реализация — плейсхолдер, возвращает `Kick` |

### Тонкий момент: Set Compression

`LoginStartBundle` отправляет `Set Compression` (`0x03`) **до** вызова `EnableCompression`. Это не баг: Set Compression
должен уйти несжатым (компрессия на канале ещё не включена), а следующий пакет — Login Success — уже в compressed
framing. `PacketOutbound.Commit` читает threshold канала live, поэтому смена режима применяется ровно после Set
Compression. Шифрование и Mojang-аутентификация (online mode) — вне offline-MVP.

## GatewayHandoffPolicy

Политика трансфера в Realm. `TryTransfer` возвращает `true`, когда:

1. На сущности есть `UserProfile` и `NetworkSession` (логин пройден).
2. `FlowState.BundleIndex ≥ 6` — конвейер дошёл до конца Configuration.

Собирает `EnterRealmHandoffData(profile, session)` и отдаёт Bridge. `BridgeTransferSystem` переносит ownership канала
в Realm, сокет не закрывается.

## GatewayCacheStore

`ProtoAspectInject`: пулы `NetworkSession`, `UserProfile`, `PacketFlowState` и фильтр `ActiveSessionsFilter`
(`Inc<NetworkSession, PacketFlowState>`). Фазовые данные лежат по той же сущности, что и в Bridge — связку обеспечивает
`BridgeStateCacheStore.GetChannel(entity)`.

Доступ к чужому миру Global — через `systems.NamedWorlds()[ServerWorldScopes.GLOBAL].Aspect<T>()` в `Init` бандла
(напр. `StatusExchangeBundle` берёт `ServerInfoCacheStore`, `KnownPacksBundle` — `SyncedRegistryCatalog`).
