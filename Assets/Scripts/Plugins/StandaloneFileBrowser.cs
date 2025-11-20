// Unity Standalone File Browser - https://github.com/gkngkc/UnityStandaloneFileBrowser
//
// Native file browser for Unity standalone platforms
//
// Copyright (C) 2017-2022 Gökhan Gökçe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#if UNITY_STANDALONE_OSX
#define NATIVE_FILE_BROWSER_AVAILABLE
#elif UNITY_STANDALONE_WIN
#define NATIVE_FILE_BROWSER_AVAILABLE
#elif UNITY_STANDALONE_LINUX
#define NATIVE_FILE_BROWSER_AVAILABLE
#endif

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AOT;

namespace SFB {
    public struct ExtensionFilter {
        public string Name;
        public string[] Extensions;

        public ExtensionFilter(string filterName, params string[] filterExtensions) {
            Name = filterName;
            Extensions = filterExtensions;
        }
    }

    public class StandaloneFileBrowser {
#if NATIVE_FILE_BROWSER_AVAILABLE
        private static readonly IStandaloneFileBrowser _platformWrapper =
#if UNITY_STANDALONE_OSX
            new StandaloneFileBrowserMac();
#elif UNITY_STANDALONE_WIN
            new StandaloneFileBrowserWindows();
#elif UNITY_STANDALONE_LINUX
            new StandaloneFileBrowserLinux();
#else
            null;
#endif
#endif

        private static Action<string[]> _openFileCallback;
        private static Action<string[]> _openFolderCallback;
        private static Action<string> _saveFileCallback;

        /// <summary>
        /// Native open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="extension">Allowed extension, e.g. "txt"</param>
        /// <param name="multiselect">Allow multiple file selection</param>
        /// <returns>Returns array of chosen paths. Zero length array when cancelled</returns>
        public static string[] OpenFilePanel(string title, string directory, string extension, bool multiselect) {
            var extensions = string.IsNullOrEmpty(extension) ?
                null :
                new [] { new ExtensionFilter("", extension) };
            return OpenFilePanel(title, directory, extensions, multiselect);
        }

        /// <summary>
        /// Native open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="extensions">List of extension filters. Filter Example: new ExtensionFilter("Image Files", "jpg", "png")</param>
        /// <param name="multiselect">Allow multiple file selection</param>
        /// <returns>Returns array of chosen paths. Zero length array when cancelled</returns>
        public static string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            return _platformWrapper.OpenFilePanel(title, directory, extensions, multiselect);
#else
            return new string[0];
#endif
        }

        /// <summary>
        /// Native open file dialog async
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="extension">Allowed extension, e.g. "txt"</param>
        /// <param name="multiselect">Allow multiple file selection</param>
        /// <param name="callback">Callback")</param>
        public static void OpenFilePanelAsync(string title, string directory, string extension, bool multiselect, Action<string[]> callback) {
            var extensions = string.IsNullOrEmpty(extension) ?
                null :
                new [] { new ExtensionFilter("", extension) };
            OpenFilePanelAsync(title, directory, extensions, multiselect, callback);
        }

        /// <summary>
        /// Native open file dialog async
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="extensions">List of extension filters. Filter Example: new ExtensionFilter("Image Files", "jpg", "png")</param>
        /// <param name="multiselect">Allow multiple file selection</param>
        /// <param name="callback">Callback")</param>
        public static void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> callback) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            _openFileCallback = callback;
            _platformWrapper.OpenFilePanelAsync(title, directory, extensions, multiselect, (paths) => {
                _openFileCallback(paths);
            });
#else
            callback(new string[0]);
#endif
        }

        /// <summary>
        /// Native open folder dialog
        /// </summary>
        /// <param name="title"></param>
        /// <param name="directory">Root directory</param>
        /// <param name="multiselect"></param>
        /// <returns>Returns array of chosen paths. Zero length array when cancelled</returns>
        public static string[] OpenFolderPanel(string title, string directory, bool multiselect) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            return _platformWrapper.OpenFolderPanel(title, directory, multiselect);
