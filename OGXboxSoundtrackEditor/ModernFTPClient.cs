using System;
using System.Collections.Generic;
using System.IO;
using FluentFTP;
using System.Net;
using System.Linq;
using FluentFTP.Helpers;

namespace OGXboxSoundtrackEditor
{
    public class ModernFtpClient : IDisposable
    {
        private readonly AsyncFtpClient _client;
        public List<FtpLogEntry> FtpLogEntries { get; } = new List<FtpLogEntry>();
        public string CurrentWorkingDirectory { get; private set; } = "/";

        public ModernFtpClient(string hostname, string username, string password)
        {
            // Create the FTP client with basic credentials
            _client = new AsyncFtpClient(hostname, username, password);
            LogOperation($"FTP client created for {hostname}");
        }

        private void LogOperation(string message)
        {
            FtpLogEntries.Add(new FtpLogEntry
            {
                EntryData = message,
                EntryTime = DateTime.Now
            });
            System.Diagnostics.Debug.WriteLine(message);
        }

        public bool Connect()
        {
            try
            {
                LogOperation("Connecting to FTP server");
                _client.Connect().Wait();
                LogOperation("Connected to FTP server");
                return _client.IsConnected;
            }
            catch (Exception ex)
            {
                LogOperation($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public bool Login()
        {
            // Login is handled automatically in Connect
            return _client.IsConnected;
        }

        public bool MakeDirectory(string name)
        {
            try
            {
                LogOperation($"Creating directory: {name}");
                _client.CreateDirectory(name).Wait();
                return true;
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to create directory: {ex.Message}");
                return false;
            }
        }

        public bool DeleteFolder(string name)
        {
            if (name == "..")
                return true;

            try
            {
                LogOperation($"Deleting directory: {name}");
                _client.DeleteDirectory(name).Wait();
                return true;
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to delete directory: {ex.Message}");
                return false;
            }
        }

        public bool DeleteFile(string name)
        {
            try
            {
                LogOperation($"Deleting file: {name}");
                _client.DeleteFile(name).Wait();
                return true;
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to delete file: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (_client.IsConnected)
            {
                LogOperation("Disconnecting from FTP server");
                _client.Disconnect().Wait();
            }
        }

        public bool ChangeWorkingDirectory(string path)
        {
            try
            {
                LogOperation($"Changing directory to: {path}");
                _client.SetWorkingDirectory(path).Wait();
                CurrentWorkingDirectory = _client.GetWorkingDirectory().Result;
                return true;
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to change directory: {ex.Message}");
                return false;
            }
        }

        public byte[] DownloadFile(string filename)
        {
            try
            {
                LogOperation($"Downloading file: {filename}");

                // Create a unique temporary file path
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");

                // Download file to the temporary location
                var result = _client.DownloadFile(tempFile, filename).Result;

                if (result.IsSuccess())
                {
                    // Read the downloaded file
                    byte[] fileData = File.ReadAllBytes(tempFile);

                    // Clean up the temporary file
                    try { File.Delete(tempFile); } catch { }

                    LogOperation($"Successfully downloaded {fileData.Length} bytes");
                    return fileData;
                }
                else
                {
                    LogOperation($"Download failed with status: {result}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to download file: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogOperation($"Inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }

        public bool UploadFile(string localPath, string remoteFilename)
        {
            try
            {
                LogOperation($"Uploading file: {localPath} to {remoteFilename}");
                var status = _client.UploadFile(localPath, remoteFilename).Result;
                return status.IsSuccess();
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to upload file: {ex.Message}");
                return false;
            }
        }

        public bool UploadData(byte[] data, string remoteFilename)
        {
            try
            {
                LogOperation($"Uploading data to: {remoteFilename}");
                using (var memoryStream = new MemoryStream(data))
                {
                    var status = _client.UploadStream(memoryStream, remoteFilename).Result;
                    return status.IsSuccess();
                }
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to upload data: {ex.Message}");
                return false;
            }
        }

        public List<FtpFile> GetFiles()
        {
            try
            {
                LogOperation($"Listing files in: {CurrentWorkingDirectory}");
                var items = _client.GetListing().Result;

                return items
                    .Where(item => item.Type == FtpObjectType.File)
                    .Select(item => new FtpFile
                    {
                        name = item.Name,
                        attributes = item.RawPermissions,
                        size = (int)item.Size,
                        dateModified = item.Modified.ToShortDateString(),
                        timeModified = item.Modified.ToShortTimeString()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to list files: {ex.Message}");
                return new List<FtpFile>();
            }
        }

        public List<FtpDirectory> GetDirectories()
        {
            try
            {
                LogOperation($"Listing directories in: {CurrentWorkingDirectory}");
                var items = _client.GetListing().Result;

                return items
                    .Where(item => item.Type == FtpObjectType.Directory)
                    .Select(item => new FtpDirectory
                    {
                        Name = item.Name,
                        attributes = item.RawPermissions
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to list directories: {ex.Message}");
                return new List<FtpDirectory>();
            }
        }

        public void List()
        {
            try
            {
                LogOperation($"Getting full listing of: {CurrentWorkingDirectory}");
                _client.GetListing().Wait();
            }
            catch (Exception ex)
            {
                LogOperation($"Failed to get listing: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _client?.Dispose();
        }
    }
}