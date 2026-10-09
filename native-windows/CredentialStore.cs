using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SnkMessage
{
    internal static class CredentialStore
    {
        private const string Target="SnkMessage/OpenRouterApiKey";
        private const uint Generic=1;
        private const uint LocalMachinePersistence=2;

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags; public uint Type; public string TargetName; public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist;
            public uint AttributeCount; public IntPtr Attributes; public string TargetAlias; public string UserName;
        }

        [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern bool CredWrite(ref NativeCredential credential,uint flags);
        [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
        [DllImport("advapi32.dll",SetLastError=true)]
        private static extern void CredFree(IntPtr buffer);

        internal static bool HasApiKey { get { return !String.IsNullOrWhiteSpace(ReadApiKey()); } }

        internal static void SaveApiKey(string value)
        {
            if(String.IsNullOrWhiteSpace(value))throw new ArgumentException("API Key 不能为空。",nameof(value));
            byte[] bytes=Encoding.Unicode.GetBytes(value.Trim());
            IntPtr blob=Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes,0,blob,bytes.Length);
                var credential=new NativeCredential{Type=Generic,TargetName=Target,CredentialBlobSize=(uint)bytes.Length,CredentialBlob=blob,Persist=LocalMachinePersistence,UserName=Environment.UserName};
                if(!CredWrite(ref credential,0))throw new InvalidOperationException("无法保存 API Key，Windows 错误："+Marshal.GetLastWin32Error());
            }
            finally{Marshal.FreeCoTaskMem(blob);}
        }

        internal static string ReadApiKey()
        {
            IntPtr pointer;
            if(!CredRead(Target,Generic,0,out pointer))return String.Empty;
            try
            {
                var credential=(NativeCredential)Marshal.PtrToStructure(pointer,typeof(NativeCredential));
                if(credential.CredentialBlob==IntPtr.Zero||credential.CredentialBlobSize==0)return String.Empty;
                byte[] bytes=new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob,bytes,0,bytes.Length);
                return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            }
            finally{CredFree(pointer);}
        }
    }
}
