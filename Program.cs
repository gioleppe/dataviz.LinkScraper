using System.Globalization;
using CsvHelper;
using PuppeteerSharp;

const int pageSize = 30;
var totalRestaurants = int.MaxValue;

if (args.Length < 2)
{
    Console.WriteLine("Please use as $parser <url> <csvFileName>");
}

var URL = args[0];
var csvFilename = args[1];

async Task<IPage> SetupAndNavigate(string florenceUrl)
{
    var options = new LaunchOptions()
    {
        Headless = true,
        ExecutablePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
    };

    var browser = await Puppeteer.LaunchAsync(options);
    var page3 = await browser.NewPageAsync();

    await page3.GoToAsync(florenceUrl);
    return page3;
}

async Task GoToNextPage(IPage page2)
{
    const string nextPage = $@"document.getElementsByClassName('next')[0].click();";
    await page2.EvaluateExpressionAsync(nextPage);
}

async Task<bool> ExtractData(IEnumerable<int> rankings, IPage page1, ISet<Restaurant> hashSet)
{
    foreach (var ranking in rankings)
    {
        if (ranking >= totalRestaurants)
            return false;
        var link = $@"document.querySelector('[data-test=\'{ranking}_list_item\']').getElementsByTagName('a')[0].href;";
        var url = await page1.EvaluateExpressionAsync<string>(link);
        var restaurantRankAndName = $@"document.querySelector('[data-test=\'{ranking}_list_item\']').getElementsByTagName('a')[1].innerText;";
        var rankAndName = (await page1.EvaluateExpressionAsync<string>(restaurantRankAndName)).Split(".");
        var restaurant = new Restaurant(rankAndName[1].TrimStart(), int.Parse(rankAndName[0]), url);
        hashSet.Add(restaurant);
    }

    return true;
}

async Task WaitForAllSelectors(IEnumerable<int> rankings, IPage page)
{
    foreach (var ranking in rankings)
    {
        var selector = $"[data-test=\'{ranking}_list_item\']";
        
        try
        {
            await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
            {
                Timeout = 5000
            });
        }
        catch (WaitTaskTimeoutException)
        {
            Console.WriteLine($"Not finding selector for ranking {ranking}, scraping is over!");
            totalRestaurants = ranking;
            break;
        }
    }
}

Console.WriteLine("Setting up headless Chrome");
var page = await SetupAndNavigate(URL);

Console.WriteLine("Finding Restaurant Links");
var restaurantsData = new HashSet<Restaurant>();

// main loop
var visitedPages = 0;
while (true)
{
    Thread.Sleep(TimeSpan.FromSeconds(1));

    var firstRanking = 1 + (visitedPages * pageSize);
    Console.WriteLine($"Scraping page number {visitedPages + 1}, starting from ranking {firstRanking}");
    var rankings = Enumerable.Range(firstRanking, pageSize);

    await WaitForAllSelectors(rankings, page);

    if (!await ExtractData(rankings, page, restaurantsData))
        break;

    await GoToNextPage(page);
    visitedPages++;
}

Console.WriteLine($"Finished scraping, writing scraped data to disk");

await using var writer = new StreamWriter(csvFilename);
await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

csv.WriteRecords(restaurantsData);

internal sealed record Restaurant(string Name, int Ranking, string Url);