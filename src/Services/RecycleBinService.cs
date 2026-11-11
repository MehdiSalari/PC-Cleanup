using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PcCleanupApp
{
    public static class RecycleBinService
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static long SizeOnDrive(string driveRoot)
        {
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                int hr = SHQueryRecycleBin(driveRoot, ref info);
                if (hr == 0) return info.i64Size;
            }
            catch { }
            return -1;
        }

        public static long SizeAllFixedDrives()
        {
            long total = 0;
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                long size = SizeOnDrive(d.Name);
                if (size > 0) total += size;
            }
            return total;
        }

        public static void EmptyAll()
        {
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.DriveType == DriveType.Fixed && d.IsReady)
                        SHEmptyRecycleBin(IntPtr.Zero, d.Name, 0x7);
                }
            }
            catch { }
        }
    }
}


