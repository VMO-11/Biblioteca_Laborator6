using BibliotecaGrpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace Lab1_MVC.Services;

public class ReaderRatingsGrpcService(LibraryContext db)
    : ReaderRatings.ReaderRatingsBase
{
     // 1. Listarea evaluarilor unui cititor
    public override async Task ListRatings(
        ReaderRequest request,
        IServerStreamWriter<RatingItem> responseStream,
        ServerCallContext context)
    {
        ValidateReader(request.ReaderId);

        var ratings = await db.Rating
            .AsNoTracking()
            .Include(r => r.Book)
            .Where(r => r.ReaderId == request.ReaderId)
            .OrderBy(r => r.ID)
            .ToListAsync(context.CancellationToken);

        foreach (var rating in ratings)
        {
            await responseStream.WriteAsync(ToMessage(rating));
        }
    }

    // 2. Detaliile unei evaluari
    public override async Task<RatingItem> GetRating(
        RatingId request, ServerCallContext context)
    {
        var rating = await FindRating(request.Id, context.CancellationToken);
        return ToMessage(rating);
    }

    // 3. Adaugarea unei evaluari
    public override async Task<RatingItem> AddRating(
        NewRating request, ServerCallContext context)
    {
        ValidateReader(request.ReaderId);
        ValidateStars(request.Stars);

        var book = await db.Book.FindAsync(
            new object[] { request.BookId },
            context.CancellationToken);

        if (book is null)
            throw new RpcException(new Status(
                StatusCode.NotFound, "Cartea solicitata nu exista."));

        var rating = new Rating
        {
            ReaderId = request.ReaderId,
            BookID = request.BookId,
            Book = book,
            Stars = request.Stars,
            CreatedAt = DateTime.UtcNow
        };

        db.Rating.Add(rating);
        await db.SaveChangesAsync(context.CancellationToken);

        return ToMessage(rating);
    }

    // 4. Modificarea notei
    public override async Task<RatingItem> UpdateRating(
        UpdateRatingRequest request, ServerCallContext context)
    {
        ValidateStars(request.Stars);

        var rating = await FindRating(request.Id, context.CancellationToken);
        rating.Stars = request.Stars;

        await db.SaveChangesAsync(context.CancellationToken);
        return ToMessage(rating);
    }

    // 5. Stergerea unei evaluari
    public override async Task<Empty> DeleteRating(
        RatingId request, ServerCallContext context)
    {
        var rating = await FindRating(request.Id, context.CancellationToken);

        db.Rating.Remove(rating);
        await db.SaveChangesAsync(context.CancellationToken);

        return new Empty();
    }

    // Functii ajutatoare
    private async Task<Rating> FindRating(int id, CancellationToken ct)
    {
        var rating = await db.Rating
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.ID == id, ct);

        return rating ?? throw new RpcException(new Status(
            StatusCode.NotFound, "Evaluarea solicitata nu exista."));
    }

    private static void ValidateReader(int readerId)
    {
        if (readerId <= 0)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "reader_id trebuie sa fie un numar pozitiv."));
    }

    private static void ValidateStars(int stars)
    {
        if (stars < 1 || stars > 5)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Nota trebuie sa fie intre 1 si 5."));
    }

    private static RatingItem ToMessage(Rating rating)
    {
        var createdAt = DateTime.SpecifyKind(
            rating.CreatedAt, DateTimeKind.Utc);

        return new RatingItem
        {
            Id = rating.ID,
            BookId = rating.BookID,
            Title = rating.Book.Title,
            Stars = rating.Stars,
            CreatedAt = Timestamp.FromDateTime(createdAt)
        };
    }
}