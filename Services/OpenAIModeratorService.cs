using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MicroSocialPlatform.Services
{
    public class OpenAIModeratorService : IContentModerator
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenAIModeratorService> _logger;

        private readonly string _apiKey;
        private readonly string _model;
        private readonly string? _organization;
        private readonly string? _project;

        public OpenAIModeratorService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<OpenAIModeratorService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _apiKey = configuration["OpenAI:ApiKey"] ?? "";
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("OpenAI API Key not found. Set OpenAI:ApiKey in configuration/user-secrets.");

            // Default is omni-moderation-latest per docs; we keep it explicit.
            _model = configuration["OpenAI:ModerationModel"] ?? "omni-moderation-latest";

            // Optional headers (safe to omit)
            _organization = configuration["OpenAI:Organization"];
            _project = configuration["OpenAI:Project"];
        }

        public async Task<bool> IsHarmfulAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var client = _httpClientFactory.CreateClient();

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/moderations");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // Optional headers (only if you use them)
            if (!string.IsNullOrWhiteSpace(_organization))
                req.Headers.TryAddWithoutValidation("OpenAI-Organization", _organization);

            if (!string.IsNullOrWhiteSpace(_project))
                req.Headers.TryAddWithoutValidation("OpenAI-Project", _project);

            var payload = new
            {
                model = _model,
                input = text
            };

            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var res = await client.SendAsync(req, cts.Token);

                var body = await res.Content.ReadAsStringAsync(cts.Token);

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "OpenAI moderation failed. Status={StatusCode}. Body={Body}",
                        (int)res.StatusCode,
                        body);

                    // Fail CLOSED: if moderation is down, treat as harmful so you don't silently allow unsafe content.
                    return true;
                }

                using var doc = JsonDocument.Parse(body);

                // Per OpenAI moderation response format: results[0].flagged
                var flagged = doc.RootElement
                    .GetProperty("results")[0]
                    .GetProperty("flagged")
                    .GetBoolean();

                return flagged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI moderation exception (network/key/timeout/etc).");

                // Fail CLOSED (recommended while you’re testing so you actually notice problems)
                return true;
            }
        }
    }
}
