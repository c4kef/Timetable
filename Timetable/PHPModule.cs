using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using System.Globalization;
using System.Net.Cache;
using System.Text.RegularExpressions;
using System.Windows;

namespace Timetable
{
    public class PHPModule
    {
        private List<object> tmpBufferValue;
        private List<string> tmpBufferName;
		private Uri target;
		private CookieContainer cookie;
		public PHPModule(string address)
        {
            tmpBufferValue = new List<object>();
            tmpBufferName = new List<string>();
			target = new Uri(address);
        }
        public void Add(string Name, object Value)
        {
            tmpBufferName.Add(Name);
            tmpBufferValue.Add(Value);
        }
        public void Clear()
        {
            tmpBufferName.Clear();
            tmpBufferValue.Clear();
			cookie = null;
        }
		public void SetCookie(CookieAuth? cookieAuth)
		{
			cookie = new CookieContainer();
			cookie.Add(new Cookie("login", cookieAuth?.login) { Domain = target.Host });
			cookie.Add(new Cookie("pass", cookieAuth?.password) { Domain = target.Host });
			cookie.Add(new Cookie("PHPSESSID", cookieAuth?.phpsessionid) { Domain = target.Host });
		}
		public string Send()
		{
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(target);
			request.Method = "POST";
			string s = "";
			for (int i = 0; i < tmpBufferName.Count; i++)
			{
				object[] textArray1 = new object[] { s, tmpBufferName[i], "=", tmpBufferValue[i], "&" };
				s = string.Concat(textArray1);
			}
			byte[] bytes = Encoding.UTF8.GetBytes(s);

			request.ContentType = "application/x-yametrika+json";
			request.ContentLength = (long) bytes.Length;
			request.CookieContainer = cookie ?? null;
			using (Stream stream = request.GetRequestStream())
				stream.Write(bytes, 0, bytes.Length);
			WebResponse response = request.GetResponse();
			string str2 = "";

			using (Stream stream2 = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream2))
				str2 = reader.ReadToEnd();

			response.Close();
			return str2;
		}
    }
}