using FamilyBudget.Core.Messages;
using FamilyBudget.Infrastructure.Services;

namespace FamilyBudget.API.Services;

public class MessageProcessorService : BackgroundService
{
    private readonly IMessageQueueService _messageQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageProcessorService> _logger;

    public MessageProcessorService(
        IMessageQueueService messageQueue,
        IServiceProvider serviceProvider,
        ILogger<MessageProcessorService> logger)
    {
        _messageQueue = messageQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Processor Service started");

        // Subscribe to transaction events
        _messageQueue.Subscribe<TransactionCreatedEvent>("transactions", async (message) =>
        {
            _logger.LogInformation($"Processing transaction {message.TransactionId}");
            
            using var scope = _serviceProvider.CreateScope();
            // Check budgets, send notifications, update statistics, etc.
            await CheckBudgetLimits(scope, message);
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task CheckBudgetLimits(IServiceScope scope, TransactionCreatedEvent message)
    {
        // Logic to check if transaction exceeds budget
        // If exceeded, publish BudgetExceededEvent
        _logger.LogInformation($"Checking budget for family {message.FamilyId}");
    }
}