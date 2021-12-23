using System;
using System.IO;

namespace SplitFile
{
    class Program
    {
        static void Main(string[] args)
        {
            const string USAGE = "\nUsage: SplitFile <filename> <splits> <destinationfolder> <IO Block size:{4KB}{8KB}{32KB}{64KB}>";

            Console.WriteLine("SplitFile ver 1.0");
            Console.WriteLine("Multi-Threaded Utility to Split Large Files\n");

            // Check Parameters
            if (args.Length != 4)
            {
                Console.WriteLine("Arguments are missing");
                Console.WriteLine(USAGE);
                return;
            }
            else
            {
                string filename = args[0].Trim();
                if (!File.Exists(filename))
                {
                    Console.WriteLine("Cound not find the file to split");
                    Console.WriteLine(USAGE);
                    return;
                }

                int splits;
                bool test = int.TryParse(args[1], out splits);
                if (!test)
                {
                    Console.WriteLine("Please enter a numeric argument for splits count");
                    Console.WriteLine(USAGE);
                    return;
                }

                if (splits < 2 || splits > 30)
                {
                    Console.WriteLine("Number of splits must be between 2 and 30");
                    Console.WriteLine(USAGE);
                    return;
                }

                string destfolder = args[2].Trim();
                if (!(destfolder.EndsWith(@"\") || destfolder.EndsWith(@"/")))
                {
                    Console.WriteLine(@"Please terminate destination foldere with / or \");
                    Console.WriteLine(USAGE);
                    return;
                }
                if (!Directory.Exists(destfolder))
                {
                    Console.WriteLine("Cound not validate destination folder");
                    Console.WriteLine(USAGE);
                    return;
                }

                int ioblock = 4096;
                string blocksize = args[3].Trim().ToUpper();
                if ("4KB" == blocksize)
                    ioblock = 4096;
                else if ("8KB" == blocksize)
                    ioblock = 8192;
                else if ("32KB" == blocksize)
                    ioblock = 32768;
                else if ("64KB" == blocksize)
                    ioblock = 65536;
                else
                {
                    Console.WriteLine("Invalid IO block size. Enter 4KB or 8KB or 32KB or 64KB");
                    Console.WriteLine(USAGE);
                    return;
                }

                try
                {
                    // Call Splitter Code
                    Splitter.Split(filename, splits, destfolder, ioblock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}", ex.ToString());
                }
            }
        }
    }
}