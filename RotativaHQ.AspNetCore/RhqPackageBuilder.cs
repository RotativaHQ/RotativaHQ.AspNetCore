using AngleSharp.Dom;
using AngleSharp.Extensions;
using AngleSharp.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RotativaHQ.AspNetCore
{
    public class AssetPathCollection
    {
        internal List<AssetPath> Assets { get; set; }

        public async Task AddSerializedAsset(IElement element, string uriAttribute)
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
            var localPath = await Zipper.ReturnLocalPath(src);
            if (localPath != string.Empty)
            {
                var suffix = src.Split('.').Last().ToLower();
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
                if (!this.Assets.Any(a => a.OriginalPath == localPath))
                {
                    this.Assets.Add(new AssetPath
                    {
                        OriginalPath = localPath,
                        SerializedName = newSrc,
                        AssetType = suffix
                    });
                }
            }

        }

        public async Task AddSerializedAssets(
            IEnumerable<IElement> elements,
            string uriAttribute
            )
        {
            foreach (var element in elements)
            {
                await this.AddSerializedAsset(element, uriAttribute);
            }
        }

    }

    public class AssetPath
    {
        public string OriginalPath { get; set; }
        public string SerializedName { get; set; }
        public string AssetType { get; set; }
        public byte[] Content { get; set; }
    }

    public class RhqPackageBuilder: IDisposable
    {
        IMapPathResolver mapPathResolver;
        string htmlPage;
        string webRoot;
        MemoryStream ms;
        ZipArchive zipArchive;
        private AssetPathCollection Assets { get; set; }
        private AssetPathCollection StyleAssets { get; set; }

        public RhqPackageBuilder(IMapPathResolver mapPathResolver, string webRoot)
        {
            this.mapPathResolver = mapPathResolver;
            this.webRoot = webRoot;
            ms = new MemoryStream();
            zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true);
            Assets = new AssetPathCollection();
            StyleAssets = new AssetPathCollection();
        }

        private async Task<AssetPathCollection> ExtractAssets(string source, string suffix, string pagePath, AssetPathCollection assets)
        {
            if (suffix.ToLower().StartsWith("htm"))
            {
                var parser = new AngleSharp.Parser.Html.HtmlParser();
                var doc = parser.Parse(source);
                var images = doc.Images
                    .Where(x => x.HasAttribute("src"));
                var styles = doc.GetElementsByTagName("link")
                    .Where(l => l.Attributes["rel"].Value.Trim().ToLower() == "stylesheet")
                    .Where(c => c.HasAttribute("href"));
                var scripts = doc.GetElementsByTagName("script")
                    .Where(x => x.HasAttribute("src"));
                assets.AddSerializedAssets(images, "src");
                assets.AddSerializedAssets(scripts, "src");
                assets.AddSerializedAssets(styles, "href");
                foreach (var asset in assets.Assets.Where(a => a.AssetType == "css"))
                {
                    var content = await Zipper.GetStringAsset(asset.OriginalPath, mapPathResolver, webRoot, pagePath);
                    var binaryContent = Encoding.UTF8.GetBytes(content);
                    asset.Content = binaryContent;
                    await ExtractAssets(content, "css", asset.OriginalPath, assets);
                }

                foreach (var asset in assets.Assets.Where(a => a.AssetType != "css"))
                {
                    var binaryContent = await Zipper.GetBinaryAsset(asset.OriginalPath, mapPathResolver, webRoot, pagePath);
                    asset.Content = binaryContent;
                }
                
            }
            else if (suffix.ToLower() == "css")
            {
                var urls = Zipper.ExtaxtUrlsFromStyle(source);
                foreach (var url in urls)
                {
                    var csslocalPath = await Zipper.ReturnLocalPath(url);
                    var csssuffix = csslocalPath.Split('.').Last();
                    var newUrl = Guid.NewGuid().ToString().Replace("-", "") + "." + suffix;
                    source = source.Replace(url, newUrl);
                    //assets.AddSerializedAssets()
                    //if (!doneAssets.Contains(newUrl))
                    //{
                    //    zipArchive.AddBinaryAssetToArchive(newUrl, localPath, mapPathResolver, webRoot, serialStyle.Key);
                    //    doneAssets.Add(newUrl);
                    //}
                }
            }
            return assets;
        }

        public async Task AddPage(string html, string webRoot, string pagePath)
        {
            this.htmlPage = html;
            this.webRoot = webRoot;
            //this.pagePath = pagePath;

            var parser = new AngleSharp.Parser.Html.HtmlParser();
            var doc = parser.Parse(html);
            var images = doc.Images
                .Where(x => x.HasAttribute("src"));
            var styles = doc.GetElementsByTagName("link")
                .Where(l => l.Attributes["rel"].Value.Trim().ToLower() == "stylesheet")
                .Where(c => c.HasAttribute("href"));
            var scripts = doc.GetElementsByTagName("script")
                .Where(x => x.HasAttribute("src"));
            Assets.AddSerializedAssets(images, "src");
            Assets.AddSerializedAssets(scripts, "src");
            StyleAssets.AddSerializedAssets(styles, "href");
            
            var newHtml = doc.ToHtml(new HtmlMarkupFormatter());
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
            }
            foreach (var serialStyle in this.StyleAssets.Assets)
            {
                //if (!serialStyle.Zipped)
                {
                    var style = await
                        Zipper.GetStringAsset(serialStyle.OriginalPath, mapPathResolver, webRoot, pagePath);
                    if (!string.IsNullOrEmpty(style))
                    {
                       
                        //var sentry = zipArchive.CreateEntry(serialStyle.Value, CompressionLevel.Fastest);
                        //using (StreamWriter writer = new StreamWriter(sentry.Open()))
                        //{
                        //    writer.Write(style);
                        //}
                        //doneAssets.Add(serialStyle.Value);
                    }
                }
            }
            //foreach (var serialAsset in serialAssets)
            //{
            //    if (!doneAssets.Contains(serialAsset.Value))
            //    {
            //        zipArchive.AddBinaryAssetToArchive(
            //            serialAsset.Value, serialAsset.Key, mapPathResolver, webRoot, pagePath);
            //        doneAssets.Add(serialAsset.Value);
            //    }
            //}
        }

        public byte[] GetZippedPage()
        {
            //var zippedPage= Zipper.ZipPage(this.htmlPage, this.mapPathResolver, this.webRoot, this.pagePath);

            zipArchive.Dispose();
            var zippedPage = ms.ToArray();
            ms.Dispose();
            return zippedPage;
        }

        public void Dispose()
        {
            zipArchive.Dispose();
            ms.Dispose();
        }
    }
}
