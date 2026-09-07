using System.Text;
using System.Text.Json;

namespace WebApplication1.Services
{
    /// <summary>
    /// Calls the Gemini 2.5 Flash REST API to generate an AI-powered
    /// analytics report based on the clinic's live metrics.
    /// </summary>
    public class GeminiAnalyticsService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<GeminiAnalyticsService> _logger;

        public GeminiAnalyticsService(
            HttpClient http,
            IConfiguration config,
            ILogger<GeminiAnalyticsService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Sends clinic metrics to Gemini and returns a ready-to-render HTML snippet.
        /// </summary>
        // Fallback chain: first model that returns a non-404 wins.
        // Full URL shape (explicit interpolation — no hidden template substitution):
        //   https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=<key>
        private static readonly string[] ModelFallbackChain = new[]
        {
            "gemini-2.5-flash", // primary
            "gemini-2.0-flash"  // fallback (gemini-1.5-x returns 404 on v1beta)
        };

        // Builds the request URL using direct string interpolation.
        // modelName is the bare name (e.g. "gemini-2.5-flash") — no path prefix — so
        // "models/" appears exactly once in the final URL.
        private static string BuildUrl(string modelName, string apiKey) =>
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        public async Task<string> GenerateClinicReportAsync(ClinicMetrics metrics)
        {
            var apiKey = _config["Gemini:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BuildFallbackHtml(
                    "⚙️ Gemini API key not configured",
                    "Please add your Gemini API key to <code>appsettings.json</code> under <code>Gemini:ApiKey</code>.");
            }

            var prompt = BuildPrompt(metrics);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            try
            {
                HttpResponseMessage? response = null;
                string? usedModel = null;

                // Try each model in order; fall back on 404 (model not found / retired).
                foreach (var modelName in ModelFallbackChain)
                {
                    // Guard: ensure modelName is never null or empty before building the URL.
                    var safeModelName = string.IsNullOrEmpty(modelName)
                        ? "gemini-2.5-flash"
                        : modelName;

                    var url = BuildUrl(safeModelName, apiKey);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Google Cloud Express/service keys require both the query-param AND the header.
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = content
                    };
                    request.Headers.Add("x-goog-api-key", apiKey);

                    // Log the exact resolved URL so it can be verified in output.
                    var resolvedUrl =
                        $"https://generativelanguage.googleapis.com/v1beta/models/{safeModelName}:generateContent";
                    _logger.LogInformation(
                        "Calling Gemini model: {Model} — URL: {Url}", safeModelName, resolvedUrl);
                    response = await _http.SendAsync(request);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var notFoundBody = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning(
                            "Model {Model} returned 404 — falling back. Google response: {Body}",
                            modelName, notFoundBody);
                        continue; // try next model
                    }

                    usedModel = modelName;
                    break; // success or a non-404 error — stop trying
                }

                if (response is null)
                {
                    return BuildFallbackHtml(
                        "❌ All Gemini model endpoints returned 404",
                        "None of the fallback models are available for this API key. Check the Google AI Studio console.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Surface the exact raw error body from Google so the caller can diagnose it.
                    var rawError = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Gemini API error [{Status}] on model {Model}. Raw Google response:\n{Body}",
                        (int)response.StatusCode, usedModel, rawError);

                    // Escape HTML special chars so the JSON is safe to embed in the page.
                    var safeError = System.Net.WebUtility.HtmlEncode(rawError);
                    return BuildFallbackHtml(
                        $"❌ Gemini API error {(int)response.StatusCode} ({response.StatusCode})",
                        $"<strong>Raw Google response:</strong><br/><pre class=\"mb-0 mt-1\" style=\"white-space:pre-wrap;font-size:.75rem\">{safeError}</pre>");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var parsed = JsonDocument.Parse(responseJson);

                // Navigate: candidates[0].content.parts[0].text
                var text = parsed
                    .RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;

                // Gemini sometimes wraps the reply in markdown code fences — strip them
                text = StripMarkdownFences(text);

                return text;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while calling Gemini API");
                return BuildFallbackHtml("🌐 Network Error", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GeminiAnalyticsService");
                return BuildFallbackHtml("⚠️ Unexpected Error", ex.Message);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string BuildPrompt(ClinicMetrics m)
        {
            return $"""
                You are an expert medical clinic financial analyst. 
                Analyse the following clinic performance metrics and produce a concise, 
                professional HTML report (no <!DOCTYPE>, no <html>/<body> tags — only 
                inner HTML content using Bootstrap 5 classes).

                ## Clinic Metrics (as of today {DateTime.Today:dd MMM yyyy})
                - Total Patients Registered : {m.TotalPatients}
                - Active Doctors             : {m.TotalDoctors}
                - Departments               : {m.TotalDepartments}
                - Today's Appointments      : {m.TodayAppointments}
                - Total Invoices Issued      : {m.TotalInvoicesCount}
                - Total Revenue (Paid)       : {m.TotalRevenue:N2} JD
                - Pending Amount (Unpaid)    : {m.PendingRevenue:N2} JD
                - Collection Rate           : {m.CollectionRate:P1}

                ## Instructions
                1. Start with a short 2-sentence executive summary inside a 
                   <div class="alert alert-info"> block.
                2. List 3–5 key insights as <li> items inside a <ul class="list-group mb-3">.
                3. Provide 2–3 actionable recommendations in a styled 
                   <div class="card border-0 shadow-sm mb-3"> block each.
                4. End with a risk assessment badge row using Bootstrap badges 
                   (bg-success / bg-warning / bg-danger).
                5. Use only Bootstrap 5 utility classes — no inline style attributes.
                6. Keep the tone professional and concise (≤ 350 words total).
                """;
        }

        private static string StripMarkdownFences(string text)
        {
            text = text.Trim();
            if (text.StartsWith("```html", StringComparison.OrdinalIgnoreCase))
                text = text[7..].Trim();
            else if (text.StartsWith("```"))
                text = text[3..].Trim();

            if (text.EndsWith("```"))
                text = text[..^3].Trim();

            return text;
        }

        private static string BuildFallbackHtml(string title, string body)
        {
            return $"""
                <div class="alert alert-warning d-flex align-items-start gap-3 mb-0" role="alert">
                    <span style="font-size:1.6rem;">🤖</span>
                    <div>
                        <h6 class="alert-heading mb-1">{title}</h6>
                        <p class="mb-0 small">{body}</p>
                    </div>
                </div>
                """;
        }
    }

    // ── DTO ────────────────────────────────────────────────────────────────────

    /// <summary>Snapshot of clinic KPIs passed to the AI service.</summary>
    public class ClinicMetrics
    {
        public int TotalPatients { get; init; }
        public int TotalDoctors { get; init; }
        public int TotalDepartments { get; init; }
        public int TodayAppointments { get; init; }
        public int TotalInvoicesCount { get; init; }
        public decimal TotalRevenue { get; init; }
        public decimal PendingRevenue { get; init; }

        /// <summary>Percentage of invoiced amount that has been collected.</summary>
        public decimal CollectionRate => (TotalRevenue + PendingRevenue) == 0
            ? 0m
            : TotalRevenue / (TotalRevenue + PendingRevenue);
    }
}
