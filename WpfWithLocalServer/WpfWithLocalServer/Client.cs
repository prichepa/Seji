using System.Net.Sockets;
using System.Windows.Media;

namespace WpfWithLocalServer
{
    public class Client
    {
        public Socket? Socket { get; set; }
        public string? Login { get; set; }
        public SolidColorBrush? ColorBrush { get; set; }
        public DateTime? DateOfConnection { get; set; }

        public override string ToString()
        {
            return $"{Login} | {DateOfConnection.Value.Hour}:{DateOfConnection.Value.Minute}";
        }
    }
}