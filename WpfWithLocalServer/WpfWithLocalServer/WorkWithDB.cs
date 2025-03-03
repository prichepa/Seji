using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WpfWithLocalServer
{
    public static class WorkWithDB
    {
        public static User? currentUser;

        static MyDBContext db = new MyDBContext(@"Server=localhost\SQLEXPRESS;
                                  Database=SejiDB;
                                  Trusted_Connection=True;
                                  Encrypt=False;
                                  TrustServerCertificate=True");

        public static bool AddUser(string login, string password, byte[] avatar, SolidColorBrush color)
        {
            User user = new User() { Login = login, Password = password, Avatar = avatar, ColorBrush = color.ToString() };
            currentUser = user;

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
                currentUser = db.Users.FirstOrDefault(u => u.Login == login && u.Password == password);

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
            //var chatUser = db.ChatUsers.FirstOrDefault(cu => cu.UserId == currentUser.Id);
            //int chatId = Convert.ToInt32(db.Chats.FirstOrDefault(c => c.Id == chatUser.ChatId));

            //create

            Message newMessage = new Message() {
                Extension = extension,
                MessageContent = message,
                UserId = userId,
                ChatId = 1,
                SentTime = DateTime.Now
            };

            db.Messages.Add(newMessage);
            db.SaveChanges();
        }

        /*public static (byte[], DateTime) GetChatMessage(int chatId, int skipIndex)
        {
            var chatUser = db.ChatUsers.FirstOrDefault(c => c.UserId == currentUser.Id);
            var chat = db.Chats.FirstOrDefault(c => c.Id == chatUser.ChatId);
            var message = db.Messages.FirstOrDefault(m => m.ChatId == chatId);

            return (message.MessageContent, message.SentTime);
        }*/
    }
}