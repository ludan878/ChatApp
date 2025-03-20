using System;

namespace ChatApplication
{
    public enum MessageType
    {
        Text,
        Buzz,
        Image,
        System
    }

    public class Message
    {
        public string Sender { get; set; }
        public string Content { get; set; }
        public MessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
