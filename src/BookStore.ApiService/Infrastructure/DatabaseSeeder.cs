using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Projections;
using BookStore.ApiService.Services;
using BookStore.Shared.Models;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Seeds the database with initial data.
/// </summary>
public class DatabaseSeeder(IDocumentStore store, IMessageBus bus, ILogger<DatabaseSeeder> logger)
{

    public async Task SeedAsync()
    {
        await using var session = store.LightweightSession();

        // Check if already seeded
        var existingBooks = await session.Query<BookSearchProjection>().AnyAsync();
        if (existingBooks)
        {
            Log.Seeding.DatabaseAlreadySeeded(logger);
            return; // Already seeded
        }

        Log.Seeding.StartingDatabaseSeeding(logger);

        // Seed in dependency order: Publishers → Authors → Categories → Books
        var publisherIds = SeedPublishers(session, logger);
        var authorIds = SeedAuthors(session, logger);
        var categoryIds = SeedCategories(session, logger);

        await session.SaveChangesAsync();

        await SeedBooksAsync(store, bus, publisherIds, authorIds, categoryIds, logger);

        Log.Seeding.DatabaseSeedingCompleted(logger);
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

    static async Task SeedBooksAsync(
        IDocumentStore store,
        IMessageBus bus,
        Dictionary<string, PublisherAdded> publishers,
        Dictionary<string, AuthorAdded> authors,
        Dictionary<string, CategoryAdded> categories,
        ILogger logger)
    {
        Log.Seeding.SeedingBooks(logger);
        var books = new[]
        {
            // English Books
            new { 
                Title = "The Great Gatsby", 
                Author = "Fitzgerald", 
                Category = "Fiction", 
                Year = 1925, 
                Language = "en",
                Description = "The Great Gatsby is a 1925 novel by American writer F. Scott Fitzgerald. Set in the Jazz Age on Long Island, near New York City, the novel depicts first-person narrator Nick Carraway's interactions with mysterious millionaire Jay Gatsby and Gatsby's obsession to reunite with his former lover, Daisy Buchanan."
            },
            new { 
                Title = "To Kill a Mockingbird", 
                Author = "Lee", 
                Category = "Fiction", 
                Year = 1960, 
                Language = "en",
                Description = "To Kill a Mockingbird is a novel by the American author Harper Lee. It was published in 1960 and was instantly successful. In the United States, it is widely read in high schools and middle schools. To Kill a Mockingbird has become a classic of modern American literature."
            },
            new { 
                Title = "1984", 
                Author = "Orwell", 
                Category = "SciFi", 
                Year = 1949, 
                Language = "en",
                Description = "Nineteen Eighty-Four (also stylised as 1984) is a dystopian social science fiction novel and cautionary tale by English novelist George Orwell. It was published on 8 June 1949 by Secker & Warburg as Orwell's ninth and final book completed in his lifetime."
            },
            new { 
                Title = "Pride and Prejudice", 
                Author = "Austen", 
                Category = "Romance", 
                Year = 1813, 
                Language = "en",
                Description = "Pride and Prejudice is an 1813 novel of manners by Jane Austen. The novel follows the character development of Elizabeth Bennet, the dynamic protagonist of the book who learns about the repercussions of hasty judgments and comes to appreciate the difference between superficial goodness and actual goodness."
            },
            new { 
                Title = "Harry Potter and the Sorcerer's Stone", 
                Author = "Rowling", 
                Category = "Fantasy", 
                Year = 1997, 
                Language = "en",
                Description = "Harry Potter and the Philosopher's Stone is a fantasy novel written by British author J. K. Rowling. The first novel in the Harry Potter series and Rowling's debut novel, it follows Harry Potter, a young wizard who discovers his magical heritage on his eleventh birthday, when he receives a letter of acceptance to Hogwarts School of Witchcraft and Wizardry."
            },
            new { 
                Title = "The Lord of the Rings", 
                Author = "Tolkien", 
                Category = "Fantasy", 
                Year = 1954, 
                Language = "en",
                Description = "The Lord of the Rings is an epic high-fantasy novel by English author and scholar J. R. R. Tolkien. Set in Middle-earth, intended to be Earth at some distant period of the past, the story began as a sequel to Tolkien's 1937 children's book The Hobbit, but eventually developed into a much larger work."
            },
            new { 
                Title = "The Old Man and the Sea", 
                Author = "Hemingway", 
                Category = "Fiction", 
                Year = 1952, 
                Language = "en",
                Description = "The Old Man and the Sea is a short novel written by the American author Ernest Hemingway in 1951 in Cayo Blanco (Cuba), and published in 1952. It was the last major work of fiction by Hemingway that was published during his lifetime."
            },
            new { 
                Title = "Murder on the Orient Express", 
                Author = "Christie", 
                Category = "Mystery", 
                Year = 1934, 
                Language = "en",
                Description = "Murder on the Orient Express is a detective novel by English writer Agatha Christie featuring the Belgian detective Hercule Poirot. It was first published in the United Kingdom by the Collins Crime Club on 1 January 1934."
            },
            
            // Spanish Books (Translations + Originals)
            new { 
                Title = "El Gran Gatsby", 
                Author = "Fitzgerald", 
                Category = "Fiction", 
                Year = 1925, 
                Language = "es",
                Description = "El gran Gatsby es una novela de 1925 escrita por el autor estadounidense F. Scott Fitzgerald que sigue a un grupo de personajes que viven en la ciudad ficticia de West Egg en la próspera Long Island en el verano de 1922."
            },
            new { 
                Title = "Matar a un ruiseñor", 
                Author = "Lee", 
                Category = "Fiction", 
                Year = 1960, 
                Language = "es",
                Description = "Matar a un ruiseñor es una novela de 1960 de la escritora estadounidense Harper Lee. Su publicación fue un éxito instantáneo, ganando el premio Pulitzer y pasando a convertirse en un clásico de la literatura estadounidense moderna."
            },
            new { 
                Title = "El Aleph", 
                Author = "Borges", 
                Category = "Fiction", 
                Year = 1949, 
                Language = "es",
                Description = "El Aleph es uno de los cuentos más representativos del escritor argentino Jorge Luis Borges. Publicado en la revista Sur en 1945, fue incluido en el libro homónimo de 1949."
            },
            new { 
                Title = "Cien años de soledad", 
                Author = "Marquez", 
                Category = "Fiction", 
                Year = 1967, 
                Language = "es",
                Description = "Cien años de soledad es una novela del escritor colombiano Gabriel García Márquez, ganador del Premio Nobel de Literatura en 1982. Es considerada una obra maestra de la literatura hispanoamericana y universal."
            },
            new { 
                Title = "Don Quijote", 
                Author = "Cervantes", 
                Category = "Classic", 
                Year = 1605, 
                Language = "es",
                Description = "Don Quijote de la Mancha es una novela escrita por el español Miguel de Cervantes Saavedra. Publicada su primera parte con el título de El ingenioso hidalgo don Quijote de la Mancha a comienzos de 1605, es la obra más destacada de la literatura española."
            },
            
            // French Books (Translations + Originals)
            new { 
                Title = "Orgueil et Préjugés", 
                Author = "Austen", 
                Category = "Romance", 
                Year = 1813, 
                Language = "fr",
                Description = "Orgueil et Préjugés est un roman de la femme de lettres anglaise Jane Austen paru en 1813. Il est considéré comme l'une de ses œuvres les plus significatives et c'est aussi la plus connue du grand public."
            },
            new { 
                Title = "Harry Potter à l'école des sorciers", 
                Author = "Rowling", 
                Category = "Fantasy", 
                Year = 1997, 
                Language = "fr",
                Description = "Harry Potter à l'école des sorciers est le premier roman de la série littéraire centrée sur le personnage de Harry Potter, créé par J. K. Rowling."
            },
            new { 
                Title = "Les Misérables", 
                Author = "Hugo", 
                Category = "Classic", 
                Year = 1862, 
                Language = "fr",
                Description = "Les Misérables est un roman de Victor Hugo paru en 1862. Il a donné lieu à de nombreuses adaptations, au cinéma et sur de nombreux autres supports."
            },
            new { 
                Title = "L'Étranger", 
                Author = "Camus", 
                Category = "Fiction", 
                Year = 1942, 
                Language = "fr",
                Description = "L'Étranger est le premier roman d'Albert Camus, paru en 1942. Il prend place dans la tétralogie que Camus appellera « le cycle de l'absurde » qui décrit les fondements de la philosophie camusienne : l'absurde."
            },
            new { 
                Title = "Le Petit Prince", 
                Author = "SaintExupery", 
                Category = "Fiction", 
                Year = 1943, 
                Language = "fr",
                Description = "Le Petit Prince est une œuvre de langue française, la plus connue d'Antoine de Saint-Exupéry. Publié en 1943 à New York simultanément avec sa traduction anglaise, c'est un conte poétique et philosophique sous l'apparence d'un conte pour enfants."
            },
            
             // German Books (Translations + Originals)
            new { 
                Title = "Der alte Mann und das Meer", 
                Author = "Hemingway", 
                Category = "Fiction", 
                Year = 1952, 
                Language = "de",
                Description = "Der alte Mann und das Meer ist eine Novelle von Ernest Hemingway. Sie entstand 1951 auf Kuba und wurde 1952 veröffentlicht. Es war das letzte Werk Hemingways, das zu seinen Lebzeiten erschien."
            },
            new { 
                Title = "Mord im Orient-Express", 
                Author = "Christie", 
                Category = "Mystery", 
                Year = 1934, 
                Language = "de",
                Description = "Mord im Orient-Express ist ein Kriminalroman von Agatha Christie. Er erschien in Großbritannien 1934 und in den USA im selben Jahr unter dem Titel Murder in the Calais Coach."
            },
            new { 
                Title = "Faust", 
                Author = "Goethe", 
                Category = "Classic", 
                Year = 1808, 
                Language = "de",
                Description = "Faust. Eine Tragödie ist ein Drama von Johann Wolfgang von Goethe. Es gilt als das bedeutendste und meistzitierte Werk der deutschsprachigen Literatur."
            },
            new { 
                Title = "Der Prozess", 
                Author = "Kafka", 
                Category = "Fiction", 
                Year = 1925, 
                Language = "de",
                Description = "Der Process (auch Der Prozess) ist neben Das Schloss und Der Verschollene einer von drei unvollendeten Romanen von Franz Kafka."
            },

            // Portuguese Books (Translations + Originals)
            new { 
                Title = "A Guerra dos Tronos", 
                Author = "Martin", 
                Category = "Fantasy", 
                Year = 1996, 
                Language = "pt",
                Description = "A Game of Thrones é o primeiro livro da série de fantasia épica As Crônicas de Gelo e Fogo, escrita pelo norte-americano George R. R. Martin e publicada pela editora Bantam spectra."
            },
            new { 
                Title = "O Iluminado", 
                Author = "King", 
                Category = "Horror", 
                Year = 1977, 
                Language = "pt",
                Description = "The Shining é um romance de terror do autor americano Stephen King. Publicado em 1977, foi o terceiro livro publicado de King e seu primeiro best-seller de capa dura."
            },
            new { 
                Title = "Dom Casmurro", 
                Author = "Assis", 
                Category = "Classic", 
                Year = 1899, 
                Language = "pt",
                Description = "Dom Casmurro é um romance escrito por Machado de Assis, publicado em 1899. A obra é considerada uma das maiores da literatura brasileira e um dos melhores estudos sobre o ciúme na história da literatura mundial."
            },
            new { 
                Title = "Ensaio sobre a Cegueira", 
                Author = "Saramago", 
                Category = "Fiction", 
                Year = 1995, 
                Language = "pt",
                Description = "Ensaio sobre a Cegueira é um romance do escritor português José Saramago, publicado em 1995, e traduzido para diversas línguas."
            },
            
            // More English to fill up
            new { 
                Title = "The Winds of Winter", 
                Author = "Martin", 
                Category = "Fantasy", 
                Year = 2026, 
                Language = "en",
                Description = "The Winds of Winter is the forthcoming sixth novel in the epic fantasy series A Song of Ice and Fire by American writer George R. R. Martin."
            },
            new { 
                Title = "The Shining", 
                Author = "King", 
                Category = "Horror", 
                Year = 1977, 
                Language = "en",
                Description = "The Shining is a 1977 gothic horror novel by American author Stephen King. It is King's third published novel and first hardback bestseller."
            },
            new { 
                Title = "Dune", 
                Author = "Herbert", 
                Category = "SciFi", 
                Year = 1965, 
                Language = "en",
                Description = "Dune is a 1965 epic science fiction novel by American author Frank Herbert. It won the inaugural Nebula Award for Best Novel in 1965 and the 1966 Hugo Award."
            },
            new { 
                Title = "Foundation", 
                Author = "Asimov", 
                Category = "SciFi", 
                Year = 1951, 
                Language = "en",
                Description = "Foundation is a science fiction novel by American writer Isaac Asimov. It is the first published in his Foundation Trilogy (later expanded into the Foundation series)."
            },
            new { 
                Title = "2001: A Space Odyssey", 
                Author = "Clarke", 
                Category = "SciFi", 
                Year = 1968, 
                Language = "en",
                Description = "2001: A Space Odyssey is a 1968 science fiction novel by British writer Arthur C. Clarke. It was developed concurrently with Stanley Kubrick's film version and published after the release of the film."
            },
            new { 
                Title = "Do Androids Dream of Electric Sheep?", 
                Author = "Dick", 
                Category = "SciFi", 
                Year = 1968, 
                Language = "en",
                Description = "Do Androids Dream of Electric Sheep? is a 1968 science fiction novel by American writer Philip K. Dick. It is set in a post-apocalyptic San Francisco, where Earth's life has been greatly damaged by a nuclear global war."
            },
            new { 
                Title = "The Left Hand of Darkness", 
                Author = "LeGuin", 
                Category = "SciFi", 
                Year = 1969, 
                Language = "en",
                Description = "The Left Hand of Darkness is a science fiction novel by U.S. writer Ursula K. Le Guin. Published in 1969, it became immensely popular, and established Le Guin's status as a major author of science fiction."
            },
            new { 
                Title = "A Game of Thrones", 
                Author = "Martin", 
                Category = "Fantasy", 
                Year = 1996, 
                Language = "en",
                Description = "A Game of Thrones is the first novel in A Song of Ice and Fire, a series of fantasy novels by the American author George R. R. Martin. It was first published on August 1, 1996."
            },
            new { 
                Title = "A Clash of Kings", 
                Author = "Martin", 
                Category = "Fantasy", 
                Year = 1998, 
                Language = "en",
                Description = "A Clash of Kings is the second novel in A Song of Ice and Fire, an epic fantasy series by American author George R. R. Martin. It was first published on 16 November 1998 in the United Kingdom."
            },
            new { 
                Title = "A Storm of Swords", 
                Author = "Martin", 
                Category = "Fantasy", 
                Year = 2000, 
                Language = "en",
                Description = "A Storm of Swords is the third of seven planned novels in A Song of Ice and Fire, a fantasy series by American author George R. R. Martin. It was first published on August 8, 2000, in the United Kingdom."
            },
            new { 
                Title = "The Hobbit", 
                Author = "Tolkien", 
                Category = "Fantasy", 
                Year = 1937, 
                Language = "en",
                Description = "The Hobbit, or There and Back Again is a children's fantasy novel by the English author J. R. R. Tolkien. It was published on 21 September 1937 to wide critical acclaim, being nominated for the Carnegie Medal and awarded a prize from the New York Herald Tribune for best juvenile fiction."
            }
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
                new Dictionary<string, BookTranslation> { [book.Language] = new BookTranslation(book.Description) },
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
                Log.Seeding.FailedToGenerateCover(logger, ex, book.Title);
            }
        }

        await bookSession.SaveChangesAsync();

        foreach (var cmd in bookCommands)
        {
            await bus.InvokeAsync(cmd);
        }

        Log.Seeding.SeededBooks(logger, books.Length);
    }

    public async Task SeedSalesAsync()
    {
        Log.Seeding.StartingSalesSeeding(logger);

        await using var session = store.LightweightSession();

        // Get some books to add sales to
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
                    _ = session.Events.Append(books[0].Id, saleEvent);
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
                    _ = session.Events.Append(books[1].Id, saleEvent);
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
                    _ = session.Events.Append(books[2].Id, saleEvent);
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
                    _ = session.Events.Append(books[3].Id, saleEvent);
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
                    _ = session.Events.Append(books[4].Id, saleEvent);
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
