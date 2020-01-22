/*****************************************************************************************************************
 * ReadersWriterLockGlobal - a readers–writer lock implementation that can be used for synchronizing
 * activities between different processes.
 * 
 * For readers–writer lock design pattern, see 
 * - https://en.wikipedia.org/wiki/Readers%E2%80%93writer_lock and
 * - https://en.wikipedia.org/wiki/Readers%E2%80%93writers_problem
 * 
 * Note that .NET classes ReaderWriterLock and ReaderWriterLockSlim do not support synchronization between different 
 * processes, only synchronization between threads inside a process.
 * 
 * See also/modified from:
 * https://stackoverflow.com/questions/3503833/cross-process-read-write-synchronization-primative-in-net
 * 
 *****************************************************************************************************************/
using System.Threading;

namespace SharedFile
{
    class ReadersWriterLockGlobal
    {
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

            m_mutex = new Mutex(false, $"{name}.mutex", out mutexCreated);

            // play it safe by having one more potential request
            m_max_nrof_readers = maxReaders + 1;

            // make all requests initially available
            m_semaphore = new Semaphore(m_max_nrof_readers, m_max_nrof_readers, $"{name}.semaphore", out semaphoreCreated); 
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

        public void EnterReadLock()
        {
            m_mutex.WaitOne();
            m_semaphore.WaitOne();
            m_mutex.ReleaseMutex();
        }

        public void ExitReadLock()
        {
            m_semaphore.Release();
        }

        public void EnterWriteLock()
        {
            m_mutex.WaitOne();
            for (int i = 0; i < m_max_nrof_readers; i++)
                m_semaphore.WaitOne(); // drain out all readers-in-progress
            m_mutex.ReleaseMutex();
        }

        public void ExitWriteLock()
        {
            for (int i = 0; i < m_max_nrof_readers; i++)
                m_semaphore.Release();
        }

    }
}
