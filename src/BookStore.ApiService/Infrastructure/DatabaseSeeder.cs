using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.ApiService.Services;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Seeds the database with initial data.
/// </summary>
public class DatabaseSeeder(
    IDocumentStore store,
    IMessageBus bus,
    ILogger<DatabaseSeeder> logger)
{

    public async Task SeedTenantsAsync(string[] tenantIds)
    {
        // Use a session on the 'default' tenant to manage Tenant documents
        // This assumes Tenant documents are stored in the default tenant
        await using var session = store.LightweightSession();

        foreach (var tenantId in tenantIds)
        {
            var existing = await session.LoadAsync<BookStore.ApiService.Models.Tenant>(tenantId);

            var (name, tagline, color) = GetTenantInfo(tenantId);

            if (existing == null)
            {
                var tenant = new BookStore.ApiService.Models.Tenant
                {
                    Id = tenantId,
                    Name = name,
                    Tagline = tagline,
                    ThemePrimaryColor = color,
                    IsEnabled = true
                };
                session.Store(tenant);
                Log.Seeding.SeedingNewTenant(logger, tenantId);
            }
            else
            {
                // Ensure tenant is enabled and data is correct
                existing.IsEnabled = true;
                existing.Name = name;
                existing.Tagline = tagline;
                existing.ThemePrimaryColor = color;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                // Marten automatically tracks changes - no need for Update()
                Log.Seeding.UpdatedExistingTenant(logger, tenantId);
            }
        }

        await session.SaveChangesAsync();
    }

    static (string Name, string Tagline, string Color) GetTenantInfo(string tenantId) => tenantId.ToLowerInvariant() switch
    {
        "acme" => ("Acme Corp", "Innovation in every page - Your corporate knowledge hub", "#FF9800"),
        "contoso" => ("Contoso Ltd", "Empowering minds through literature and learning", "#D32F2F"),
        _ => ("BookStore", "Discover your next great read from our curated collection", "#594AE2")
    };

    public async Task SeedAsync(string tenantId)
    {
        // Since we use "*DEFAULT*" as the default tenant ID, we can pass it directly
        await using var session = store.LightweightSession(tenantId);

        // Seed tenant-specific admin user using the tenant-scoped session
        // This is outside the 'existingBooks' guard to ensure admins are always present
        // even if data was previously partially seeded. The method itself handles idempotency.
        _ = await SeedAdminUserAsync(session, tenantId, logger: logger);

        // Check if already seeded with books
        var existingBooks = await session.Query<BookSearchProjection>().AnyAsync();
        if (existingBooks)
        {
            Log.Seeding.DatabaseAlreadySeeded(logger);
            return; // Already seeded with content
        }

        Log.Seeding.StartingTenantSeeding(logger, tenantId);

        // Seed in dependency order: Publishers → Authors → Categories → Books
        var publisherIds = SeedPublishers(session, logger);
        var authorIds = SeedAuthors(session, logger);
        var categoryIds = SeedCategories(session, logger);

        await session.SaveChangesAsync();

        var books = GetBooksForTenant(tenantId);
        await SeedBooksAsync(store, bus, publisherIds, authorIds, categoryIds, logger, tenantId, books);

        // Seed sales logic works by querying existing books, so it adapts to whatever was seeded
        await SeedSalesAsync(tenantId);

        Log.Seeding.DatabaseSeedingCompleted(logger);
    }

    /// <summary>
    /// Seeds a tenant-specific admin user for development purposes
    /// </summary>
    public static async Task<Models.ApplicationUser?> SeedAdminUserAsync(
        IDocumentSession session,
        string tenantId,
        string? email = null,
        string? password = null,
        bool confirmEmail = true,
        ILogger? logger = null)
    {
        // Generate tenant-specific email if not provided
        var adminEmail = email ?? (StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? "admin@bookstore.com"
            : $"admin@{tenantId}.com");

        var adminPassword = password ?? "Admin123!";

        // Check if admin user already exists in THIS tenant
        var existingAdmin = await session.Query<Models.ApplicationUser>()
            .Where(u => u.Email == adminEmail)
            .FirstOrDefaultAsync();

        if (existingAdmin is not null)
        {
            // User already exists in this tenant, ensure they have Admin role and are confirmed
            var changed = false;
            if (!existingAdmin.Roles.Contains("Admin"))
            {
                existingAdmin.Roles.Add("Admin");
                changed = true;
            }

            if (!existingAdmin.EmailConfirmed)
            {
                existingAdmin.EmailConfirmed = true;
                changed = true;
            }

            // In development, ensure the password is what we expect
            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Models.ApplicationUser>();
            var verifyResult = passwordHasher.VerifyHashedPassword(existingAdmin, existingAdmin.PasswordHash!, adminPassword);
            if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                existingAdmin.PasswordHash = passwordHasher.HashPassword(existingAdmin, adminPassword);
                changed = true;
            }

            if (changed)
            {
                session.Store(existingAdmin);
                await session.SaveChangesAsync();
                if (logger != null)
                {
                    Log.Seeding.UpdatedExistingAdminUser(logger, adminEmail, tenantId);
                }
            }

            return existingAdmin;
        }

        // Create admin user directly in the tenant-scoped session
        // This ensures the user is created in the CORRECT tenant
        var adminUser = new Models.ApplicationUser
        {
            UserName = adminEmail,
            NormalizedUserName = adminEmail.ToUpperInvariant(),
            Email = adminEmail,
            NormalizedEmail = adminEmail.ToUpperInvariant(),
            EmailConfirmed = confirmEmail,
            Roles = ["Admin"],
            SecurityStamp = Guid.CreateVersion7().ToString("D"),
            ConcurrencyStamp = Guid.CreateVersion7().ToString("D")
        };

        // Hash the password
        var passwordHasherForNew = new Microsoft.AspNetCore.Identity.PasswordHasher<Models.ApplicationUser>();
        adminUser.PasswordHash = passwordHasherForNew.HashPassword(adminUser, adminPassword);

        // Store in the tenant-scoped session
        session.Store(adminUser);
        await session.SaveChangesAsync();

        if (logger != null)
        {
            Log.Seeding.CreatedNewAdminUser(logger, adminEmail, tenantId);
        }

        return adminUser;
    }

    static Dictionary<string, PublisherAdded> SeedPublishers(IDocumentSession session, ILogger logger)
    {
        Log.Seeding.SeedingPublishers(logger);

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

        Log.Seeding.seededPublishers(logger, publishers.Count);
        return publishers;
    }

    static Dictionary<string, AuthorAdded> SeedAuthors(IDocumentSession session, ILogger logger)
    {
        Log.Seeding.SeedingAuthors(logger);
        var authors = new[]
        {
            (Key: "Fitzgerald", Event: new AuthorAdded(Guid.CreateVersion7(), "F. Scott Fitzgerald", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Francis Scott Key Fitzgerald was an American novelist, essayist, and screenwriter. He is best known for his novels depicting the flamboyance and excess of the Jazz Age."),
                ["es"] = new("Francis Scott Key Fitzgerald fue un novelista, ensayista y guionista estadounidense. Es conocido por sus novelas que retratan la extravagancia y el exceso de la Era del Jazz.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Lee", Event: new AuthorAdded(Guid.CreateVersion7(), "Harper Lee", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Nelle Harper Lee was an American novelist. She is best known for her 1960 novel To Kill a Mockingbird, which won the 1961 Pulitzer Prize and has become a classic of modern American literature."),
                ["es"] = new("Nelle Harper Lee fue una novelista estadounidense. Es conocida por su novela de 1960 Matar a un ruiseñor, que ganó el Premio Pulitzer de 1961.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Orwell", Event: new AuthorAdded(Guid.CreateVersion7(), "George Orwell", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Eric Arthur Blair, better known by his pen name George Orwell, was an English novelist, essayist, journalist, and critic. He is best known for the allegorical novella Animal Farm (1945) and the dystopian novel Nineteen Eighty-Four (1949)."),
                ["es"] = new("Eric Arthur Blair, más conocido por su seudónimo George Orwell, fue un novelista, ensayista, periodista y crítico inglés.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Austen", Event: new AuthorAdded(Guid.CreateVersion7(), "Jane Austen", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Jane Austen was an English novelist known primarily for her six major novels, which interpret, critique, and comment upon the British landed gentry at the end of the 18th century."),
                ["fr"] = new("Jane Austen est une romancière anglaise connue principalement pour ses six grands romans, qui interprètent, critiquent et commentent la petite noblesse terrienne britannique à la fin du XVIIIe siècle.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Rowling", Event: new AuthorAdded(Guid.CreateVersion7(), "J.K. Rowling", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Joanne Rowling, better known by her pen name J.K. Rowling, is a British author, philanthropist, film producer, television producer, and screenwriter. She is best known for writing the Harry Potter fantasy series."),
                ["fr"] = new("Joanne Rowling, plus connue sous son nom de plume J.K. Rowling, est une romancière, scénariste et productrice de cinéma britannique.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Tolkien", Event: new AuthorAdded(Guid.CreateVersion7(), "J.R.R. Tolkien", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("John Ronald Reuel Tolkien was an English writer, poet, philologist, and academic, best known as the author of the high fantasy works The Hobbit and The Lord of the Rings."),
                ["de"] = new("John Ronald Reuel Tolkien war ein englischer Schriftsteller, Philologe und Akademiker.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Hemingway", Event: new AuthorAdded(Guid.CreateVersion7(), "Ernest Hemingway", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Ernest Miller Hemingway was an American novelist, short-story writer, and journalist. His economical and understated style—which he termed the iceberg theory—had a strong influence on 20th-century fiction."),
                ["de"] = new("Ernest Miller Hemingway war ein US-amerikanischer Schriftsteller und Reporter.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Christie", Event: new AuthorAdded(Guid.CreateVersion7(), "Agatha Christie", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Dame Agatha Mary Clarissa Christie was an English writer known for her 66 detective novels and 14 short story collections, particularly those revolving around fictional detectives Hercule Poirot and Miss Marple."),
                ["de"] = new("Dame Agatha Mary Clarissa Christie war eine britische Schriftstellerin.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Martin", Event: new AuthorAdded(Guid.CreateVersion7(), "George R.R. Martin", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("George Raymond Richard Martin, also known as GRRM, is an American novelist, screenwriter, television producer, and short story writer. He is the author of the series of epic fantasy novels A Song of Ice and Fire."),
                ["pt"] = new("George Raymond Richard Martin, também conhecido como GRRM, é um roteirista e escritor de ficção científica, terror e fantasia norte-americano.")
            }, DateTimeOffset.UtcNow)),
            (Key: "King", Event: new AuthorAdded(Guid.CreateVersion7(), "Stephen King", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Stephen Edwin King is an American author of horror, supernatural fiction, suspense, crime, science-fiction, and fantasy novels."),
                ["pt"] = new("Stephen Edwin King é um escritor norte-americano de contos de horror, ficção sobrenatural, suspense, ficção científica e fantasia.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Herbert", Event: new AuthorAdded(Guid.CreateVersion7(), "Frank Herbert", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Franklin Patrick Herbert Jr. was an American science fiction author best known for his 1965 novel Dune and its five sequels.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Asimov", Event: new AuthorAdded(Guid.CreateVersion7(), "Isaac Asimov", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Isaac Asimov was an American writer and professor of biochemistry at Boston University. He was known for his works of science fiction and popular science.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Clarke", Event: new AuthorAdded(Guid.CreateVersion7(), "Arthur C. Clarke", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Sir Arthur Charles Clarke was an English science-fiction writer, science writer, futurist, inventor, undersea explorer, and television series host.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Dick", Event: new AuthorAdded(Guid.CreateVersion7(), "Philip K. Dick", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Philip Kindred Dick was an American science fiction writer. He wrote 44 novels and about 121 short stories, most of which appeared in science fiction magazines during his lifetime.")
            }, DateTimeOffset.UtcNow)),
            (Key: "LeGuin", Event: new AuthorAdded(Guid.CreateVersion7(), "Ursula K. Le Guin", new Dictionary<string, AuthorTranslation> {
                ["en"] = new("Ursula Kroeber Le Guin was an American author best known for her works of speculative fiction, including science fiction works set in her Hainish universe, and the Earthsea fantasy series.")
            }, DateTimeOffset.UtcNow)),
            
            // Spanish Authors
            (Key: "Borges", Event: new AuthorAdded(Guid.CreateVersion7(), "Jorge Luis Borges", new Dictionary<string, AuthorTranslation> {
                ["es"] = new("Jorge Francisco Isidoro Luis Borges fue un escritor, poeta, ensayista y traductor argentino, extensamente considerado una figura clave tanto para la literatura en habla hispana como para la literatura universal."),
                ["en"] = new("Jorge Francisco Isidoro Luis Borges was an Argentine short-story writer, essayist, poet and translator, and a key figure in Spanish-language and universal literature.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Marquez", Event: new AuthorAdded(Guid.CreateVersion7(), "Gabriel García Márquez", new Dictionary<string, AuthorTranslation> {
                ["es"] = new("Gabriel José de la Concordia García Márquez fue un escritor y periodista colombiano. Reconocido principalmente por sus novelas y cuentos, también escribió narrativa de no ficción, discursos, reportajes, críticas cinematográficas y memorias."),
                ["en"] = new("Gabriel José de la Concordia García Márquez was a Colombian novelist, short-story writer, screenwriter, and journalist.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Cervantes", Event: new AuthorAdded(Guid.CreateVersion7(), "Miguel de Cervantes", new Dictionary<string, AuthorTranslation> {
                ["es"] = new("Miguel de Cervantes Saavedra fue un novelista, poeta, dramaturgo y soldado español. Es ampliamente considerado una de las máximas figuras de la literatura española."),
                ["en"] = new("Miguel de Cervantes Saavedra was a Spanish writer widely regarded as the greatest writer in the Spanish language and one of the world's pre-eminent novelists.")
            }, DateTimeOffset.UtcNow)),
            
            // French Authors
            (Key: "Hugo", Event: new AuthorAdded(Guid.CreateVersion7(), "Victor Hugo", new Dictionary<string, AuthorTranslation> {
                ["fr"] = new("Victor-Marie Hugo est un poète, dramaturge, et prosateur romantique considéré comme l'un des plus importants écrivains de la langue française."),
                ["en"] = new("Victor-Marie Hugo was a French poet, novelist, and dramatist of the Romantic movement.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Camus", Event: new AuthorAdded(Guid.CreateVersion7(), "Albert Camus", new Dictionary<string, AuthorTranslation> {
                ["fr"] = new("Albert Camus est un écrivain, philosophe, romancier, dramaturge, essayiste et nouvelliste français."),
                ["en"] = new("Albert Camus was a French philosopher, author, dramatist, and journalist.")
            }, DateTimeOffset.UtcNow)),
            (Key: "SaintExupery", Event: new AuthorAdded(Guid.CreateVersion7(), "Antoine de Saint-Exupéry", new Dictionary<string, AuthorTranslation> {
                ["fr"] = new("Antoine de Saint-Exupéry, né le 29 juin 1900 à Lyon et disparu en vol le 31 juillet 1944 au large de Marseille, est un écrivain, poète, aviateur et reporter français."),
                ["en"] = new("Antoine Marie Jean-Baptiste Roger, comte de Saint-Exupéry, simply known as de Saint-Exupéry, was a French writer, poet, aristocrat, journalist and pioneering aviator.")
            }, DateTimeOffset.UtcNow)),

            // German Authors
            (Key: "Goethe", Event: new AuthorAdded(Guid.CreateVersion7(), "Johann Wolfgang von Goethe", new Dictionary<string, AuthorTranslation> {
                ["de"] = new("Johann Wolfgang von Goethe war ein deutscher Dichter und Naturforscher. Er gilt als einer der bedeutendsten Schöpfer deutschsprachiger Dichtung."),
                ["en"] = new("Johann Wolfgang von Goethe was a German poet, playwright, novelist, scientist, statesman, theatre director, and critic.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Kafka", Event: new AuthorAdded(Guid.CreateVersion7(), "Franz Kafka", new Dictionary<string, AuthorTranslation> {
                ["de"] = new("Franz Kafka war ein deutschsprachiger Schriftsteller. Sein breiterem Publikum erst postum bekannt gewordenes Werk zählt zum unbestrittenen Kanon der Weltliteratur."),
                ["en"] = new("Franz Kafka was a German-speaking Bohemian novelist and short-story writer, widely regarded as one of the major figures of 20th-century literature.")
            }, DateTimeOffset.UtcNow)),

            // Portuguese Authors
            (Key: "Assis", Event: new AuthorAdded(Guid.CreateVersion7(), "Machado de Assis", new Dictionary<string, AuthorTranslation> {
                ["pt"] = new("Joaquim Maria Machado de Assis foi um escritor brasileiro, considerado por muitos críticos, estudiosos, escritores e leitores como um dos maiores senão o maior nome da literatura do Brasil."),
                ["en"] = new("Joaquim Maria Machado de Assis was a Brazilian pioneer novelist, poet, playwright and short story writer, widely regarded as the greatest writer of Brazilian literature.")
            }, DateTimeOffset.UtcNow)),
            (Key: "Saramago", Event: new AuthorAdded(Guid.CreateVersion7(), "José Saramago", new Dictionary<string, AuthorTranslation> {
                ["pt"] = new("José de Sousa Saramago foi um escritor, argumentista, teatral, ensaísta, jornalista, dramaturgo, contista, romancista e poeta português."),
                ["en"] = new("José de Sousa Saramago was a Portuguese writer and recipient of the 1998 Nobel Prize in Literature.")
            }, DateTimeOffset.UtcNow)),
        };
        var result = new Dictionary<string, AuthorAdded>();

        foreach (var (key, @event) in authors)
        {
            _ = session.Events.StartStream<AuthorAggregate>(@event.Id, @event);
            result[key] = @event;
        }

        Log.Seeding.SeededAuthors(logger, result.Count);
        return result;
    }

    static Dictionary<string, CategoryAdded> SeedCategories(IDocumentSession session, ILogger logger)
    {
        Log.Seeding.SeedingCategories(logger);
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

        Log.Seeding.SeededCategories(logger, result.Count);
        return result;
    }

    static IEnumerable<BookSeedData> GetBooksForTenant(string tenantId)
    {
        var allBooks = new BookSeedData[]
        {
            // English Books
            new("The Great Gatsby", "Fitzgerald", "Fiction", 1925, "en", new() {
                ["en"] = "The Great Gatsby is a 1925 novel by American writer F. Scott Fitzgerald. Set in the Jazz Age on Long Island.",
                ["es"] = "El gran Gatsby es una novela de 1925 escrita por el autor estadounidense F. Scott Fitzgerald. Ambientada en la Era del Jazz.",
                ["fr"] = "Gatsby le Magnifique est un roman de F. Scott Fitzgerald paru en 1925. L'histoire se déroule à Long Island pendant les années folles.",
                ["de"] = "Der große Gatsby ist ein Roman von F. Scott Fitzgerald aus dem Jahr 1925. Er spielt auf Long Island während der 'Roaring Twenties'.",
                ["pt"] = "O Grande Gatsby é um romance de 1925 do escritor americano F. Scott Fitzgerald. Situado na Era do Jazz em Long Island."
            }),
            new("To Kill a Mockingbird", "Lee", "Fiction", 1960, "en", new() {
                ["en"] = "To Kill a Mockingbird is a novel by the American author Harper Lee. It was published in 1960 and was instantly successful.",
                ["es"] = "Matar a un ruiseñor es una novela de la escritora estadounidense Harper Lee. Publicada en 1960, tuvo un éxito instantáneo.",
                ["fr"] = "Ne tirez pas sur l'oiseau moqueur est un roman de la romancière américaine Harper Lee, publié en 1960.",
                ["de"] = "Wer die Nachtigall stört ist ein Roman der US-amerikanischen Schriftstellerin Harper Lee aus dem Jahr 1960.",
                ["pt"] = "O Sol é para Todos é um romance da escritora norte-americana Harper Lee, publicado em 1960."
            }),
            new("1984", "Orwell", "SciFi", 1949, "en", new() {
                ["en"] = "Nineteen Eighty-Four is a dystopian social science fiction novel by English novelist George Orwell. Published in 1949.",
                ["es"] = "1984 es una novela distópica de ficción social y política escrita por George Orwell. Publicada en 1949.",
                ["fr"] = "1984 est un roman dystopique de George Orwell publié en 1949.",
                ["de"] = "1984 ist ein dystopischer Roman von George Orwell, erschienen 1949.",
                ["pt"] = "1984 é um romance distópico do escritor inglês George Orwell, publicado em 1949."
            }),
            new("Pride and Prejudice", "Austen", "Romance", 1813, "en", new() {
                ["en"] = "Pride and Prejudice is an 1813 novel of manners by Jane Austen. It follows the character development of Elizabeth Bennet.",
                ["es"] = "Orgullo y prejuicio es una novela de 1813 de Jane Austen. Sigue el desarrollo del carácter de Elizabeth Bennet.",
                ["fr"] = "Orgueil et Préjugés est un roman de Jane Austen paru en 1813.",
                ["de"] = "Stolz und Vorurteil ist ein Roman von Jane Austen aus dem Jahr 1813.",
                ["pt"] = "Orgulho e Preconceito é um romance de 1813 de Jane Austen."
            }),
            new("Harry Potter and the Sorcerer's Stone", "Rowling", "Fantasy", 1997, "en", new() {
                ["en"] = "Harry Potter and the Philosopher's Stone is a fantasy novel written by British author J. K. Rowling.",
                ["es"] = "Harry Potter y la piedra filosofal es el primer libro de la serie Harry Potter escrito por J.K. Rowling.",
                ["fr"] = "Harry Potter à l'école des sorciers est le premier roman de la série Harry Potter.",
                ["de"] = "Harry Potter und der Stein der Weisen ist der erste Band der Harry-Potter-Reihe.",
                ["pt"] = "Harry Potter e a Pedra Filosofal é o primeiro romance da série Harry Potter."
            }),
            new("The Lord of the Rings", "Tolkien", "Fantasy", 1954, "en", new() {
                ["en"] = "The Lord of the Rings is an epic high-fantasy novel by English author J. R. R. Tolkien.",
                ["es"] = "El Señor de los Anillos es una novela de fantasía épica del escritor inglés J. R. R. Tolkien.",
                ["fr"] = "Le Seigneur des anneaux est un roman de haute fantasy de J. R. R. Tolkien.",
                ["de"] = "Der Herr der Ringe ist ein High-Fantasy-Roman von J. R. R. Tolkien.",
                ["pt"] = "O Senhor dos Anéis é um romance de alta fantasia de J. R. R. Tolkien."
            }),
            new("Dune", "Herbert", "SciFi", 1965, "en", new() {
                ["en"] = "Dune is a 1965 epic science fiction novel by American author Frank Herbert.",
                ["es"] = "Dune es una novela de ciencia ficción épica de 1965 del autor estadounidense Frank Herbert.",
                ["fr"] = "Dune est un roman de science-fiction de Frank Herbert publié en 1965.",
                ["de"] = "Der Wüstenplanet ist ein Science-Fiction-Roman von Frank Herbert aus dem Jahr 1965.",
                ["pt"] = "Duna é um romance de ficção científica de 1965 do autor americano Frank Herbert."
            }),
             new("Foundation", "Asimov", "SciFi", 1951, "en", new() {
                ["en"] = "Foundation is a science fiction novel by American writer Isaac Asimov.",
                ["es"] = "Fundación es una novela de ciencia ficción del escritor estadounidense Isaac Asimov.",
                ["fr"] = "Fondation est un roman de science-fiction d'Isaac Asimov.",
                ["de"] = "Foundation ist ein Science-Fiction-Roman von Isaac Asimov.",
                ["pt"] = "Fundação é um romance de ficção científica do escritor americano Isaac Asimov."
            }),
            new("Don Quijote", "Cervantes", "Classic", 1605, "es", new() {
                ["en"] = "Don Quixote is a Spanish novel by Miguel de Cervantes. Published in 1605.",
                ["es"] = "Don Quijote de la Mancha es una novela escrita por el español Miguel de Cervantes Saavedra.",
                ["fr"] = "Don Quichotte est un roman écrit par Miguel de Cervantes.",
                ["de"] = "Don Quijote ist ein Roman von Miguel de Cervantes.",
                ["pt"] = "Dom Quixote é um romance escrito pelo espanhol Miguel de Cervantes."
            }),
            new("Cien años de soledad", "Marquez", "Fiction", 1967, "es", new() {
                ["en"] = "One Hundred Years of Solitude is a landmark 1967 novel by Colombian author Gabriel García Márquez.",
                ["es"] = "Cien años de soledad es una novela del escritor colombiano Gabriel García Márquez.",
                ["fr"] = "Cent ans de solitude est un roman de Gabriel García Márquez.",
                ["de"] = "Hundert Jahre Einsamkeit ist ein Roman von Gabriel García Márquez.",
                ["pt"] = "Cem Anos de Solidão é um romance do escritor colombiano Gabriel García Márquez."
            }),
            new("The Shining", "King", "Horror", 1977, "en", new() {
                ["en"] = "The Shining is a 1977 gothic horror novel by American author Stephen King.",
                ["es"] = "El resplandor es una novela de terror gótico de 1977 del autor estadounidense Stephen King.",
                ["fr"] = "Shining est un roman d'horreur de Stephen King publié en 1977.",
                ["de"] = "Shining ist ein Horrorroman von Stephen King aus dem Jahr 1977.",
                ["pt"] = "O Iluminado é um romance de terror gótico de 1977 do autor americano Stephen King."
            })
        };

        return tenantId.ToLowerInvariant() switch
        {
            "acme" => allBooks.Where(b => b.Category is "SciFi" or "Fantasy"),
            "contoso" => allBooks.Where(b => b.Category is "Classic" or "Horror" or "Fiction" or "History" or "Romance"),
            _ => allBooks
        };
    }

    static async Task SeedBooksAsync(
        IDocumentStore store,
        IMessageBus bus,
        Dictionary<string, PublisherAdded> publishers,
        Dictionary<string, AuthorAdded> authors,
        Dictionary<string, CategoryAdded> categories,
        ILogger logger,
        string tenantId,
        IEnumerable<BookSeedData> books)
    {
        Log.Seeding.SeedingBooks(logger);

        await using var bookSession = store.LightweightSession(tenantId);
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

            var translations = book.Descriptions.ToDictionary(
                k => k.Key,
                v => new BookTranslation(v.Value));

            var bookAdded = new BookAdded(
                bookId,
                book.Title,
                null, // ISBN
                book.Language,
                translations,
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
                bookCommands.Add(new UpdateBookCover(bookId, coverImage, "image/png", null, tenantId));
            }
            catch (Exception ex)
            {
                Log.Seeding.FailedToGenerateCover(logger, ex, book.Title);
            }
        }

        await bookSession.SaveChangesAsync();

        foreach (var cmd in bookCommands)
        {
            await bus.InvokeAsync(cmd);
        }

        Log.Seeding.SeededBooks(logger, books.Count());
    }

    public async Task SeedSalesAsync(string tenantId)
    {
        Log.Seeding.StartingSalesSeeding(logger);

        await using var session = store.LightweightSession(tenantId);

        var books = await session.Query<BookSearchProjection>()
            .Where(b => !b.Deleted)
            .Take(5)
            .ToListAsync();

        Log.Seeding.FoundBooksForSalesSeeding(logger, books.Count);

        if (books.Count == 0)
        {
            Log.Seeding.NoBooksFoundForSalesSeeding(logger);
            return;
        }

        // Check if any books already have sales (idempotency for retry scenarios)
        var booksWithSales = await session.Query<BookSearchProjection>()
            .Where(b => b.Sales.Count > 0)
            .AnyAsync();

        if (booksWithSales)
        {
            Log.Seeding.SalesAlreadySeeded(logger);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        try
        {
            // Active sale (started 1 day ago, ends in 7 days)
            if (books.Count > 0)
            {
                var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(books[0].Id);
                if (aggregate != null)
                {
                    var saleEvent = aggregate.ScheduleSale(25m, now.AddDays(-1), now.AddDays(7));
                    _ = session.Events.Append(books[0].Id, saleEvent.Value);
                    Log.Seeding.ScheduledSale(logger, 25m, books[0].Id, books[0].Title);
                }
            }

            // Active sale (started 12 hours ago, ends in 5 days)
            if (books.Count > 1)
            {
                var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(books[1].Id);
                if (aggregate != null)
                {
                    var saleEvent = aggregate.ScheduleSale(15m, now.AddHours(-12), now.AddDays(5));
                    _ = session.Events.Append(books[1].Id, saleEvent.Value);
                    Log.Seeding.ScheduledSale(logger, 15m, books[1].Id, books[1].Title);
                }
            }

            // Active sale (started 1 hour ago, ends in 3 days)
            if (books.Count > 2)
            {
                var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(books[2].Id);
                if (aggregate != null)
                {
                    var saleEvent = aggregate.ScheduleSale(30m, now.AddHours(-1), now.AddDays(3));
                    _ = session.Events.Append(books[2].Id, saleEvent.Value);
                    Log.Seeding.ScheduledSale(logger, 30m, books[2].Id, books[2].Title);
                }
            }

            // Upcoming sale (starts in 1 hour, ends in 2 days)
            if (books.Count > 3)
            {
                var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(books[3].Id);
                if (aggregate != null)
                {
                    var saleEvent = aggregate.ScheduleSale(20m, now.AddHours(1), now.AddDays(2));
                    _ = session.Events.Append(books[3].Id, saleEvent.Value);
                    Log.Seeding.ScheduledSale(logger, 20m, books[3].Id, books[3].Title);
                }
            }

            // Upcoming sale (starts in 6 hours, ends in 4 days)
            if (books.Count > 4)
            {
                var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(books[4].Id);
                if (aggregate != null)
                {
                    var saleEvent = aggregate.ScheduleSale(10m, now.AddHours(6), now.AddDays(4));
                    _ = session.Events.Append(books[4].Id, saleEvent.Value);
                    Log.Seeding.ScheduledSale(logger, 10m, books[4].Id, books[4].Title);
                }
            }

            await session.SaveChangesAsync();
            Log.Seeding.SalesSeedingCompleted(logger);
        }
        catch (Exception ex)
        {
            Log.Seeding.ErrorSeedingSales(logger, ex);
            throw;
        }
    }
}
