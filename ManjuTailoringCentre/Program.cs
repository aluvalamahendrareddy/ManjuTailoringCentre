using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using ManjuTailoringCentre.Data;
using ManjuTailoringCentre.Models;
using ManjuTailoringCentre.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add MVC
builder.Services.AddControllersWithViews();

// SQLite database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=gallery.db"));

// Image storage service (Local disk when no keys provided, Cloudinary when keys are provided)
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();

// Cookie authentication for Admin login
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-create and seed SQLite database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // 1. Seed Categories if empty
    if (!db.Categories.Any())
    {
        var defaults = new[] { "Designer Blouse", "Normal Blouse", "Kids Frocks & Dresses", "Pattu Langa / Half Saree", "Chudidhar & Salwar", "Long Gowns & Frocks", "Custom Dresses", "Alterations" };
        foreach (var d in defaults)
        {
            db.Categories.Add(new CategoryItem { Name = d });
        }
        db.SaveChanges();
    }

    // 2. Seed Gallery Photos from embedded gallery_seed.json if empty
    if (!db.GalleryPhotos.Any())
    {
        var seedPath = Path.Combine(app.Environment.ContentRootPath, "Data", "gallery_seed.json");
        if (File.Exists(seedPath))
        {
            try
            {
                var json = File.ReadAllText(seedPath);
                var items = JsonSerializer.Deserialize<List<GallerySeedItem>>(json);
                if (items != null && items.Count > 0)
                {
                    foreach (var it in items)
                    {
                        db.GalleryPhotos.Add(new GalleryPhoto
                        {
                            Title = it.Title,
                            Category = it.Category,
                            ImageUrl = it.ImageUrl,
                            CloudinaryPublicId = it.PublicId,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                    db.SaveChanges();
                }
            }
            catch { }
        }
    }
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
