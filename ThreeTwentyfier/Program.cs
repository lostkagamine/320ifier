using System.Diagnostics;

// 320ifier - parallel automatic bulk transcoding toolkit
// 2023-2024 Nightshade System
// warning: this program will use as many cores as it can, be careful

namespace ThreeTwentyfier;

public static class Program
{
    static List<WorkUnit> GetPsiList(Mode mode)
    {
        var list = new List<WorkUnit>();

        var baseDir = Path.GetFileName(Environment.CurrentDirectory);
        Directory.CreateDirectory(baseDir);

        var procCount = Environment.ProcessorCount;
        var currentThread = 0u;
        var currentId = 0u;
        
        var dir = Directory.EnumerateFiles("input");
        foreach (var f in dir)
        {
            var filename = Path.GetFileNameWithoutExtension(f);
            var workingDirectory = Environment.CurrentDirectory;
            var inputFile = Path.Combine(workingDirectory, "input", $"{filename}.flac");
            var outputFile = Path.Combine(workingDirectory, baseDir, filename);
            var extension = mode switch
            {
                Mode.To320 => "mp3",
                Mode.To16Bit => "flac",
                Mode.ToV0 => "mp3",
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
            var psi = mode switch
            {
                Mode.To320 => new ProcessStartInfo
                {
                    FileName = @"ffmpeg",
                    Arguments = $"""
                                 -y -i "{inputFile}" -ab 320k -map_metadata 0 -id3v2_version 3 "{outputFile}.{extension}"
                                 """
                },
                Mode.To16Bit => new ProcessStartInfo
                {
                    FileName = @"ffmpeg",
                    Arguments = $"""
                                 -y -i "{inputFile}" -sample_fmt s16 "{outputFile}.{extension}"
                                 """
                },
                Mode.ToV0 => new ProcessStartInfo
                {
                    FileName = @"ffmpeg",
                    Arguments = $"""
                                 -y -i "{inputFile}" -c:a libmp3lame -q:a 0 -map_metadata 0 -id3v2_version 3 "{outputFile}.{extension}"
                                 """
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
            psi.CreateNoWindow = true;
            psi.UseShellExecute = true;
            list.Add(new()
            {
                Info = psi,
                ThreadId = currentThread++,
                FileName = filename,
                WorkId = currentId++
            });
            if (currentThread >= procCount) currentThread = 0;
        }

        return list;
    }
    
    public static void Main(string[] args)
    {
        var mode = Mode.To320;
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "16bit":
                    Console.WriteLine("16-bit mode");
                    mode = Mode.To16Bit;
                    break;
                case "v0":
                    Console.WriteLine("v0 mode");
                    mode = Mode.ToV0;
                    break;
            }
        }

        var processStartInfoList = GetPsiList(mode);
        var fileCount = processStartInfoList.Count;
        var psiSlots = Environment.ProcessorCount;

        if (psiSlots > fileCount) psiSlots = fileCount;
        
        Console.WriteLine($"Starting {psiSlots} threads");
        
        var complete = 0;
        var threads = new List<Thread>();
        var timeAtStart = Stopwatch.GetTimestamp();
        
        for (uint i = 0; i < psiSlots; i++)
        {
            var threadId = i;
            var thread = new Thread(() =>
            {
                var done = new List<uint>();
                while (true)
                {
                    if (processStartInfoList.Count == 0) break;
                    
                    var psi = processStartInfoList.FirstOrDefault(x =>
                        x.ThreadId == threadId && !done.Contains(x.WorkId));
                    if (psi == null) break;
                    
                    Console.WriteLine($"Thread {threadId} picked up '{psi.FileName}'");
                    
                    var process = Process.Start(psi.Info)!;
                    process.WaitForExit();
                    
                    done.Add(psi.WorkId);
                    
                    Console.WriteLine($"Thread {threadId} finished '{psi.FileName}' ({++complete}/{fileCount})");
                }
                Console.WriteLine($"Thread {threadId} exhausted work pool");
            });
            thread.Start();
            threads.Add(thread);
        }
        
        foreach (var t in threads) t.Join();

        var timeAtEnd = Stopwatch.GetElapsedTime(timeAtStart);

        Console.WriteLine(timeAtEnd.TotalSeconds > 60
            ? $"Complete! Took {timeAtEnd.TotalMinutes}m:{timeAtEnd.TotalSeconds % 60}s."
            : $"Complete! Took {timeAtEnd.TotalSeconds} seconds.");
    }
}