using Aspose.Email.Mapi;
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
        static string[] allowedExtensions;
        internal static void MoveFiles(PathInfo pathInfo)
        {
            path = pathInfo;
            string[] files = [];
            //get all files in directory
            try
            {
                files = Directory.GetFiles(pathInfo.From);
            }
            catch (Exception ex)
            {
                string msg = $"Can not Get files from {pathInfo.From}";
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                Logging.SendEmail(ex, msg);
                throw new Exception(msg, ex);
            }

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
                    string msg = $"Faild to get file type of {fileInfo.FullName}";
                    Logging.LogException(ex);
                    Logging.WriteNotes(msg);
                    continue;
                }
                catch (BadImageFormatException ex)
                {
                    string msg = $"Faild to get file type of {fileInfo.FullName}";
                    Logging.LogException(ex);
                    Logging.WriteNotes(msg);
                    continue;
                }
                catch (MagicException ex)
                {
                    string msg = $"Faild to get file type of {fileInfo.FullName}";
                    Logging.LogException(ex);
                    Logging.WriteNotes(msg);
                    continue;
                }
                catch (Exception ex)
                {
                    string msg = $"Faild to get file type of {fileInfo.FullName}";
                    Logging.LogException(ex);
                    Logging.WriteNotes(msg);
                    continue;
                }

                //check file extension and type
                if (!CheckFileExtension())
                {
                    string msg = "File extension is not allowed";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    try
                    {
                        MoveToRejected(fileInfo, "extension");
                    }
                    catch (Exception ex)
                    {
                        Logging.LogException(ex);
                        Logging.SendEmail(ex, $"Faild to move file {fileInfo.FullName} to rejected folder");
                    }
                    continue;
                }

                //check file size
                try
                {
                    if (fileInfo.Length > Configuration.MaxFileSize)
                    {
                        string msg = $"file size is grater than max file size ({Configuration.MaxFileSize} bytes)";
                        msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                        Logging.WriteNotes(msg);
                        //move file to rejected folder
                        try
                        {
                            MoveToRejected(fileInfo, "size");
                        }
                        catch (Exception ex)
                        {
                            Logging.LogException(ex);
                            Logging.WriteNotes(msg);
                        }
                        continue;
                    }
                    else if (fileInfo.Length == 0)
                        continue;
                }
                catch (Exception ex)
                {
                    Logging.LogException(ex);
                    Logging.SendEmail(ex);
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
                    string msg = $"Faild to insert moved file data into database.";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.SendEmail(ex, msg);
                    Logging.LogException(ex);
                    Logging.WriteNotes(msg);
                    throw new Exception(msg, ex);
                }


                //copy file to audit folder
                string backupFile;
                try
                {
                    backupFile = CopyFileToBackupFolder(fileInfo);
                }
                catch (Exception ex)
                {
                    string msg = "Can not move file to backup folder";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    Logging.LogException(ex);
                    Logging.SendEmail(ex, msg);

                    dB.Rollback(cmd);
                    throw new Exception($"Faild to copy file {fileInfo.FullName} to backup folder.", ex);
                }

                //move file to destination folder
                try
                {
                    MoveFileToDestinationFolder(fileInfo);
                }
                catch (Exception ex)
                {
                    string msg = "Faild to move file to destination folder";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    Logging.LogException(ex);
                    Logging.SendEmail(ex, msg);
                    if (!string.IsNullOrEmpty(backupFile))
                        try
                        {
                            File.Delete(backupFile);
                        }
                        catch (Exception e)
                        {
                            msg = $"Can not delete file {backupFile} from backup folder";
                            Logging.WriteNotes(msg);
                            Logging.LogException(e);
                        }
                    dB.Rollback(cmd);
                    continue;
                }

                dB.Commit(cmd);
            }
        }

        internal static void MoveFiles(DeptMoveData deptMoveData)
        {
            if (deptMoveData.ExtIn != null)
            {
                allowedExtensions = deptMoveData.ExtIn;
                PathInfo pathInfo = new()
                {
                    Dept = deptMoveData.Dept,
                    Destination = "local",
                    From = deptMoveData.NetPath + "\\OUT",
                    To = deptMoveData.LocalPath + "\\IN"
                };
                MoveFiles(pathInfo);
            }

            if (deptMoveData.ExtOut != null)
            {
                allowedExtensions = deptMoveData.ExtOut;
                PathInfo pathInfo = new()
                {
                    Dept = deptMoveData.Dept,
                    Destination = "net",
                    From = deptMoveData.LocalPath + "\\OUT",
                    To = deptMoveData.NetPath + "\\IN"
                };
                MoveFiles(pathInfo);
            }
        }

        private static void MoveToRejected(FileInfo file, string rejectedReason)
        {
            string rejectedDirectory = path.From + $"\\rejected\\{rejectedReason}";
            if (!Directory.Exists(rejectedDirectory))
                Directory.CreateDirectory(rejectedDirectory);

            string datetime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string[] fileParts = file.Name.Split('.');
            if (fileParts.Length > 1)
                file.MoveTo($"{rejectedDirectory}\\{fileParts[0]}-{datetime}.{fileParts[fileParts.Length - 1]}");
            else
                file.MoveTo($"{rejectedDirectory}\\{fileParts[0]}-{datetime}");
        }

        private static void MoveFileToDestinationFolder(FileInfo file)
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

        private static string CopyFileToBackupFolder(FileInfo file)
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

        private static bool CheckFileExtension(bool firstCall = true)
        {
            if (!allowedExtensions.Contains(fileInfo.Extension.Substring(1).ToLower()))
                return false;

            if (fileType.Extension.ToLower() == "bin")
                if (firstCall && fileInfo.Extension == ".msg" && fileType.MimeType.ToLower() == "application/vnd.ms-outlook")
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
                else if (fileInfo.Extension.ToLower() == ".vsdx" && fileType.MimeType.ToLower() == "application/vnd.ms-visio.drawing.main+xml")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".xml" && fileType.MimeType.ToLower() == "text/xml")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".rar" && fileType.MimeType.ToLower() == "application/vnd.rar")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".jsx" && fileType.MimeType.ToLower() == "application/javascript")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".js" && fileType.MimeType.ToLower() == "application/javascript")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".tsx" && fileType.MimeType.ToLower() == "application/javascript")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".ts" && fileType.MimeType.ToLower() == "application/javascript")
                    return true;
                else if (fileInfo.Extension.ToLower() == ".txt" && fileType.MimeType.ToLower() == "text/x-affix")
                    return true;
                else
                    return false;

            if (fileInfo.Extension == ".vsd" && fileType.Extension.ToLower() == "thmx")//visio
                if (fileType.MimeType.ToLower() == "application/vnd.ms-office")
                    return true;

            if (fileType.Extension == "jpeg") //jpg, jpeg, jpe, jfif
                return true;

            if (fileType.Extension == "tiff") //tiff, tif
                return true;

            if (fileType.Extension == "zip" && fileType.MimeType.ToLower() == "application/zip") //zip, nupkg, vsix
                return true;

            if (!allowedExtensions.Contains(fileType.Extension.ToLower()))
                return false;

            return true;
        }
    }
}
