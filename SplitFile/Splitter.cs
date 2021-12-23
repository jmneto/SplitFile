using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace SplitFile
{
    public static class Splitter
    {
        // Need public static to find how to divide the available queue space in Writer Method
        public static int numofsplits = 2;

        // If an exception occurs all work should stop
        public static bool exceptionoccurred = false;

        // Stores available free memory when process starts
        public static double freememoryMB = 1024; // Defaults to 1GB;

        // Split
        public static void Split(string file, int splits, string destfolder, int ioblocksize)
        {
            // Stopwatch to calculate total elapsed time
            Stopwatch ts = Stopwatch.StartNew();
            try
            {
                // Get computer metrics
                var mmc = new MemoryMetricsClient();
                var metrics = mmc.GetMetrics();
                Console.WriteLine("Memory: Total {0} MB, Used {1} MB, Free {2} MB", metrics.TotalMB, metrics.UsedMB, metrics.FreeMB);
                freememoryMB = metrics.FreeMB;

                // Get the size of the file to split
                FileInfo info = new FileInfo(file);
                long filesize = info.Length;

                // Check if the file is big enough to split
                if (filesize < ioblocksize * splits * 2)
                    throw new Exception("File size is too small for the requested combination of IO block size and number of splits");

                // Calculate number of Blocks per split
                long blockspersplit = (long)Math.Floor((decimal)(Math.Ceiling((decimal)filesize / ioblocksize)) / splits);

                // Tasks Array
                List<Task> tasks = new List<Task>();

                // Update number of splits
                numofsplits = splits;

                // Launch Workers (Last worker reads to the end of file)
                for (int i = 1; i <= splits; i++)
                {
                    object arg = i;
                    tasks.Add(Task.Factory.StartNew((parm) =>
                    {
                        new SplitterWorker(file, (int)parm, blockspersplit, (int)parm == splits, destfolder, ioblocksize);
                    }, arg));
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                Splitter.exceptionoccurred = true;
                Console.WriteLine("One or more exceptions occurred:");
                foreach (var ex in ae.InnerExceptions)
                    Console.WriteLine("{0}", ex.ToString());
            }
            catch (Exception ex)
            {
                Splitter.exceptionoccurred = true;
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                // Stop stopwatch
                ts.Stop();
                Console.WriteLine("Duration {0:00}:{1:00}:{2:00}.{3:00}", ts.Elapsed.Hours, ts.Elapsed.Minutes, ts.Elapsed.Seconds, ts.Elapsed.Milliseconds);

                // Provide final status
                if (Splitter.exceptionoccurred == true)
                    Console.WriteLine("Errors occurred. Unsuccessful split");

                Console.WriteLine("Done.");
            }
        }
    }

    // Structure IOBlock
    class IOBlock
    {
        public byte[] data;
        public int length;
    }

    // Worker
    class SplitterWorker
    {
        // Private memory buffer for this worker
        private Queue<IOBlock> ioblocks = new Queue<IOBlock>();

        // Worker control flags
        private bool readoperationcompleted = false;
        private bool pausereader = false;

        // Worker constructor
        public SplitterWorker(string file, int split, long blockspersplit, bool readtoend, string destfolder, int ioblocksize)
        {
            try
            {
                Console.WriteLine("Splitter worker {0} created. IO block size {1} KB", split, ioblocksize / 1024);

                // Prepare and launch worker threads
                List<Task> tasks = new List<Task>();

                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Reader(file, split, blockspersplit, readtoend, ioblocksize);
                }));

                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Writer(file, split, destfolder, ioblocksize);
                }));

                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                Splitter.exceptionoccurred = true;
                foreach (var ex in ae.InnerExceptions)
                    throw new Exception(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Splitter.exceptionoccurred = true;
                throw new Exception(ex.Message, ex);
            }
        }

        private void Reader(string file, int split, long blockspersplit, bool readtoend, int ioblocksize)
        {
            try
            {
                // Calculate location to start reading
                long startsplitlocation = (split - 1) * ioblocksize * blockspersplit;
                int blocksread = 0;

                // Read from location and save to buffer queue
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    long location = startsplitlocation;
                    byte[] iobuffer = new byte[ioblocksize];

                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        if (location > 0)
                            br.BaseStream.Seek(location, SeekOrigin.Begin);

                        int bytesread;
                        while (((bytesread = br.Read(iobuffer, 0, ioblocksize)) > 0) && ((blocksread < blockspersplit) || readtoend) && !Splitter.exceptionoccurred)
                        {
                            IOBlock ioblock = new IOBlock
                            {
                                length = bytesread,
                                data = new byte[ioblocksize]
                            };
                            iobuffer.CopyTo(ioblock.data, 0);

                            lock (ioblocks)
                                ioblocks.Enqueue(ioblock);

                            blocksread++;

                            if (pausereader)
                                Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Splitter.exceptionoccurred = true;
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                readoperationcompleted = true;
            }
        }

        private void Writer(string file, int split, string destfolder, int ioblocksize)
        {
            try
            {
                // Stopwatch to calculate MB/s                
                Stopwatch ts = Stopwatch.StartNew();
                double elapsedwatermark = ts.Elapsed.TotalSeconds + 1;

                // Calculate maximun and fallback buffer queue sizes
                int maxqueuesize = (int)((Math.Max(Splitter.freememoryMB * 0.8, 1024) * 1014 * 1024) / ioblocksize / Splitter.numofsplits);
                int queueresetsize = maxqueuesize / 3;

                // Read from buffer queue and write to split
                long byteswritten = 0;
                using (FileStream fs = new FileStream(String.Format("{0}.Split.{1:00}", destfolder + Path.GetFileName(file), split), FileMode.Create, FileAccess.Write))
                {
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        while (true)
                        {
                            while (ioblocks.Count > 0)
                            {
                                IOBlock ioblock;

                                lock (ioblocks)
                                    ioblock = ioblocks.Dequeue();

                                bw.Write(ioblock.data, 0, ioblock.length);

                                byteswritten += ioblock.length;

                                // Show stats every 1 second
                                double elapsedsecs = ts.Elapsed.TotalSeconds;
                                if (elapsedsecs > elapsedwatermark)
                                {
                                    elapsedwatermark = elapsedsecs + 1;
                                    double mb = byteswritten / 1024d / 1024d;
                                    double mbs = mb / elapsedsecs;
                                    Console.WriteLine("Worker {0} wrote {1:0} MB averaging {2:0} MB/s queued {3:0} MB", split, mb, mbs, ioblocks.Count * ((double)ioblocksize / 1024d / 1024d));
                                }

                                // Pause / Resume reader thread depending on how behind in the writer
                                if (!pausereader && ioblocks.Count > maxqueuesize)
                                {
                                    Console.WriteLine("Worker {0} writer queue too big, pausing the reader", split);
                                    pausereader = true;
                                }
                                else if (pausereader && ioblocks.Count < queueresetsize)
                                {
                                    Console.WriteLine("Worker {0} resuming", split);
                                    pausereader = false;
                                }
                            }

                            // Check if we are done!
                            if ((ioblocks.Count == 0 && readoperationcompleted) || Splitter.exceptionoccurred)
                                break;

                            // Sleep the thread in case the Buffer Queue is Empty
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Splitter.exceptionoccurred = true;
                throw new Exception(ex.Message, ex);
            }
        }
    }
}
