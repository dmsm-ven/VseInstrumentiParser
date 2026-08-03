using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Text;

namespace VseInstrumentiParser;

public class ViParser
{
    private readonly HttpClient client;
    private readonly string downloadFolder;
    private bool isFirstRun = true;

    public string LastParseDescriptionHtml { get; private set; }
    public DimensionsData LastDimensionsData { get; private set; }
    public string[] LastImagesData { get; private set; }
    public string LastModelData { get; private set; }

    public ViParser(string downloadFolder)
    {
        client = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            CookieContainer = new System.Net.CookieContainer(),
            UseCookies = true
        });
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Sec-ch-Ua", "\"Not;A=Brand\";v=\"8\", \"Chromium\";v=\"150\", \"Google Chrome\";v=\"150\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-arch", "\"x86\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-bitness", "\"64\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-full-version", "\"150.0.7871.187\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-full-version-list", "\"Not;A=Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"150.0.7871.187\", \"Google Chrome\";v=\"150.0.7871.187\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        client.DefaultRequestHeaders.Add("sec-ch-ua-model", "\"\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        client.DefaultRequestHeaders.Add("sec-ch-ua-platform-version", "\"19.0.0\"");
        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
        client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
        client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
        client.DefaultRequestHeaders.Add("Pragma", "no-cache");
        client.DefaultRequestHeaders.Add("priority", "u=0, i");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.Add("Referer", "https://www.vseinstrumenti.ru/product/rafter-universalnyj-krovelnyj-ugolnik-305-mm-5-v-1-stayer-34306-30-8355687/");
        client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br, zstd");
        client.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9,ru;q=0.8");
        client.DefaultRequestHeaders.Add("cache-control", "no-cache");
        this.downloadFolder = downloadFolder;
    }

    internal async Task<string> ParseFromHtml(string html,
        string model = null,
        IProgress<string> progress = null)
    {
        ClearLastParsedData();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        if (string.IsNullOrWhiteSpace(model))
        {
            model = doc.DocumentNode.SelectSingleNode("//h1").InnerText.Split(' ').Last().Trim();
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length < 3)
        {
            throw new FormatException("Model name is invalid or too short.");
        }

        LastModelData = model;

        var images = doc.DocumentNode.SelectNodes("//div[@data-qa='carousel-image']/div/img")
            .Select(img => img.GetAttributeValue("src", ""))
            .Where(img => !string.IsNullOrEmpty(img) && !img.StartsWith("data"))
            .Select(img => img.Replace("/68x60/", "/1200x800/"))
            .ToArray();

        LastImagesData = new string[images.Length];

        await DownloadImages(model, images).ConfigureAwait(false);

        progress?.Report($"Загружены изображения: {images.Length}");

        string description = BuildDescription(doc, model);

        return description;
    }

    private async Task DownloadImages(string model, string[] images)
    {
        string pathModel = model.Replace("/", "_").Replace("\\", "_");

        // Download images in parallel with limited concurrency to improve throughput
        var imageCount = images.Length;
        var maxDegreeOfParallelism = Math.Min(8, Math.Max(2, Environment.ProcessorCount));
        var semaphore = new System.Threading.SemaphoreSlim(maxDegreeOfParallelism);
        var downloadTasks = new List<Task>(imageCount);

        for (int i = 0; i < imageCount; i++)
        {
            var index = i; // capture
            var imageUrl = images[index];

            if (!string.IsNullOrEmpty(imageUrl))
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var bytes = await client.GetByteArrayAsync(imageUrl).ConfigureAwait(false);
                        var fileName = Path.Combine(downloadFolder, $"{pathModel}_{index + 1}{Path.GetExtension(imageUrl)}");
                        await File.WriteAllBytesAsync(fileName, bytes).ConfigureAwait(false);
                        LastImagesData[index] = Path.GetFileNameWithoutExtension(fileName) + ".jpg";
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            else
            {
                LastImagesData[index] = string.Empty;
            }
        }

        await Task.WhenAll(downloadTasks).ConfigureAwait(false);
    }

    private void ClearLastParsedData()
    {
        LastDimensionsData = null;
        LastImagesData = null;
        LastModelData = null;
        LastParseDescriptionHtml = null;
    }

    private string BuildDescription(HtmlDocument doc, string model)
    {
        var cardRoot = doc.DocumentNode.SelectSingleNode("//nav[@data-qa='cart-navigation']/following-sibling::div/div");
        string shortDescription = cardRoot.SelectSingleNode("./div/div").InnerHtml.Trim();
        if (shortDescription.StartsWith("<h3")) // битое описание, его реально нет
        {
            shortDescription = "";
        }

        string features = "";
        if (cardRoot.SelectSingleNode(".//p[contains(text(), 'Преимущества')]/following-sibling::div") != null)
        {
            features = cardRoot.SelectSingleNode(".//p[contains(text(), 'Преимущества')]/following-sibling::div").InnerHtml.Trim();
        }
        var characteristics = new List<(string Name, string Value)>();

        if (cardRoot.SelectSingleNode(".//div[@data-qa='product-card-characteristics']//div[@data-qa='specification-item']") != null)
        {
            var charRows = cardRoot.SelectNodes(".//div[@data-qa='product-card-characteristics']//div[@data-qa='specification-item']");


            foreach (var div in charRows)
            {
                string name = div.SelectSingleNode(".//span[@data-qa='specification-item-name']").InnerText.Trim();
                string value = div.SelectSingleNode(".//*[@data-qa='specification-item-value']").InnerText.Trim();
                characteristics.Add((name, value));
            }
        }

        var complectationNode = cardRoot.SelectSingleNode(".//p[contains(@class, '_heading_') and contains(text(), 'Комплектация')]");
        string complectation = "";
        if (complectationNode != null)
        {
            complectation = complectationNode.SelectSingleNode("./following-sibling::div/ul").OuterHtml.Trim();
        }

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(shortDescription))
        {
            sb.AppendLine(shortDescription);
        }
        if (characteristics.Any())
        {
            sb.AppendLine("<p><strong>Технические характеристики</strong></p>");
            sb.AppendLine("<div class=\"table-responsive\">");
            sb.AppendLine("<table class=\"table\">");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr><th>Характеристика</th><th>Значение</th></tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            foreach (var (name, value) in characteristics)
            {
                sb.AppendLine($"<tr><td>{name}</td><td>{value}</td></tr>");
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</div>");
        }
        if (!string.IsNullOrWhiteSpace(features))
        {
            sb.AppendLine("<p><strong>Преимущества</strong></p>");
            sb.AppendLine(features);
        }

        if (!string.IsNullOrWhiteSpace(complectation))
        {
            sb.AppendLine("<p><strong>Комплектация</strong></p>");
            sb.Append(complectation);
        }

        var dimRoot = cardRoot.SelectSingleNode(".//h3[contains(text(), 'Информация об упаковке')]");
        if (dimRoot != null)
        {
            var weight = dimRoot.SelectSingleNode("./following-sibling::p[2]")?.InnerText.Trim().Replace("Вес, кг: ", "");
            var length = dimRoot.SelectSingleNode("./following-sibling::p[3]")?.InnerText.Trim().Replace("Длина, мм: ", "");
            var width = dimRoot.SelectSingleNode("./following-sibling::p[4]")?.InnerText.Trim().Replace("Ширина, мм: ", "");
            var height = dimRoot.SelectSingleNode("./following-sibling::p[5]")?.InnerText.Trim().Replace("Высота, мм: ", "");

            var dimData = DimensionsData.Parse(weight, length, width, height);

            LastDimensionsData = dimData;
        }

        LastParseDescriptionHtml = sb.ToString();

        return LastParseDescriptionHtml;
    }

    private async Task<HtmlDocument> GetDocument(string uri)
    {

        if (isFirstRun)
        {
            isFirstRun = false;
            var initialResponse = await client.GetAsync(uri);
        }

        var response = await client.GetAsync(uri);
        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }
}
