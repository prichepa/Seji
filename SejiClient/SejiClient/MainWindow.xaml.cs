using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SejiClient
{
    public partial class MainWindow : Window
    {
        string? filePath = null;
        string? secondUser = null;

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

        private void NewChat_KeyDown(object sender, KeyEventArgs e)
        {
            if (tbChat.Text != "" && e.Key == Key.Enter)
            {
                tBlockTextChat.Visibility = Visibility.Visible;

                if (lvChats.Items.Contains(tbChat.Text))
                {
                    lvChats.SelectedItem = tbChat.Text;
                }
                else
                {
                    lvChats.Items.Add(tbChat.Text);
                    lvChats.SelectedItem = tbChat.Text;
                }
                lvChats.ScrollIntoView(tbChat.Text);

                tbChat.Clear();
            }
            else
            {
                tBlockTextChat.Visibility = Visibility.Hidden;
            }
        }

        private async void LvChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvChats.SelectedItem != null)
            {
                secondUser = lvChats.SelectedItem.ToString();
                labelChatName.Content = secondUser;

                lvChat.Items.Clear();

                await ServerClient.GetChat(secondUser);
            }
        }

        private async void Message_KeyDown(object sender, KeyEventArgs e)
        {
            if (secondUser == null)
            {
                MessageBox.Show("Оберіть співбесідника!");
                tBlockText.Visibility = Visibility.Hidden;
                return;
            }

            if (filePath != null && e.Key == Key.Enter)
            {
                await ServerClient.SendMessage(filePath, true);
            }
            else if (tbMessage.Text != "" && e.Key == Key.Enter)
            {
                await ServerClient.SendMessage(tbMessage.Text, false);
            }
            else
            {
                tBlockText.Visibility = Visibility.Hidden;
            }

            filePath = null;
        }

        private async void signUpBtn_Click(object sender, RoutedEventArgs e)
        {
            string login = loginTBox.Text;
            string password = passwordTBox.Text;

            if (login != "" && password != "" && filePath != null)
            {
                try
                {
                    if (await ServerClient.Start(login, password, filePath, 's'))
                    {
                        MessageBox.Show("Акаунт створено!");
                        logInSignUpGrid.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        MessageBox.Show("Таке ім'я вже існує, або формат аватару не вірний!");
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

            filePath = null;
        }

        private async void logInBtn_Click(object sender, RoutedEventArgs e)
        {
            string login = loginTBox.Text;
            string password = passwordTBox.Text;

            if (login != "" && password != "")
            {
                try
                {
                    if (await ServerClient.Start(login, password, null, 'l'))
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

            filePath = null;
        }

        private void fileBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                filePath = openFileDialog.FileName;
            }
        }
    }
}