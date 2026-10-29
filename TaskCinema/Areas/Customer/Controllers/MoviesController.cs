using CinemaProject.Data;
using CinemaProject.Models;
using CinemaProject.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaProject.Areas.Customer.Controllers;

[Area("Customer")]
public sealed class MoviesController : Controller
{
    private readonly ApplicationDbContext _context;

    public MoviesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        IQueryable<Movie> query = _context.Movies
            .AsNoTracking()
            .Where(movie => movie.Status)
            .Include(movie => movie.Category)
            .Include(movie => movie.Cinema);

        if (categoryId.HasValue)
        {
            query = query.Where(movie => movie.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(movie => movie.Name.Contains(searchTerm));
        }

        var movies = await query
            .OrderByDescending(movie => movie.ReleaseDate)
            .Select(movie => new MovieCardViewModel
            {
                Id = movie.Id,
                Name = movie.Name,
                Description = movie.Description,
                Price = movie.Price,
                ReleaseDate = movie.ReleaseDate,
                MainImg = movie.MainImg,
                CategoryName = movie.Category!.Name,
                CinemaName = movie.Cinema!.Name
            })
            .ToListAsync();

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name
            })
            .ToListAsync();

        return View(new MovieListViewModel
        {
            Movies = movies,
            Categories = categories,
            CategoryId = categoryId,
            Search = search
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var movie = await _context.Movies
            .AsNoTracking()
            .Where(item => item.Status)
            .Include(item => item.Category)
            .Include(item => item.Cinema)
            .Include(item => item.Actors)
            .Include(item => item.SubImages)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (movie is null)
        {
            return NotFound();
        }

        var model = new MovieDetailsViewModel
        {
            Id = movie.Id,
            Name = movie.Name,
            Description = movie.Description,
            Price = movie.Price,
            ReleaseDate = movie.ReleaseDate,
            MainImg = movie.MainImg,
            CategoryName = movie.Category?.Name ?? string.Empty,
            CinemaName = movie.Cinema?.Name ?? string.Empty,
            CinemaAddress = movie.Cinema?.Address,
            Actors = movie.Actors.Select(actor => actor.Name).ToList(),
            Images = movie.SubImages.Select(image => image.ImageUrl).ToList()
        };

        return View(model);
    }
}
