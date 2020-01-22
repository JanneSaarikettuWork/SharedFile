using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


/*
 * Test program for developing and testing feature: Conveyor: Shared local cache for lookup table (RAD-1047) 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 */

/*
 * Perskutarallaa: ReaderWriterLock[Slim] ei näytä toimivan prosessien välillä, kuten esim. globaali (nimetty) mutex.
 * Tästä oli juttua stackoverflowssa:
 * - https://stackoverflow.com/questions/3503833/cross-process-read-write-synchronization-primative-in-net
 * 
 * Tehty oma luokka joka toimii myös prosessien välillä, kst. ReadersWriterLockGlobal.cs
 * 
 */


namespace SharedFile
{
    class Program
    {
        // private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        private ReadersWriterLockGlobal cacheLock = new ReadersWriterLockGlobal("lookup_cache", 10);

        const string cacheFileName = "test_cache.dat";

        static void Main(string[] args)
        {
            var proggis = new Program();
            proggis.CommandLoop();
        }

        ~Program()
        {
            if (cacheLock != null)
                cacheLock.Dispose();
        }


        public void CommandLoop ()
        {
            int delayMsBetweenFileOperations = 0;
            int repeatCountForOperations = 1;
            bool readersWriterLockUsed = false; 

            while (true) // repeat forever
            {
                Console.WriteLine("");
                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine("Menu");
                Console.WriteLine("1. Save to cache file()");
                Console.WriteLine("2. Load from cache file()");
                Console.WriteLine("3. Delete cache file()");
                Console.WriteLine("...");
                Console.WriteLine($"101. Set delay (*100 ms) to file operations (now: {delayMsBetweenFileOperations})");
                Console.WriteLine($"102. Set repeat count (1-999999) for save & load (now: {repeatCountForOperations})");
                Console.WriteLine($"103. Set using readers–writer lock {!readersWriterLockUsed} (now: {readersWriterLockUsed})");
                Console.WriteLine ("  Q. Quit");
                Console.WriteLine("----------------------------------------------------------------");
                Console.Write    ("Command > ");

                try
                {
                    // int inputNumber = 0;
                    string inputString = Console.ReadLine();
                    Console.WriteLine("");

                    //if (false == int.TryParse(inputString, out inputNumber))
                    //    continue;

                    switch (inputString)
                    {
                        case "1":
                            for (int iCounter = 0; iCounter < repeatCountForOperations; iCounter++)
                                SaveToFile(readersWriterLockUsed, delayMsBetweenFileOperations);
                            break;
                        case "2":
                            for (int iCounter = 0; iCounter < repeatCountForOperations; iCounter++)
                                LoadFromFile(readersWriterLockUsed, delayMsBetweenFileOperations);
                            break;
                        case "3":
                            DeleteFile(readersWriterLockUsed);
                            break;

                        case "101":
                            delayMsBetweenFileOperations = AskNumber("Enter delay (*100 ms) to file operations> ");
                            break;
                        case "102":
                            repeatCountForOperations = AskNumber("Enter repeat count (1-999999) for operations> ");
                            break;
                        case "103":
                            readersWriterLockUsed = !readersWriterLockUsed;
                            break;

                        case "Q":
                        case "q":
                            return;


                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Just show problem and continue
                    LogEvent(ex.Message);
                }
            }
        }

        // ----------------------------------------------------------------------------
        // private int AskNumber()
        // ----------------------------------------------------------------------------
        private int AskNumber(string prompt)
        {
            int delayNumber = 0;

            while(true)
            {
                try
                {
                    Console.Write(prompt);
                    string delayString = Console.ReadLine();

                    if (false == int.TryParse(delayString, out delayNumber))
                        continue;
                    return delayNumber;
                }
                catch (Exception ex)
                {
                    // Just show problem and continue
                    LogEvent(ex.Message);
                }
            }
        }

        // ----------------------------------------------------------------------------
        // Write some stuff to cache file
        // 
        // Use StreamWriter as used in ..\Libs\radea_rfidData\rfid\Sku.cs SaveToFile
        // ----------------------------------------------------------------------------
        public void SaveToFile(bool readersWriterLockUsed, int delayMsBetweenFileOperations = 0)
        {
            string randString = "{0} pientä elefanttia marssi näin.";

            if (readersWriterLockUsed)
            {
                LogEvent("trying to enter SaveToFile()");
                cacheLock.EnterWriteLock();
            }

            LogEvent("entering SaveToFile()");
            try
            {
                System.IO.StreamWriter writer = new System.IO.StreamWriter(cacheFileName);

                for (int lineCount = 1; lineCount <= 100; lineCount++)
                {
                    string toWrite = String.Format(randString, lineCount);
                    writer.WriteLine(toWrite);

                    if (delayMsBetweenFileOperations > 0)
                        Thread.Sleep(delayMsBetweenFileOperations); // Make writing slower
                }

                writer.Flush();
                writer.Close();
                writer.Dispose();
            }

            // Expected exception 
            catch (IOException e)
            {
                Console.WriteLine("Application \"{0}\" instance \"{1}\" got an IOexception:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
                Console.WriteLine("Exception type {0}", e.GetType().Name);
            }
            catch (Exception e)
            {
                Console.WriteLine("Application \"{0}\" instance \"{1}\" got an exception:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
                Console.WriteLine("Exception type {0}", e.GetType().Name);
            }
            finally
            {
                if (readersWriterLockUsed)
                    cacheLock.ExitWriteLock();
            }

            LogEvent("exiting SaveToFile()");
        }

        // ----------------------------------------------------------------------------
        // Read stuff from cache file
        // 
        // Use StreamReader as used in ..\Libs\radea_rfidData\rfid\Sku.cs LoadFromFile
        // ----------------------------------------------------------------------------
        public void LoadFromFile(bool readersWriterLockUsed, int delayMsBetweenFileOperations = 0, bool printToStdout = false)
        {
            if (readersWriterLockUsed)
            { 
                LogEvent("trying to enter LoadFromFile()");
                cacheLock.EnterReadLock();
            }

            LogEvent("entering LoadFromFile()");
            try
            {
                System.IO.StreamReader reader = new System.IO.StreamReader(cacheFileName);
                // string buffer = reader.ReadToEnd();

                string lineBuffer = null;

                do
                {
                    lineBuffer = reader.ReadLine();

                    if (printToStdout)
                        Console.WriteLine(lineBuffer);

                    if (delayMsBetweenFileOperations > 0)
                        Thread.Sleep(delayMsBetweenFileOperations); // Make writing slower

                } while (lineBuffer != null);

                reader.Close();
                reader.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine("Application {0} instance {1} got exception:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
            }
            finally
            {
                if (readersWriterLockUsed)
                    cacheLock.ExitReadLock();
            }

            LogEvent("exiting LoadFromFile()");
        }

        // ----------------------------------------------------------------------------
        // Read stuff from cache file
        // 
        // Use StreamReader as used in ..\Libs\radea_rfidData\rfid\Sku.cs LoadFromFile
        // ----------------------------------------------------------------------------
        public void DeleteFile(bool readersWriterLockUsed)
        {
            if (readersWriterLockUsed)
            {
                LogEvent("trying to enter DeleteFile()");
                cacheLock.EnterWriteLock();
            }

            LogEvent("entering DeleteFile()");

            try
            {
                File.Delete(cacheFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Application {0} instance {1} got exception:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
            }

            LogEvent("exiting DeleteFile()");
        }


        // ----------------------------------------------------------------------------
        // public void LogEvent (string logString)
        // ----------------------------------------------------------------------------
        public void LogEvent (string logString)
        {
            try // to precede the log string with process id (pid)
            {
                string pid = Process.GetCurrentProcess().Id.ToString();
                Console.Write($"({pid}) "); // without newline
            }
            catch(Exception)
            {
                // do nothing
            }

            Console.WriteLine(logString);
        }
    }
}

