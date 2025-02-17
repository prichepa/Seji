using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WpfWithLocalServer
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public byte[]? Avatar { get; set; }
        public string? ColorBrush { get; set; }
        public int? ChatId { get; set; }
    }
}