#else
            return new string[0];
#endif
        }

        /// <summary>
        /// Native open folder dialog async
        /// </summary>
        /// <param name="title"></param>
        /// <param name="directory">Root directory</param>
        /// <param name="multiselect"></param>
        /// <param name="callback"></param>
        public static void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> callback) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            _openFolderCallback = callback;
            _platformWrapper.OpenFolderPanelAsync(title, directory, multiselect, (paths) => {
                _openFolderCallback(paths);
            });
#else
            callback(new string[0]);
#endif
        }

        /// <summary>
        /// Native save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="defaultName">Default file name</param>
        /// <param name="extension">File extension, e.g. "txt"</param>
        /// <returns>Returns chosen path. Empty string when cancelled</returns>
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) {
            var extensions = string.IsNullOrEmpty(extension) ?
                null :
                new [] { new ExtensionFilter("", extension) };
            return SaveFilePanel(title, directory, defaultName, extensions);
        }

        /// <summary>
        /// Native save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="defaultName">Default file name</param>
        /// <param name="extensions">List of extension filters. Filter Example: new ExtensionFilter("Image Files", "jpg", "png")</param>
        /// <returns>Returns chosen path. Empty string when cancelled</returns>
        public static string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            return _platformWrapper.SaveFilePanel(title, directory, defaultName, extensions);
#else
            return "";
#endif
        }

        /// <summary>
        /// Native save file dialog async
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="defaultName">Default file name</param>
        /// <param name="extension">File extension, e.g. "txt"</param>
        /// <param name="callback">Callback")</param>
        public static void SaveFilePanelAsync(string title, string directory, string defaultName, string extension, Action<string> callback) {
            var extensions = string.IsNullOrEmpty(extension) ?
                null :
                new [] { new ExtensionFilter("", extension) };
            SaveFilePanelAsync(title, directory, defaultName, extensions, callback);
        }

        /// <summary>
        /// Native save file dialog async
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="directory">Root directory</param>
        /// <param name="defaultName">Default file name</param>
        /// <param name="extensions">List of extension filters. Filter Example: new ExtensionFilter("Image Files", "jpg", "png")</param>
        /// <param name="callback">Callback")</param>
        public static void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> callback) {
#if NATIVE_FILE_BROWSER_AVAILABLE
            _saveFileCallback = callback;
            _platformWrapper.SaveFilePanelAsync(title, directory, defaultName, extensions, (path) => {
                _saveFileCallback(path);
            });
#else
            callback("");
#endif
        }
    }

    public interface IStandaloneFileBrowser {
        string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect);
        void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb);
        string[] OpenFolderPanel(string title, string directory, bool multiselect);
        void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb);
        string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions);
        void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb);
    }

