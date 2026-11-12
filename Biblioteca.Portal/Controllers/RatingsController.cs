using BibliotecaGrpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Portal.Controllers;

public class RatingsController(
    ReaderRatings.ReaderRatingsClient client) : Controller
{
     // Afiseaza evaluarile unui cititor
    [HttpGet]
    public async Task<IActionResult> Index(int readerId = 5)
    {
        ViewBag.ReaderId = readerId;
        var ratings = new List<RatingItem>();

        try
        {
            using var call = client.ListRatings(
                new ReaderRequest { ReaderId = readerId });

            while (await call.ResponseStream.MoveNext(
                       HttpContext.RequestAborted))
            {
                ratings.Add(call.ResponseStream.Current);
            }
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
        }

        return View(ratings);
    }

    // Adauga o evaluare
    [HttpGet]
    public IActionResult Create(int readerId = 5)
    {
        return View(new NewRating { ReaderId = readerId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewRating model)
    {
        try
        {
            await client.AddRatingAsync(model);
            return RedirectToAction(nameof(Index),
                new { readerId = model.ReaderId });
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
            return View(model);
        }
    }

    // Modifica nota
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var rating = await client.GetRatingAsync(
                new RatingId { Id = id });

            return View(rating);
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
            return View(new RatingItem { Id = id });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, int stars)
    {
        try
        {
            await client.UpdateRatingAsync(
                new UpdateRatingRequest { Id = id, Stars = stars });

            return RedirectToAction(nameof(Index));
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
            return View(new RatingItem { Id = id, Stars = stars });
        }
    }

    // Sterge o evaluare
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var rating = await client.GetRatingAsync(
                new RatingId { Id = id });

            return View(rating);
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
            return View(new RatingItem { Id = id });
        }
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await client.DeleteRatingAsync(new RatingId { Id = id });
            return RedirectToAction(nameof(Index));
        }
        catch (RpcException ex)
        {
            ViewBag.Error = $"{ex.StatusCode}: {ex.Status.Detail}";
            return View("Delete", new RatingItem { Id = id });
        }
    }
}