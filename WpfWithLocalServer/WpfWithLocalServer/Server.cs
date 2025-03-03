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
using System.Windows.Media.Imaging;

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

            try
            {
                while (true)
                {
                    if (!clientFilled)
                    {
                        string login = Encoding.UTF8.GetString(ReceiveData(clientSocket));
                        string password = Encoding.UTF8.GetString(ReceiveData(clientSocket));
                        char entrType = Encoding.UTF8.GetString(ReceiveData(clientSocket))[0];

                        if (entrType == 's')
                        {
                            byte[] avatar = ReceiveData(clientSocket);

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
                                //int chatId;
                                //WorkWithDB.GetChatMessage(chatId, i);
                                //chats(💀)

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
                        string secondUser = Encoding.UTF8.GetString(ReceiveData(client.Socket));
                        byte[] extension = ReceiveData(client.Socket);
                        byte[] message = ReceiveData(client.Socket);
                        byte[] login = Encoding.UTF8.GetBytes(client.User.Login);
                        byte[] avatar = client.User.Avatar;
                        byte[] color = Encoding.UTF8.GetBytes(client.User.ColorBrush);

                        WorkWithDB.AddMessage(extension, message, client.User.Id, secondUser);

                        SendData(extension, client.Socket);
                        SendData(message, client.Socket);
                        SendData(login, client.Socket);
                        SendData(avatar, client.Socket);
                        SendData(color, client.Socket);
                    }
                }
            }
            catch(Exception ex)
            {
                Window?.Dispatcher.Invoke(() => Window.lvChat.Items.Add(ex.Message));
            }
            finally
            {
                clientSocket?.Close();
                clients.Remove(client);

                Window?.Dispatcher.Invoke(() => Window.labelClients.Content = $"Clients: {clients.Count}");
            }
        }
        private static byte[] ReceiveData(Socket client)
        {
            byte[] lengthBuffer = new byte[4];
            int bytesRead = client.Receive(lengthBuffer);
            if (bytesRead < 4) return null;

            int dataLength = BitConverter.ToInt32(lengthBuffer);
            byte[] buffer = new byte[dataLength];

            int totalBytesRead = 0;
            while (totalBytesRead < dataLength)
            {
                bytesRead = client.Receive(buffer, totalBytesRead, dataLength - totalBytesRead, SocketFlags.None);
                totalBytesRead += bytesRead;
            }

            return buffer;
        }

        private static void SendData(byte[] data, Socket? clientSocket)
        {
            byte[]? length = BitConverter.GetBytes(data.Length);
            if (length != null)
            {
                foreach(var client in clients)
                {
                    if(client.Socket != clientSocket)
                    {
                        client.Socket.Send(length);
                        client.Socket.Send(data);
                    }
                }
            }
        }
    }
}