#if UNITY_STANDALONE_WIN

    public class StandaloneFileBrowserWindows : IStandaloneFileBrowser {
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            var fd = new OpenFileName();
            fd.structSize = Marshal.SizeOf(fd);
            fd.title = title;
            fd.file = new string(new char[2048]);
            fd.maxFile = fd.file.Length;
            fd.fileTitle = new string(new char[256]);
            fd.maxFileTitle = fd.fileTitle.Length;
            fd.filter = GetFilterFromFileExtensionList(extensions);
            fd.initialDir = directory;
            fd.flags =
                (int)FileOpenDialogFlags.OFN_EXPLORER |
                (int)FileOpenDialogFlags.OFN_FILEMUSTEXIST |
                (int)FileOpenDialogFlags.OFN_PATHMUSTEXIST |
                (int)FileOpenDialogFlags.OFN_NOCHANGEDIR |
                (int)FileOpenDialogFlags.OFN_HIDEREADONLY;

            if (multiselect) {
                fd.flags |= (int)FileOpenDialogFlags.OFN_ALLOWMULTISELECT;
            }

            if (Comdlg32.GetOpenFileName(fd)) {
                return ParseResults(fd.file);
            }
            return new string[0];
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var paths = OpenFilePanel(title, directory, extensions, multiselect);
                mainThread.Post(d => cb(paths), null);
            });
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            var fd = new FileBrowserDialog();
            fd.pDisplayName = new string(new char[2048]);
            fd.pszDisplayName = Marshal.StringToHGlobalAuto(fd.pDisplayName);
            fd.lpszTitle = title;
            fd.ulFlags = (uint)BrowseInfoFlags.BIF_NEWDIALOGSTYLE | (uint)BrowseInfoFlags.BIF_RETURNONLYFSDIRS;
            fd.lpfn = new BFFCALLBACK(BrowseCallbackProc);

            var pidl = Shell32.SHBrowseForFolder(fd);

            if (pidl == IntPtr.Zero) {
                return new string[0];
            }

            var path = new char[256];
            Shell32.SHGetPathFromIDList(pidl, path);
            Marshal.FreeCoTaskMem(pidl);

            return new[] { new string(path) };
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var paths = OpenFolderPanel(title, directory, multiselect);
                mainThread.Post(d => cb(paths), null);
            });
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            var fd = new OpenFileName();
            fd.structSize = Marshal.SizeOf(fd);
            fd.title = title;
            fd.file = defaultName + "\0" + new string(new char[2048]);
            fd.maxFile = fd.file.Length;
            fd.fileTitle = new string(new char[256]);
            fd.maxFileTitle = fd.fileTitle.Length;
            fd.filter = GetFilterFromFileExtensionList(extensions);
            fd.initialDir = directory;
            fd.defExt = extensions != null ? extensions[0].Extensions[0] : "";
            fd.flags =
                (int)FileOpenDialogFlags.OFN_EXPLORER |
                (int)FileOpenDialogFlags.OFN_PATHMUSTEXIST |
                (int)FileOpenDialogFlags.OFN_OVERWRITEPROMPT |
                (int)FileOpenDialogFlags.OFN_NOCHANGEDIR |
                (int)FileOpenDialogFlags.OFN_HIDEREADONLY;

            if (Comdlg32.GetSaveFileName(fd)) {
                return fd.file;
            }
            return "";
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var path = SaveFilePanel(title, directory, defaultName, extensions);
                mainThread.Post(d => cb(path), null);
            });
        }

        [MonoPInvokeCallback(typeof(BFFCALLBACK))]
        private static int BrowseCallbackProc(IntPtr hwnd, uint uMsg, IntPtr lParam, IntPtr lpData)
        {
            if (uMsg == (uint)BrowseInfoFlags.BIF_INITIALIZED)
            {
                Shell32.SendMessage(hwnd, (uint)BrowseInfoFlags.BFFM_SETSELECTION, 1, lpData);
            }

            return 0;
        }

        private static string GetFilterFromFileExtensionList(ExtensionFilter[] extensions) {
            if (extensions == null) {
                return "";
            }
            var filterString = "";
            foreach (var filter in extensions) {
                filterString += filter.Name;
                filterString += "\0";
                filterString += string.Join(";", filter.Extensions.Select(e => "*." + e));
                filterString += "\0";
            }
            filterString += "\0";
            return filterString;
        }

        private static string[] ParseResults(string a_results) {
            if (string.IsNullOrEmpty(a_results)) {
                return new string[0];
            }

            var directory = "";
            var filenames = new List<string>();

            var fileArray = a_results.Split('\0');

            if (fileArray.Length > 1) { // multiple files
                directory = fileArray[0];
                for (var i = 1; i < fileArray.Length; i++) {
                    var filename = fileArray[i];
                    if (filename.Length > 0) {
                        filenames.Add(Path.Combine(directory, filename));
                    }
                }
            } else { // single file
                filenames.Add(fileArray[0]);
            }

            return filenames.ToArray();
        }
    }

