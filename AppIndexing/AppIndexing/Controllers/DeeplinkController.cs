using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using AppIndexing.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace AppIndexing.Controllers
{
    public class DeeplinkController : ApiController
    {
        // https://developers.google.com/app-indexing/webmasters/server

        private const int ConcurrentRequestLimit = 20;
        private readonly static SemaphoreSlim throttle = new SemaphoreSlim(ConcurrentRequestLimit);
        private List<object> errors = new List<object>();

        [HttpPost]
        public async Task<DeeplinkResult> GetDeeplinksAsync([FromBody]DeeplinkRequest request)
        {
            if (request == null)
            {
                return new DeeplinkResult();
            }

            var tasks = request.Urls.Select(GetAlternateLinksForUrlAsync);
            var results = await Task.WhenAll(tasks);

            return new DeeplinkResult
            {
                Errors = errors.Count > 0 ? errors : null,
                Request = request,
                Links = results
            };
        }

        private async Task<string[]> GetAlternateLinksForUrlAsync(string url)
        {
            string html = await GetHtmlForUrlAsync(url);

            if (html == null)
            {
                return new string[0];
            }

            HtmlDocument doc = new HtmlDocument();

            doc.LoadHtml(html);

            var links = GetAlternateLinksForHtmlDocument(doc);
            var viewActions = GetViewActionsForHtmlDocument(doc);

            return links
                .Concat(viewActions)
                .Where(x => x.StartsWith("android-app://", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
        }

        private static string[] GetAlternateLinksForHtmlDocument(HtmlDocument html)
        {
            return html
                .DocumentNode
                .Descendants("link")
                .Where(IsAlternateLinkElement)
                .Select(element => element.GetAttributeValue("href", "").Trim())
                .Where(href => href.Length > 0)
                .ToArray();
        }

        private static bool IsAlternateLinkElement(HtmlNode element)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(element.GetAttributeValue("rel", "").Trim(), "alternate");
        }

        private string[] GetViewActionsForHtmlDocument(HtmlDocument html)
        {
            return html
                .DocumentNode
                .Descendants("script")
                .Where(IsViewActionScriptElement)
                .Select(GetViewActionTargetFromScript)
                .ToArray();
        }

        private static bool IsViewActionScriptElement(HtmlNode element)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(element.GetAttributeValue("type", "").Trim(), "application/ld+json");
        }

        private string GetViewActionTargetFromScript(HtmlNode element)
        {
            string script = element.InnerHtml;

            PageSchemaModel pageSchema = JsonConvert.DeserializeObject<PageSchemaModel>(script);

            if (pageSchema.PotentialAction != null &&
                StringComparer.OrdinalIgnoreCase.Equals(pageSchema.PotentialAction.Type, "ViewAction"))
            {
                return pageSchema.PotentialAction.Target;
            }

            return null;
        }

        private async Task<string> GetHtmlForUrlAsync(string url)
        {
            await throttle.WaitAsync();

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

                ApplyCurrentRequest(request);

                request.AllowAutoRedirect = true;

                HttpWebResponse response;

                try
                {
                    response = (HttpWebResponse)await request.GetResponseAsync();
                }
                catch (WebException e)
                {
                    response = e.Response as HttpWebResponse;
                }

                if (response == null)
                {
                    return null;
                }

                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            finally
            {
                throttle.Release();
            }
        }

        private readonly static Action<HttpWebRequest, string, string> NoAssignment = (request, name, value) => { };
        private readonly static Action<HttpWebRequest, string, string> DefaultAssignment = (request, name, value) => request.Headers.Add(name, value);
        private readonly static IDictionary<string, Action<HttpWebRequest, string, string>> requestParameterAssignments = new Dictionary<string, Action<HttpWebRequest, string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "user-agent", (request, name, value) => request.UserAgent = value },
            { "accept", (request, name, value) => request.Accept = value },
            { "connection", NoAssignment },
            { "host", NoAssignment },
        };

        private void ApplyCurrentRequest(HttpWebRequest request)
        {
            foreach (var header in this.Request.Headers)
            {
                Action<HttpWebRequest, string, string> assignment;
                if (!requestParameterAssignments.TryGetValue(header.Key, out assignment))
                {
                    assignment = DefaultAssignment;
                }

                foreach (var value in header.Value)
                {
                    try
                    {
                        assignment(request, header.Key, value);
                    }
                    catch (Exception e)
                    {
                        errors.Add(e.Message);
                    }
                }
            }
        }
    }
}
