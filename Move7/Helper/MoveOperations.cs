﻿using Aspose.Email.Mapi;
using HeyRed.Mime;
using Move7.Model;
using MySqlConnector;

namespace Move7.Helper
{
    internal static class MoveOperations
    {
        static PathInfo path;
        static FileType fileType;
        static FileInfo fileInfo;
        internal static void MoveFiles(PathInfo pathInfo)
        {
            path = pathInfo;
            //get all files in directory
            string[] files = GetAllFiles(pathInfo.From);
            //loop for all files above
            foreach (string file in files)
            {
                fileInfo = new FileInfo(file);
                string ext;
                if (fileInfo.Extension.Length > 0)
                    ext = fileInfo.Extension.Substring(1, fileInfo.Extension.Length - 1);
                else
                {
                    MoveToRejected(fileInfo, "extension");
                    continue;
                }
                //get file type
                try
                {
                    List<string> s = file.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToList();
                    string[] d = s[s.Count - 1].Split('.', StringSplitOptions.RemoveEmptyEntries);
                    string g = string.Join('\\', s.Take(s.Count - 1));
                    //get extension if there are many dots in file name
                    g = $"\\\\{g}\\e.{d[d.Length - 1]}";
                    //rename file before chech file type
                    //because it does not work with arabic name files
                    fileInfo.MoveTo(g);
                    fileType = MimeGuesser.GuessFileType(g);
                    fileInfo.MoveTo(file);
                }
                catch (DllNotFoundException ex)
                {
                    continue;
                }
                catch (BadImageFormatException ex)
                {
                    continue;
                }
                catch (MagicException ex)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    continue;
                }

                //check file extension and type
                if (!CheckFileExtension())
                {
                    string msg = "file extension is not in allowed extensions list";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    MoveToRejected(fileInfo, "extension");
                    continue;
                }

                //check file size
                try
                {
                    if (fileInfo.Length > Configuration.MaxFileSize)
                    {
                        string msg = "file size is grater than max file size ({Configuration.MaxFileSize} bytes)";
                        msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                        Logging.WriteNotes(msg);
                        //move file to rejected folder
                        MoveToRejected(fileInfo, "size");
                        continue;
                    }
                    else if (fileInfo.Length == 0)
                        continue;
                }
                catch
                {
                    continue;
                }

                //start write in database
                DB dB = new DB();
                MySqlCommand cmd = null;
                try
                {
                    cmd = dB.InsertIntoTable(fileInfo.Name, ext, fileType.Extension, fileInfo.Length, path.Dept, path.Destination);
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    throw new Exception(msg);
                }


                //copy file to audit folder
                string backupFile;
                try
                {
                    backupFile = CopyFileToBackupFolder(fileInfo);
                }
                catch (Exception ex)
                {
                    dB.Rollback(cmd);
                    throw new Exception(ex.Message);
                }

                //move file to destination folder
                try
                {
                    MoveFileToDestinationFolder(fileInfo);
                }
                catch (Exception)
                {
                    if (!string.IsNullOrEmpty(backupFile))
                        DeleteFile(backupFile);
                    dB.Rollback(cmd);
                    continue;
                }

                dB.Commit(cmd);
            }
        }

        private static void MoveToRejected(FileInfo file, string rejectedReason)
        {
            string rejectedDirectory = path.From + $"rejected\\{rejectedReason}";
            if (!Directory.Exists(rejectedDirectory))
                Directory.CreateDirectory(rejectedDirectory);

            string datetime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string[] fileParts = file.Name.Split('.');
            if (fileParts.Length > 1)
                file.MoveTo($"{rejectedDirectory}\\{fileParts[0]}-{datetime}.{fileParts[fileParts.Length - 1]}");
            else
                file.MoveTo($"{rejectedDirectory}\\{fileParts[0]}-{datetime}");
        }

        private static void DeleteFile(string backupFile)
        {
            try
            {
                File.Delete(backupFile);
            }
            catch (Exception ex)
            {
                string msg = "Can not delete file from backup folder";
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
            }
        }

