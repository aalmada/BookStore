using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.ApiService.Services;
using BookStore.Shared.Models;
using Marten;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Seeds the database with sample data using event sourcing
/// </summary>

/// <summary>
/// Seeds the database with sample data using event sourcing
/// </summary>
public class DatabaseSeeder(IDocumentStore store, IMessageBus bus)
{

    public async Task SeedAsync()
    {
        await using var session = store.LightweightSession();

        // Check if already seeded
        var existingBooks = await session.Query<BookSearchProjection>().AnyAsync();
        if (existingBooks)
        {
            return; // Already seeded
        }

        // Seed in dependency order: Publishers → Authors → Categories → Books
        var publisherIds = SeedPublishers(session);
        var authorIds = SeedAuthors(session);
        var categoryIds = SeedCategories(session);

        await session.SaveChangesAsync();

        await SeedBooksAsync(store, bus, publisherIds, authorIds, categoryIds);
    }

    /// <summary>
    /// Seeds a default admin user for development purposes
    /// </summary>
    public static async Task SeedAdminUserAsync(
        Microsoft.AspNetCore.Identity.UserManager<Models.ApplicationUser> userManager)
    {
        const string adminEmail = "admin@bookstore.com";
        const string adminPassword = "Admin123!";

        // Check if admin user already exists
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is not null)
        {
            return; // Admin already exists
        }

        // Create admin user
        var adminUser = new Models.ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            // Assign Admin role
            _ = await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    static Dictionary<string, PublisherAdded> SeedPublishers(IDocumentSession session)
    {
        var publishers = new Dictionary<string, PublisherAdded>
        {
            ["Penguin"] = new(Guid.CreateVersion7(), "Penguin Random House", DateTimeOffset.UtcNow),
            ["HarperCollins"] = new(Guid.CreateVersion7(), "HarperCollins Publishers", DateTimeOffset.UtcNow),
            ["Simon"] = new(Guid.CreateVersion7(), "Simon & Schuster", DateTimeOffset.UtcNow),
            ["Hachette"] = new(Guid.CreateVersion7(), "Hachette Book Group", DateTimeOffset.UtcNow),
            ["Macmillan"] = new(Guid.CreateVersion7(), "Macmillan Publishers", DateTimeOffset.UtcNow)
        };

        foreach (var (key, @event) in publishers)
        {
            _ = session.Events.StartStream<PublisherAggregate>(@event.Id, @event);
        }

        return publishers;
    }

