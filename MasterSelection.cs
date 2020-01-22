/*****************************************************************************************************************
 * MasterSelection - determines the master or slave role of this Conveyor application process (process in OS sense). 
 * The master uses network connection to read lookup data from Radea and saves it to a local file. Slaves read 
 * lookup data from the local file. The master selection is implemented in such a way that:
 * - There is one master at a time and the other Conveyor processes are slaves.
 * - There is always a master as long as there is at least one Conveyor process running. For example, shutting 
 *   down or killing a master process will cause another (currently slave) process to step up and take the role 
 *   of a new master.
 * ------------------------------------------------------------------------------------------------------
 * In the beginning of check loop, in a write -locked critical section:
 * 1.     Check the existence of masterlock.dat file
 * 2.     If file does not exist, claim_master_role
 * 2.1.     Create file and write pid and reader title to the file
 * 2.2.     Return as master
 * 3.     If file exists 
 * 3.1.     Read file_pid from the file and compare it with pid of this process
 * 3.1.1.     Match -> return as master
 * 3.1.2.     No match -> check_master_status
 * 3.1.2.1.     Get a list of all running Conveyor pids
 * 3.1.2.2.     If file_pid is found in the list of pids -> that is the alive master
 * 3.1.2.2.1.     Return as a slave
 * 3.1.2.3.     If file_pid is not found in the list of pids -> master is not running any more
 * 3.1.2.3.1.     claim_master_role (item 2. above)
 * 
 *****************************************************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SharedFile
{

    class MasterSelection
    {

        private ReadersWriterLockGlobal masterLock = new ReadersWriterLockGlobal("master_lock", 5);
        private const string masterLockFileName = "masterlock.dat";

        private int MyPid => Process.GetCurrentProcess().Id;
        private string MyPname => Process.GetCurrentProcess().ProcessName;

        // TODO restore this when applying to Conveyor
        // private const string nameOfTheseProcesses = "Conveyor";
        private const string nameOfTheseProcesses = "SharedFile";


        // TODO: this is for demo only - use the one (that will be) defined in ..\Libs\radea_lib_Process\Process\Process\Conveyor.cs
        public static string SharedDataFileFullPathName(string fileName) // E.g. "C:\ProgramData\Nordic ID\Conveyor\cache\[fileName]"
        {
            return fileName;
        }


        // ctor
        public MasterSelection()
        {
        }

        // dtor
        ~MasterSelection()
        {
            if (masterLock != null)
                masterLock.Dispose();
        }

        // ----------------------------------------------------------------------------
        // IAmTheMaster() - let application process call this method for determining 
        // (and possibly changing) its role, master or slave.
        // ----------------------------------------------------------------------------
        /// <summary>
        /// Tells application process if it is the master. Called in the beginning of lookup data refresh
        /// </summary>
        /// <returns>true if caller is master, false if caller is slave</returns>
        public bool IAmTheMaster()
        {
            masterLock.EnterWriteLock();

            try
            {
                if (!File.Exists(masterLockFileName)) // 1.
                    return (ClaimMasterRole()); // 2.
                else // 3
                {
                    int masterLockPid = ReadMasterLockPid(); // 3.1.

                    if (MyPid == masterLockPid) // 3.1.1.
                        return true; // I am the master (..ou jeah!)
                    else // 3.1.2.
                    {
                        if (ConveyorProcessExists(masterLockPid)) // 3.1.2.2.
                            return false; // I am a slave (..damn!) 3.1.2.2.1
                        else
                            return (ClaimMasterRole()); // 3.1.2.3.1. -> 2.
                    }
                }
            }
            finally
            {
                masterLock.ExitWriteLock();
            }
        }

        private bool ConveyorProcessExists(int masterLockPid)
        {

            var processes = Process.GetProcessesByName(nameOfTheseProcesses);

            foreach (var process in processes)
            {
                if (process.Id == masterLockPid)
                    return true; // the (master) process exists
            }
            return false; // could not find given Conveyor process
        }

        private int ReadMasterLockPid()
        {
            int pid = 0;
            string humanFriendly = "";  // use readers[0].Hostname or something like that

            try
            {
                LoadMasterLock(ref pid, ref humanFriendly);
            }
            catch (Exception e) // do not pass here
            {
                return 0;
            }
            
            return pid;
        }

        private bool ClaimMasterRole()
        {
            //*2.1.Create file and write pid and reader title to the file
            //*2.2.Return as master
            int pid = MyPid;
            string humanFrindlyDescription = "(esim. reader.hostname)";

            SaveMasterLock(pid, humanFrindlyDescription);
            return true;
        }




        // ----------------------------------------------------------------------------
        // SaveMasterLock()
        // ----------------------------------------------------------------------------
        private void SaveMasterLock(int pid, string humanFrindlyDescription)
        {
            LogEvent("entering SaveMasterLock");
            try
            {
                string fileName = SharedDataFileFullPathName(masterLockFileName);

                using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                {
                    BinaryWriter binaryWriter = new BinaryWriter(fileStream);
                    binaryWriter.Write(pid);
                    binaryWriter.Write(humanFrindlyDescription);
                }
            }

            catch (Exception e)
            {
                Console.WriteLine("Application \"{0}\" instance \"{1}\" failed to save master lock:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
                Console.WriteLine("Exception type {0}", e.GetType().Name);
                throw e;
            }

            LogEvent("exiting SaveMasterLock()");
        }

        // ----------------------------------------------------------------------------
        // LoadMasterLock()
        // ----------------------------------------------------------------------------
        private void LoadMasterLock(ref int pid, ref string humanFrindlyDescription)
        {
            LogEvent("entering LoadMasterLock()");
            try
            {
                string fileName = SharedDataFileFullPathName(masterLockFileName);

                using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
                {
                    BinaryReader binaryReader = new BinaryReader(fileStream);
                    pid = binaryReader.ReadInt32();
                    humanFrindlyDescription = binaryReader.ReadString();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Application \"{0}\" instance \"{1}\" failed to load master lock:\n\"{2}\"", this.ToString(), this.GetHashCode(), e.Message);
                Console.WriteLine("Exception type {0}", e.GetType().Name);
                throw e;
            }
            LogEvent("exiting LoadMasterLock()");
        }

        // ----------------------------------------------------------------------------
        // LogEvent(string logString)
        // ----------------------------------------------------------------------------
        public void LogEvent(string logString)
        {
            try // to precede the log string with process id (pid)
            {
                string pid = Process.GetCurrentProcess().Id.ToString();
                Console.Write($"({pid}) "); // without newline
            }
            catch (Exception)
            {
                // do nothing
            }

            Console.WriteLine(logString);
        }
    }
}
