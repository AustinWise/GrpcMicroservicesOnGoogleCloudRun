using FrontendWebApp.Models;
using GrpcContracts.Greeters;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FrontendWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Greeter.GreeterClient _greeter;

        public HomeController(ILogger<HomeController> logger, Greeter.GreeterClient greeter)
        {
            _logger = logger;
            _greeter = greeter;
        }

        public async Task<IActionResult> Index()
        {
            var res = await _greeter.SayHelloAsync(new HelloRequest()
            {
                Name = "frontend"
            });

            // Cast to object so that it is used as the Model
            return View((object)res.Message);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}