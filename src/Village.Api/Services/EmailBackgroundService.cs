using System.Threading.Channels;

namespace Village.Api.Services;

/// <summary>
/// Reliable fire-and-forget email delivery.
/// Emails are queued and sent on a background thread so a slow or failing SMTP
/// call can't block (or fail) the request, failures are logged instead of
/// silently swallowed, and queued work survives the request scope.
/// </summary>
public sealed class EmailBackgroundService : BackgroundService
{
    private readonly Channel<Func<IEmailService, Task>> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EmailBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queue = Channel.CreateUnbounded<Func<IEmailService, Task>>(
            new UnboundedChannelOptions { SingleReader = true });
    }

    /// <summary>Queue an email to be sent in the background. Never throws.</summary>
    public void Enqueue(Func<IEmailService, Task> send)
    {
        if (!_queue.Writer.TryWrite(send))
            _logger.LogError("Failed to enqueue background email; the queue is full or shutting down");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Resolve a fresh IEmailService per job so each send gets its own
                // typed HttpClient from the shared handler pool (no scoped capture).
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await job(emailService);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background email delivery failed");
            }
        }
    }
}