    static Dictionary<string, AuthorAdded> SeedAuthors(IDocumentSession session)
    {
        var authors = new[]
        {
            (Key: "Fitzgerald", Event: new AuthorAdded(Guid.CreateVersion7(), "F. Scott Fitzgerald", new Dictionary<string, AuthorTranslation> { ["en"] = new("American novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Lee", Event: new AuthorAdded(Guid.CreateVersion7(), "Harper Lee", new Dictionary<string, AuthorTranslation> { ["en"] = new("American novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Orwell", Event: new AuthorAdded(Guid.CreateVersion7(), "George Orwell", new Dictionary<string, AuthorTranslation> { ["en"] = new("English novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Austen", Event: new AuthorAdded(Guid.CreateVersion7(), "Jane Austen", new Dictionary<string, AuthorTranslation> { ["en"] = new("English novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Rowling", Event: new AuthorAdded(Guid.CreateVersion7(), "J.K. Rowling", new Dictionary<string, AuthorTranslation> { ["en"] = new("British author") }, DateTimeOffset.UtcNow)),
            (Key: "Tolkien", Event: new AuthorAdded(Guid.CreateVersion7(), "J.R.R. Tolkien", new Dictionary<string, AuthorTranslation> { ["en"] = new("English writer") }, DateTimeOffset.UtcNow)),
            (Key: "Hemingway", Event: new AuthorAdded(Guid.CreateVersion7(), "Ernest Hemingway", new Dictionary<string, AuthorTranslation> { ["en"] = new("American novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Christie", Event: new AuthorAdded(Guid.CreateVersion7(), "Agatha Christie", new Dictionary<string, AuthorTranslation> { ["en"] = new("English writer") }, DateTimeOffset.UtcNow)),
            (Key: "Martin", Event: new AuthorAdded(Guid.CreateVersion7(), "George R.R. Martin", new Dictionary<string, AuthorTranslation> { ["en"] = new("American novelist") }, DateTimeOffset.UtcNow)),
            (Key: "King", Event: new AuthorAdded(Guid.CreateVersion7(), "Stephen King", new Dictionary<string, AuthorTranslation> { ["en"] = new("American author of horror") }, DateTimeOffset.UtcNow)),
            (Key: "Herbert", Event: new AuthorAdded(Guid.CreateVersion7(), "Frank Herbert", new Dictionary<string, AuthorTranslation> { ["en"] = new("American science fiction author") }, DateTimeOffset.UtcNow)),
            (Key: "Asimov", Event: new AuthorAdded(Guid.CreateVersion7(), "Isaac Asimov", new Dictionary<string, AuthorTranslation> { ["en"] = new("American writer and professor of biochemistry") }, DateTimeOffset.UtcNow)),
            (Key: "Clarke", Event: new AuthorAdded(Guid.CreateVersion7(), "Arthur C. Clarke", new Dictionary<string, AuthorTranslation> { ["en"] = new("British science fiction writer") }, DateTimeOffset.UtcNow)),
            (Key: "Dick", Event: new AuthorAdded(Guid.CreateVersion7(), "Philip K. Dick", new Dictionary<string, AuthorTranslation> { ["en"] = new("American science fiction writer") }, DateTimeOffset.UtcNow)),
            (Key: "LeGuin", Event: new AuthorAdded(Guid.CreateVersion7(), "Ursula K. Le Guin", new Dictionary<string, AuthorTranslation> { ["en"] = new("American author") }, DateTimeOffset.UtcNow)),
            
            // Spanish Authors
            (Key: "Borges", Event: new AuthorAdded(Guid.CreateVersion7(), "Jorge Luis Borges", new Dictionary<string, AuthorTranslation> { ["es"] = new("Argentine short-story writer") }, DateTimeOffset.UtcNow)),
            (Key: "Marquez", Event: new AuthorAdded(Guid.CreateVersion7(), "Gabriel García Márquez", new Dictionary<string, AuthorTranslation> { ["es"] = new("Colombian novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Cervantes", Event: new AuthorAdded(Guid.CreateVersion7(), "Miguel de Cervantes", new Dictionary<string, AuthorTranslation> { ["es"] = new("Spanish writer") }, DateTimeOffset.UtcNow)),
            
            // French Authors
            (Key: "Hugo", Event: new AuthorAdded(Guid.CreateVersion7(), "Victor Hugo", new Dictionary<string, AuthorTranslation> { ["fr"] = new("French Romantic writer") }, DateTimeOffset.UtcNow)),
            (Key: "Camus", Event: new AuthorAdded(Guid.CreateVersion7(), "Albert Camus", new Dictionary<string, AuthorTranslation> { ["fr"] = new("French philosopher") }, DateTimeOffset.UtcNow)),
            (Key: "SaintExupery", Event: new AuthorAdded(Guid.CreateVersion7(), "Antoine de Saint-Exupéry", new Dictionary<string, AuthorTranslation> { ["fr"] = new("French writer and aviator") }, DateTimeOffset.UtcNow)),

            // German Authors
            (Key: "Goethe", Event: new AuthorAdded(Guid.CreateVersion7(), "Johann Wolfgang von Goethe", new Dictionary<string, AuthorTranslation> { ["de"] = new("German writer") }, DateTimeOffset.UtcNow)),
            (Key: "Kafka", Event: new AuthorAdded(Guid.CreateVersion7(), "Franz Kafka", new Dictionary<string, AuthorTranslation> { ["de"] = new("German-speaking Bohemian novelist") }, DateTimeOffset.UtcNow)),

            // Portuguese Authors
            (Key: "Assis", Event: new AuthorAdded(Guid.CreateVersion7(), "Machado de Assis", new Dictionary<string, AuthorTranslation> { ["pt"] = new("Brazilian novelist") }, DateTimeOffset.UtcNow)),
            (Key: "Saramago", Event: new AuthorAdded(Guid.CreateVersion7(), "José Saramago", new Dictionary<string, AuthorTranslation> { ["pt"] = new("Portuguese writer") }, DateTimeOffset.UtcNow)),
        };
        var result = new Dictionary<string, AuthorAdded>();

        foreach (var (key, @event) in authors)
        {
            _ = session.Events.StartStream<AuthorAggregate>(@event.Id, @event);
            result[key] = @event;
        }

        return result;
    }

    static Dictionary<string, CategoryAdded> SeedCategories(IDocumentSession session)
    {
        var categories = new[]
        {
            (Key: "Fiction", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Fiction", null) }, DateTimeOffset.UtcNow)),
            (Key: "Classic", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Classic Literature", null) }, DateTimeOffset.UtcNow)),
            (Key: "Fantasy", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Fantasy", null) }, DateTimeOffset.UtcNow)),
            (Key: "Mystery", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Mystery", null) }, DateTimeOffset.UtcNow)),
            (Key: "SciFi", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Science Fiction", null) }, DateTimeOffset.UtcNow)),
            (Key: "Romance", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Romance", null) }, DateTimeOffset.UtcNow)),
            (Key: "Horror", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Horror", null) }, DateTimeOffset.UtcNow)),
            (Key: "History", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("History", null) }, DateTimeOffset.UtcNow)),
        };

        var result = new Dictionary<string, CategoryAdded>();

        foreach (var (key, @event) in categories)
        {
            _ = session.Events.StartStream<CategoryAggregate>(@event.Id, @event);
            result[key] = @event;
        }

        return result;
    }

    async Task SeedBooksAsync(
        IDocumentStore store,
        IMessageBus bus,
        Dictionary<string, PublisherAdded> publishers,
        Dictionary<string, AuthorAdded> authors,
        Dictionary<string, CategoryAdded> categories)
    {
        var books = new[]
        {
            // English Books
            new { Title = "The Great Gatsby", Author = "Fitzgerald", Category = "Fiction", Year = 1925, Language = "en" },
            new { Title = "To Kill a Mockingbird", Author = "Lee", Category = "Fiction", Year = 1960, Language = "en" },
            new { Title = "1984", Author = "Orwell", Category = "SciFi", Year = 1949, Language = "en" },
            new { Title = "Pride and Prejudice", Author = "Austen", Category = "Romance", Year = 1813, Language = "en" },
            new { Title = "Harry Potter 1", Author = "Rowling", Category = "Fantasy", Year = 1997, Language = "en" },
            new { Title = "Lord of the Rings", Author = "Tolkien", Category = "Fantasy", Year = 1954, Language = "en" },
            new { Title = "Old Man and the Sea", Author = "Hemingway", Category = "Fiction", Year = 1952, Language = "en" },
            new { Title = "Orient Express", Author = "Christie", Category = "Mystery", Year = 1934, Language = "en" },
            
            // Spanish Books (Translations + Originals)
            new { Title = "El Gran Gatsby", Author = "Fitzgerald", Category = "Fiction", Year = 1925, Language = "es" },
            new { Title = "Matar a un ruiseñor", Author = "Lee", Category = "Fiction", Year = 1960, Language = "es" },
            new { Title = "El Aleph", Author = "Borges", Category = "Fiction", Year = 1949, Language = "es" },
            new { Title = "Cien años de soledad", Author = "Marquez", Category = "Fiction", Year = 1967, Language = "es" },
            new { Title = "Don Quijote", Author = "Cervantes", Category = "Classic", Year = 1605, Language = "es" },
            
            // French Books (Translations + Originals)
            new { Title = "Orgueil et Préjugés", Author = "Austen", Category = "Romance", Year = 1813, Language = "fr" },
            new { Title = "Harry Potter à l'école des sorciers", Author = "Rowling", Category = "Fantasy", Year = 1997, Language = "fr" },
            new { Title = "Les Misérables", Author = "Hugo", Category = "Classic", Year = 1862, Language = "fr" },
            new { Title = "L'Étranger", Author = "Camus", Category = "Fiction", Year = 1942, Language = "fr" },
            new { Title = "Le Petit Prince", Author = "SaintExupery", Category = "Fiction", Year = 1943, Language = "fr" },
            
             // German Books (Translations + Originals)
            new { Title = "Der alte Mann und das Meer", Author = "Hemingway", Category = "Fiction", Year = 1952, Language = "de" },
            new { Title = "Mord im Orient-Express", Author = "Christie", Category = "Mystery", Year = 1934, Language = "de" },
            new { Title = "Faust", Author = "Goethe", Category = "Classic", Year = 1808, Language = "de" },
            new { Title = "Der Prozess", Author = "Kafka", Category = "Fiction", Year = 1925, Language = "de" },

            // Portuguese Books (Translations + Originals)
            new { Title = "A Guerra dos Tronos", Author = "Martin", Category = "Fantasy", Year = 1996, Language = "pt" },
            new { Title = "O Iluminado", Author = "King", Category = "Horror", Year = 1977, Language = "pt" },
            new { Title = "Dom Casmurro", Author = "Assis", Category = "Classic", Year = 1899, Language = "pt" },
            new { Title = "Ensaio sobre a Cegueira", Author = "Saramago", Category = "Fiction", Year = 1995, Language = "pt" },
            
            // More English to fill up
            new { Title = "Winds of Winter", Author = "Martin", Category = "Fantasy", Year = 2026, Language = "en" },
            new { Title = "The Shining", Author = "King", Category = "Horror", Year = 1977, Language = "en" },
            new { Title = "Dune", Author = "Herbert", Category = "SciFi", Year = 1965, Language = "en" },
            new { Title = "Foundation", Author = "Asimov", Category = "SciFi", Year = 1951, Language = "en" },
            new { Title = "2001: A Space Odyssey", Author = "Clarke", Category = "SciFi", Year = 1968, Language = "en" },
            new { Title = "Do Androids Dream?", Author = "Dick", Category = "SciFi", Year = 1968, Language = "en" },
            new { Title = "Left Hand of Darkness", Author = "LeGuin", Category = "SciFi", Year = 1969, Language = "en" },
            new { Title = "A Game of Thrones", Author = "Martin", Category = "Fantasy", Year = 1996, Language = "en" },
            new { Title = "A Clash of Kings", Author = "Martin", Category = "Fantasy", Year = 1998, Language = "en" },
            new { Title = "A Storm of Swords", Author = "Martin", Category = "Fantasy", Year = 2000, Language = "en" },
            new { Title = "The Hobbit", Author = "Tolkien", Category = "Fantasy", Year = 1937, Language = "en" }
        };

        await using var bookSession = store.LightweightSession();
        var bookCommands = new List<UpdateBookCover>();

        foreach (var book in books)
        {
            var bookId = Guid.CreateVersion7();

            // Handle new authors dynamically if missing from seeded map, or map to existing
            var authorId = authors.TryGetValue(book.Author, out var author) ? author.Id : authors.Values.First().Id;
            var authorName = author?.Name ?? "Unknown Author";

            var categoryId = categories.TryGetValue(book.Category, out var category) ? category.Id : categories.Values.First().Id;
            var publisherId = publishers.Values.First().Id;

            var basePriceValue = book.Category switch
            {
                "Classic" => Random.Shared.Next(7, 13),
                "SciFi" or "Fantasy" => Random.Shared.Next(15, 31),
                "History" => Random.Shared.Next(20, 41),
                _ => Random.Shared.Next(10, 26)
            };

            var decimalPart = Random.Shared.Next(0, 4) switch
            {
                0 => 0.00m,
                1 => 0.49m,
                2 => 0.95m,
                _ => 0.99m
            };

            var usdPrice = (decimal)basePriceValue + decimalPart;
            var eurPrice = decimal.Max(0.99m, decimal.Round(usdPrice * 0.92m * 2) / 2 - 0.01m);
            var gbpPrice = decimal.Max(0.99m, decimal.Round(usdPrice * 0.78m * 2) / 2 - 0.01m);

            var bookAdded = BookAggregate.CreateEvent(
                bookId,
                book.Title,
                $"978000{Random.Shared.Next(1000000, 9999999)}", // valid 13-digit format
                book.Language,
                new Dictionary<string, BookTranslation> { [book.Language] = new BookTranslation($"Description for {book.Title} ({book.Language})") },
                new PartialDate(book.Year),
                publisherId,
                [authorId],
                [categoryId],
                new Dictionary<string, decimal>
                {
                    ["USD"] = usdPrice,
                    ["EUR"] = eurPrice,
                    ["GBP"] = gbpPrice
                }
            );

            _ = bookSession.Events.StartStream<BookAggregate>(bookId, bookAdded);

            try
            {
                // Use full author name and NO category
                var coverImage = CoverGenerator.GenerateCover(book.Title, authorName);
                bookCommands.Add(new UpdateBookCover(bookId, coverImage, "image/png"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate cover for {book.Title}: {ex.Message}");
            }
        }

        await bookSession.SaveChangesAsync();

        foreach (var cmd in bookCommands)
        {
            await bus.InvokeAsync(cmd);
        }
    }
}
