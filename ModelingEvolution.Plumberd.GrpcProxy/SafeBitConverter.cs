using System;
using System.Text;
using Serilog;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public static class SafeBitConverter
    {
        private static readonly ILogger Log = Serilog.Log.ForContext(typeof(SafeBitConverter));
        public static long SafeToInt64(this byte[] data, long defaultValue, string name)
        {
            if (data != null && data.Length == 8) return BitConverter.ToInt64(data);
            
            StringBuilder msgInvestigation = new StringBuilder();
            if (data == null)
                msgInvestigation.Append("Data is null.");
            else
            {
                msgInvestigation.Append($"Data lenght is {data.Length}.");
                if (data.Length < 8)
                {
                    msgInvestigation.Append(BitConverter.ToString(data));
                    msgInvestigation.AppendLine();
                }
            }
            Log.Warning("Could not convert '{fieldName}' to int64. {investigation}", name, msgInvestigation.ToString());
            return defaultValue;
        }
        public static int SafeToInt32(this byte[] data, int defaultValue, string name)
        {
            if (data != null && data.Length == 4) return BitConverter.ToInt32(data);
            
            StringBuilder msgInvestigation = new StringBuilder();
            if (data == null)
                msgInvestigation.Append("Data is null.");
            else
            {
                msgInvestigation.Append($"Data lenght is {data.Length}.");
                if (data.Length < 4)
                {
                    msgInvestigation.Append(BitConverter.ToString(data));
                    msgInvestigation.AppendLine();
                }
            }
            Log.Warning("Could not convert '{fieldName}' to int32. {investigation}", name, msgInvestigation.ToString());
            return defaultValue;
        }
        public static bool SafeToBoolean(this byte[] data, bool defaultValue, string name)
        {
            if (data != null && data.Length == 1) return BitConverter.ToBoolean(data);
            
            StringBuilder msgInvestigation = new StringBuilder();
            if (data == null)
                msgInvestigation.Append("Data is null.");
            else
            {
                msgInvestigation.Append($"Data lenght is {data.Length}.");
                if (data.Length < 4)
                {
                    msgInvestigation.Append(BitConverter.ToString(data));
                    msgInvestigation.AppendLine();
                }
            }
            Log.Warning("Could not convert '{fieldName}' to bit. {investigation}", name, msgInvestigation.ToString());
            return defaultValue;
        }
    }
}