# Test Builder

Визуальный конструктор интеграционных тестов для Windows. Сценарий собирается из
последовательных шагов, сохраняется в файл `.tbtest` и может выполняться как из UI,
так и из консоли в CI.

> **Скриншот:** добавьте изображение в `docs/screenshot.png`.
>
> `![Test Builder UI](docs/screenshot.png)`

## Возможности

- Три типа блоков:
  - **Отправка API запроса** — метод, URL, headers, query, body, auth, timeout,
    JSONPath-проверки ответа, сохранение ответа в переменную.
  - **Проверить данные (Elma365)** — чтение элемента приложения, JSONPath-проверки
    полей, сохранение объекта в переменную.
  - **Ожидание** — фиксированная пауза с джиттером или ожидание простого условия
    (`==`, `!=`, `>`, `<`, `>=`, `<=`).
- Последовательное выполнение с отменой и прогрессом.
- Шаблоны `{{variable}}` и `{{variable.path.to.field}}`.
- Переменные окружения и сценария.
- Undo/Redo, дублирование, перемещение и удаление шагов.
- Отчёты JSON, HTML и JUnit XML.
- CLI-раннер для CI.
- Подробные Serilog-логи UI.

## Требования

- Windows 10/11 для UI.
- .NET 8 SDK.
- Для sample-сценариев с `httpbin.org` — доступ в интернет.

## Быстрый старт

```powershell
dotnet restore TestBuilder.sln
dotnet build TestBuilder.sln -c Release
dotnet run --project .\TestBuilder.UI\TestBuilder.UI.csproj
```

`dotnet run` без `--project` не работает: в корне находится solution из нескольких
проектов, и CLI не может автоматически выбрать запускаемый проект.

## Структура решения

| Проект | Назначение |
| --- | --- |
| `TestBuilder.Core` | Модель блоков, движок, шаблоны, JSON, проверки, команды, отчёты |
| `TestBuilder.Blocks` | `WaitBlock`, `ApiRequestBlock`, `CheckDataBlock` и их фабрики |
| `TestBuilder.Elma365` | HTTP-клиент Elma365 с retry и типизированными исключениями |
| `TestBuilder.UI` | WinForms UI, палитра, инспектор свойств, журнал выполнения |
| `TestBuilder.Runner` | CLI-раннер для CI на `System.CommandLine` |
| `TestBuilder.Tests` | xUnit-тесты Core/Blocks/Elma365/Runner и WinForms smoke-тест |

## Блоки

### Отправка API запроса

Поддерживает методы `GET`, `POST`, `PUT`, `DELETE` (выбор из выпадающего списка),
JSON/FormUrlEncoded/Raw body, Basic/Bearer/ApiKeyHeader auth, проверки JSONPath
и специальный путь `$status` для HTTP-статуса.

Basic/Bearer-авторизация автоматически формирует HTTP-заголовок `Authorization`;
отдельная строка в таблице Headers не требуется. Если такая строка задана вручную,
значение из полей авторизации имеет приоритет. В поле Bearer-токена можно вставлять
как чистое значение, так и строку с префиксом `Bearer ` — префикс нормализуется.

В многострочных редакторах (например, Body) доступны кнопки **UUID** и **Число**.
Они вставляют шаблоны:

```text
{{random.uuid}}              случайный UUID
{{random.number}}            случайное целое от 1 до 1 000 000
{{random.number:10:500}}     случайное целое в диапазоне [10..500]
```

Пример JSON body:

```json
{
  "requestId": "{{random.uuid}}",
  "amount": {{random.number:10:500}}
}
```

### Сохранить JSON запроса

При `BodyType=Json` доступна настройка **«Сохранить json запроса»**:

- включите её и укажите имя переменной, например `requestJson`;
- имя должно состоять из латиницы, цифр и `_`, начинаться с буквы или `_`;
- JSON-тело разрешается, проверяется и сохраняется в контекст **после отправки запроса**;
- в переменной хранятся уже подставленные значения, включая сгенерированные UUID/числа;
- переменная доступна всем последующим блокам сценария.

В теле следующего API-запроса и в поле **Фильтр** блока Elma365 можно вставлять
всю структуру или её часть:

```json
{
  "customer": {$requestJson.customer},
  "customerName": {$requestJson.customer.name}
}
```

