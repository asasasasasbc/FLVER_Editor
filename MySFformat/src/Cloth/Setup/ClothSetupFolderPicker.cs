using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MySFformat
{
    // Windows Common Item Dialog in folder mode (Explorer UI, not the legacy tree dialog).
    internal static class ClothSetupFolderPicker
    {
        internal static string Show(IWin32Window owner)
        {
            IFileOpenDialog dialog=null;IShellItem item=null;IntPtr path=IntPtr.Zero;
            try
            {
                dialog=(IFileOpenDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
                uint options;dialog.GetOptions(out options);
                dialog.SetOptions(options|0x20u|0x40u|0x800u|0x8u); // PICKFOLDERS, FORCEFILESYSTEM, PATHMUSTEXIST, NOCHANGEDIR
                dialog.SetTitle("Choose a parent folder for a new Cloth Setup output folder");
                dialog.SetOkButtonLabel("Select Folder");
                int hr=dialog.Show(owner==null?IntPtr.Zero:owner.Handle);
                if(hr==unchecked((int)0x800704C7))return null;
                if(hr<0)Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out item);item.GetDisplayName(0x80058000u,out path); // SIGDN_FILESYSPATH
                return Marshal.PtrToStringUni(path);
            }
            finally
            {
                if(path!=IntPtr.Zero)Marshal.FreeCoTaskMem(path);
                if(item!=null)Marshal.ReleaseComObject(item);
                if(dialog!=null)Marshal.ReleaseComObject(dialog);
            }
        }
        [ComImport,Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOpenDialog
        {
            [PreserveSig]int Show(IntPtr owner);
            void SetFileTypes(uint count,IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events,out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string text);
            void GetResult(out IShellItem item);
        }
        [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint kind,out IntPtr name);
            void GetAttributes(uint mask,out uint attributes);
            void Compare(IShellItem other,uint hint,out int order);
        }
    }
}
