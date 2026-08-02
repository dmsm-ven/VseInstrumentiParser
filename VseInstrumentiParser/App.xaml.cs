using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Windows;

namespace VseInstrumentiParser;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IHost? AppHost { get; private set; }
    public App()
    {

        Assembly assembly = this.GetType().Assembly;

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddUserSecrets(assembly, optional: true);
            })
            .ConfigureServices(x =>
            {
                x.AddSingleton<FtpUploader>((service) =>
                {
                    var ftpConnection = service.GetRequiredService<IConfiguration>()
                        .GetSection("FtpData")
                        .Get<FtpConnectionInfo>();
                    return new FtpUploader(ftpConnection);
                });
                x.AddSingleton<DbQueryRunner>((service) =>
                {
                    var connectionString = service.GetRequiredService<IConfiguration>()
                        .GetConnectionString("etk_image_downloader");
                    return new DbQueryRunner(connectionString);
                });
            })
            .Build();
    }
}

