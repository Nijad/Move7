
using Move7.Model;
using System.Net.Mail;
using System.Text;

namespace Move7.Helper
{
    internal class Logging
    {
        public static void LogException(Exception exc)
        {
            try
            {
                string execPath = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = execPath + @"\Log\";
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);
                string logBaseName = "error log";
                string logFile = logDirectory + logBaseName + "_" + DateTime.Today.ToString("dd-MM-yyyy") + ".txt";

                // Open the log file for append and write the log
                StreamWriter sw = new StreamWriter(logFile, true);
                sw.WriteLine("******************** {0} ********************", DateTime.Now);

                sw.Write("Exception Type: ");
                sw.WriteLine(exc.GetType().ToString());
                sw.WriteLine("Exception: ");
                sw.WriteLine(ErrorMsg(exc));
                sw.WriteLine("Source: " + exc.Source);
                sw.WriteLine("Stack Trace: ");
                if (exc.StackTrace != null)
                {
                    sw.WriteLine(exc.StackTrace);
                    sw.WriteLine();
                }
                sw.Close();
            }
            catch (Exception e)
            {
                SendEmail(e, "Can not write in log.");
            }
        }

        private static string ErrorMsg(Exception e)
        {
            string m = e.Message;

            StringBuilder msg = new StringBuilder(m);

            Exception ie = e.InnerException;
            while (ie != null)
            {
                msg = msg.AppendLine(ie.Message);
                ie = ie.InnerException;
            }

            return msg.ToString();
        }

        public static void WriteNotes(string msg)
        {
            try
            {
                string execPath = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = execPath + @"\Log\";
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);
                string logBaseName = "Notes";
                string logFile = logDirectory + logBaseName + "_" + DateTime.Today.ToString("dd-MM-yyyy") + ".txt";

                // Open the log file for append and write the log
                StreamWriter sw = new StreamWriter(logFile, true);
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                sw.WriteLine($"{now} | {msg}");
                sw.WriteLine("----------------------------------------------------------------------------------------------");
                sw.Close();
            }
            catch (Exception e)
            {
                SendEmail(e, "Can not write in notes");
            }
        }

        public static void SendEmail(Exception ex, string msg = "")
        {
            return;
            try
            {
                SmtpClient smtp = new SmtpClient();
                using (MailMessage message = new MailMessage())
                {
                    message.From = new MailAddress("move@bso.com.sy");
                    message.Subject = "MoveApp Error Occourd";
                    foreach (string developer in Configuration.Developers)
                    {
                        message.To.Add($"{developer}@bso.com.sy");
                    }
                    string body = ex.Message;
                    body += "\n" + ex.Source;
                    body += "\n" + ex.StackTrace;
                    message.Body = body;
                    smtp.Host = "192.168.0.124"; ;
                    smtp.Port = 25;
                    smtp.Send(message);

                    if (!string.IsNullOrEmpty(msg))
                    {
                        message.To.Clear();
                        foreach (string admin in Configuration.Admins)
                        {
                            message.To.Add($"{admin}@bso.com.sy");
                        }
                        message.Body = msg;
                        smtp.Send(message);
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                WriteNotes("Can not send email.");
            }
        }
    }
}
