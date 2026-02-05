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



                usage: lossymxf [-? | -h | --help] mirror [--dry-run] <src> <dst>

                The src path will be recursively mirrored to the dst
                path. Any mxfs found will be compressed.
                """;
    }

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(Usage());
            return 0;
        }

        foreach (var s in args)
        {
            switch (s)
            {
                case "-h":
                case "--help":
                case "-?":
                    Console.WriteLine(Usage());
                    return 0;
            }
        }


        if (args.Length > 2)
        {
            return Mirror(args);
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

    static int Mirror(string[] args)
    {
        if (!args[0].Equals("mirror"))
        {
            Console.WriteLine(Usage());
            return 0;
        }

        int v = 1;
        bool verbose = true;
        bool dryrun = false;
        if (args[v].Equals("--dry-run"))
        {
            v += 1;
            dryrun = true;
        }

        var pathSrc = args[v++];
        if (string.IsNullOrWhiteSpace(pathSrc))
        {
            Console.WriteLine("Src path arg was null or whitespace.");
            return 1;
        }

        var pathDst = args[v];
        if (string.IsNullOrWhiteSpace(pathDst))
        {
            Console.WriteLine("Dst path arg was null or whitespace.");
            return 1;
        }

        if (Directory.Exists(pathDst))
        {
            if (Directory.GetFileSystemEntries(pathDst, "*", SearchOption.TopDirectoryOnly).Length > 0)
            {
                Console.WriteLine("Dst path is not empty.");
                return 1;
            }
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
            MatchType = MatchType.Simple,
            AttributesToSkip = FileAttributes.Hidden,
        };

        var src = new DirectoryInfo(pathSrc);

        foreach (var info in src.EnumerateFileSystemInfos("*", options))
        {
            var rpath = Path.GetRelativePath(pathSrc, info.FullName);
            var mpath = Path.Combine(pathDst, rpath);

            if (info.Attributes.HasFlag(FileAttributes.Directory))
            {
                if (verbose) Console.WriteLine($"D: {mpath}");
                if (dryrun) continue;

                Directory.CreateDirectory(mpath);

                continue;
            }

            switch (info.Extension)
            {
                case ".mxf":
                {
                    if (verbose) Console.WriteLine($"M: {mpath}");
                    if (dryrun) continue;

                    try
                    {
                        Lossy.Copy(info.FullName, mpath);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(
                            $"Error compressing or copying mxf.\nsrc: {info.FullName}\ndst: {mpath}\n{e}");
                    }

                    break;
                }

                default:
                {
                    if (verbose) Console.WriteLine($"F: {mpath}");
                    if (dryrun) continue;

                    try
                    {
                        File.Copy(sourceFileName: info.FullName, destFileName: mpath);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(
                            $"Error copying regular file.\nsrc: {info.FullName}\ndst: {mpath}\n{e}");
                        return 1;
                    }

                    break;
                }
            }
        }

        return 0;
    }
}