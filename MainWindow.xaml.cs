using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32; // För att hantera OpenFileDialog

namespace ChatApplication
{
    public partial class MainWindow : Window
    {
        // ObservableCollection bunden till chatmeddelandelistan
        public ObservableCollection<Message> Messages { get; set; } = new ObservableCollection<Message>();

        // ObservableCollection för meddelanden i vald historik-konversation
        public ObservableCollection<Message> SelectedConversationMessages { get; set; } = new ObservableCollection<Message>();

        private ChatClient chatClient;
        private ConversationManager conversationManager;
        private Conversation currentConversation;
        private bool isListening = false; // Spårar om vi lyssnar

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            conversationManager = new ConversationManager();
            LoadHistory();
        }

        private void LoadHistory()
        {
            lstHistory.ItemsSource = conversationManager.Conversations;
        }

        // Toggle: Starta eller stoppa lyssning
        private async void btnStartListening_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                // Stoppa lyssning
                chatClient?.Disconnect();
                isListening = false;
                btnStartListening.Content = "Start Listening";
                ellipseListening.Fill = new SolidColorBrush(Colors.Gray);
                txtConnectionStatus.Text = "Not connected";
                return;
            }

            if (!int.TryParse(txtPort.Text, out int port))
            {
                MessageBox.Show("Ogiltigt portnummer.");
                return;
            }
            if (string.IsNullOrEmpty(txtUserName.Text))
            {
                MessageBox.Show("Ange ditt namn.");
                return;
            }

            // Uppdatera UI direkt
            isListening = true;
            btnStartListening.Content = "Stop Listening";
            ellipseListening.Fill = new SolidColorBrush(Colors.Red);
            txtConnectionStatus.Text = "Waiting for connection...";

            chatClient = new ChatClient { UserName = txtUserName.Text };
            chatClient.OnInvitationReceived += ChatClient_OnInvitationReceived;
            chatClient.OnMessageReceived += ChatClient_OnMessageReceived;
            chatClient.OnError += ChatClient_OnError;
            chatClient.OnDisconnected += ChatClient_OnDisconnected;

            // Starta lyssning asynkront (fire-and-forget)
            _ = chatClient.StartListeningAsync(port);
        }

        private async void btnSendInvitation_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPort.Text, out int port))
            {
                MessageBox.Show("Ogiltigt portnummer.");
                return;
            }
            if (string.IsNullOrEmpty(txtUserName.Text))
            {
                MessageBox.Show("Ange ditt namn.");
                return;
            }
            if (string.IsNullOrEmpty(txtIP.Text))
            {
                MessageBox.Show("Ange en IP-adress.");
                return;
            }
            chatClient = new ChatClient { UserName = txtUserName.Text };
            chatClient.OnInvitationResponse += ChatClient_OnInvitationResponse;
            chatClient.OnMessageReceived += ChatClient_OnMessageReceived;
            chatClient.OnError += ChatClient_OnError;
            chatClient.OnDisconnected += ChatClient_OnDisconnected;

            await chatClient.ConnectAsync(txtIP.Text, port);
        }

        private void ChatClient_OnInvitationReceived(string inviterName)
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show($"Chattinbjudan från {inviterName}. Acceptera?", "Inbjudan", MessageBoxButton.YesNo);
                bool accept = result == MessageBoxResult.Yes;
                if (accept)
                {
                    currentConversation = new Conversation
                    {
                        StartTime = DateTime.Now,
                        Participant = inviterName,
                        Messages = new System.Collections.Generic.List<Message>()
                    };
                    txtConnectionStatus.Text = $"Connected to: {inviterName}";
                }
                chatClient.SendInvitationResponseAsync(accept);
            });
        }

        private void ChatClient_OnInvitationResponse(string response)
        {
            Dispatcher.Invoke(() =>
            {
                if (response.StartsWith("ACCEPT"))
                {
                    string partnerName = response.Split('|')[1];
                    MessageBox.Show("Inbjudan accepterad. Chatt startad.");
                    currentConversation = new Conversation
                    {
                        StartTime = DateTime.Now,
                        Participant = partnerName,
                        Messages = new System.Collections.Generic.List<Message>()
                    };
                    txtConnectionStatus.Text = $"Connected to: {partnerName}";
                }
                else
                {
                    MessageBox.Show("Inbjudan nekad.");
                    txtConnectionStatus.Text = "Not connected";
                }
            });
        }

        private void ChatClient_OnMessageReceived(Message message)
        {
            Dispatcher.Invoke(() =>
            {
                Messages.Add(message);
                currentConversation?.Messages.Add(message);
                if (message.Type == MessageType.Buzz)
                {
                    ShakeWindow();
                }
                // Vid bildmeddelanden visas dem nu via DataTemplate i ListBox.
            });
        }

        private void ChatClient_OnError(string error)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Fel: " + error);
            });
        }

        private void ChatClient_OnDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Anslutningen avbröts.");
                if (currentConversation != null)
                {
                    conversationManager.AddConversation(currentConversation);
                    currentConversation = null;
                    Messages.Clear();
                    LoadHistory();
                }
                txtConnectionStatus.Text = "Not connected";
                // Återställ lyssningstillståndet om nödvändigt.
                isListening = false;
                btnStartListening.Content = "Start Listening";
                ellipseListening.Fill = new SolidColorBrush(Colors.Gray);
            });
        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (chatClient == null || !chatClient.IsConnected)
            {
                MessageBox.Show("Ej ansluten.");
                return;
            }
            if (string.IsNullOrEmpty(txtMessage.Text))
                return;

            var msg = new Message
            {
                Sender = txtUserName.Text,
                Content = txtMessage.Text,
                Timestamp = DateTime.Now,
                Type = MessageType.Text
            };
            Messages.Add(msg);
            currentConversation?.Messages.Add(msg);
            await chatClient.SendChatMessageAsync(msg);
            txtMessage.Clear();
        }

        private async void btnBuzz_Click(object sender, RoutedEventArgs e)
        {
            if (chatClient == null || !chatClient.IsConnected)
            {
                MessageBox.Show("Ej ansluten.");
                return;
            }
            var msg = new Message
            {
                Sender = txtUserName.Text,
                Content = "Buzz",
                Timestamp = DateTime.Now,
                Type = MessageType.Buzz
            };
            Messages.Add(msg);
            currentConversation?.Messages.Add(msg);
            await chatClient.SendChatMessageAsync(msg);
        }

        private async void btnSendImage_Click(object sender, RoutedEventArgs e)
        {
            if (chatClient == null || !chatClient.IsConnected)
            {
                MessageBox.Show("Ej ansluten.");
                return;
            }
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Bildfiler|*.jpg;*.png;*.bmp"
            };
            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(filePath);
                    // Se till att vi inte lägger in newline-tecken i Base64-strängen
                    string base64Image = Convert.ToBase64String(imageBytes, Base64FormattingOptions.None);
                    var msg = new Message
                    {
                        Sender = txtUserName.Text,
                        Content = base64Image,
                        Timestamp = DateTime.Now,
                        Type = MessageType.Image
                    };
                    Messages.Add(msg);
                    currentConversation?.Messages.Add(msg);
                    await chatClient.SendChatMessageAsync(msg);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fel vid sändning av bild: " + ex.Message);
                }
            }
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = txtSearch.Text;
            var results = conversationManager.Search(query);
            lstHistory.ItemsSource = results;
        }

        // Uppdaterad: Visa konversationsdetaljer i en dedikerad lista med stöd för bilder
        private void lstHistory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SelectedConversationMessages.Clear();
            if (lstHistory.SelectedItem is Conversation conv)
            {
                foreach (var msg in conv.Messages)
                {
                    SelectedConversationMessages.Add(msg);
                }
            }
        }

        private async void ShakeWindow()
        {
            var originalLeft = this.Left;
            int shakeAmplitude = 10;
            for (int i = 0; i < 10; i++)
            {
                this.Left = originalLeft + ((i % 2 == 0) ? shakeAmplitude : -shakeAmplitude);
                await Task.Delay(50);
            }
            this.Left = originalLeft;
        }
    }
}
