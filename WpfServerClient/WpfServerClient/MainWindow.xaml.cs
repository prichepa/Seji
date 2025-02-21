using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfServerClient
{
    public partial class MainWindow : Window
    {
        byte[]? file = null;
        string? extension = null;
        string? filePath = null;

        public MainWindow()
        {
            InitializeComponent();

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri("background.jpg", UriKind.RelativeOrAbsolute);
            bi.EndInit();

            bgImage.Source = bi;
        }

        private void Login_KeyDown(object sender, KeyEventArgs e)
        {
            loginTBlock.Visibility = Visibility.Hidden;
        }

        private void Password_KeyDown(object sender, KeyEventArgs e)
        {
            passwordTBlock.Visibility = Visibility.Hidden;
        }

        private void Message_KeyDown(object sender, KeyEventArgs e)
        {
            if (file != null && e.Key == Key.Enter)
            {
                ServerClient.SendMessage(file, extension, filePath);
            }
            else if (tbMessage.Text != "" && e.Key == Key.Enter)
            {
                byte[]? bMessage = Encoding.UTF8.GetBytes(tbMessage.Text);

                ServerClient.SendMessage(bMessage, ".message", filePath);
            }
            else
            {
                tBlockText.Visibility = Visibility.Hidden;
            }

            file = null;
            extension = null;
        }

        private void signUpBtn_Click(object sender, RoutedEventArgs e)
        {
            string login = loginTBox.Text;
            string password = passwordTBox.Text;

            if (login != "" && password != "" && file != null)
            {
                try
                {
                    if (ServerClient.Start(login, password, file, 's') && (extension == ".jpg" || extension == ".png"))
                    {
                        MessageBox.Show("Акаунт створено!");
                        logInSignUpGrid.Visibility = Visibility.Hidden;

                        file = null;
                        extension = null;
                    }
                    else
                    {
                        MessageBox.Show("Таке ім'я вже існує,\nабо формат аватару не вірний!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBox.Show("Заповніть усі поля!");
            }
        }

        private void logInBtn_Click(object sender, RoutedEventArgs e)
        {
            string login = loginTBox.Text;
            string password = passwordTBox.Text;

            if (login != "" && password != "")
            {
                try
                {
                    if (ServerClient.Start(login, password, null, 'l'))
                    {
                        MessageBox.Show("Ви успішно увійшли!");
                        logInSignUpGrid.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        MessageBox.Show("Помилка!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBox.Show("Заповніть усі поля!");
            }
        }

        private void fileBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { };
            
            if (openFileDialog.ShowDialog() == true)
            {
                filePath = openFileDialog.FileName;

                extension = System.IO.Path.GetExtension(filePath);
                file = System.IO.File.ReadAllBytes(filePath);
            }
        }
    }
}