using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuilder.UI;

/// <summary>
/// Окно просмотра переменных, сохранённых во время выполнения сценария.
/// </summary>
public sealed class VariablesForm : Form
{
    private readonly ListBox _namesList;
    private readonly TextBox _valueBox;
    private readonly Label _summaryLabel;
    private readonly SplitContainer _split;
    private IReadOnlyDictionary<string, object> _variables =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Создаёт окно просмотра переменных.
    /// </summary>
    public VariablesForm()
    {
        Text = "Сохраненные переменные";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 420);
        Size = new Size(900, 620);
        ShowInTaskbar = false;

        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = "Переменных: 0",
        };

        var copyButton = new Button
        {
            Text = "Копировать",
            Dock = DockStyle.Right,
            Width = 110,
            UseVisualStyleBackColor = true,
        };
        copyButton.Click += (_, _) => CopyCurrentValue();

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(0, 4, 4, 4),
        };
        topPanel.Controls.Add(_summaryLabel);
        topPanel.Controls.Add(copyButton);

        _namesList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            SelectionMode = SelectionMode.One,
        };
        _namesList.SelectedIndexChanged += (_, _) => ShowSelectedValue();

        _valueBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            AcceptsTab = true,
            Font = new Font("Consolas", 10F),
            BackColor = SystemColors.Window,
        };

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
        };
        _split.Panel1.Controls.Add(_namesList);
        _split.Panel2.Controls.Add(_valueBox);

        Controls.Add(_split);
        Controls.Add(topPanel);
    }

    /// <inheritdoc />
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _split.Panel1MinSize = 180;
        _split.Panel2MinSize = 300;
        _split.SplitterDistance = Math.Clamp(
            240,
            _split.Panel1MinSize,
            Math.Max(_split.Panel1MinSize, _split.Width - _split.Panel2MinSize));
    }

    /// <summary>
    /// Обновляет список переменных. Окно можно держать открытым между запусками.
    /// </summary>
    /// <param name="variables">Переменные контекста выполнения.</param>
    public void UpdateVariables(IReadOnlyDictionary<string, object> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var selectedName = _namesList.SelectedItem as string;
        _variables = new Dictionary<string, object>(variables, StringComparer.OrdinalIgnoreCase);

        var names = _variables.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _namesList.BeginUpdate();
        try
        {
            _namesList.Items.Clear();
            _namesList.Items.AddRange(names);
        }
        finally
        {
            _namesList.EndUpdate();
        }

        _summaryLabel.Text = $"Переменных: {_variables.Count}";

        if (names.Length == 0)
        {
            _valueBox.Text = "Переменные появятся после выполнения сценария.";
            return;
        }

        var index = selectedName is null
            ? 0
            : Array.FindIndex(names, name =>
                string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase));
        _namesList.SelectedIndex = index >= 0 ? index : 0;
    }

    private void ShowSelectedValue()
    {
        if (_namesList.SelectedItem is not string name ||
            !_variables.TryGetValue(name, out var value))
        {
            return;
        }

        _valueBox.Text = FormatValue(value);
        _valueBox.SelectionStart = 0;
        _valueBox.SelectionLength = 0;
    }

    private void CopyCurrentValue()
    {
        if (_valueBox.TextLength == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(_valueBox.Text);
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException)
        {
            MessageBox.Show(
                this,
                $"Не удалось скопировать значение: {ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            JToken token => token.ToString(Formatting.Indented),
            string text => TryFormatJson(text),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static string TryFormatJson(string value)
    {
        var trimmed = value.Trim();
        if (!(trimmed.StartsWith('{') || trimmed.StartsWith('[')))
        {
            return value;
        }

        try
        {
            return JToken.Parse(trimmed).ToString(Formatting.Indented);
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
