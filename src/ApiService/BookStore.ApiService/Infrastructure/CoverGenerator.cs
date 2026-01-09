using SkiaSharp;

namespace BookStore.ApiService.Infrastructure;

public static class CoverGenerator
{
    public static byte[] GenerateCover(string title, string author)
    {
        // 1. Pick background color based on title hash (deterministic) or just random for variety
        var bgColor = GetDeterministicColor(title);

        // 2. Create surface
        using var surface = SKSurface.Create(new SKImageInfo(400, 600));
        var canvas = surface.Canvas;

        // 3. Draw background
        canvas.Clear(bgColor);

        // 4. Draw Pattern (optional, to make it less flat)
        using (var paint = new SKPaint { Color = SKColors.White.WithAlpha(20), IsAntialias = true })
        {
            for (var i = 0; i < 400; i += 40)
            {
                canvas.DrawLine(i, 0, i, 600, paint);
            }
        }

        // 5. Draw Title
        using (var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 40))
        using (var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            var lines = WrapText(title, titleFont, 360);
            float y = 250 - (lines.Count * 22); // Center vertically around 250 (slightly higher now)

            foreach (var line in lines)
            {
                canvas.DrawText(line, 200, y, SKTextAlign.Center, titleFont, textPaint);
                y += 45;
            }
        }

        // 6. Draw Author
        using (var authorFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 24))
        using (var textPaint = new SKPaint { Color = SKColors.White.WithAlpha(200), IsAntialias = true })
        {
            canvas.DrawText("by", 200, 480, SKTextAlign.Center, authorFont, textPaint);

            // Wrap author name if too long
            var authorLines = WrapText(author, authorFont, 380);
            float authorY = 510;
            foreach (var line in authorLines)
            {
                canvas.DrawText(line, 200, authorY, SKTextAlign.Center, authorFont, textPaint);
                authorY += 30;
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        return data.ToArray();
    }

    static SKColor GetDeterministicColor(string text)
    {
        var hash = text.GetHashCode();
        var colors = new[]
        {
            SKColors.DarkSlateBlue, SKColors.Maroon, SKColors.Indigo,
            SKColors.DarkSlateGray, SKColors.MidnightBlue, SKColors.DeepPink,
            SKColors.DarkRed, SKColors.SaddleBrown, SKColors.DarkOliveGreen,
            SKColors.Teal, SKColors.Purple, SKColors.DarkCyan
        };
        return colors[int.Abs(hash) % colors.Length];
    }

    static List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = words[0];

        for (var i = 1; i < words.Length; i++)
        {
            var word = words[i];
            var width = font.MeasureText(currentLine + " " + word);
            if (width < maxWidth)
            {
                currentLine += " " + word;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        lines.Add(currentLine);
        return lines;
    }
}
