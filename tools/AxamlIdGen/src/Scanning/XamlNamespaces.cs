using System.Xml.Linq;

namespace AxamlIdGen.Scanning;

internal static class XamlNamespaces
{
    public const string XamlX = "http://schemas.microsoft.com/winfx/2006/xaml";
    public const string Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public const string Avalonia = "https://github.com/avaloniaui";
    public const string XamarinForms = "http://xamarin.com/schemas/2014/forms";

    public static readonly XName XAttrName = XName.Get("Name", XamlX);

    public static bool IsRecognizedDefault(XNamespace ns)
        => ns.NamespaceName == Wpf
        || ns.NamespaceName == Avalonia
        || ns.NamespaceName == XamarinForms;
}
