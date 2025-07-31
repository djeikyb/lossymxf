using System.Diagnostics;
using TheBasicFileStreamVersion;

namespace lossymxf;

class Program
{
    static string Usage()
    {
        return $"""
                usage: lossymxf [-? | -h | --help] <src> <dst>

                The src path should be a dcp folder.
                
                The src dcp will be compressed and written to the path
                specified by the second arg.
                
                Compression is highly disk efficient, and irreversible.
                Good if you just need a dcp full of mxf-shaped objects.
                Bad if the actual video and audio essence is important.
                Ugly if you deliver the compressed version to a theatre,
                and expect playback.
                """;
    }

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(Usage());
            return 0;
        }

        foreach (var a in args)
        {
            switch (a)
            {
                case "-h":
                case "--help":
                case "-?":
                    Console.WriteLine(Usage());
                    return 0;
            }
        }

        var pathSrcDcp = args[0];
        if (string.IsNullOrWhiteSpace(pathSrcDcp))
        {
            Console.WriteLine("Src dcp arg was null or whitespace.");
            return 1;
        }

        var pathDstDcp = args[1];
        if (string.IsNullOrWhiteSpace(pathDstDcp))
        {
            Console.WriteLine("Dst dcp arg was null or whitespace.");
            return 1;
        }

        Directory.CreateDirectory(pathDstDcp);

        var sw = Stopwatch.StartNew();

        foreach (var pathSrcDcpFile in Directory.EnumerateFiles(pathSrcDcp))
        {
            var pathDstDcpFile = Path.Combine(pathDstDcp, Path.GetFileName(pathSrcDcpFile));
            var ext = Path.GetExtension(pathSrcDcpFile);
            switch (ext)
            {
                case ".xml":
                    File.Copy(pathSrcDcpFile, pathDstDcpFile, true);
                    break;
                case ".mxf":
                    try
                    {
                        Lossy.Copy(pathSrcDcpFile, pathDstDcpFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error compressing or copying mxf.\nsrc: {pathSrcDcpFile}\ndst: {pathDstDcpFile}\n{e}");
                        return 1;
                    }

                    break;
            }
        }

        sw.Stop();

        Console.WriteLine($"done in {sw.ElapsedMilliseconds}ms");
        return 0;
    }
}