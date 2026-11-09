# Test Builder — Handoff

Дата подготовки: 2026-11-09  
Корень решения: каталог, в котором лежат `TestBuilder.sln`, `README.md` и эта папка проектов.

## Статус

Проект реализован полностью, все 8 этапов завершены.

- Solution собирается на .NET 8: **0 ошибок, 0 предупреждений**.
- Кроссплатформенные тесты: `net8.0` — **66/66**.
- Windows-тесты с UI smoke: `net8.0-windows` — **77/77**.
- UI запускается и открывает главное окно.
- CLI отрабатывает samples и возвращает корректный ExitCode.
- `sample-simple-http`, `sample-with-wait` и `sample-json-variable` проверены через CLI: **exit 0**.
- `sample-json-variable` проверяет сохранение JSON запроса и вставку `{$requestJson...}` в следующий запрос.
- `sample-elma365` загружается и проходит round-trip, но содержит плейсхолдеры и требует реального стенда Elma365.

## Что реализовано

### Core

- `ITestBlock`, `BlockResult`, `IBlockFactory`, `IBlockRegistry`, `BlockRegistry`.
- `TestRunner` с последовательным выполнением, прогрессом, отменой и `RunReport`.
- `TemplateEngine`: `{{var}}`, `{{var.path.to.field}}`, `ResolveDeep`.
- Полиморфная JSON-сериализация `$type` через `IBlockRegistry`.
- `TimeSpan` строкой, enum camelCase-строками.
- `CheckEvaluator` со всеми операторами проверок.
- `UndoRedoStack` и `IUndoableCommand`.
- Экспортёры JSON, HTML, JUnit XML.
- `ScenarioService` с загрузкой, атомарным сохранением и валидацией.
- `ExecutionContextFactory` с объединением переменных environment/scenario и `baseUrl`.

### Blocks

- `WaitBlock`: Fixed и UntilCondition, джиттер, отмена, MaxWait. Длительность в UI принимает `60`, `60с`, `1m`, `mm:ss`, `hh:mm:ss` и сохраняется после потери фокуса.
- `ApiRequestBlock`: методы GET/POST/PUT/DELETE (dropdown), URL/query/headers/body, JSON/FormUrlEncoded/Raw, Basic/Bearer/ApiKeyHeader, timeout, JSONPath и `$status`, сохранение ответа.
- Автоматический `Authorization` для Basic/Bearer; отдельный header не нужен.
- Bearer-токен нормализуется: можно вставлять значение с префиксом `Bearer` или без него.
- `SaveRequestJson` + JSON-фрагменты `{$requestJson.customer.name}`; сохранение после отправки, автодополнение всех полей (string/number/bool/object/array) в Body/Filter по JSON предыдущих API-блоков.
- `SaveResponseVariable` и `SaveObjectVariable` также доступны через `{$var.path}`; подсказки строятся по JSONPath-проверкам.
- Генераторы `{{random.uuid}}`, `{{random.number}}`, `{{random.number:min:max}}` + кнопки UUID/Число в multiline-редакторе.
- `CheckDataBlock`: Elma365, прямое поле токена (Secret) с fallback на переменную, поиск по ID (`GET /{ns}/{code}/{id}/get`) или по фильтру (`POST /{ns}/{code}/list`), проверки полей, сохранение объекта.
- Все три фабрики зарегистрированы в `AddBuiltInBlocks`.


### Elma365

- `Elma365Client` с `Bearer`, retry на 429/503, backoff 200/500/1500 мс.
- Исключения: `Elma365Exception`, `Elma365AuthException`, `Elma365NotFoundException`, `Elma365ServerException`.
- `GetObjectAsync`, `FindObjectAsync`, `GetObjectTypesAsync`, `GetObjectSchemaAsync`.
- `FindObjectAsync` проверяет JSON фильтра до запроса и считает `result.total = 0` / пустой `result.result` ошибкой «не найден».

### UI

- `MainForm`: toolbar, палитра, ListView сценария, инспектор, журнал.
- Add/Remove/Move/Duplicate, Undo/Redo.
- New/Open/Save, dirty-state, подтверждение закрытия.
- Run/Stop, прогресс, подсветка статусов, лог.
- Export JUnit/JSON/HTML.
- `PropertyInspectorControl` для Auto/Multiline/Secret/KeyValue/JsonPathCheck.
- DI, Serilog, `MainForm` через `ServiceProvider`.

### Runner

- `System.CommandLine`: `--scenario`, `--output`, `--format`, `--environment`, `--verbose`.
- ExitCode: 0 — успех, 1 — провал шагов, 2 — ошибка конфигурации/запуска.
- Прогресс в консоль, экспорт отчёта.
- `--environment` пока принимается, но не переключает окружение.

