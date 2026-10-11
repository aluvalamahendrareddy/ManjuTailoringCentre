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

        // ─── ADMIN ROOT ROUTE (/Admin) ─────────────────────────────────────────
        [AdminOnly]
        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        [AdminOnly]
        public async Task<IActionResult> Dashboard([FromServices] IWebHostEnvironment env, [FromServices] IImageStorageService storageService)
        {
            var photos = await _db.GalleryPhotos
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();

            // Auto-recovery from Cloudinary if empty on server start
            if (!photos.Any())
            {
                var cloudPhotos = await storageService.FetchAllImagesFromCloudinaryAsync();
                if (cloudPhotos.Any())
                {
                    foreach (var cp in cloudPhotos)
                    {
                        var cat = !string.IsNullOrWhiteSpace(cp.Category) ? cp.Category : "Designer Blouse";
                        if (cat == "Pattu Langa / Half Saree") cat = "Saree Kongu Muddillu";

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

            // Seed default categories if none exist yet
            if (!await _db.Categories.AnyAsync())
            {
                var defaults = new[] { "Designer Blouse", "Normal Blouse", "Crop Top", "Kids Frocks & Dresses", "Saree kongu mudulu", "Chudidhar & Salwar", "Long Gowns & Frocks", "Custom Dresses", "Alterations" };
                foreach (var d in defaults)
                {
                    _db.Categories.Add(new CategoryItem { Name = d });
                }
                await _db.SaveChangesAsync();
            }

            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategoriesList = categories.GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

            // Calculate storage used on disk in wwwroot/uploads
            long totalBytes = 0;
            var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
            if (Directory.Exists(uploadsPath))
            {
                var dirInfo = new DirectoryInfo(uploadsPath);
                totalBytes = dirInfo.GetFiles().Sum(f => f.Length);
            }

            // If using Cloudinary or disk, estimate average photo size ~1.2MB if only on cloud
            if (totalBytes == 0 && photos.Count > 0)
            {
                totalBytes = photos.Count * (long)(1.2 * 1024 * 1024);
            }

            double totalMB = Math.Round((double)totalBytes / (1024 * 1024), 2);
            ViewBag.StorageUsedMB = totalMB;
            ViewBag.StorageLimitGB = 25; // Cloudinary free plan provides 25 GB
            ViewBag.StoragePercentage = Math.Min(100, Math.Round((totalMB / (25.0 * 1024)) * 100, 2));

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
                    var (imageUrl, publicId) = await _storageService.UploadImageAsync(photo, selectedCategory);

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

        // ─── UPDATE PHOTO CATEGORY ───────────────────────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> UpdatePhotoCategory(int photoId, string newCategory)
        {
            var photo = await _db.GalleryPhotos.FindAsync(photoId);
            if (photo != null && !string.IsNullOrWhiteSpace(newCategory))
            {
                var clean = newCategory.Trim();
                photo.Category = clean;
                photo.Title = clean;
                await _db.SaveChangesAsync();

                if (!string.IsNullOrEmpty(photo.CloudinaryPublicId))
                {
                    await _storageService.UpdateImageCategoryAsync(photo.CloudinaryPublicId, clean);
                }

                TempData["Success"] = $"Photo moved to '{clean}' and synced to cloud!";
            }
            return RedirectToAction("Dashboard");
        }

        // ─── CATEGORIES MANAGEMENT PAGE ──────────────────────────────────────
        [AdminOnly]
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var rawCategories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            var categories = rawCategories.GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            // Count photos per category
            var counts = await _db.GalleryPhotos
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Category, x => x.Count);

            ViewBag.PhotoCounts = counts;
            return View(categories);
        }

        // ─── ADD NEW CATEGORY (WITH OPTIONAL PHOTOS) ───────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> AddCategory(string categoryName, List<IFormFile>? photos, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] = "Category name cannot be empty.";
                if (returnUrl == "categories") return RedirectToAction("Categories");
                return RedirectToAction("Dashboard");
            }

            var cleanName = categoryName.Trim();
            var exists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == cleanName.ToLower());
            if (!exists)
            {
                _db.Categories.Add(new CategoryItem { Name = cleanName });
                await _db.SaveChangesAsync();
            }

            int uploadedCount = 0;
            if (photos != null && photos.Count > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                foreach (var photo in photos)
                {
                    if (photo.Length == 0 || !allowedTypes.Contains(photo.ContentType.ToLower())) continue;
                    try
                    {
                        var (imageUrl, publicId) = await _storageService.UploadImageAsync(photo, cleanName);
                        _db.GalleryPhotos.Add(new GalleryPhoto
                        {
                            Title = cleanName,
                            Category = cleanName,
                            ImageUrl = imageUrl,
                            CloudinaryPublicId = publicId,
                            UploadedAt = DateTime.UtcNow
                        });
                        uploadedCount++;
                    }
                    catch { }
                }
                if (uploadedCount > 0)
                {
                    await _db.SaveChangesAsync();
                }
            }

            if (uploadedCount > 0)
            {
                TempData["Success"] = $"Category '{cleanName}' created with {uploadedCount} photo(s)!";
            }
            else
            {
                TempData["Success"] = $"Category '{cleanName}' added successfully!";
            }

            if (returnUrl == "categories") return RedirectToAction("Categories");
            return RedirectToAction("Dashboard");
        }

        // ─── RENAME CATEGORY ──────────────────────────────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> RenameCategory(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                TempData["Error"] = "Category name cannot be empty.";
                return RedirectToAction("Categories");
            }

            var cat = await _db.Categories.FindAsync(id);
            if (cat != null)
            {
                var oldName = cat.Name;
                var trimmed = newName.Trim();
                cat.Name = trimmed;

                // Also update any photos tagged with the old category name so they don't break
                var affectedPhotos = await _db.GalleryPhotos.Where(p => p.Category == oldName).ToListAsync();
                foreach (var p in affectedPhotos)
                {
                    p.Category = trimmed;
                    p.Title = trimmed;
                    if (!string.IsNullOrEmpty(p.CloudinaryPublicId))
                    {
                        await _storageService.UpdateImageCategoryAsync(p.CloudinaryPublicId, trimmed);
                    }
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = $"Category renamed to '{trimmed}'!";
            }
            return RedirectToAction("Categories");
        }

        // ─── DELETE CATEGORY ──────────────────────────────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int id, string? returnUrl)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat != null)
            {
                var catName = cat.Name;
                _db.Categories.Remove(cat);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Category '{catName}' deleted.";
            }
            if (returnUrl == "categories") return RedirectToAction("Categories");
            return RedirectToAction("Dashboard");
        }

        // ─── DELETE PHOTO ─────────────────────────────────────────────────────
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

                TempData["Success"] = $"Photo deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Delete failed: {ex.Message}";
            }

            return RedirectToAction("Dashboard");
        }

        // ─── DELETE MULTIPLE PHOTOS (BULK DELETE) ─────────────────────────────
        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> DeleteMultiple([FromForm] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                TempData["Error"] = "Please select at least one photo to delete.";
                return RedirectToAction("Dashboard");
            }

            int count = 0;
            foreach (var id in ids)
            {
                var photo = await _db.GalleryPhotos.FindAsync(id);
                if (photo != null)
                {
                    if (!string.IsNullOrEmpty(photo.CloudinaryPublicId))
                    {
                        try
                        {
                            await _storageService.DeleteImageAsync(photo.CloudinaryPublicId);
                        }
                        catch { }
                    }
                    _db.GalleryPhotos.Remove(photo);
                    count++;
                }
            }

            if (count > 0)
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = $"{count} photo(s) deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Selected photos could not be found.";
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
