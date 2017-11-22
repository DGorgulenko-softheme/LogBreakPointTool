using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.ServiceProcess;

namespace BreakPointTestTool
{
    class Program
    {
        static void Main(string[] args)
        {
            //Load application configuration
            int UpdateInterval = Convert.ToInt32(ConfigurationManager.AppSettings["UpdateInterval"]);
            string LogPath = ConfigurationManager.AppSettings["LogPath"];
            string BreakpointPhrase = ConfigurationManager.AppSettings["BreakpointPhrase"];
            string ProcessNameToKill = ConfigurationManager.AppSettings["ProcessNameToKill"];
            bool ExitFlag = Convert.ToBoolean(ConfigurationManager.AppSettings["ExitFlag"]);
            string IgnoringPhrase = ConfigurationManager.AppSettings["IgnoringPhrase"];
            string ServiceName = ConfigurationManager.AppSettings["ServiceToStart"];
            
            //Validation Application configuration
            if (!ValidateConfig(LogPath, BreakpointPhrase, ProcessNameToKill, ServiceName))
            {
                Console.WriteLine("Please fix configuration and press any key");
                Console.ReadKey();
                Main(args);
            }

            Console.WriteLine("Starting");
            long OldFileLengh = 0;
            long NewBytesCount;
            while (true)
            {
                //Open File without lock
                using (FileStream fs = File.Open(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    //Get Changes
                    if (OldFileLengh == 0)
                    {
                        OldFileLengh = fs.Length;
                    }
                    if (OldFileLengh <= fs.Length)
                    {
                        NewBytesCount = fs.Length - OldFileLengh;
                    }
                    else
                    {
                        //If new Log file has been created - skip it
                        OldFileLengh = 0;
                        continue;
                    }
                    //If changes are too big and they can not be converted to int - we skip it.
                    if (NewBytesCount != 0 && NewBytesCount < Int32.MaxValue)
                    {
                        Console.WriteLine("Checking new  "+ NewBytesCount+" bytes");
                        OldFileLengh = fs.Length; 
                        fs.Seek(-NewBytesCount, SeekOrigin.End);
                        byte[] bytes = new byte[NewBytesCount];
                        fs.Read(bytes, 0, unchecked((int)NewBytesCount));
                        string s = Encoding.Default.GetString(bytes);
                        //Found Magic Phrase - Check for ignoring phrase
                        if (s.Contains(BreakpointPhrase))
                        {
                            if (String.IsNullOrEmpty(IgnoringPhrase) || !s.Contains(IgnoringPhrase))
                            {
                                Console.WriteLine("Get magic phrase!");
                                //Kill Process 
                                Process.GetProcessesByName(ProcessNameToKill).FirstOrDefault().Kill();
                                //If loop is disabled - exiting
                                if (ExitFlag) { break; }
                                Thread.Sleep(UpdateInterval*10);
                                //Restart service after crash
                                if (ServiceName != String.Empty)
                                {
                                    ServiceController service = new ServiceController(ServiceName, "127.0.0.1");
                                    service.Start();
                                }
                            }
                            else
                            {
                                Console.WriteLine("Ignoring magic phrase because too late.");
                            }
                        }                        
                    }
                    else
                    {
                        Console.WriteLine("Skipping "+ NewBytesCount + " bytes");
                        OldFileLengh = fs.Length;
                    }
                    Thread.Sleep(UpdateInterval);
                }
            }

        }

        private static bool ValidateConfig(string logPath, string breakpointPhrase, string processNameToKill, string serviceName)
        {
            //Validate Service param
            try
            {
                if (serviceName != String.Empty)
                { 
                    ServiceController service = new ServiceController(serviceName, "127.0.0.1");
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        Console.WriteLine("Target service is not running");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Can not get Target Service");
                Console.WriteLine(ex);
                return false;
            }

            try
            {
                //Validate that Process are running
                if (!Process.GetProcessesByName(processNameToKill).Any())
                {
                    Console.WriteLine("Target process was not found");
                    return false;
                }
                //Validate that Log path are correct
                if (!File.Exists(logPath))
                {
                    Console.WriteLine("Can not find Log File");
                    return false;
                }
                //Magic word can not be null
                if (breakpointPhrase == String.Empty)
                {
                    Console.WriteLine("BreakPoint phrase can not be empty");
                    return false;
                }
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Can not validate parameters");
                Console.WriteLine(ex);
                return false;
            }
        }
    }
}
