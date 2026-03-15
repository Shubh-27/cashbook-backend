using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace backend.common
{
    public static class CommonHelper
    {
        public static string GenerateUniqueSID(string prefix)
        {
            return (prefix + Guid.NewGuid().ToString()).ToUpper();
        }

        public static string DictionaryToXml(Dictionary<string, object> dic, string rootElement = "Root")
        {
            string strXMLResult = string.Empty;

            if (dic != null && dic.Count > 0)
            {
                foreach (KeyValuePair<string, object> pair in dic)
                {
                    strXMLResult += "<" + pair.Key + ">" + pair.Value + "</" + pair.Key + ">";
                }

                strXMLResult = "<" + rootElement + ">" + strXMLResult + "</" + rootElement + ">";
            }

            return strXMLResult;
        }

        public static TTo UpdateModel<TFrom, TTo>(TFrom fromModel, TTo toModel, List<string>? skipFields = null)
        {
            skipFields ??= [];

            if (fromModel == null || toModel == null)
                throw new ArgumentNullException("Models cannot be null");

            PropertyInfo[] fromProperties = typeof(TFrom).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] toProperties = typeof(TTo).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var fromProp in fromProperties)
            {
                var toProp = Array.Find(toProperties, p => p.Name.Equals(fromProp.Name, StringComparison.OrdinalIgnoreCase) && p.CanWrite);
                if (skipFields.Contains(fromProp.Name))
                {
                    continue;
                }
                if (toProp != null)
                {
                    var value = fromProp.GetValue(fromModel);
                    toProp.SetValue(toModel, value);
                }
            }

            return toModel;
        }
    }
}
