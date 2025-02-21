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
    }
}