using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using Marten;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Seeds the database with sample data using event sourcing
/// </summary>
public class DatabaseSeeder(IDocumentStore store)
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
        SeedBooks(session, publisherIds, authorIds, categoryIds);

        await session.SaveChangesAsync();
    }

    Dictionary<string, Guid> SeedPublishers(IDocumentSession session)
    {
        var publishers = new Dictionary<string, (Guid Id, string Name, string? Website)>
        {
            ["Penguin"] = (Guid.CreateVersion7(), "Penguin Random House", "https://www.penguinrandomhouse.com"),
            ["HarperCollins"] = (Guid.CreateVersion7(), "HarperCollins Publishers", "https://www.harpercollins.com"),
            ["Simon"] = (Guid.CreateVersion7(), "Simon & Schuster", "https://www.simonandschuster.com"),
            ["Hachette"] = (Guid.CreateVersion7(), "Hachette Book Group", "https://www.hachettebookgroup.com"),
            ["Macmillan"] = (Guid.CreateVersion7(), "Macmillan Publishers", "https://us.macmillan.com")
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, (id, name, website)) in publishers)
        {
            var @event = PublisherAggregate.Create(id, name);
            session.Events.StartStream<PublisherAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    Dictionary<string, Guid> SeedAuthors(IDocumentSession session)
    {
        var authors = new Dictionary<string, (Guid Id, string Name, string? Bio)>
        {
            ["Fitzgerald"] = (Guid.CreateVersion7(), "F. Scott Fitzgerald", "American novelist and short story writer"),
            ["Lee"] = (Guid.CreateVersion7(), "Harper Lee", "American novelist known for To Kill a Mockingbird"),
            ["Orwell"] = (Guid.CreateVersion7(), "George Orwell", "English novelist, essayist, and critic"),
            ["Austen"] = (Guid.CreateVersion7(), "Jane Austen", "English novelist known for her romantic fiction"),
            ["Rowling"] = (Guid.CreateVersion7(), "J.K. Rowling", "British author, creator of Harry Potter"),
            ["Tolkien"] = (Guid.CreateVersion7(), "J.R.R. Tolkien", "English writer and philologist, author of The Lord of the Rings"),
            ["Hemingway"] = (Guid.CreateVersion7(), "Ernest Hemingway", "American novelist and short story writer"),
            ["Christie"] = (Guid.CreateVersion7(), "Agatha Christie", "English writer known for detective novels")
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, (id, name, bio)) in authors)
        {
            var @event = AuthorAggregate.Create(id, name, bio);
            session.Events.StartStream<AuthorAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    Dictionary<string, Guid> SeedCategories(IDocumentSession session)
    {
        var categories = new Dictionary<string, (Guid Id, Dictionary<string, string> Names)>
        {
            ["Fiction"] = (Guid.CreateVersion7(), new() { ["en"] = "Fiction", ["pt"] = "Ficção", ["es"] = "Ficción" }),
            ["Classic"] = (Guid.CreateVersion7(), new() { ["en"] = "Classic Literature", ["pt"] = "Literatura Clássica", ["es"] = "Literatura Clásica" }),
            ["Fantasy"] = (Guid.CreateVersion7(), new() { ["en"] = "Fantasy", ["pt"] = "Fantasia", ["es"] = "Fantasía" }),
            ["Mystery"] = (Guid.CreateVersion7(), new() { ["en"] = "Mystery", ["pt"] = "Mistério", ["es"] = "Misterio" }),
            ["SciFi"] = (Guid.CreateVersion7(), new() { ["en"] = "Science Fiction", ["pt"] = "Ficção Científica", ["es"] = "Ciencia Ficción" }),
            ["Romance"] = (Guid.CreateVersion7(), new() { ["en"] = "Romance", ["pt"] = "Romance", ["es"] = "Romance" })
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, (id, names)) in categories)
        {
            // Create CategoryTranslation dictionary with all language variants
            var translations = names.ToDictionary(
                kvp => kvp.Key,
                kvp => new CategoryTranslation(kvp.Value, null));
            
            var @event = CategoryAggregate.Create(id, translations);
            session.Events.StartStream<CategoryAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    void SeedBooks(
        IDocumentSession session,
        Dictionary<string, Guid> publisherIds,
        Dictionary<string, Guid> authorIds,
        Dictionary<string, Guid> categoryIds)
    {
        var books = new[]
        {
            new
            {
                Title = "The Great Gatsby",
                Isbn = "978-0-7432-7356-5",
                Description = "A novel set in the Jazz Age that explores themes of decadence, idealism, resistance to change, social upheaval, and excess.",
                PublicationDate = new DateOnly(1925, 4, 10),
                Publisher = "Penguin",
                Authors = new[] { "Fitzgerald" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "To Kill a Mockingbird",
                Isbn = "978-0-06-112008-4",
                Description = "A gripping, heart-wrenching, and wholly remarkable tale of coming-of-age in a South poisoned by virulent prejudice.",
                PublicationDate = new DateOnly(1960, 7, 11),
                Publisher = "HarperCollins",
                Authors = new[] { "Lee" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "1984",
                Isbn = "978-0-452-28423-4",
                Description = "A dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.",
                PublicationDate = new DateOnly(1949, 6, 8),
                Publisher = "Penguin",
                Authors = new[] { "Orwell" },
                Categories = new[] { "Fiction", "SciFi", "Classic" }
            },
            new
            {
                Title = "Pride and Prejudice",
                Isbn = "978-0-14-143951-8",
                Description = "A romantic novel of manners that follows the character development of Elizabeth Bennet.",
                PublicationDate = new DateOnly(1813, 1, 28),
                Publisher = "Penguin",
                Authors = new[] { "Austen" },
                Categories = new[] { "Fiction", "Classic", "Romance" }
            },
            new
            {
                Title = "Harry Potter and the Philosopher's Stone",
                Isbn = "978-0-7475-3269-9",
                Description = "The first novel in the Harry Potter series, following a young wizard's journey at Hogwarts School of Witchcraft and Wizardry.",
                PublicationDate = new DateOnly(1997, 6, 26),
                Publisher = "Penguin",
                Authors = new[] { "Rowling" },
                Categories = new[] { "Fantasy" }
            },
            new
            {
                Title = "The Lord of the Rings",
                Isbn = "978-0-618-00222-1",
                Description = "An epic high-fantasy novel following the quest to destroy the One Ring.",
                PublicationDate = new DateOnly(1954, 7, 29),
                Publisher = "HarperCollins",
                Authors = new[] { "Tolkien" },
                Categories = new[] { "Fantasy", "Classic" }
            },
            new
            {
                Title = "The Old Man and the Sea",
                Isbn = "978-0-684-80122-3",
                Description = "The story of an aging Cuban fisherman who struggles with a giant marlin far out in the Gulf Stream.",
                PublicationDate = new DateOnly(1952, 9, 1),
                Publisher = "Simon",
                Authors = new[] { "Hemingway" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "Murder on the Orient Express",
                Isbn = "978-0-06-207348-8",
                Description = "A detective novel featuring Hercule Poirot investigating a murder on the famous train.",
                PublicationDate = new DateOnly(1934, 1, 1),
                Publisher = "HarperCollins",
                Authors = new[] { "Christie" },
                Categories = new[] { "Mystery", "Fiction", "Classic" }
            }
        };

        foreach (var book in books)
        {
            var bookId = Guid.CreateVersion7();
            var @event = BookAggregate.Create(
                bookId,
                book.Title,
                book.Isbn,
                book.Description,
                book.PublicationDate,
                publisherIds[book.Publisher],
                book.Authors.Select(a => authorIds[a]).ToList(),
                book.Categories.Select(c => categoryIds[c]).ToList()
            );

            session.Events.StartStream<BookAggregate>(bookId, @event);
        }
    }
}
