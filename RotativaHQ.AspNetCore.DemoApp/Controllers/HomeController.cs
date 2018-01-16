using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            return View();
        }

        public async Task<IActionResult> Contact()
        {
            ViewData["Message"] = "Your contact page.";
//            var t = await PdfHelper.GetPdfUrl("~/Views/Home/Contact.cshtml", this.ControllerContext);

            var t = await PdfHelper.GetPdfUrl("", this.ControllerContext);
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
