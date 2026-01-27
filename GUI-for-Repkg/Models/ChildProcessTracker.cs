using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GUI_for_Repkg.Models
{
    public static class JobObjectManager
    {
        private static readonly IntPtr _jobHandle;

        static JobObjectManager()
        {
            // 1. 创建 Job Object
            _jobHandle = CreateJobObject(IntPtr.Zero, null);

            // 2. 配置 Job Object：主进程结束时，强制结束所有子进程
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = info
            };

            int length = Marshal.SizeOf(extendedInfo);
            IntPtr ptr = Marshal.AllocHGlobal(length);

            try
            {
                Marshal.StructureToPtr(extendedInfo, ptr, false);

                // InfoClass 9 = JobObjectExtendedLimitInformation
                if (!SetInformationJobObject(_jobHandle, 9, ptr, (uint)length))
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[JobObject] 警告: 设置 Job 属性失败。错误代码: {error}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static void AddProcess(IntPtr processHandle)
        {
            if (_jobHandle == IntPtr.Zero) return;

            // 尝试将进程绑定到作业对象
            if (!AssignProcessToJobObject(_jobHandle, processHandle))
            {
                int error = Marshal.GetLastWin32Error();
                // 忽略 "Access Denied" (5) 如果进程已经退出
                // 忽略 "The parameter is incorrect" (87) 如果进程已经在Job中
                Debug.WriteLine($"[JobObject] 无法绑定进程 {processHandle}。错误代码: {error}");
            }
        }

        #region Win32 API 导入

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }
        #endregion
    }
}