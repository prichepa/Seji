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

        public static Client FillClient(Socket? clientSocket, User user)
        {
            Client client = new Client();

            client.Socket = clientSocket;
            client.User = user;

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
                        Array.Resize(ref buffer, bytesCount);
                        string receivedUserInfo = Encoding.UTF8.GetString(buffer, 0, bytesCount);

                        char entrType = receivedUserInfo[receivedUserInfo.Length - 1];

                        Array.Resize(ref buffer, bytesCount - 1);
                        receivedUserInfo = receivedUserInfo[..^1];

                        byte[] avatar = new byte[0];
                        if (entrType == 's')
                        {
                            string avatarLength = receivedUserInfo.Substring(receivedUserInfo.LastIndexOf('.') + 1);
                            avatar = new byte[Convert.ToInt32(avatarLength)];

                            Array.Resize(ref buffer, buffer.Length - avatarLength.Length - 1);

                            for (int i = 0; i < avatar.Length; i++)
                            {
                                avatar[i] = buffer[buffer.Length - avatar.Length + i];
                            }

                            Array.Resize(ref buffer, buffer.Length - avatar.Length);
                            receivedUserInfo = Encoding.UTF8.GetString(buffer);
                        }
                        int loginLength = Convert.ToInt32(receivedUserInfo.Substring(0, receivedUserInfo.IndexOf('.')));
                        string login = receivedUserInfo.Substring(receivedUserInfo.IndexOf('.') + 1, receivedUserInfo.IndexOf('.') + loginLength - 1);
                        string password = receivedUserInfo.Substring(receivedUserInfo.IndexOf('.') + 1 + loginLength);

                        if(entrType == 's')
                        {
                            Random random = new Random();
                            var color = new SolidColorBrush(Color.FromRgb(
                                (byte)random.Next(0, 255),
                                (byte)random.Next(0, 255),
                                (byte)random.Next(0, 255)
                            ));

                            if (WorkWithDB.AddUser(login, password, avatar, color))
                            {
                                clientSocket.Send(Encoding.UTF8.GetBytes(Convert.ToString(1)));

                                client = FillClient(clientSocket, WorkWithDB.currentUser);
                                clients.Add(client);
                                clientFilled = true;

                                Window?.Dispatcher.Invoke(() => Window.labelClients.Content = $"Clients: {clients.Count}");
                            }
                            else
                            {
                                clientSocket.Send(Encoding.UTF8.GetBytes(Convert.ToString(0)));
                            }
                        }
                        else
                        {
                            if (WorkWithDB.ConnectUser(login, password))
                            {
                                clientSocket.Send(Encoding.UTF8.GetBytes(Convert.ToString(1)));

                                client = FillClient(clientSocket, WorkWithDB.currentUser);
                                clients.Add(client);
                                clientFilled = true;

                                Window?.Dispatcher.Invoke(() => Window.labelClients.Content = $"Clients: {clients.Count}");
                            }
                            else
                            {
                                clientSocket.Send(Encoding.UTF8.GetBytes(Convert.ToString(0)));
                            }
                        }
                    }
                    else
                    {
                        Array.Resize(ref buffer, bytesCount);

                        string colorNLogin = client.User.ColorBrush.ToString() + client.User.Login;
                        byte[] bColorNLogin = Encoding.UTF8.GetBytes(colorNLogin);
                        byte[] avatarLength = Encoding.UTF8.GetBytes("." + Convert.ToString(client.User.Avatar.Length));

                        Array.Resize(ref buffer, buffer.Length + bColorNLogin.Length + client.User.Avatar.Length + avatarLength.Length);
                        
                        for (int i = 0; i < bColorNLogin.Length; i++)
                        {
                            buffer[i + buffer.Length - bColorNLogin.Length - client.User.Avatar.Length - avatarLength.Length] = bColorNLogin[i];
                        }
                        for (int i = 0; i < client.User.Avatar.Length; i++)
                        {
                            buffer[i + buffer.Length - client.User.Avatar.Length - avatarLength.Length] = client.User.Avatar[i];
                        }
                        for (int i = 0; i < avatarLength.Length; i++)
                        {
                            buffer[i + buffer.Length - avatarLength.Length] = avatarLength[i];
                        }

                        Broadcast(buffer, client);
                    }

                    buffer = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    buffer = new byte[536870912];
                }
            }
            catch(Exception ex)
            {
                Window?.Dispatcher.Invoke(() => Window.lvChat.Items.Add(ex.Message));
                clientSocket?.Close();
                clients.Remove(client);
            }
            finally
            {
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