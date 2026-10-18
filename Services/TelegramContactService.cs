using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace XoSoWeb.Services
{
    public class TelegramContactService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramContactService> _logger;

        public TelegramContactService(IConfiguration configuration, ILogger<TelegramContactService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendAsync(ContactSupportRequest request, CancellationToken cancellationToken)
        {
            var token = _configuration["TelegramContact:BotToken"];
            var chatId = _configuration["TelegramContact:ChatId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
            {
                _logger.LogWarning("Telegram contact configuration is missing.");
                return false;
            }

            var text = $"LIÊN HỆ WEBSITE XỔ SỐ\nHọ tên: {request.FullName.Trim()}\nEmail: {request.Email.Trim()}\nTiêu đề: {request.Subject.Trim()}\n\n{request.Content.Trim()}";
            try
            {
                using var response = await Client.PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage",
                    new { chat_id = chatId, text }, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Telegram contact send failed with status {StatusCode}.", (int)response.StatusCode);
                    return false;
                }
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                return payload.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is OperationCanceledException || exception is JsonException)
            {
                // Do not log request URLs or exception details: URLs contain the bot token.
                _logger.LogWarning("Telegram contact delivery could not be confirmed.");
                return false;
            }
        }
    }
}
