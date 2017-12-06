﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using DokanNet;
using GinClientLibrary.Extensions;
using Newtonsoft.Json;
using static GinClientLibrary.DokanInterface;

namespace GinClientLibrary
{
    [DataContract]
    public class GinRepository : GinRepositoryData, IDisposable
    {
        /// <summary>
        ///     A Dokan driver interface
        /// </summary>
        private DokanInterface DokanInterface { get; }

        public enum FileStatus
        {
            InAnnex,
            InAnnexModified,
            OnDisk,
            OnDiskModified,
            Unknown,
            Directory,
            Unlocked,
            Removed
        }

        private static readonly StringBuilder Output = new StringBuilder("");
        

        private Dictionary<string, FileStatus> _scache;

        public GinRepository(DirectoryInfo physicalDirectory, DirectoryInfo mountpoint, string name, string commandline)
        {
            PhysicalDirectory = physicalDirectory;
            Mountpoint = mountpoint;
            Name = name;
            Commandline = commandline;
            DokanInterface = new DokanInterface(this, false);
            DokanInterface.FileOperationStarted += DokanInterface_FileOperationStarted;
            DokanInterface.FileOperationCompleted += DokanInterface_FileOperationCompleted;
            
        }

        public GinRepository(GinRepositoryData data)
        {
            Mountpoint = data.Mountpoint;
            PhysicalDirectory = data.PhysicalDirectory;
            Name = data.Name;
            Commandline = data.Commandline;
        }

        public Dictionary<string, FileStatus> StatusCache =>
            _scache ?? (_scache = new Dictionary<string, FileStatus>());

        public void DownloadUpdateInfo()
        {
            //During download, we need to unmount the repository, since gin can and will make drastic changes
            Dokan.RemoveMountPoint(Mountpoint.FullName.Trim('\\'));

            GetCommandLineOutput("cmd.exe", "/C gin.exe download", PhysicalDirectory.FullName, out var error);

            if (string.IsNullOrEmpty(error))
                Mount();
            else
                OnFileOperationError(error);
        }

        public void Initialize()
        {
            if (!Directory.Exists(Mountpoint.FullName))
                Directory.CreateDirectory(Mountpoint.FullName);

            ReadRepoStatus();
        }

        public void Mount()
        {
            try
            {
                DokanInterface.Initialize();
            }
            catch (Exception e)
            {
                OnFileOperationError(e.Message);
            }
        }

        private FileStatus TranslateFileStatus(string status)
        {
            if (string.CompareOrdinal(status, "OK") == 0)
                return FileStatus.OnDisk;
            if (string.CompareOrdinal(status, "NC") == 0)
                return FileStatus.InAnnex;
            if (string.CompareOrdinal(status, "MD") == 0)
                return FileStatus.InAnnexModified;
            if (string.CompareOrdinal(status, "LC") == 0)
                return FileStatus.OnDiskModified;
            if (string.CompareOrdinal(status, "RC") == 0)
                return FileStatus.InAnnexModified;
            if (string.CompareOrdinal(status, "UL") == 0)
                return FileStatus.Unlocked;
            if (string.CompareOrdinal(status, "RM") == 0)
                return FileStatus.Removed;
            if (string.CompareOrdinal(status, "??") == 0)
                return FileStatus.Unknown;

            return FileStatus.Unknown;
        }

        /// <summary>
        ///     Retrieve the status of every file in this repository
        ///     Possible statuses are:
        ///     -In Annex
        ///     -In Annex, modified remotely
        ///     -On Disk
        ///     -On Disk, modified
        ///     -Unknown (this includes files not yet added to the gin working tree)
        /// </summary>
        private void ReadRepoStatus()
        {
            lock (this)
            {
                var output = GetCommandLineOutput("cmd.exe", "/c gin.exe ls --json", PhysicalDirectory.FullName,
                    out var error);

                if (!string.IsNullOrEmpty(error))
                {
                    OnFileOperationError(error);
                    return;
                }

                var statusCollection = JsonConvert.DeserializeObject<List<Filestatus>>(output);

                foreach (var fstatus in statusCollection)
                {
                    var filePath =
                        Path.GetFullPath(PhysicalDirectory.FullName + Path.DirectorySeparatorChar + fstatus.filename);
                    var status = TranslateFileStatus(fstatus.status);

                    if (!StatusCache.ContainsKey(filePath.ToLowerInvariant()))
                        StatusCache.Add(filePath.ToLowerInvariant(), status);
                    else
                        StatusCache[filePath.ToLowerInvariant()] = status;
                }
            }
        }

        public FileStatus GetFileStatus(string filePath)
        {
            lock (this)
            {
                if (Directory.Exists(filePath))
                    return FileStatus.Directory;

                if (!File.Exists(filePath))
                    return FileStatus.Unknown;

                //Need to normalize the path here
                GetActualFilename(filePath, out var directoryName, out var filename);

                filePath = directoryName + Path.DirectorySeparatorChar + filename;

                if (StatusCache.ContainsKey(filePath.ToLowerInvariant()))
                    return StatusCache[filePath.ToLowerInvariant()];


                //Windows will sometimes try to inspect the contents of a zip file; we need to catch this here and return the filestatus of the zip
                var parentDirectory = Directory.GetParent(filePath).FullName;
                if (parentDirectory.ToLower().Contains(".zip"))
                    return GetFileStatus(parentDirectory);

                return FileStatus.Unknown;
            }
        }

