using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace WpfServerClient
{
    public static class ServerClient
    {
        static Socket client;
        public static MainWindow? Window { get; set; }

        public static bool Start(string login, string password, byte[]? avatar, char entrType)
        {
            Window = Application.Current.MainWindow as MainWindow;

            client = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
                );
            Stickers();

            try
            {
                client.Connect(new IPEndPoint(IPAddress.Parse("26.10.226.173"), 80));

                SendData(Encoding.UTF8.GetBytes(login));
                SendData(Encoding.UTF8.GetBytes(password));
                SendData(Encoding.UTF8.GetBytes(Convert.ToString(entrType)));

                if (entrType == 's')
                {
                    SendData(avatar);
                }

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
            catch (Exception ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);
                return false;
            }
        }
        

        public static void SendMessage(byte[] message, string? extension, string? filePath)
        {
            extension = extension.ToLower();

            try
            {
                ListViewItem item;
                if (extension == ".message")
                {
                    Window?.tbMessage.Clear();
                    Window.tBlockText.Visibility = Visibility.Visible;

                    string strMessage = Encoding.UTF8.GetString(message);

                    item = new ListViewItem()
                    {
                        Content = strMessage,
                        HorizontalContentAlignment = HorizontalAlignment.Right
                    };
                }
                else if (extension == ".png" || extension == ".jpg")
                {
                    item = new ListViewItem { HorizontalContentAlignment = HorizontalAlignment.Right };
                    Image img = new Image();
                    img.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                    img.MaxHeight = 350;
                    img.MaxWidth = 350;
                    item.Content = img;
                    item.SizeChanged += (sender, args) =>
                    {
                        Window?.lvChat?.ScrollIntoView(item);
                    };

                    ShowFolder(item, filePath);
                }
                else if (extension == ".mp4" || extension == ".gif" || extension == ".mp3")
                {
                    MediaElement media = new MediaElement
                    {
                        Source = new Uri(filePath, UriKind.Absolute),
                        LoadedBehavior = MediaState.Manual,
                        MaxHeight = 350,
                        MaxWidth = 350
                    };
                    media.Pause();

                    StackPanel? panel = null;
                    if (extension == ".mp3")
                    {
                        panel = new StackPanel { Orientation = Orientation.Horizontal };

                        TextBlock textBlock = new TextBlock { Text = "▶", FontFamily = new FontFamily("Segoe UI Emoji") };

                        media.Height = 0;

                        panel.Children.Add(textBlock);
                        panel.Children.Add(media);

                        item = new ListViewItem { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Right };
                    }
                    else
                    {
                        item = new ListViewItem { Content = media, HorizontalContentAlignment = HorizontalAlignment.Right };
                        item.SizeChanged += (sender, args) =>
                        {
                            Window?.lvChat?.ScrollIntoView(item);
                        };
                    }

                    WorkWithVideo(item, media, extension, panel);

                    ShowFolder(item, filePath);
                }
                else
                {
                    StackPanel panel = new StackPanel() { Orientation = Orientation.Horizontal };
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri("fileicon.png", UriKind.RelativeOrAbsolute);
                    bi.EndInit();
                    Image img = new Image()
                    {
                        Source = bi,
                        Height = 25,
                        Width = 25,
                    };

                    panel.Children.Add(img);
                    TextBlock text = new TextBlock()
                    {
                        Text = " " + Path.GetFileName(filePath),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    panel.Children.Add(text);
                    item = new ListViewItem()
                    {
                        Content = panel,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    OpenFile(item, filePath);
                    ShowFolder(item, filePath);
                }

                Window?.lvChat?.Items.Add(item);
                Window?.lvChat?.ScrollIntoView(item);

                byte[] bExtension = Encoding.UTF8.GetBytes(extension);
                byte[] bSecondUserName = Encoding.UTF8.GetBytes("aboba");
                SendData(bSecondUserName);
                SendData(bExtension);
                SendData(message);
            }
            catch (SocketException ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);
            }
        }
        private static void SendData(byte[] data)
        {
            byte[] length = BitConverter.GetBytes(data.Length);
            client.Send(length);
            client.Send(data);
        }
        public static void Stickers()
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Stickers");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string[] files = Directory.GetFiles(folderPath);
            for(int i = 0; i < files.Length; i++)
            {
                string filePath = Path.Combine(folderPath, $"Sticker{i}.png");
                Window?.Dispatcher.Invoke(() =>
                {
                    Image img = new Image()
                    {
                        MaxHeight = 100,
                        MaxWidth = 100,
                        Source = new BitmapImage(new Uri(filePath, UriKind.Absolute))
                    };
                    ListViewItem item = new ListViewItem()
                    {
                        Content = img
                    };
                    Window?.Stickers.Items.Add(item);
                    SendStikers(item, filePath);
                });
            }
        }
        public static void SendStikers(ListViewItem item, string filePath)
        {
            item.PreviewMouseLeftButtonDown += (s, e) =>
            {
                byte[] file = System.IO.File.ReadAllBytes(filePath);
                byte[] bSecondUserName = Encoding.UTF8.GetBytes("aboba");
                byte[] bExtension = Encoding.UTF8.GetBytes(".png");
                Window?.Dispatcher.Invoke(() =>
                {
                    Image img = new Image()
                    {
                        Source = new BitmapImage(new Uri(filePath, UriKind.Absolute))
                    };
                    ListViewItem item = new ListViewItem()
                    {
                        Content = img,
                        HorizontalContentAlignment = HorizontalAlignment.Right
                    };
                    Window?.lvChat.Items.Add(item);
                });
                SendData(bSecondUserName);
                SendData(bExtension);
                SendData(file);
            };
        }

        private static void RecieveMessages()
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            try
            {
                while (true)
                {
                    byte[]? bExtension = ReceiveData();
                    string? extension = Encoding.UTF8.GetString(bExtension);

                    byte[]? buffer = ReceiveData();
                    if (buffer == null) break;

                    byte[]? bLogin = ReceiveData();
                    string? login = Encoding.UTF8.GetString(bLogin);

                    byte[]? avatar = ReceiveData();

                    byte[]? bColor = ReceiveData();
                    string? color = Encoding.UTF8.GetString(bColor)[1..];
                    byte alpha = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte red = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte green = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    byte blue = byte.Parse(color.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                    ListViewItem item;
                    Window?.Dispatcher.Invoke(() =>
                    {
                        string[] files = Directory.GetFiles(folderPath);
                        string filePath = Path.Combine(folderPath, $"file({files.Length}).jpg");
                        File.WriteAllBytes(filePath, avatar);

                        StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };

                        Image img = new Image();
                        img.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                        img.Width = 20;

                        TextBlock textBlock = new TextBlock { Text = $"{login}: ", Margin = new Thickness(5, 0, 0, 0), Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, red, green, blue)) };

                        panel.Children.Add(img);
                        panel.Children.Add(textBlock);

                        item = new ListViewItem();
                        item.Content = panel;

                        Window?.lvChat?.Items.Add(item);
                    });

                    string message;
                    if (extension == ".message")
                    {
                        message = Encoding.UTF8.GetString(buffer);

                        Window?.Dispatcher.Invoke(() =>
                        {
                            item = new ListViewItem()
                            {
                                Content = message,
                                Foreground = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue))
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
                                item = new ListViewItem();
                                Image img = new Image();
                                img.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                                img.MaxHeight = 350;
                                img.MaxWidth = 350;
                                item.Content = img;
                                item.SizeChanged += (sender, args) =>
                                {
                                    Window?.lvChat?.ScrollIntoView(item);
                                };

                                Window?.lvChat?.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                                ShowFolder(item, filePath);
                            });
                        }
                        else if (extension == ".mp4" || extension == ".gif" || extension == ".mp3")
                        {
                            Window?.Dispatcher.Invoke(() =>
                            {
                                MediaElement media = new MediaElement
                                {
                                    Source = new Uri(filePath, UriKind.Absolute),
                                    LoadedBehavior = MediaState.Manual,
                                    MaxHeight = 350,
                                    MaxWidth = 350
                                };
                                media.Pause();

                                StackPanel? panel = null;
                                if (extension == ".mp3")
                                {
                                    panel = new StackPanel { Orientation = Orientation.Horizontal };

                                    TextBlock textBlock = new TextBlock { Text = "▶", FontFamily = new FontFamily("Segoe UI Emoji") };

                                    media.Height = 0;

                                    panel.Children.Add(textBlock);
                                    panel.Children.Add(media);

                                    item = new ListViewItem { Content = panel };
                                }
                                else
                                {
                                    item = new ListViewItem { Content = media };
                                    item.SizeChanged += (sender, args) =>
                                    {
                                        Window?.lvChat?.ScrollIntoView(item);
                                    };
                                }

                                WorkWithVideo(item, media, extension, panel);

                                Window?.lvChat?.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                                ShowFolder(item, filePath);
                            });
                        }
                        else
                        {
                            Window?.Dispatcher.Invoke(() =>
                            {
                                StackPanel panel = new StackPanel() { Orientation = Orientation.Horizontal };
                                var bi = new BitmapImage();
                                bi.BeginInit();
                                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                                bi.CacheOption = BitmapCacheOption.OnLoad;
                                bi.UriSource = new Uri("fileicon.png", UriKind.RelativeOrAbsolute);
                                bi.EndInit();
                                Image img = new Image()
                                {
                                    Source = bi,
                                    Height = 25,
                                    Width = 25,
                                };

                                panel.Children.Add(img);
                                TextBlock text = new TextBlock()
                                {
                                    Text = " " + Path.GetFileName(filePath),
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                panel.Children.Add(text);
                                item = new ListViewItem()
                                {
                                    Content = panel
                                };

                                OpenFile(item, filePath);

                                Window?.lvChat?.Items.Add(item);
                                Window?.lvChat?.ScrollIntoView(item);
                                ShowFolder(item, filePath);
                            });
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                Window?.Dispatcher.Invoke(() => Window?.lvChat?.Items.Add(ex.Message));
            }
        }

        private static byte[] ReceiveData()
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

        private static void ShowFolder(ListViewItem item, string filePath)
        {
            item.PreviewMouseRightButtonDown += (s, e) =>
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            };
        }

        private static void OpenFile(ListViewItem item, string filePath)
        {
            item.PreviewMouseLeftButtonDown += (s, e) =>
            {
                string file = Path.GetFileName(filePath);
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            };
        }

        private static void WorkWithVideo(ListViewItem item, MediaElement media, string extension, StackPanel? panel)
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

                        if (extension == ".mp3")
                        {
                            panel.Children.Clear();
                            TextBlock textBlock = new TextBlock { Text = "▶", FontFamily = new FontFamily("Segoe UI Emoji") };

                            panel.Children.Add(textBlock);
                            panel.Children.Add(media);

                            item.Content = panel;
                        }
                    }
                    else
                    {
                        media.Play();
                        isPlaying = true;

                        if (extension == ".mp3")
                        {
                            panel.Children.Clear();
                            TextBlock textBlock = new TextBlock { Text = "⏸" };

                            panel.Children.Add(textBlock);
                            panel.Children.Add(media);

                            item.Content = panel;
                        }
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