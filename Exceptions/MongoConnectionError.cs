using System;

namespace MigrasiLogee.Exceptions
{
    public class MongoConnectionError : Exception
    {
        public MongoConnectionError()
        {
        }

        public MongoConnectionError(string message)
            : base(message)
        {
        }

        public MongoConnectionError(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