        private static void MoveFileToDestinationFolder(FileInfo file)
        {
            try
            {
                string fl = path.To + $"\\{file.Name}";
                if (File.Exists(fl))
                {
                    string datetime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string[] fileParts = file.Name.Split('.');
                    file.MoveTo(path.To + $"\\{fileParts[0]}-{datetime}.{fileParts[1]}");
                }
                else
                    file.MoveTo(path.To + $"\\{file.Name}");
            }
            catch (Exception ex)
            {
                string msg = "Can not move file to destination folder";
                msg += $"\nfile name : {file.Name} - dept : {path.Dept} - destination : {path.Destination}";
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                Logging.SendEmail(ex, msg);
                throw new Exception(msg);
            }
        }

        private static string CopyFileToBackupFolder(FileInfo file)
        {
            try
            {
                string backupFile = "";
                string BackupPath = Configuration.BackupPath;
                if (!Directory.Exists(BackupPath))
                    Directory.CreateDirectory(BackupPath);
                string today = DateTime.Now.ToString("yyyy-MM-dd");

                BackupPath += $"\\{today}";
                if (!Directory.Exists(BackupPath))
                    Directory.CreateDirectory(BackupPath);

                BackupPath += $"\\{path.Dept}";
                if (!Directory.Exists(BackupPath))
                    Directory.CreateDirectory(BackupPath);

                BackupPath += $"\\Moved_To-{path.Destination}";
                if (!Directory.Exists(BackupPath))
                    Directory.CreateDirectory(BackupPath);

                string fl = BackupPath + $"\\{file.Name}";
                if (File.Exists(fl))
                {
                    string datetime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string[] fileParts = file.Name.Split('.');
                    backupFile = BackupPath + $"\\{fileParts[0]}-{datetime}.{fileParts[1]}";
                }
                else
                    backupFile = BackupPath + $"\\{file.Name}";
                file.CopyTo(backupFile);
                return backupFile;
            }
            catch (Exception ex)
            {
                string msg = "Can not move file to audit folder";
                msg += $"\nfile name : {file.Name} - dept : {path.Dept} - destination : {path.Destination}";
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                Logging.SendEmail(ex, msg);
                throw new Exception(msg);
            }
        }

        private static bool CheckFileExtension(bool firstCall = true)
        {
            if (fileType.Extension.ToLower() == "bin")
                if (fileType.MimeType.ToLower() == "application/vnd.ms-outlook" && firstCall)
                {
                    MapiMessage email = MapiMessage.FromMailMessage(fileInfo.FullName);
                    foreach (MapiAttachment attachment in email.Attachments)
                    {
                        if (attachment.MimeTag == "message/rfc822")
                            return false;
                        fileType = MimeGuesser.GuessFileType(attachment.BinaryData);
                        if (!CheckFileExtension(false))
                            return false;

                    }
                    return true;
                }
                else if(fileType.MimeType == "application/encrypted" && fileInfo.Extension == ".xlsx")
                    return true;
                else if (fileType.MimeType == "application/encrypted" && fileInfo.Extension == ".docx")
                    return true;
                else
                    return false;

            if (fileType.MimeType.ToLower() == "text/xml")
                return true;

            if (fileType.MimeType.ToLower() == "application/vnd.ms-visio.drawing.main+xml")
                return true;

            if (fileType.Extension.ToLower() == "thmx")
                if (fileType.MimeType.ToLower() == "application/vnd.ms-office")
                    return true;

            try
            {
                return Configuration.Extensions.Contains(fileType.Extension);
            }
            catch
            {
                return false;
            }
        }


        private static string[] GetAllFiles(string from)
        {
            try
            {
                return Directory.GetFiles(from);
            }
            catch (Exception ex)
            {
                string msg = $"Can not Get files from {from}";
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                Logging.SendEmail(ex, msg);
                return null;
            }
        }
    }
}
