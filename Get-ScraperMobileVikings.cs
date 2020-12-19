using System;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Scraper
{
    [Cmdlet(VerbsCommon.Get,"ScraperMobileVikings")]
    [OutputType(typeof(FavoriteStuff))]
    public class GetScraperMobileVikings : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public string EmailOrPhonenumber { get; set; }

        [Parameter(Mandatory = true)]
        public string Password { get; set; }

        [Parameter]
        public SwitchParameter DontIncludePoints { get; set; }

        [Parameter]
        public SwitchParameter ShowWindow { get; set; }

        protected override void BeginProcessing()
        {
        }

        protected override void ProcessRecord()
        {
            var result = Run().Result;
            WriteObject(result);
        }

        private async Task<MobileVikings> Run()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = !ShowWindow
            }))
            using(var page = await browser.NewPageAsync())
            {
                await page.GoToAsync("https://mobilevikings.be/nl/my-viking/login");

                // Cookies
                var cookie = await page.WaitForSelectorAsync(
                    "body > div:nth-child(1) > div:nth-child(1) > div.cookieWall.isVisible > div > div.cookieWall-ctas > button:nth-child(1)");
                await cookie.TapAsync();

                // Login
                var loginField = await page.WaitForSelectorAsync("#loginName__0");
                await loginField.TypeAsync(EmailOrPhonenumber);
                var passwordField = await page.WaitForSelectorAsync("#password__1");
                await passwordField.TypeAsync(Password);
                var loginButton = await page.WaitForSelectorAsync(
                    "body > div:nth-child(1) > div:nth-child(3) > div > div > main > section > div.loginBox > form > footer > button");
                System.Threading.Thread.Sleep(3000);
                await loginButton.TapAsync();
                await page.WaitForNavigationAsync();

                // Get credits
                var creditField = await page.WaitForSelectorAsync(
                    "body > div:nth-child(1) > div:nth-child(3) > div > div > main > div > section > div > section > section.MvSection.balanceItemSection > div > div > div:nth-child(2) > strong");
                var creditText = await creditField.EvaluateFunctionAsync<string>("e => e.innerText");
                var creditMatch = Regex.Match(creditText, @"^[^\d]*(\d+)[,\.](\d+)$");
                decimal credits;
                if (creditMatch.Success) {
                    creditText = creditMatch.Groups[1].Value + CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator + (creditMatch.Groups[2].Value ?? "0");
                    credits = decimal.Parse(creditText, CultureInfo.InvariantCulture.NumberFormat);
                } else {
                    throw new Exception($"Credits not found ({creditText})");
                }

                // Get points
                System.Threading.Thread.Sleep(3000);
                decimal? points = null;
                if (!DontIncludePoints)
                {
                    await page.GoToAsync("https://mobilevikings.be/en/my-viking/viking-points");
                    Match pointMatch = null;
                    string pointText = "";
                    for (int i = 0; i < 10 && (pointMatch == null || !pointMatch.Success); i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        var pointField = await page.WaitForSelectorAsync(
                            "body > div:nth-child(1) > div:nth-child(3) > div > div > main > div > section.vikingPointsView__header.vikingPointsViewHeader > div.vikingPointsViewHeader__details.vikingPointsViewHeaderItem.available > div");
                        pointText = await pointField.EvaluateFunctionAsync<string>("e => e.innerText");
                        pointMatch = Regex.Match(pointText, @"^[^\d]*(\d+)[,\.](\d+)$");
                    }
                    if (pointMatch.Success) {
                        pointText = pointMatch.Groups[1].Value + CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator + (pointMatch.Groups[2].Value ?? "0");
                        points = decimal.Parse(pointText, CultureInfo.InvariantCulture.NumberFormat);
                    } else {
                        throw new Exception($"Points not found ({pointText})");
                    }
                }

                // Bundle
                return new MobileVikings { 
                    Credits = credits,
                    Points = points
                };
            }
        }

        protected override void EndProcessing()
        {
        }
    }

    public class MobileVikings
    {
        public decimal Credits { get; set; }
        public decimal? Points { get; set; }
    }
}
