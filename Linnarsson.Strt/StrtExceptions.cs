using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Strt
{
    public class NoAnnotationsFileFoundException : ApplicationException
    {
        public NoAnnotationsFileFoundException(string msg)
            : base(msg)
        { }
    }

    public class NoMapFilesFoundException : ApplicationException
    {
        public NoMapFilesFoundException(string msg)
            : base(msg)
        { }
    }

    public class ChromosomeMissingException : ApplicationException
    {
        public ChromosomeMissingException(string msg)
            : base(msg)
        { }
    }
}
