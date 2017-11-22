using AngleSharp.Dom;
using AngleSharp.Extensions;
using AngleSharp.Html;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RotativaHQ.AspNetCore
{
    public static class Zipper
    {
        private static bool DetectBundle(string url)
        {
            var queryString = url.Split('?').LastOrDefault();
            var localPath = url;
            if (queryString != null && !url.Contains('.'))
            {
                return true;
            }
            return false;
        }

        public static async Task<string> ReturnLocalPath(string url)
        {
            //if (DetectBundle(url))
            //    return string.Empty;

            if (!url.ToLower().StartsWith("http"))
            {
                return url;
            }
            Uri uri = new Uri(url);
            var hostname = uri.Host;
            if (await IsLocalhost(hostname))
            {
                return uri.LocalPath;
            }
            return string.Empty;
        }

        // TODO: refactor please. Method says Add but in fact it also modifies the elements, 
        // not clear side effect
        public static async Task AddSerializedAssets(
            this Dictionary<string, string> serialAssets, 
            IEnumerable<IElement> elements, 
            string uriAttribute
            )
        {
            foreach (var element in elements)
            {
                var src = element.Attributes[uriAttribute].Value;
                // remove any querystring from images
                if (element.TagName.ToLower() == "img")
                {
                    var qI = src.LastIndexOf('?');
                    if (qI > 0) src = src.Substring(0, qI);
                    var hI = src.LastIndexOf('#');
                    if (hI > 0) src = src.Substring(0, hI);
                }
                var localPath = await ReturnLocalPath(src);
                if (localPath != string.Empty)
                {
                    var suffix = src.Split('.').Last();
                    if (suffix == localPath)
                    {
                        switch (element.TagName.ToLower())
                        {
                            case "link":
                                suffix = "css";
                                break;
                            case "script":
                                suffix = "js";
                                break;
                            default:
                                break;
                        };
                    }
                    var newSrc = Guid.NewGuid().ToString().Replace("-", "") + "." + suffix;
                    element.Attributes[uriAttribute].Value = newSrc;
                    serialAssets.Add(localPath, newSrc);
                }
            }
        }

        public static List<string> ExtaxtUrlsFromStyle(string styleContent)
        {
            var list = new List<string>();
            var reg = @"url *(?:\(['|""]?)(.*?)(?:['|""]?\))";
            Regex regex = new Regex(reg);
            MatchCollection matches = regex.Matches(styleContent);
            foreach (Match match in matches)
            {
               foreach (Group group in match.Groups)
               {
                   if (group.Value != match.Value)
                   {
                       var url = group.Value;
                       var qI = url.LastIndexOf('?');
                       if (qI > 0) url = url.Substring(0, qI);
                       var hI = url.LastIndexOf('#');
                       if (hI > 0) url = url.Substring(0, hI);
                       list.Add(url);
                   }
               }
            }
            return list.Distinct().ToList();
        }


        public static async Task<byte[]> ZipPage(string html, IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            var parser = new AngleSharp.Parser.Html.HtmlParser();
            var doc = parser.Parse(html);
            var images = doc.Images
                .Where(x => x.HasAttribute("src"));
            var styles = doc.GetElementsByTagName("link")
                .Where(l => l.Attributes["rel"].Value.Trim().ToLower() == "stylesheet")
                .Where(c => c.HasAttribute("href"));
            var scripts = doc.GetElementsByTagName("script")
                .Where(x => x.HasAttribute("src"));
            var serialAssets = new Dictionary<string, string>();
            await serialAssets.AddSerializedAssets(images, "src");
            await serialAssets.AddSerializedAssets(scripts, "src");
            var serialStyles = new Dictionary<string, string>();
            await serialStyles.AddSerializedAssets(styles, "href");
            
            var newHtml = doc.ToHtml(new HtmlMarkupFormatter());
            var doneAssets = new List<string>();
            using (var ms = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
    			{
    			    //foreach (var attachment in attachmentFiles)
    			    {
    			        var entry = zipArchive.CreateEntry("index.html", CompressionLevel.Fastest);
                        using (StreamWriter writer = new StreamWriter(entry.Open()))
                        {
                            try
                            {
                                writer.Write(newHtml);
                            }
                            catch (Exception ex)
                            {
                                throw;
                            }
                            doneAssets.Add("index.html");
                        }
    			    }
                    foreach (var serialStyle in serialStyles)
                    {
                        if (!doneAssets.Contains(serialStyle.Value))
                        {
                            var style = await
                                GetStringAsset(serialStyle.Key, mapPathResolver, webRoot, pagePath);
                            if (!string.IsNullOrEmpty(style))
                            {
                                var urls = ExtaxtUrlsFromStyle(style);
                                foreach (var url in urls)
                                {
                                    var localPath = await ReturnLocalPath(url);
                                    var suffix = localPath.Split('.').Last();
                                    var newUrl = Guid.NewGuid().ToString().Replace("-", "") + "." + suffix;
                                    style = style.Replace(url, newUrl);
                                    if (!doneAssets.Contains(newUrl))
                                    {
                                        await zipArchive.AddBinaryAssetToArchive(newUrl, localPath, mapPathResolver, webRoot, serialStyle.Key);
                                        doneAssets.Add(newUrl);
                                    }
                                }
                                var sentry = zipArchive.CreateEntry(serialStyle.Value, CompressionLevel.Fastest);
                                using (StreamWriter writer = new StreamWriter(sentry.Open()))
                                {
                                    writer.Write(style);
                                }
                                doneAssets.Add(serialStyle.Value);
                            }
                        }
                    }
                    foreach (var serialAsset in serialAssets)
                    {
                        if (!doneAssets.Contains(serialAsset.Value))
                        {
                            await zipArchive.AddBinaryAssetToArchive(
                                serialAsset.Value, serialAsset.Key, mapPathResolver, webRoot, pagePath);
                            doneAssets.Add(serialAsset.Value);
                        }
                    }
    			}
                return ms.ToArray();
            }
        }

        public static async Task<string> GetStringAsset(string path, IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            if (DetectBundle(path))
            {
                using (var webClient = new HttpClient())
                {
                    try
                    {
                        var style = await webClient.GetStringAsync(webRoot + path);
                        return style;
                    }
                    catch (WebException wex)
                    {
                        // TODO: log web exception
                        return string.Empty;
                    }
                }
            }
            var localpath = mapPathResolver.MapPath(pagePath, path);
            if (File.Exists(localpath))
            { 
                var style = File.ReadAllText(localpath);
                return style;
            }
            else
            {
                return string.Empty;
            }
        }

        public static async Task<byte[]> GetBinaryAsset(string path, IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            if (DetectBundle(path))
            {
                using (var webClient = new HttpClient())
                {
                    var asset = await webClient.GetByteArrayAsync(webRoot + path);
                    return asset;
                }
            }

            var localpath = mapPathResolver.MapPath(pagePath, path);
            if (File.Exists(localpath))
            {
                var asset = File.ReadAllBytes(localpath);
                return asset;
            }
            else
            {
                return new byte[] { };
            }
        }

        public static async Task AddBinaryAssetToArchive(
            this ZipArchive zipArchive, 
            string serialAssetName, 
            string serialAssetPath,
            IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            
            var asset = await GetBinaryAsset(serialAssetPath, mapPathResolver, webRoot, pagePath);
            if (asset.Length > 0)
            {
                var nentry = zipArchive.CreateEntry(serialAssetName, CompressionLevel.Fastest);
                using (var writer = new BinaryWriter(nentry.Open()))
                {
                    writer.Write(asset);
                }
            }
        }

        private static async Task<bool> IsLocalhost(string hostNameOrAddress)
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
                return false;

            try
            {
                // get host IP addresses
                IPAddress[] hostIPs = await Dns.GetHostAddressesAsync(hostNameOrAddress);
                // get local IP addresses
                IPAddress[] localIPs = await Dns.GetHostAddressesAsync(Dns.GetHostName());
                // test if any host IP is a loopback IP or is equal to any local IP
                return hostIPs.Any(hostIP => IPAddress.IsLoopback(hostIP) || localIPs.Contains(hostIP));
            }
            catch
            {
                return false;
            }
        }
    }
}
