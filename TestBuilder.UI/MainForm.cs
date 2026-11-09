using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Commands;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Reports;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Serialization;
using TestBuilder.UI.Blocks;
using TestBuilder.UI.Commands;
using TestBuilder.UI.Controls;
using TestBuilder.UI.Json;
using Environment = System.Environment;

namespace TestBuilder.UI;

/// <summary>
/// Главное окно Test Builder.
/// </summary>
public sealed class MainForm : Form
{
    private readonly IBlockRegistry _registry;
    private readonly TestRunner _runner;
    private readonly ScenarioService _scenarioService;
    private readonly ExecutionContextFactory _contextFactory;
    private readonly IReadOnlyList<IReportExporter> _exporters;
    private readonly ILogger<MainForm> _logger;
    private readonly UndoRedoStack _undoStack = new();

    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _newButton;
    private readonly ToolStripButton _openButton;
    private readonly ToolStripButton _saveButton;
    private readonly ToolStripButton _runButton;
    private readonly ToolStripButton _stopButton;
    private readonly ToolStripButton _exportButton;
    private readonly ToolStripButton _undoButton;
    private readonly ToolStripButton _redoButton;

    private readonly SplitContainer _outerSplit;
    private readonly SplitContainer _topSplit;
    private readonly SplitContainer _centerSplit;
    private readonly ListBox _paletteList;
    private readonly Button _addBlockButton;
    private readonly ListView _scenarioList;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly Button _deleteButton;
    private readonly Button _duplicateButton;
    private readonly PropertyInspectorControl _inspector;
    private readonly DataGridView _logGrid;

    private TestScenario _scenario = new() { Name = "Новый сценарий" };
    private string? _currentPath;
    private bool _isDirty;
    private bool _isRunning;
    private bool _isRefreshingList;
    private CancellationTokenSource? _cts;
    private RunReport? _lastReport;
    private Guid? _pendingSelection;

    /// <summary>
    /// Создаёт главное окно.
    /// </summary>
    public MainForm(
        IBlockRegistry registry,
        TestRunner runner,
        ScenarioService scenarioService,
        ExecutionContextFactory contextFactory,
        IEnumerable<IReportExporter> exporters,
        ILogger<MainForm> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _scenarioService = scenarioService ?? throw new ArgumentNullException(nameof(scenarioService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _exporters = (exporters ?? throw new ArgumentNullException(nameof(exporters))).ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Text = "Test Builder";
        Name = nameof(MainForm);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        ClientSize = new Size(1300, 850);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.Professional,
            Padding = new Padding(6, 2, 6, 2),
        };

        _newButton = CreateToolButton("Новый", "Новый сценарий (Ctrl+N)", OnNewClick);
        _openButton = CreateToolButton("Открыть", "Открыть сценарий (Ctrl+O)", OnOpenClick);
        _saveButton = CreateToolButton("Сохранить", "Сохранить сценарий (Ctrl+S)", OnSaveClick);
        _runButton = CreateToolButton("Запустить", "Запустить сценарий (F5)", OnRunClick);
        _stopButton = CreateToolButton("Стоп", "Остановить выполнение (Esc)", OnStopClick);
        _exportButton = CreateToolButton("Экспорт отчёта", "Экспортировать последний отчёт", OnExportClick);
        _undoButton = CreateToolButton("Отменить", "Отменить последнее действие", OnUndoClick);
        _redoButton = CreateToolButton("Повторить", "Повторить отменённое действие", OnRedoClick);

        _toolStrip.Items.AddRange(new ToolStripItem[]
        {
            _newButton,
            _openButton,
            _saveButton,
            new ToolStripSeparator(),
            _runButton,
            _stopButton,
            new ToolStripSeparator(),
            _exportButton,
            new ToolStripSeparator(),
            _undoButton,
            _redoButton,
        });

        _outerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
        };

        _topSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
        };

