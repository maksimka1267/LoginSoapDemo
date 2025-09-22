using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    // === Константы сервиса (рабочая зв'язка) ===
    private const string ENDPOINT = "http://isapi.mekashron.com/icu-tech/icutech-test.dll/soap/IICUTech";
    private const string NS = "http://tempuri.org/";
    private const string ACTION_LOGIN = NS + "Login";

    // === Поля форми (тільки логін) ===
    [BindProperty, Required] public string Email { get; set; } = string.Empty;   // Login.UserName
    [BindProperty, Required] public string Password { get; set; } = string.Empty;
    [BindProperty] public string? SignupIP { get; set; }

    // === Вивід на сторінку ===
    public bool? IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public string? RawXml { get; set; }

    public void OnGet() { }

    // ЄДИНИЙ POST: підтримуємо тільки handler=login
    public async Task<IActionResult> OnPostAsync([FromForm] string handler)
    {
        var h = (handler ?? "").Trim().ToLowerInvariant();

        try
        {
            if (h == "login" || string.IsNullOrEmpty(h))
            {
                await CallLoginAsync();
            }
            else
            {
                IsSuccess = false;
                Message = "Реєстрація вимкнена в цій збірці. Доступний тільки вхід.";
            }
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            Message = $"Помилка: {ex.Message}";
        }

        return Page();
    }

    // --------- LOGIN (SOAP 1.2) ---------
    private async Task CallLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            IsSuccess = false;
            Message = "Введіть логін та пароль.";
            return;
        }

        var ip = SignupIP ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

        var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"">
  <soap:Body>
    <Login xmlns=""{NS}"">
      <UserName>{System.Security.SecurityElement.Escape(Email)}</UserName>
      <Password>{System.Security.SecurityElement.Escape(Password)}</Password>
      <IPs>{System.Security.SecurityElement.Escape(ip)}</IPs>
    </Login>
  </soap:Body>
</soap:Envelope>";

        var (ok, body, msg) = await SendSoap12Async(ENDPOINT, envelope, ACTION_LOGIN);
        RawXml = body;

        if (!ok)
        {
            IsSuccess = false;
            Message = msg;
            return;
        }

        // У сервісу відповідь часто в <return> як рядок (іноді JSON)
        var text = SafeGetValue(body, "return") ?? SafeGetValue(body, "LoginResult");

        var asJson = TryParseJson<ServiceResult>(text);
        if (asJson is not null)
        {
            IsSuccess = asJson.ResultCode == 0;
            Message = string.IsNullOrWhiteSpace(asJson.ResultMessage)
                ? "Login response received."
                : asJson.ResultMessage!;
        }
        else
        {
            // простий рядок → вважаємо помилкою, якщо виглядає як системна
            var t = (text ?? "").Trim();

            if (t.Contains("Cannot open file", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("FileZilla", StringComparison.OrdinalIgnoreCase))
            {
                IsSuccess = false;
                Message = "Сервіс авторизації тимчасово недоступний на стороні постачальника. Спробуйте пізніше.";
            }
            else
            {
                IsSuccess = t.Length > 0 && !LooksError(t);
                Message = t.Length == 0 ? "Login response received." : t;
            }
        }
    }

    // ===== Transport: SOAP 1.2, без редиректів =====
    private static async Task<(bool ok, string body, string msg)> SendSoap12Async(string endpoint, string envelopeXml, string action)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new HttpClient(handler);

        var content = new StringContent(envelopeXml, Encoding.UTF8);
        content.Headers.Clear();
        content.Headers.TryAddWithoutValidation("Content-Type", $"application/soap+xml; charset=utf-8; action=\"{action}\"");

        HttpResponseMessage resp;
        string body;
        try
        {
            resp = await http.PostAsync(endpoint, content);
            body = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return (false, ex.ToString(), $"Помилка з'єднання: {ex.Message}");
        }

        if (!resp.IsSuccessStatusCode)
            return (false, body, $"HTTP {(int)resp.StatusCode}");

        var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
        var looksXml = ct.Contains("xml", StringComparison.OrdinalIgnoreCase)
                       || (body.TrimStart().StartsWith("<") && body.Contains("Envelope"));
        if (!looksXml)
            return (false, body, "Отримано не XML (ймовірно HTML).");

        return (true, body, "OK");
    }

    // ===== Helpers =====
    private static string? SafeGetValue(string xml, string localName)
    {
        try
        {
            var d = XDocument.Parse(xml);
            return d.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        }
        catch { return null; }
    }

    private static bool LooksError(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("error") || t.Contains("cannot") || t.Contains("exception") || t.Contains("fail");
    }

    private sealed class ServiceResult
    {
        public int ResultCode { get; set; }
        public string? ResultMessage { get; set; }
        public int EntityId { get; set; }
        public int AffiliateResultCode { get; set; }
        public string? AffiliateResultMessage { get; set; }
    }

    private static T? TryParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return default; }
    }
}
