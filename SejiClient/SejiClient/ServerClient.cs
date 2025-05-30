using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SejiClient
{
    public static class ServerClient
    {
        static string url = "http://26.10.226.173:8080/";
        static HttpClient client = new HttpClient { BaseAddress = new Uri(url) };
        static HttpListener listener = new HttpListener();
        public static MainWindow? Window { get; set; }

        static string? currentLogin;
        static string? secondUserLogin;
        static string? secondUserAvatarPath;

        public static async Task<bool> Start(string login, string password, string? avatarPath, char entrType)
        {
            //Window = Application.Current.MainWindow as MainWindow;
            try
            {
                HttpResponseMessage response;

                string? json;
                if (entrType == 's')
                {
                    (json, var fileData) = await SendMultipartRequest("signUp", avatarPath, $"{{\"login\":\"{login}\",\"password\":\"{password}\",\"extension\":\"{Path.GetExtension(avatarPath)}\"}}");
                }
                else
                {
                    (json, var fileData) = await SendMultipartRequest("login", "", $"{{\"login\":\"{login}\",\"password\":\"{password}\"}}");
                }

                var result = JsonConvert.DeserializeObject<dynamic>(json);

                if (result.loginResult == "true")
                {
                    currentLogin = login;

                    if (entrType == 'l')
                    {
                        int chatsNumber = result.chats.Count;
                        for (int i = 0; i < chatsNumber; i++)
                        {
                            Window?.lvChats?.Items.Add((string)result.chats[i]);
                        }
                    }

                    _ = StartReceivingMessages();

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        public static async Task GetChat(string secondUser)
        {
            try
            {
                Window.lvChats.IsEnabled = false;

                var (json, fileData) = await SendMultipartRequest("chat", "", $"{{\"login\":\"{currentLogin}\",\"secondUserLogin\":\"{secondUser}\"}}");

                var result = JsonConvert.DeserializeObject<dynamic>(json);

                Window?.lvChat?.Items.Clear();

                if ((int)result.messagesNumber == -1)
                {
                    Window?.lvChats?.Items.Remove(secondUser);
                    Window.labelChatName.Content = "Seji";
                    MessageBox.Show("Такого користувача не існує!");
                    secondUserLogin = null;
                    Window.lvChats.IsEnabled = true;
                    return;
                }

                string avatarsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                if (!Directory.Exists(avatarsFolderPath))
                {
                    Directory.CreateDirectory(avatarsFolderPath);
                }
                else
                {
                    var avatarFiles = Directory.GetFiles(avatarsFolderPath, $"{secondUser}.*");

                    if (avatarFiles.Length > 0)
                    {
                        secondUserAvatarPath = avatarFiles[0];
                    }
                    else
                    {
                        (json, fileData) = await SendMultipartRequest("avatar", "", $"{{\"secondUserLogin\":\"{secondUser}\"}}");
                        dynamic avatarInfo = JsonConvert.DeserializeObject<dynamic>(json);

                        File.WriteAllBytes(avatarsFolderPath + "\\" + secondUser + (string)avatarInfo.extension, fileData);

                        secondUserAvatarPath = avatarsFolderPath + "\\" + secondUser + (string)avatarInfo.extension;
                    }
                }

                secondUserLogin = secondUser;

                for(int i = 0; i < (int)result.messagesNumber; i++)
                {
                    (json, fileData) = await SendMultipartRequest("getMessage", "", $"{{\"index\":\"{i}\",\"login\":\"{currentLogin}\",\"secondUserName\":\"{secondUser}\"}}");
                    dynamic messageInfo = JsonConvert.DeserializeObject<dynamic>(json);

                    WorkWithMessage((string)messageInfo.login, (string)messageInfo.extension, ((string)messageInfo.color)[1..], fileData);
                }

                Window.lvChats.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public static async Task SendMessage(string message, bool isFile)
        {
            string extension = ".message";
            string path = " ";
            if (isFile)
            {
                extension = Path.GetExtension(message);
                path = message;
                message = " ";
            }

            await SendMultipartRequest("recieveMessage", path, $"{{\"extension\":\"{extension}\",\"message\":\"{message}\",\"login\":\"{currentLogin}\",\"secondUserLogin\":\"{secondUserLogin}\"}}");

            Window?.tbMessage.Clear();
            Window.tBlockText.Visibility = Visibility.Visible;

            byte[] bMessage;
            if (isFile)
            {
                bMessage = File.ReadAllBytes(path);
            }
            else
            {
                bMessage = Encoding.UTF8.GetBytes(message);
            }
            WorkWithMessage(currentLogin, extension, "000000", bMessage);
        }

        public static async Task StartReceivingMessages()
        {
            while (true)
            {
                try
                {
                    (string? json, byte[]? fileData) = await SendMultipartRequest("messageStream", "", $"{{\"login\":\"{currentLogin}\"}}");

                    if (!string.IsNullOrEmpty(json))
                    {
                        dynamic messageInfo = JsonConvert.DeserializeObject<dynamic>(json);
                        string extension = (string)messageInfo.extension;

                        if (extension != "empty")
                        {
                            WorkWithMessage((string)messageInfo.login, extension, ((string)messageInfo.color)[1..], fileData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Можливо, сервер не працює зараз: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        public static async Task<(string? json, byte[]? fileData)> SendMultipartRequest(string requestType, string filePath, string jsonString)
        {
            using var client = new HttpClient();
            using var form = new MultipartFormDataContent();

            if (File.Exists(filePath))
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", Path.GetFileName(filePath));
            }

            var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
            form.Add(jsonContent, "json");

            HttpResponseMessage response = await client.PostAsync(url + requestType, form);
            if (!response.IsSuccessStatusCode)
            {
                return (null, null);
            }

            var responseData = await response.Content.ReadAsByteArrayAsync();

            var contentType = response.Content.Headers.ContentType?.ToString();
            if (contentType == null) return (null, null);

            var match = Regex.Match(contentType, @"boundary=(?:""([^""]+)""|([^;]+))");
            if (!match.Success) return (null, null);
            var boundary = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            var boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary);
            var separator = Encoding.ASCII.GetBytes("\r\n\r\n");

            int pos = 0;
            string? json = null;
            byte[]? fileData = null;

            while (pos < responseData.Length)
            {
                int partStart = FindSequence(responseData, boundaryBytes, pos);
                if (partStart == -1) break;
                partStart += boundaryBytes.Length + 2;

                int nextPartStart = FindSequence(responseData, boundaryBytes, partStart);
                int partEnd = nextPartStart == -1 ? responseData.Length : nextPartStart - 2;

                int headersEnd = FindSequence(responseData, separator, partStart);
                if (headersEnd == -1) break;

                var headers = Encoding.UTF8.GetString(responseData, partStart, headersEnd - partStart);
                int contentStart = headersEnd + separator.Length;
                int contentLength = partEnd - contentStart;

                var contentBytes = new byte[contentLength];
                Array.Copy(responseData, contentStart, contentBytes, 0, contentLength);

                if (headers.Contains("application/json"))
                {
                    json = Encoding.UTF8.GetString(contentBytes);
                }
                else if (headers.Contains("filename="))
                {
                    fileData = contentBytes;
                }

                pos = partEnd;
            }

            return (json, fileData);
        }


        static int FindSequence(byte[] source, byte[] sequence, int start)
        {
            for (int i = start; i <= source.Length - sequence.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (source[i + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        static void WorkWithMessage(string login, string extension, string color, byte[] bMessage)
        {
            if (login != secondUserLogin && login != currentLogin)
            {
                return;
            }

            byte alpha = 255;
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            if (login != currentLogin)
            {
                alpha = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                red = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                green = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                blue = byte.Parse(color.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }

            string filesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");
            if (!Directory.Exists(filesFolderPath))
            {
                Directory.CreateDirectory(filesFolderPath);
            }

            ListViewItem item;

            if (login != currentLogin)
            {
                Window?.Dispatcher.Invoke(() =>
                {
                    item = new ListViewItem();

                    StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };

                    Image img = new Image();
                    img.Source = new BitmapImage(new Uri(secondUserAvatarPath, UriKind.Absolute));
                    img.Width = 20;

                    TextBlock textBlock = new TextBlock { Text = $"{login}: ", Margin = new Thickness(5, 0, 0, 0), Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, red, green, blue)) };

                    panel.Children.Add(img);
                    panel.Children.Add(textBlock);

                    item.Content = panel;

                    Window?.lvChat?.Items.Add(item);
                });
            }

            if (extension == ".message")
            {
                string message = Encoding.UTF8.GetString(bMessage);

                Window?.Dispatcher.Invoke(() =>
                {
                    item = new ListViewItem();

                    item.Content = message;
                    item.Foreground = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));

                    if (login == currentLogin)
                    {
                        item.HorizontalAlignment = HorizontalAlignment.Right;
                    }

                    Window?.lvChat?.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                });
            }
            else
            {
                string[] files = Directory.GetFiles(filesFolderPath);
                string filePath = Path.Combine(filesFolderPath, $"file({files.Length}){extension}");
                File.WriteAllBytes(filePath, bMessage);

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
                            Window.lvChat.ScrollIntoView(Window.lvChat.Items[Window.lvChat.Items.Count - 1]);
                        };

                        if (login == currentLogin)
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Left;
                        }

                        Window?.lvChat?.Items.Add(item);
                        Window?.lvChat?.ScrollIntoView(item);
                        ShowFolder(item, filePath);
                    });
                }
                else if (extension == ".mp4" || extension == ".gif" || extension == ".mp3")
                {
                    Window?.Dispatcher.Invoke(() =>
                    {
                        item = new ListViewItem();

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

                            item.Content = panel;
                        }
                        else
                        {
                            item.Content = media;
                            item.SizeChanged += (sender, args) =>
                            {
                                Window.lvChat.ScrollIntoView(Window.lvChat.Items[Window.lvChat.Items.Count - 1]);
                            };
                        }

                        WorkWithVideo(item, media, extension, panel);

                        if (login == currentLogin)
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Left;
                        }

                        Window?.lvChat?.Items.Add(item);
                        Window?.lvChat?.ScrollIntoView(item);
                        ShowFolder(item, filePath);
                    });
                }
                else
                {
                    Window?.Dispatcher.Invoke(() =>
                    {
                        item = new ListViewItem();

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
                        item.Content = panel;

                        OpenFile(item, filePath);

                        if (login == currentLogin)
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else
                        {
                            item.HorizontalAlignment = HorizontalAlignment.Left;
                        }

                        Window?.lvChat?.Items.Add(item);
                        Window?.lvChat?.ScrollIntoView(item);
                        ShowFolder(item, filePath);
                    });
                }
            }
        }

        static void ShowFolder(ListViewItem item, string filePath)
        {
            item.PreviewMouseRightButtonDown += (s, e) =>
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            };
        }

        static void OpenFile(ListViewItem item, string filePath)
        {
            item.PreviewMouseLeftButtonDown += (s, e) =>
            {
                string file = Path.GetFileName(filePath);
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            };
        }

        static void WorkWithVideo(ListViewItem item, MediaElement media, string extension, StackPanel? panel)
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