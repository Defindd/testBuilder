using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.UI.Json;

namespace TestBuilder.UI.Controls;

/// <summary>
/// Инспектор свойств выбранного блока. Строит редакторы по атрибутам <see cref="BlockPropertyAttribute"/>.
/// </summary>
public sealed class PropertyInspectorControl : UserControl
{
    private readonly ErrorProvider _errors = new();
    private readonly ToolTip _toolTip = new();
    private IReadOnlyList<JsonVariableInfo> _jsonVariables = Array.Empty<JsonVariableInfo>();
    private object? _target;

    /// <summary>
    /// Возникает после успешной записи изменённого значения в целевой объект.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Создаёт инспектор свойств.
    /// </summary>
    public PropertyInspectorControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        Padding = new Padding(8);
        DoubleBuffered = true;

        _errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        _errors.ContainerControl = this;
    }

    /// <summary>
    /// Привязывает инспектор к объекту. <see langword="null"/> очищает контрол.
    /// </summary>
    /// <param name="target">Объект с размеченными свойствами.</param>
    /// <param name="jsonVariables">JSON-переменные предыдущих блоков для автодополнения.</param>
    public void Bind(
        object? target,
        IReadOnlyList<JsonVariableInfo>? jsonVariables = null)
    {
        _target = target;
        _jsonVariables = jsonVariables ?? Array.Empty<JsonVariableInfo>();
        Rebuild();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _errors.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Rebuild()
    {
        SuspendLayout();

        var oldControls = Controls.Cast<Control>().ToArray();
        Controls.Clear();
        foreach (var control in oldControls)
        {
            control.Dispose();
        }

        if (_target is not null)
        {
            Controls.Add(BuildTable(_target));
        }

        ResumeLayout(performLayout: true);
    }

    private TableLayoutPanel BuildTable(object target)
    {
        var properties = target.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<BlockPropertyAttribute>(),
            })
            .Where(item =>
                item.Attribute is not null &&
                item.Property.CanRead &&
                item.Property.CanWrite &&
                item.Property.GetIndexParameters().Length == 0)
            .OrderBy(item => item.Attribute!.Order)
            .ThenBy(item => item.Property.Name, StringComparer.Ordinal)
            .ToArray();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = Math.Max(properties.Length, 1),
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Margin = new Padding(0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        if (properties.Length == 0)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var empty = new Label
            {
                Text = "Нет настраиваемых свойств.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 8, 3, 3),
            };
            table.Controls.Add(empty, 0, 0);
            table.SetColumnSpan(empty, 2);
            return table;
        }

        for (var index = 0; index < properties.Length; index++)
        {
            var item = properties[index];
            var attribute = item.Attribute!;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label
            {
                Text = attribute.DisplayName,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 10, 3),
            };

            if (!string.IsNullOrWhiteSpace(attribute.Description))
            {
                _toolTip.SetToolTip(label, attribute.Description);
            }

            object? currentValue = null;
            try
            {
                currentValue = item.Property.GetValue(target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PropertyInspector: не удалось прочитать {item.Property.Name}: {ex}");
            }

            var editor = CreateEditor(target, item.Property, attribute, currentValue);
            if (!string.IsNullOrWhiteSpace(attribute.Description))
            {
                _toolTip.SetToolTip(editor, attribute.Description);
            }

            table.Controls.Add(label, 0, index);
            table.Controls.Add(editor, 1, index);
        }

        return table;
    }

    private Control CreateEditor(
        object target,
        PropertyInfo property,
        BlockPropertyAttribute attribute,
        object? currentValue)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        return attribute.Editor switch
        {
            PropertyEditorType.KeyValueTable => CreateKeyValueGrid(target, property, currentValue),
            PropertyEditorType.JsonPathCheckTable => CreateJsonPathGrid(target, property, currentValue),
            PropertyEditorType.MultilineText when propertyType == typeof(string) =>
                CreateTextBox(target, property, currentValue, multiline: true, secret: false),
            PropertyEditorType.Secret when propertyType == typeof(string) =>
                CreateTextBox(target, property, currentValue, multiline: false, secret: true),
            _ => CreateAutoEditor(target, property, propertyType, currentValue),
        };
    }

    private Control CreateAutoEditor(
        object target,
        PropertyInfo property,
        Type propertyType,
        object? currentValue)
    {
        if (propertyType == typeof(string))
        {
            return CreateTextBox(target, property, currentValue, multiline: false, secret: false);
        }

        if (propertyType == typeof(int) ||
            propertyType == typeof(long) ||
            propertyType == typeof(short) ||
            propertyType == typeof(byte) ||
            propertyType == typeof(decimal) ||
            propertyType == typeof(double) ||
            propertyType == typeof(float))
        {
            return CreateNumericEditor(target, property, propertyType, currentValue);
        }

        if (propertyType == typeof(bool))
        {
            var checkBox = new CheckBox
            {
                Checked = currentValue is true,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 3, 3),
            };
            checkBox.CheckedChanged += (_, _) => SetAndNotify(target, property, checkBox.Checked);
            return checkBox;
        }

        if (propertyType.IsEnum)
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };

            foreach (var value in Enum.GetValues(propertyType))
            {
                comboBox.Items.Add(new EnumOption(value, GetEnumDisplayName(value)));
            }

            if (currentValue is not null)
            {
                var selected = comboBox.Items
                    .Cast<EnumOption>()
                    .FirstOrDefault(option => Equals(option.Value, currentValue));
                if (selected is not null)
                {
                    comboBox.SelectedItem = selected;
                }
                else if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }
            else if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }

            comboBox.SelectedIndexChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is EnumOption option)
                {
                    SetAndNotify(target, property, option.Value);
                }
            };
            return comboBox;
        }

        if (propertyType == typeof(TimeSpan))
        {
            return CreateTimeSpanEditor(target, property, currentValue);
        }

        if (propertyType == typeof(DateTime))
        {
            var picker = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
            };

            if (currentValue is DateTime dateTime &&
                dateTime >= picker.MinDate &&
                dateTime <= picker.MaxDate)
            {
                picker.Value = dateTime;
            }

            picker.ValueChanged += (_, _) => SetAndNotify(target, property, picker.Value);
            return picker;
        }

        return new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = currentValue?.ToString() ?? string.Empty,
        };
    }

    private Control CreateTextBox(
        object target,
        PropertyInfo property,
        object? currentValue,
        bool multiline,
        bool secret)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = currentValue as string ?? currentValue?.ToString() ?? string.Empty,
            Multiline = multiline,
            AcceptsReturn = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            UseSystemPasswordChar = secret,
            AutoSize = !multiline,
        };

        if (multiline)
        {
            textBox.Height = 120;
            textBox.MinimumSize = new Size(0, 110);
        }

        textBox.TextChanged += (_, _) => SetAndNotify(target, property, textBox.Text);

        if (!multiline)
        {
            return textBox;
        }

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 150,
            MinimumSize = new Size(0, 150),
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(3),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 3, 0, 0),
        };

        var uuidButton = new Button
        {
            Text = "UUID",
            Width = 70,
            Height = 26,
            UseVisualStyleBackColor = true,
            Margin = new Padding(0, 0, 6, 0),
        };
        uuidButton.Click += (_, _) => InsertToken(textBox, "{{random.uuid}}");
        _toolTip.SetToolTip(uuidButton, "Вставить {{random.uuid}}");

        var numberButton = new Button
        {
            Text = "Число",
            Width = 70,
            Height = 26,
            UseVisualStyleBackColor = true,
            Margin = new Padding(0),
        };
        numberButton.Click += (_, _) => InsertToken(textBox, "{{random.number}}");
        _toolTip.SetToolTip(numberButton, "Вставить {{random.number}}");

        var suggestions = new ListBox
        {
            Dock = DockStyle.Fill,
            Height = 90,
            IntegralHeight = false,
            Visible = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 0),
        };
        suggestions.MouseClick += (_, _) =>
        {
            if (suggestions.SelectedItem is string path)
            {
                InsertJsonPath(textBox, suggestions, panel, path);
            }
        };
        suggestions.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && suggestions.SelectedItem is string path)
            {
                InsertJsonPath(textBox, suggestions, panel, path);
                e.SuppressKeyPress = true;
            }
        };

        textBox.TextChanged += (_, _) => UpdateJsonSuggestions(textBox, suggestions, panel);
        textBox.Leave += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    var isOverSuggestions = suggestions.Visible &&
                        suggestions.RectangleToScreen(suggestions.ClientRectangle)
                            .Contains(Cursor.Position);
                    if (!suggestions.Focused && !isOverSuggestions)
                    {
                        HideJsonSuggestions(suggestions, panel);
                    }
                }));
            }
        };

        buttons.Controls.Add(uuidButton);
        buttons.Controls.Add(numberButton);
        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(buttons, 0, 1);
        panel.Controls.Add(suggestions, 0, 2);
        return panel;
    }

    private void UpdateJsonSuggestions(
        TextBox textBox,
        ListBox suggestions,
        TableLayoutPanel panel)
    {
        if (_jsonVariables.Count == 0)
        {
            HideJsonSuggestions(suggestions, panel);
            return;
        }

        var caret = Math.Min(textBox.SelectionStart, textBox.Text.Length);
        var beforeCaret = textBox.Text[..caret];
        var tokenStart = beforeCaret.LastIndexOf("{$", StringComparison.Ordinal);
        if (tokenStart < 0 || beforeCaret.IndexOf('}', tokenStart) >= 0)
        {
            HideJsonSuggestions(suggestions, panel);
            return;
        }

        var fragment = beforeCaret[(tokenStart + 2)..].TrimStart();
        var paths = _jsonVariables
            .SelectMany(variable => variable.Paths)
            .Where(path =>
                path.StartsWith(fragment, StringComparison.OrdinalIgnoreCase) &&
                path.Length > fragment.Length)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        if (paths.Length == 0)
        {
            HideJsonSuggestions(suggestions, panel);
            return;
        }

        suggestions.BeginUpdate();
        try
        {
            suggestions.Items.Clear();
            suggestions.Items.AddRange(paths);
        }
        finally
        {
            suggestions.EndUpdate();
        }

        suggestions.Visible = true;
        suggestions.BringToFront();
        panel.Height = 250;
    }

    private static void HideJsonSuggestions(ListBox suggestions, TableLayoutPanel panel)
    {
        suggestions.Visible = false;
        suggestions.Items.Clear();
        panel.Height = 150;
    }

    private static void InsertJsonPath(
        TextBox textBox,
        ListBox suggestions,
        TableLayoutPanel panel,
        string path)
    {
        var caret = Math.Min(textBox.SelectionStart, textBox.Text.Length);
        var beforeCaret = textBox.Text[..caret];
        var tokenStart = beforeCaret.LastIndexOf("{$", StringComparison.Ordinal);
        if (tokenStart < 0)
        {
            HideJsonSuggestions(suggestions, panel);
            return;
        }

        var replacement = "{$" + path + "}";
        var afterCaret = textBox.Text[caret..];
        textBox.Text = beforeCaret[..tokenStart] + replacement + afterCaret;
        textBox.SelectionStart = tokenStart + replacement.Length;
        textBox.SelectionLength = 0;
        HideJsonSuggestions(suggestions, panel);
        textBox.Focus();
    }

    private static string GetEnumDisplayName(object value)
    {
        var field = value.GetType().GetField(value.ToString()!);
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description
               ?? value.ToString()
               ?? string.Empty;
    }

    private static void InsertToken(TextBox textBox, string token)
    {
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        textBox.Text = textBox.Text
            .Remove(selectionStart, selectionLength)
            .Insert(selectionStart, token);
        textBox.SelectionStart = selectionStart + token.Length;
        textBox.SelectionLength = 0;
        textBox.Focus();
    }

    private Control CreateNumericEditor(
        object target,
        PropertyInfo property,
        Type propertyType,
        object? currentValue)
    {
        var editor = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            DecimalPlaces = propertyType == typeof(int) ||
                            propertyType == typeof(long) ||
                            propertyType == typeof(short) ||
                            propertyType == typeof(byte)
                ? 0
                : 4,
            Increment = 1M,
        };

        if (propertyType == typeof(int))
        {
            editor.Minimum = int.MinValue;
            editor.Maximum = int.MaxValue;
        }
        else if (propertyType == typeof(long))
        {
            editor.Minimum = long.MinValue;
            editor.Maximum = long.MaxValue;
        }
        else if (propertyType == typeof(short))
        {
            editor.Minimum = short.MinValue;
            editor.Maximum = short.MaxValue;
        }
        else if (propertyType == typeof(byte))
        {
            editor.Minimum = byte.MinValue;
            editor.Maximum = byte.MaxValue;
        }
        else if (propertyType == typeof(decimal))
        {
            editor.Minimum = decimal.MinValue;
            editor.Maximum = decimal.MaxValue;
        }
        else
        {
            editor.Minimum = decimal.MinValue;
            editor.Maximum = decimal.MaxValue;
            editor.Increment = 0.1M;
        }

        if (currentValue is not null)
        {
            try
            {
                var numeric = Convert.ToDecimal(currentValue, CultureInfo.InvariantCulture);
                editor.Value = Math.Clamp(numeric, editor.Minimum, editor.Maximum);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                Debug.WriteLine($"PropertyInspector: не удалось преобразовать {property.Name}: {ex.Message}");
            }
        }

        editor.ValueChanged += (_, _) =>
            SetAndNotify(target, property, editor.Value);
        return editor;
    }

    private Control CreateTimeSpanEditor(
        object target,
        PropertyInfo property,
        object? currentValue)
    {
        var editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = currentValue is TimeSpan duration
                ? duration.ToString("c", CultureInfo.InvariantCulture)
                : "00:00:00",
        };

        editor.TextChanged += (_, _) => _errors.SetError(editor, string.Empty);
        editor.Leave += (_, _) =>
        {
            if (!TimeSpan.TryParse(editor.Text, CultureInfo.InvariantCulture, out var parsed))
            {
                _errors.SetError(editor, "Формат: hh:mm:ss или d.hh:mm:ss.");
                return;
            }

            _errors.SetError(editor, string.Empty);
            SetAndNotify(target, property, parsed);
        };

        return editor;
    }

    private Control CreateKeyValueGrid(
        object target,
        PropertyInfo property,
        object? currentValue)
    {
        var source = currentValue as IEnumerable<KeyValueItem> ?? Enumerable.Empty<KeyValueItem>();
        var bindingList = new BindingList<KeyValueItem>(
            source.Select(item => new KeyValueItem
            {
                Key = item.Key,
                Value = item.Value,
            }).ToList());

        var grid = CreateGridBase();
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Key",
            DataPropertyName = nameof(KeyValueItem.Key),
            FillWeight = 40,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Value",
            DataPropertyName = nameof(KeyValueItem.Value),
            FillWeight = 60,
        });
        grid.DataSource = bindingList;

        void Commit()
        {
            SetAndNotify(
                target,
                property,
                bindingList.Select(item => new KeyValueItem
                {
                    Key = item.Key,
                    Value = item.Value,
                }).ToList());
        }

        grid.CellValueChanged += (_, _) => Commit();
        grid.CellEndEdit += (_, _) => Commit();
        bindingList.ListChanged += (_, _) => Commit();
        return grid;
    }

    private Control CreateJsonPathGrid(
        object target,
        PropertyInfo property,
        object? currentValue)
    {
        var source = currentValue as IEnumerable<JsonPathCheck> ?? Enumerable.Empty<JsonPathCheck>();
        var bindingList = new BindingList<JsonPathCheck>(
            source.Select(item => new JsonPathCheck
            {
                Path = item.Path,
                Operator = item.Operator,
                ExpectedValue = item.ExpectedValue,
            }).ToList());

        var grid = CreateGridBase();
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Path",
            DataPropertyName = nameof(JsonPathCheck.Path),
            FillWeight = 34,
        });
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Оператор",
            DataPropertyName = nameof(JsonPathCheck.Operator),
            DataSource = Enum.GetValues<CheckOperator>(),
            ValueType = typeof(CheckOperator),
            FillWeight = 26,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Ожидаемое значение",
            DataPropertyName = nameof(JsonPathCheck.ExpectedValue),
            FillWeight = 40,
        });
        grid.DataSource = bindingList;
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        void Commit()
        {
            SetAndNotify(
                target,
                property,
                bindingList.Select(item => new JsonPathCheck
                {
                    Path = item.Path,
                    Operator = item.Operator,
                    ExpectedValue = item.ExpectedValue,
                }).ToList());
        }

        grid.CellValueChanged += (_, _) => Commit();
        grid.CellEndEdit += (_, _) => Commit();
        bindingList.ListChanged += (_, _) => Commit();
        return grid;
    }

    private static DataGridView CreateGridBase()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Height = 140,
            MinimumSize = new Size(0, 140),
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditOnEnter,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
        };

        grid.DataError += (_, e) => e.ThrowException = false;
        return grid;
    }

    private void SetAndNotify(object target, PropertyInfo property, object? value)
    {
        if (!property.CanWrite)
        {
            return;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var converted = value;

            if (value is not null && !targetType.IsInstanceOfType(value))
            {
                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            property.SetValue(target, converted);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PropertyInspector: не удалось записать {property.Name}: {ex.Message}");
        }
    }

    private sealed class EnumOption
    {
        public EnumOption(object value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public object Value { get; }

        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
