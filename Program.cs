using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace LagoaSportRpa
{
    // ==============================
    // App Settings
    // ==============================
    public class AppSettings
    {
        public AgendamentoSettings Agendamento { get; set; } = new AgendamentoSettings();
    }

    public class AgendamentoSettings
    {
        public string UrlLogin { get; set; } = string.Empty;
        public string UrlQuadra { get; set; } = string.Empty;
        public string TextoDia { get; set; } = string.Empty;
        public int DuracaoHoras { get; set; } = 1;
        public ParticipanteSettings Participante { get; set; } = new ParticipanteSettings();
        public List<LoginSettings> Logins { get; set; } = new List<LoginSettings>();
    }

    public class ParticipanteSettings
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Rg { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class LoginSettings
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Horario { get; set; } = string.Empty;
    }

    // ==============================
    // Baribot Framework - Core
    // ==============================
    public interface IPipelineStep
    {
        void Execute(PipelineContext context);
    }

    public class PipelineContext
    {
        public Dictionary<string, object> Items { get; } = new Dictionary<string, object>();
    }

    public class PipelineBuilder
    {
        private readonly List<IPipelineStep> _steps = new();

        public PipelineBuilder AddStep(IPipelineStep step)
        {
            _steps.Add(step);
            return this;
        }

        public Pipeline Build()
        {
            return new Pipeline(_steps);
        }
    }

    public class Pipeline
    {
        private readonly List<IPipelineStep> _steps;

        public Pipeline(List<IPipelineStep> steps)
        {
            _steps = steps;
        }

        public void Execute(PipelineContext context)
        {
            foreach (var step in _steps)
            {
                step.Execute(context);
            }
        }
    }

    // ==============================
    // Contexto Específico
    // ==============================
    public class AgendamentoContext : PipelineContext
    {
        public string UrlLogin { get; set; } = "https://lagoasport.lagoasanta.mg.gov.br/login";
        public string UrlQuadra { get; set; } = "https://lagoasport.lagoasanta.mg.gov.br/locations/6";
        public string Email { get; set; }
        public string Senha { get; set; }
        public string TextoDia { get; set; } 
        public string Horario { get; set; }     
        public string ParticipanteNome { get; set; }      
        public string ParticipanteCpf { get; set; } 
        public string ParticipanteRg { get; set; }
        public string ParticipanteTelefone { get; set; } 
        public string ParticipanteEmail { get; set; } 
    }

    // ==============================
    // API Client
    // ==============================
    public sealed class LagoaSportApiClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly CookieContainer _cookies = new();
        private readonly HttpClient _http;

        public LagoaSportApiClient(string baseUrl)
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
            };

            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
            _http.DefaultRequestHeaders.AcceptLanguage.Clear();
            _http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));
            _http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt", 0.9));
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<InertiaPage<DashboardProps>> LoginAsync(string email, string senha)
        {
            await GetStringAsync("login");

            var xsrf = GetCookieValue("XSRF-TOKEN");
            var request = new HttpRequestMessage(HttpMethod.Post, "login");
            request.Headers.Referrer = new Uri(_http.BaseAddress!, "login");
            request.Headers.TryAddWithoutValidation("Origin", _http.BaseAddress!.GetLeftPart(UriPartial.Authority));
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            var loginBody = new List<KeyValuePair<string, string>>
            {
                new("email", email),
                new("password", senha)
            };

            if (!string.IsNullOrWhiteSpace(xsrf))
            {
                var token = Uri.UnescapeDataString(xsrf);
                request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
            }

            request.Content = new FormUrlEncodedContent(loginBody);

            using var response = await _http.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Login HTTP falhou: {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    $"{TruncateForError(html)}");
            }

            var page = ParsePage<DashboardProps>(html);

            if (page.Props?.Auth?.User == null)
            {
                throw new InvalidOperationException("Login HTTP falhou: usuário não retornou em dashboard.");
            }

            return page;
        }

        public Task<InertiaPage<DashboardProps>> GetDashboardAsync()
        {
            return GetPageAsync<DashboardProps>("dashboard");
        }

        public Task<InertiaPage<LocationPageProps>> GetLocationAsync(int locationId)
        {
            return GetPageAsync<LocationPageProps>($"locations/{locationId}");
        }

        public Task<InertiaPage<AppointmentCreateProps>> OpenAppointmentAsync(int slotId)
        {
            return GetPageAsync<AppointmentCreateProps>($"appointments/{slotId}");
        }

        public async Task<ActiveReservationResponse> GetActiveReservationAsync()
        {
            using var response = await _http.GetAsync("timeslots/active-reservation");
            var json = await ReadBodyOrThrowAsync(response, "GET", "timeslots/active-reservation");
            return JsonSerializer.Deserialize<ActiveReservationResponse>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Resposta inválida em timeslots/active-reservation.");
        }

        public async Task<InertiaPage<GenericFlashProps>> SubmitAppointmentAsync(
            IReadOnlyList<int> timeSlotIds,
            IReadOnlyList<ParticipantPayload> participants,
            string? inertiaVersion,
            string? refererPath)
        {
            var xsrf = GetCookieValue("XSRF-TOKEN");
            var payload = JsonSerializer.Serialize(new
            {
                timeSlotIds,
                participants
            }, JsonOptions);

            var request = new HttpRequestMessage(HttpMethod.Post, "appointments")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Origin", _http.BaseAddress!.GetLeftPart(UriPartial.Authority));
            request.Headers.Referrer = new Uri(_http.BaseAddress!, refererPath?.TrimStart('/') ?? "appointments");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("X-Inertia", "true");
            if (!string.IsNullOrWhiteSpace(inertiaVersion))
            {
                request.Headers.TryAddWithoutValidation("X-Inertia-Version", inertiaVersion);
            }
            request.Headers.TryAddWithoutValidation("Accept", "text/html, application/xhtml+xml");
            if (!string.IsNullOrWhiteSpace(xsrf))
            {
                request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", Uri.UnescapeDataString(xsrf));
            }

            using var response = await _http.SendAsync(request);
            var body = await ReadBodyOrThrowAsync(response, "POST", "appointments");
            return ParseInertiaResponse<GenericFlashProps>(body);
        }

        public async Task CancelReservationAsync(int slotId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"timeslots/{slotId}/cancel");
            request.Headers.TryAddWithoutValidation("Origin", _http.BaseAddress!.GetLeftPart(UriPartial.Authority));
            request.Headers.Referrer = new Uri(_http.BaseAddress!, $"timeslots/{slotId}/cancel");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            var xsrf = GetCookieValue("XSRF-TOKEN");
            if (!string.IsNullOrWhiteSpace(xsrf))
            {
                var token = Uri.UnescapeDataString(xsrf);
                request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
            }

            using var response = await _http.SendAsync(request);
            await ReadBodyOrThrowAsync(response, "POST", $"timeslots/{slotId}/cancel");
        }

        private async Task<InertiaPage<TProps>> GetPageAsync<TProps>(string relativeUrl)
        {
            var html = await GetStringAsync(relativeUrl);
            return ParsePage<TProps>(html);
        }

        private async Task<string> GetStringAsync(string relativeUrl)
        {
            using var response = await _http.GetAsync(relativeUrl);
            return await ReadBodyOrThrowAsync(response, "GET", relativeUrl);
        }

        private static async Task<string> ReadBodyOrThrowAsync(HttpResponseMessage response, string method, string relativeUrl)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"{method} {relativeUrl} HTTP falhou: {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    $"{TruncateForError(body)}");
            }

            return body;
        }

        private static InertiaPage<TProps> ParsePage<TProps>(string html)
        {
            var match = Regex.Match(html, @"<div id=""app"" data-page=""(?<json>.*?)""></div>", RegexOptions.Singleline);
            if (!match.Success)
            {
                throw new InvalidOperationException("Não foi possível localizar data-page Inertia na resposta.");
            }

            var json = WebUtility.HtmlDecode(match.Groups["json"].Value);
            return JsonSerializer.Deserialize<InertiaPage<TProps>>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Falha ao desserializar página Inertia.");
        }

        private static InertiaPage<TProps> ParseInertiaResponse<TProps>(string body)
        {
            var trimmed = body.TrimStart();
            if (trimmed.StartsWith("{"))
            {
                return JsonSerializer.Deserialize<InertiaPage<TProps>>(trimmed, JsonOptions)
                       ?? throw new InvalidOperationException("Falha ao desserializar resposta Inertia JSON.");
            }

            return ParsePage<TProps>(body);
        }

        private string? GetCookieValue(string name)
        {
            var cookies = _cookies.GetCookies(_http.BaseAddress!);
            return cookies[name]?.Value;
        }

        private static string TruncateForError(string value, int max = 300)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.ReplaceLineEndings(" ").Trim();
            return normalized.Length <= max ? normalized : normalized[..max] + "...";
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }

    // ==============================
    // API Models
    // ==============================
    public sealed class InertiaPage<TProps>
    {
        [JsonPropertyName("component")]
        public string? Component { get; set; }

        [JsonPropertyName("props")]
        public TProps? Props { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    public sealed class DashboardProps
    {
        [JsonPropertyName("auth")]
        public AuthProps? Auth { get; set; }
    }

    public sealed class LocationPageProps
    {
        [JsonPropertyName("auth")]
        public AuthProps? Auth { get; set; }

        [JsonPropertyName("location")]
        public LocationDto? Location { get; set; }

        [JsonPropertyName("slotsByDate")]
        public Dictionary<string, List<TimeSlotDto>>? SlotsByDate { get; set; }
    }

    public sealed class AppointmentCreateProps
    {
        [JsonPropertyName("auth")]
        public AuthProps? Auth { get; set; }

        [JsonPropertyName("canExtendTime")]
        public bool CanExtendTime { get; set; }

        [JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }

        [JsonPropertyName("nextSlotId")]
        public int NextSlotId { get; set; }

        [JsonPropertyName("slot")]
        public TimeSlotDto? Slot { get; set; }
    }

    public sealed class GenericFlashProps
    {
        [JsonPropertyName("flash")]
        public FlashProps? Flash { get; set; }
    }

    public sealed class FlashProps
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("success")]
        public string? Success { get; set; }
    }

    public sealed class ActiveReservationResponse
    {
        [JsonPropertyName("slot")]
        public TimeSlotDto? Slot { get; set; }
    }

    public sealed class AuthProps
    {
        [JsonPropertyName("user")]
        public UserDto? User { get; set; }
    }

    public sealed class LocationDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("min_participants")]
        public int MinParticipants { get; set; }
    }

    public sealed class TimeSlotDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("is_available")]
        [JsonConverter(typeof(FlexibleBooleanConverter))]
        public bool IsAvailable { get; set; }

        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }
    }

    public sealed class UserDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        [JsonPropertyName("rg")]
        public string? Rg { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public long? Phone { get; set; }
    }

    public sealed class FlexibleBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => reader.TryGetInt32(out var number) ? number != 0 : reader.GetDouble() != 0,
                JsonTokenType.String => TryParseString(reader.GetString()),
                _ => throw new JsonException($"Tipo inválido para bool: {reader.TokenType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }

        private static bool TryParseString(string? value)
        {
            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue != 0;
            }

            return !string.IsNullOrWhiteSpace(value);
        }
    }

    public sealed class ParticipantPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("cpf")]
        public string Cpf { get; set; } = string.Empty;

        [JsonPropertyName("rg")]
        public string Rg { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class LoginExecutionResult
    {
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AgendamentoRunResult
    {
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset FinishedAt { get; set; }
        public string UrlLogin { get; set; } = string.Empty;
        public string UrlQuadra { get; set; } = string.Empty;
        public string TextoDia { get; set; } = string.Empty;
        public int TotalLogins { get; set; }
        public int Sucessos { get; set; }
        public int Falhas { get; set; }
        public List<LoginExecutionResult> Itens { get; set; } = new();
    }

    // ==============================
    // Selenium Helper (único ponto de driver)
    // ==============================
    public static class SeleniumHelper
    {
        private static IWebDriver? _driver;
        private static readonly int DefaultTimeoutSeconds =
            int.TryParse(Environment.GetEnvironmentVariable("WAIT_SECONDS"), out var seconds) && seconds > 0
                ? seconds
                : 60;

        public static void Start()
        {
            if (_driver != null) return;

            var options = new ChromeOptions();

            // --- CONFIGURAÇÕES DE VISUAL ---
            options.AddArgument("--start-maximized");
            options.AddArgument("--window-size=1920,1080");

            // --- MODO FURTIVO (CRÍTICO PARA SERVIDOR) ---
            // 1. Muda o User-Agent para parecer um Windows normal, não um robô Linux
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // 2. Desativa a flag interna que avisa "Estou sendo controlado por automação"
            options.AddArgument("--disable-blink-features=AutomationControlled");

            // 3. Remove a barra amarela "Chrome is being controlled by automated software"
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            // --- CONFIGURAÇÕES DOCKER ---
            var headless = string.Equals(Environment.GetEnvironmentVariable("HEADLESS"), "true", StringComparison.OrdinalIgnoreCase);
            if (headless)
            {
                // Use o modo "new" que é muito mais estável
                options.AddArgument("--headless=new");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--disable-extensions");
            }

            var chromeBin = Environment.GetEnvironmentVariable("CHROME_BIN");
            if (!string.IsNullOrWhiteSpace(chromeBin))
            {
                options.BinaryLocation = chromeBin;
            }

            var driverPath = Environment.GetEnvironmentVariable("CHROMEDRIVER_PATH");
            ChromeDriverService service = string.IsNullOrWhiteSpace(driverPath)
                ? ChromeDriverService.CreateDefaultService()
                : ChromeDriverService.CreateDefaultService(driverPath);
            service.HideCommandPromptWindow = true;

            _driver = new ChromeDriver(service, options);
        }

        public static void Quit()
        {
            try { _driver?.Quit(); }
            finally { _driver = null; }
        }

        public static void GoToUrl(string url)
        {
            EnsureDriver();
            _driver!.Navigate().GoToUrl(url);
        }

        public static bool MapsToUrl(string urlPrefix)
        {
            EnsureDriver();
            return _driver!.Url.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static void FillTextBox(By by, string text)
        {
            var el = WaitForElement(by, DefaultTimeoutSeconds);
            el.Clear();
            el.SendKeys(text);
        }

        public static void Click(By by)
        {
            var el = WaitForElement(by, DefaultTimeoutSeconds);
            el.Click();
        }

        public static void WaitUntil(Func<IWebDriver, bool> condition, int timeoutSeconds = 0)
        {
            EnsureDriver();
            var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds));
            wait.Until(condition);
        }

        public static bool ElementExists(By by, int timeoutSeconds = 0)
        {
            try
            {
                WaitForElement(by, timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IWebElement WaitForElement(By by, int timeoutSeconds = 10)
        {
            EnsureDriver();
            var wait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds));
            return wait.Until(drv => drv.FindElement(by));
        }

        public static void TakeScreenshot(string fileName)
        {
            if (_driver == null) return;
            try
            {
                var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
                var path = Path.Combine(Directory.GetCurrentDirectory(), "prints");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                var filePath = Path.Combine(path, fileName);
                screenshot.SaveAsFile(filePath);
                Console.WriteLine($"Screenshot salvo em: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tirar screenshot: {ex.Message}");
            }
        }
        private static void EnsureDriver()
        {
            if (_driver == null)
            {
                throw new InvalidOperationException("SeleniumHelper não foi iniciado. Chame SeleniumHelper.Start() antes de usar.");
            }
        }
    }

    // ==============================
    // Exceções de Negócio
    // ==============================
    public class DiaNaoEncontradoException : Exception
    {
        public DiaNaoEncontradoException(string dia)
            : base($"Dia não encontrado no calendário: {dia}") { }
    }

    public class HorarioIndisponivelException : Exception
    {
        public HorarioIndisponivelException(string horario)
            : base($"Horário indisponível ou não encontrado: {horario}") { }
    }

    public class LoginFalhouException : Exception
    {
        public LoginFalhouException()
            : base("Login falhou: não foi possível acessar a área após autenticação.") { }
    }

    public class ModalParticipanteNaoEncontradoException : Exception
    {
        public ModalParticipanteNaoEncontradoException()
            : base("Modal de participante não encontrado.") { }
    }

    public class ParticipanteInvalidoException : Exception
    {
        public ParticipanteInvalidoException()
            : base("Dados do participante inválidos ou confirmação não habilitada.") { }
    }

    public class ConfirmacaoAgendamentoNaoDisponivelException : Exception
    {
        public ConfirmacaoAgendamentoNaoDisponivelException()
            : base("Botão de confirmar agendamento não está disponível.") { }
    }

    // ==============================
    // Steps
    // ==============================
    public class StartBrowserStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            SeleniumHelper.Start();
        }
    }

    public class LoginStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var ctx = (AgendamentoContext)context;

            SeleniumHelper.GoToUrl(ctx.UrlLogin);

            SeleniumHelper.FillTextBox(By.Id("email"), ctx.Email);
            SeleniumHelper.FillTextBox(By.Id("password"), ctx.Senha);

            SeleniumHelper.Click(By.XPath("//button[@type='submit']"));
            SeleniumHelper.WaitUntil(drv => !drv.Url.Contains("/login", StringComparison.OrdinalIgnoreCase), 20);
        }
    }

    public class NavegarQuadraStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var ctx = (AgendamentoContext)context;
            SeleniumHelper.GoToUrl(ctx.UrlQuadra);

            if (!SeleniumHelper.MapsToUrl(ctx.UrlQuadra))
            {
                throw new LoginFalhouException();
            }
        }
    }

    public class SelecionarDiaStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var ctx = (AgendamentoContext)context;
            var xpathDia = $"//button[.//div[normalize-space(text())='{ctx.TextoDia}']]";

            if (!SeleniumHelper.ElementExists(By.XPath(xpathDia), 5))
            {
                throw new DiaNaoEncontradoException(ctx.TextoDia);
            }

            SeleniumHelper.Click(By.XPath(xpathDia));
        }
    }

    public class SelecionarHorarioStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var ctx = (AgendamentoContext)context;
            var xpathHorario = $"//ul//button[contains(normalize-space(.), '{ctx.Horario}')]";

            if (!SeleniumHelper.ElementExists(By.XPath(xpathHorario), 5))
            {
                throw new HorarioIndisponivelException(ctx.Horario);
            }

            SeleniumHelper.Click(By.XPath(xpathHorario));
        }
    }

    public class MarcarSouParticipanteStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var xpathSouParticipante = "//button[@type='button' and contains(normalize-space(.), 'Sou um participante')]";
            SeleniumHelper.Click(By.XPath(xpathSouParticipante));
        }
    }

    public class AbrirModalOutroParticipanteStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var xpathAdicionar = "//button[@type='button' and contains(normalize-space(.), 'Adicionar outro participante')]";
            SeleniumHelper.Click(By.XPath(xpathAdicionar));

            var modalRoot = By.XPath("//div[contains(@class,'rounded-xl')][.//h4[contains(normalize-space(.), 'Dados do Participante')]]");
            if (!SeleniumHelper.ElementExists(modalRoot, 5))
            {
                throw new ModalParticipanteNaoEncontradoException();
            }
        }
    }

    public class PreencherParticipanteStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var ctx = (AgendamentoContext)context;

            SeleniumHelper.FillTextBox(By.Id("p-name"), ctx.ParticipanteNome);
            SeleniumHelper.FillTextBox(By.Id("p-cpf"), ctx.ParticipanteCpf);
            SeleniumHelper.FillTextBox(By.Id("p-rg"), ctx.ParticipanteRg);
            SeleniumHelper.FillTextBox(By.Id("p-phone"), ctx.ParticipanteTelefone);
            SeleniumHelper.FillTextBox(By.Id("p-email"), ctx.ParticipanteEmail);
        }
    }

    public class ConfirmarParticipanteStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var confirmarButton = By.XPath("//button[@type='button' and contains(normalize-space(.), 'Confirmar')]");

            try
            {
                SeleniumHelper.WaitUntil(drv =>
                {
                    var el = drv.FindElement(confirmarButton);
                    return el.Enabled;
                }, 10);
            }
            catch
            {
                throw new ParticipanteInvalidoException();
            }

            if (!SeleniumHelper.ElementExists(confirmarButton, 2))
            {
                throw new ParticipanteInvalidoException();
            }

            SeleniumHelper.Click(confirmarButton);
        }
    }

    public class ConfirmarAgendamentoStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            var confirmarAgendamento = By.XPath("//button[@type='submit' and .//span[normalize-space(text())='Confirmar Agendamento']]");

            try
            {
                SeleniumHelper.WaitUntil(drv =>
                {
                    var el = drv.FindElement(confirmarAgendamento);
                    return el.Enabled;
                }, 15);
            }
            catch
            {
                throw new ConfirmacaoAgendamentoNaoDisponivelException();
            }

            if (!SeleniumHelper.ElementExists(confirmarAgendamento, 2))
            {
                throw new ConfirmacaoAgendamentoNaoDisponivelException();
            }

            SeleniumHelper.Click(confirmarAgendamento);
        }
    }

    public class FinalizarStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            // Placeholder para confirmação final, se existir.
        }
    }

    public class StopBrowserStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            SeleniumHelper.Quit();
        }
    }

    // ==============================
    // Program
    // ==============================
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            LoadDotEnv(Path.Combine(AppContext.BaseDirectory, ".env"));
            LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:8080");

            var app = builder.Build();

            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

            app.MapPost("/executar", async (IConfiguration config, CancellationToken ct) =>
            {
                var settings = config.Get<AppSettings>();
                if (settings?.Agendamento == null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Configuração inválida: seção Agendamento não encontrada."
                    });
                }

                try
                {
                    var result = await ExecutarAgendamentoAsync(settings, ct);
                    return Results.Ok(result);
                }
                catch (OperationCanceledException)
                {
                    return Results.StatusCode(499);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.Run();
        }

        private static async Task<AgendamentoRunResult> ExecutarAgendamentoAsync(AppSettings settings, CancellationToken ct)
        {
            if (settings.Agendamento == null)
            {
                throw new InvalidOperationException("Configuração inválida: seção Agendamento não encontrada.");
            }

            var ag = settings.Agendamento;
            var locationId = ExtractLocationId(ag.UrlQuadra);
            var startedAt = DateTimeOffset.UtcNow;
            var results = new List<LoginExecutionResult>();

            foreach (var login in ag.Logins)
            {
                ct.ThrowIfCancellationRequested();

                using var api = new LagoaSportApiClient("https://lagoasport.lagoasanta.mg.gov.br");

                try
                {
                    var dashboard = await api.LoginAsync(login.Email, login.Senha);
                    var authUser = dashboard.Props?.Auth?.User
                                   ?? throw new InvalidOperationException("Usuário autenticado não retornou no dashboard.");

                    var activeReservation = await api.GetActiveReservationAsync();
                    var slotId = activeReservation.Slot?.Id;

                    if (slotId != null)
                    {
                        var sameLocation = activeReservation.Slot?.LocationId == locationId;
                        var sameTime = string.Equals(
                            NormalizeForComparison($"{NormalizeTime(activeReservation.Slot?.StartTime)} às {NormalizeTime(activeReservation.Slot?.EndTime)}"),
                            NormalizeForComparison(login.Horario),
                            StringComparison.Ordinal);

                        if (!sameLocation || !sameTime)
                        {
                            await api.CancelReservationAsync(slotId.Value);
                            slotId = null;
                        }
                    }

                    if (slotId == null)
                    {
                        var locationPage = await api.GetLocationAsync(locationId);
                        slotId = SelectSlotId(locationPage.Props?.SlotsByDate, ag.TextoDia, login.Horario);
                    }

                    var appointmentPage = await api.OpenAppointmentAsync(slotId.Value);
                    var bookedSlot = appointmentPage.Props?.Slot
                                     ?? throw new InvalidOperationException("Slot não retornou na tela de confirmação.");

                    var participants = BuildParticipants(authUser, ag.Participante);
                    var timeSlotIds = new List<int> { bookedSlot.Id };

                    if (ag.DuracaoHoras >= 2 && appointmentPage.Props?.CanExtendTime == true && appointmentPage.Props.NextSlotId > 0)
                    {
                        timeSlotIds.Add(appointmentPage.Props.NextSlotId);
                    }

                    var confirmation = await api.SubmitAppointmentAsync(timeSlotIds, participants, appointmentPage.Version, appointmentPage.Url);
                    var success = confirmation.Props?.Flash?.Success;
                    var message = !string.IsNullOrWhiteSpace(success)
                        ? success
                        : "Agendamento executado com sucesso.";

                    results.Add(new LoginExecutionResult
                    {
                        Email = login.Email,
                        Success = true,
                        Message = message
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new LoginExecutionResult
                    {
                        Email = login.Email,
                        Success = false,
                        Message = ex.Message
                    });
                }
            }

            return new AgendamentoRunResult
            {
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow,
                UrlLogin = ag.UrlLogin,
                UrlQuadra = ag.UrlQuadra,
                TextoDia = ag.TextoDia,
                TotalLogins = results.Count,
                Sucessos = results.Count(x => x.Success),
                Falhas = results.Count(x => !x.Success),
                Itens = results
            };
        }

        private static int ExtractLocationId(string url)
        {
            var uri = new Uri(url);
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                throw new InvalidOperationException($"URL de quadra inválida: {url}");
            }

            return id;
        }

        private static int SelectSlotId(Dictionary<string, List<TimeSlotDto>>? slotsByDate, string textoDia, string horario)
        {
            if (slotsByDate == null || slotsByDate.Count == 0)
            {
                throw new InvalidOperationException("Nenhum horário disponível retornado pela API.");
            }

            var diaFiltro = NormalizeForComparison(textoDia);
            foreach (var entry in slotsByDate.OrderBy(x => ParseDate(x.Key)))
            {
                var date = ParseDate(entry.Key);
                if (!string.IsNullOrWhiteSpace(diaFiltro))
                {
                    var diaDaSemana = NormalizeForComparison(GetDayAbbreviationPtBr(date.DayOfWeek));
                    if (!diaDaSemana.StartsWith(diaFiltro, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                var slot = entry.Value
                    .Where(s => s.IsAvailable)
                    .FirstOrDefault(s => NormalizeForComparison($"{NormalizeTime(s.StartTime)} às {NormalizeTime(s.EndTime)}") == NormalizeForComparison(horario));

                if (slot != null)
                {
                    return slot.Id;
                }
            }

            throw new HorarioIndisponivelException(horario);
        }

        private static DateOnly ParseDate(string date)
        {
            return DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string GetDayAbbreviationPtBr(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "SEG.",
                DayOfWeek.Tuesday => "TER.",
                DayOfWeek.Wednesday => "QUA.",
                DayOfWeek.Thursday => "QUI.",
                DayOfWeek.Friday => "SEX.",
                DayOfWeek.Saturday => "SÁB.",
                DayOfWeek.Sunday => "DOM.",
                _ => string.Empty
            };
        }

        private static string NormalizeTime(string? time)
        {
            if (string.IsNullOrWhiteSpace(time))
            {
                return string.Empty;
            }

            return time.Length >= 5 ? time.Substring(0, 5) : time;
        }

        private static string NormalizeForComparison(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString()
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
        }

        private static List<ParticipantPayload> BuildParticipants(UserDto authUser, ParticipanteSettings participante)
        {
            var primary = new ParticipantPayload
            {
                Name = authUser.Name ?? string.Empty,
                Cpf = DigitsOnly(authUser.Cpf),
                Rg = DigitsOnly(authUser.Rg),
                Phone = DigitsOnly(authUser.Phone?.ToString(CultureInfo.InvariantCulture)),
                Email = authUser.Email ?? string.Empty
            };

            var secondary = new ParticipantPayload
            {
                Name = participante.Nome,
                Cpf = DigitsOnly(participante.Cpf),
                Rg = DigitsOnly(participante.Rg),
                Phone = DigitsOnly(participante.Telefone),
                Email = participante.Email
            };

            if (NormalizeForComparison(primary.Email) == NormalizeForComparison(secondary.Email) ||
                NormalizeForComparison(primary.Cpf) == NormalizeForComparison(secondary.Cpf))
            {
                throw new InvalidOperationException("Participante do formulário não pode duplicar usuário autenticado.");
            }

            return new List<ParticipantPayload> { primary, secondary };
        }

        private static string DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value, @"\D", string.Empty);
        }

        private static void LoadDotEnv(string path)
        {
            if (!File.Exists(path)) return;

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (trimmed.StartsWith("#")) continue;

                var idx = trimmed.IndexOf('=');
                if (idx <= 0) continue;

                var key = trimmed.Substring(0, idx).Trim();
                var value = trimmed.Substring(idx + 1).Trim();
                value = value.Trim('"');

                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}































