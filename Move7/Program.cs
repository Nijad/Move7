using Move7.Helper;
using Move7.Model;
using System.Data;
using System.Security.AccessControl;
using System.Security.Principal;

internal class Program
{
    static bool running = true;

    static void Main(string[] args)
    {
        try
        {

            bool isNewInstance;
            const string mutexName = "Global\\MyUniqueApplicationName"; // "Global" ensures visibility across all sessions
            // Create security settings for the mutex
            MutexSecurity security = new MutexSecurity();

            // Allow all users to access the mutex
            SecurityIdentifier everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new MutexAccessRule(
                everyoneSid,
                MutexRights.FullControl,
                AccessControlType.Allow
            ));

            using (Mutex mutex = new Mutex(false, mutexName, out isNewInstance))
            {
                if (!isNewInstance)
                {
                    Console.WriteLine("Program is already running.");
                    Console.WriteLine("Press Enter to exit.");
                    Console.ReadLine();
                    return;
                }
                // Apply security settings to the mutex
                mutex.SetAccessControl(security);

                Console.WriteLine("Start Move Program");
                Console.WriteLine();

                Console.WriteLine("Test Connection Database...");
                DB dB = new DB();
                try
                {
                    dB.CheckDatabaseConnection();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine("Connecting to database failed");
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }
                DataTable dt;
                try
                {
                    dt = dB.GetConfig();
                }
                catch (Exception ex)
                {
                    string msg = "Error occurred while reading config from database.";
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine(msg);
                    Logging.SendEmail(ex, msg);
                    Logging.LogException(ex);
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["key"].ToString().ToLower() == "admins")
                            Configuration.Admins = row["value"].ToString().ToLower().Split(',');

                        if (row["key"].ToString().ToLower() == "developers")
                            Configuration.Developers = row["value"].ToString().ToLower().Split(',');

                        if (row["key"].ToString().ToLower() == "backup_path")
                            Configuration.BackupPath = row["value"].ToString().ToLower();

                        if (row["key"].ToString().ToLower() == "duration")
                        {
                            string[] duration = row["value"].ToString().ToLower().Split(',');
                            switch (duration[1].ToLower())
                            {
                                case "s":
                                    Configuration.Duration = int.Parse(duration[0]) * 1000;
                                    break;
                                case "m":
                                    Configuration.Duration = int.Parse(duration[0]) * 60000;
                                    break;
                                case "h":
                                    Configuration.Duration = int.Parse(duration[0]) * 3600000;
                                    break;
                            }
                        }

                        if (row["key"].ToString().ToLower() == "max_file_size")
                        {
                            string[] fileSize = row["value"].ToString().ToLower().Split(',');
                            switch (fileSize[1].ToLower())
                            {
                                case "k":
                                    Configuration.MaxFileSize = long.Parse(fileSize[0]) * 1024;
                                    break;
                                case "m":
                                    Configuration.MaxFileSize = long.Parse(fileSize[0]) * 1024 * 1024;
                                    break;
                                case "g":
                                    Configuration.MaxFileSize = long.Parse(fileSize[0]) * 1024 * 1024 * 1024;
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string msg = "Error occurred while reading config.";
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine(msg);
                    Logging.SendEmail(ex, msg);
                    Logging.LogException(ex);
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine("Program start successfuly");
                int i = 0;
                while (running)
                {
                    List<DeptMoveData> moveData = new();
                    dt.Clear();
                    try
                    {
                        dt = dB.GetMoveData();
                    }
                    catch (Exception ex)
                    {
                        string msg = "Error occurred while reading dept-ext data from database";
                        Logging.SendEmail(ex, msg);
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                        Console.WriteLine($"{msg}");
                        Console.WriteLine($"please check log");
                        Console.WriteLine("Program will exit. Press Enter to exit");
                        Console.ReadLine();
                        return;
                    }

                    try
                    {
                        foreach (DataRow dr in dt.Rows)
                            moveData.Add(new DeptMoveData()
                            {
                                Dept = dr["dept"].ToString(),
                                ExtIn = dr["ext_in"]?.ToString().Split(','),
                                ExtOut = dr["ext_out"]?.ToString().Split(','),
                                LocalPath = dr["local_path"].ToString(),
                                NetPath = dr["net_path"].ToString()
                            });
                    }
                    catch (Exception ex)
                    {
                        string msg = "Error occurred while reading dept-ext data";
                        Logging.SendEmail(ex, msg);
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                        Console.WriteLine($"{msg}");
                        Console.WriteLine($"please check log");
                        Console.WriteLine("Program will exit. Press Enter to exit");
                        Console.ReadLine();
                        return;
                    }

                    try
                    {
                        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + $"    -    run no {++i}");

                        foreach (DeptMoveData deptMoveData in moveData)
                            MoveOperations.MoveFiles(deptMoveData);

                        Thread.Sleep(Configuration.Duration);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                        Console.WriteLine($"{ex.Message}");
                        Console.WriteLine($"please check log");
                        Console.WriteLine("Program will exit. Press Enter to exit");
                        Console.ReadLine();
                        return;
                    }
                }
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.WriteLine("Access Control List (ACL) APIs are part of resource management on Windows and are not supported on this platform.");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Program is already running.");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
        catch
        {
            Console.WriteLine("Something went wrong.");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
    }
}