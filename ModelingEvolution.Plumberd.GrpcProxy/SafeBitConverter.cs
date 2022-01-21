using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public static class SafeBitConverter
    {
#if MODELLUTION
        private static readonly ILogger Log = ModelingEvolution.Plumberd.Logging.LogFactory.GetLogger();
#endif
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
#if MODELLUTION
            Log.LogWarning("Could not convert '{fieldName}' to int64. {investigation}", name, msgInvestigation.ToString());
#endif
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
#if MODELLUTION
            Log.LogWarning("Could not convert '{fieldName}' to int32. {investigation}", name, msgInvestigation.ToString());
#endif
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
#if MODELLUTION
            Log.LogWarning("Could not convert '{fieldName}' to bit. {investigation}", name, msgInvestigation.ToString());
#endif
            return defaultValue;
        }
    }
}