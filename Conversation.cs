using System;
using System.Collections.Generic;

namespace ChatApplication
{
    public class Conversation
    {
        public DateTime StartTime { get; set; }
        public string Participant { get; set; }
        public List<Message> Messages { get; set; }

        // Används vid visning i historik
        public string DisplayText => $"Konversation med {Participant} - {StartTime:g}";
    }
}
