using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
    // Selenium Helper (único ponto de driver)
    // ==============================
    public static class SeleniumHelper
    {
        private static IWebDriver? _driver;
        private static readonly int DefaultTimeoutSeconds =
            int.TryParse(Environment.GetEnvironmentVariable("WAIT_SECONDS"), out var seconds) && seconds > 0
                ? seconds
                : 15;

        public static void Start()
        {
            if (_driver != null) return;

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-extensions");

            var headless = string.Equals(Environment.GetEnvironmentVariable("HEADLESS"), "true", StringComparison.OrdinalIgnoreCase);
            if (headless)
            {
                options.AddArgument("--headless=new");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
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

    public class AguardarAntesConfirmarAgendamentoStep : IPipelineStep
    {
        public void Execute(PipelineContext context)
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
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
        public static void Main()
        {
            LoadDotEnv(Path.Combine(AppContext.BaseDirectory, ".env"));
            LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var settings = config.Get<AppSettings>();
            if (settings == null || settings.Agendamento == null)
            {
                throw new InvalidOperationException("Configuração inválida: seção Agendamento não encontrada.");
            }

            var ag = settings.Agendamento;

            var contexts = ag.Logins.Select(login => new AgendamentoContext
            {
                UrlLogin = ag.UrlLogin,
                UrlQuadra = ag.UrlQuadra,
                TextoDia = ag.TextoDia,
                Horario = login.Horario,
                Email = login.Email,
                Senha = login.Senha,
                ParticipanteNome = ag.Participante.Nome,
                ParticipanteCpf = ag.Participante.Cpf,
                ParticipanteRg = ag.Participante.Rg,
                ParticipanteTelefone = ag.Participante.Telefone,
                ParticipanteEmail = ag.Participante.Email
            }).ToList();

            var pipeline = new PipelineBuilder()
                .AddStep(new StartBrowserStep())
                .AddStep(new LoginStep())
                .AddStep(new NavegarQuadraStep())
                .AddStep(new SelecionarDiaStep())
                .AddStep(new SelecionarHorarioStep())
                .AddStep(new MarcarSouParticipanteStep())
                .AddStep(new AbrirModalOutroParticipanteStep())
                .AddStep(new PreencherParticipanteStep())
                .AddStep(new ConfirmarParticipanteStep())
                .AddStep(new AguardarAntesConfirmarAgendamentoStep())
                .AddStep(new ConfirmarAgendamentoStep())
                .AddStep(new FinalizarStep())
                .AddStep(new StopBrowserStep())
                .Build();

            foreach (var context in contexts)
            {
                try
                {
                    pipeline.Execute(context);
                    Console.WriteLine($"Agendamento executado com sucesso para {context.Email}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro para {context.Email}: {ex.Message}");
                    SeleniumHelper.Quit();
                }
            }
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































