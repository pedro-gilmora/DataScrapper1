using Microsoft.AspNetCore.SignalR;
using Microsoft.Playwright;

namespace FreightningScrapper;

public static class Scrappers
{
    internal static async Task<List<StatusHistory>> GetMinimaxStatusHistoryAsync(IPage page, string trackingNumber)
    {
        var results = new List<StatusHistory>();
        var attempts = 3;

        try
        {
            AppLogger.Info("Navigating source page...");

            await page.GotoAsync("https://minimax.tracking.dtms.ca");

        SEARCH:

            try
            {
                AppLogger.Info($"Filling in tracking number with {trackingNumber}...");

                if (await page.WaitForSelectorAsync("input#mat-input-2") is not { } inputField)
                {
                    AppLogger.Error("Could not find input fields.");
                    return results;
                }

                await inputField.FillAsync(trackingNumber);

                await page.ClickAsync("button[mattooltip=\"Click to search\"]");

            }
            catch
            {
                if (--attempts == 0) {
                    throw;
                }
                goto SEARCH;
            }

        SCRAP:
            try
            {
                // Wait for the "Historique du Status" section to appear
                AppLogger.Info("Waiting for 'Status:' section to load...");

                if (await page.WaitForSelectorAsync("xpath=//div[contains(@class, 'allign-right') and contains(text(), 'Status:')]") is not { } statusSectionHeader)
                {
                    AppLogger.Warn("Status history section not found.");
                    return results;
                }

                var status = await statusSectionHeader.EvaluateAsync<string>("n => n.nextElementSibling.innerText");

                AppLogger.Info($"Status: {status}");

                AppLogger.Info(await (await page.WaitForSelectorAsync("tbody[role=\"presentation\"]"))!.TextContentAsync() ?? "Not found");

                // Query rows inside the correct section
                var statusRows = await page.QuerySelectorAllAsync("tbody[role=\"presentation\"] tr:not(:first-child):not(.dx-freespace-row)");
                
                foreach (var row in statusRows)
                {
                    var cells = await row.QuerySelectorAllAsync("td");
                    var date = await cells[1].InnerTextAsync();
                    var time = await cells[2].InnerTextAsync();
                    status = await cells[7].InnerTextAsync();
                    var location = await cells[9].InnerTextAsync();
                    results.Add(new StatusHistory($"{date} {time}", status, location));
                    AppLogger.Info($"Row: {date} {time} | {status}");
                }

                if (results.Count == 0)
                {
                    AppLogger.Warn("Table loaded, but no status rows found.");
                }
                else
                {
                    AppLogger.Success($"Successfully extracted {results.Count} status entries.");
                }
            }
            catch
            {
                if (--attempts == 0) {
                    throw;
                }
                goto SCRAP;
            }

            return results;
        }
        catch (PlaywrightException pex)
        {
            AppLogger.Error($"Playwright error: {pex.Message}");
            return results;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Unexpected error: {ex.Message}");
            return results;
        }
    }
}