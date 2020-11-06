using AngleSharp.Dom.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RotativaHQ.AspNetCore
{
    public class Asset
    {
        public string Uri { get; set; }
        public string Suffix { get; set; }
        public string NewUri { get; set; }
    }

    public class AssetContent
    {
        public string Uri { get; set; }
        public string Suffix { get; set; }
        public string NewUri { get; set; }
        public byte[] Content { get; set; }
    }

    public class PackageBuilder
    {
        IMapPathResolver mapPathResolver;
        string htmlPage;
        string webRoot;
        public List<AssetContent> AssetsContents { get; set; }
        //MemoryStream ms;
        //ZipArchive zipArchive;

        public PackageBuilder(IMapPathResolver mapPathResolver, string webRoot)
        {
            this.mapPathResolver = mapPathResolver;
            this.webRoot = webRoot;
            AssetsContents = new List<AssetContent>();
            //ms = new MemoryStream();
            //zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true);
        }

        public async Task<List<Asset>> GetHtmlAssets(IHtmlDocument doc)
        {
            var assets = new List<Asset>();
            var images = doc.Images
                .Where(x => x.HasAttribute("src"));
            var styles = doc.GetElementsByTagName("link")
                .Where(l => l.Attributes["rel"].Value.Trim().ToLower() == "stylesheet")
                .Where(c => c.HasAttribute("href"));
            var scripts = doc.GetElementsByTagName("script")
                .Where(x => x.HasAttribute("src"));
            var inlineStyles = doc.GetElementsByTagName("style");

            //var cssparser = new AngleSharp.Parser.Css.CssParser();
            //var f = cssparser.ParseStylesheet(inlineStyles[0].InnerHtml);
            foreach (var inlineStyle in inlineStyles)
            {
                var inlineStyleAssets = GetCssAssets(inlineStyle.InnerHtml);
                assets.AddRange(inlineStyleAssets);
            }

            foreach (var image in images)
            {
                var src = image.Attributes["src"].Value;
                if (await IsLocalPath(src) && !assets.Any(a => a.Uri == src))
                {
                    var suffix = src.Split('.').Last().Split('?').First().Split('#').First();
                    var asset = new Asset
                    {
                        Uri = src,
                        Suffix = suffix,
                        NewUri = Guid.NewGuid().ToString().Replace("-", "")
                    };
                    assets.Add(asset);
                }
            }
            foreach (var css in styles)
            {
                var src = css.Attributes["href"].Value;
                if (await IsLocalPath(src) && !assets.Any(a => a.Uri == src))
                {
                    var asset = new Asset
                    {
                        Uri = src,
                        Suffix = "css",
                        NewUri = Guid.NewGuid().ToString().Replace("-", "")
                    };
                    assets.Add(asset);
                }
            }
            foreach (var script in scripts)
            {
                var src = script.Attributes["src"].Value;
                if (await IsLocalPath(src) && !assets.Any(a => a.Uri == src))
                {
                    var suffix = src.Split('.').Last().Split('?').First().Split('#').First();
                    if (suffix == src.Split('?').First())
                        suffix = "js";
                    var asset = new Asset
                    {
                        Uri = src,
                        Suffix = suffix,
                        NewUri = Guid.NewGuid().ToString().Replace("-", "")
                    };
                    assets.Add(asset);
                }
            }

            return assets;

        }

        public async Task<List<Asset>> GetHtmlAssets(string html)
        {
            var assets = new List<Asset>();
            var parser = new AngleSharp.Parser.Html.HtmlParser();
            var doc = parser.Parse(html.ToLowerInvariant());
            assets = await GetHtmlAssets(doc);
            return assets;
        }

        public List<Asset> GetCssAssets(string css)
        {
            var assets = new List<Asset>();
            var urls = Zipper.ExtaxtUrlsFromStyle(css);
            foreach (var src in urls.Where(s => IsLocalPath(s).Result))
            {
                var suffix = src.Split('.').Last().Split('?').First().Split('#').First();
                var asset = new Asset
                {
                    Uri = src,
                    Suffix = suffix,
                    NewUri = Guid.NewGuid().ToString().Replace("-", "")
                };
                assets.Add(asset);
            }

            return assets;
        }

        public static async Task<bool> IsLocalPath(string url)
        {
            if (!url.ToLower().StartsWith("http://") && !url.StartsWith("//"))
            {
                return true;
            }
            Uri uri = new Uri(url);
            var hostname = uri.Host;
            var isLocaHost = await IsLocalhost(hostname);
            return isLocaHost;
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

        public async Task AddBinaryAssetsContents(List<AssetContent> currentContents, List<Asset> assets, string pagePath)
        {
            foreach (var asset in assets)
            {
            	try
            	{
            	    var assetContent = await GetBinaryAsset(
            	        asset.Uri, this.mapPathResolver, this.webRoot, pagePath);
                    if (assetContent != null && assetContent.Length > 0 && !currentContents.Any(a => a.Uri == asset.Uri))
            	    {
                        currentContents.Add(new AssetContent
            	        {
            	            Uri = asset.Uri,
            	            NewUri = asset.NewUri,
            	            Suffix = asset.Suffix,
            	            Content = assetContent
            	        });
            	    }
            	}
            	catch (Exception ex)
            	{
            	    // TODO: trace somewhere
            	}
            }
        }

        public async Task<List<AssetContent>> GetAssetsContents(string html, string pagePath, string htmlName)
        {
            var assetsContents = new List<AssetContent>();
            var parser = new AngleSharp.Parser.Html.HtmlParser();
            var doc = parser.Parse(html);

            var htmlAssets = await GetHtmlAssets(doc);
            var nonCssAssets = htmlAssets.Where(a => a.Suffix.ToLower() != "css").ToList();
            await AddBinaryAssetsContents(assetsContents, nonCssAssets, pagePath);
            
            foreach (var asset in htmlAssets.Where(a => a.Suffix.ToLower() == "css"))
            {
                try
                {
                    var cssStringContent = await GetStringAsset(asset.Uri, mapPathResolver, webRoot, pagePath);
                    if (!string.IsNullOrEmpty(cssStringContent))
                    {
                        var cssAssets = GetCssAssets(cssStringContent);
                        await AddBinaryAssetsContents(assetsContents, cssAssets, asset.Uri);
                        foreach (var assetContent in assetsContents)
                        {
                            // TODO: use regex to avoid replace uri that is not link but text
                            cssStringContent = cssStringContent.Replace(assetContent.Uri, assetContent.NewUri + "." + assetContent.Suffix);
                        }
                        var cssassetContent = Encoding.UTF8.GetBytes(cssStringContent);
                        if (!assetsContents.Any(a => a.Uri == asset.Uri))
                        {
                            assetsContents.Add(new AssetContent
                            {
                                Uri = asset.Uri,
                                NewUri = asset.NewUri,
                                Suffix = asset.Suffix,
                                Content = cssassetContent
                            });
                        }
                    }
                }
                catch (Exception ex)
                { /* TODO: Trace somewhere */ }

            }
            // finally add index html
            foreach (var assetContent in assetsContents)
            {
                // TODO: use regex to avoid replace uri that is not link but text
                html = html.Replace(assetContent.Uri, assetContent.NewUri + "." + assetContent.Suffix);
            }
            html = "<!DOCTYPE html>" + html;
            var htmlContent = Encoding.UTF8.GetBytes(html);
            assetsContents.Add(new AssetContent
            {   
                Uri = pagePath,
                NewUri = htmlName,
                Suffix = "html",
                Content = htmlContent
            });
            return assetsContents;
        }

        public async Task AddHtmlToPackage(string html, string pagePath, string htmlName)
        {
            var assets = await GetAssetsContents(html, pagePath, htmlName);
            AssetsContents.AddRange(assets);
        }

        public static async Task<string> GetStringAsset(string path, IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            var stringContent = string.Empty;
            try
            {
                var localpath = mapPathResolver.MapPath(pagePath, path);
                if (File.Exists(localpath))
                {
                    stringContent = File.ReadAllText(localpath);
                }
            }
            catch (Exception ex)
            { /* TODO: Trace something */ }
            if (stringContent == string.Empty)
            {
                using (var webClient = new HttpClient())
                {
                    try
                    {
                        if (path.StartsWith("http") || path.StartsWith("//"))
                        {
                            stringContent = await webClient.GetStringAsync(path);
                        }
                        else
                        {
                            stringContent = await webClient.GetStringAsync(webRoot + path);
                        }
                    }
                    catch (WebException wex)
                    {
                        // TODO: log web exception
                    }
                }
            }
            
            return stringContent;
        }

        public static async Task<byte[]> GetBinaryAsset(string path, IMapPathResolver mapPathResolver, string webRoot, string pagePath)
        {
            byte[] content = null;
            try
            {
                var localpath = mapPathResolver.MapPath(pagePath, path);
                if (File.Exists(localpath))
                {
                    content = File.ReadAllBytes(localpath);
                }
            }
            catch (Exception ex)
            {
                // TODO: trace somewhere
            }
            if (content == null)
            {
                using (var webClient = new HttpClient())
                {
                    try
                    {
                        if (path.StartsWith("http") || path.StartsWith("//"))
                        {
                            content = await webClient.GetByteArrayAsync(path);
                        }
                        else
                        {
                            content = await webClient.GetByteArrayAsync(webRoot + path);
                        }
                    }
                    catch (Exception ex)
                    {
                        // TODO: trace somewhere
                    }
                }
            }
            return content;
        }
        
        public byte[] GetPackage()
        {
            var package = GetPackage(AssetsContents);
            return package;
        }

        public byte[] GetPackage(List<AssetContent> assetsContents)
        {
            using (var ms = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var assetContent in assetsContents)
                    {
                        if (assetContent.Content.Length > 0)
                        {
                            var nentry = zipArchive.CreateEntry(assetContent.NewUri + "." + assetContent.Suffix, CompressionLevel.Fastest);
                            using (var writer = new BinaryWriter(nentry.Open()))
                            {
                                writer.Write(assetContent.Content);
                            }
                        }
                    }
                }
                return ms.ToArray();
            }
        }
    }
}
