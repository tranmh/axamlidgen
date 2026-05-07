using Avalonia.Controls;
using SampleApp.Generated;
using SampleApp.Views;
using Xunit;

namespace SampleApp.Headless;

public class AutomationIdLookupTests
{
    [Fact]
    public void LoginView_exposes_email_field()
    {
        AvaloniaTestApp.RunOnUi(() =>
        {
            var view = new LoginView();
            var window = new Window { Content = view };
            window.Show();

            var found = AutomationIdLookup.FindByAutomationId(view, AutomationIds.LoginView_Email_Field);
            Assert.NotNull(found);
            Assert.IsType<TextBox>(found);
        });
    }

    [Fact]
    public void LoginView_exposes_synthesized_submit_button()
    {
        AvaloniaTestApp.RunOnUi(() =>
        {
            var view = new LoginView();
            var window = new Window { Content = view };
            window.Show();

            var found = AutomationIdLookup.FindByAutomationId(view, AutomationIds.LoginView_Button_Submit);
            Assert.NotNull(found);
            Assert.IsType<Button>(found);
        });
    }

    [Fact]
    public void SettingsView_synthesised_DarkMode_toggle_resolves()
    {
        AvaloniaTestApp.RunOnUi(() =>
        {
            var view = new SettingsView();
            var window = new Window { Content = view };
            window.Show();

            var found = AutomationIdLookup.FindByAutomationId(view, AutomationIds.SettingsView_ToggleSwitch_DarkMode);
            Assert.NotNull(found);
            Assert.IsType<ToggleSwitch>(found);
        });
    }

    [Fact]
    public void Every_LoginView_id_in_manifest_resolves_to_a_control()
    {
        AvaloniaTestApp.RunOnUi(() =>
        {
            var view = new LoginView();
            var window = new Window { Content = view };
            window.Show();

            string[] expected =
            {
                AutomationIds.LoginView_Email_Field,
                AutomationIds.LoginView_Password_Field,
                AutomationIds.LoginView_Button_Submit,
                AutomationIds.LoginView_Button_Cancel,
                AutomationIds.LoginView_Error_Banner,
            };

            foreach (var id in expected)
            {
                var found = AutomationIdLookup.FindByAutomationId(view, id);
                Assert.NotNull(found);
            }
        });
    }

    [Fact]
    public void Unknown_AutomationId_returns_null()
    {
        AvaloniaTestApp.RunOnUi(() =>
        {
            var view = new LoginView();
            var window = new Window { Content = view };
            window.Show();
            Assert.Null(AutomationIdLookup.FindByAutomationId(view, "DoesNotExist"));
        });
    }
}
