using System;
using System.Globalization;
using System.Xml.Linq;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public static class SvgSizeParser
    {
        private static CultureInfo EN_US = CultureInfo.GetCultureInfo("en-US");
        public static (double,double) Load(string file)
        {
            XDocument doc = XDocument.Load(file);
            var root = doc.Root;
            var wAttr = root.Attribute("width");
            var hAttr = root.Attribute("height");
            
            double w=0, h = 0;
            if (wAttr != null) w = double.Parse(wAttr.Value.Replace("px", ""),EN_US);
            if (hAttr != null) h = double.Parse(hAttr.Value.Replace("px", ""),EN_US);
            if (w > 0 && h > 0) 
                return (w, h); 
            
            // fallback.
            var viewBoxAttr = root.Attribute("viewBox");
            string[] values = viewBoxAttr.Value.Split(new char[]{' ',','}, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 4)
            {
                double viewBoxWidth = double.Parse(values[2], EN_US);
                double viewBoxHeight = double.Parse(values[3], EN_US);

                if (w == 0) w = viewBoxWidth;
                if (h == 0) h = viewBoxHeight;
            }
            return (w, h);
        }
    }
}