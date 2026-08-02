namespace VseInstrumentiParser;

using System.IO;
using System.Net;

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
    public async Task UploadImages(string rootFolder, IEnumerable<string> images, IProgress<double>? progress = null)
    {
        if (images == null) throw new ArgumentNullException(nameof(images));

        var imageList = images
            .ToList();

        if (imageList.Count == 0)
            return;

        // Normalize host
        var hostWithoutScheme = _host;
        if (hostWithoutScheme.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            hostWithoutScheme = hostWithoutScheme.Substring("ftp://".Length);
        if (hostWithoutScheme.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
            hostWithoutScheme = hostWithoutScheme.Substring("ftps://".Length);

        // Ensure rootFolder trimmed
        var remoteFolder = (rootFolder ?? string.Empty).Trim('/');

        for (int i = 0; i < imageList.Count; i++)
        {
            var localPath = imageList[i];
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Local file not found: {localPath}", localPath);

            var fileName = Path.GetFileName(localPath);
            var relativePath = $"image/{remoteFolder}/{fileName}";

            var uri = new Uri($"ftp://{hostWithoutScheme}/{relativePath}");

            // Upload using FtpWebRequest   
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(_login, _password);
            request.UseBinary = true;
            request.UsePassive = true;
            request.KeepAlive = false;

            // Read file and write to request stream
            using (var fileStream = File.OpenRead(localPath))
            using (var requestStream = await request.GetRequestStreamAsync())
            {
                await fileStream.CopyToAsync(requestStream);
            }

            // Get response to complete the upload
            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            {
                // Optionally we could inspect response.StatusDescription
            }

            progress?.Report((i + 1) / (double)imageList.Count);
        }
    }
}

