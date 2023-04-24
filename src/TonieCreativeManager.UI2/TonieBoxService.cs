namespace TonieCreativeManager.UI2
{
    public class TonieBoxService : BackgroundService
    {
        public TonieBoxService(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<TonieBoxService>();
        }

        public ILogger Logger { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("ServiceA is starting.");

            stoppingToken.Register(() => Logger.LogInformation("ServiceA is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("ServiceA is doing background work.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            Logger.LogInformation("ServiceA has stopped.");
        }

    }
}
