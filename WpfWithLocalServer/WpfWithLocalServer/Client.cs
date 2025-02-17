using System.Net.Sockets;
using System.Windows.Media;

namespace WpfWithLocalServer
{
    public class Client
    {
        public Socket? Socket { get; set; }

        public User? User { get; set; }
    }
}