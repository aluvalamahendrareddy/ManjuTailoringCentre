using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ManjuTailoringCentre.Models;
using ManjuTailoringCentre.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ManjuTailoringCentre.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;

        public HomeController(ILogger<HomeController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            var services = GetServices().Take(4).ToList();
            var reviews = GetReviews();
            ViewBag.Services = services;
            ViewBag.Reviews = reviews;
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Services()
        {
            var services = GetServices();
            return View(services);
        }

        public async Task<IActionResult> Gallery()
        {
            var photos = await _db.GalleryPhotos
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();
            return View(photos);
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Contact(ContactFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                TempData["SuccessMessage"] = "Message received! We will get back to you soon.";
                return RedirectToAction("Contact");
            }
            return View(model);
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

        private List<ServiceViewModel> GetServices()
        {
            return new List<ServiceViewModel>
            {
                new ServiceViewModel { Title = "Designer Blouses", Description = "Custom patterns, bridal neck designs, piping & perfect fitting.", Icon = "bi-stars", Price = "₹600+", ImageUrl = "/images/blouse.webp" },
                new ServiceViewModel { Title = "Kids Frocks & Dresses", Description = "Cute, stylish & comfortable custom outfits for baby girls & children.", Icon = "bi-person-heart", Price = "₹450+", ImageUrl = "/images/kids-dress.webp" },
                new ServiceViewModel { Title = "Pattu Langa & Half Sarees", Description = "Traditional silk lehengas, pattu langas & festive wear.", Icon = "bi-gem", Price = "₹700+", ImageUrl = "/images/pattu-langa.webp" },
                new ServiceViewModel { Title = "Chudidhars & Salwars", Description = "Daily wear & party wear suits with trendy cuts & perfect fit.", Icon = "bi-palette", Price = "₹400+", ImageUrl = "/images/chudidhar.webp" },
                new ServiceViewModel { Title = "Trendy Long Gowns", Description = "Modern festive gowns, umbrella frocks & custom designs.", Icon = "bi-flower1", Price = "₹800+", ImageUrl = "/images/gown.webp" },
                new ServiceViewModel { Title = "Fast Alterations", Description = "Fitting, resizing & length adjustment done with care.", Icon = "bi-scissors", Price = "₹50+", ImageUrl = "/images/alterations.webp" }
            };
        }

        private List<ReviewViewModel> GetReviews()
        {
            return new List<ReviewViewModel>
            {
                new ReviewViewModel { Author = "Anjali", Comment = "Excellent work, Affordable prices, Trending designs, Trust worthy, Perfect in no less time!", Rating = 5 },
                new ReviewViewModel { Author = "Prasad", Comment = "Customer friendly, Perfect fit, Turns you elegant", Rating = 5 }
            };
        }
    }
}
