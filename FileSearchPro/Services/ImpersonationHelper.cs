using System.Net;
using System.Runtime.InteropServices;

namespace FileSearchPro.Services;

public static class ImpersonationHelper
{
    private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
    private const int LOGON32_PROVIDER_WINNT50 = 3;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUserW(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool RevertToSelf();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public static T RunAs<T>(NetworkCredential credential, Func<T> func)
    {
        if (string.IsNullOrEmpty(credential.UserName))
            return func();

        IntPtr tokenHandle = IntPtr.Zero;
        try
        {
            bool loggedOn = LogonUserW(
                credential.UserName,
                credential.Domain ?? "",
                credential.Password ?? "",
                LOGON32_LOGON_NEW_CREDENTIALS,
                LOGON32_PROVIDER_WINNT50,
                out tokenHandle);

            if (!loggedOn)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine(
                    $"[ImpersonationHelper] LogonUserW failed for '{credential.Domain}\\{credential.UserName}': Win32Error={error}");
                return func();
            }

            if (!ImpersonateLoggedOnUser(tokenHandle))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine(
                    $"[ImpersonationHelper] ImpersonateLoggedOnUser failed: Win32Error={error}");
                return func();
            }

            try
            {
                return func();
            }
            finally
            {
                RevertToSelf();
            }
        }
        finally
        {
            if (tokenHandle != IntPtr.Zero)
                CloseHandle(tokenHandle);
        }
    }

    public static void RunAs(NetworkCredential credential, Action action)
    {
        RunAs<object>(credential, () => { action(); return null!; });
    }
}
