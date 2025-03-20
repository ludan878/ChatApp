using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json; // Observera att du behöver lägga till NuGet-paketet Newtonsoft.Json

namespace ChatApplication
{
    public class ConversationManager
    {
        private const string FileName = "chat_history.json";
        public List<Conversation> Conversations { get; set; } = new List<Conversation>();

        public ConversationManager()
        {
            Load();
        }

        public void AddConversation(Conversation conv)
        {
            Conversations.Add(conv);
            Save();
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Conversations, Formatting.Indented);
                File.WriteAllText(FileName, json);
            }
            catch (Exception ex)
            {
                throw new ChatException("Fel vid sparning av konversationshistorik.", ex);
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(FileName))
                {
                    string json = File.ReadAllText(FileName);
                    Conversations = JsonConvert.DeserializeObject<List<Conversation>>(json) ?? new List<Conversation>();
                }
            }
            catch (Exception ex)
            {
                throw new ChatException("Fel vid inläsning av konversationshistorik.", ex);
            }
        }

        public List<Conversation> Search(string query)
        {
            return Conversations
                .Where(c => c.Participant.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(c => c.StartTime)
                .ToList();
        }
    }
}
