using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class BLPImage
{
    private const int BLP_JPEG_HEADER_SIZE = 624;
    private const uint BLP_COMPRESSION_JPEG = 0;
    private const uint BLP_COMPRESSION_NONE = 1;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool HasMipmaps { get; private set; }
    public SKBitmap Image { get; private set; }

    private uint compression;
    private uint alphaBits;

    public BLPImage(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("BLP file not found.", path);

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        Parse(br);
    }

    private void Parse(BinaryReader br)
    {
        var magic = new string(br.ReadChars(4));
        if (magic != "BLP1") throw new NotSupportedException("Only BLP1 is supported.");

        compression = br.ReadUInt32();
        alphaBits = br.ReadUInt32();
        Width = (int)br.ReadUInt32();
        Height = (int)br.ReadUInt32();
        _ = br.ReadUInt32(); // type
        HasMipmaps = br.ReadUInt32() != 0;

        uint[] mipmapOffsets = Enumerable.Range(0, 16).Select(_ => br.ReadUInt32()).ToArray();
        uint[] mipmapSizes = Enumerable.Range(0, 16).Select(_ => br.ReadUInt32()).ToArray();

        if (compression == BLP_COMPRESSION_JPEG)
        {
            int headerSize = (int)br.ReadUInt32();
            var jpegHeader = br.ReadBytes(headerSize);

            br.BaseStream.Seek(mipmapOffsets[0], SeekOrigin.Begin);
            var jpegData = br.ReadBytes((int)mipmapSizes[0]);
            var fullJpeg = jpegHeader.Concat(jpegData).ToArray();

            Image = SKBitmap.Decode(fullJpeg);
        }
        else if (compression == BLP_COMPRESSION_NONE)
        {
            // Read palette (BGR)
            var palette = new SKColor[256];
            for (int i = 0; i < 256; i++)
            {
                byte b = br.ReadByte();
                byte g = br.ReadByte();
                byte r = br.ReadByte();
                byte a = br.ReadByte(); // unused
                palette[i] = new SKColor(r, g, b);
            }

            br.BaseStream.Seek(mipmapOffsets[0], SeekOrigin.Begin);
            byte[] indices = br.ReadBytes(Width * Height);
            byte[] alphas = alphaBits > 0 ? br.ReadBytes(Width * Height) : null;

            Image = new SKBitmap(Width, Height);
            int idx = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++, idx++)
                {
                    var color = palette[indices[idx]];
                    byte alpha = alphas != null ? alphas[idx] : (byte)255;
                    Image.SetPixel(x, y, color.WithAlpha(alpha));
                }
        }
        else throw new NotSupportedException("Unsupported BLP compression format.");
    }

    public void SaveAs(string outputPath, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(Image);
        using var data = image.Encode(format, 100);
        File.WriteAllBytes(outputPath, data.ToArray());
    }

    public static void ConvertImageToBLP1_JPEG(string inputImagePath, string outputBlpPath)
    {
        using var bitmap = SKBitmap.Decode(inputImagePath);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpegData = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        byte[] jpegBytes = jpegData.ToArray();
        byte[] header = jpegBytes.Take(Math.Min(BLP_JPEG_HEADER_SIZE, jpegBytes.Length)).ToArray();
        byte[] mipmap = jpegBytes.Skip(header.Length).ToArray();

        using var fs = new FileStream(outputBlpPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        bw.Write(System.Text.Encoding.ASCII.GetBytes("BLP1"));
        bw.Write(BLP_COMPRESSION_JPEG);
        bw.Write((uint)0); // alphaBits
        bw.Write((uint)bitmap.Width);
        bw.Write((uint)bitmap.Height);
        bw.Write((uint)0); // type
        bw.Write((uint)1); // mipmaps

        // Mipmap offsets
        uint headerOffset = (uint)(4 + 4 * 6 + 4 * 16 * 2 + 4 + header.Length);
        bw.Write(headerOffset);
        for (int i = 1; i < 16; i++) bw.Write(0u);

        // Mipmap sizes
        bw.Write((uint)mipmap.Length);
        for (int i = 1; i < 16; i++) bw.Write(0u);

        // JPEG header
        bw.Write((uint)header.Length);
        bw.Write(header);

        // Mipmap
        bw.Write(mipmap);
    }

    public static void ConvertImageToBLP1_Palette(string inputImagePath, string outputBlpPath)
    {
        using var bitmap = SKBitmap.Decode(inputImagePath);
        bitmap.PeekPixels().ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);

        var palette = GeneratePalette(bitmap, 256);
        byte[] indexedPixels = new byte[bitmap.Width * bitmap.Height];
        byte[] alphaChannel = new byte[bitmap.Width * bitmap.Height];
        int idx = 0;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++, idx++)
            {
                var pixel = bitmap.GetPixel(x, y);
                int closest = FindClosestColor(pixel, palette);
                indexedPixels[idx] = (byte)closest;
                alphaChannel[idx] = pixel.Alpha;
            }
        }

        using var fs = new FileStream(outputBlpPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        bw.Write(System.Text.Encoding.ASCII.GetBytes("BLP1"));
        bw.Write(BLP_COMPRESSION_NONE);
        bw.Write((uint)8); // 8-bit alpha
        bw.Write((uint)bitmap.Width);
        bw.Write((uint)bitmap.Height);
        bw.Write((uint)1); // type
        bw.Write((uint)1); // mipmaps

        uint paletteOffset = 4 + 4 * 6 + 4 * 16 * 2;
        uint pixelOffset = paletteOffset + 256 * 4;
        uint alphaOffset = pixelOffset + (uint)indexedPixels.Length;

        bw.Write(pixelOffset);
        for (int i = 1; i < 16; i++) bw.Write(0u);

        bw.Write((uint)(indexedPixels.Length + alphaChannel.Length));
        for (int i = 1; i < 16; i++) bw.Write(0u);

        // Write palette
        foreach (var color in palette)
        {
            bw.Write(color.Blue);
            bw.Write(color.Green);
            bw.Write(color.Red);
            bw.Write((byte)255);
        }

        // Write indices and alpha
        bw.Write(indexedPixels);
        bw.Write(alphaChannel);
    }

    private static int FindClosestColor(SKColor pixel, SKColor[] palette)
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < palette.Length; i++)
        {
            var c = palette[i];
            int dr = c.Red - pixel.Red;
            int dg = c.Green - pixel.Green;
            int db = c.Blue - pixel.Blue;
            int dist = dr * dr + dg * dg + db * db;
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static SKColor[] GeneratePalette(SKBitmap bitmap, int maxColors)
    {
        var colors = new Dictionary<SKColor, int>();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y).WithAlpha(255);
                colors[c] = colors.TryGetValue(c, out var count) ? count + 1 : 1;
            }

        return colors
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .Take(maxColors)
            .ToArray();
    }
}
