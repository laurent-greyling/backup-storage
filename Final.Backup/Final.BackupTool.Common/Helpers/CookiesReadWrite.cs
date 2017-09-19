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
                    [cookieKey] = HttpUtility.UrlEncode(cookieValue),
                    Expires = DateTime.Now.AddDays(5)
                };
            HttpContext.Current.Response.Cookies.Add(setCookie);
            WaitForCookieSet(cookie, cookieKey);
        }

        public static string Read(string cookie, string cookieKey)
        {
            if (HttpContext.Current.Request.Cookies[cookie] == null) return string.Empty;
            var cookieValue = HttpUtility.UrlDecode(HttpContext.Current.Request.Cookies[cookie][cookieKey]) ?? string.Empty;
            return cookieValue;
        }

        public static void Delete(string cookie)
        {
            var cookieDel = new HttpCookie(cookie) {Expires = DateTime.Now.AddDays(-5)};
            HttpContext.Current.Response.Cookies.Add(cookieDel);
        }

        public static void Update(string cookie, string cookieKey, string cookieValue)
        {
            var setCookie = HttpContext.Current.Request.Cookies[cookie];
            if (setCookie == null) return;
            setCookie.Values[cookieKey] = HttpUtility.UrlEncode(cookieValue);
            setCookie.Expires = DateTime.Now.AddDays(5);
            HttpContext.Current.Response.Cookies.Add(setCookie);
            WaitForCookieSet(cookie, cookieKey);
        }

        private static void WaitForCookieSet(string cookie, string cookieKey)
        {
            var numberOfRetries = 10;
            var readCookie = string.Empty;
            while (string.IsNullOrEmpty(readCookie))
            {
                Thread.Sleep(1000);
                readCookie = Read(cookie, cookieKey);
                if (--numberOfRetries == 0)
                    break;
            }
        }
    }
}