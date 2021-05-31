using System;

namespace MigrasiLogee.Exceptions
{
    public class MongoException : Exception
    {
        public MongoException()
        {
        }

        public MongoException(string message)
            : base(message)
        {
        }

        public MongoException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
