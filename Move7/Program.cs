using Move7.Helper;
using Move7.Model;
using System.Data;
using System.Security.AccessControl;
using System.Security.Principal;

internal class Program
{
    static bool running = true;
    static System.Timers.Timer aTimer;
    static int seconds = 5;

    
    static void Main(string[] args)
    {
        //must pass first argument to run to prevent run program by double click
        if (args.Length == 0 || args[0] != "nijad")
        {
            ExitCountDown();
            return;
        }

        try
        {
            bool isNewInstance;
            // "Global" ensures visibility across all sessions
            const string mutexName = "Global\\MyUniqueApplicationName";
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
                    ExitCountDown();
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

                Console.WriteLine("Program start successfuly");
                int i = 0;
                while (running)
                {
                    try
                    {
                        GetConfiguration(dB);
                    }
                    catch
                    {
                        Console.WriteLine("Program will exit. Press Enter to continue");
                        Console.ReadLine();
                        return;
                    }

                    List<DeptMoveData> moveData = new();
                    DataTable dt;
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

    private static void GetConfiguration(DB dB)
    {
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
            throw;
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
            throw;
        }
    }

    private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
        Console.Write(--seconds);
        if (seconds == 0)
            Environment.Exit(1);
    }

    private static void ExitCountDown()
    {
        // Create a timer and set a two second interval.
        aTimer = new System.Timers.Timer();
        aTimer.Interval = 1000;

        // Hook up the Elapsed event for the timer. 
        aTimer.Elapsed += OnTimedEvent;

        // Have the timer fire repeated events (true is the default)
        aTimer.AutoReset = true;

        // Start the timer
        aTimer.Enabled = true;

        Console.WriteLine($"Program will exit after {seconds} seconds,\n\nOr Press the Enter key to exit the program\n");
        if (seconds > 0)
            Console.ReadLine();
        return;
    }
}