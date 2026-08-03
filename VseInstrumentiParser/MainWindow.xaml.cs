using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace VseInstrumentiParser;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string downloadFolder = @"C:\Users\user\Downloads";
    private readonly string imageResizerExeDir;
    private readonly ViParser parser = new(downloadFolder);
    private readonly SqlProductFormatter sqlFormatter = new();
    private readonly FtpUploader ftpUploader;
    private readonly DbQueryRunner dbQueryRunner;
    private readonly ObservableCollection<string> logItems = new();
    private readonly BrowserSimulator browserSimulator = new();
    public MainWindow()
    {
        InitializeComponent();
        ftpUploader = App.AppHost.Services.GetRequiredService<FtpUploader>();
        dbQueryRunner = App.AppHost.Services.GetRequiredService<DbQueryRunner>();
        imageResizerExeDir = App.AppHost.Services.GetRequiredService<IConfiguration>()["ImageResizerExeDir"];
        lbLogs.ItemsSource = logItems;

    }

    private async void btnParseFromHtml_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        DeletePrevFiles();

        var desc = await parser.ParseFromHtml(Clipboard.GetText(), txtModelName.Text, new Progress<string>((v) =>
        {
            Title = v;
            this.AddLogEntry(v);
        }));

        txtDescription.Text = desc.ToString();
        txtModelName.Text = "";
        string lastSql = "";

        var swGlobal = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        if (btnGenerateSql.IsChecked == true)
        {
            AddLogEntry("Начало генерации SQL");
            lastSql = sqlFormatter.GenerateSqlForLastParsedProduct(parser, txtImageManufacturerPath.Text);
            Clipboard.SetText(lastSql);
            AddLogEntry($"Конец генерации SQL", sw);

        }

        if (chkResizeImages.IsChecked == true)
        {
            sw.Restart();
            AddLogEntry("Начало изменения размера изображений");
            await ResizeImages();
            AddLogEntry($"Конец изменения размера изображений", sw);

        }

        if (btnUploadToFtp.IsChecked == true && parser.LastImagesData != null && parser.LastImagesData.Length > 0)
        {
            sw.Restart();
            AddLogEntry("Начало загрузки изображений на FTP");
            await ftpUploader.UploadImages(txtImageManufacturerPath.Text,
                parser.LastImagesData.Select(img => Path.Combine(downloadFolder, img)),
                new Progress<double>((v) => Title = $"Uploading images: {v:P2}"));
            AddLogEntry($"Конец загрузки изображений на FTP", sw);
        }

        if (btnGenerateSql.IsChecked == true && chkAutoRunSql.IsChecked == true && !string.IsNullOrWhiteSpace(lastSql))
        {
            sw.Restart();
            AddLogEntry("Начало выполнения SQL");

            await dbQueryRunner.ValidateModelName(parser.LastModelData);
            await dbQueryRunner.Execute(lastSql);
            AddLogEntry($"Конец выполнения SQL", sw);
        }

        AddLogEntry($"Парсинг {parser.LastModelData ?? "???"} завершен", swGlobal);
        IsEnabled = true;

        if (chkPlayBeep.IsChecked == true)
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    private void DeletePrevFiles()
    {
        var prevFiles = Directory.GetFiles(downloadFolder, "*.jpg");
        foreach (var file in prevFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                AddLogEntry($"Не удалось удалить файл {file}: {ex.Message}");
            }
        }
    }

    private void AddLogEntry(string v, Stopwatch sw = null)
    {
        string msg = $"{DateTime.Now} {v}";
        if (sw != null)
        {
            msg += $" (заняло {(int)sw.Elapsed.TotalMilliseconds} ms)";
        }
        logItems.Insert(0, msg);
    }

    private async Task ResizeImages()
    {
        await Task.Run(async () =>
        {
            string resolution = "1000x1000";
            string exe = $@"{imageResizerExeDir}\ImageCompressorApp.exe";
            var pi = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = $"{downloadFolder} {resolution}",
                WorkingDirectory = imageResizerExeDir,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            var p = Process.Start(pi);
            await p.WaitForExitAsync();
        });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(App.SETTINGS_FILE_NAME))
        {
            File.WriteAllText(App.SETTINGS_FILE_NAME, "catalog/manufacturer/products");
        }
        txtImageManufacturerPath.Text = File.ReadAllText(App.SETTINGS_FILE_NAME);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        File.WriteAllText(App.SETTINGS_FILE_NAME, txtImageManufacturerPath.Text);
    }

    private async void btnSeleniumSearch_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        await browserSimulator.SearchNext("36951-235-30", "Kraftool");
        IsEnabled = true;
    }

    private async void btnSeleniumOpen_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        await browserSimulator.OpenBrowser();
        IsEnabled = true;
    }

}
