using CinemaProject.Data;
using CinemaProject.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaProject.Areas.Customer.Controllers;

[Area("Customer")]
public sealed class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var movies = await _context.Movies
            .AsNoTracking()
            .Where(movie => movie.Status)
            .Include(movie => movie.Category)
            .Include(movie => movie.Cinema)
            .OrderByDescending(movie => movie.ReleaseDate)
            .Take(4)
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

        return View(new CinemaHomeViewModel
        {
            Movies = movies,
            Categories = categories
        });
    }
}