Синтаксис: `{$variableName.path.to.field}`. Объекты и массивы вставляются как JSON,
строки — как JSON-строки в кавычках, отсутствующий путь — как `null`.

При вводе `{$` и точки в многострочном поле под редактором появляется список
доступных путей. Он строится по JSON-телам предыдущих включённых API-блоков,
у которых включено сохранение JSON. Показываются поля всех типов: строки, числа,
булевы значения, объекты и массивы. Выбор пути вставляет его в позицию курсора.

### Проверить данные (Elma365)

Токен можно ввести напрямую в поле **«Токен Elma365»** (Secret). Если оно пустое,
блок берёт токен из переменной **«Переменная с токеном»** (`elmaToken` по умолчанию).
Прямое поле имеет приоритет.

Поле **Код приложения** соответствует `{code}` в URL. Поддерживаются два режима:

- **По ID** — `GET /pub/v1/app/{namespace}/{code}/{id}/get`;
- **По фильтру** — `POST /pub/v1/app/{namespace}/{code}/list` с JSON-телом из поля
  **Фильтр**.

Если фильтр вернул несколько объектов, блок использует первый и пишет предупреждение
в лог. Найденный `JObject` можно сохранить в переменную сценария.

### Ожидание

Режимы:

- `Fixed` — пауза `Duration` плюс случайный джиттер `±JitterMs`.
- `UntilCondition` — опрос выражения до `MaxWait` с интервалом `PollInterval`.

Поля длительности принимают `60` (секунды), `60с`, `1m`, `00:01:00` или
`0.00:01:00`. В `.tbtest` значение сохраняется в формате `c`.

## Формат `.tbtest`

```json
{
  "name": "Мой сценарий",
  "environment": {
    "name": "stage",
    "variables": {
      "baseUrl": "https://example.test",
      "token": "REPLACE_ME"
    }
  },
  "variables": {
    "staticKey": "staticValue"
  },
  "blocks": [
    {
      "$type": "apiRequest",
      "id": "11111111-1111-4111-8111-111111111111",
      "title": "Создать объект",
      "enabled": true,
      "method": "POST",
      "url": "{{baseUrl}}/items",
      "bodyType": "json",
      "body": "{\"name\":\"test\"}",
      "responseChecks": [
        {
          "path": "$.id",
          "operator": "notEmpty",
          "expectedValue": ""
        }
      ],
      "saveResponseVariable": "created"
    },
    {
      "$type": "wait",
      "id": "22222222-2222-4222-8222-222222222222",
      "title": "Подождать",
      "enabled": true,
      "mode": "fixed",
      "duration": "00:00:02"
    }
  ]
}
```

Полиморфный дискриминатор — `$type`. Значения enum сериализуются camelCase-строками,
`TimeSpan` — строкой формата `c` (`hh:mm:ss` или `d.hh:mm:ss`).

### Переменные

1. Сначала добавляются переменные `environment.variables`.
2. Затем их переопределяют `variables` сценария.
3. Шаблоны `{{variable}}` подставляются в строковые поля блоков, включая JSONPath и ожидаемые значения.
4. JSON-фрагменты `{$variable.path}` подставляются в JSON-тело API и JSON-фильтр Elma365.

Неизвестная переменная `{{...}}` заменяется пустой строкой; неизвестный JSON-фрагмент —
`null`; в лог пишется предупреждение.

## Samples

```text
samples/
  sample-simple-http.tbtest
  sample-with-wait.tbtest
  sample-json-variable.tbtest
  sample-elma365.tbtest
```

- `sample-simple-http` — POST на `https://httpbin.org/post`, проверка
  `$.json.name == "test"`.
- `sample-with-wait` — POST → пауза 2 секунды → GET с проверкой query-параметра.
- `sample-json-variable` — сохранение JSON запроса в `requestJson` и вставка
  `{$requestJson.customer.name}` в тело следующего запроса.
- `sample-elma365` — заготовка `CheckDataBlock`; замените `your_app`, App Code,
  токен, `REPLACE_WITH_OBJECT_ID` и BaseUrl.

Запуск sample через CLI:

```powershell
dotnet .\TestBuilder.Runner\bin\Release\net8.0\TestBuilder.Runner.dll `
  --scenario .\samples\sample-simple-http.tbtest `
  --output .\artifacts\result.xml `
  --format junit `
  --verbose
```

## CLI-раннер