        public bool RetrieveFile(string filePath)
        {
            GetActualFilename(filePath, out var directoryName, out var filename);

            lock (this)
            {
                GetCommandLineOutputEvent("cmd.exe", "/c gin.exe get-content " + filename + " --json", directoryName,
                    out var error);
                

                ReadRepoStatus();

                return string.IsNullOrEmpty(error);
            }
        }


        public bool RemoveFile(string filePath)
        {
            GetActualFilename(filePath, out var directoryName, out var filename);

            lock (this)
            {
                GetCommandLineOutput("cmd.exe", "/C gin.exe remove-content " + filename /*+ " -json"*/,
                    directoryName, out var error);

                Output.Clear();

                ReadRepoStatus();

                if (!string.IsNullOrEmpty(error))
                {
                    OnFileOperationError(error);
                    return false;
                }

                return true;
            }
        }

        //TODO: Implement this
        /// <summary>
        ///     Creates a new repository folder from scratch
        /// </summary>
        /// <returns></returns>
        public void CreateDirectories()
        {
            if (!Directory.Exists(PhysicalDirectory.Parent.FullName))
                Directory.CreateDirectory(PhysicalDirectory.Parent.FullName);
            if (!Directory.Exists(Mountpoint.FullName))
                Directory.CreateDirectory(Mountpoint.FullName);

            if (PhysicalDirectory.IsEmpty())
                GetCommandLineOutputEvent("cmd.exe", "/C gin.exe get " + Commandline + " --json", PhysicalDirectory.Parent.FullName, out string error);
        }

        public void DeleteRepository()
        {
            PhysicalDirectory.Empty();
            Mountpoint.Empty();

            Directory.Delete(PhysicalDirectory.FullName);
            Directory.Delete(Mountpoint.FullName);
        }

        private struct Filestatus
        {
            public string filename { get; set; }
            public string status { get; set; }
        }

        #region Properties

        #endregion

        #region Dokan Interface Events

        public event FileOperationStartedHandler FileOperationStarted;

        public delegate void FileOperationStartedHandler(object sender, FileOperationEventArgs e);

        protected virtual void OnFileOperationStarted(FileOperationEventArgs e)
        {
            FileOperationStarted?.Invoke(this, e);
        }

        public event FileOperationCompleteHandler FileOperationCompleted;

        public delegate void FileOperationCompleteHandler(object sender, FileOperationEventArgs e);

        protected virtual void OnFileOperationCompleted(FileOperationEventArgs e)
        {
            FileOperationCompleted?.Invoke(this, e);
        }

        private void DokanInterface_FileOperationCompleted(object sender, FileOperationEventArgs e)
        {
            OnFileOperationCompleted(e);
        }

        private void DokanInterface_FileOperationStarted(object sender, FileOperationEventArgs e)
        {
            OnFileOperationStarted(e);
        }

        public class FileOperationErrorEventArgs : EventArgs
        {
            public string RepositoryName { get; set; }
            public string Message { get; set; }
        }

        public event FileOperationErrorHandler FileOperationError;

        public delegate void FileOperationErrorHandler(object sender, FileOperationErrorEventArgs e);

        protected virtual void OnFileOperationError(string message)
        {
            FileOperationError?.Invoke(this,
                new FileOperationErrorEventArgs {RepositoryName = Name, Message = message});
        }

        #endregion

        #region Helpers

        private void GetActualFilename(string filePath, out string directoryName, out string filename)
        {
            directoryName = Directory.GetParent(filePath).FullName;
            filename = Directory.GetFiles(directoryName)
                .Single(s => string.CompareOrdinal(s.ToUpperInvariant(), filePath.ToUpperInvariant()) == 0);
            filename = Path.GetFileName(filename);
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Output.AppendLine(e.Data);
        }

        private void Process_OutputDataReceivedThroughput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                OnCmdLineOutput(this, e.Data);
        }

        private readonly object _thisLock = new object();

        /// <summary>
        ///     Execute a commandline program and capture its output
        /// </summary>
        /// <param name="program">The program to execute, e.g. cmd.exe</param>
        /// <param name="commandline">Any commandline arguments</param>
        /// <param name="workingDirectory">The working directory</param>
        /// <param name="error">stderr output</param>
        /// <returns>Any return values of the command</returns>
        private string GetCommandLineOutput(string program, string commandline, string workingDirectory,
            out string error)
        {
            lock (_thisLock)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = program,
                        WorkingDirectory = workingDirectory,
                        Arguments = commandline,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                process.OutputDataReceived += Process_OutputDataReceived;
                Output.Clear();
                process.Start();
                process.BeginOutputReadLine();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var output = Output.ToString();
                Output.Clear();
                return output;
            }
        }

        private void GetCommandLineOutputEvent(string program, string commandline, string workingDirectory,
            out string error)
        {
            lock (_thisLock)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = program,
                        WorkingDirectory = workingDirectory,
                        Arguments = commandline,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                process.OutputDataReceived += Process_OutputDataReceivedThroughput;
                Output.Clear();
                process.Start();
                process.BeginOutputReadLine();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
            }
        }

        public event CmdLineOutputHandler FileOperationProgress;

        public delegate void CmdLineOutputHandler(object sender, string message);

        protected virtual void OnCmdLineOutput(object sender, string message)
        {
            FileOperationProgress?.Invoke(sender, message);
        }

        #endregion

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
                if (disposing)
                {
                    Dokan.RemoveMountPoint(Mountpoint.FullName.Trim('\\'));
                }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposedValue = true;
        }

        ~GinRepository()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}