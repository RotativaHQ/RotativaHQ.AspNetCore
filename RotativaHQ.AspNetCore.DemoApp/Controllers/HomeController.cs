using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RotativaHQ.AspNetCore.DemoApp.Models;

namespace RotativaHQ.AspNetCore.DemoApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";
           

            return new ViewAsPdf()
            {
                HeaderView = "Header",
                FooterView = "Footer"
            };
        }

        public async Task<IActionResult> Contact()
        {
            ViewData["Message"] = "Your contact page.";

            //var pdf = await PdfHelper.GetPdfUrl(this.ControllerContext);

            //return Redirect(pdf);
            var pdf = await PdfHelper.GetPdf(this.ControllerContext);

            return File(pdf, "application/pdf", "test.pdf");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
