using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


namespace LagoaSportRpa;

public sealed class ExecutarAgendamentoRequest
{
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string Horario { get; set; } = string.Empty;
}

public sealed class FixedSettings
{
    public string UrlLogin { get; set; } = "https://lagoasport.lagoasanta.mg.gov.br/login";
    public string UrlQuadra { get; set; } = "https://lagoasport.lagoasanta.mg.gov.br/locations/2";
    public string TextoDia { get; set; } = "SEX.";
    public int DuracaoHoras { get; set; } = 1;
    public ParticipanteSettings Participante { get; set; } = new();
}

public sealed class ParticipanteSettings
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Rg { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class ExecutarAgendamentoResponse
{
    public bool Success { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Horario { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string UrlLogin { get; set; } = string.Empty;
    public string UrlQuadra { get; set; } = string.Empty;
    public string TextoDia { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
}

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

public sealed class BrowserLoginSession
{
    public required CookieContainer Cookies { get; init; }
}

public static class BrowserLoginService
{
    public static Task<BrowserLoginSession> LoginAsync(string loginUrl, string email, string senha)
    {
        return Task.Run(() =>
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            var chromeBin = Environment.GetEnvironmentVariable("CHROME_BIN");
            if (!string.IsNullOrWhiteSpace(chromeBin))
            {
                options.BinaryLocation = chromeBin;
            }

            var driverPath = Environment.GetEnvironmentVariable("CHROMEDRIVER_PATH");
            var service = string.IsNullOrWhiteSpace(driverPath)
                ? ChromeDriverService.CreateDefaultService()
                : ChromeDriverService.CreateDefaultService(driverPath);
            service.HideCommandPromptWindow = true;

            using var driver = new ChromeDriver(service, options);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

            driver.Navigate().GoToUrl(loginUrl);
            wait.Until(d => d.FindElement(By.Id("email")));

            driver.FindElement(By.Id("email")).Clear();
            driver.FindElement(By.Id("email")).SendKeys(email);
            driver.FindElement(By.Id("password")).Clear();
            driver.FindElement(By.Id("password")).SendKeys(senha);
            driver.FindElement(By.XPath("//button[@type='submit']")).Click();

            wait.Until(d => !d.Url.Contains("/login", StringComparison.OrdinalIgnoreCase));

            var jar = new CookieContainer();
            var baseUri = new Uri(loginUrl);
            foreach (var cookie in driver.Manage().Cookies.AllCookies)
            {
                var netCookie = new System.Net.Cookie(
                    cookie.Name,
                    cookie.Value,
                    string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                    string.IsNullOrWhiteSpace(cookie.Domain) ? baseUri.Host : cookie.Domain.TrimStart('.'));

                netCookie.Secure = cookie.Secure;
                netCookie.HttpOnly = cookie.IsHttpOnly;

                jar.Add(baseUri, netCookie);
            }

            return new BrowserLoginSession { Cookies = jar };
        });
    }
}

public sealed class LagoaSportApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CookieContainer _cookies;
    private readonly HttpClient _http;

    public LagoaSportApiClient(string baseUrl, CookieContainer? cookies = null)
    {
        _cookies = cookies ?? new CookieContainer();
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
        request.Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("email", email),
            new("password", senha)
        });

        if (!string.IsNullOrWhiteSpace(xsrf))
        {
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", Uri.UnescapeDataString(xsrf));
        }

        using var response = await _http.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"POST login HTTP falhou: {(int)response.StatusCode} {response.ReasonPhrase}. {TruncateForError(html)}");
        }

        var page = ParsePage<DashboardProps>(html);
        if (page.Props?.Auth?.User == null)
        {
            throw new InvalidOperationException("Login HTTP falhou: usuário não retornou em dashboard.");
        }

        return page;
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
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", Uri.UnescapeDataString(xsrf));
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
                $"{method} {relativeUrl} HTTP falhou: {(int)response.StatusCode} {response.ReasonPhrase}. {TruncateForError(body)}");
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

public static class Program
{
    public static async Task Main(string[] args)
    {
        LoadDotEnv(Path.Combine(AppContext.BaseDirectory, ".env"));
        LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:8080");

        var app = builder.Build();

        app.MapGet("/", () => Results.Ok(new { status = "ok", service = "lagoa-sport-rpa" }));
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/executar", async (HttpRequest httpRequest, CancellationToken ct) =>
        {
            var request = await ReadRequestAsync(httpRequest);
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Senha) ||
                string.IsNullOrWhiteSpace(request.Horario))
            {
                return Results.BadRequest(new
                {
                    error = "Campos obrigatórios: email, senha, horario."
                });
            }

