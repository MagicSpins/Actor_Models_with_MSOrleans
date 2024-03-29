﻿using CarManager.Orleans.Hubs.Hubs;
using CarsManager.Orleans.Grains.CarTracker;
using Microsoft.AspNetCore.SignalR;
using Orleans.Concurrency;
using Orleans.Runtime;

namespace CarsManager.Orleans.Silo.Hubs;

[Reentrant]
internal sealed class HubListUpdater : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HubListUpdater> _logger;
    private readonly ILocalSiloDetails _localSiloDetails;
    private readonly RemoteLocationHub _locationBroadcaster;

    public HubListUpdater(
        IGrainFactory grainFactory,
        ILogger<HubListUpdater> logger,
        ILocalSiloDetails localSiloDetails,
        IHubContext<LocationHub> hubContext)
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _localSiloDetails = localSiloDetails;
        _locationBroadcaster = new RemoteLocationHub(hubContext);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubListGrain = _grainFactory.GetGrain<IHubListGrain>(Guid.Empty);
        var localSiloAddress = _localSiloDetails.SiloAddress;
        IRemoteLocationHub selfReference = _grainFactory.CreateObjectReference<IRemoteLocationHub>(_locationBroadcaster) as IRemoteLocationHub;

        // This runs in a loop because the HubListGrain does not use any form of persistence, so if the
        // host which it is activated on stops, then it will lose any internal state.
        // If HubListGrain was changed to use persistence, then this loop could be safely removed.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await hubListGrain.AddHub(localSiloAddress, selfReference);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Error polling location hub list");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch
                {
                    // Ignore cancellation exceptions, since cancellation is handled by the outer loop.
                }
            }
        }
    }
}
