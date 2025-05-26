using System.Text;
using System.Windows.Media;

namespace SejiServer
{
    public static class WorkWithDB
    {
        static MyDBContext db = new MyDBContext(@"Server=localhost\SQLEXPRESS;
                                  Database=SejiDB;
                                  Trusted_Connection=True;
                                  Encrypt=False;
                                  TrustServerCertificate=True");

        public static bool AddUser(string login, string password, byte[] avatar, string avatarExtension, SolidColorBrush color)
        {
            if(avatarExtension != ".jpg" && avatarExtension != ".png" && avatarExtension != ".gif" && avatarExtension != ".jpeg")
            {
                return false;
            }

            User user = new User() { Login = login, Password = password, Avatar = avatar, avatarExtension = avatarExtension, ColorBrush = color.ToString() };

            try
            {
                db.Users.Add(user);
                db.SaveChanges();

                return true;
            }
            catch
            {
                db.Users.Remove(user);
                return false;
            }
        }

        public static bool ConnectUser(string login, string password)
        {
            try
            {
                User? currentUser = db.Users.FirstOrDefault(u => u.Login == login && u.Password == password);

                if(currentUser != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void AddMessage(byte[] extension, byte[] message, int userId, string secondUserName)
        {
            var secondUser = db.Users.FirstOrDefault(u => u.Login == secondUserName);

            var chat = db.Chats
                .FirstOrDefault(c => db.ChatUsers.Any(cu => cu.ChatId == c.Id && cu.UserId == userId) &&
                db.ChatUsers.Any(cu => cu.ChatId == c.Id && cu.UserId == secondUser.Id));

            if (chat == null)
            {
                chat = new Chat() { IsGroup = false };
                db.Chats.Add(chat);
                db.SaveChanges();

                db.ChatUsers.Add(new ChatUser { ChatId = chat.Id, UserId = userId });
                db.ChatUsers.Add(new ChatUser { ChatId = chat.Id, UserId = secondUser.Id });
                db.SaveChanges();
            }

            Message newMessage = new Message() {
                Extension = extension,
                MessageContent = message,
                UserId = userId,
                ChatId = chat.Id,
                SentTime = DateTime.Now
            };

            db.Messages.Add(newMessage);
            db.SaveChanges();
        }

        public static User GetUserInfo(string login)
        {
            return db.Users.FirstOrDefault(u => u.Login == login);
        }

        public static (byte[], byte[], string) GetMessage(string secondUserName, int skipIndex, int currentUserId)
        {
            var secondUser = db.Users.FirstOrDefault(u => u.Login == secondUserName);

            var chatId = db.ChatUsers
                .Where(cu => cu.UserId == currentUserId)
                .Select(cu => cu.ChatId)
                .Intersect(db.ChatUsers.Where(cu => cu.UserId == secondUser.Id).Select(cu => cu.ChatId))
                .FirstOrDefault();

            var message = db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.SentTime)
            .Skip(skipIndex)
            .Take(1)
            .FirstOrDefault();

            var login = db.Users.FirstOrDefault(u => u.Id == message.UserId).Login;

            return (message.Extension, message.MessageContent, login);
        }

        public static int GetChatsNumber(string login)
        {
            return db.ChatUsers.Count(cu => cu.UserId == GetUserInfo(login).Id);
        }

        public static int GetMessagesNumber(string secondUserName, int currentUserId)
        {
            var secondUser = db.Users.FirstOrDefault(u => u.Login == secondUserName);

            if (secondUser == null)
            {
                return -1;
            }

            var chatId = db.ChatUsers
                .Where(cu => cu.UserId == currentUserId)
                .Select(cu => cu.ChatId)
                .Intersect(db.ChatUsers.Where(cu => cu.UserId == secondUser.Id).Select(cu => cu.ChatId))
                .FirstOrDefault();

            var count = db.Messages
            .Where(m => m.ChatId == chatId)
            .Count();

            return count;
        }

        public static string GetSecondUserName(int chatsCountIndex, int userId)
        {
            var userChats = db.ChatUsers
                .Where(cu => cu.UserId == userId)
                .Select(cu => cu.ChatId)
                .ToList();

            var targetChatId = userChats[chatsCountIndex];

            var secondUserLogin = db.ChatUsers
                .Where(cu => cu.ChatId == targetChatId && cu.UserId != userId)
                .Join(db.Users, cu => cu.UserId, u => u.Id, (cu, u) => u.Login)
                .FirstOrDefault();

            return secondUserLogin;
        }
    }
}