using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SampleApp.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
