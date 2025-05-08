using Move7.Helper;
using Move7.Model;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

internal class Program
{
    static bool running = true;
    static System.Timers.Timer aTimer;
    static int secondsToClose = 5;

    #region Trap application termination
    [DllImport("Kernel32")]
    private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

    private delegate bool EventHandler(CtrlType sig);
    static EventHandler _handler;

    enum CtrlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    private static bool Handler(CtrlType sig)
    {
        running = false;
        //Console.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");
        DB dB = new DB();
        dB.Stop();
        //do your cleanup here
        Thread.Sleep(5000); //simulate some cleanup delay

        //shutdown right away so there are no lingering threads
        Environment.Exit(-1);

        return true;
    }
    #endregion

    static void Main(string[] args)
    {
        // Some boilerplate to react to close window event, CTRL-C, kill, etc
        _handler += new EventHandler(Handler);
        SetConsoleCtrlHandler(_handler, true);

        //must pass first argument to run to prevent run program by double click
        if (args.Length < 2 || args[0] != "RunFromApp")
        {
            ExitCountDown();
            return;
        }
        string username = args[1];

        try
        {
            bool isNewInstance;
            // "Global" ensures visibility across all sessions
            const string mutexName = "Global\\MoveAppV2";
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
                    dB.Stop("Failed to Connect database.");
                    ExitCountDown();
                    return;
                }
                Process process = Process.GetCurrentProcess();
                try
                {
                    dB.Start(username, process.Id, process.ProcessName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Faild to start program.");
                    dB.Stop("Faild to start program.");
                    ExitCountDown();
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
                        Console.WriteLine("Faild to get configurations.");
                        dB.Stop("Faild to get configurations.");
                        ExitCountDown();
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
                        dB.Stop("Error occurred while reading dept-ext data from database.");
                        ExitCountDown();
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
                        dB.Stop(msg);
                        ExitCountDown();
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
                        dB.Stop(ex.Message);
                        ExitCountDown();
                        return;
                    }
                }
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.WriteLine("Access Control List (ACL) APIs are part of resource management on Windows and are not supported on this platform.");
            ExitCountDown();
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Program is already running.");
            ExitCountDown();
        }
        catch
        {
            Console.WriteLine("Something went wrong.");
            ExitCountDown();
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
        Console.Write(--secondsToClose);
        if (secondsToClose == 0)
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

        Console.WriteLine($"Program will exit after {secondsToClose} seconds,\n\nOr Press the Enter key to exit the program\n");
        if (secondsToClose > 0)
            Console.ReadLine();
        return;
        //Environment.Exit(1);
    }

}