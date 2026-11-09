using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using FluentAssertions;
using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Blocks.Wait;
using TestBuilder.UI.Controls;
using TestBuilder.UI.Json;
using Xunit;

namespace TestBuilder.Tests.UI;

/// <summary>
/// Smoke-тесты инспектора свойств.
/// </summary>
public sealed class PropertyInspectorControlTests
{
    [Fact]
    public void Bind_WaitBlock_CreatesEditorControls()
    {
        RunOnSta(() =>
        {
            using var control = new PropertyInspectorControl();
            control.Bind(new WaitBlock());

            control.Controls.Count.Should().BeGreaterThan(0);
            control.Controls[0].Controls.Count.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void Bind_ApiRequestBlock_EditsMethodAuthAndRandomButtons()
    {
        RunOnSta(() =>
        {
            using var control = new PropertyInspectorControl();
            var block = new ApiRequestBlock();
            control.Bind(block);

            var table = control.Controls.OfType<TableLayoutPanel>().Single();

            var method = GetEditor<ComboBox>(table, "Метод");
            var methodItems = method.Items.Cast<object>().Select(item => item.ToString()).ToArray();
            methodItems.Should().Contain("GET");
            methodItems.Should().Contain("POST");
            methodItems.Should().Contain("PUT");
            methodItems.Should().Contain("DELETE");
            method.SelectedItem = method.Items.Cast<object>().Single(item => item.ToString() == "PUT");
            block.Method.Should().Be(HttpMethodType.Put);

            var auth = GetEditor<ComboBox>(table, "Авторизация");
            auth.SelectedItem = auth.Items.Cast<object>().Single(item => item.ToString() == "Bearer");
            block.AuthType.Should().Be(HttpAuthType.Bearer);

            var token = GetEditor<TextBox>(table, "Bearer-токен");
            token.Text = "secret-token";
            block.AuthToken.Should().Be("secret-token");

            var bodyEditor = GetEditor<Control>(table, "Тело");
            var buttons = Descendants(bodyEditor).OfType<Button>().Select(button => button.Text).ToArray();
            buttons.Should().Contain("UUID");
            buttons.Should().Contain("Число");
        });
    }

    [Fact]
    public void Bind_CheckDataBlock_ShowsAppCodeAndSearchModes()
    {
        RunOnSta(() =>
        {
            using var control = new PropertyInspectorControl();
            control.Bind(new CheckDataBlock());

            var table = control.Controls.OfType<TableLayoutPanel>().Single();
            var labels = table.Controls.OfType<Label>().Select(label => label.Text).ToArray();
            labels.Should().Contain("Код приложения");
            labels.Should().Contain("Поиск");
            labels.Should().Contain("Фильтр");
            labels.Should().NotContain("Тип объекта");

            var search = GetEditor<ComboBox>(table, "Поиск");
            var modes = search.Items.Cast<object>().Select(item => item.ToString()).ToArray();
            modes.Should().Contain("По ID");
            modes.Should().Contain("По фильтру");
        });
    }

    [Fact]
    public void Bind_CheckDataFilter_ShowsJsonPathSuggestions()
    {
        RunOnSta(() =>
        {
            using var control = new PropertyInspectorControl();
            var variables = new[]
            {
                new JsonVariableInfo(
                    "requestJson",
                    new[] { "requestJson", "requestJson.customer", "requestJson.customer.name" }),
            };

            control.Bind(new CheckDataBlock(), variables);
            var table = control.Controls.OfType<TableLayoutPanel>().Single();
            var filterPanel = GetEditor<Control>(table, "Фильтр");
            var filterBox = Descendants(filterPanel).OfType<TextBox>().Single();
            var suggestions = Descendants(filterPanel).OfType<ListBox>().Single();

            filterBox.Text = string.Empty;
            filterBox.SelectionStart = 0;
            filterBox.AppendText("{$requestJson.");

            suggestions.Items.Cast<object>()
                .Select(item => item.ToString())
                .Should()
                .Contain("requestJson.customer.name");
        });
    }

    private static T GetEditor<T>(TableLayoutPanel table, string labelText)
        where T : Control
    {
        var label = table.Controls
            .OfType<Label>()
            .Single(item => item.Text == labelText);
        var row = table.GetRow(label);
        return table.GetControlFromPosition(1, row) as T
               ?? throw new InvalidOperationException($"Редактор для '{labelText}' не найден.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
