using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Lin
{
    public delegate void LogHandler(LogData aData);

    public static class Log
    {
        static public void i(string format, params object[] args)
        {
            if (LogEvent != null)
                LogEvent(new LogData(DateTime.Now, false, string.Format(format, args)));
        }

        static public void e(string format, params object[] args)
        {
            if (LogEvent != null)
                LogEvent( new LogData(DateTime.Now, true, string.Format(format, args)));
        }

        static public event LogHandler LogEvent;
    }

    public class LogData
    {
        public LogData(DateTime aTime, bool aIsError, string aMsg)
        {
            this.Time = aTime;
            this.IsError = aIsError;
            this.Message = aMsg;
        }

        public DateTime Time { get; private set; }
        public bool IsError { get; private set; }
        public string  Message { get; private set; }

        public override string ToString()
        {
            return string.Format("{0:HHmmss'FF} {1) - {2}", Time, IsError, Message);
        }
    }
}

