﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DokanNet;
using GinClientLibrary.Extensions;
using Newtonsoft.Json;
using static System.String;

namespace GinClientLibrary
{
    /// <summary>
    ///     Manages all repositories. Responsible for mounting, unmounting, deleting and creating repositories on disk.
    /// </summary>
    public class RepositoryManager
    {
        public NotifyIcon AppIcon;

        public delegate void FileOperationProgressHandler(string filename, GinRepositoryData repository, int progress,
            string speed, string state);

        public delegate void
            FileRetrievalCompletedHandler(object sender, GinRepositoryData repo, string file, bool success);

        public delegate void FileRetrievalStartedHandler(object sender, GinRepositoryData repo, string file);

        public delegate void RepositoryOperationErrorHandler(object sender,
            GinRepository.FileOperationErrorEventArgs message);

        private static RepositoryManager _instance;
        private static readonly StringBuilder Output = new StringBuilder("");

        private readonly object _thisLock = new object();

        private readonly string GinCliPath = AppDomain.CurrentDomain.BaseDirectory+"gin-cli\\bin\\";
        private readonly string GinCliExe = "\""+AppDomain.CurrentDomain.BaseDirectory + "gin-cli\\bin\\gin.exe\"";

        private List<GinRepository> _repositories;

        private const string ginError= "Gin Error! Error message is: ";

        private RepositoryManager()
        {
        }

        public static RepositoryManager Instance => _instance ?? (_instance = new RepositoryManager());

