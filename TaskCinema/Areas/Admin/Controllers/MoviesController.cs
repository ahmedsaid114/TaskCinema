using CinemaProject.Data;
using CinemaProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace CinemaProject.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public MoviesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }


        public async Task<IActionResult> Index(
            string? searchString,
            int? categoryId,
            int? cinemaId,
            int page = 1)
        {
            const int pageSize = 5;

            if (page < 1)
                page = 1;

            // Load dropdowns for filtering
            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name",
                categoryId);

            ViewBag.Cinemas = new SelectList(
                await _context.Cinemas
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name",
                cinemaId);

            // Build query
            var query = _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Cinema)
                .AsNoTracking()
                .AsQueryable();

            // Filter by movie name
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                query = query.Where(m =>
                    m.Name.Contains(searchString));
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                query = query.Where(m =>
                    m.CategoryId == categoryId.Value);
            }

            // Filter by cinema
            if (cinemaId.HasValue)
            {
                query = query.Where(m =>
                    m.CinemaId == cinemaId.Value);
            }

            // Count after filtering
            int totalMovies = await query.CountAsync();

            int totalPages = (int)Math.Ceiling(
                totalMovies / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            // Get current page
            var movies = await query
                .OrderBy(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Send data to View
            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.CinemaId = cinemaId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalMovies = totalMovies;

            return View(movies);
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Movie movie,
            IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                ModelState.AddModelError(
                    "MainImg",
                    "Please select a movie poster.");
            }
            else if (!IsValidImage(imageFile))
            {
                ModelState.AddModelError(
                    "MainImg",
                    "Please upload a valid JPG, PNG, or WEBP image.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns(movie.CategoryId, movie.CinemaId);
                return View(movie);
            }

            var fileName = await SaveImage(imageFile!);
            movie.MainImg = fileName;

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
                return NotFound();

            await LoadDropdowns(movie.CategoryId, movie.CinemaId);
            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Movie movie,
            IFormFile? imageFile)
        {
            if (id != movie.Id)
                return BadRequest();

            var existingMovie = await _context.Movies
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existingMovie == null)
                return NotFound();

            if (imageFile != null && imageFile.Length > 0
                && !IsValidImage(imageFile))
            {
                ModelState.AddModelError(
                    "MainImg",
                    "Please upload a valid JPG, PNG, or WEBP image.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns(movie.CategoryId, movie.CinemaId);
                return View(movie);
            }

            existingMovie.Name = movie.Name;
            existingMovie.Description = movie.Description;
            existingMovie.Price = movie.Price;
            existingMovie.Status = movie.Status;
            existingMovie.ReleaseDate = movie.ReleaseDate;
            existingMovie.CategoryId = movie.CategoryId;
            existingMovie.CinemaId = movie.CinemaId;

            if (imageFile != null && imageFile.Length > 0)
            {
                var oldImage = existingMovie.MainImg;
                existingMovie.MainImg = await SaveImage(imageFile);

                DeleteImage(oldImage);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
            {
                return NotFound();
            }

            var image = movie.MainImg;

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            DeleteImage(image);

            return RedirectToAction(nameof(Index));
        }


        private async Task LoadDropdowns(
            int? categoryId = null,
            int? cinemaId = null)
        {
            ViewBag.Categories = new SelectList(
                await _context.Categories.AsNoTracking().ToListAsync(),
                "Id", "Name", categoryId);

            ViewBag.Cinemas = new SelectList(
                await _context.Cinemas.AsNoTracking().ToListAsync(),
                "Id", "Name", cinemaId);
        }

        private static bool IsValidImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return file.Length <= 5 * 1024 * 1024
                && allowedExtensions.Contains(extension);
        }

        private async Task<string> SaveImage(IFormFile file)
        {
            var folder = Path.Combine(
                _environment.WebRootPath, "images", "movies");

            Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var path = Path.Combine(folder, fileName);

            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/images/movies/{fileName}";
        }

        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            var relativePath = imagePath.TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(
                _environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
    }
}