        _centerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
        };

        _paletteList = new ListBox
        {
            Dock = DockStyle.Fill,
            DisplayMember = nameof(BlockFactoryItem.DisplayName),
            IntegralHeight = false,
            SelectionMode = SelectionMode.One,
        };

        _addBlockButton = new Button
        {
            Text = "Добавить →",
            Dock = DockStyle.Bottom,
            Height = 32,
            UseVisualStyleBackColor = true,
        };

        var paletteGroup = new GroupBox
        {
            Text = "Доступные блоки",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        var paletteButtonPanel = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        paletteButtonPanel.Controls.Add(_addBlockButton);
        paletteGroup.Controls.Add(_paletteList);
        paletteGroup.Controls.Add(paletteButtonPanel);
        _topSplit.Panel1.Controls.Add(paletteGroup);

        _scenarioList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _scenarioList.Columns.Add("#", 42);
        _scenarioList.Columns.Add("Название", 320);
        _scenarioList.Columns.Add("Тип", 190);
        _scenarioList.Columns.Add("Статус", 120);

        _moveUpButton = CreateScenarioButton("▲", "Переместить вверх");
        _moveDownButton = CreateScenarioButton("▼", "Переместить вниз");
        _deleteButton = CreateScenarioButton("🗑", "Удалить шаг");
        _duplicateButton = CreateScenarioButton("Дублировать", "Дублировать шаг");
        _duplicateButton.Width = 105;

        var scenarioButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 4, 4, 4),
        };
        scenarioButtons.Controls.Add(_moveUpButton);
        scenarioButtons.Controls.Add(_moveDownButton);
        scenarioButtons.Controls.Add(_deleteButton);
        scenarioButtons.Controls.Add(_duplicateButton);

        var scenarioGroup = new GroupBox
        {
            Text = "Сценарий",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        scenarioGroup.Controls.Add(_scenarioList);
        scenarioGroup.Controls.Add(scenarioButtons);

        _inspector = new PropertyInspectorControl { Dock = DockStyle.Fill };
        var inspectorGroup = new GroupBox
        {
            Text = "Настройки шага",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        inspectorGroup.Controls.Add(_inspector);

        _centerSplit.Panel1.Controls.Add(scenarioGroup);
        _centerSplit.Panel2.Controls.Add(inspectorGroup);
        _topSplit.Panel2.Controls.Add(_centerSplit);
        _outerSplit.Panel1.Controls.Add(_topSplit);

        _logGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            EditMode = DataGridViewEditMode.EditProgrammatically,
        };
        _logGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Время", FillWeight = 10 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Шаг", FillWeight = 24 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Статус", FillWeight = 12 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Длительность", FillWeight = 12 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Сообщение", FillWeight = 42 });

        var logGroup = new GroupBox
        {
            Text = "Лог выполнения",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        logGroup.Controls.Add(_logGrid);
        _outerSplit.Panel2.Controls.Add(logGroup);

        Controls.Add(_outerSplit);
        Controls.Add(_toolStrip);
        _toolStrip.BringToFront();

        _paletteList.DoubleClick += (_, _) => AddSelectedBlock();
        _addBlockButton.Click += (_, _) => AddSelectedBlock();
        _scenarioList.SelectedIndexChanged += OnScenarioSelectionChanged;
        _inspector.ValueChanged += OnInspectorValueChanged;
        _moveUpButton.Click += (_, _) => MoveSelectedBlock(-1);
        _moveDownButton.Click += (_, _) => MoveSelectedBlock(1);
        _deleteButton.Click += (_, _) => DeleteSelectedBlock();
        _duplicateButton.Click += (_, _) => DuplicateSelectedBlock();
        _undoStack.Changed += (_, _) => UpdateCommandStates();
        FormClosing += OnFormClosingHandler;
        KeyDown += OnMainFormKeyDown;

        _logger.LogInformation("Главное окно Test Builder инициализировано.");

        LoadPalette();
        RefreshBlockList();
        UpdateTitle();
        UpdateCommandStates();
    }

    /// <inheritdoc />
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _outerSplit.Panel1MinSize = 260;
        _outerSplit.Panel2MinSize = 140;
        _topSplit.Panel1MinSize = 200;
        _topSplit.Panel2MinSize = 500;
        _centerSplit.Panel1MinSize = 150;
        _centerSplit.Panel2MinSize = 180;

        _outerSplit.SplitterDistance = Math.Max(
            _outerSplit.Panel1MinSize,
            (int)(_outerSplit.Height * 0.62));
        _topSplit.SplitterDistance = Math.Max(
            _topSplit.Panel1MinSize,
            (int)(_topSplit.Width * 0.22));
        _centerSplit.SplitterDistance = Math.Max(
            _centerSplit.Panel1MinSize,
            (int)(_centerSplit.Height * 0.58));
    }

    private void LoadPalette()
    {
        _paletteList.Items.Clear();
        foreach (var factory in _registry.All())
        {
            _paletteList.Items.Add(new BlockFactoryItem(factory));
        }

        if (_paletteList.Items.Count > 0)
        {
            _paletteList.SelectedIndex = 0;
        }

        UpdateCommandStates();
    }

    private void AddSelectedBlock()
    {
        if (_isRunning || _paletteList.SelectedItem is not BlockFactoryItem item)
        {
            return;
        }

        ITestBlock block;
        try
        {
            block = item.Factory.Create();
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось создать блок «{item.DisplayName}»: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(block.Title))
        {
            block.Title = item.DisplayName;
        }

        var selectedIndex = GetSelectedIndex();
        var insertIndex = selectedIndex >= 0 ? selectedIndex + 1 : _scenario.Blocks.Count;
        _pendingSelection = block.Id;

        _undoStack.Push(new AddBlockCommand(_scenario, block, insertIndex));
        MarkDirty();
        RefreshBlockList();
    }

    private void DuplicateSelectedBlock()
    {
        if (_isRunning || SelectedBlock is not { } selected)
        {
            return;
        }

        try
        {
            var options = JsonSetup.CreateDefault(_registry);
            var json = JsonSerializer.Serialize(selected, typeof(ITestBlock), options);
            var clone = JsonSerializer.Deserialize<ITestBlock>(json, options)
                        ?? throw new InvalidOperationException("Клон блока не создан.");

            clone.Id = Guid.NewGuid();
            clone.Title = "Копия — " + (string.IsNullOrWhiteSpace(selected.Title) ? "шаг" : selected.Title);

            var index = _scenario.Blocks.IndexOf(selected);
            _pendingSelection = clone.Id;
            _undoStack.Push(new AddBlockCommand(_scenario, clone, index + 1));
            MarkDirty();
            RefreshBlockList();
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось дублировать блок: {ex.Message}");
        }
    }

    private void MoveSelectedBlock(int direction)
    {
        if (_isRunning || SelectedBlock is not { } selected)
        {
            return;
        }

        var index = _scenario.Blocks.IndexOf(selected);
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= _scenario.Blocks.Count)
        {
            return;
        }

        _pendingSelection = selected.Id;
        _undoStack.Push(new MoveBlockCommand(_scenario, index, newIndex));
        MarkDirty();
        RefreshBlockList();
    }

    private void DeleteSelectedBlock()
    {
        if (_isRunning || SelectedBlock is not { } selected)
        {
            return;
        }

        var index = _scenario.Blocks.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        _pendingSelection = index + 1 < _scenario.Blocks.Count
            ? _scenario.Blocks[index + 1].Id
            : index > 0
                ? _scenario.Blocks[index - 1].Id
                : null;

        _undoStack.Push(new RemoveBlockCommand(_scenario, selected, index));
        MarkDirty();
        RefreshBlockList();
    }

    private void OnUndoClick(object? sender, EventArgs e)
    {
        if (_isRunning || !_undoStack.CanUndo)
        {
            return;
        }

        _pendingSelection = null;
        _undoStack.Undo();
        MarkDirty();
        RefreshBlockList();
    }

    private void OnRedoClick(object? sender, EventArgs e)
    {
        if (_isRunning || !_undoStack.CanRedo)
        {
            return;
        }

        _pendingSelection = null;
        _undoStack.Redo();
        MarkDirty();
        RefreshBlockList();
    }

    private void RefreshBlockList()
    {
        var selectedId = _pendingSelection ?? SelectedBlock?.Id;
        _pendingSelection = null;

        _isRefreshingList = true;
        _scenarioList.BeginUpdate();
        try
        {
            _scenarioList.Items.Clear();
            for (var index = 0; index < _scenario.Blocks.Count; index++)
            {
                var block = _scenario.Blocks[index];
                var listItem = new ListViewItem((index + 1).ToString(CultureInfo.InvariantCulture))
                {
                    Tag = block,
                };
                listItem.SubItems.Add(block.Title);
                listItem.SubItems.Add(GetBlockTypeDisplay(block.BlockType));
                listItem.SubItems.Add("Ожидает");
                _scenarioList.Items.Add(listItem);
            }
        }
        finally
        {
            _scenarioList.EndUpdate();
            _isRefreshingList = false;
        }

        ListViewItem? target = null;
        if (selectedId is not null)
        {
            target = FindItem(selectedId.Value);
        }

        if (target is null && _scenarioList.Items.Count > 0)
        {
            target = _scenarioList.Items[0];
        }

        if (target is not null)
        {
            target.Selected = true;
            target.Focused = true;
            target.EnsureVisible();
        }
        else
        {
            _inspector.Bind(null);
        }

        UpdateCommandStates();
    }

    private void OnScenarioSelectionChanged(object? sender, EventArgs e)
    {
        if (_isRefreshingList)
        {
            return;
        }

        _inspector.Bind(SelectedBlock, BuildJsonVariables());
        UpdateCommandStates();
    }

    private IReadOnlyList<JsonVariableInfo> BuildJsonVariables()
    {
        var index = GetSelectedIndex();
        return index > 0
            ? JsonVariableCatalog.Build(_scenario, index)
            : Array.Empty<JsonVariableInfo>();
    }

    private void OnInspectorValueChanged(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (SelectedBlock is { } block)
        {
            var item = FindItem(block.Id);
            if (item is not null)
            {
                item.SubItems[1].Text = block.Title;
                item.SubItems[2].Text = GetBlockTypeDisplay(block.BlockType);
            }
        }

        MarkDirty();
    }

    private void OnNewClick(object? sender, EventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        _scenario = new TestScenario { Name = "Новый сценарий" };
        _currentPath = null;
        _isDirty = false;
        _lastReport = null;
        _undoStack.Clear();
        _inspector.Bind(null);
        _logGrid.Rows.Clear();
        RefreshBlockList();
        UpdateTitle();
        UpdateCommandStates();
    }

    private void OnOpenClick(object? sender, EventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Открыть сценарий",
            Filter = "Test Builder scenario (*.tbtest)|*.tbtest|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var scenario = _scenarioService.Load(dialog.FileName);
            _scenario = scenario;
            _currentPath = dialog.FileName;
            _isDirty = false;
            _lastReport = null;
            _undoStack.Clear();
            _logGrid.Rows.Clear();
            RefreshBlockList();
            UpdateTitle();
            UpdateCommandStates();
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось открыть сценарий:{Environment.NewLine}{ex.Message}");
        }
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        SaveScenario();
    }

    private bool SaveScenario()
    {
        var path = _currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить сценарий",
                Filter = "Test Builder scenario (*.tbtest)|*.tbtest|Все файлы (*.*)|*.*",
                DefaultExt = "tbtest",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(_scenario.Name) ? "scenario.tbtest" : _scenario.Name + ".tbtest",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            path = dialog.FileName;
        }

        try
        {
            if (string.Equals(_scenario.Name, "Новый сценарий", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_scenario.Name))
            {
                _scenario.Name = Path.GetFileNameWithoutExtension(path);
            }

            _scenarioService.Save(_scenario, path);
            _currentPath = path;
            _isDirty = false;
            UpdateTitle();
            _logger.LogInformation("Сценарий сохранён в {Path}.", path);
            return true;
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось сохранить сценарий:{Environment.NewLine}{ex.Message}");
            return false;
        }
    }

    private async void OnRunClick(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _scenarioService.Validate(_scenario);
        }
        catch (ValidationException ex)
        {
            ShowError(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            ShowError($"Сценарий не прошёл валидацию:{Environment.NewLine}{ex.Message}");
            return;
        }

        RefreshBlockList();
        _logGrid.Rows.Clear();
        _lastReport = null;
        _exportButton.Enabled = false;

        _cts = new CancellationTokenSource();
        var cts = _cts;
        SetRunning(true);

        var progress = new Progress<StepProgress>(step =>
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(() => UpdateProgress(step)));
        });

        try
        {
            var context = await _contextFactory.CreateAsync(_scenario, cts.Token);
            _lastReport = await _runner.RunAsync(_scenario, context, progress);
            _logger.LogInformation(
                "Сценарий '{Name}' завершён. Успех: {Passed}.",
                _scenario.Name,
                _lastReport.AllPassed);
        }
        catch (OperationCanceledException)
        {
            AppendLog("-", "Выполнение", "Отменено", "", "Запуск сценария отменён.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска сценария.");
            ShowError($"Ошибка выполнения сценария:{Environment.NewLine}{ex.Message}");
            AppendLog("-", "Ошибка", "Ошибка", "", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
                cts.Dispose();
            }
            else
            {
                cts.Dispose();
            }

            SetRunning(false);
            UpdateCommandStates();
        }
    }

    private void OnStopClick(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _stopButton.Enabled = false;
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        if (_lastReport is null)
        {
            MessageBox.Show(
                this,
                "Сначала выполните сценарий, чтобы сформировать отчёт.",
                "Test Builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт отчёта",
            Filter = "JUnit XML (*.xml)|*.xml|JSON (*.json)|*.json|HTML (*.html)|*.html",
            FilterIndex = 1,
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var format = dialog.FilterIndex switch
        {
            2 => "json",
            3 => "html",
            _ => "junit",
        };

        var exporter = _exporters.FirstOrDefault(item =>
            string.Equals(item.Format, format, StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
        {
            ShowError($"Экспортёр формата '{format}' не зарегистрирован.");
            return;
        }

        try
        {
            using var stream = File.Create(dialog.FileName);
            exporter.Export(_lastReport, stream);
            MessageBox.Show(
                this,
                $"Отчёт сохранён:{Environment.NewLine}{dialog.FileName}",
                "Test Builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось сохранить отчёт:{Environment.NewLine}{ex.Message}");
        }
    }

    private void UpdateProgress(StepProgress progress)
    {
        if (IsDisposed)
        {
            return;
        }

        var item = FindItem(progress.BlockId);
        if (item is not null)
        {
            item.SubItems[3].Text = GetStatusText(progress.Status);
            item.BackColor = GetStatusColor(progress.Status);
        }

        var duration = progress.Result is null
            ? string.Empty
            : FormatDuration(progress.Result.Duration);
        var message = progress.Result?.Message ??
                      (progress.Status == StepStatus.Running ? "Выполняется…" : string.Empty);

        AppendLog(
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            progress.Title,
            GetStatusText(progress.Status),
            duration,
            message,
            GetStatusColor(progress.Status));
    }

    private void AppendLog(
        string time,
        string step,
        string status,
        string duration,
        string message,
        Color? color = null)
    {
        var rowIndex = _logGrid.Rows.Add(time, step, status, duration, message);
        if (color is not null)
        {
            _logGrid.Rows[rowIndex].DefaultCellStyle.BackColor = color.Value;
        }

        if (rowIndex >= 0)
        {
            _logGrid.FirstDisplayedScrollingRowIndex = rowIndex;
        }
    }

    private void SetRunning(bool running)
    {
        _isRunning = running;
        _runButton.Enabled = !running;
        _stopButton.Enabled = running;
        _newButton.Enabled = !running;
        _openButton.Enabled = !running;
        _saveButton.Enabled = !running;
        _paletteList.Enabled = !running;
        _scenarioList.Enabled = !running;
        _inspector.Enabled = !running;
        _addBlockButton.Enabled = !running;
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        _undoButton.Enabled = !_isRunning && _undoStack.CanUndo;
        _redoButton.Enabled = !_isRunning && _undoStack.CanRedo;
        _exportButton.Enabled = !_isRunning && _lastReport is not null;

        var hasSelection = SelectedBlock is not null;
        var index = GetSelectedIndex();
        _moveUpButton.Enabled = !_isRunning && hasSelection && index > 0;
        _moveDownButton.Enabled = !_isRunning && hasSelection && index >= 0 && index < _scenario.Blocks.Count - 1;
        _deleteButton.Enabled = !_isRunning && hasSelection;
        _duplicateButton.Enabled = !_isRunning && hasSelection;
        _addBlockButton.Enabled = !_isRunning && _paletteList.SelectedItem is BlockFactoryItem;
    }

    private void MarkDirty()
    {
        if (_isDirty)
        {
            return;
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrWhiteSpace(_scenario.Name) ? "Без имени" : _scenario.Name;
        Text = $"Test Builder — {name}{(_isDirty ? " *" : string.Empty)}";
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "В сценарии есть несохранённые изменения. Сохранить их?",
            "Test Builder",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => SaveScenario(),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void OnFormClosingHandler(object? sender, FormClosingEventArgs e)
    {
        if (_isRunning)
        {
            var result = MessageBox.Show(
                this,
                "Сценарий ещё выполняется. Остановить выполнение и закрыть окно?",
                "Test Builder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _cts?.Cancel();
        }

        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N)
        {
            OnNewClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.O)
        {
            OnOpenClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            OnSaveClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            OnRunClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape && _isRunning)
        {
            OnStopClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
    }

    private ITestBlock? SelectedBlock =>
        _scenarioList.SelectedItems.Count > 0
            ? _scenarioList.SelectedItems[0].Tag as ITestBlock
            : null;

    private int GetSelectedIndex()
    {
        if (_scenarioList.SelectedItems.Count == 0)
        {
            return -1;
        }

        return _scenario.Blocks.IndexOf((ITestBlock)_scenarioList.SelectedItems[0].Tag!);
    }

    private ListViewItem? FindItem(Guid blockId)
    {
        foreach (ListViewItem item in _scenarioList.Items)
        {
            if (item.Tag is ITestBlock block && block.Id == blockId)
            {
                return item;
            }
        }

        return null;
    }

    private string GetBlockTypeDisplay(string blockType)
    {
        return _registry.Find(blockType)?.DisplayName ?? blockType;
    }

    private static ToolStripButton CreateToolButton(
        string text,
        string toolTip,
        EventHandler clickHandler)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = toolTip,
            AutoToolTip = false,
            Margin = new Padding(2, 1, 2, 1),
        };
        button.Click += clickHandler;
        return button;
    }

    private static Button CreateScenarioButton(string text, string toolTip)
    {
        var button = new Button
        {
            Text = text,
            Width = 36,
            Height = 28,
            UseVisualStyleBackColor = true,
            Margin = new Padding(2, 0, 2, 0),
        };
        new ToolTip().SetToolTip(button, toolTip);
        return button;
    }

    private static string GetStatusText(StepStatus status)
    {
        return status switch
        {
            StepStatus.Success => "Успех",
            StepStatus.Failed => "Ошибка",
            StepStatus.Cancelled => "Отменён",
            StepStatus.Skipped => "Пропущен",
            StepStatus.Running => "Выполняется",
            _ => "Ожидает",
        };
    }

    private static Color GetStatusColor(StepStatus status)
    {
        return status switch
        {
            StepStatus.Running => Color.LightBlue,
            StepStatus.Success => Color.LightGreen,
            StepStatus.Failed => Color.LightCoral,
            StepStatus.Cancelled => Color.LightYellow,
            _ => Color.White,
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " с";
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Test Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
