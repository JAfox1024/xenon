using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xenonServer;

namespace xenonServer
{
    public partial class Node
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        private SemaphoreSlim OneReceiveAtATime = new SemaphoreSlim(1);
        public bool IsDisposed { get; private set; }
        private Action<Node> OnDisconnect;
        private List<Action<Node>> TempOnDisconnects = new List<Action<Node>>();
        public List<Node> SubNodes { get; private set; }
        private Dictionary<int, Node> SubNodeWait { get; set; }
        public SocketHandler Sock { get; set; }
        public Node Parent { get; set; }
        public int ID { get; set; }
        public int SubNodeIdCount { get; set; }
        public int SockType { get; set; } // 0 = main, 1 = heartbeat, 2 = anything else

        public Node(SocketHandler sock, Action<Node> onDisconnect)
        {
            Sock = sock;
            SubNodes = new List<Node>();
            SubNodeWait = new Dictionary<int, Node>();
            OnDisconnect = onDisconnect;
        }

        private byte[] GetByteArray(int size)
        {
            Random rnd = new Random();
            byte[] b = new byte[size];
            rnd.NextBytes(b);
            return b;
        }

        public void SetID(int id)
        {
            ID = id;
        }

        private bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.SequenceEqual(b2);
        }

        private async Task<int> GetSocketType()
        {
            byte[] type = await Sock.ReceiveAsync();
            if (type == null)
            {
                Disconnect();
                return -1;
            }
            int intType = Sock.BytesToInt(type);
            return intType;

        }


        public async void Disconnect()
        {
                if (IsDisposed) return;
                IsDisposed = true;
            try
            {
                if (Sock.sock != null)
                {
                    await Task.Factory.FromAsync(Sock.sock.BeginDisconnect, Sock.sock.EndDisconnect, true, null);
                }
            }
            catch
            {
                Sock.sock?.Close(0);
            }
            Sock.sock?.Dispose();
            OneReceiveAtATime.Dispose();
            if (SockType == 0)
            {
                foreach (Node subNode in SubNodes.ToList())
                {
                    try
                    {
                        if (subNode.SockType != 1)
                        {
                            subNode?.OnDisconnect(null);
                        }
                    }
                    catch { }
                }
            }
            OnDisconnect?.Invoke(this);
            List<Action<Node>> copy = TempOnDisconnects.ToList();
            TempOnDisconnects.Clear();
            foreach (Action<Node> tempDisconnect in copy)
            {
                tempDisconnect(this);
            }
            copy.Clear();
            SubNodes.Remove(this);
        }

        public void SetRecvTimeout(int ms)
        {
            Sock.SetRecvTimeout(ms);
        }

        public void ResetRecvTimeout()
        {
            Sock.ResetRecvTimeout();
        }

        public bool Connected()
        {
            try
            {
                return Sock.sock.Connected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ReceiveAsync()
        {
            if (IsDisposed)
            {
                return null;
            }
            await OneReceiveAtATime.WaitAsync();
            try
            {
                byte[] data = await Sock.ReceiveAsync();
                if (data == null)
                {
                    Disconnect();
                    return null;
                }
                return data;
            }
            finally
            {
                OneReceiveAtATime.Release();
            }
        }

        public async Task<bool> SendAsync(byte[] data)
        {
            if (!(await Sock.SendAsync(data)))
            {
                Disconnect();
                return false;
            }
            return true;
        }

        public string GetIp()
        {
            string ip = "N/A";
            try
            {
                ip = ((IPEndPoint)Sock.sock.RemoteEndPoint).Address.ToString();
            }
            catch
            {
            }
            return ip;
        }

        public async Task<Node> CreateSubNodeAsync(int type) // 1 or 2
        {
            if (type < 1 || type > 2)
            {
                throw new Exception("ID too high or low. Must be 1 or 2.");
            }
            Random rnd = new Random();
            int retId = rnd.Next(1, 256);
            while (SubNodeWait.ContainsKey(retId))
            {
                retId = rnd.Next(1, 256);
            }
            SubNodeWait[retId] = null;
            byte[] createSubReq = new byte[] { 0, (byte)type, (byte)retId };
            await SendAsync(createSubReq);
            byte[] worked = await ReceiveAsync();
            if (worked == null || worked[0] == 0)
            {
                SubNodeWait.Remove(retId);
                return null;
            }
            int count = 0;
            while (SubNodeWait[retId] == null && Connected() && count < 10)
            {
                await Task.Delay(1000);
                count++;
            }
            Node subNode = SubNodeWait[retId];
            SubNodeWait.Remove(retId);
            return subNode;
        }

        public void AddTempOnDisconnect(Action<Node> function)
        {
            TempOnDisconnects.Add(function);
        }

        public void RemoveTempOnDisconnect(Action<Node> function)
        {
            TempOnDisconnects.Remove(function);
        }

        public async Task AddSubNode(Node subNode)
        {
            if (subNode.SockType != 0)
            {
                byte[] retId = await subNode.ReceiveAsync();
                if (retId == null)
                {
                    subNode.Disconnect();
                }
                SubNodeWait[retId[0]] = subNode;
            }
            else
            {
                subNode.Disconnect();
            }
            SubNodes.Add(subNode);
        }

        public async Task<bool> AuthenticateAsync(int id) // First call that should ever be made!
        {
            try
            {
                byte[] randomKey = GetByteArray(100);
                byte[] data;
                if (!(await Sock.SendAsync(randomKey)))
                {
                    return false;
                }
                Sock.SetRecvTimeout(10000);
                data = await Sock.ReceiveAsync();
                if (data == null)
                {
                    return false;
                }
                if (ByteArrayCompare(randomKey, data))
                {
                    if (!(await Sock.SendAsync(new byte[] { 109, 111, 111, 109, 56, 50, 53 })))
                    {
                        return false;
                    }
                    int type = await GetSocketType();
                    if (type > 2 || type < 0)
                    {
                        return false;
                    }
                    if (type == 0)
                    {
                        byte[] sockId = Sock.IntToBytes(id);
                        ID = id;
                        if (!(await Sock.SendAsync(sockId)))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        data = await Sock.ReceiveAsync();
                        if (data == null)
                        {
                            Disconnect();
                            return false;
                        }
                        int sockId = Sock.BytesToInt(data);
                        ID = sockId;
                    }
                    SockType = type;
                    Sock.ResetRecvTimeout();
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
