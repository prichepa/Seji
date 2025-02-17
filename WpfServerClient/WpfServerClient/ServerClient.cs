using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Drawing;
using System.Windows.Interop;
using System.IO;
using System.Dynamic;
using System.Collections;
using System.Windows.Media.Imaging;

namespace WpfServerClient
{
    public static class ServerClient
    {
        static Socket client;
        public static MainWindow? Window { get; set; }

        public static bool Start(string login, string password, char entrType)
        {
            Window = Application.Current.MainWindow as MainWindow;

            client = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
                );

            try
            {
                client.Connect(new IPEndPoint(IPAddress.Parse("26.10.226.173"), 80));

                client.Send(Encoding.UTF8.GetBytes(login.Length + "." + login + password + entrType));

                byte[] buffer = new byte[1];
                client.Receive(buffer);

                if (Convert.ToBoolean(Convert.ToInt32(Encoding.UTF8.GetString(buffer))))
                {
                    Task.Run(RecieveMessages);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (SocketException ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);
                return false;
            }
        }

        public static void SendMessage(byte[] message, string? extension, string? filePath)
        {
            try
            {
                int lengthWithoutExtension = message.Length;
                byte[] bExtension = Encoding.UTF8.GetBytes(extension);

                Array.Resize(ref message, message.Length + bExtension.Length);

                for (int i = 0; i < bExtension.Length; i++)
                {
                    message[i + lengthWithoutExtension] = bExtension[i];
                }

                ListViewItem item;
                if (extension == ".message")
                {
                    Window?.tbMessage.Clear();
                    Window.tBlockText.Visibility = Visibility.Visible;

                    string strMessage = Encoding.UTF8.GetString(message);
                    strMessage = strMessage[..^8];

                    item = new ListViewItem()
                    {
                        Content = strMessage,
                        HorizontalContentAlignment = HorizontalAlignment.Right
                    };

                    Window?.lvChat?.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                }
                else if (extension == ".png" || extension == ".jpg")
                {
                    item = new ListViewItem { HorizontalContentAlignment = HorizontalAlignment.Right };
                    Image img = new Image();
                    img.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                    img.Height = 200;
                    item.Content = img;

                    Window?.lvChat?.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                }
                else if (extension == ".mp4")
                {
                    MediaElement media = new MediaElement
                    {
                        Source = new Uri(filePath, UriKind.Absolute),
                        LoadedBehavior = MediaState.Manual,
                        Height = 200
                    };
                    media.Pause();

                    item = new ListViewItem
                    {
                        Content = media,
                        HorizontalContentAlignment = HorizontalAlignment.Right
                    };

                    WorkWithVideo(item, media);

                    Window.lvChat.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                }
                else
                {
                    item = new ListViewItem()
                    {
                        Content = "*File sended*",
                        HorizontalContentAlignment = HorizontalAlignment.Right,
                    };

                    Window?.lvChat?.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                }

                client.Send(message);
            }
            catch (SocketException ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);//ex when typing and no server
            }
        }

        private static void RecieveMessages()
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            byte[]? buffer = new byte[536870912];
            int bytesCount = 0;

            try
            {
                while ((bytesCount = client.Receive(buffer)) > 0)
                {
                    Array.Resize(ref buffer, bytesCount);

                    string result = Encoding.UTF8.GetString(buffer);
                    string extensionColorLogin = result.Substring(result.LastIndexOf('.'));

                    byte[] bExtensionColorLogin = Encoding.UTF8.GetBytes(extensionColorLogin);
                    Array.Resize(ref buffer, bytesCount - bExtensionColorLogin.Length);
                    string sigma = Encoding.UTF8.GetString(buffer);

                    string extension = extensionColorLogin.Substring(0, extensionColorLogin.IndexOf('#'));
                    string color = extensionColorLogin.Substring(extensionColorLogin.IndexOf('#') + 1, 9);
                    string login = extensionColorLogin.Substring(extensionColorLogin.IndexOf('#') + 9);

                    byte alpha = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte red = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte green = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    byte blue = byte.Parse(color.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                    string message = $"{login}: ";
                    if (extension == ".message")
                    {
                        message += Encoding.UTF8.GetString(buffer);

                        Window?.Dispatcher.Invoke(() =>
                        {
                            ListViewItem item = new ListViewItem()
                            {
                                    Content = message,
                                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, red, green, blue))
                            };

                            Window?.lvChat?.Items.Add(item);
                            Window?.lvChat?.ScrollIntoView(item);
                        });
                    }
                    else
                    {
                        string[] files = Directory.GetFiles(folderPath);
                        string filePath = Path.Combine(folderPath, $"file({files.Length}){extension}");
                        File.WriteAllBytes(filePath, buffer);

                        if (extension == ".png" || extension == ".jpg")
                        {
                            Window?.Dispatcher.Invoke(() =>
                            {
                                ListViewItem item = new ListViewItem();
                                Image img = new Image();
                                img.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                                img.Height = 200;
                                item.Content = img;

                                Window?.lvChat?.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                            });
                        }
                        else if (extension == ".mp4")
                        {
                            Window?.Dispatcher.Invoke(() =>
                            {
                                MediaElement media = new MediaElement
                                {
                                    Source = new Uri(filePath, UriKind.Absolute),
                                    LoadedBehavior = MediaState.Manual,
                                    Height = 200
                                };
                                media.Pause();

                                ListViewItem item = new ListViewItem { Content = media };
                                WorkWithVideo(item, media);

                                Window.lvChat.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                            });
                        }
                        else
                        {
                            Window?.Dispatcher.Invoke(() =>
                            {
                                ListViewItem item = new ListViewItem()
                                {
                                    Content = "*File recieved*"
                                };

                                Window?.lvChat?.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                            });
                        }
                    }

                    buffer = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    buffer = new byte[536870912];
                }
            }
            catch (SocketException ex)
            {
                Window?.Dispatcher.Invoke(() =>
                {
                    Window?.lvChat?.Items.Add(ex.Message);//ex when disconnected
                });
            }
        }

        private static void WorkWithVideo(ListViewItem item, MediaElement media)
        {
            bool isPlaying = false;
            item.PreviewMouseLeftButtonDown += (s, e) =>
            {
                Window?.Dispatcher.Invoke(() =>
                {
                    if (isPlaying)
                    {
                        media.Pause();
                        isPlaying = false;
                    }
                    else
                    {
                        media.Play();
                        isPlaying = true;
                    }

                    media.MediaEnded += (s, e) =>
                    {
                        media.Position = TimeSpan.Zero;
                        media.Play();
                    };
                });
            };
        }
    }
}