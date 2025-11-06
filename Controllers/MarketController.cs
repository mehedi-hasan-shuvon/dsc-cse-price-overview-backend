using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace MarketScraper.Controllers
{
    [ApiController]
    [Route("api")]
    public class MarketController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public MarketController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }


[HttpGet("dse-table")]
public async Task<IActionResult> GetDseTable()
{
    try
    {
        string url1 = "https://www.dsebd.org/ltp_industry.php?area=88";
        string url2 = "https://www.dsebd.org/latest_share_price_scroll_l.php";

        var allRows = new List<List<string>>();
        var headers = new List<string> { "Serial", "TRADING CODE", "HIGH", "LOW", "CLOSEP*" };
        int serialCounter = 1;

        // Function to fetch table + header text
        async Task<(string headerText, List<List<string>> rows)> FetchTableAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class,'shares-table')]");
            if (table == null)
                return ("", new List<List<string>>());

            // Extract header text
            var h2Node = doc.DocumentNode.SelectSingleNode("//h2[contains(@class,'BodyHead') and contains(@class,'topBodyHead')]");
            string headerText = "";
            if (h2Node != null)
            {
                headerText = System.Text.RegularExpressions.Regex.Replace(h2Node.InnerText, @"\s+", " ").Trim();
            }

            // Identify columns
            var tableHeaders = table.SelectSingleNode(".//tr").SelectNodes("th|td")
                                    .Select(x => x.InnerText.Trim())
                                    .ToList();

            var requiredColumns = new[] { "TRADING CODE", "HIGH", "LOW", "CLOSEP*" };
            var columnIndexes = requiredColumns
                                .Select(h => tableHeaders.FindIndex(th => th.Equals(h, StringComparison.OrdinalIgnoreCase)))
                                .ToArray();

            // Extract rows
            var rows = table.SelectNodes(".//tr").Skip(1)
                            .Select(tr =>
                            {
                                var tds = tr.SelectNodes("td")?.ToList();
                                if (tds == null) return null;

                                var row = new List<string> { serialCounter.ToString() };
                                foreach (var idx in columnIndexes)
                                {
                                    if (idx >= 0 && idx < tds.Count)
                                        row.Add(tds[idx].InnerText.Trim());
                                    else
                                        row.Add("");
                                }
                                serialCounter++;
                                return row;
                            })
                            .Where(r => r != null)
                            .ToList();

            return (headerText, rows);
        }

        // Fetch both
        var (headerText1, rows1) = await FetchTableAsync(url1);
        var (headerText2, rows2) = await FetchTableAsync(url2);

        // Combine rows
        allRows.AddRange(rows1);
        allRows.AddRange(rows2);

        if (allRows.Count == 0)
            return NotFound("No DSE data found");

        // ✅ Final output format
        var result = new
        {
            headerText1,
            headerText2,
            headers,
            rows = allRows
        };

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = ex.Message });
    }
}




        [HttpGet("cse-bonds")]
        public async Task<IActionResult> GetCseBonds()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/bond_current_price";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var table = doc.DocumentNode.SelectSingleNode("//table[@id='TABLE_2']");
                if (table == null) return NotFound("CSE Table not found");

                // Get headers
                var headers = table.SelectNodes(".//thead/tr/th")
                    .Select(th => th.InnerText.Trim()).ToList();

                // Get rows
                var rows = table.SelectNodes(".//tbody/tr")
                    .Select(tr => tr.SelectNodes("td").Select(td => td.InnerText.Trim()).ToList())
                    .Where(r => r.Count > 0)
                    .ToList();

                return Ok(new { headers, rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpGet("cse-current-price")]
        public async Task<IActionResult> GetCseCurrentPrice()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/current_price";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Select the table
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='dataTable']");
                if (table == null) return NotFound("CSE Current Market Price table not found");

                // Get headers
                var headers = table.SelectNodes(".//thead/tr/th")
                    .Select(th => th.InnerText.Trim())
                    .ToList();

                // Get rows
                var rows = table.SelectNodes(".//tbody/tr")
                    .Select(tr => tr.SelectNodes("td")
                                    .Select(td => td.InnerText.Trim())
                                    .ToList())
                    .Where(r => r.Count > 0)
                    .ToList();

                return Ok(new { headers, rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("cse-merged")]
        public async Task<IActionResult> GetCseMergedAsync()
        {
            try
            {
                // Step 1: Call both endpoints internally
                var currentPriceResponse = await GetCseCurrentPrice() as OkObjectResult;
                var bondResponse = await GetCseBonds() as OkObjectResult;

                if (currentPriceResponse == null && bondResponse == null)
                    return NotFound("No data found from CSE sources.");

                var allRows = new List<List<string>>();
                var headers = new List<string> { "SL", "STOCK CODE", "HIGH", "LOW", "CP" };
                int sl = 1;

                // Helper function to filter rows
                List<List<string>> FilterColumns(dynamic data)
                {
                    var filteredRows = new List<List<string>>();
                    var headerList = ((IEnumerable<object>)data.headers).Select(h => h.ToString().Trim().ToUpper()).ToList();

                    // Find indexes of required columns
                    int stockCodeIndex = headerList.FindIndex(h => h.Contains("STOCK CODE"));
                    int highIndex = headerList.FindIndex(h => h == "HIGH");
                    int lowIndex = headerList.FindIndex(h => h == "LOW");
                    int cpIndex = headerList.FindIndex(h => h == "CP" || h == "LTP");

                    foreach (var rowObj in (IEnumerable<object>)data.rows)
                    {
                        var row = ((IEnumerable<object>)rowObj).Select(r => r.ToString().Trim()).ToList();
                        if (row.Count == 0) continue;

                        // Skip rows missing key data
                        if (stockCodeIndex < 0 || stockCodeIndex >= row.Count) continue;

                        var newRow = new List<string>
                {
                    sl.ToString(),
                    stockCodeIndex >= 0 && stockCodeIndex < row.Count ? row[stockCodeIndex] : "",
                    highIndex >= 0 && highIndex < row.Count ? row[highIndex] : "",
                    lowIndex >= 0 && lowIndex < row.Count ? row[lowIndex] : "",
                    cpIndex >= 0 && cpIndex < row.Count ? row[cpIndex] : ""
                };

                        filteredRows.Add(newRow);
                        sl++;
                    }

                    return filteredRows;
                }

                // Step 2: Merge both tables
                if (currentPriceResponse?.Value != null)
                {
                    dynamic data = currentPriceResponse.Value;
                    allRows.AddRange(FilterColumns(data));
                }

                if (bondResponse?.Value != null)
                {
                    dynamic data = bondResponse.Value;
                    allRows.AddRange(FilterColumns(data));
                }

                // Step 3: Return merged result
                if (allRows.Count == 0)
                    return NotFound("No CSE data found.");

                return Ok(new { headers, rows = allRows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }




    }
}
