using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
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

    static Dictionary<string, Guid> SeedPublishers(IDocumentSession session)
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
            _ = session.Events.StartStream<PublisherAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    static Dictionary<string, Guid> SeedAuthors(IDocumentSession session)
    {
        var authors = new Dictionary<string, (Guid Id, string Name, Dictionary<string, string> Bios)>
        {
            ["Fitzgerald"] = (Guid.CreateVersion7(), "F. Scott Fitzgerald", new()
            {
                ["en"] = "American novelist and short story writer",
                ["pt"] = "Romancista e contista americano",
                ["es"] = "Novelista y cuentista estadounidense",
                ["fr"] = "Romancier et nouvelliste américain",
                ["de"] = "Amerikanischer Romanautor und Kurzgeschichtenschreiber"
            }),
            ["Lee"] = (Guid.CreateVersion7(), "Harper Lee", new()
            {
                ["en"] = "American novelist known for To Kill a Mockingbird",
                ["pt"] = "Romancista americana conhecida por O Sol é Para Todos",
                ["es"] = "Novelista estadounidense conocida por Matar un ruiseñor",
                ["fr"] = "Romancière américaine connue pour Ne tirez pas sur l'oiseau moqueur",
                ["de"] = "Amerikanische Romanautorin, bekannt für Wer die Nachtigall stört"
            }),
            ["Orwell"] = (Guid.CreateVersion7(), "George Orwell", new()
            {
                ["en"] = "English novelist, essayist, and critic",
                ["pt"] = "Romancista, ensaísta e crítico inglês",
                ["es"] = "Novelista, ensayista y crítico inglés",
                ["fr"] = "Romancier, essayiste et critique anglais",
                ["de"] = "Englischer Romanautor, Essayist und Kritiker"
            }),
            ["Austen"] = (Guid.CreateVersion7(), "Jane Austen", new()
            {
                ["en"] = "English novelist known for her romantic fiction",
                ["pt"] = "Romancista inglesa conhecida por sua ficção romântica",
                ["es"] = "Novelista inglesa conocida por su ficción romántica",
                ["fr"] = "Romancière anglaise connue pour sa fiction romantique",
                ["de"] = "Englische Romanautorin, bekannt für ihre romantischen Romane"
            }),
            ["Rowling"] = (Guid.CreateVersion7(), "J.K. Rowling", new()
            {
                ["en"] = "British author, creator of Harry Potter",
                ["pt"] = "Autora britânica, criadora de Harry Potter",
                ["es"] = "Autora británica, creadora de Harry Potter",
                ["fr"] = "Auteure britannique, créatrice de Harry Potter",
                ["de"] = "Britische Autorin, Schöpferin von Harry Potter"
            }),
            ["Tolkien"] = (Guid.CreateVersion7(), "J.R.R. Tolkien", new()
            {
                ["en"] = "English writer and philologist, author of The Lord of the Rings",
                ["pt"] = "Escritor e filólogo inglês, autor de O Senhor dos Anéis",
                ["es"] = "Escritor y filólogo inglés, autor de El Señor de los Anillos",
                ["fr"] = "Écrivain et philologue anglais, auteur du Seigneur des Anneaux",
                ["de"] = "Englischer Schriftsteller und Philologe, Autor von Der Herr der Ringe"
            }),
            ["Hemingway"] = (Guid.CreateVersion7(), "Ernest Hemingway", new()
            {
                ["en"] = "American novelist and short story writer",
                ["pt"] = "Romancista e contista americano",
                ["es"] = "Novelista y cuentista estadounidense",
                ["fr"] = "Romancier et nouvelliste américain",
                ["de"] = "Amerikanischer Romanautor und Kurzgeschichtenschreiber"
            }),
            ["Christie"] = (Guid.CreateVersion7(), "Agatha Christie", new()
            {
                ["en"] = "English writer known for detective novels",
                ["pt"] = "Escritora inglesa conhecida por romances policiais",
                ["es"] = "Escritora inglesa conocida por novelas de detectives",
                ["fr"] = "Écrivaine anglaise connue pour ses romans policiers",
                ["de"] = "Englische Schriftstellerin, bekannt für Kriminalromane"
            }),
            ["Martin"] = (Guid.CreateVersion7(), "George R.R. Martin", new()
            {
                ["en"] = "American novelist and short story writer, author of A Song of Ice and Fire",
                ["pt"] = "Romancista e contista americano, autor de As Crônicas de Gelo e Fogo",
                ["es"] = "Novelista y cuentista estadounidense, autor de Canción de Hielo y Fuego",
                ["fr"] = "Romancier et nouvelliste américain, auteur du Trône de Fer",
                ["de"] = "Amerikanischer Romanautor und Kurzgeschichtenschreiber, Autor von Das Lied von Eis und Feuer"
            })
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, (id, name, bios)) in authors)
        {
            // Create AuthorTranslation dictionary with all language variants
            var biographies = bios.ToDictionary(
                kvp => kvp.Key,
                kvp => new AuthorTranslation(kvp.Value));

            var @event = AuthorAggregate.Create(id, name, biographies);
            _ = session.Events.StartStream<AuthorAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    static Dictionary<string, Guid> SeedCategories(IDocumentSession session)
    {
        var categories = new Dictionary<string, (Guid Id, Dictionary<string, string> Names)>
        {
            ["Fiction"] = (Guid.CreateVersion7(), new() { ["en"] = "Fiction", ["pt"] = "Ficção", ["es"] = "Ficción", ["fr"] = "Fiction", ["de"] = "Belletristik" }),
            ["Classic"] = (Guid.CreateVersion7(), new() { ["en"] = "Classic Literature", ["pt"] = "Literatura Clássica", ["es"] = "Literatura Clásica", ["fr"] = "Littérature Classique", ["de"] = "Klassische Literatur" }),
            ["Fantasy"] = (Guid.CreateVersion7(), new() { ["en"] = "Fantasy", ["pt"] = "Fantasia", ["es"] = "Fantasía", ["fr"] = "Fantaisie", ["de"] = "Fantasy" }),
            ["Mystery"] = (Guid.CreateVersion7(), new() { ["en"] = "Mystery", ["pt"] = "Mistério", ["es"] = "Misterio", ["fr"] = "Mystère", ["de"] = "Krimi" }),
            ["SciFi"] = (Guid.CreateVersion7(), new() { ["en"] = "Science Fiction", ["pt"] = "Ficção Científica", ["es"] = "Ciencia Ficción", ["fr"] = "Science-Fiction", ["de"] = "Science-Fiction" }),
            ["Romance"] = (Guid.CreateVersion7(), new() { ["en"] = "Romance", ["pt"] = "Romance", ["es"] = "Romance", ["fr"] = "Romance", ["de"] = "Liebesroman" })
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, (id, names)) in categories)
        {
            // Create CategoryTranslation dictionary with all language variants
            var translations = names.ToDictionary(
                kvp => kvp.Key,
                kvp => new CategoryTranslation(kvp.Value, null));

            var @event = CategoryAggregate.Create(id, translations);
            _ = session.Events.StartStream<CategoryAggregate>(id, @event);
            result[key] = id;
        }

        return result;
    }

    static void SeedBooks(
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
                Language = "en",
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "A novel set in the Jazz Age that explores themes of decadence, idealism, resistance to change, social upheaval, and excess.",
                    ["pt"] = "Um romance ambientado na Era do Jazz que explora temas de decadência, idealismo, resistência à mudança, agitação social e excesso.",
                    ["es"] = "Una novela ambientada en la Era del Jazz que explora temas de decadencia, idealismo, resistencia al cambio, agitación social y exceso.",
                    ["fr"] = "Un roman situé dans l'ère du Jazz qui explore les thèmes de la décadence, de l'idéalisme, de la résistance au changement, des bouleversements sociaux et de l'excès.",
                    ["de"] = "Ein Roman aus der Jazz-Ära, der Themen wie Dekadenz, Idealismus, Widerstand gegen Veränderung, soziale Umwälzung und Exzess erforscht."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "A gripping, heart-wrenching, and wholly remarkable tale of coming-of-age in a South poisoned by virulent prejudice.",
                    ["pt"] = "Uma história comovente, emocionante e notável sobre amadurecimento em um Sul envenenado por preconceito virulento.",
                    ["es"] = "Una historia apasionante, desgarradora y completamente notable sobre la mayoría de edad en un Sur envenenado por prejuicios virulentos.",
                    ["fr"] = "Un récit captivant, déchirant et tout à fait remarquable sur le passage à l'âge adulte dans un Sud empoisonné par des préjugés virulents.",
                    ["de"] = "Eine fesselnde, herzzerreißende und bemerkenswerte Geschichte über das Erwachsenwerden in einem von virulenten Vorurteilen vergifteten Süden."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "A dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.",
                    ["pt"] = "Um romance distópico de ficção científica social e conto de advertência sobre os perigos do totalitarismo.",
                    ["es"] = "Una novela distópica de ciencia ficción social y cuento de advertencia sobre los peligros del totalitarismo.",
                    ["fr"] = "Un roman dystopique de science-fiction sociale et un conte d'avertissement sur les dangers du totalitarisme.",
                    ["de"] = "Ein dystopischer sozialwissenschaftlicher Science-Fiction-Roman und eine Warnung vor den Gefahren des Totalitarismus."
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
                Translations = new Dictionary<string, string>
                {
                    ["pt"] = "Um romance distópico de ficção científica social e conto de advertência sobre os perigos do totalitarismo.",
                    ["en"] = "Portuguese edition of the dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.",
                    ["es"] = "Edición portuguesa de la novela distópica de ciencia ficción social y cuento de advertencia sobre los peligros del totalitarismo.",
                    ["fr"] = "Édition portugaise du roman dystopique de science-fiction sociale et un conte d'avertissement sur les dangers du totalitarisme.",
                    ["de"] = "Portugiesische Ausgabe des dystopischen sozialwissenschaftlichen Science-Fiction-Romans und eine Warnung vor den Gefahren des Totalitarismus."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "A romantic novel of manners that follows the character development of Elizabeth Bennet.",
                    ["pt"] = "Um romance de costumes que acompanha o desenvolvimento do personagem de Elizabeth Bennet.",
                    ["es"] = "Una novela romántica de costumbres que sigue el desarrollo del personaje de Elizabeth Bennet.",
                    ["fr"] = "Un roman romantique de mœurs qui suit le développement du personnage d'Elizabeth Bennet.",
                    ["de"] = "Ein romantischer Sittenroman, der die Charakterentwicklung von Elizabeth Bennet verfolgt."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "The first novel in the Harry Potter series, following a young wizard's journey at Hogwarts School of Witchcraft and Wizardry.",
                    ["pt"] = "O primeiro romance da série Harry Potter, seguindo a jornada de um jovem bruxo na Escola de Magia e Bruxaria de Hogwarts.",
                    ["es"] = "La primera novela de la serie Harry Potter, siguiendo el viaje de un joven mago en la Escuela de Magia y Hechicería de Hogwarts.",
                    ["fr"] = "Le premier roman de la série Harry Potter, suivant le voyage d'un jeune sorcier à l'école de sorcellerie de Poudlard.",
                    ["de"] = "Der erste Roman der Harry-Potter-Reihe, der die Reise eines jungen Zauberers an der Hogwarts-Schule für Hexerei und Zauberei verfolgt."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "An epic high-fantasy novel following the quest to destroy the One Ring.",
                    ["pt"] = "Um épico romance de alta fantasia seguindo a busca para destruir o Um Anel.",
                    ["es"] = "Una épica novela de alta fantasía que sigue la búsqueda para destruir el Anillo Único.",
                    ["fr"] = "Un roman épique de haute fantasy suivant la quête pour détruire l'Anneau Unique.",
                    ["de"] = "Ein epischer High-Fantasy-Roman, der die Quest zur Zerstörung des Einen Rings verfolgt."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "The story of an aging Cuban fisherman who struggles with a giant marlin far out in the Gulf Stream.",
                    ["pt"] = "A história de um pescador cubano idoso que luta com um marlim gigante no Golfo do México.",
                    ["es"] = "La historia de un pescador cubano envejecido que lucha con un marlín gigante en el Golfo de México.",
                    ["fr"] = "L'histoire d'un pêcheur cubain vieillissant qui lutte avec un marlin géant dans le Gulf Stream.",
                    ["de"] = "Die Geschichte eines alternden kubanischen Fischers, der mit einem riesigen Marlin weit draußen im Golfstrom kämpft."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "A detective novel featuring Hercule Poirot investigating a murder on the famous train.",
                    ["pt"] = "Um romance policial com Hercule Poirot investigando um assassinato no famoso trem.",
                    ["es"] = "Una novela de detectives con Hercule Poirot investigando un asesinato en el famoso tren.",
                    ["fr"] = "Un roman policier mettant en vedette Hercule Poirot enquêtant sur un meurtre dans le célèbre train.",
                    ["de"] = "Ein Kriminalroman mit Hercule Poirot, der einen Mord im berühmten Zug untersucht."
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
                Translations = new Dictionary<string, string>
                {
                    ["en"] = "The highly anticipated sixth novel in the epic fantasy series A Song of Ice and Fire.",
                    ["pt"] = "O aguardado sexto romance da série épica de fantasia As Crônicas de Gelo e Fogo.",
                    ["es"] = "La esperada sexta novela de la serie de fantasía épica Canción de Hielo y Fuego.",
                    ["fr"] = "Le sixième roman très attendu de la série de fantasy épique Le Trône de Fer.",
                    ["de"] = "Der mit Spannung erwartete sechste Roman der epischen Fantasy-Serie Das Lied von Eis und Feuer."
                },
                PublicationDate = new PartialDate(2026, 3),
                Publisher = "Penguin",
                Authors = new[] { "Martin" },
                Categories = new[] { "Fantasy", "Fiction" }
            }
        };

        foreach (var book in books)
        {
            // Create BookTranslation dictionary with all language variants
            var descriptions = book.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => new BookTranslation(kvp.Value));

            var bookId = Guid.CreateVersion7();
            var @event = BookAggregate.Create(
                bookId,
                book.Title,
                book.Isbn,
                book.Language,
                descriptions,
                book.PublicationDate,
                publisherIds[book.Publisher],
                [.. book.Authors.Select(a => authorIds[a])],
                [.. book.Categories.Select(c => categoryIds[c])]
            );

            _ = session.Events.StartStream<BookAggregate>(bookId, @event);
        }
    }
}