```text
--scenario <path>     обязательный путь к .tbtest
--output <path>       обязательный путь к отчёту
--format <format>     junit | json | html (по умолчанию junit)
--environment <name>  зарезервировано для будущих версий
--verbose             подробный вывод
```

Коды выхода:

- `0` — все шаги успешны;
- `1` — есть проваленные/отменённые шаги;
- `2` — ошибка загрузки, валидации, формата или экспорта.

## Портативная сборка без .NET

Можно собрать self-contained single-file версию: .NET 8 runtime и все NuGet-зависимости
будут внутри exe, устанавливать .NET на целевой машине не нужно.

UI:

```powershell
dotnet publish .\TestBuilder.UI\TestBuilder.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o .\publish\TestBuilder.UI-win-x64
```

Runner:

```powershell
dotnet publish .\TestBuilder.Runner\TestBuilder.Runner.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o .\publish\TestBuilder.Runner-win-x64
```

Особенности:

- требуется Windows 10/11 **x64**; для ARM64 нужен отдельный `-r win-arm64`;
- .NET 8 устанавливать не нужно;
- single-file host распаковывает native-компоненты во временный каталог; он должен быть доступен на запись;
- если `%TEMP%` ограничен, задайте `DOTNET_BUNDLE_EXTRACT_BASE_DIR` и `TEMP`/`TMP` в локальную папку;
- готовый портативный комплект для этого проекта: `TestBuilder-standalone-win-x64.zip`.

## Как добавить новый блок

1. Создайте класс, реализующий `ITestBlock`:

```csharp
public sealed class MyBlock : ITestBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Мой блок";
    public bool Enabled { get; set; } = true;
    public string BlockType => "myBlock";

    [BlockProperty("URL", 0, PropertyEditorType.MultilineText)]
    public string Url { get; set; } = "";

    public Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
    {
        return Task.FromResult(BlockResult.Ok());
    }
}
```

2. Добавьте `IBlockFactory`:

```csharp
public sealed class MyBlockFactory : IBlockFactory
{
    public string BlockType => "myBlock";
    public string DisplayName => "Мой блок";
    public string? IconKey => null;
    public ITestBlock Create() => new MyBlock();
}
```

3. Зарегистрируйте фабрику в `BlockRegistryExtensions.AddBuiltInBlocks`.
4. Добавьте тесты блока и, при необходимости, JSON-пример.

Инспектор свойств сам построит редакторы по атрибутам `[BlockProperty]`.

## Тесты

```powershell
dotnet test .\TestBuilder.Tests\TestBuilder.Tests.csproj -c Release -f net8.0
dotnet test .\TestBuilder.Tests\TestBuilder.Tests.csproj -c Release -f net8.0-windows
```

`net8.0` — кроссплатформенные тесты; `net8.0-windows` дополнительно запускает
WinForms smoke-тест.

В `TestBuilder.Tests/runtimeconfig.template.json` отключён parent-process watchdog
testhost. Это обход `Win32Exception (5)` в sandbox/AppContainer-окружениях.

## CI

В репозитории есть `.github/workflows/build.yml`:

- Ubuntu: restore/build/test для `net8.0`;
- Windows: restore/build solution и тесты `net8.0-windows`.

Пример запуска раннера в стороннем workflow:

```yaml
name: integration-tests
on: [push]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Build runner
        run: dotnet build TestBuilder.Runner/TestBuilder.Runner.csproj -c Release

      - name: Run scenario
        run: |
          dotnet TestBuilder.Runner/bin/Release/net8.0/TestBuilder.Runner.dll \
            --scenario samples/sample-simple-http.tbtest \
            --output artifacts/junit.xml \
            --format junit \
            --verbose

      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: junit-report
          path: artifacts/junit.xml
```

## Ограничения текущей версии

- Нет ветвлений и условного выполнения блоков.
- Нет параллельного выполнения шагов.
- Drag&drop не поддерживается осознанно: порядок задаётся кнопками ▲ ▼ 🗑.
- `--environment` CLI пока только принимается и не переключает окружение.
- Редактор переменных сценария/окружения в UI отсутствует; переменные задаются в `.tbtest`.
- Токены и секреты хранятся в `.tbtest`/переменных; не коммитьте реальные секреты.
- `sample-simple-http` и `sample-with-wait` требуют доступа к `httpbin.org`.
