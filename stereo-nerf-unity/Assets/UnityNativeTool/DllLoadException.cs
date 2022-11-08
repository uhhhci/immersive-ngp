using System;
using System.Runtime.Serialization;

namespace UnityNativeTool
{
    public class NativeDllException : Exception
    {
        public NativeDllException()
        {
        }

        public NativeDllException(string message) : base(message)
        {
        }

        public NativeDllException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NativeDllException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
