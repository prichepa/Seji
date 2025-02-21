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
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Media;
using System.Numerics;

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

            try
            {
                client.Connect(new IPEndPoint(IPAddress.Parse("26.10.226.173"), 80));

                byte[] bUserInfo = Encoding.UTF8.GetBytes(login.Length + "." + login + password);
                
                if (entrType == 's')
                {
                    byte[] bAvatarLength = Encoding.UTF8.GetBytes("." + Convert.ToString(avatar.Length));
                    Array.Resize(ref bUserInfo, bUserInfo.Length + avatar.Length + bAvatarLength.Length);

                    for (int i = 0; i < avatar.Length; i++)
                    {
                        bUserInfo[i + bUserInfo.Length - avatar.Length - bAvatarLength.Length] = avatar[i];
                    }
                    for (int i = 0; i < bAvatarLength.Length; i++)
                    {
                        bUserInfo[i + bUserInfo.Length - bAvatarLength.Length] = bAvatarLength[i];
                    }
                }
                Array.Resize(ref bUserInfo, bUserInfo.Length + 1);
                bUserInfo[bUserInfo.Length - 1] = Convert.ToByte(entrType);

                client.Send(bUserInfo);

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
                else if (extension == ".mp4" || extension == ".gif" || extension == ".mp3")
                {
                    MediaElement media = new MediaElement
                    {
                        Source = new Uri(filePath, UriKind.Absolute),
                        LoadedBehavior = MediaState.Manual,
                        Height = 200
                    };
                    media.Pause();

                    StackPanel? panel = null;
                    if (extension == ".mp3")
                    {
                        panel = new StackPanel { Orientation = Orientation.Horizontal };

                        TextBlock textBlock = new TextBlock { Text = "▶", Margin = new Thickness(0, 0, 0, 5) };

                        media.Height = 0;

                        panel.Children.Add(textBlock);
                        panel.Children.Add(media);

                        item = new ListViewItem { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Right };
                    }
                    else
                    {
                        item = new ListViewItem { Content = media, HorizontalContentAlignment = HorizontalAlignment.Right };
                    }

                    WorkWithVideo(item, media, extension, panel);

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

                byte[] bExtension = Encoding.UTF8.GetBytes(extension);

                Array.Resize(ref message, message.Length + bExtension.Length);

                for (int i = 0; i < bExtension.Length; i++)
                {
                    message[i + message.Length - bExtension.Length] = bExtension[i];
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

                    string avatarLength = result.Substring(result.LastIndexOf('.') + 1);
                    byte[] avatar = new byte[Convert.ToInt32(avatarLength)];

                    Array.Resize(ref buffer, buffer.Length - avatarLength.Length - 1);
                    for (int i = 0; i < avatar.Length; i++)
                    {
                        avatar[i] = buffer[buffer.Length - avatar.Length + i];
                    }

                    Array.Resize(ref buffer, buffer.Length - avatar.Length);
                    result = Encoding.UTF8.GetString(buffer);

                    string login = result.Substring(result.LastIndexOf('#') + 9);
                    result = result[..^(login.Length)];

                    string color = result.Substring(result.LastIndexOf('#') + 1);

                    string extension = result.Substring(result.LastIndexOf('.'), result.LastIndexOf('#') - result.LastIndexOf('.'));

                    byte alpha = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte red = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte green = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    byte blue = byte.Parse(color.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                    Array.Resize(ref buffer, buffer.Length - Encoding.UTF8.GetBytes(login).Length - Encoding.UTF8.GetBytes(color).Length - 1 - Encoding.UTF8.GetBytes(extension).Length);

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

                        ListViewItem item = new ListViewItem();
                        item.Content = panel;

                        Window?.lvChat?.Items.Add(item);
                    });

                    string message = "";
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
                        else if (extension == ".mp4" || extension == ".gif" || extension == ".mp3")
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

                                ListViewItem item;
                                StackPanel? panel = null;
                                if (extension == ".mp3")
                                {
                                    panel = new StackPanel { Orientation = Orientation.Horizontal };

                                    TextBlock textBlock = new TextBlock { Text = "▶", Margin = new Thickness(0, 0, 0, 5) };

                                    media.Height = 0;

                                    panel.Children.Add(textBlock);
                                    panel.Children.Add(media);

                                    item = new ListViewItem { Content = panel };
                                }
                                else
                                {
                                    item = new ListViewItem { Content = media };
                                }
                                
                                WorkWithVideo(item, media, extension, panel);

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
                            TextBlock textBlock = new TextBlock { Text = "▶", Margin = new Thickness(0, 0, 0, 5) };

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
                            TextBlock textBlock = new TextBlock { Text = "⏸", Margin = new Thickness(0, 0, 0, 5) };

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