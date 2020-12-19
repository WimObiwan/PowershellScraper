using System;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Scraper
{
    [Cmdlet(VerbsCommon.Get,"ScraperEtable")]
    [OutputType(typeof(FavoriteStuff))]
    public class GetScraperEtable : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public string Username { get; set; }

        [Parameter(Mandatory = true)]
        public string Password { get; set; }

        [Parameter]
        public string StudentName { get; set; }

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

        private async Task<Etable> Run()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = !ShowWindow
            }))
            using(var page = await browser.NewPageAsync())
            {
                await page.GoToAsync("https://e-table.be/admin/login-v2.php");

                // Cookies
                var cookie = await page.WaitForSelectorAsync(
                    "#username");
                await cookie.TapAsync();

                // Login
                var loginField = await page.WaitForSelectorAsync("#username");
                await loginField.TypeAsync(Username);
                var passwordField = await page.WaitForSelectorAsync("#password");
                await passwordField.TypeAsync(Password);
                var loginButton = await page.WaitForSelectorAsync(
                    "#login-form > div:nth-child(3) > div:nth-child(1) > div > button");
                await loginButton.TapAsync();
                await page.WaitForNavigationAsync();

                await page.GoToAsync("https://e-table.be/admin/select-student.php");
                // System.Threading.Thread.Sleep(6000);
                //await page.WaitForNavigationAsync();
                await page.WaitForSelectorAsync("#student1");
                string link = null;
                for (int i = 0; i < 4; i++) {
                    var studentSelector = await page.WaitForSelectorAsync($"#student{i+1}");
                    var nameField = await page.WaitForSelectorAsync($"#student{i+1} > a > div > h5");
                    var text = await nameField.EvaluateFunctionAsync<string>("e => e.innerText");
                    Console.WriteLine(text);
                    if (string.IsNullOrEmpty(StudentName) || string.Compare(text, StudentName, true) == 0)
                    {
                        Console.WriteLine(text);
                        var studentLink = await page.WaitForSelectorAsync($"#student{i+1} > a");
                        link = await studentLink.EvaluateFunctionAsync<string>("e => e.getAttribute('href')");
                        Console.WriteLine(link);
                        break;
                    }
                }

                await page.GoToAsync($"https://e-table.be/admin/{link}");

                var creditField = await page.WaitForSelectorAsync(
                    "#page-wrapper > div > div.row.bg-title > div.col-lg-6.col-md-6.col-sm-12.col-xs-12.text-xs-center > h5 > b:nth-child(2)");
                var creditText = await creditField.EvaluateFunctionAsync<string>("e => e.innerText");
                Console.WriteLine(creditText);

                var creditMatch = Regex.Match(creditText, @"^[^\d]*(\d+)[,\.](\d+)$");
                decimal credits;
                if (creditMatch.Success) {
                    creditText = creditMatch.Groups[1].Value + CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator + (creditMatch.Groups[2].Value ?? "0");
                    credits = decimal.Parse(creditText, CultureInfo.InvariantCulture.NumberFormat);
                } else {
                    throw new Exception($"Credits not found ({creditText})");
                }


                if (link == null)
                    throw new Exception("Student not found");

                

                if (ShowWindow)
                    System.Threading.Thread.Sleep(60000);

                // Bundle
                return new Etable { 
                    Credits = credits
                };
            }
        }

        protected override void EndProcessing()
        {
        }
    }

    public class Etable
    {
        public decimal Credits { get; set; }
    }
}
