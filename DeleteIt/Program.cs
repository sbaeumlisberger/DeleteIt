using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

namespace DeleteIt
{
    public class Program
    {
        public static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                DeleteFile(arg);
            }
            Console.WriteLine($"Successfully deleted {args.Length} file(s).");
        }

        private static void DeleteFile(string file)
        {
            var processes = FindLockingProcesses(file);

            if (processes.Count > 0)
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        KillLocalProcess(process.ProcessName);
                    }
                    catch (InvalidOperationException ex) // may happen
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                File.Delete(file);
            }
            else
            {
                File.Delete(file);
            }
        }

        private static void KillLocalProcess(string processName)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                process.Kill();
            }
        }


        /// <summary>
        /// Finds all processes that have a lock on the specified file.
        /// </summary>
        private static List<Process> FindLockingProcesses(string path)
        {
            // http://csharphelper.com/blog/2017/01/see-processes-file-locked-c/ 
            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx

            string key = Guid.NewGuid().ToString();
            var processes = new List<Process>();

            int res = RmStartSession(out uint handle, 0, key);
            if (res != 0)
            {
                throw new Exception("Could not begin restart session. Unable to determine file locker.");
            }

            try
            {
                const int ERROR_MORE_DATA = 234;
                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = RmRebootReasonNone;

                res = RmRegisterResources(handle, 1, new string[] { path }, 0, null, 0, null);
                if (res != 0)
                {
                    throw new Exception("Could not register resource.");
                }

                /* There's a potential race around condition here. The first call to RmGetList() returns the total number of process. 
                 * However, when we call RmGetList() again to get the actual processes this number may have increased. */
                res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (res == ERROR_MORE_DATA)
                {
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        processes = new List<Process>((int)pnProcInfo);

                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                            }
                            catch (ArgumentException)
                            {
                                // Occurs in case the process is no longer running
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Could not list processes locking resource.");
                    }
                }
                else if (res != 0)
                {
                    throw new Exception("Could not list processes locking resource. Failed to get size of result.");
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int RmRebootReasonNone = 0;
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;

            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint pSessionHandle, UInt32 nFiles, string[] rgsFilenames, UInt32 nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications, UInt32 nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);
    }
}
