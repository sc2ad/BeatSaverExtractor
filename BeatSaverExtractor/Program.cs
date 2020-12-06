using BeatSaverSharp;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace BeatSaverExtractor
{
    internal class Program
    {
        private static async Task Main()
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("Enter map link (or key): ");
                    var link = Console.ReadLine();
                    if (link is null)
                    {
                        Console.WriteLine("Invalid link!");
                        continue;
                    }
                    if (link.Contains("://"))
                    {
                        var innerLink = link.Split('/').LastOrDefault(item => !string.IsNullOrEmpty(item));
                        if (innerLink is null)
                        {
                            Console.WriteLine($"Could not parse beatsaver key from: {link}!");
                            Console.WriteLine("Try providing the key instead.");
                            continue;
                        }
                        link = innerLink;
                    }

                    var client = new BeatSaver(new HttpOptions
                    {
                        ApplicationName = Assembly.GetExecutingAssembly().GetName().Name,
                        Version = Assembly.GetExecutingAssembly().GetName().Version
                    });
                    Console.WriteLine($"Creating temporary folder: {Path.GetFullPath(link)}");
                    Directory.CreateDirectory(link);
                    var dataPath = Path.Combine(link, "data");
                    Console.WriteLine("Downloading from beatsaver...");
                    Beatmap res;
                    using (var progress = new ProgressBar())
                    {
                        res = await client.Key(link, progress);
                    }
                    byte[] bytes;
                    using (var progress = new ProgressBar())
                        bytes = await res.DownloadZip(progress: progress);
                    using (var s = new MemoryStream(bytes))
                    {
                        var z = new ZipArchive(s);
                        z.ExtractToDirectory(dataPath, true);
                        var info = z.GetEntry("Info.dat");
                        if (info is null)
                            info = z.GetEntry("info.dat");
                        if (info is null)
                        {
                            Console.WriteLine($"Could not load 'Info.dat' or 'info.dat' from song: {Path.GetFullPath(dataPath)}!");
                            continue;
                        }
                        var infoJson = await JsonDocument.ParseAsync(info.Open());
                        if (!TryParseJson(infoJson.RootElement, "_songName", out var dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "songName.txt"), dstString);
                        if (!TryParseJson(infoJson.RootElement, "_songAuthorName", out dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "songAuthorName.txt"), dstString);
                        if (!TryParseJson(infoJson.RootElement, "_levelAuthorName", out dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "levelAuthorName.txt"), dstString);
                        if (!TryParseJson(infoJson.RootElement, "_songFilename", out dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "songFilename.txt"), dstString);
                        if (!TryParseJson(infoJson.RootElement, "_beatsPerMinute", out dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "beatsPerMinute.txt"), dstString);
                        if (!TryParseJson(infoJson.RootElement, "_coverImageFilename", out dstString))
                            continue;
                        await File.WriteAllTextAsync(Path.Combine(link, "coverImageFilename.txt"), dstString);
                        // Copy over cover image
                        var coverImage = z.GetEntry(dstString);
                        if (coverImage is not null)
                            coverImage.ExtractToFile(Path.Combine(link, dstString));
                        else
                        {
                            Console.WriteLine($"Could not load cover image from path: {dstString}");
                            continue;
                        }
                        if (infoJson.RootElement.TryGetProperty("_difficultyBeatmapSets", out var difficultySets))
                        {
                            foreach (var item in difficultySets.EnumerateArray())
                            {
                                if (item.TryGetProperty("_beatmapCharacteristicName", out var charNameE) && "Standard" == charNameE.GetString())
                                {
                                    var diffCount = item.GetProperty("_difficultyBeatmaps").GetArrayLength();
                                    await File.WriteAllTextAsync(Path.Combine(link, "difficultyCount.txt"), diffCount.ToString(CultureInfo.InvariantCulture));
                                    break;
                                }
                            }
                        }
                    }
                    Console.WriteLine($"Complete with download of key: {link} to: {Path.GetFullPath(link)}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception ocurred: {e}");
                }
            }
        }

        private static bool TryParseJson(JsonElement parent, string propName, out string dst)
        {
            if (!parent.TryGetProperty(propName, out var dstE) || dstE.GetString() is null)
            {
                dst = dstE.GetString()!;
                Console.WriteLine($"Failed to get: {propName} from json!");
                return false;
            }
            dst = dstE.GetString()!;
            return true;
        }
    }
}