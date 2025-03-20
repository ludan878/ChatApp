using System;

namespace ChatApplication
{
    public class ChatException : Exception
    {
        public ChatException() : base() { }
        public ChatException(string message) : base(message) { }
        public ChatException(string message, Exception inner) : base(message, inner) { }
    }
}
