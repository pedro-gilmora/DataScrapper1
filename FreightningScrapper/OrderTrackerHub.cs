using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Playwright;

namespace FreightningScrapper;

public class OrderTrackerHub(/*ITrackingRepository trackingRepository*/) : Hub
{
    readonly ConcurrentDictionary<string, CancellationTokenSource> cancelByClients = [];

    // public async Task RegisterTrackingNumberAsync(string clientName, string[] trackingNumbers)
    // {
    //     if (string.IsNullOrWhiteSpace(clientName) || trackingNumbers.Length == 0)
    //     {
    //         throw new ArgumentException("Client name and tracking number cannot be null or empty.");
    //     }

    //     await trackingRepository.AddTrackingNumbersAsync(Context.ConnectionId, trackingNumbers);

    //     await UpdateConnectionIdAsync(Context.ConnectionId);

    //     AppLogger.Info($"Client {clientName} ({Context.ConnectionId}) is watching tracking number: {trackingNumbers}");
    // }

    public async Task UpdateConnectionIdAsync(string clientName, string[] trackingNumbers)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new ArgumentException("Client name cannot be null or empty.");
            }

            if (trackingNumbers.Length == 0)
            {
                AppLogger.Warn("No tracking numbers found for any clients.");
                return;
            }

            string connectionId = Context.ConnectionId;

            try
            {
                if (cancelByClients.TryRemove(clientName, out var cts))
                {
                    cts.Cancel();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error removing client {clientName}: {ex.Message}");
            }

            AppLogger.Info("Launching browser...");

            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Timeout = 60000,
                Headless = true
            });

            var context = await browser.NewContextAsync(new() { JavaScriptEnabled = true });
            var page = await context.NewPageAsync();

            try
            {
                CancellationTokenSource cancellator = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);

                cancelByClients.TryAdd(clientName, cancellator);
                try
                {
                    foreach (var trackingNumber in trackingNumbers)
                    {
                        AppLogger.Info($"Tracking number: {trackingNumber}");

                        var history = await Scrappers.GetMinimaxStatusHistoryAsync(page, trackingNumber);

                        if (history.Count > 0)
                        {
                            await Clients.Client(Context.ConnectionId).SendAsync("Update", new { trackingNumber, history }, cancellator.Token);
                            AppLogger.Success($"{history.Count} updates were sent to {clientName} (with connection: {Context.ConnectionId})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Updates error: {ex.Message}");
                }
            }
            finally
            {
                await page.CloseAsync();
                await context.CloseAsync();
                await browser.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error retrieving status history: {ex.Message}");
        }
    }
}
