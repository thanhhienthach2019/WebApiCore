using DataAccess.EFCore.Repositories.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.EFCore.Repositories.BackgroundServices
{
    public class ExpiredTokenCleaner : BackgroundService
    {
        private readonly TokensService _tokensService;
        private readonly ILogger<ExpiredTokenCleaner> _logger;
        private Timer timer;
        public ExpiredTokenCleaner(TokensService tokensService, ILogger<ExpiredTokenCleaner> logger)
        {
            _tokensService = tokensService;
            _logger = logger;
        }
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start BackgroundService ExpiredTokenCleaner");

            await base.StartAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var startMessage = "Start executing RemoveExpiredTokensAsync";
            var executeMessage = "Executing RemoveExpiredTokensAsync";

            _logger.LogInformation(startMessage);
            await Console.Out.WriteLineAsync(startMessage);
            this.timer = new Timer(async _ =>
            {
                _logger.LogInformation(executeMessage);
                await Console.Out.WriteLineAsync(executeMessage);
                await _tokensService.RemoveExpiredTokensAsync();
            },
                null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(3));
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop BackgroundService ExpiredTokenCleaner");

            await base.StopAsync(cancellationToken);

            await this.timer.DisposeAsync();
        }
    }
}
