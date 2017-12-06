﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.ServiceModel;
using GinClientLibrary;

namespace GinClientService
{
    [ServiceContract(CallbackContract = typeof(IGinClientCallback))]
    public interface IGinClientService
    {
        /// <summary>
        /// Adds a new repository to the list of managed Repositories. If necessary,
        /// this will also create the directories for the repository and perform an 
        /// initial checkout.
        /// </summary>
        /// <param name="physicalDirectory">A full path to a directory with a gin repository (or where a new one can be created)</param>
        /// <param name="mountpoint">A full path to a directory where a mountpoint should be created</param>
        /// <param name="name">The name of this repository</param>
        /// <param name="commandline">The gin get commandline used to initialize this, e.g. "gin get username/repository"</param>
        /// <returns>True if repository creation succeeded</returns>
        [OperationContract]
        bool AddRepository(string physicalDirectory, string mountpoint, string name, string commandline);

        /// <summary>
        /// Unmounts a repository.
        /// </summary>
        /// <param name="repoName"></param>
        /// <returns></returns>
        [OperationContract]
        bool MountRepository(string repoName);

        [OperationContract]
        bool UnmountRepository(string repoName);

        /// <summary>
        /// Completely deletes a repository and all data associated with it
        /// </summary>
        /// <param name="repoName"></param>
        [OperationContract]
        void DeleteRepository(string repoName);

        /// <summary>
        /// Unmounts all currently managed repositories
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        bool UnmmountAllRepositories();

        /// <summary>
        /// Logs a user into GIN
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [OperationContract]
        bool Login(string username, string password);

        /// <summary>
        /// Get a list of all currently managed repositories
        /// </summary>
        /// <returns>A JSON representation of a GinRepositoryData array</returns>
        [OperationContract]
        string GetRepositoryList();

        /// <summary>
        /// Updates the stored information for the Repository indicated by repoName
        /// </summary>
        /// <param name="repoName">The previous name of the repository</param>
        /// <param name="data">The new data</param>
        /// <returns></returns>
        [OperationContract]
        bool UpdateRepository(string repoName, GinRepositoryData data);

        /// <summary>
        /// Performs a gin get-content operation on a file
        /// </summary>
        /// <param name="repoName">The repository in which the file resides</param>
        /// <param name="filepath">The path to the file</param>
        /// <returns></returns>
        [OperationContract]
        bool RetrieveFile(string repoName, string filepath);

        /// <summary>
        /// Upload a file to the remote GIN repository
        /// </summary>
        /// <param name="repoName">Name of the repository</param>
        /// <param name="filepath">Path to the file</param>
        /// <returns></returns>
        [OperationContract]
        bool UploadFile(string repoName, string filepath);

        /// <summary>
        /// Removes all local content of a file. Equivalent to gin remove-content.
        /// </summary>
        /// <param name="repoName">Name of the repository</param>
        /// <param name="filepath">Path to the file</param>
        /// <returns></returns>
        [OperationContract]
        bool StashFile(string repoName, string filepath);

        /// <summary>
        /// Updates the repository to the current version on the remote server.
        /// Equivalent to gin download.
        /// </summary>
        /// <param name="repoName">Name of the repository</param>
        [OperationContract]
        void DownloadUpdateInfo(string repoName);
    }

    [SuppressMessage("ReSharper", "OperationContractWithoutServiceContract")]
    public interface IGinClientCallback
    {
        /// <summary>
        /// Called when a file operation (i.e. download, upload or checkout) is started.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="repository"></param>
        [OperationContract]
        void FileOperationStarted(string filename, string repository);

        /// <summary>
        /// Called when a file operation (i.e. download, upload or checkout) is finished.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="repository"></param>
        /// <param name="success"></param>
        [OperationContract]
        void FileOperationFinished(string filename, string repository, bool success);

        /// <summary>
        /// Called when a file operation (i.e. download, upload or checkout) progresses.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="repository"></param>
        /// <param name="progress"></param>
        /// <param name="speed"></param>
        /// <param name="state"></param>
        [OperationContract]
        void FileOperationProgress(string filename, string repository, int progress, string speed, string state);

        /// <summary>
        /// Called when the Gin Client service experiences an internal error.
        /// </summary>
        /// <param name="message"></param>
        [OperationContract]
        void GinServiceError(string message);
    }
}