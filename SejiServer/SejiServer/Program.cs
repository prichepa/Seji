using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SejiServer
{
    internal class Program
    {
        public static class Server
        {
            static readonly HttpListener listener = new HttpListener();

            static readonly List<HttpListenerContext> waitingClients = new List<HttpListenerContext>();
            static readonly Dictionary<string, HttpListenerContext> userContexts = new Dictionary<string, HttpListenerContext>();
            static readonly object lockObj = new object();

            public static void Start()
            {
                listener.Prefixes.Add("http://*:8080/");
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
                    await GetChatRequest(context);
                }
                else if (path == "/avatar")
                {
                    await GetAvatarRequest(context);
                }
                else if (path == "/messageStream")
                {
                    await SendMessageStream(context);
                }
                else if (path == "/getMessage")
                {
                    await getMessageRequest(context);
                }
                else if (path == "/recieveMessage")
                {
                    await recieveMessage(context);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                }
            }

            static async Task SendMessageStream(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);

                string userLogin = "";
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var userInfo = JsonConvert.DeserializeObject<dynamic>(json);
                        userLogin = userInfo?.login ?? "";
                    }
                    catch { }
                }

                lock (lockObj)
                {
                    if (!string.IsNullOrEmpty(userLogin) && userContexts.ContainsKey(userLogin))
                    {
                        userContexts.Remove(userLogin);
                    }

                    if (!string.IsNullOrEmpty(userLogin))
                    {
                        userContexts[userLogin] = context;
                    }

                    waitingClients.Add(context);
                }

                await Task.Delay(30000);

                lock (lockObj)
                {
                    if (waitingClients.Contains(context))
                    {
                        waitingClients.Remove(context);

                        if (!string.IsNullOrEmpty(userLogin) && userContexts.ContainsKey(userLogin))
                        {
                            userContexts.Remove(userLogin);
                        }

                        SendEmptyMessage(context);
                    }
                }
            }

            static async void SendEmptyMessage(HttpListenerContext context)
            {
                var jsonContent = JsonConvert.SerializeObject(new { extension = "empty" });
                await SendMultipartResponse(context, jsonContent);
            }

            static async Task recieveMessage(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);
                var info = JsonConvert.DeserializeObject<dynamic>(json);

                if((string)info.extension == ".message")
                {
                    byte[] bMessage = Encoding.UTF8.GetBytes((string)info.message);
                    byte[] bExtension = Encoding.UTF8.GetBytes((string)info.extension);
                    WorkWithDB.AddMessage(bExtension, bMessage, WorkWithDB.GetUserInfo((string)info.login).Id, (string)info.secondUserLogin);
                }
                else
                {
                    byte[] bExtension = Encoding.UTF8.GetBytes((string)info.extension);
                    WorkWithDB.AddMessage(bExtension, fileData, WorkWithDB.GetUserInfo((string)info.login).Id, (string)info.secondUserLogin);
                }
                await SendMultipartResponse(context, $"{{\"OK\"}}");

                await NotifyWaitingClients((string)info.login, (string)info.extension,
                    ((string)info.extension == ".message") ? Encoding.UTF8.GetBytes((string)info.message) : fileData,
                    (string)info.secondUserLogin);
            }

            static async Task NotifyWaitingClients(string senderLogin, string extension, byte[] messageContent, string recipientLogin)
            {
                HttpListenerContext recipientContext = null;

                lock (lockObj)
                {
                    if (userContexts.ContainsKey(recipientLogin))
                    {
                        recipientContext = userContexts[recipientLogin];

                        waitingClients.Remove(recipientContext);
                        userContexts.Remove(recipientLogin);
                    }
                }

                if (recipientContext != null)
                {
                    var messageData = new
                    {
                        extension,
                        login = senderLogin,
                        color = WorkWithDB.GetUserInfo(senderLogin).ColorBrush
                    };
                    string jsonContent = JsonConvert.SerializeObject(messageData);

                    try
                    {
                        await SendMultipartResponse(recipientContext, jsonContent, messageContent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while trying to send message to {recipientLogin}: {ex.Message}");
                    }
                }
            }

            static async Task getMessageRequest(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);
                dynamic userInfo = JsonConvert.DeserializeObject<dynamic>(json);

                var (bExtension, messageContent, secondUserLogin) = WorkWithDB.GetMessage((string)userInfo.secondUserName, (int)userInfo.index, WorkWithDB.GetUserInfo((string)userInfo.login).Id);
                json = $"{{\"extension\":\"{Encoding.UTF8.GetString(bExtension)}\",\"login\":\"{secondUserLogin}\",\"color\":\"{WorkWithDB.GetUserInfo(secondUserLogin).ColorBrush}\"}}";

                await SendMultipartResponse(context, json, messageContent);
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

                await SendMultipartResponse(context, json);
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

                await SendMultipartResponse(context, json);
            }

            static async Task GetChatRequest(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);

                var info = JsonConvert.DeserializeObject<dynamic>(json);

                var dataToSend = new { messagesNumber = WorkWithDB.GetMessagesNumber((string)info.secondUserLogin, WorkWithDB.GetUserInfo((string)info.login).Id) };
                json = JsonConvert.SerializeObject(dataToSend);

                await SendMultipartResponse(context, json);
            }

            static async Task GetAvatarRequest(HttpListenerContext context)
            {
                var (json, fileData) = await MultipartRequest(context);
                var info = JsonConvert.DeserializeObject<dynamic>(json);

                await SendMultipartResponse(context, $"{{\"extension\":\"{WorkWithDB.GetUserInfo((string)info.secondUserLogin).avatarExtension}\"}}", WorkWithDB.GetUserInfo((string)info.secondUserLogin).Avatar);
            }

            static async Task SendMessageStream(HttpListenerContext context, string extension, byte[] messageContent, string secondUserLogin)
            {
                var messageData = new { extension, secondUserLogin, color = WorkWithDB.GetUserInfo(secondUserLogin).ColorBrush };
                string jsonContent = JsonConvert.SerializeObject(messageData);

                await SendMultipartResponse(context, jsonContent, messageContent);
            }

            static async Task<(string? json, byte[]? fileData)> MultipartRequest(HttpListenerContext context)
            {
                try
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
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                return (null, null);
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

            static async Task SendMultipartResponse(HttpListenerContext context, string jsonContent, byte[] fileData = null)
            {
                try
                {
                    string fileContentType = "application/octet-stream";

                    string boundary = "------------------------" + DateTime.Now.Ticks.ToString("x");
                    context.Response.ContentType = $"multipart/form-data; boundary={boundary}";

                    using (var outputStream = context.Response.OutputStream)
                    using (var writer = new StreamWriter(outputStream))
                    {
                        writer.WriteLine($"--{boundary}");
                        writer.WriteLine("Content-Disposition: form-data; name=\"json\"");
                        writer.WriteLine("Content-Type: application/json");
                        writer.WriteLine();
                        writer.WriteLine(jsonContent);
                        writer.WriteLine();

                        if (fileData != null && fileData.Length > 0)
                        {
                            writer.WriteLine($"--{boundary}");
                            writer.WriteLine($"Content-Disposition: form-data; name=\"file\"; filename=\"{"file"}\"");
                            writer.WriteLine($"Content-Type: {fileContentType}");
                            writer.WriteLine("Content-Transfer-Encoding: binary");
                            writer.WriteLine();
                            writer.Flush();

                            await outputStream.WriteAsync(fileData, 0, fileData.Length);
                            writer.WriteLine();
                        }

                        writer.WriteLine($"--{boundary}--");
                        await writer.FlushAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                context.Response.Close();
            }
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