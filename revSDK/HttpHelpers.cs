using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;


namespace revCore.Utilities
{
    public static class HttpHelpers
    {
        public static async Task<string> ensureResponseSuccess(HttpResponseMessage response)
        {
            var resStr = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return resStr;

            var errMessage = $"Server error code:{ response.StatusCode} ";

            try
            {
                var e1 = String.IsNullOrWhiteSpace(resStr) ? null : JsonConvert.DeserializeAnonymousType(resStr, new { message = "" });

                errMessage += (null == e1 || String.IsNullOrWhiteSpace(e1.message) ? resStr ?? String.Empty : e1.message);
            }
            catch { }

            //throw new LogAndThrow(errMessage);
            throw new Exception(errMessage);
        }

        public static async Task<dynamic> handleHttpResponse(HttpResponseMessage response)
        {
            var resStr = await ensureResponseSuccess(response);

            return JsonConvert.DeserializeObject<dynamic>(resStr);

        }

        public static async Task<T> handleHttpResponse<T>(T proto, HttpResponseMessage response) where T : class
        {
            var resStr = await ensureResponseSuccess(response);

            if (String.IsNullOrWhiteSpace(resStr))
                return default(T);

            if (proto is String)
                return resStr as T;

            return JsonConvert.DeserializeObject<T>(resStr);

        }
    }

    public class RetryException : Exception
    {
        public RetryException(string message, Exception inner) : base(message, inner) { }
    }
}
