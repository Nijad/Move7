using HeyRed.Mime;
using Move7.Model;
using MySqlConnector;

namespace Move7.Helper
{
    internal static class MoveOperations
    {
        static PathInfo path;
        static FileType fileType;
        internal static void MoveFiles(PathInfo pathInfo)
        {
            path = pathInfo;
            //get all files in directory
            string[] files = GetAllFiles(pathInfo.From);
            //loop for all files above
            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                string ext = fileInfo.Extension.Substring(1, fileInfo.Extension.Length - 1);

                try
                {
                    List<string> s = file.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToList();
                    string[] d = s[s.Count - 1].Split('.');
                    string g = string.Join('\\', s.Take(s.Count - 1));
                    g = $"\\\\{g}\\e.{d[1]}";
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
                    else if (file.Length == 0)
                        continue;
                }
                catch
                {
                    continue;
                }

                //check file size
                //if (!CheckFileSize(fileInfo))
                //{
                //    string msg = "file size is grater than max file size ({Configuration.MaxFileSize} bytes)";
                //    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                //    Logging.WriteNotes(msg);
                //    continue;
                //}

                //check file extension
                if (!CheckFileExtension())
                {
                    string msg = "file extension is not in allowed extensions list";
                    msg += $"\nfile name : {fileInfo.Name} - dept : {path.Dept} - destination : {path.Destination}";
                    Logging.WriteNotes(msg);
                    MoveToRejected(fileInfo, "extension");
                    continue;
                }

                //start write in database
                DB dB = new DB(Configuration.DatabaseIP);
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
            file.MoveTo($"{rejectedDirectory}\\{fileParts[0]}-{datetime}.{fileParts[1]}");
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

        private static bool CheckFileExtension()
        {
            if (fileType.Extension.ToLower() == "bin")
                if (fileType.MimeType.ToLower() == "application/vnd.ms-outlook")
                    return true;
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

        private static int CheckSize(FileInfo file)
        {
            try
            {
                if (file.Length > Configuration.MaxFileSize)
                    return 0;
                else if (file.Length == 0)
                    return -1;
            }
            catch
            {
                return 0;
            }
            return 1;
        }

        private static bool CheckFileSize(FileInfo file)
        {
            try
            {
                return (file.Length <= Configuration.MaxFileSize && file.Length != 0);
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
