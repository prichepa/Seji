using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;

namespace SejiClient
{
    public static class ServerClient
    {
        static readonly string url = "http://26.10.226.173:80/";
        static readonly HttpClient client = new HttpClient { BaseAddress = new Uri(url) };
        static readonly HttpListener listener = new HttpListener();
        public static MainWindow? Window { get; set; }

        static string? currentLogin;
        static string? secondUserLogin;
        static string? secondUserAvatarPath;

        public static async Task<bool> Start(string login, string password, string? avatarPath, char entrType)
        {
            Window = Application.Current.MainWindow as MainWindow;
            try
            {
                HttpResponseMessage response;
                if (entrType == 's')
                {
                    response = await SendMultipartRequest("signUp", avatarPath, $"{{\"login\":\"{login}\",\"password\":\"{password}\",\"extension\":\"{Path.GetExtension(avatarPath)}\"}}");
                }
                else
                {
                    response = await SendMultipartRequest("login", "", $"{{\"login\":\"{login}\",\"password\":\"{password}\"}}");
                }

                var json = await response.Content.ReadAsStringAsync();
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

                    //Task.Run(ReciveMessages);

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

        /*static async void ReciveMessages()
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => WorkWithRequest(context));
            }
        }

        static async Task WorkWithRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;

            if (path == "/reciveMessage")
            {
                //await ReciveMessage(context);
            }
            else if (path == "/secondUserAvatar")
            {
                //await SetSecondUserAvatar(context);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
            }
        }
        
        static async Task SetSecondUserAvatar(HttpListenerContext context)
        {
            string? json;
            byte[]? file;

            (json, file) = MultipartRequest(context).Result;
            var jsonData = JsonConvert.DeserializeObject<dynamic>(json);

            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
            string filePath = Path.Combine(folderPath, $"{secondUserLogin}{jsonData.extension}");

            File.WriteAllBytes(filePath, file);

            secondUserAvatarPath = filePath;
        }

        static async Task ReciveMessage(HttpListenerContext context)
        {
            string? json;
            byte[]? file;

            (json, file) = MultipartRequest(context).Result;
            var jsonData = JsonConvert.DeserializeObject<dynamic>(json);

            byte alpha = 255;
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            if ((string)jsonData.login != currentLogin)
            {
                alpha = byte.Parse((string)jsonData.color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                red = byte.Parse((string)jsonData.color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                green = byte.Parse((string)jsonData.color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                blue = byte.Parse((string)jsonData.color.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }

            ListViewItem item;

            if ((string)jsonData.extension == ".message")
            {
                Window?.Dispatcher.Invoke(() =>
                {
                    item = new ListViewItem();

                    item.Content = (string)jsonData.message;
                    item.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, red, green, blue));

                    if ((string)jsonData.login == currentLogin)
                    {
                        item.HorizontalAlignment = HorizontalAlignment.Right;
                    }

                    Window?.lvChat?.Items.Add(item);
                    Window?.lvChat?.ScrollIntoView(item);
                });
            }
        }*/

        public static async Task<HttpResponseMessage> SendMultipartRequest(string requestType, string filePath, string jsonString)
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

            return response;
        }


        static async Task<(string? json, byte[]? fileData)> MultipartRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var contentType = request.ContentType;

            var match = Regex.Match(contentType, @"boundary=(?:""([^""]+)""|([^;]+))");
            if (!match.Success) return (null, null);
            var boundary = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            using var stream = request.InputStream;
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            var boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary);
            var separator = Encoding.ASCII.GetBytes("\r\n\r\n");

            int pos = 0;
            string? json = null;
            byte[]? fileData = null;

            while (pos < data.Length)
            {
                int partStart = FindSequence(data, boundaryBytes, pos);
                if (partStart == -1) break;
                partStart += boundaryBytes.Length + 2;

                int nextPartStart = FindSequence(data, boundaryBytes, partStart);
                int partEnd = nextPartStart == -1 ? data.Length : nextPartStart - 2;

                int headersEnd = FindSequence(data, separator, partStart);
                if (headersEnd == -1) break;

                var headers = Encoding.UTF8.GetString(data, partStart, headersEnd - partStart);
                int contentStart = headersEnd + separator.Length;
                int contentLength = partEnd - contentStart;

                var contentBytes = new byte[contentLength];
                Array.Copy(data, contentStart, contentBytes, 0, contentLength);

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
        /*
        public static async Task GetChat(string secondUser)
        {
            try
            {
                var requestData = new { index = -1, login = currentLogin, secondUserLogin = secondUser };

                var json = System.Text.Json.JsonSerializer.Serialize(requestData);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("chat", content);

                json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(json);

                Window?.lvChat?.Items.Clear();

                if(result.messagesNumber == -1)
                {
                    Window?.lvChats?.Items.Remove(secondUser);
                    Window.labelChatName.Content = "Seji";
                    MessageBox.Show("Такого користувача не існує!");
                    return;
                }

                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                else
                {
                    string filePath = Path.Combine(folderPath, $"{secondUser}.jpg");
                    if (File.Exists(filePath))
                    {
                        secondUserAvatarPath = filePath;
                    }
                    else
                    {
                        await client.PostAsync("chat/avatar", null);
                    }
                }

                secondUserLogin = secondUser;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /*
        public static async Task SendMessage(string message, string secondUser)
        {
            try
            {
                var requestData = new { sender = currentLogin, receiver = secondUser, message };
                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("sendMessage", content);
                if (response.IsSuccessStatusCode)
                {
                    Window?.lvChat?.Items.Add($"You: {message}");
                }
            }
            catch (Exception ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);
            }
        }

        public static async Task SendFile(string filePath, string secondUser)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                form.Add(new StringContent(currentLogin), "sender");
                form.Add(new StringContent(secondUser), "receiver");
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await client.PostAsync("sendFile", form);
                if (response.IsSuccessStatusCode)
                {
                    Window?.lvChat?.Items.Add($"You sent a file: {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                Window?.lvChat?.Items.Add(ex.Message);
            }
        }
        */
    }
}