using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

namespace xenonServer
{
    public class TcpListenerManager
    {
        public Dictionary<int, TcpPortListener> Listeners { get; } = new Dictionary<int, TcpPortListener>();
        private readonly Func<Socket, Task> _connectCallback;

        public TcpListenerManager(Func<Socket, Task> connectCallback)
        {
            _connectCallback = connectCallback;
        }

        public bool IsPortInUse(int port)
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipProperties.GetActiveTcpListeners().Any(endPoint => endPoint.Port == port);
        }

        public void CreateListener(int port)
        {
            if (IsPortInUse(port))
            {
                Console.WriteLine("当前端口已占用!");
                return;
            }

            if (!Listeners.ContainsKey(port))
            {
                Listeners[port] = new TcpPortListener(port);
            }

            try
            {
                Listeners[port].StartListening(_connectCallback);
            }
            catch (Exception ex)
            {
                Listeners[port].StopListening();
                Console.WriteLine($"端口监听异常: {ex.Message}");
            }
        }

        public void StopListener(int port)
        {
            if (Listeners.ContainsKey(port))
            {
                Listeners[port].StopListening();
            }
        }
    }

    public class TcpPortListener
    {
        private Socket _listener;
        private readonly int _port;
        public bool IsListening { get; private set; }

        public TcpPortListener(int port)
        {
            _port = port;
        }

        public async Task StartListening(Func<Socket, Task> connectCallback)
        {
            var ipAddress = IPAddress.Any;
            var localEndPoint = new IPEndPoint(ipAddress, _port);
            _listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _listener.Bind(localEndPoint);
            _listener.Listen(100);
            IsListening = true;

            while (IsListening)
            {
                try
                {
                    var handler = await _listener.AcceptAsync();
                    await connectCallback(handler);
                }
                catch (ObjectDisposedException)
                {
                    // Listener has been stopped
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"连接异常: {e.Message}");
                }
            }
        }

        public void StopListening()
        {
            IsListening = false;
            try { _listener.Shutdown(SocketShutdown.Both); } catch { }
            try { _listener.Close(); } catch { }
            try { _listener.Dispose(); } catch { }
        }
    }
}