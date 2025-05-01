using SkiaSharp;
using System;

public class BlpConverterCli
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        string inputFile = string.Empty;
        string outputFile = string.Empty;
        bool blp2png = false, blp2jpg = false, jpg2blp = false, png2blp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-f":
                case "--file":
                    inputFile = args[++i];
                    break;
                case "-o":
                case "--output":
                    outputFile = args[++i];
                    break;
                case "-blp2png":
                    blp2png = true;
                    break;
                case "-blp2jpg":
                    blp2jpg = true;
                    break;
                case "-jpg2blp":
                    jpg2blp = true;
                    break;
                case "-png2blp":
                    png2blp = true;
                    break;
            }
        }

        try
        {
            if (string.IsNullOrEmpty(inputFile) || string.IsNullOrEmpty(outputFile))
                throw new ArgumentException("Missing required -f or -o arguments.");

            if (blp2png)
            {
                var blp = new BLPImage(inputFile);
                blp.SaveAs(outputFile, SKEncodedImageFormat.Png);
                Console.WriteLine($"Converted BLP to PNG: {outputFile}");
            }
            else if (blp2jpg)
            {
                var blp = new BLPImage(inputFile);
                blp.SaveAs(outputFile, SKEncodedImageFormat.Jpeg);
                Console.WriteLine($"Converted BLP to JPG: {outputFile}");
            }
            else if (jpg2blp)
            {
                BLPImage.ConvertImageToBLP1_JPEG(inputFile, outputFile);
                Console.WriteLine($"Converted JPG to BLP1: {outputFile}");
            }
            else if (png2blp)
            {
                BLPImage.ConvertImageToBLP1_Palette(inputFile, outputFile);
                Console.WriteLine($"Converted PNG to palette-based BLP1: {outputFile}");
            }
            else
            {
                throw new ArgumentException("No conversion operation specified.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
            PrintHelp();
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- -f <input_file> -o <output_file> [conversion_flag]");
        Console.WriteLine("  MagicBLP_CLI.exe -f <input_file> -o <output_file> [conversion_flag]");
        Console.WriteLine("Flags:");
        Console.WriteLine("  -f, --file       Input file path");
        Console.WriteLine("  -o, --output     Output file path");
        Console.WriteLine("  -blp2png         Convert BLP to PNG");
        Console.WriteLine("  -blp2jpg         Convert BLP to JPG");
        Console.WriteLine("  -jpg2blp         Convert JPG to BLP (JPEG-based)");
        Console.WriteLine("  -png2blp         Convert PNG to BLP (palette-based)");
    }
}
