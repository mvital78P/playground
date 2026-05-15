using DriveSearch.Application.Services;
using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Search;
using DriveSearch.TelegramBot.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DriveSearch.TelegramBot.Services;

/// <summary>
/// Background service that runs the Telegram bot.
/// </summary>
public class BotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotService> _logger;
    private readonly IConfiguration _configuration;
    private readonly long _allowedUserId;

    public BotService(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<BotService> logger,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        _allowedUserId = long.TryParse(configuration["Telegram:AllowedUserId"], out var userId)
            ? userId
            : 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation("Bot started: @{Username}", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>(),
            DropPendingUpdates = true // Force Telegram to drop old polling sessions
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation("Bot is listening for updates...");

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message || message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;

        _logger.LogInformation("Received message from {UserId}: {Text}", userId, messageText);

        // Security check
        if (_allowedUserId != 0 && userId != _allowedUserId)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "⛔ Sie sind nicht autorisiert, diesen Bot zu verwenden.",
                cancellationToken: ct
            );
            return;
        }

        try
        {
            await HandleCommandAsync(botClient, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command");
            await botClient.SendMessage(
                chatId: chatId,
                text: $"❌ Fehler: {ex.Message}",
                cancellationToken: ct
            );
        }
    }

    private async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var text = message.Text ?? string.Empty;

        if (text.StartsWith("/start"))
        {
            await HandleStartCommand(botClient, chatId, ct);
        }
        else if (text.StartsWith("/help"))
        {
            await HandleHelpCommand(botClient, chatId, ct);
        }
        else if (text.StartsWith("/search "))
        {
            var query = text[8..].Trim();
            await HandleSearchCommand(botClient, chatId, query, ct);
        }
        else if (text.StartsWith("/ask "))
        {
            var query = text[5..].Trim();
            await HandleAskCommand(botClient, chatId, query, ct);
        }
        else if (text.StartsWith("/status"))
        {
            await HandleStatusCommand(botClient, chatId, ct);
        }
        else if (text.StartsWith("/recent"))
        {
            await HandleRecentCommand(botClient, chatId, ct);
        }
        else
        {
            // Free text → treat as RAG question
            await HandleAskCommand(botClient, chatId, text, ct);
        }
    }

    private async Task HandleStartCommand(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        var text = @"🔍 <b>DriveSearch Bot</b>

Willkommen beim DriveSearch Bot! Ich kann dir helfen, in deinen Google Drive Dokumenten zu suchen.

Verwende /help um alle verfügbaren Befehle zu sehen.";

        await botClient.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private async Task HandleHelpCommand(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        var text = @"📚 <b>Verfügbare Befehle:</b>

🔍 <b>/search</b> [query]
   Sucht nach Dokumenten mit dem angegebenen Begriff

💬 <b>/ask</b> [frage]
   Stellt eine Frage und bekommt eine KI-generierte Antwort basierend auf deinen Dokumenten

📊 <b>/status</b>
   Zeigt Statistiken der Dokumenten-Datenbank

📄 <b>/recent</b>
   Zeigt die zuletzt indexierten Dokumente

❓ <b>/help</b>
   Zeigt diese Hilfe an";

        await botClient.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private async Task HandleSearchCommand(ITelegramBotClient botClient, long chatId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ Bitte gib einen Suchbegriff an: /search [query]",
                cancellationToken: ct
            );
            return;
        }

        await botClient.SendMessage(
            chatId: chatId,
            text: "🔍 Suche wird durchgeführt...",
            cancellationToken: ct
        );

        using var scope = _serviceProvider.CreateScope();
        var hybridSearch = scope.ServiceProvider.GetRequiredService<HybridSearchService>();
        var embeddingFactory = scope.ServiceProvider.GetRequiredService<EmbeddingProviderFactory>();

        var embeddingProvider = embeddingFactory.Create();
        var queryVector = await embeddingProvider.GetEmbeddingAsync(query, "RETRIEVAL_QUERY");

        var results = await hybridSearch.SearchAsync(query, queryVector, 10);

        if (results.Count == 0)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ Keine Ergebnisse gefunden.",
                cancellationToken: ct
            );
            return;
        }

        var response = ResponseFormatter.FormatSearchResults(results);

        await botClient.SendMessage(
            chatId: chatId,
            text: response,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private async Task HandleAskCommand(ITelegramBotClient botClient, long chatId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ Bitte stelle eine Frage: /ask [frage]",
                cancellationToken: ct
            );
            return;
        }

        await botClient.SendMessage(
            chatId: chatId,
            text: "💭 Antwort wird generiert...",
            cancellationToken: ct
        );

        using var scope = _serviceProvider.CreateScope();
        var ragService = scope.ServiceProvider.GetRequiredService<RagService>();

        var result = await ragService.AskAsync(query, 5, ct);

        var response = ResponseFormatter.FormatRagResult(result);

        await botClient.SendMessage(
            chatId: chatId,
            text: response,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private async Task HandleStatusCommand(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var (totalDocuments, lastSync) = await repository.GetStatsAsync();

        var lastSyncText = lastSync.HasValue
            ? lastSync.Value.ToString("dd.MM.yyyy HH:mm")
            : "Nie";

        var text = $@"📊 <b>Datenbank Status</b>

📄 Dokumente: {totalDocuments}
🕐 Letzter Sync: {lastSyncText}";

        await botClient.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private async Task HandleRecentCommand(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var recentDocuments = await repository.GetRecentDocumentsAsync(10);

        if (recentDocuments.Count == 0)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ Keine Dokumente gefunden.",
                cancellationToken: ct
            );
            return;
        }

        var response = ResponseFormatter.FormatRecentDocuments(recentDocuments);

        await botClient.SendMessage(
            chatId: chatId,
            text: response,
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Polling error");
        return Task.CompletedTask;
    }
}
