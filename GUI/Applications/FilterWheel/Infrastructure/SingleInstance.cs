using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FilterWheel.Infrastructure
{
    internal sealed class SingleInstance
    {
        public static bool AlreadyRunning()
        {
            bool running = false;
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcesses())
                {
                    if (p.Id != currentProcess.Id)
                    {
                        if (p.ProcessName == currentProcess.ProcessName)
                        {
                            running = true;
                            IntPtr hFound = p.MainWindowHandle;
                            if (IsIconic(hFound))
                            {
                                ShowWindow(hFound, SW_RESTORE);
                            }
                            SetForegroundWindow(hFound);
                            break;
                        }
                    }
                }
            }
            catch { }
            return running;
        }

        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;
    }
}
