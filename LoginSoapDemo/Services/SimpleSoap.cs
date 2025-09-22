using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LoginSoapDemo.Services
{
    public static class SimpleSoap
    {
        // SOAP 1.1: Content-Type text/xml, нужен заголовок SOAPAction
        public static async Task<(bool ok, string xml, int code)> PostSoap11Async(
            string url, string soapAction, string envelopeXml)
        {
            using var http = new HttpClient();
            var content = new StringContent(envelopeXml, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", $"\"{soapAction}\"");
            var resp = await http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            return ((int)resp.StatusCode is >= 200 and < 300, body, (int)resp.StatusCode);
        }

        // SOAP 1.2: Content-Type application/soap+xml; action="..."
        public static async Task<(bool ok, string xml, int code)> PostSoap12Async(
            string url, string soapAction, string envelopeXml)
        {
            using var http = new HttpClient();
            var ct = $"application/soap+xml; charset=utf-8; action=\"{soapAction}\"";
            var content = new StringContent(envelopeXml, Encoding.UTF8);
            content.Headers.Clear();
            content.Headers.TryAddWithoutValidation("Content-Type", ct);
            var resp = await http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            return ((int)resp.StatusCode is >= 200 and < 300, body, (int)resp.StatusCode);
        }
    }
}
