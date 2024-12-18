using Move7.Helper;
using Move7.Model;

internal class Program
{
    static bool running = true;
    static void Main(string[] args)
    {
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
        DB dB = new DB(Configuration.DatabaseIP);
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
                Console.WriteLine("Program will exit. Press Enter to continue");
                Console.ReadLine();
                return;
            }
        }
    }
}