            var settings = ReadFixedSettings();
            try
            {
                var result = await ExecutarUmaVezAsync(settings, request, ct);
                return Results.Ok(result);
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(499);
            }
            catch (Exception ex)
            {
                return Results.Ok(new ExecutarAgendamentoResponse
                {
                    Success = false,
                    Email = request.Email,
                    Horario = request.Horario,
                    Message = ex.Message,
                    UrlLogin = settings.UrlLogin,
                    UrlQuadra = settings.UrlQuadra,
                    TextoDia = settings.TextoDia,
                    StartedAt = DateTimeOffset.UtcNow,
                    FinishedAt = DateTimeOffset.UtcNow
                });
            }
        });

        app.Run();
    }

    private static async Task<ExecutarAgendamentoRequest> ReadRequestAsync(HttpRequest httpRequest)
    {
        if (httpRequest.ContentLength.GetValueOrDefault() > 0 &&
            (httpRequest.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var payload = await JsonSerializer.DeserializeAsync<ExecutarAgendamentoRequest>(
                httpRequest.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (payload != null)
            {
                return payload;
            }
        }

        return new ExecutarAgendamentoRequest
        {
            Email = httpRequest.Query["email"].ToString(),
            Senha = httpRequest.Query["senha"].ToString(),
            Horario = httpRequest.Query["horario"].ToString()
        };
    }

    private static async Task<ExecutarAgendamentoResponse> ExecutarUmaVezAsync(
        FixedSettings settings,
        ExecutarAgendamentoRequest request,
        CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var locationId = ExtractLocationId(settings.UrlQuadra);

        var browserSession = await BrowserLoginService.LoginAsync(settings.UrlLogin, request.Email, request.Senha);
        using var api = new LagoaSportApiClient("https://lagoasport.lagoasanta.mg.gov.br", browserSession.Cookies);

        var locationPage = await api.GetLocationAsync(locationId);
        ct.ThrowIfCancellationRequested();

        var authUser = (locationPage.Props?.Auth?.User)
                       ?? throw new InvalidOperationException("Usuário autenticado não retornou na sessão.");

        var activeReservation = await api.GetActiveReservationAsync();
        var slotId = activeReservation.Slot?.Id;

        if (slotId != null)
        {
            var sameLocation = activeReservation.Slot?.LocationId == locationId;
            var sameTime = string.Equals(
                NormalizeForComparison($"{NormalizeTime(activeReservation.Slot?.StartTime)} às {NormalizeTime(activeReservation.Slot?.EndTime)}"),
                NormalizeForComparison(request.Horario),
                StringComparison.Ordinal);

            if (!sameLocation || !sameTime)
            {
                await api.CancelReservationAsync(slotId.Value);
                slotId = null;
            }
        }

        if (slotId == null)
        {
            slotId = SelectSlotId(locationPage.Props?.SlotsByDate, settings.TextoDia, request.Horario);
        }

        var appointmentPage = await api.OpenAppointmentAsync(slotId.Value);
        var bookedSlot = appointmentPage.Props?.Slot
                         ?? throw new InvalidOperationException("Slot não retornou na tela de confirmação.");

        var participants = BuildParticipants(authUser, settings.Participante);
        var timeSlotIds = new List<int> { bookedSlot.Id };

        if (settings.DuracaoHoras >= 2 && appointmentPage.Props?.CanExtendTime == true && appointmentPage.Props.NextSlotId > 0)
        {
            timeSlotIds.Add(appointmentPage.Props.NextSlotId);
        }

        var confirmation = await api.SubmitAppointmentAsync(timeSlotIds, participants, appointmentPage.Version, appointmentPage.Url);
        var success = confirmation.Props?.Flash?.Success;
        var message = !string.IsNullOrWhiteSpace(success)
            ? success
            : "Agendado com sucesso.";

        return new ExecutarAgendamentoResponse
        {
            Success = true,
            Email = request.Email,
            Horario = request.Horario,
            Message = message,
            UrlLogin = settings.UrlLogin,
            UrlQuadra = settings.UrlQuadra,
            TextoDia = settings.TextoDia,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow
        };
    }

    private static FixedSettings ReadFixedSettings()
    {
        return new FixedSettings
        {
            UrlLogin = Environment.GetEnvironmentVariable("AGENDAMENTO__URLLOGIN")
                       ?? "https://lagoasport.lagoasanta.mg.gov.br/login",
            UrlQuadra = Environment.GetEnvironmentVariable("AGENDAMENTO__URLQUADRA")
                        ?? "https://lagoasport.lagoasanta.mg.gov.br/locations/2",
            TextoDia = Environment.GetEnvironmentVariable("AGENDAMENTO__TEXTODIA") ?? "SEX.",
            DuracaoHoras = int.TryParse(Environment.GetEnvironmentVariable("AGENDAMENTO__DURACAOHORAS"), out var duracao) && duracao > 0
                ? duracao
                : 1,
            Participante = new ParticipanteSettings
            {
                Nome = Environment.GetEnvironmentVariable("AGENDAMENTO__PARTICIPANTE__NOME") ?? string.Empty,
                Cpf = Environment.GetEnvironmentVariable("AGENDAMENTO__PARTICIPANTE__CPF") ?? string.Empty,
                Rg = Environment.GetEnvironmentVariable("AGENDAMENTO__PARTICIPANTE__RG") ?? string.Empty,
                Telefone = Environment.GetEnvironmentVariable("AGENDAMENTO__PARTICIPANTE__TELEFONE") ?? string.Empty,
                Email = Environment.GetEnvironmentVariable("AGENDAMENTO__PARTICIPANTE__EMAIL") ?? string.Empty
            }
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

        throw new InvalidOperationException($"Horário indisponível ou não encontrado: {horario}");
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

        return time.Length >= 5 ? time[..5] : time;
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
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
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
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            var idx = trimmed.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim().Trim('"');
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
