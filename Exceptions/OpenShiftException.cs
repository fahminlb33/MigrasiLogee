using System;

namespace MigrasiLogee.Exceptions
{
    public class OpenShiftException : Exception
    {
        public OpenShiftException()
        {
        }

        public OpenShiftException(string message)
            : base(message)
        {
        }

        public OpenShiftException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
