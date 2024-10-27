using System;

namespace FastBindings.Helpers
{
    internal class ExceptionHolder
    {
        public Exception Exception { get; private set; }
        internal ExceptionHolder(Exception ex) 
        {
            Exception = ex;
        }
    }
}
