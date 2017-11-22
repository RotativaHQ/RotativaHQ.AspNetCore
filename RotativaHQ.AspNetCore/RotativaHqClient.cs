using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RotativaHQ.AspNetCore
{
    public sealed class GzipContent : HttpContent
    {
        private readonly HttpContent content;

        public GzipContent(HttpContent content)
        {
            this.content = content;

            // Keep the original content's headers ...
            foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // ... and let the server know we've Gzip-compressed the body of this request.
            Headers.ContentEncoding.Add("gzip");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // Open a GZipStream that writes to the specified output stream.
            using (GZipStream gzip = new GZipStream(stream, CompressionMode.Compress, true))
            {
                // Copy all the input content to the GZip stream.
                await content.CopyToAsync(gzip);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
    public sealed class GzipCompressingHandler : DelegatingHandler
    {
        public GzipCompressingHandler(HttpMessageHandler innerHandler)
        {
            if (null == innerHandler)
            {
                throw new ArgumentNullException("innerHandler");
            }

            InnerHandler = innerHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpContent content = request.Content;

            if (request.Method == HttpMethod.Post)
            {
                // Wrap the original HttpContent in our custom GzipContent class.
                // If you want to compress only certain content, make the decision here!
                request.Content = new GzipContent(request.Content);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
    public class RotativaHqClient
    {
        string apiKey;

        public RotativaHqClient(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public string GetPdfUrl(HttpContext context, string switches, string html, string fileName = "", string header = "", string footer = "", string contentDisposition = "")
        {
                var webRoot = string.Format("{0}://{1}{2}",
                    context.Request.Scheme,
                    context.Request.Host.Host,
                    context.Request.Host.Port == 80
                      ? string.Empty : ":" + context.Request.Host.Port);
                webRoot = webRoot.TrimEnd('/');
                var requestPath = context.Request.Path;
                var packageBuilder = new PackageBuilder(new MapPathResolver(), webRoot);
                var task1 = Task.Run(async () => {
                    await packageBuilder.AddHtmlToPackage(html, requestPath, "index");
                });
                task1.Wait();
                if (!string.IsNullOrEmpty(header))
                {
                    var task2 = Task.Run(async () => { await packageBuilder.AddHtmlToPackage(header, requestPath, "header"); });
                    task2.Wait();
                }
                if (!string.IsNullOrEmpty(footer))
                {
                    var task2 = Task.Run(async () => { await packageBuilder.AddHtmlToPackage(footer, requestPath, "footer"); });
                    task2.Wait();
                }
                var assets = packageBuilder.AssetsContents
                    .Select(a => new KeyValuePair<string, byte[]>(
                        a.NewUri + "." + a.Suffix, a.Content))
                    .ToDictionary(x => x.Key, x => x.Value);
                var payload = new PdfRequestPayloadV2
                {
                    Id = Guid.NewGuid(),
                    Filename = fileName,
                    Switches = switches,
                    HtmlAssets = assets
                };
                string gzipIt = "1";
                //if (HttpContext.Current != null && HttpContext.Current.Request.IsLocal && gzipIt == null)
                //{
                //    gzipIt = "1";
                //}
                if (gzipIt == "1")
                {
                    var httpClient = new HttpClient(new GzipCompressingHandler(new HttpClientHandler()));
                    using (
                        var request = CreateRequest("/v2", "application/json", HttpMethod.Post))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            var sw = new StreamWriter(ms);//, new UnicodeEncoding());
                            Serializer.Serialize(ms, payload);
                            ms.Position = 0;
                            HttpContent content = new StreamContent(ms);
                            request.Content = content; // new GzipContent(content);
                            using (
                                HttpResponseMessage response =
                                    httpClient.SendAsync(request, new CancellationTokenSource().Token).Result)
                            {
                                var httpResponseMessage = response;
                                var result = response.Content.ReadAsStringAsync();
                                var jsonReponse = JObject.Parse(result.Result);
                                if (response.StatusCode == HttpStatusCode.Unauthorized)
                                {
                                    var error = jsonReponse["error"].Value<string>();
                                    throw new UnauthorizedAccessException(error);
                                }
                                var pdfUrl = jsonReponse["pdfUrl"].Value<string>(); // 
                                return pdfUrl;
                            }
                        }
                    }
                }
                else
                {
                    var httpClient = new HttpClient();
                    using (
                        var request = CreateRequest("/v2", "application/json", HttpMethod.Post))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            var sw = new StreamWriter(ms, new UnicodeEncoding());
                            Serializer.Serialize(ms, payload);
                            ms.Position = 0;
                            HttpContent content = new StreamContent(ms);
                            request.Content = content;

                            using (
                                HttpResponseMessage response =
                                    httpClient.SendAsync(request, new CancellationTokenSource().Token).Result)
                            {
                                var httpResponseMessage = response;
                                var result = response.Content.ReadAsStringAsync();
                                var jsonReponse = JObject.Parse(result.Result);
                                if (response.StatusCode == HttpStatusCode.Unauthorized)
                                {
                                    var error = jsonReponse["error"].Value<string>();
                                    throw new UnauthorizedAccessException(error);
                                }
                                var pdfUrl = jsonReponse["pdfUrl"].Value<string>(); // 
                                return pdfUrl;
                            }
                        }
                    }
                }


        }

        /// <summary>
        /// This method is taken from Filip W in a blog post located at: http://www.strathweb.com/2012/06/asp-net-web-api-integration-testing-with-in-memory-hosting/
        /// </summary>
        /// <param name="url"></param>
        /// <param name="mthv"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        protected HttpRequestMessage CreateRequest(string url, string mthv, HttpMethod method)
        {
            var request = CreateRawRequest(url, mthv, method);
            request.Headers.Add("X-ApiKey", apiKey);
            return request;
        }

        protected HttpRequestMessage CreateRawRequest(string url, string mthv, HttpMethod method)
        {
            var apiUrl = RotativaHqConfiguration.RotativaHqUrl;
            if (apiUrl == null)
            {
                throw new InvalidOperationException("Endpoint URL not defined.");
            }
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(apiUrl + url)
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mthv));
            request.Method = method;
            return request;
        }
    }
}
