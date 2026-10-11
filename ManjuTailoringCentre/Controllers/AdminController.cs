using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using ManjuTailoringCentre.Data;
using ManjuTailoringCentre.Models;
using ManjuTailoringCentre.Services;
using Microsoft.EntityFrameworkCore;

namespace ManjuTailoringCentre.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IImageStorageService _storageService;
        private readonly IConfiguration _config;

        public AdminController(AppDbContext db, IImageStorageService storageService, IConfiguration config)
        {
            _db = db;
            _storageService = storageService;
            _config = config;
        }

        // ─── LOGIN ────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var adminUser = _config["Admin:Username"];
            var adminPass = _config["Admin:Password"];

            if (username == adminUser && password == adminPass)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "Admin")
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTime.UtcNow.AddDays(7) }
                );

                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [AdminOnly]
        public async Task<IActionResult> Dashboard([FromServices] IWebHostEnvironment env)
        {
            var photos = await _db.GalleryPhotos
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();

            // Calculate storage used on disk in wwwroot/uploads
            long totalBytes = 0;
            var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
            if (Directory.Exists(uploadsPath))
            {
                var dirInfo = new DirectoryInfo(uploadsPath);
                totalBytes = dirInfo.GetFiles().Sum(f => f.Length);
            }

            // Convert to human readable MB or KB
            double totalMB = Math.Round((double)totalBytes / (1024 * 1024), 2);
            ViewBag.StorageUsedMB = totalMB;
            ViewBag.StorageLimitGB = 5; // 5 GB limit target
            ViewBag.StoragePercentage = Math.Min(100, Math.Round((totalMB / (5 * 1024)) * 100, 2));

            return View(photos);
        }

        // ─── UPLOAD (Multiple photos supported) ──────────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> Upload(string? category, List<IFormFile> photos)
        {
            if (photos == null || photos.Count == 0)
            {
                TempData["Error"] = "Please select at least one photo to upload.";
                return RedirectToAction("Dashboard");
            }

            if (photos.Count > 20)
            {
                TempData["Error"] = "You can upload a maximum of 20 photos at a time.";
                return RedirectToAction("Dashboard");
            }

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            var selectedCategory = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            int successCount = 0;
            var errors = new List<string>();

            foreach (var photo in photos)
            {
                if (photo.Length == 0) continue;

                if (!allowedTypes.Contains(photo.ContentType.ToLower()))
                {
                    errors.Add($"{photo.FileName}: Invalid format (only JPG, PNG, WebP allowed).");
                    continue;
                }

                if (photo.Length > 10 * 1024 * 1024)
                {
                    errors.Add($"{photo.FileName}: File size exceeds 10MB limit.");
                    continue;
                }

                try
                {
                    var (imageUrl, publicId) = await _storageService.UploadImageAsync(photo);

                    var galleryPhoto = new GalleryPhoto
                    {
                        Title = selectedCategory,
                        Category = selectedCategory,
                        ImageUrl = imageUrl,
                        CloudinaryPublicId = publicId,
                        UploadedAt = DateTime.UtcNow
                    };

                    _db.GalleryPhotos.Add(galleryPhoto);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{photo.FileName}: {ex.Message}");
                }
            }

            if (successCount > 0)
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Successfully uploaded {successCount} photo{(successCount > 1 ? "s" : "")}!";
            }

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join("<br/>", errors);
            }

            return RedirectToAction("Dashboard");
        }

        // ─── DELETE ───────────────────────────────────────────────────────────

        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await _db.GalleryPhotos.FindAsync(id);
            if (photo == null)
            {
                TempData["Error"] = "Photo not found.";
                return RedirectToAction("Dashboard");
            }

            try
            {
                // Delete photo using storage service
                if (!string.IsNullOrEmpty(photo.CloudinaryPublicId))
                    await _storageService.DeleteImageAsync(photo.CloudinaryPublicId);

                // Then remove from database
                _db.GalleryPhotos.Remove(photo);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"'{photo.Title}' deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Delete failed: {ex.Message}";
            }

            return RedirectToAction("Dashboard");
        }
    }

    // ─── AUTHORIZATION FILTER ─────────────────────────────────────────────────

    /// <summary>
    /// Redirects unauthenticated users to Admin/Login instead of throwing 401.
    /// </summary>
    public class AdminOnlyAttribute : Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute
    {
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToActionResult("Login", "Admin", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
