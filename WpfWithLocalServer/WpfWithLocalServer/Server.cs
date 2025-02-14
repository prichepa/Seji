using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfWithLocalServer
{
    public static class Server
    {
        public static List<Client> clients = new List<Client>();
        public static Socket? ServerSocket { get; set; }
        public static MainWindow? Window { get; set; }

        public static void Start()
        {
            Window = Application.Current.MainWindow as MainWindow;

            ServerSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
                );

            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, 80));
            ServerSocket.Listen(100);

            ListViewItem item = new ListViewItem();
            item.Foreground = Brushes.Indigo;
            item.Content = "Server online";
            Window?.lvChat.Items.Add(item);

            Task.Run(WaitForClients);
        }

        public static void WaitForClients()
        {
            while (true)
            {
                Socket? clientSocket = ServerSocket?.Accept();

                Task.Run(() => ManageClient(clientSocket));
            }
        }

        public static Client FillClient(Socket? clientSocket, string? login)
        {
            Client client = new Client();
            Random random = new Random();

            client.Login = login;
            client.Socket = clientSocket;
            client.DateOfConnection = DateTime.Now;
            client.ColorBrush = new SolidColorBrush(Color.FromRgb(
                (byte)random.Next(0, 255),
                (byte)random.Next(0, 255),
                (byte)random.Next(0, 255)
                ));

            return client;
        }

        public static void ManageClient(Socket? clientSocket)
        {
            bool clientFilled = false;
            Client client = new Client();

            byte[]? buffer = new byte[536870912];
            int bytesCount;

            try
            {
                while ((bytesCount = clientSocket.Receive(buffer)) > 0)
                {
                    if (!clientFilled)
                    {
                        string login = Encoding.UTF8.GetString(buffer, 0, bytesCount);
                        client = FillClient(clientSocket, login);
                        clients.Add(client);
                        clientFilled = true;

                        Window?.Dispatcher.Invoke(() => Window?.lvClients.Items.Add(client));
                        Window?.Dispatcher.Invoke(() => Window?.lvChat.Items.Add($"{DateTime.Now} connected {client.Login}"));
                        Window?.Dispatcher.Invoke(() => Window?.lvChat?.ScrollIntoView($"{DateTime.Now} connected {client.Login}"));
                        Window?.Dispatcher.Invoke(() => Window.labelClients.Content = $"Clients: {clients.Count}");
                    }
                    else
                    {
                        Array.Resize(ref buffer, bytesCount);

                        string colorNLogin = client.ColorBrush.ToString() + client.Login;
                        byte[] bColorNLogin = Encoding.ASCII.GetBytes(colorNLogin);

                        Array.Resize(ref buffer, buffer.Length + bColorNLogin.Length);

                        for (int i = 0; i < bColorNLogin.Length; i++)
                        {
                            buffer[i + bytesCount] = bColorNLogin[i];
                        }

                        Broadcast(buffer, client);

                        buffer = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        buffer = new byte[536870912];
                    }
                }
            }
            catch { }
            finally
            {
                Window?.Dispatcher.Invoke(() => Window?.lvChat.Items.Add($"{DateTime.Now} disconnected {client.Login}"));
                Window?.Dispatcher.Invoke(() => Window?.lvChat?.ScrollIntoView($"{DateTime.Now} disconnected {client.Login}"));
                Window?.Dispatcher.Invoke(() => Window?.lvClients.Items.Remove(client));

                clientSocket?.Close();
                clients.Remove(client);

                Window?.Dispatcher.Invoke(() => Window.labelClients.Content = $"Clients: {clients.Count}");
            }
        }

        /*
        public static void PrintMessage(string? message, Color color, string login)
        {
            Window?.Dispatcher.Invoke(() => {
                ListViewItem item = new ListViewItem() {
                    Content = $"{login}: " + message,
                    Foreground = new SolidColorBrush(color)
                };

                Window?.lvChat?.Items.Add(item);
                Window?.lvChat?.ScrollIntoView(item);
            });
        }*/

        static void Broadcast(byte[] message, Client currentclient)
        {
            foreach (Client client in clients)
            {
                if (client != currentclient)
                {
                    client.Socket?.Send(message);
                }
            }
        }
    }
}