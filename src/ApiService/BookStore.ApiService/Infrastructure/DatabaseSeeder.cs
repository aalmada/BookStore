using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.Extensions.Options;

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
            (Key: "Fitzgerald", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "F. Scott Fitzgerald",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("American novelist and short story writer"),
                    ["pt"] = new("Romancista e contista americano"),
                    ["es"] = new("Novelista y cuentista estadounidense"),
                    ["fr"] = new("Romancier et nouvelliste américain"),
                    ["de"] = new("Amerikanischer Romanautor und Kurzgeschichtenschreiber")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Lee", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "Harper Lee",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("American novelist known for To Kill a Mockingbird"),
                    ["pt"] = new("Romancista americana conhecida por O Sol é Para Todos"),
                    ["es"] = new("Novelista estadounidense conocida por Matar un ruiseñor"),
                    ["fr"] = new("Romancière américaine connue pour Ne tirez pas sur l'oiseau moqueur"),
                    ["de"] = new("Amerikanische Romanautorin, bekannt für Wer die Nachtigall stört")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Orwell", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "George Orwell",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("English novelist, essayist, and critic"),
                    ["pt"] = new("Romancista, ensaísta e crítico inglês"),
                    ["es"] = new("Novelista, ensayista y crítico inglés"),
                    ["fr"] = new("Romancier, essayiste et critique anglais"),
                    ["de"] = new("Englischer Romanautor, Essayist und Kritiker")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Austen", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "Jane Austen",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("English novelist known for her romantic fiction"),
                    ["pt"] = new("Romancista inglesa conhecida por sua ficção romântica"),
                    ["es"] = new("Novelista inglesa conocida por su ficción romántica"),
                    ["fr"] = new("Romancière anglaise connue pour sa fiction romantique"),
                    ["de"] = new("Englische Romanautorin, bekannt für ihre romantischen Romane")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Rowling", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "J.K. Rowling",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("British author, creator of Harry Potter"),
                    ["pt"] = new("Autora britânica, criadora de Harry Potter"),
                    ["es"] = new("Autora británica, creadora de Harry Potter"),
                    ["fr"] = new("Auteure britannique, créatrice de Harry Potter"),
                    ["de"] = new("Britische Autorin, Schöpferin von Harry Potter")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Tolkien", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "J.R.R. Tolkien",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("English writer and philologist, author of The Lord of the Rings"),
                    ["pt"] = new("Escritor e filólogo inglês, autor de O Senhor dos Anéis"),
                    ["es"] = new("Escritor y filólogo inglês, autor de El Señor de los Anillos"),
                    ["fr"] = new("Écrivain et philologue anglais, auteur du Seigneur des Anneaux"),
                    ["de"] = new("Englischer Schriftsteller und Philologe, Autor von Der Herr der Ringe")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Hemingway", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "Ernest Hemingway",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("American novelist and short story writer"),
                    ["pt"] = new("Romancista e contista americano"),
                    ["es"] = new("Novelista y cuentista estadounidense"),
                    ["fr"] = new("Romancier et nouvelliste américain"),
                    ["de"] = new("Amerikanischer Romanautor und Kurzgeschichtenschreiber")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Christie", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "Agatha Christie",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("English writer known for detective novels"),
                    ["pt"] = new("Escritora inglesa conhecida por romances policiais"),
                    ["es"] = new("Escritora inglesa conocida por novelas de detectives"),
                    ["fr"] = new("Écrivaine anglaise connue pour ses romans policiers"),
                    ["de"] = new("Englische Schriftstellerin, bekannt für Kriminalromane")
                },
                DateTimeOffset.UtcNow)),
            (Key: "Martin", Event: new AuthorAdded(
                Guid.CreateVersion7(),
                "George R.R. Martin",
                new Dictionary<string, AuthorTranslation>
                {
                    ["en"] = new("American novelist and short story writer, author of A Song of Ice and Fire"),
                    ["pt"] = new("Romancista e contista americano, autor de As Crônicas de Gelo e Fogo"),
                    ["es"] = new("Novelista y cuentista estadounidense, autor de Canción de Hielo y Fuego"),
                    ["fr"] = new("Romancier et nouvelliste américain, auteur du Trône de Fer"),
                    ["de"] = new("Amerikanischer Romanautor und Kurzgeschichtenschreiber, Autor von Das Lied von Eis und Feuer")
                },
                DateTimeOffset.UtcNow))
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
            (Key: "Fiction", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Fiction", null), ["pt"] = new("Ficção", null), ["es"] = new("Ficción", null), ["fr"] = new("Fiction", null), ["de"] = new("Belletristik", null) }, DateTimeOffset.UtcNow)),
            (Key: "Classic", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Classic Literature", null), ["pt"] = new("Literatura Clássica", null), ["es"] = new("Literatura Clásica", null), ["fr"] = new("Littérature Classique", null), ["de"] = new("Klassische Literatur", null) }, DateTimeOffset.UtcNow)),
            (Key: "Fantasy", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Fantasy", null), ["pt"] = new("Fantasia", null), ["es"] = new("Fantasía", null), ["fr"] = new("Fantaisie", null), ["de"] = new("Fantasy", null) }, DateTimeOffset.UtcNow)),
            (Key: "Mystery", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Mystery", null), ["pt"] = new("Mistério", null), ["es"] = new("Misterio", null), ["fr"] = new("Mystère", null), ["de"] = new("Krimi", null) }, DateTimeOffset.UtcNow)),
            (Key: "SciFi", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Science Fiction", null), ["pt"] = new("Ficção Científica", null), ["es"] = new("Ciencia Ficción", null), ["fr"] = new("Science-Fiction", null), ["de"] = new("Science-Fiction", null) }, DateTimeOffset.UtcNow)),
            (Key: "Romance", Event: new CategoryAdded(Guid.CreateVersion7(), new Dictionary<string, CategoryTranslation> { ["en"] = new("Romance", null), ["pt"] = new("Romance", null), ["es"] = new("Romance", null), ["fr"] = new("Romance", null), ["de"] = new("Liebesroman", null) }, DateTimeOffset.UtcNow))
        };

        var result = new Dictionary<string, CategoryAdded>();

        foreach (var (key, @event) in categories)
        {
            _ = session.Events.StartStream<CategoryAggregate>(@event.Id, @event);
            result[key] = @event;
        }

        return result;
    }

    static void SeedBooks(
        IDocumentSession session,
        Dictionary<string, PublisherAdded> publishers,
        Dictionary<string, AuthorAdded> authors,
        Dictionary<string, CategoryAdded> categories)
    {
        var books = new[]
        {
            new
            {
                Title = "The Great Gatsby",
                Isbn = "978-0-7432-7356-5",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("A novel set in the Jazz Age that explores themes of decadence, idealism, resistance to change, social upheaval, and excess.")),
                    ("pt", new BookTranslation("Um romance ambientado na Era do Jazz que explora temas de decadência, idealismo, resistência à mudança, agitação social e excesso.")),
                    ("es", new BookTranslation("Una novela ambientada en la Era del Jazz que explora temas de decadencia, idealismo, resistencia al cambio, agitación social y exceso.")),
                    ("fr", new BookTranslation("Un roman situé dans l'ère du Jazz qui explore les thèmes de la décadence, de l'idéalisme, de la résistance au changement, des bouleversements sociaux et de l'excès.")),
                    ("de", new BookTranslation("Ein Roman aus der Jazz-Ära, der Themen wie Dekadenz, Idealismus, Widerstand gegen Veränderung, soziale Umwälzung und Exzess erforscht."))
                },
                PublicationDate = new PartialDate(1925, 4, 10),
                Publisher = "Penguin",
                Authors = new[] { "Fitzgerald" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "To Kill a Mockingbird",
                Isbn = "978-0-06-112008-4",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("A gripping, heart-wrenching, and wholly remarkable tale of coming-of-age in a South poisoned by virulent prejudice.")),
                    ("pt", new BookTranslation("Uma história comovente, emocionante e notável sobre amadurecimento em um Sul envenenado por preconceito virulento.")),
                    ("es", new BookTranslation("Una historia apasionante, desgarradora y completamente notable sobre la mayoría de edad en un Sur envenenado por prejuicios virulentos.")),
                    ("fr", new BookTranslation("Un récit captivant, déchirant et tout à fait remarquable sur le passage à l'âge adulte dans un Sud empoisonné par des préjugés virulents.")),
                    ("de", new BookTranslation("Eine fesselnde, herzzerreißende und bemerkenswerte Geschichte über das Erwachsenwerfen in einem von virulenten Vorurteilen vergifteten Süden."))
                },
                PublicationDate = new PartialDate(1960, 7), // Year and month only
                Publisher = "HarperCollins",
                Authors = new[] { "Lee" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "1984",
                Isbn = "978-0-452-28423-4",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("A dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.")),
                    ("pt", new BookTranslation("Um romance distópico de ficção científica social e conto de advertência sobre os perigos do totalitarismo.")),
                    ("es", new BookTranslation("Una novela distópica de ciencia ficción social y cuento de advertencia sobre los peligros del totalitarismo.")),
                    ("fr", new BookTranslation("Un roman dystopique de science-fiction sociale et un conte d'avertissement sur les dangers du totalitarisme.")),
                    ("de", new BookTranslation("Ein dystopischer sozialwissenschaftlicher Science-Fiction-Roman und eine Warnung vor den Gefahren des Totalitarismus."))
                },
                PublicationDate = new PartialDate(1949), // Year only
                Publisher = "Penguin",
                Authors = new[] { "Orwell" },
                Categories = new[] { "Fiction", "SciFi", "Classic" }
            },
            new
            {
                Title = "1984",
                Isbn = "978-85-359-0277-4",
                Language = "pt",
                Descriptions = new[]
                {
                    ("pt", new BookTranslation("Um romance distópico de ficção científica social e conto de advertência sobre os perigos do totalitarismo.")),
                    ("en", new BookTranslation("Portuguese edition of the dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.")),
                    ("es", new BookTranslation("Edición portuguesa de la novela distópica de ciencia ficción social y cuento de advertencia sobre los peligros del totalitarismo.")),
                    ("fr", new BookTranslation("Édition portugaise du roman dystopique de science-fiction sociale et un conte d'avertissement sur les dangers du totalitarisme.")),
                    ("de", new BookTranslation("Portugiesische Ausgabe des dystopischen sozialwissenschaftlichen Science-Fiction-Romans und eine Warnung vor den Gefahren des Totalitarismus."))
                },
                PublicationDate = new PartialDate(1984), // Brazilian Portuguese edition
                Publisher = "Penguin",
                Authors = new[] { "Orwell" },
                Categories = new[] { "Fiction", "SciFi", "Classic" }
            },
            new
            {
                Title = "Pride and Prejudice",
                Isbn = "978-0-14-143951-8",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("A romantic novel of manners that follows the character development of Elizabeth Bennet.")),
                    ("pt", new BookTranslation("Um romance de costumes que acompanha o desenvolvimento do personagem de Elizabeth Bennet.")),
                    ("es", new BookTranslation("Una novela romántica de costumbres que sigue el desarrollo del personaje de Elizabeth Bennet.")),
                    ("fr", new BookTranslation("Un roman romantique de mœurs qui suit le développement du personnage d'Elizabeth Bennet.")),
                    ("de", new BookTranslation("Ein romantischer Sittenroman, der die Charakterentwicklung von Elizabeth Bennet verfolgt."))
                },
                PublicationDate = new PartialDate(1813, 1, 28),
                Publisher = "Penguin",
                Authors = new[] { "Austen" },
                Categories = new[] { "Fiction", "Classic", "Romance" }
            },
            new
            {
                Title = "Harry Potter and the Philosopher's Stone",
                Isbn = "978-0-7475-3269-9",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("The first novel in the Harry Potter series, following a young wizard's journey at Hogwarts School of Witchcraft and Wizardry.")),
                    ("pt", new BookTranslation("O primeiro romance da série Harry Potter, seguindo a jornada de um jovem bruxo na Escola de Magia e Bruxaria de Hogwarts.")),
                    ("es", new BookTranslation("La primera novela de la serie Harry Potter, siguiendo el viaje de un joven mago en la Escuela de Magia y Hechicería de Hogwarts.")),
                    ("fr", new BookTranslation("Le premier roman de la série Harry Potter, suivant le voyage d'un jeune sorcier à l'école de sorcellerie de Poudlard.")),
                    ("de", new BookTranslation("Der erste Roman der Harry-Potter-Reihe, der die Reise eines jungen Zauberers an der Hogwarts-Schule für Hexerei und Zauberei verfolgt."))
                },
                PublicationDate = new PartialDate(1997, 6, 26),
                Publisher = "Penguin",
                Authors = new[] { "Rowling" },
                Categories = new[] { "Fantasy" }
            },
            new
            {
                Title = "The Lord of the Rings",
                Isbn = "978-0-618-00222-1",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("An epic high-fantasy novel following the quest to destroy the One Ring.")),
                    ("pt", new BookTranslation("Um épico romance de alta fantasia seguindo a busca para destruir o Um Anel.")),
                    ("es", new BookTranslation("Una épica novela de alta fantasía que sigue la búsqueda para destruir el Anillo Único.")),
                    ("fr", new BookTranslation("Un roman épique de haute fantasy suivant la quête pour détruire l'Anneau Unique.")),
                    ("de", new BookTranslation("Ein epischer High-Fantasy-Roman, der die Quest zur Zerstörung des Einen Rings verfolgt."))
                },
                PublicationDate = new PartialDate(1954, 7), // Year and month only
                Publisher = "HarperCollins",
                Authors = new[] { "Tolkien" },
                Categories = new[] { "Fantasy", "Classic" }
            },
            new
            {
                Title = "The Old Man and the Sea",
                Isbn = "978-0-684-80122-3",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("The story of an aging Cuban fisherman who struggles with a giant marlin far out in the Gulf Stream.")),
                    ("pt", new BookTranslation("A história de um pescador cubano idoso que luta com um marlim gigante no Golfo do México.")),
                    ("es", new BookTranslation("La historia de un pescador cubano envejecido que lucha con un marlín gigante en el Golfo de México.")),
                    ("fr", new BookTranslation("L'histoire d'un pêcheur cubain vieillissant qui lutte avec un marlin géant dans le Gulf Stream.")),
                    ("de", new BookTranslation("Die Geschichte eines alternden kubanischen Fischers, der mit einem riesigen Marlin weit draußen im Golfstrom kämpft."))
                },
                PublicationDate = new PartialDate(1952), // Year only
                Publisher = "Simon",
                Authors = new[] { "Hemingway" },
                Categories = new[] { "Fiction", "Classic" }
            },
            new
            {
                Title = "Murder on the Orient Express",
                Isbn = "978-0-06-207348-8",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("A detective novel featuring Hercule Poirot investigating a murder on the famous train.")),
                    ("pt", new BookTranslation("Um romance policial com Hercule Poirot investigando um assassinato no famoso trem.")),
                    ("es", new BookTranslation("Una novela de detectives con Hercule Poirot investigando un asesinato en el famoso tren.")),
                    ("fr", new BookTranslation("Un roman policier mettant en vedette Hercule Poirot enquêtant sur un meurtre dans le célèbre train.")),
                    ("de", new BookTranslation("Ein Kriminalroman mit Hercule Poirot, der einen Mord im berühmten Zug untersucht."))
                },
                PublicationDate = new PartialDate(1934, 1, 1),
                Publisher = "HarperCollins",
                Authors = new[] { "Christie" },
                Categories = new[] { "Mystery", "Fiction", "Classic" }
            },
            new
            {
                Title = "The Winds of Winter",
                Isbn = "978-0-553-80147-7",
                Language = "en",
                Descriptions = new[]
                {
                    ("en", new BookTranslation("The highly anticipated sixth novel in the epic fantasy series A Song of Ice and Fire.")),
                    ("pt", new BookTranslation("O aguardado sexto romance da série épica de fantasia As Crônicas de Gelo e Fogo.")),
                    ("es", new BookTranslation("La esperada sexta novela de la serie de fantasía épica Canción de Hielo y Fuego.")),
                    ("fr", new BookTranslation("Le sixième roman très attendu de la série de fantasy épique Le Trône de Fer.")),
                    ("de", new BookTranslation("Der mit Spannung erwartete sechste Roman der epischen Fantasy-Serie Das Lied von Eis und Feuer."))
                },
                PublicationDate = new PartialDate(2026, 3),
                Publisher = "Penguin",
                Authors = new[] { "Martin" },
                Categories = new[] { "Fantasy", "Fiction" }
            }
        };

        foreach (var book in books)
        {
            var bookId = Guid.CreateVersion7();
            var @event = BookAggregate.Create(
                bookId,
                book.Title,
                book.Isbn,
                book.Language,
                book.Descriptions.ToDictionary(x => x.Item1, x => x.Item2),
                book.PublicationDate,
                publishers[book.Publisher].Id,
                [.. book.Authors.Select(a => authors[a].Id)],
                [.. book.Categories.Select(c => categories[c].Id)]
            );

            _ = session.Events.StartStream<BookAggregate>(bookId, @event);
        }
    }
}
