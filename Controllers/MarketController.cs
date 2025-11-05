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
        // URLs
        string[] urls = new[]
        {
            "https://www.dsebd.org/ltp_industry.php?area=88",
            "https://www.dsebd.org/latest_share_price_scroll_l.php"
        };

        var allRows = new List<List<string>>();
        List<string> headers = null;
        int serialCounter = 1; // Serial number counter

        foreach (var url in urls)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Table
            var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class,'shares-table')]");
            if (table == null) continue;

            // Headers (take only from first table)
            if (headers == null)
            {
                headers = table.SelectSingleNode(".//tr").SelectNodes("th|td")
                    .Select(x => x.InnerText.Trim())
                    .ToList();
                headers.Add("Date");
            }

            // === Extract Date ===
            string date = "";
            var h2Node = doc.DocumentNode.SelectSingleNode("//h2[contains(@class,'BodyHead') and contains(@class,'topBodyHead')]");
            if (h2Node != null)
            {
                var h2Html = h2Node.InnerHtml;
                var h2Text = System.Text.RegularExpressions.Regex.Replace(h2Html, "<.*?>", "").Trim();
                h2Text = h2Text.Replace("&nbsp;", " ");

                var match = System.Text.RegularExpressions.Regex.Match(h2Text, @"on\s+([A-Za-z]+\s+\d{2},\s+\d{4})");
                if (match.Success) date = match.Groups[1].Value;
            }

            // Rows
            var rows = table.SelectNodes(".//tr").Skip(1)
                .Select(tr =>
                {
                    var row = tr.SelectNodes("td")?.Select(td => td.InnerText.Trim()).ToList() ?? new List<string>();
                    if (row.Count > 0)
                    {
                        row[0] = serialCounter.ToString(); // Replace first column with running serial
                        row.Add(date); // Add date at the end
                        serialCounter++;
                    }
                    return row;
                })
                .Where(r => r.Count > 0)
                .ToList();

            allRows.AddRange(rows);
        }

        if (headers == null) return NotFound("DSE tables not found");

        return Ok(new { headers, rows = allRows });
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
    }
}
