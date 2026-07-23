using System.Runtime.InteropServices;
using System.Text;

namespace FileSearchPro.Services;

public static class ShareEnumerator
{
    private const int MAX_PREFERRED_LENGTH = -1;
    private const int NERR_Success = 0;
    private const int ERROR_MORE_DATA = 234;
    private const uint STYPE_DISKTREE = 0;
    private const uint STYPE_SPECIAL = 0x80000000;

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetShareEnum(
        string serverName,
        int level,
        out IntPtr bufPtr,
        int prefMaxLen,
        out int entriesRead,
        out int totalEntries,
        ref int resumeHandle);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHARE_INFO_1
    {
        public string shi1_netname;
        public uint shi1_type;
        public string shi1_remark;
    }

    public static List<string> EnumerateShares(string ip)
    {
        var shares = new List<string>();
        IntPtr bufPtr = IntPtr.Zero;
        int entriesRead = 0;
        int totalEntries = 0;
        int resumeHandle = 0;

        try
        {
            int result = NetShareEnum(
                $"\\\\{ip}",
                1,
                out bufPtr,
                MAX_PREFERRED_LENGTH,
                out entriesRead,
                out totalEntries,
                ref resumeHandle);

            if (result != NERR_Success && result != ERROR_MORE_DATA)
                return shares;

            if (bufPtr == IntPtr.Zero || entriesRead == 0)
                return shares;

            IntPtr current = bufPtr;
            int structSize = Marshal.SizeOf<SHARE_INFO_1>();

            for (int i = 0; i < entriesRead; i++)
            {
                var info = Marshal.PtrToStructure<SHARE_INFO_1>(current);

                // Filter: only disk tree shares (skip IPC$, printer shares, etc.)
                // C$, D$, Users have STYPE_DISKTREE | STYPE_SPECIAL
                if ((info.shi1_type & 0xFF) == STYPE_DISKTREE)
                {
                    var name = info.shi1_netname?.TrimEnd('\0');
                    if (!string.IsNullOrEmpty(name))
                        shares.Add(name);
                }

                current += structSize;
            }
        }
        finally
        {
            if (bufPtr != IntPtr.Zero)
                NetApiBufferFree(bufPtr);
        }

        return shares;
    }
}
