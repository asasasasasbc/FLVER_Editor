// GPL-3.0. Process-local offline guard. Changes only this child's Winsock import
// bindings, never vendor files, solver instructions, global hooks or firewall rules.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MySFformat.Cloth.Preview
{
    internal sealed class HostNetworkGuard : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualProtect(IntPtr address,UIntPtr bytes,uint protection,out uint previous);
        [DllImport("ws2_32.dll")] static extern void WSASetLastError(int error);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr SocketCall(int af,int type,int protocol);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr WsaSocketCall(int af,int type,int protocol,IntPtr info,uint group,uint flags);
        readonly SocketCall socket;
        readonly WsaSocketCall wsaSocket;
        readonly List<Tuple<IntPtr,IntPtr>> originals=new List<Tuple<IntPtr,IntPtr>>();
        readonly HashSet<IntPtr> seen=new HashSet<IntPtr>();
        int denied;
        public int DeniedSocketCalls { get { return denied; } }
        public int GuardedImports { get { return originals.Count; } }
        public HostNetworkGuard()
        {
            socket=delegate(int af,int type,int protocol){return Deny();};
            wsaSocket=delegate(int af,int type,int protocol,IntPtr info,uint group,uint flags){return Deny();};
            AppDomain.CurrentDomain.AssemblyLoad+=Loaded;
        }
        IntPtr Deny(){Interlocked.Increment(ref denied);WSASetLastError(10013);return new IntPtr(-1);}
        void Loaded(object sender,AssemblyLoadEventArgs e){GuardLoadedModules();}
        static bool Vendor(string name)
        {
            return name.Equals("hctPreviewPlugin.dll",StringComparison.OrdinalIgnoreCase)||name.Equals("hkCompatFormats.dll",StringComparison.OrdinalIgnoreCase)||
                name.StartsWith("Havok.",StringComparison.OrdinalIgnoreCase);
        }
        static void Write(IntPtr slot,IntPtr value)
        {
            uint old;if(!VirtualProtect(slot,(UIntPtr)8,4,out old))throw new System.ComponentModel.Win32Exception();
            try{Marshal.WriteIntPtr(slot,value);}finally{uint ignored;VirtualProtect(slot,(UIntPtr)8,old,out ignored);}
        }
        public void GuardLoadedModules()
        {
            using(var process=Process.GetCurrentProcess())foreach(ProcessModule module in process.Modules)
            {
                if(!Vendor(module.ModuleName))continue;var basis=module.BaseAddress;if(!seen.Add(basis))continue;
                int size=module.ModuleMemorySize;
                Func<int,int,IntPtr> Address=delegate(int offset,int length){if(offset<0||length<0||(long)offset+length>size)throw new InvalidOperationException("Invalid imported-module range");return IntPtr.Add(basis,offset);};
                int nt=Marshal.ReadInt32(Address(0x3c,4));if(Marshal.ReadInt16(Address(nt+24,2))!=0x20b)throw new InvalidOperationException("Offline guard requires PE32+");
                int imports=Marshal.ReadInt32(Address(nt+24+120,4));if(imports==0)continue;
                for(int d=0;d<4096;d++)
                {
                    var descriptor=Address(imports+d*20,20);int name=Marshal.ReadInt32(descriptor,12);if(name==0)break;
                    if(!Marshal.PtrToStringAnsi(Address(name,1)).Equals("ws2_32.dll",StringComparison.OrdinalIgnoreCase))continue;
                    int lookup=Marshal.ReadInt32(descriptor),iat=Marshal.ReadInt32(descriptor,16);if(lookup==0)throw new InvalidOperationException("Offline guard requires import names");
                    for(int i=0;i<4096;i++)
                    {
                        long symbol=Marshal.ReadInt64(Address(lookup+i*8,8));if(symbol==0)break;if(symbol<0)continue;
                        string function=Marshal.PtrToStringAnsi(Address(checked((int)symbol)+2,1));IntPtr replacement=IntPtr.Zero;
                        if(function=="socket")replacement=Marshal.GetFunctionPointerForDelegate(socket);
                        if(function=="WSASocketW"||function=="WSASocketA")replacement=Marshal.GetFunctionPointerForDelegate(wsaSocket);
                        if(replacement==IntPtr.Zero)continue;var slot=Address(iat+i*8,8);originals.Add(Tuple.Create(slot,Marshal.ReadIntPtr(slot)));Write(slot,replacement);
                    }
                }
            }
        }
        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyLoad-=Loaded;
            for(int i=originals.Count-1;i>=0;i--)Write(originals[i].Item1,originals[i].Item2);
            originals.Clear();GC.KeepAlive(socket);GC.KeepAlive(wsaSocket);
        }
    }
}
