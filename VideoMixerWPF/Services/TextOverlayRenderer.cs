using System.IO;
using SkiaSharp;
using VideoMixer.Models;

namespace VideoMixer.Services;

public static class TextOverlayRenderer
{
    private const int Margin = 45;

    // ── Rendu final : PNG transparent posé sur la vidéo par FFmpeg ───────────
    public static string Render(VideoTemplate tpl, int videoW, int videoH, string tempDir)
    {
        var outPath = Path.Combine(tempDir, $"ov_{Guid.NewGuid():N}.png");

        using var surface = SKSurface.Create(
            new SKImageInfo(videoW, videoH, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);

        using var tf = GetTypeface(tpl.Style);
        DrawText(surface.Canvas, tpl, tf, videoW, videoH, 0, 0, tpl.FontSize);

        SavePng(surface, outPath);
        return outPath;
    }

    // ── Prévisualisation composite (frame vidéo + texte) ─────────────────────
    // Retourne aussi les métriques de la zone vidéo pour le drag
    public static (string path, int vx, int vy, int vw, int vh)
        RenderPreview(VideoTemplate tpl, int canvasW, int canvasH,
                      string tempDir, string? bgPath = null)
    {
        // Zone vidéo dans le canvas (letterbox Stretch="Uniform")
        int vx = 0, vy = 0, vw = canvasW, vh = canvasH;
        SKBitmap? bg = null;

        if (bgPath != null && File.Exists(bgPath))
        {
            bg = SKBitmap.Decode(bgPath);
            if (bg != null)
            {
                float s = Math.Min((float)canvasW / bg.Width, (float)canvasH / bg.Height);
                vw = (int)(bg.Width  * s);
                vh = (int)(bg.Height * s);
                vx = (canvasW - vw) / 2;
                vy = (canvasH - vh) / 2;
            }
        }

        // Police mise à l'échelle selon la largeur de la zone vidéo
        int scaledSize = Math.Max(6, (int)(tpl.FontSize * vw / 1080.0));

        using var surface = SKSurface.Create(
            new SKImageInfo(canvasW, canvasH, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var c = surface.Canvas;
        c.Clear(SKColors.Black);

        if (bg != null)
        {
            c.DrawBitmap(bg, new SKRect(vx, vy, vx + vw, vy + vh));
            bg.Dispose();
        }

        using var tf = GetTypeface(tpl.Style);
        DrawText(c, tpl, tf, vw, vh, vx, vy, scaledSize);

        var outPath = Path.Combine(tempDir, $"prev_{Guid.NewGuid():N}.jpg");
        using var img  = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 88);
        using var fs   = File.OpenWrite(outPath);
        data.SaveTo(fs);

        return (outPath, vx, vy, vw, vh);
    }

    // ── Dessin du texte (partagé Render / RenderPreview) ─────────────────────
    private static void DrawText(SKCanvas canvas, VideoTemplate tpl, SKTypeface tf,
                                  int areaW, int areaH, int ox, int oy, int fontSize)
    {
        var lines  = tpl.Text.Replace("\r\n", "\n").Split('\n');
        float lineH = fontSize * 1.25f;
        float totalH = lines.Length * lineH;
        float bw = Math.Max(1.5f, fontSize / 14f);
        var fgColor = ParseColor(tpl.Color);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;

            using var mp = new SKPaint { Typeface = tf, TextSize = fontSize, IsAntialias = true };
            float tw = mp.MeasureText(line);

            // Centre du bloc = (PositionX, PositionY) dans la zone vidéo
            float cx = (float)(areaW * tpl.PositionX);
            float cy = (float)(areaH * tpl.PositionY);

            float x = cx - tw / 2f;
            float y = cy - totalH / 2f + i * lineH + fontSize; // baseline

            // Clamp dans la zone
            x = Math.Clamp(x, Margin, Math.Max(Margin, areaW - tw - Margin));
            y = Math.Clamp(y, fontSize + 5, areaH - 5);

            // Offset canvas (letterbox)
            float px = x + ox;
            float py = y + oy;

            // Dessin du style
            switch (tpl.Style)
            {
                case TextStyle.Outline:
                    using (var op = new SKPaint
                    {
                        Typeface    = tf, TextSize    = fontSize,
                        IsAntialias = true, Style      = SKPaintStyle.Stroke,
                        StrokeWidth = bw * 2f, StrokeJoin = SKStrokeJoin.Round,
                        Color       = new SKColor(0, 0, 0, 220),
                    }) canvas.DrawText(line, px, py, op);
                    break;

                case TextStyle.Shadow:
                    using (var sp = new SKPaint
                    {
                        Typeface = tf, TextSize = fontSize, IsAntialias = true,
                        Color    = new SKColor(0, 0, 0, 160),
                    }) canvas.DrawText(line, px + fontSize * 0.06f, py + fontSize * 0.06f, sp);
                    break;
            }

            // Texte principal
            using var fp = new SKPaint { Typeface = tf, TextSize = fontSize, IsAntialias = true, Color = fgColor };
            canvas.DrawText(line, px, py, fp);
        }
    }

    // ── Poids de police selon le style ────────────────────────────────────────
    private static SKTypeface GetTypeface(TextStyle style)
    {
        var w = style switch
        {
            TextStyle.Bold    => SKFontStyleWeight.ExtraBold,
            TextStyle.Outline => SKFontStyleWeight.Bold,
            TextStyle.Shadow  => SKFontStyleWeight.SemiBold,
            _                 => SKFontStyleWeight.Normal,   // Normal = regular, pas de gras
        };
        return SKTypeface.FromFamilyName("Bahnschrift", w, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.FromFamilyName("Arial Rounded MT Bold", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
    }

    private static void SavePng(SKSurface surface, string path)
    {
        using var img  = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs   = File.OpenWrite(path);
        data.SaveTo(fs);
    }

    private static SKColor ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            return new SKColor(r, g, b);
        return SKColors.White;
    }
}
