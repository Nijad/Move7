using Move7.Helper;
using Move7.Model;
using System.Security.AccessControl;
using System.Security.Principal;
//test 2 
//test branch
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

                XML xML;

                Console.WriteLine("Start Move Program");
                Console.WriteLine();
                try
                {
                    xML = new XML();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine("Reading Extensions file...");
                try
                {
                    xML.GetExtensions();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine("Reading 'Extensions.xml' file failed");
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine("Reading Paths file...");
                List<PathInfo> paths = new List<PathInfo>();

                try
                {
                    xML.GetPaths(paths);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine("Reading 'Paths.xml' file failed");
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine("Reading Config file...");

                try
                {
                    xML.GetConfig();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Program will exit. Press Enter to continue");
                    Console.ReadLine();
                    return;
                }

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

                try
                {
                    dB.CreateTableIfNotExist();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Console.WriteLine("Can not create table on database");
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
                        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + $"    -    run no {++i}");
                        foreach (PathInfo path in paths)
                            MoveOperations.MoveFiles(path);

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