#if NETFULL
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace RotativaHQ.DotNetStandard
{
    public class ViewAsPdf : ActionResult
    {
        private const string ContentType = "application/pdf";

        /// <summary>
        /// This will be send to the browser as a name of the generated PDF file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Sets the page margins.
        /// </summary>
        public Margins PageMargins { get; set; }

        /// <summary>
        /// Sets the page size.
        /// </summary>
        [OptionFlag("-s")]
        public Size? PageSize { get; set; }

        /// <summary>
        /// Sets the page width in mm.
        /// </summary>
        /// <remarks>Has priority over <see cref="PageSize"/> but <see cref="PageHeight"/> has to be also specified.</remarks>
        [OptionFlag("--page-width")]
        public double? PageWidth { get; set; }

        /// <summary>
        /// Sets the page height in mm.
        /// </summary>
        /// <remarks>Has priority over <see cref="PageSize"/> but <see cref="PageWidth"/> has to be also specified.</remarks>
        [OptionFlag("--page-height")]
        public double? PageHeight { get; set; }

        /// <summary>
        /// Sets the page orientation.
        /// </summary>
        [OptionFlag("-O")]
        public Orientation? PageOrientation { get; set; }

        /// <summary>
        /// Indicates whether the page can run JavaScript.
        /// </summary>
        [OptionFlag("-n")]
        public bool IsJavaScriptDisabled { get; set; }

        /// <summary>
        /// Indicates whether the PDF should be generated in lower quality.
        /// </summary>
        [OptionFlag("-l")]
        public bool IsLowQuality { get; set; }

        /// <summary>
        /// Indicates whether the page background should be disabled.
        /// </summary>
        [OptionFlag("--no-background")]
        public bool IsBackgroundDisabled { get; set; }

        /// <summary>
        /// Minimum font size.
        /// </summary>
        [OptionFlag("--minimum-font-size")]
        public int? MinimumFontSize { get; set; }

        /// <summary>
        /// Number of copies to print into the PDF file.
        /// </summary>
        [OptionFlag("--copies")]
        public int? Copies { get; set; }

        /// <summary>
        /// Indicates whether the PDF should be generated in grayscale.
        /// </summary>
        [OptionFlag("-g")]
        public bool IsGrayScale { get; set; }

        /// <summary>
        /// Use this if you need another switches that are not currently supported by Rotativa.
        /// </summary>
        [OptionFlag("")]
        public string CustomSwitches { get; set; }

        private string _viewName;

        public string ViewName
        {
            get { return _viewName ?? string.Empty; }
            set { _viewName = value; }
        }

        private string _masterName;

        public string MasterName
        {
            get { return _masterName ?? string.Empty; }
            set { _masterName = value; }
        }

        public object Model { get; set; }

        public virtual string ExtraSwitches { get; set; }
        
        public string HeaderView { get; set; }
        public string FooterView { get; set; }
        
        public bool ShowInline { get; set; }

        public ViewAsPdf()
        {
            MasterName = string.Empty;
            ViewName = string.Empty;
            Model = null;
        }

        public ViewAsPdf(string viewName)
            : this()
        {
            ViewName = viewName;
            PageMargins = new Margins();
        }

        public ViewAsPdf(object model)
            : this()
        {
            Model = model;
        }

        public ViewAsPdf(string viewName, object model)
            : this()
        {
            ViewName = viewName;
            Model = model;
        }

        public ViewAsPdf(string viewName, string masterName, object model)
            : this(viewName, model)
        {
            MasterName = masterName;
        }


        /// <summary>
        /// Returns properties with OptionFlag attribute as one line that can be passed to wkhtmltopdf binary.
        /// </summary>
        /// <returns>Command line parameter that can be directly passed to wkhtmltopdf binary.</returns>
        protected string GetConvertOptions()
        {
            var result = new StringBuilder();

            if (PageMargins != null)
                result.Append(PageMargins.ToString());

            var fields = GetType().GetProperties();
            foreach (var fi in fields)
            {
                var of = fi.GetCustomAttributes(typeof(OptionFlag), true).FirstOrDefault() as OptionFlag;
                if (of == null)
                    continue;

                object value = fi.GetValue(this, null);
                if (value == null)
                    continue;

                if (fi.PropertyType == typeof(Dictionary<string, string>))
                {
                    var dictionary = (Dictionary<string, string>)value;
                    foreach (var d in dictionary)
                    {
                        result.AppendFormat(" {0} {1} {2}", of.Name, d.Key, d.Value);
                    }
                }
                else if (fi.PropertyType == typeof(bool))
                {
                    if ((bool)value)
                        result.AppendFormat(CultureInfo.InvariantCulture, " {0}", of.Name);
                }
                else
                {
                    result.AppendFormat(CultureInfo.InvariantCulture, " {0} {1}", of.Name, value);
                }
            }

            var switches = result.ToString().Trim();
            if (!string.IsNullOrEmpty(ExtraSwitches))
            {
                switches += ExtraSwitches;
            }
            return switches;
        }

        private string GetWkParams(ControllerContext context)
        {
            var switches = string.Empty;

            switches += " " + GetConvertOptions();

            if (!string.IsNullOrEmpty(ExtraSwitches))
            {
                switches += ExtraSwitches;
            }

            return switches;
        }

        
        public string BuildPdf(ControllerContext context)
        {

            var fileContent = CallTheDriver(context);

            //if (string.IsNullOrEmpty(SaveOnServerPath) == false)
            //{
            //    File.WriteAllBytes(SaveOnServerPath, fileContent);
            //}

            return fileContent;
        }

        
        protected ViewEngineResult GetView(ControllerContext context, string viewName, string masterName)
        {
            return ViewEngines.Engines.FindView(context, viewName, masterName);
        }
        
        public string CallTheDriver(ControllerContext context)
        {
            context.Controller.ViewData.Model = Model;

            // use action name if the view name was not provided
            if (string.IsNullOrEmpty(ViewName))
                ViewName = context.RouteData.GetRequiredString("action");
            StringBuilder html = new StringBuilder();
            StringBuilder header = new StringBuilder();
            StringBuilder footer = new StringBuilder();

            using (var sw = new StringWriter())
            {
                ViewEngineResult viewResult = GetView(context, ViewName, MasterName);

                // view not found, throw an exception with searched locations
                if (viewResult.View == null)
                {
                    var locations = new StringBuilder();
                    locations.AppendLine();

                    foreach (string location in viewResult.SearchedLocations)
                    {
                        locations.AppendLine(location);
                    }

                    throw new InvalidOperationException(string.Format("The view '{0}' or its master was not found, searched locations: {1}", ViewName, locations));
                }

                var viewContext = new ViewContext(context, viewResult.View, context.Controller.ViewData, context.Controller.TempData, sw);
                viewResult.View.Render(viewContext, sw);

                html = sw.GetStringBuilder();
            }

            if (!string.IsNullOrEmpty(HeaderView))
            {
                using (var hw = new StringWriter())
                {
                    ViewEngineResult headerViewResult = GetView(context, HeaderView, MasterName);
                    if (headerViewResult != null)
                    {
                        var viewContext = new ViewContext(context, headerViewResult.View, context.Controller.ViewData, context.Controller.TempData, hw);
                        headerViewResult.View.Render(viewContext, hw);

                		header = hw.GetStringBuilder();
                        ExtraSwitches += " --header-html header.html";
                    }
                }
            }

            if (!string.IsNullOrEmpty(FooterView))
            {
                using (var hw = new StringWriter())
                {
                    ViewEngineResult footerViewResult = GetView(context, FooterView, MasterName);
                    if (footerViewResult != null)
                    {
                        var viewContext = new ViewContext(context, footerViewResult.View, context.Controller.ViewData, context.Controller.TempData, hw);
                        footerViewResult.View.Render(viewContext, hw);

                		footer = hw.GetStringBuilder();
                        ExtraSwitches += " --footer-html footer.html";
                    }
                }
            }

            // replace href and src attributes with full URLs
            if (!ConfigurationManager.AppSettings.AllKeys.Contains("RotativaKey"))
            {
                throw new ConfigurationException("RotativaKey AppSetting not found");
            }

            var apiKey = ConfigurationManager.AppSettings["RotativaKey"].ToString();
            var client = new RotativaHqClient(apiKey);
            var contentDisposition = this.ShowInline ? "inline" : "";
            var fileUrl = client.GetPdfUrl(GetConvertOptions(), html.ToString(), this.FileName, header.ToString(), footer.ToString(), contentDisposition);
            return fileUrl;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var fileUrl = BuildPdf(context);
            
            var response = PrepareResponse(context.HttpContext.Response);

            //response.OutputStream.Write(fileContent, 0, fileContent.Length);
            response.Redirect(fileUrl, true);
        }

        private static string SanitizeFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars()));
            string invalidCharsPattern = string.Format(@"[{0}]+", invalidChars);

            string result = Regex.Replace(name, invalidCharsPattern, "_");
            return result;
        }

        protected HttpResponseBase PrepareResponse(HttpResponseBase response)
        {
            response.ContentType = ContentType;

            if (!String.IsNullOrEmpty(FileName))
                response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}\"", SanitizeFileName(FileName)));

            response.AddHeader("Content-Type", ContentType);

            return response;
        }

        protected HttpResponse PrepareResponse(HttpResponse response)
        {
            response.ContentType = ContentType;

            if (!String.IsNullOrEmpty(FileName))
                response.Headers.Add("Content-Disposition", string.Format("attachment; filename=\"{0}\"", SanitizeFileName(FileName)));

            response.ContentType = ContentType;

            return response;
        }

    }
}
#else

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RotativaHQ.DotNetStandard
{
    public class ViewAsPdf : ActionResult
    {
        private const string ContentType = "application/pdf";

        /// <summary>
        /// This will be send to the browser as a name of the generated PDF file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Sets the page margins.
        /// </summary>
        public Margins PageMargins { get; set; }

        /// <summary>
        /// Sets the page size.
        /// </summary>
        [OptionFlag("-s")]
        public Size? PageSize { get; set; }

        /// <summary>
        /// Sets the page width in mm.
        /// </summary>
        /// <remarks>Has priority over <see cref="PageSize"/> but <see cref="PageHeight"/> has to be also specified.</remarks>
        [OptionFlag("--page-width")]
        public double? PageWidth { get; set; }

        /// <summary>
        /// Sets the page height in mm.
        /// </summary>
        /// <remarks>Has priority over <see cref="PageSize"/> but <see cref="PageWidth"/> has to be also specified.</remarks>
        [OptionFlag("--page-height")]
        public double? PageHeight { get; set; }

        /// <summary>
        /// Sets the page orientation.
        /// </summary>
        [OptionFlag("-O")]
        public Orientation? PageOrientation { get; set; }

        /// <summary>
        /// Indicates whether the page can run JavaScript.
        /// </summary>
        [OptionFlag("-n")]
        public bool IsJavaScriptDisabled { get; set; }

        /// <summary>
        /// Indicates whether the PDF should be generated in lower quality.
        /// </summary>
        [OptionFlag("-l")]
        public bool IsLowQuality { get; set; }

        /// <summary>
        /// Indicates whether the page background should be disabled.
        /// </summary>
        [OptionFlag("--no-background")]
        public bool IsBackgroundDisabled { get; set; }

        /// <summary>
        /// Minimum font size.
        /// </summary>
        [OptionFlag("--minimum-font-size")]
        public int? MinimumFontSize { get; set; }

        /// <summary>
        /// Number of copies to print into the PDF file.
        /// </summary>
        [OptionFlag("--copies")]
        public int? Copies { get; set; }

        /// <summary>
        /// Indicates whether the PDF should be generated in grayscale.
        /// </summary>
        [OptionFlag("-g")]
        public bool IsGrayScale { get; set; }

        /// <summary>
        /// Use this if you need another switches that are not currently supported by Rotativa.
        /// </summary>
        [OptionFlag("")]
        public string CustomSwitches { get; set; }

        private string _viewName;

        public string ViewName
        {
            get { return _viewName ?? string.Empty; }
            set { _viewName = value; }
        }

        private string _masterName;

        public string MasterName
        {
            get { return _masterName ?? string.Empty; }
            set { _masterName = value; }
        }

        public object Model { get; set; }

        protected virtual string ExtraSwitches { get; set; }

        public ViewAsPdf()
        {
            MasterName = string.Empty;
            ViewName = string.Empty;
            Model = null;
        }

        public ViewAsPdf(string viewName)
            : this()
        {
            ViewName = viewName;
            PageMargins = new Margins();
        }

        public ViewAsPdf(object model)
            : this()
        {
            Model = model;
        }

        public ViewAsPdf(string viewName, object model)
            : this()
        {
            ViewName = viewName;
            Model = model;
        }

        public ViewAsPdf(string viewName, string masterName, object model)
            : this(viewName, model)
        {
            MasterName = masterName;
        }


        /// <summary>
        /// Returns properties with OptionFlag attribute as one line that can be passed to wkhtmltopdf binary.
        /// </summary>
        /// <returns>Command line parameter that can be directly passed to wkhtmltopdf binary.</returns>
        protected string GetConvertOptions()
        {
            var result = new StringBuilder();

            if (PageMargins != null)
                result.Append(PageMargins.ToString());

            var fields = GetType().GetProperties();
            foreach (var fi in fields)
            {
                var of = fi.GetCustomAttributes(typeof(OptionFlag), true).FirstOrDefault() as OptionFlag;
                if (of == null)
                    continue;

                object value = fi.GetValue(this, null);
                if (value == null)
                    continue;

                if (fi.PropertyType == typeof(Dictionary<string, string>))
                {
                    var dictionary = (Dictionary<string, string>)value;
                    foreach (var d in dictionary)
                    {
                        result.AppendFormat(" {0} {1} {2}", of.Name, d.Key, d.Value);
                    }
                }
                else if (fi.PropertyType == typeof(bool))
                {
                    if ((bool)value)
                        result.AppendFormat(CultureInfo.InvariantCulture, " {0}", of.Name);
                }
                else
                {
                    result.AppendFormat(CultureInfo.InvariantCulture, " {0} {1}", of.Name, value);
                }
            }

            var switches = result.ToString().Trim();
            if (!string.IsNullOrEmpty(ExtraSwitches))
            {
                switches += ExtraSwitches;
            }
            return switches;
        }

        private string GetWkParams(ControllerContext context)
        {
            var switches = string.Empty;

            switches += " " + GetConvertOptions();

            if (!string.IsNullOrEmpty(ExtraSwitches))
            {
                switches += ExtraSwitches;
            }

            return switches;
        }


        protected virtual ViewEngineResult GetView(ActionContext context, string viewName, string masterName)
        {
            var engine = context.HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine)) as ICompositeViewEngine;
            return engine.FindView(context, ViewName, true);
        }

        protected async Task<string> CallTheDriver<TModel>(ActionContext context, TModel model)
        {
            //context.Controller.ViewData.Model = Model;

            // use action name if the view name was not provided
            if (string.IsNullOrEmpty(ViewName))
                ViewName = ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor).ActionName;

            using (var sw = new StringWriter())
            {
                ViewEngineResult viewResult = GetView(context, ViewName, MasterName);

                // view not found, throw an exception with searched locations
                if (viewResult.View == null)
                {
                    var locations = new StringBuilder();
                    locations.AppendLine();

                    foreach (string location in viewResult.SearchedLocations)
                    {
                        locations.AppendLine(location);
                    }

                    throw new InvalidOperationException(string.Format("The view '{0}' or its master was not found, searched locations: {1}", ViewName, locations));
                }

                ITempDataProvider tempDataProvider = context.HttpContext.RequestServices.GetService(typeof(ITempDataProvider)) as ITempDataProvider;

                using (var output = new StringWriter())
                {
                    var view = viewResult.View;
                    var viewContext = new ViewContext(
                        context,
                        viewResult.View,
                        new ViewDataDictionary<TModel>(
                            metadataProvider: new EmptyModelMetadataProvider(),
                            modelState: new ModelStateDictionary())
                        {
                            Model = model
                        },
                        new TempDataDictionary(context.HttpContext, tempDataProvider),
                        output,
                        new HtmlHelperOptions());

                    await view.RenderAsync(viewContext);

                    var html = output.ToString();


                    //var viewContext = new ViewContext(context, viewResult.View, context.Controller.ViewData, context.Controller.TempData, sw);
                    //viewResult.View.Render(viewContext, sw);

                    //StringBuilder html = sw.GetStringBuilder();

                    // replace href and src attributes with full URLs
                    var apiKey = RotativaHqConfiguration.RotativaHqApiKey; //"b4301d1a95c2492b9b3fbacf380fbf55"; // ConfigurationManager.AppSettings["RotativaKey"].ToString();
                    if (apiKey == null)
                    {
                        throw new InvalidOperationException("Apikey not defined.");
                    }
                    var client = new RotativaHqClient(apiKey);
                    var fileUrl = client.GetPdfUrl(context.HttpContext, GetConvertOptions(), html.ToString(), this.FileName);
                    return fileUrl;
                }
            }
        }

        public async Task<string> BuildPdf(ActionContext context)
        {

            var fileUrl = await CallTheDriver(context, Model);

            //if (string.IsNullOrEmpty(SaveOnServerPath) == false)
            //{
            //    File.WriteAllBytes(SaveOnServerPath, fileContent);
            //}

            return fileUrl;
        }

        private static string SanitizeFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars()));
            string invalidCharsPattern = string.Format(@"[{0}]+", invalidChars);

            string result = Regex.Replace(name, invalidCharsPattern, "_");
            return result;
        }

        protected HttpResponse PrepareResponse(HttpResponse response)
        {
            response.ContentType = ContentType;

            if (!String.IsNullOrEmpty(FileName))
                response.Headers.Add("Content-Disposition", string.Format("attachment; filename=\"{0}\"", SanitizeFileName(FileName)));

            response.ContentType = ContentType;

            return response;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var fileUrl = await BuildPdf(context);

            var response = PrepareResponse(context.HttpContext.Response);

            //response.OutputStream.Write(fileContent, 0, fileContent.Length);
            response.Redirect(fileUrl, true);
        }
    }
}

#endif 
