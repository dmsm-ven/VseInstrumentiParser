using OpenQA.Selenium;

namespace VseInstrumentiParser;

public class BrowserSimulator
{
    IWebDriver driver;

    public async Task OpenBrowser()
    {
        throw new NotImplementedException();
        //ChromeOptions options = new ChromeOptions();
        //
        //// Добавляем параметры
        //options.AddArgument("--start-maximized");
        //options.AddArgument("--profile-directory=Profile 2");
        //options.AddArgument(@"--user-data-dir=C:\Users\user\AppData\Local\Google\Chrome\User Data");
        //
        //driver = new ChromeDriver(options);
        //driver.Navigate().GoToUrl("https://www.vseinstrumenti.ru/");
    }

    public async Task SearchNext(string modelOrSku, string manufacturerName)
    {
        throw new NotImplementedException();
        /*var searchBox = driver.FindElement(By.XPath("//input[@data-qa='header-search-input']"));
        searchBox.Clear();
        searchBox.SendKeys(modelOrSku);

        await Task.Delay(TimeSpan.FromSeconds(1.25));

        var searchList = driver.FindElements(By.XPath("//div[@data-qa='header-search-results']/div/div[@data-qa='header-search-results-item']/div/div/a"));

        foreach (var link in searchList)
        {
            var linkText = link.Text.Trim();
            if (linkText.EndsWith(modelOrSku, StringComparison.OrdinalIgnoreCase) &&
                linkText.Contains(manufacturerName, StringComparison.OrdinalIgnoreCase))
            {
                link.Click();
            }
        }*/
    }
}
