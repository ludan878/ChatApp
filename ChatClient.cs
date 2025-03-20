using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatApplication
{
    /// <summary>
    /// Hanterar nätverkskommunikationen med TcpListener/TcpClient enligt vårt enkla protokoll:
    /// INVITE|UserName
    /// ACCEPT|UserName
    /// REJECT|UserName
    /// CHAT|UserName|Meddelande
    /// BUZZ|UserName
    /// IMAGE|UserName|Base64Bild
    /// DISCONNECT|UserName
    /// </summary>
    public class ChatClient
    {
        public TcpClient Client { get; private set; }
        public TcpListener Listener { get; private set; }
        public NetworkStream Stream { get; private set; }
        public bool IsConnected { get; private set; } = false;
        public string UserName { get; set; }
        public event Action<string> OnError;
        public event Action<string> OnInvitationReceived; // t.ex. inparameter: inbjudarens namn
        public event Action<string> OnInvitationResponse; // t.ex. "ACCEPT|UserName" eller "REJECT|UserName"
        public event Action<Message> OnMessageReceived;
        public event Action OnDisconnected;

        private CancellationTokenSource cts;

        public async Task StartListeningAsync(int port)
        {
            try
            {
                Listener = new TcpListener(IPAddress.Any, port);
                Listener.Start();
                // Vänta på en anslutning
                Client = await Listener.AcceptTcpClientAsync();
                Stream = Client.GetStream();
                IsConnected = true;
                // Första meddelandet bör vara en inbjudan
                string invitation = await ReadMessageAsync();
                if (invitation.StartsWith("INVITE|"))
                {
                    string inviterName = invitation.Split('|')[1];
                    OnInvitationReceived?.Invoke(inviterName);
                }
                // Starta bakgrundstråd för att lyssna på vidare meddelanden
                cts = new CancellationTokenSource();
                Task.Run(() => ListenForMessagesAsync(cts.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid lyssning: " + ex.Message);
            }
        }

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                Client = new TcpClient();
                await Client.ConnectAsync(ip, port);
                Stream = Client.GetStream();
                IsConnected = true;
                // Skicka inbjudan med vårt användarnamn
                string inviteMsg = $"INVITE|{UserName}";
                await SendMessageAsync(inviteMsg);
                // Vänta på svar (ACCEPT eller REJECT)
                string response = await ReadMessageAsync();
                OnInvitationResponse?.Invoke(response);
                if (response.StartsWith("ACCEPT"))
                {
                    cts = new CancellationTokenSource();
                    Task.Run(() => ListenForMessagesAsync(cts.Token));
                }
                else
                {
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid anslutning: " + ex.Message);
            }
        }

        public async Task SendInvitationResponseAsync(bool accept)
        {
            try
            {
                string response = accept ? $"ACCEPT|{UserName}" : $"REJECT|{UserName}";
                await SendMessageAsync(response);
                if (!accept)
                {
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid sändning av svar: " + ex.Message);
            }
        }

        public async Task SendChatMessageAsync(Message message)
        {
            try
            {
                string msgType = message.Type switch
                {
                    MessageType.Text => "CHAT",
                    MessageType.Buzz => "BUZZ",
                    MessageType.Image => "IMAGE",
                    _ => "CHAT"
                };
                // Format: TYPE|UserName|Content
                string fullMessage = $"{msgType}|{UserName}|{message.Content}";
                await SendMessageAsync(fullMessage);
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid sändning av chattmeddelande: " + ex.Message);
            }
        }

        private async Task SendMessageAsync(string message)
        {
            try
            {
                if (Stream != null && Stream.CanWrite)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message + "\n");
                    await Stream.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid sändning: " + ex.Message);
            }
        }

        private async Task<string> ReadMessageAsync()
        {
            var sb = new StringBuilder();
            var buffer = new byte[4096];
            while (true)
            {
                int bytesRead = await Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Anslutningen stängdes
                    break;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                // Om vi har mottagit ett newline-tecken så antar vi att meddelandet är komplett.
                if (sb.ToString().Contains("\n"))
                {
                    break;
                }
            }
            // Ta bort newline-tecknet
            string message = sb.ToString();
            int newlineIndex = message.IndexOf("\n");
            if (newlineIndex >= 0)
            {
                message = message.Substring(0, newlineIndex);
            }
            return message.Trim();
        }


        private async Task ListenForMessagesAsync(CancellationToken token)
        {
            try
            {
                while (IsConnected && !token.IsCancellationRequested)
                {
                    string message = await ReadMessageAsync();
                    if (string.IsNullOrEmpty(message))
                    {
                        // Anslutningen stängdes
                        Disconnect();
                        OnDisconnected?.Invoke();
                        break;
                    }
                    // Tolka meddelandet enligt vårt protokoll
                    string[] parts = message.Split('|');
                    if (parts.Length >= 2)
                    {
                        string command = parts[0];
                        string sender = parts[1];
                        if (command == "CHAT" && parts.Length >= 3)
                        {
                            string content = message.Substring(command.Length + sender.Length + 2);
                            OnMessageReceived?.Invoke(new Message
                            {
                                Sender = sender,
                                Content = content,
                                Type = MessageType.Text,
                                Timestamp = DateTime.Now
                            });
                        }
                        else if (command == "BUZZ")
                        {
                            OnMessageReceived?.Invoke(new Message
                            {
                                Sender = sender,
                                Content = "Buzz!",
                                Type = MessageType.Buzz,
                                Timestamp = DateTime.Now
                            });
                        }
                        else if (command == "IMAGE" && parts.Length >= 3)
                        {
                            string content = message.Substring(command.Length + sender.Length + 2);
                            OnMessageReceived?.Invoke(new Message
                            {
                                Sender = sender,
                                Content = content,
                                Type = MessageType.Image,
                                Timestamp = DateTime.Now
                            });
                        }
                        else if (command == "DISCONNECT")
                        {
                            Disconnect();
                            OnDisconnected?.Invoke();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel i lyssningstråden: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            try
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    cts?.Cancel();
                    if (Stream != null)
                        Stream.Close();
                    if (Client != null)
                        Client.Close();
                    Listener?.Stop();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Fel vid frånkoppling: " + ex.Message);
            }
        }
    }
}
