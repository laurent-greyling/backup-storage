using System;
using System.Threading;
using System.Web;

namespace Final.BackupTool.Common.Helpers
{
    public class CookiesReadWrite
    {
        public static void Write(string cookie, string cookieKey, string cookieValue)
        {
            var setCookie =
                new HttpCookie(cookie)
                {
                    [cookieKey] = cookieValue
                };
            HttpContext.Current.Response.Cookies.Add(setCookie);
            WaitForCookieSet(cookie, cookieKey, cookieValue);
        }

        public static string Read(string cookie, string cookieKey)
        {
            if (HttpContext.Current.Request.Cookies[cookie] == null) return string.Empty;
            var cookieValue = HttpContext.Current.Request.Cookies[cookie][cookieKey] ?? string.Empty;
            return cookieValue;
        }

        private static void WaitForCookieSet(string cookie, string cookieKey, string cookieValue)
        {
            var numberOfRetries = 10;
            while (string.IsNullOrEmpty(Read(cookie, cookieKey)))
            {
                try
                {
                    Write(cookie, cookieKey, cookieValue);
                    break;
                }
                catch
                {
                    if (--numberOfRetries == 0)
                        throw;
                    Thread.Sleep(1000);
                }
            }
        }
    }
}