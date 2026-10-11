using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ManjuTailoringCentre.Models;
using ManjuTailoringCentre.Data;
using ManjuTailoringCentre.Services;
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

        public async Task<IActionResult> Gallery([FromServices] IImageStorageService storageService)
        {
            var photos = await _db.GalleryPhotos
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();

            // Auto-recovery: If local database was restarted/cleared, recover uploaded photos from Cloudinary!
            if (!photos.Any())
            {
                var cloudPhotos = await storageService.FetchAllImagesFromCloudinaryAsync();
                if (cloudPhotos.Any())
                {
                    foreach (var cp in cloudPhotos)
                    {
                        var cat = !string.IsNullOrWhiteSpace(cp.Category) ? cp.Category : "Designer Blouse";
                        if (cat.Equals("Pattu Langa / Half Saree", StringComparison.OrdinalIgnoreCase) || 
                            cat.Equals("Saree Kongu Muddillu", StringComparison.OrdinalIgnoreCase))
                        {
                            cat = "Saree kongu mudulu";
                        }

                        _db.GalleryPhotos.Add(new GalleryPhoto
                        {
                            Title = cat,
                            Category = cat,
                            ImageUrl = cp.ImageUrl,
                            CloudinaryPublicId = cp.PublicId,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                    await _db.SaveChangesAsync();
                    photos = await _db.GalleryPhotos.OrderByDescending(p => p.UploadedAt).ToListAsync();
                }
            }

            return View(photos);
        }

        // ─── DEDICATED DESIGN DETAIL PAGE (Full Open Graph image preview on WhatsApp) ───
        public async Task<IActionResult> Design(string? img, string? cat, int? id)
        {
            GalleryPhoto? photo = null;
            if (id.HasValue)
            {
                photo = await _db.GalleryPhotos.FindAsync(id.Value);
            }
            if (photo == null && !string.IsNullOrEmpty(img))
            {
                photo = await _db.GalleryPhotos.FirstOrDefaultAsync(p => p.ImageUrl == img);
            }

            var model = new DesignPreviewViewModel
            {
                ImageUrl = photo?.ImageUrl ?? img ?? "/images/og-preview.jpg",
                Category = photo?.Category ?? cat ?? "Designer Wear",
                Title = photo?.Title ?? cat ?? "Custom Stitched Design"
            };

            return View(model);
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
        public IActionResult Error(int? statusCode = null)
        {
            var code = statusCode ?? HttpContext.Response.StatusCode;
            if (code == 200) code = 404; // Default fallback if reached via 404 re-execute

            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = code,
                Message = code == 404
                    ? "Oops! The page, design, or link you are looking for doesn't exist or was moved."
                    : "An unexpected error occurred while processing your request. Please try again or return home."
            };

            return View(model);
        }

        private List<ServiceViewModel> GetServices()
        {
            return new List<ServiceViewModel>
            {
                new ServiceViewModel { Title = "Designer Blouses", Description = "Custom patterns, bridal neck designs, piping & perfect fitting.", Icon = "bi-stars", Price = "₹600+", ImageUrl = "/images/blouse.webp" },
                new ServiceViewModel { Title = "Kids Frocks & Dresses", Description = "Cute, stylish & comfortable custom outfits for baby girls & children.", Icon = "bi-person-heart", Price = "₹450+", ImageUrl = "/images/kids-dress.webp" },
                new ServiceViewModel { Title = "Saree Kongu Muddillu", Description = "Traditional silk lehengas, pattu langas & festive half sarees.", Icon = "bi-suit-heart", Price = "₹700+", ImageUrl = "/images/pattu-langa.webp" },
                new ServiceViewModel { Title = "Chudidhars & Salwars", Description = "Daily wear & party wear suits with trendy cuts & perfect fit.", Icon = "bi-person-standing-dress", Price = "₹400+", ImageUrl = "/images/chudidhar.webp" },
                new ServiceViewModel { Title = "Trendy Long Gowns", Description = "Modern festive gowns, umbrella frocks & custom designs.", Icon = "bi-flower1", Price = "₹800+", ImageUrl = "/images/gown.webp" },
                new ServiceViewModel { Title = "Fast Alterations", Description = "Fitting, resizing & length adjustment done with care.", Icon = "bi-scissors", Price = "₹50+", ImageUrl = "/images/alterations.webp" }
            };
        }

        private List<ReviewViewModel> GetReviews()
        {
            return new List<ReviewViewModel>
            {
                new ReviewViewModel { Author = "Anjali Reddy", Comment = "Excellent work and affordable prices! Blouse stitching was accurate and delivered right on time for the wedding.", Rating = 5 },
                new ReviewViewModel { Author = "Swapna Gadwal", Comment = "Very friendly and understood my exact pattern requirement. The pattu langa fit for my daughter was super cute!", Rating = 5 },
                new ReviewViewModel { Author = "Madhavi Latha", Comment = "No multiple alterations needed. Shankaramma garu cuts and stitches with 100% precision. Truly trustworthy!", Rating = 5 }
            };
        }
    }
}
