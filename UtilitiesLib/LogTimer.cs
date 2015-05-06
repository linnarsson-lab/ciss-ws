using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Utilities
{
    public class LogTimer
    {
        private DateTime t;

        public LogTimer()
        {
            t = DateTime.Now;
        }

        public int Seconds()
        {
            int s = (int)Math.Round(DateTime.Now.Subtract(t).TotalSeconds);
            t = DateTime.Now;
            return s;
        }
    }
}
