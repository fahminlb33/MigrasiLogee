using System;

namespace MigrasiLogee.Exceptions
{
    public class KubectlException : Exception
    {
        public KubectlException()
        {
        }

        public KubectlException(string message)
            : base(message)
        {
        }

        public KubectlException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
