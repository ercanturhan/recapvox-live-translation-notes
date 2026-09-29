using System.Runtime.InteropServices;
using System.Text;

namespace Translator.Desktop;

internal static class CredentialStore
{
    private const string Target = "Translator/Soniox";
    private const uint Generic = 1;
    private const uint LocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential);

    public static void Write(string value) => WriteTarget(Target, value);

    public static void WriteForProvider(string provider, string value) => WriteTarget(ProviderTarget(provider), value);
    public static void WriteLiveDeepSeek(string value) => WriteTarget("Translator/Live/DeepSeek", value);
    public static string? ReadLiveDeepSeek() => ReadTarget("Translator/Live/DeepSeek") ?? ReadForProvider("DeepSeek");

    private static void WriteTarget(string target, string value)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(value);
        if (bytes.Length > 5120) throw new ArgumentException("API anahtarı çok uzun.");
        IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = Generic, TargetName = target, CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob, Persist = LocalMachine, UserName = Environment.UserName
            };
            if (!CredWrite(ref credential, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            for (int i = 0; i < bytes.Length; i++) bytes[i] = 0;
            for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
            Marshal.FreeHGlobal(blob);
        }
    }

    public static string? Read() => ReadTarget(Target);

    public static string? ReadForProvider(string provider) => ReadTarget(ProviderTarget(provider));

    private static string ProviderTarget(string provider) => provider switch
    {
        "OpenAI" or "DeepSeek" or "Gemini" or "Claude" => "Translator/Assistant/" + provider,
        _ => throw new ArgumentException("Bilinmeyen sağlayıcı.", nameof(provider))
    };

    private static string? ReadTarget(string target)
    {
        if (!CredRead(target, Generic, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally { CredFree(pointer); }
    }
}
