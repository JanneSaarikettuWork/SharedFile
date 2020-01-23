/*****************************************************************************************************************
 * ReadersWriterLockGlobal - a readers–writer lock implementation that can be used for synchronizing
 * reading and writing activities of files shared between different processes (and threads).
 * 
 * For readers–writer lock design pattern, see 
 * - https://en.wikipedia.org/wiki/Readers%E2%80%93writer_lock and
 * - https://en.wikipedia.org/wiki/Readers%E2%80%93writers_problem
 * 
 * Note that .NET classes ReaderWriterLock and ReaderWriterLockSlim do not support synchronization between different 
 * processes, only synchronization between threads inside a process.
 * 
 * See also/created from pseudocode in:
 * https://stackoverflow.com/questions/3503833/cross-process-read-write-synchronization-primative-in-net
 * 
 *****************************************************************************************************************/
using System.Threading;

namespace SharedFile
{
    class ReadersWriterLockGlobal
    {
        const string sync_object_prefix = ".conveyor_"; // Prefix syncronization object names with this for easy finding
        const int enter_lock_timeout_ms = 600000;       // A failsafe timeout (10 min) for handling an unlikely situation where something has gone seriously wrong in process synchronization 
                                                            
        Mutex m_mutex;
        Semaphore m_semaphore;
        int m_max_nrof_readers;

        /// <summary>
        /// Construct a readers–writer lock that can be used to synchronize the activities of processes.
        /// </summary>
        /// <param name="name">Name of the readers–writer lock.</param>
        /// <param name="maxReaders">Maximum number of readers</param>
        public ReadersWriterLockGlobal(string name, int maxReaders)
        {
            bool mutexCreated, semaphoreCreated;

            m_mutex = new Mutex(false, $"{sync_object_prefix}{name}.mutex", out mutexCreated);

            // play it safe by having one more potential request
            m_max_nrof_readers = maxReaders + 1;

            // make all requests initially available
            m_semaphore = new Semaphore(m_max_nrof_readers, m_max_nrof_readers, $"{sync_object_prefix}{name}.semaphore", out semaphoreCreated); 
        }

        public void Dispose()
        {
            if (m_mutex != null)
            {
                m_mutex.Dispose();
                m_mutex = null;
            }
                
            if (m_semaphore != null)
            {
                m_semaphore.Dispose();
                m_semaphore = null;
            }
        }

        ~ReadersWriterLockGlobal()
        {
            Dispose();
        }

        /// <summary>
        /// Enter read lock in the beginning of an inter-process read operation
        /// </summary>
        /// <returns><c>true</c> if lock was successfully entered; 
        /// otherwise <c>false</c> in which case cancel the read operation and do not try to exit the lock</returns>
        public bool EnterReadLock()
        {
            if (false == m_mutex.WaitOne(enter_lock_timeout_ms))
                return false;

            m_semaphore.WaitOne();
            m_mutex.ReleaseMutex();
            return true;
        }

        /// <summary>
        /// Exit read lock at the end of an inter-process read operation
        /// </summary>
        public void ExitReadLock()
        {
            m_semaphore.Release();
        }

        /// <summary>
        /// Enter write lock in the beginning of an inter-process write operation
        /// </summary>
        /// <returns><c>true</c> if lock was successfully entered; 
        /// otherwise <c>false</c> in which case cancel the write operation and do not try to exit the lock</returns>
        public bool EnterWriteLock()
        {
            if (false == m_mutex.WaitOne(enter_lock_timeout_ms))
                return false;

            for (int i = 0; i < m_max_nrof_readers; i++)
                m_semaphore.WaitOne(); // drain out all readers-in-progress
            m_mutex.ReleaseMutex();
            return true;
        }

        /// <summary>
        /// Exit write lock at the end of an inter-process write operation
        /// </summary>
        public void ExitWriteLock()
        {
            for (int i = 0; i < m_max_nrof_readers; i++)
                m_semaphore.Release();
        }

    }
}