### Samples

```text
samples/sample-simple-http.tbtest
samples/sample-with-wait.tbtest
samples/sample-json-variable.tbtest
samples/sample-elma365.tbtest
```

### CI

```text
.github/workflows/build.yml
```

- Ubuntu: restore/build/test `TestBuilder.Tests` для `net8.0`.
- Windows: restore/build solution и тесты `net8.0-windows`.

## Быстрая проверка на новом устройстве

Требования:

- Windows 10/11 для UI.
- .NET 8 SDK.
- Для samples с `httpbin.org` — доступ в интернет.

```powershell
dotnet restore TestBuilder.sln
dotnet build TestBuilder.sln -c Release

dotnet test .\TestBuilder.Tests\TestBuilder.Tests.csproj -c Release -f net8.0
dotnet test .\TestBuilder.Tests\TestBuilder.Tests.csproj -c Release -f net8.0-windows

dotnet run --project .\TestBuilder.UI\TestBuilder.UI.csproj
```

CLI:

```powershell
dotnet .\TestBuilder.Runner\bin\Release\net8.0\TestBuilder.Runner.dll `
  --scenario .\samples\sample-simple-http.tbtest `
  --output .\artifacts\result.xml `
  --format junit `
  --verbose
```

## Портативная self-contained сборка

Готовый комплект: `TestBuilder-standalone-win-x64.zip`.

- UI: `TestBuilder.UI.exe` (~69 МБ) — single-file, self-contained.
- Runner: `TestBuilder.Runner.exe` (~34 МБ) — single-file, self-contained.
- Запуск UI: `start-ui.cmd` (задаёт локальные TEMP/bundle) или напрямую `TestBuilder.UI.exe`.
- Запуск CLI: `run-runner.cmd --scenario samples\sample-simple-http.tbtest --output result.xml --format junit`.
- .NET 8 на целевой машине **не требуется**; нужна Windows x64.
- Проверено запуском с отключёнными `DOTNET_ROOT`/`DOTNET_MULTILEVEL_LOOKUP`.

Профили повторной сборки:

```text
TestBuilder.UI/Properties/PublishProfiles/WinX64SelfContained.pubxml
TestBuilder.Runner/Properties/PublishProfiles/WinX64SelfContained.pubxml
```

## Важные особенности окружения

1. **NuGet-источники.** Предупреждения `NU1900` могут появляться из-за корпоративных источников в `%APPDATA%\NuGet\NuGet.Config`, даже если проект использует только nuget.org. Это не ошибка сборки. В проекте намеренно нет `NuGet.config`, перекрывающего пользовательские источники.
2. **testhost в sandbox.** В `TestBuilder.Tests/runtimeconfig.template.json` отключён parent-process watchdog. Это обход `Win32Exception (5)` при запуске testhost в ограниченных окружениях. Не удаляйте этот файл без необходимости.
3. **UI и Windows.** `TestBuilder.UI` и WinForms smoke-тест запускаются только на Windows.
4. **Сеть.** `sample-simple-http`, `sample-with-wait` и `sample-json-variable` обращаются к `https://httpbin.org`.
5. **Секреты.** Не коммитьте реальные токены в `.tbtest`; используйте переменные окружения/локальные значения.

## Ограничения текущей версии

- Нет ветвлений и условий между шагами.
- Нет параллельного выполнения.
- Drag&drop не поддерживается осознанно.
- `--environment` в CLI пока не переключает окружение.
- В UI нет редактора переменных environment/scenario; они задаются в `.tbtest`.
- `sample-elma365` — только заготовка, требует реального Elma365 и токена.
- UI-интеграция загрузки схемы Elma365 не завершена; клиентский метод `GetObjectSchemaAsync` уже есть.

## Как продолжить в новом чате DSH

1. Перенесите проект на новое устройство.
2. Откройте каталог проекта как workspace в DSH.
3. Приложите этот `HANDOFF.md`.
4. При необходимости приложите Markdown-экспорт прошлой сессии:
   - в DSH: Session Header → ⋯ → **Copy Session as Markdown**;
   - или `/export` → ZIP с логом сессии.
5. Начните новый диалог с фразы:

```text
Продолжаем проект Test Builder. Прочитай HANDOFF.md и README.md,
проверь dotnet build/test. Следующая задача: <...>.
```

Официального импорта живого DSH-диалога из ZIP нет: ZIP — это архив лога. Не копируйте вручную `%USERPROFILE%\.dsh\sessions\*.zstd` без необходимости: формат внутренний и завязан на workspace.

## Полезные ссылки

- `README.md` — описание, формат, CLI, CI.
- `.github/workflows/build.yml` — CI.
- `samples/` — примеры сценариев.
- `TestBuilder.Tests/` — тесты.
