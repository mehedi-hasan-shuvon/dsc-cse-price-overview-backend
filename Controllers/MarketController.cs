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

        [HttpGet("cse-sme-price")]
        public async Task<IActionResult> GetCseSMEPrice()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/sme_current_price";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // SME table ID is TABLE_2, not dataTable
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='TABLE_2']");
                if (table == null)
                    return NotFound("CSE SME Market Price table not found");

                var headers = table.SelectNodes(".//thead/tr/th")
                    ?.Select(th => th.InnerText.Trim())
                    .ToList();

                var rows = table.SelectNodes(".//tbody/tr")
                    ?.Select(tr =>
                        tr.SelectNodes("td")
                          .Select(td => td.InnerText.Trim())
                          .ToList()
                    )
                    .Where(r => r.Count > 0)
                    .ToList();

                return Ok(new { headers, rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("cse-atb-price")]
        public async Task<IActionResult> GetCseATBPrice()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/atb_current_price";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // ATB table ID is also TABLE_2
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='TABLE_2']");
                if (table == null)
                    return NotFound("CSE ATB Market Price table not found");

                var headers = table.SelectNodes(".//thead/tr/th")
                    ?.Select(th => th.InnerText.Trim())
                    .ToList();

                var rows = table.SelectNodes(".//tbody/tr")
                    ?.Select(tr =>
                        tr.SelectNodes("td")
                        ?.Select(td => td.InnerText.Trim())
                        .ToList()
                    )
                    .Where(r => r != null && r.Count > 0)
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
                var currentPriceResponse = await GetCseClosePrice() as OkObjectResult;
                var bondResponse = await GetCseBonds() as OkObjectResult;
                var smeResponse = await GetCseSMEPrice() as OkObjectResult;
                var atbResponse = await GetCseATBPrice() as OkObjectResult;

                if (currentPriceResponse == null && bondResponse == null && smeResponse == null && atbResponse == null)
                    return NotFound("No data found from CSE sources.");

                var allRows = new List<List<string>>();
                var headers = new List<string> { "SL", "STOCK CODE", "HIGH", "LOW", "CP" };
                int sl = 1;

                List<List<string>> FilterColumns(dynamic data)
                {
                    var filteredRows = new List<List<string>>();
                    var headerList = ((IEnumerable<object>)data.headers)
                                        .Select(h => h.ToString().Trim().ToUpper())
                                        .ToList();

                    int stockCodeIndex = headerList.FindIndex(h => h.Contains("STOCK CODE"));
                    int highIndex = headerList.FindIndex(h => h == "HIGH");
                    int lowIndex = headerList.FindIndex(h => h == "LOW");
                    int cpIndex = headerList.FindIndex(h => h == "CP" || h == "LTP");

                    foreach (var rowObj in (IEnumerable<object>)data.rows)
                    {
                        var row = ((IEnumerable<object>)rowObj).Select(r => r.ToString().Trim()).ToList();
                        if (row.Count == 0) continue;

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

                if (smeResponse?.Value != null)
                {
                    dynamic data = smeResponse.Value;
                    allRows.AddRange(FilterColumns(data));
                }

                if (atbResponse?.Value != null)
                {
                    dynamic data = atbResponse.Value;
                    allRows.AddRange(FilterColumns(data));
                }

                if (allRows.Count == 0)
                    return NotFound("No CSE data found.");

                return Ok(new { headers, rows = allRows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }



        [HttpGet("dse-trading-codes")]
        public async Task<IActionResult> GetDseTradingCodes()
        {
            try
            {
                string url = "https://www.dsebd.org/ltp_industry.php?area=88";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Find the table
                var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class,'shares-table')]");
                if (table == null)
                    return NotFound("No table found on DSE page");

                // Get table headers
                var tableHeaders = table.SelectSingleNode(".//tr").SelectNodes("th|td")
                                        .Select(x => x.InnerText.Trim())
                                        .ToList();

                int tradingCodeIndex = tableHeaders.FindIndex(h => h.Equals("TRADING CODE", StringComparison.OrdinalIgnoreCase));
                if (tradingCodeIndex == -1)
                    return NotFound("TRADING CODE column not found");

                // Extract trading codes
                var tradingCodes = table.SelectNodes(".//tr").Skip(1)
                                        .Select(tr =>
                                        {
                                            var tds = tr.SelectNodes("td");
                                            if (tds == null || tradingCodeIndex >= tds.Count) return null;
                                            return tds[tradingCodeIndex].InnerText.Trim();
                                        })
                                        .Where(tc => !string.IsNullOrEmpty(tc))
                                        .ToList();

                return Ok(tradingCodes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("cse-bond-codes")]
        public async Task<IActionResult> GetCseBondCodes()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/bond_current_price";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Find the table by ID
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='TABLE_2']");
                if (table == null) return NotFound("CSE Table not found");

                // Get headers to identify Stock Code column
                var headers = table.SelectNodes(".//thead/tr/th")
                                   .Select(th => th.InnerText.Trim())
                                   .ToList();

                int stockCodeIndex = headers.FindIndex(h => h.Equals("Stock Code", StringComparison.OrdinalIgnoreCase));
                if (stockCodeIndex == -1) return NotFound("Stock Code column not found");

                // Extract stock codes
                var stockCodes = table.SelectNodes(".//tbody/tr")
                                      .Select(tr =>
                                      {
                                          var tds = tr.SelectNodes("td");
                                          if (tds == null || stockCodeIndex >= tds.Count) return null;
                                          return tds[stockCodeIndex].InnerText.Trim();
                                      })
                                      .Where(code => !string.IsNullOrEmpty(code))
                                      .ToList();

                return Ok(stockCodes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("dse-close-price")]
        public async Task<IActionResult> GetDseClosePrice()
        {
            try
            {
                var headers = new List<string> { "SL", "TRADING CODE", "HIGH", "LOW", "CLOSEP*" };
                var mergedRows = new List<List<string>>();
                int serial = 1;

                // ----- MAIN MARKET -----
                serial = await ExtractDseData("https://www.dsebd.org/dse_close_price.php", mergedRows, serial);

                // ----- SME MARKET -----
                serial = await ExtractDseData("https://sme.dsebd.org/sme_dse_close_price.php", mergedRows, serial);

                 // ATB MARKET
                 serial = await ExtractDseData("https://atb.dsebd.org/atb_close_price.php", mergedRows, serial);

                return Ok(new { headers, rows = mergedRows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpGet("dse-close-price-codes")]
        public async Task<IActionResult> GetDseClosePriceCodes()
        {
            try
            {
                // Call the existing method
                var result = await GetDseClosePrice() as OkObjectResult;
                if (result?.Value == null)
                    return NotFound("No data found from DSE Close Price.");

                // Extract the result object
                dynamic data = result.Value;

                // mergedRows = List<List<string>>
                var rows = (IEnumerable<object>)data.rows;

                var tradingCodes = new List<string>();

                foreach (var rowObj in rows)
                {
                    var row = ((IEnumerable<object>)rowObj).Select(x => x.ToString()).ToList();

                    // row structure = SL, TRADING CODE, HIGH, LOW, CLOSEP*
                    if (row.Count > 1)
                    {
                        tradingCodes.Add(row[1]);  // TRADING CODE = index 1
                    }
                }

                return Ok(tradingCodes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        /// <summary>
        /// Extracts DSE/SME table data (2nd table) and appends to final row list.
        /// Returns the updated serial number.
        /// </summary>
        private async Task<int> ExtractDseData(string url, List<List<string>> rows, int startingSerial)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Select the 2nd table
            var tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'shares-table')]");
            if (tables == null || tables.Count < 2)
                return startingSerial;

            var table = tables[1];

            // Extract header row
            var headerRow = table.SelectSingleNode(".//tr");
            var headerColumns = headerRow.SelectNodes("th|td")
                                         .Select(h => h.InnerText.Trim().ToUpper())
                                         .ToList();

            // Extract column indexes
            int tradingCodeIndex = headerColumns.FindIndex(h => h.Contains("TRADING"));
            int closeIndex = headerColumns.FindIndex(h => h.Contains("CLOSEP"));

            if (tradingCodeIndex < 0) tradingCodeIndex = 1;
            if (closeIndex < 0) closeIndex = 2;

            // Extract table rows
            var trNodes = table.SelectNodes(".//tr").Skip(1);
            if (trNodes == null) return startingSerial;

            int serial = startingSerial;
            foreach (var tr in trNodes)
            {
                var tds = tr.SelectNodes("td");
                if (tds == null || tds.Count == 0)
                    continue;

                string tradingCode = tds[tradingCodeIndex].InnerText.Trim();
                string closep = tds[closeIndex].InnerText.Trim().Replace(",", "");

                rows.Add(new List<string>
        {
            serial.ToString(),
            tradingCode,
            "0", // HIGH
            "0", // LOW
            closep
        });

                serial++;
            }

            return serial;
        }

        [HttpGet("cse-close-price")]
        public async Task<IActionResult> GetCseClosePrice()
        {
            try
            {
                string url = "https://www.cse.com.bd/market/close_price";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var rows = new List<List<string>>();
                var headers = new List<string> { "SL", "STOCK CODE", "HIGH", "LOW", "CP" };

                var rowDivs = doc.DocumentNode.SelectNodes("//div[contains(@class,'close_tabs_cont')]");
                if (rowDivs == null)
                    return NotFound("No CSE close price data found");

                foreach (var rowDiv in rowDivs)
                {
                    string sl = rowDiv.SelectSingleNode(".//div[@id='close_tab_1']")?.InnerText.Trim().Replace(",", "") ?? "";
                    string stockCode = rowDiv.SelectSingleNode(".//div[@id='close_tab_2']")?.InnerText.Trim() ?? "";
                    string cp = rowDiv.SelectSingleNode(".//div[@id='close_tab_4']")?.InnerText.Trim().Replace(",", "") ?? "";

                    rows.Add(new List<string>
            {
                sl,
                stockCode,
                "0", // HIGH
                "0", // LOW
                cp
            });
                }

                return Ok(new { headers, rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }




    }
}