#elif UNITY_STANDALONE_OSX

    public class StandaloneFileBrowserMac : IStandaloneFileBrowser {
        private static Action<string[]> _openFileCb;
        private static Action<string[]> _openFolderCb;
        private static Action<string> _saveFileCb;

        [DllImport("StandaloneFileBrowser")]
        private static extern IntPtr DialogOpenFilePanel(string title, string directory, string extension, bool multiselect);
        [DllImport("StandaloneFileBrowser")]
        private static extern void DialogOpenFilePanelAsync(string title, string directory, string extension, bool multiselect, Action<string> callback);
        [DllImport("StandaloneFileBrowser")]
        private static extern IntPtr DialogOpenFolderPanel(string title, string directory, bool multiselect);
        [DllImport("StandaloneFileBrowser")]
        private static extern void DialogOpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string> callback);
        [DllImport("StandaloneFileBrowser")]
        private static extern IntPtr DialogSaveFilePanel(string title, string directory, string defaultName, string extension);
        [DllImport("StandaloneFileBrowser")]
        private static extern void DialogSaveFilePanelAsync(string title, string directory, string defaultName, string extension, Action<string> callback);

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            var result = Marshal.PtrToStringAnsi(DialogOpenFilePanel(
                title,
                directory,
                GetFilterFromFileExtensionList(extensions),
                multiselect));
            return GetPaths(result);
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            _openFileCb = cb;
            DialogOpenFilePanelAsync(
                title,
                directory,
                GetFilterFromFileExtensionList(extensions),
                multiselect,
                (result) => _openFileCb(GetPaths(result))
            );
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            var result = Marshal.PtrToStringAnsi(DialogOpenFolderPanel(
                title,
                directory,
                multiselect));
            return GetPaths(result);
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            _openFolderCb = cb;
            DialogOpenFolderPanelAsync(
                title,
                directory,
                multiselect,
                (result) => _openFolderCb(GetPaths(result))
            );
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            return Marshal.PtrToStringAnsi(DialogSaveFilePanel(
                title,
                directory,
                defaultName,
                GetFilterFromFileExtensionList(extensions)));
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            _saveFileCb = cb;
            DialogSaveFilePanelAsync(
                title,
                directory,
                defaultName,
                GetFilterFromFileExtensionList(extensions),
                (result) => _saveFileCb(result)
            );
        }

        private static string GetFilterFromFileExtensionList(ExtensionFilter[] extensions) {
            if (extensions == null) {
                return "";
            }
            return string.Join(";", extensions.SelectMany(e => e.Extensions).Distinct());
        }

        private static string[] GetPaths(string result) {
            if (string.IsNullOrEmpty(result)) {
                return new string[0];
            }
            return result.Split((char)28);
        }
    }

#elif UNITY_STANDALONE_LINUX

    public class StandaloneFileBrowserLinux : IStandaloneFileBrowser {
        private const string Zenity = "zenity";

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            var args = new List<string> {
                "--file-selection",
                "--title", title,
                "--filename", Path.GetFullPath(directory) + Path.DirectorySeparatorChar
            };
            if (multiselect) {
                args.Add("--multiple");
            }
            if (extensions != null) {
                foreach (var filter in extensions) {
                    args.Add("--file-filter");
                    var patterns = string.Join(" ", filter.Extensions.Select(e => "*." + e).ToArray());
                    args.Add(string.Format("{0} | {1}", filter.Name, patterns));
                }
            }
            var results = ExecuteZenity(args.ToArray());
            return ParseResults(results);
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var paths = OpenFilePanel(title, directory, extensions, multiselect);
                mainThread.Post(d => cb(paths), null);
            });
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            var args = new List<string> {
                "--file-selection",
                "--directory",
                "--title", title,
                "--filename", Path.GetFullPath(directory) + Path.DirectorySeparatorChar
            };
            if (multiselect) {
                args.Add("--multiple");
            }
            var results = ExecuteZenity(args.ToArray());
            return ParseResults(results);
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var paths = OpenFolderPanel(title, directory, multiselect);
                mainThread.Post(d => cb(paths), null);
            });
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            var args = new List<string> {
                "--file-selection",
                "--save",
                "--title", title,
                "--filename", Path.Combine(Path.GetFullPath(directory), defaultName)
            };
            if (extensions != null) {
                foreach (var filter in extensions) {
                    args.Add("--file-filter");
                    var patterns = string.Join(" ", filter.Extensions.Select(e => "*." + e).ToArray());
                    args.Add(string.Format("{0} | {1}", filter.Name, patterns));
                }
            }
            var result = ExecuteZenity(args.ToArray());
            return result.Trim();
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            var mainThread = SynchronizationContext.Current;
            Task.Run(() => {
                var path = SaveFilePanel(title, directory, defaultName, extensions);
                mainThread.Post(d => cb(path), null);
            });
        }

        private static string ExecuteZenity(params string[] args) {
            using (var process = new System.Diagnostics.Process()) {
                process.StartInfo.FileName = Zenity;
                process.StartInfo.Arguments = string.Join(" ", args.Select(a => EscapeArgument(a)).ToArray());
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                var result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }
        }

        private static string EscapeArgument(string arg) {
            return "'" + arg.Replace("'", @"'\''") + "'";
        }

        private static string[] ParseResults(string results) {
            if (string.IsNullOrEmpty(results)) {
                return new string[0];
            }
            // Trim \n from end
            return results.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

#endif
}

