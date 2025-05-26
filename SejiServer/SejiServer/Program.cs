using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Windows.Media;
using System.Text.RegularExpressions;
using Azure.Messaging;
using System.Reflection.PortableExecutable;

namespace SejiServer
{
    internal class Program
    {
        public static class Server
        {
            static readonly HttpListener listener = new HttpListener();

            public static void Start()
            {
                listener.Prefixes.Add("http://*:80/");
                listener.Start();

                Console.WriteLine("Server online");

                Task.Run(WaitForClients);
            }

            public static async void WaitForClients()
            {
                while (true)
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => WorkWithRequest(context));
                }
            }

            private static async Task WorkWithRequest(HttpListenerContext context)
            {
                string path = context.Request.Url.AbsolutePath;

                if (path == "/login")
                {
                    await LoginRequest(context);
                }
                else if (path == "/signUp")
                {
                    await SignUpRequest(context);
                }
                else if (path == "/chat")
                {
                    //await GetChatRequest(context);
                }
                else if (path == "/sendMessage")
                {
                    //await SendMessageRequest(context);
                }
                else if (path == "/sendFile")
                {
                    //await SendFileRequest(context);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                }
            }

            static async Task LoginRequest(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);
                dynamic userInfo = JsonConvert.DeserializeObject<dynamic>(json);

                if (WorkWithDB.ConnectUser((string)userInfo.login, (string)userInfo.password))
                {
                    int chatsNumber = WorkWithDB.GetChatsNumber((string)userInfo.login);

                    List<string> secondUsers = new List<string>();
                    for (int i = 0; i < chatsNumber; i++)
                    {
                        string secondUserLogin = WorkWithDB.GetSecondUserName(i, WorkWithDB.GetUserInfo((string)userInfo.login).Id);
                        secondUsers.Add(secondUserLogin);
                    }

                    json = JsonConvert.SerializeObject(new { loginResult = true, chats = secondUsers });
                }
                else
                {
                    json = $"{{\"loginResult\":\"false\"}}";
                }

                context.Response.ContentType = "application/json";
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);

                context.Response.Close();
            }

            static async Task SignUpRequest(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);
                dynamic userInfo = JsonConvert.DeserializeObject<dynamic>(json);

                Random random = new Random();
                var color = new SolidColorBrush(Color.FromRgb(
                    (byte)random.Next(0, 255),
                    (byte)random.Next(0, 255),
                    (byte)random.Next(0, 255)
                ));

                string loginResult;
                if (WorkWithDB.AddUser((string)userInfo.login, (string)userInfo.password, fileData, (string)userInfo.extension, color))
                {
                    loginResult = "true";
                }
                else
                {
                    loginResult = "false";
                }
                json = $"{{\"loginResult\":\"{loginResult}\"}}";

                context.Response.ContentType = "application/json";
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);

                context.Response.Close();
            }

            /*
            static async Task GetChatRequest(HttpListenerContext context)
            {
                var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var json = await reader.ReadToEndAsync();
                var info = JsonConvert.DeserializeObject<dynamic>(json);

                if ((int)info.index > 0)
                {
                    var message = WorkWithDB.GetMessage((string)info.secondUserLogin, (int)info.index, WorkWithDB.GetUserInfo((string)info.login).Id);

                    SendMessage(context, message);
                }
                else
                {
                    var dataToSend = new { messagesNumber = WorkWithDB.GetMessagesNumber((string)info.secondUserLogin, WorkWithDB.GetUserInfo((string)info.login).Id) };
                    json = JsonConvert.SerializeObject(dataToSend);

                    context.Response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                context.Response.Close();
            }
            
            static void SendMessage(HttpListenerContext context, (byte[], byte[], string) message)
            {
                if (Encoding.UTF8.GetString(message.Item1) == ".message")
                {
                    var dataToSend = new { extension = message.Item1, messageContent = message.Item2, login = message.Item3 };
                    var json = JsonConvert.SerializeObject(dataToSend);

                    context.Response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    //file

                    var dataToSend = new { extension = Encoding.UTF8.GetBytes("Test str"), messageContent = Encoding.UTF8.GetBytes("Test str"), login = "test str" };
                    var json = JsonConvert.SerializeObject(dataToSend);

                    context.Response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            */
            static async Task SendFileRequest(HttpListenerContext context, bool isMessage)
            {
                var (json, fileData) = await MultipartRequest(context);

                if (json == null && fileData == null)
                {
                    var errorResponse = new { result = "error", message = "Invalid multipart data" };
                    string errorJson = JsonConvert.SerializeObject(errorResponse);
                    context.Response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(errorJson);
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.Close();
                    return;
                }

                dynamic userInfo = JsonConvert.DeserializeObject<dynamic>(json);

                if (isMessage)
                {
                    string login = userInfo?.login ?? "";
                    string secondUserLogin = userInfo?.secondUserLogin ?? "";
                    string extension = userInfo?.extension ?? "";

                    WorkWithDB.AddMessage(Encoding.UTF8.GetBytes(extension), fileData, WorkWithDB.GetUserInfo(login).Id, secondUserLogin);
                }

                context.Response.Close();
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


            /*static async Task SendMessageRequest(HttpListenerContext context)
            {
                string secondUserLogin = WorkWithDB.GetSecondUserName(i, WorkWithDB.GetUserInfo((string)userInfo).Id);
                json = await reader.ReadToEndAsync();
                var message = JsonConvert.DeserializeObject<dynamic>(json);

                WorkWithDB.AddMessage(message.extension, message.content, WorkWithDB.GetUserInfo((string)userInfo.login).Id, secondUserLogin);
            }*/
        }


        static void Main(string[] args)
        {
            try
            {
                Server.Start();
                while (true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}