        public List<GinRepository> Repositories => _repositories ?? (_repositories = new List<GinRepository>());

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!IsNullOrEmpty(e.Data))
                lock (_thisLock)
                {
                    Output.AppendLine(e.Data);
                }
        }

        public GinRepository GetRepoByName(string name)
        {
            return Repositories.Single(r => Compare(r.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public bool SetDefaultSvr(string alias)
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = "use-server \""+alias+"\"",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!IsNullOrEmpty(error) || process.ExitCode != 0)
                    return false;
                return true;
            }
        }

       

        public void Logout()
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = " logout",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                process.WaitForExit();
            }
        }

        public bool CreateNewRepository(string repoName)
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = " create --no-clone \"" + repoName+"\"",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                process.WaitForExit();
            }

            return true;
        }

        /// <summary>
        /// Get all configured servers
        /// </summary>
        /// <returns>json with configured servers</returns>
        public string GetServers()
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = "servers --json ",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!IsNullOrEmpty(error) || process.ExitCode != 0)
                    return "Error getting servers info!";
                return output.ToString();
            }
        }


        /// <summary>
        /// add new server configuration to gin-cli
        /// </summary>
        /// <param name="alias">new server alias</param>
        /// <param name="web">new web configuration string http[s]://hostname:port</param>
        /// <param name="git">new git configuration gituser@hostname:port</param>
        /// <returns>true for success</returns>
        public bool AddServer(string alias, string web, string git)
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = "add-server --web "+ web + " --git "+git+" "+ "\""+alias+"\"" ,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.StandardInput.WriteLine("yes");
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!IsNullOrEmpty(error) || process.ExitCode != 0)
                    return false;
                return true;
            }
        }

        /// <summary>
        /// remove server configuration to gin-cli
        /// </summary>
        /// <param name="alias">delete server alias</param>
        /// <returns>true for success</returns>
        public bool DeleteServer(string alias)
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = "remove-server "  + "\""+alias+"\"",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

                process.Start();
                process.BeginOutputReadLine();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!IsNullOrEmpty(error) || process.ExitCode != 0)
                    return false;
                return true;
            }
        }


        public bool Login(string username, string password, string serverAlias)
        {
            lock (this)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = @GinCliExe,
                        WorkingDirectory = @GinCliPath,
                        Arguments = " login \"" + username +"\" --server "+ "\""+serverAlias+"\"",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };

                process.OutputDataReceived += Process_OutputDataReceived;
                Output.Clear();
                process.Start();
                process.BeginOutputReadLine();
                process.StandardInput.WriteLine(password);
                var error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (IsNullOrEmpty(error)) return true;
                Instance.OnRepositoryOperationError(null,
                    new GinRepository.FileOperationErrorEventArgs
                    {
                        RepositoryName = "RepositoryManager",
                        Message = error
                    });
                return false;
            }
        }

        /// <summary>
        ///     Given a file path, return the GinRepository instance managing that path, or null if no such path can be found
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public GinRepository GetRepoByPath(string filePath)
        {
            return Repositories.Find(repo => filePath.Contains(repo.Mountpoint.FullName.Trim('\\')));
        }

        /// <summary>
        ///     Returns true if the passed path is the root directory of a repository, false otherwise
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool IsBasePath(string filePath)
        {
            var attr = File.GetAttributes(filePath);
            if ((attr & FileAttributes.Directory) != FileAttributes.Directory) return false;
            var dInfo = new DirectoryInfo(filePath);

            return Repositories.Any(repo => repo.Mountpoint.IsEqualTo(dInfo));
        }

        public string GetGinCliVersion()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = @GinCliExe,
                    WorkingDirectory = @GinCliPath,
                    Arguments = " --version",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false
                }
            };
            process.OutputDataReceived += Process_OutputDataReceived;
            lock (_thisLock)
            {
                Output.Clear();
            }
            process.Start();
            process.BeginOutputReadLine();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            lock (_thisLock)
            {
                return Output.ToString();
            }
        }

        /// <summary>
        ///     Returns true if the given path is a path managed by the Repomanager
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool IsManagedPath(string filePath)
        {
            return Repositories.Any(repo => filePath.Contains(repo.Mountpoint.FullName));
        }

        /// <summary>
        ///     Get the file status of every file in a repository
        /// </summary>
        /// <param name="ginRepository"></param>
        /// <returns></returns>
        public string GetRepositoryFileInfo(GinRepository ginRepository)
        {
            return ginRepository.GetStatusCacheJson();
        }


        /// <summary>
        ///     Mount a repository
        /// </summary>
        /// <param name="repo"></param>
        public void MountRepository(GinRepository repo)
        {
            if (!repo.Mounted)
            {
                //The Dokan Mount() function, as of Version 1.2, is a blocking operation that does not return
                //as long as the file path remains mounted.
                var thread = new Thread(repo.Mount);
                thread.Start();
            }
        }

        /// <summary>
        ///     Update the repository indicated by "repoName" with new RepositoryData.
        /// </summary>
        /// <param name="repoName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool UpdateRepository(string repoName, GinRepositoryData data)
        {
            lock (this)
            {
                var repo = Repositories.Single(r => CompareOrdinal(r.Name, repoName) == 0);
                UnmountRepository(repo);
                Repositories.Remove(repo);
                repo = new GinRepository(data);
                Repositories.Add(repo);
                MountRepository(repo);

                return true;
            }
        }

        public void UnmountRepository(GinRepository repo)
        {
            Dokan.RemoveMountPoint(repo.Mountpoint.FullName.Trim('\\'));
            repo.Mounted = false;
            Directory.Delete(repo.Mountpoint.FullName);
        }

        public void DeleteRepository(GinRepository repo)
        {
            lock (this)
            {
                UnmountRepository(repo);
                repo.DeleteRepository();

                Repositories.Remove(repo);
            }
        }

        public void UnmountAllRepositories()
        {
            lock (this)
            {
                foreach (var repo in Repositories)
                    UnmountRepository(repo);

                Repositories.Clear();
            }
        }

        public void AddRepository(DirectoryInfo physicalDirectory, DirectoryInfo mountpoint, string name,
            string commandline, bool performFullCheckout, bool createNew)
        {
            //Repository already exists
            if (Repositories.Any(repository =>
                Compare(repository.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0))
                return;

            var repo = new GinRepository(
                physicalDirectory,
                mountpoint,
                name,
                commandline, createNew);

            repo.FileOperationStarted += Repo_FileOperationStarted;
            repo.FileOperationCompleted += Repo_FileOperationCompleted;
            repo.FileOperationProgress += Repo_FileOperationProgress;
            repo.FileOperationError += RepoOnFileOperationError;
            if (!repo.CreateDirectories(performFullCheckout)) return;

            MountRepository(repo);
            repo.Initialize();

            Repositories.Add(repo);
        }

        private void RepoOnFileOperationError(object sender,
            GinRepository.FileOperationErrorEventArgs fileOperationErrorEventArgs)
        {
            OnRepositoryOperationError((GinRepository)sender, fileOperationErrorEventArgs);
        }

        public event FileOperationProgressHandler FileOperationProgress;

        private void Repo_FileOperationProgress(object sender, string message)
        {
            try
            {
                var progress = JsonConvert.DeserializeObject<fileOpProgress>(message);
                string errorMsg = "";
                if (!string.IsNullOrEmpty(progress.err))
                {
                    errorMsg = " Error : " + progress.err;
                }
                FileOperationProgress?.Invoke(progress.filename, (GinRepository)sender, progress.GetProgress(),
                    progress.rate, progress.state);

                AppIcon.BalloonTipText = progress.state + " " + progress.filename + " at " + progress.rate + ", " + progress.progress + " completed." + errorMsg;
                AppIcon.Text = progress.state + ", " + progress.progress + " completed";
            }
            catch
            {
                AppIcon.BalloonTipText = "Error: Repo_FileOperationProgress";
            }
        }

        private void OnFileRetrievalStarted(DokanInterface.FileOperationEventArgs e, GinRepository sender)
        {
            if (!string.Equals(e.File, "."))
            {
                AppIcon.ShowBalloonTip(1000, "GIN activity in progress", "Repository " + sender.Name + " operating on " + e.File, ToolTipIcon.Info);
            }
            else
            {
                AppIcon.ShowBalloonTip(1000, "GIN activity in progress", "Repository " + sender.Name + " operating on all files.", ToolTipIcon.Info);
            }
        }

        public event FileRetrievalCompletedHandler FileRetrievalCompleted;

        private void OnFileRetrievalCompleted(DokanInterface.FileOperationEventArgs e, GinRepository sender)
        {
            FileRetrievalCompleted?.Invoke(this, sender, e.File, e.Success);
            try
            {
                AppIcon.Text = "";
                AppIcon.ShowBalloonTip(1500, "GIN activity done", "Repository " + sender.Name + " work finished.", ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void Repo_FileOperationCompleted(object sender, DokanInterface.FileOperationEventArgs e)
        {
            OnFileRetrievalCompleted(e, (GinRepository)sender);
        }

        private void Repo_FileOperationStarted(object sender, DokanInterface.FileOperationEventArgs e)
        {
            OnFileRetrievalStarted(e, (GinRepository)sender);
        }

        private void OnRepositoryOperationError(GinRepository sender,
            GinRepository.FileOperationErrorEventArgs message)
        {
            MessageBox.Show(ginError + message.Message, "GIN Service Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public string GetRemoteRepoList()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = @GinCliExe,
                    WorkingDirectory = @GinCliPath,
                    Arguments = "repos --json --all",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            StringBuilder output = new StringBuilder();
            process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return output.ToString();
        }


        public string GetRemoteRepositoryInfo(string path)
        {
            var repo = GetRepoByPath(path);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = @GinCliExe,
                    WorkingDirectory = @GinCliPath,
                    Arguments = "repoinfo --json " + path,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            StringBuilder output = new StringBuilder();
            process.OutputDataReceived += (sender, args) => { output.AppendLine(args.Data); };

            process.Start();
            process.BeginOutputReadLine();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!IsNullOrEmpty(error) || process.ExitCode != 0)
                return "Error retrieving repository info!";
            return output.ToString();
        }

        private struct fileOpProgress
        {
            public string filename { get; set; }
            public string state { get; set; }
            public string progress { get; set; }
            public string rate { get; set; }
            public string err { get; set; }

            public int GetProgress()
            {
                if (!IsNullOrEmpty(progress))
                    return int.Parse(progress.Trim('%'));

                return 0;
            }
        }
    }
}