#if UNITY_STANDALONE_WIN
namespace SFB {
    internal enum FileOpenDialogFlags : int
    {
        OFN_READONLY = 0x00000001,
        OFN_OVERWRITEPROMPT = 0x00000002,
        OFN_HIDEREADONLY = 0x00000004,
        OFN_NOCHANGEDIR = 0x00000008,
        OFN_SHOWHELP = 0x00000010,
        OFN_ENABLEHOOK = 0x00000020,
        OFN_ENABLETEMPLATE = 0x00000040,
        OFN_ENABLETEMPLATEHANDLE = 0x00000080,
        OFN_NOVALIDATE = 0x00000100,
        OFN_ALLOWMULTISELECT = 0x00000200,
        OFN_EXTENSIONDIFFERENT = 0x00000400,
        OFN_PATHMUSTEXIST = 0x00000800,
        OFN_FILEMUSTEXIST = 0x00001000,
        OFN_CREATEPROMPT = 0x00002000,
        OFN_SHAREAWARE = 0x00004000,
        OFN_NOREADONLYRETURN = 0x00008000,
        OFN_NOTESTFILECREATE = 0x00010000,
        OFN_NONETWORKBUTTON = 0x00020000,
        OFN_NOLONGNAMES = 0x00040000,
        OFN_ENABLEINCLUDENOTIFY = 0x00400000,
        OFN_ENABLESIZING = 0x00800000,
        OFN_DONTADDTORECENT = 0x02000000,
        OFN_FORCESHOWHIDDEN = 0x10000000,
        OFN_EX_NOPLACESBAR = 0x00000001,
        OFN_EX_ENABLESIZING = 0x00000008,
        OFN_EXPLORER = 0x00080000,
    }

    [Flags]
    internal enum BrowseInfoFlags : uint
    {
        BIF_RETURNEIDL = 0x00000001,
        BIF_NEWDIALOGSTYLE = 0x00000040,
        BIF_RETURNONLYFSDIRS = 0x00000001,
        BIF_BROWSEINCLUDEFILES = 0x00004000,
        BIF_INITIALIZED = 0x0001,
        BFFM_SETSELECTION = 0x467,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class OpenFileName {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter = null;
        public string customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string file = null;
        public int maxFile = 0;
        public string fileTitle = null;
        public int maxFileTitle = 0;
        public string initialDir = null;
        public string title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class FileBrowserDialog {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public BFFCALLBACK lpfn;
        public IntPtr lParam;
        public int iImage;
        public string pDisplayName;
    }

    public delegate int BFFCALLBACK(IntPtr hwnd, uint uMsg, IntPtr lParam, IntPtr lpData);

    public class Comdlg32 {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetSaveFileName([In, Out] OpenFileName ofn);
    }

    public class Shell32 {
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SHBrowseForFolder([In, Out] FileBrowserDialog lpbi);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, [In, Out] char[] pszPath);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);
    }
}
#endif
