using Move7.Model;
using System.Xml;

namespace Move7.Helper
{
    internal class XML
    {
        string workPath;
        public XML()
        {
            workPath = AppDomain.CurrentDomain.BaseDirectory + @"\AssetFiles\";
            if (!Directory.Exists(workPath))
            {
                string msg = "Directory of Asset files (xml files) is not exist.";
                Logging.WriteNotes(msg);
                throw new Exception(msg);
            }
        }
        private XmlDocument XmlDoc(string filename)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(filename);
            return xmldoc;
        }

        public void GetExtensions()
        {
            try
            {
                string extensionPath = Path.Combine(workPath, "Extensions.xml");
                XmlDocument xmlDocument = XmlDoc(extensionPath);
                XmlNodeList extensionType = xmlDocument.SelectNodes("Extentions/type");
                Configuration.Extensions = new List<string>();
                foreach (XmlNode type in extensionType)
                    Configuration.Extensions.Add(type.InnerText);
            }
            catch (Exception ex)
            {
                string msg = "Can not read 'Extensions.xml' file.";
                Logging.SendEmail(ex, msg);
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                throw ex;
            }
        }

        public void GetPaths(List<PathInfo> pathInfos)
        {
            try
            {
                string paths = Path.Combine(workPath, "Paths.xml");
                XmlDocument xmlDocument = XmlDoc(paths);
                XmlNodeList pths = xmlDocument.SelectNodes("paths/path");
                foreach (XmlNode pth in pths)
                {
                    PathInfo pathInfo = new PathInfo();
                    pathInfo.Dept = pth["dept"].InnerText;
                    pathInfo.Destination = pth["destination"].InnerText;
                    pathInfo.From = pth["from"].InnerText;
                    pathInfo.To = pth["to"].InnerText;
                    pathInfos.Add(pathInfo);
                }
            }
            catch (Exception ex)
            {
                string msg = "Can not read 'Paths.xml' file.";
                Logging.SendEmail(ex, msg);
                Logging.WriteNotes(msg);
                Logging.LogException(ex);
                throw ex;
            }
        }

        public void GetConfig()
        {
            try
            {
                string configPath = Path.Combine(workPath, "Config.xml");
                XmlDocument xmlDocument = XmlDoc(configPath);
                XmlNode configuration = xmlDocument.SelectSingleNode("Configuration");

                string duration = configuration["Duration"].InnerText;

                int timeDuration = int.Parse(duration.Substring(0, duration.Length - 1));
                
                if (duration.EndsWith("s") || duration.EndsWith("S"))
                    timeDuration *= 1000;
                else if (duration.EndsWith("m") || duration.EndsWith("M"))
                    timeDuration *= 1000 * 60;
                else if (duration.EndsWith("h") || duration.EndsWith("H"))
                    timeDuration *= 1000 * 60 * 60;
                else
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    string msg = "Wrong Duration in 'Config.xml'.";
                    Console.WriteLine(msg);
                    Logging.WriteNotes("Wrong Duration in 'Config.xml'. duration should be like 60s, 2m or 1h");
                    throw new Exception(msg);
                }
                Configuration.Duration = timeDuration;

                string mxFlSz = configuration["MaxFileSize"].InnerText;
                long maxFileSize = long.Parse(mxFlSz.Substring(0, mxFlSz.Length - 1));
                
                if (mxFlSz.EndsWith("k") || mxFlSz.EndsWith("K"))
                    maxFileSize *= 1024;
                else if (mxFlSz.EndsWith("m") || mxFlSz.EndsWith("M"))
                    maxFileSize *= 1024 * 1024;
                else if (mxFlSz.EndsWith("g") || mxFlSz.EndsWith("G"))
                    maxFileSize *= 1024 * 1024 * 1024;
                else
                {
                    Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    string msg = "Wrong MaxFileSize in 'Config.xml'.";
                    Console.WriteLine(msg);
                    Logging.WriteNotes("Wrong MaxFileSize in 'Config.xml'. MaxFileSize should be like 60k, 2m or 1g");
                    throw new Exception(msg);
                }
                Configuration.MaxFileSize = maxFileSize;

                Configuration.DatabaseIP = configuration["DatabaseIP"].InnerText;

                Configuration.BackupPath = configuration["BackupPath"].InnerText;

                Configuration.Admins = configuration["Admins"].InnerText.Split(',').ToList();

                Configuration.Developers = configuration["Developers"].InnerText.Split(',').ToList();

            }
            catch (Exception ex)
            {
                string msg = "Can not read 'Config.xml' file.";
                Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                Console.WriteLine(msg);
                Logging.SendEmail(ex, msg);
                Logging.LogException(ex);
                throw ex;
            }
        }
    }
}
