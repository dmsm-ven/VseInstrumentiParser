namespace VseInstrumentiParser;

using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

public class FtpConnectionInfo
{
    public string Host { get; set; }
    public string Login { get; set; }
    public string Password { get; set; }
}
public class FtpUploader
{
    private readonly string _host;
    private readonly string _login;
    private readonly string _password;

    public FtpUploader(FtpConnectionInfo conn)
    {
        this._host = conn.Host;
        this._login = conn.Login;
        this._password = conn.Password;
    }
    public async Task UploadImages(string rootFolder, IEnumerable<string> images, IProgress<double>? progress = null, int maxDegreeOfParallelism = 4)
    {
        if (images == null) throw new ArgumentNullException(nameof(images));

        var imageList = images.ToList();
        if (imageList.Count == 0) return;

        // Normalize host
        var hostWithoutScheme = _host;
        if (hostWithoutScheme.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            hostWithoutScheme = hostWithoutScheme.Substring("ftp://".Length);
        if (hostWithoutScheme.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
            hostWithoutScheme = hostWithoutScheme.Substring("ftps://".Length);

        // Ensure rootFolder trimmed
        var remoteFolder = (rootFolder ?? string.Empty).Trim('/');

        // Increase connection limit to allow parallel uploads
        try
        {
            ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, maxDegreeOfParallelism * 2);
        }
        catch
        {
            // ignore platforms that don't allow changing this
        }

        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task>(imageList.Count);
        int completed = 0;

        foreach (var localPath in imageList)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Local file not found: {localPath}", localPath);

            await semaphore.WaitAsync().ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var fileName = Path.GetFileName(localPath);
                    var relativePath = $"image/{remoteFolder}/{fileName}";
                    var uri = new Uri($"ftp://{hostWithoutScheme}/{relativePath}");

                    var request = (FtpWebRequest)WebRequest.Create(uri);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(_login, _password);
                    request.UseBinary = true;
                    request.UsePassive = true;
                    request.KeepAlive = true; // allow connection reuse where possible

                    // Open file and set content length before getting request stream
                    using (var fileStream = File.OpenRead(localPath))
                    {
                        try
                        {
                            request.ContentLength = fileStream.Length;
                        }
                        catch
                        {
                            // Some servers may not require ContentLength; ignore if setting fails
                        }

                        using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                        {
                            // Use a larger buffer for faster transfers
                            await fileStream.CopyToAsync(requestStream, 64 * 1024).ConfigureAwait(false);
                        }
                    }

                    using (var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        // no-op; ensures the upload finalized
                    }
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    progress?.Report(done / (double)imageList.Count);
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}

