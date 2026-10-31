using System;

namespace Recora.Core
{
    /// <summary>
    /// A failure the user should hear about. <see cref="Code"/> is a short stable key that the app maps to a
    /// Vietnamese sentence; the message is for logs only.
    /// </summary>
    public sealed class RecoraException : Exception
    {
        public RecoraException(string code, string message, Exception? inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
