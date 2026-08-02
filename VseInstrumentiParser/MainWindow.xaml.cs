using System.Windows;

namespace VseInstrumentiParser;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ViParser parser = new(downloadFolder: @"C:\Users\user\Downloads");
    private readonly SqlProductFormatter sqlFormatter = new SqlProductFormatter();
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void btnParseFromHtml_Click(object sender, RoutedEventArgs e)
    {
        btnParseFromHtml.IsEnabled = false;
        var desc = await parser.ParseFromHtml(Clipboard.GetText(), txtModelName.Text, saveDimensions: (bool)chkSaveDimensions.IsChecked, new Progress<string>((v) => Title = v));
        txtDescription.Text = desc.ToString();
        btnParseFromHtml.IsEnabled = true;
        txtModelName.Text = "";

        if (btnGenerateSql.IsChecked == true)
        {
            sqlFormatter.GenerateSqlForLastParsedProduct(parser, txtImageManufacturerPath.Text);
        }

    }
}
