using System.Runtime.ExceptionServices;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using TestBuilder.UI;
using Xunit;

namespace TestBuilder.Tests.UI;

/// <summary>
/// Проверки окна сохранённых переменных.
/// </summary>
public sealed class VariablesFormTests
{
    [Fact]
    public void UpdateVariables_ShowsNamesAndFormattedJson()
    {
        RunOnSta(() =>
        {
            using var form = new VariablesForm();
            form.UpdateVariables(new Dictionary<string, object>
            {
                ["resp"] = JObject.Parse("""{"id":42,"name":"Ann"}"""),
                ["plain"] = "text",
            });

            var list = Descendants(form).OfType<ListBox>().Single();
            var valueBox = Descendants(form).OfType<TextBox>().Single();

            list.Items.Cast<object>()
                .Select(item => item.ToString())
                .Should()
                .BeEquivalentTo("plain", "resp");

            list.SelectedItem = "resp";
            valueBox.Text.Should().Contain("\"id\": 42");
            valueBox.Text.Should().Contain("\"name\": \"Ann\"");
        });
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
