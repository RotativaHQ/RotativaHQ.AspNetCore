﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RotativaHQ.AspNetCore.DemoCore.Models;

namespace RotativaHQ.AspNetCore.DemoCore.Controllers
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

            return new ViewAsPdf();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";
            //var pdf = PdfHelper.GetPdfUrl("~/Views/Home/Contact");
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
