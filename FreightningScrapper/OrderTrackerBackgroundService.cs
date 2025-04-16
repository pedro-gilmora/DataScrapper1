using Microsoft.AspNetCore.SignalR;
using Microsoft.Playwright;

namespace FreightningScrapper;

public class OrderTrackerBackgroundService(
    ITrackingRepository trackingRepository,
    OrderTrackerHub hubContext) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach(var ((clientId, name), trackingNumbers) in await trackingRepository.GetTrackingNumbersAsync(stoppingToken))
                {
                    foreach (var trackingNumber in trackingNumbers)
                    {
                        await Scrappers.GetStatusHistoryAsync(hubContext, clientId, trackingNumber, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error en el servicio en segundo plano: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(7), stoppingToken);
        }
    }
}