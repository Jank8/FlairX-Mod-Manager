using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ZZZ_Mod_Manager_X
{
    /// <summary>
    /// Utility class for common file operations with proper error handling
    /// </summary>
    public static class FileOperations
    {
        /// <summary>
        /// Safely reads a JSON file and deserializes it
        /// </summary>
        public static T? ReadJsonFile<T>(string filePath) where T : class
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.LogWarning($"JSON file not found: {filePath}");
                    return null;
                }

                var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to read JSON file: {filePath}", ex);
                return null;
            }
        }

        /// <summary>
        /// Safely writes an object to a JSON file
        /// </summary>
        public static bool WriteJsonFile<T>(string filePath, T data) where T : class
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write JSON file: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely creates a directory if it doesn't exist
        /// </summary>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Logger.LogInfo($"Created directory: {directoryPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create directory: {directoryPath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely copies a file with error handling
        /// </summary>
        public static bool SafeCopyFile(string sourcePath, string destinationPath, bool overwrite = false)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    Logger.LogWarning($"Source file not found: {sourcePath}");
                    return false;
                }

                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    EnsureDirectoryExists(destinationDir);
                }

                File.Copy(sourcePath, destinationPath, overwrite);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to copy file from {sourcePath} to {destinationPath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely deletes a file with error handling
        /// </summary>
        public static bool SafeDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete file: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets all files with a specific extension recursively
        /// </summary>
        public static IEnumerable<string> GetFilesWithExtension(string directoryPath, string extension, bool recursive = true)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Logger.LogWarning($"Directory not found: {directoryPath}");
                    return Enumerable.Empty<string>();
                }

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var pattern = extension.StartsWith("*") ? extension : $"*.{extension.TrimStart('.')}";
                
                return Directory.GetFiles(directoryPath, pattern, searchOption);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get files with extension {extension} from {directoryPath}", ex);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Checks if a path points to a symbolic link
        /// </summary>
        public static bool IsSymbolicLink(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                    return false;

                var fileInfo = new FileInfo(path);
                return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check if path is symbolic link: {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the size of a directory in bytes
        /// </summary>
        public static long GetDirectorySize(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get directory size: {directoryPath}", ex);
                return 0;
            }
        }
    }
}