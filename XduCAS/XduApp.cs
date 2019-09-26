using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XduCAS
{
    /// <summary>
    /// 提供登录 i 西电通用方法的类。
    /// </summary>
    public class XduApp
    {
        /// <summary>
        /// 获取或设置用于登录的学号。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置用于登录的密码。
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 获取 i 西电所有请求所需的 Uuid。
        /// </summary>
        public string Uuid { get; }

        /// <summary>
        /// 获取已登录用户的 userId。
        /// </summary>
        public int UserId { get; private set; }

        /// <summary>
        /// 获取应用密钥。
        /// </summary>
        public readonly string AppKey = "GiITvn";

        /// <summary>
        /// 获取学校 ID。
        /// </summary>
        public readonly int SchoolId = 190;

        /// <summary>
        /// 获取本次登录会话 Token。
        /// </summary>
        private string _token;

        /// <summary>
        /// 使用指定的登录信息初始化 <see cref="XduApp"/> 类。
        /// </summary>
        /// <param name="id">用于登录的学号。</param>
        /// <param name="password">用于登录的密码。</param>
        public XduApp(string id, string password)
        {
            Id = id;
            Password = password;
            Uuid = GetUuid();
        }

        /// <summary>
        /// <para>使用设置的学号和密码进行 i 西电系统登录。如果提供验证码，则一并使用。</para>
        /// <para>返回包含 Cookies 等信息的 <see cref="T:System.Net.Http.HttpClient"/> 类。</para>
        /// </summary>
        /// <exception cref="FormatException">登录信息格式不正确。</exception>
        /// <exception cref="ArgumentException">密码不能为空。</exception>
        /// <exception cref="Exception">登录失败。</exception>
        /// <returns>包含 Cookies 等信息的 <see cref="T:System.Net.Http.HttpClient"/> 类。</returns>
        public HttpClient Login()
        {
            // Check format of id, password and redirect uri
            if (!long.TryParse(Id, out _) || Id.Length != 11)
                throw new FormatException("学号格式不正确。");
            if (Password == "")
                throw new ArgumentException("密码不能为空。");
            HttpClient hc = new HttpClient();
            // Set HttpClient up
            hc.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Linux; Android 8.0; Pixel 2 Build/OPD3.170816.012) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Mobile Safari/537.36");
            hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            hc.DefaultRequestHeaders.ExpectContinue = false;
            hc.DefaultRequestHeaders.Referrer = new Uri("http://wx.xidian.edu.cn/wx_xdu/");
            hc.DefaultRequestHeaders.Add("Origin", "http://wx.xidian.edu.cn");
            hc.DefaultRequestHeaders.Add("token", "");
            hc.DefaultRequestHeaders.Host = "202.117.121.7:8080";
            hc.DefaultRequestHeaders.Connection.Add("keep-alive");
            if (!hc.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Build login params
            JObject param = new JObject
            {
                {"userName", Id},
                {"password", Password},
                {"uuId", Uuid},
                {"schoolId", SchoolId}
            };
            string loginParams = BuildQuery(param);
            string strReturn =
                hc.PostAsync("http://202.117.121.7:8080/baseCampus/login/login.do",
                        new StringContent(loginParams, Encoding.UTF8, "application/json")).Result.Content
                    .ReadAsStringAsync().Result;
            try
            {
                JObject jRet = (JObject)JsonConvert.DeserializeObject(strReturn);
                // Login successfully
                if (jRet["msg"].ToString() != "登录成功")
                {
                    throw new Exception($"登录失败。\n{jRet["msg"]}");
                    //MessageBox.Show($"{jRet["userBaseInfo"]["realName"]}, {jRet["userLoginInfo"]["pid"]}, {txtID.Text}, {pass}");
                }

                // Id or password is invalid
                if (strReturn.Contains("有误"))
                    throw new Exception("学号或密码不正确。");

                UserId = jRet["userBaseInfo"]["userId"].ToObject<int>();
                _token = jRet["token"][0] + "_" + jRet["token"][1];
                hc.DefaultRequestHeaders.Remove("token");
                hc.DefaultRequestHeaders.TryAddWithoutValidation("token", _token);
                return hc;
            }
            catch (Exception e)
            {
                // Other exception
                throw new Exception($"登录失败。\n{e.Message}");
            }
        }

        /// <summary>
        /// 获取表示当前时间的 Unix 时间戳。
        /// </summary>
        /// <returns>表示 Unix 时间戳的长整型。</returns>
        public long GetTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 生成用于 i 西电的随机 Uuid。
        /// </summary>
        /// <returns>表示 Uuid 的字符串。</returns>
        private string GetUuid()
        {
            string timestamp = GetTimestamp().ToString();
            string part1 =
                (Convert.ToString(
                     long.Parse(new Random().NextDouble().ToString(CultureInfo.InvariantCulture).Substring(2, 8) +
                                timestamp.Substring(timestamp.Length - 10, 10)), 16) + "").Substring(0, 8);
            timestamp = GetTimestamp().ToString();
            string part2 =
                (Convert.ToString(
                     long.Parse(new Random().NextDouble().ToString(CultureInfo.InvariantCulture).Substring(2, 8) +
                                timestamp.Substring(timestamp.Length - 10, 10)), 16) + "").Substring(0, 8);
            return "web" + part1 + part2;
        }

        /// <summary>
        /// 生成在 i 西电所有请求所需的签名。
        /// </summary>
        /// <param name="param">要签名的 param 对象。</param>
        /// <returns>表示签名的字符串。</returns>
        public string GetSign(JObject param)
        {
            return Md5Encrypt32(JsonToQuery(new JObject(param.Properties().OrderBy(p => p.Name))));
        }

        /// <summary>
        /// 生成 32 位大写 MD5。
        /// </summary>
        /// <param name="input">要加密的内容。</param>
        /// <returns>MD5 字符串。</returns>
        private static string Md5Encrypt32(string input)
        {
            MD5 md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();
            foreach (byte t in data)
                sBuilder.Append(t.ToString("x2"));

            return sBuilder.ToString();
        }

        /// <summary>
        /// 构造请求参数。
        /// </summary>
        /// <param name="param">请求参数中的 param 对象。</param>
        /// <param name="specificSchool">是否在请求参数中加入 schoolId。</param>
        /// <param name="acceptSecure">是否对请求参数中 param 对象进行加密。</param>
        /// <returns>构造的请求参数字符串。</returns>
        public string BuildQuery(JObject param, bool specificSchool = false, bool acceptSecure = false)
        {
            JObject queryPost = new JObject
            {
                {"appKey", AppKey},
                {"param", param.ToString(Formatting.None)},
                {"time", GetTimestamp()},
                {"secure", specificSchool ? 1 : 0}
            };
            if (acceptSecure)
                queryPost.Add("acceptSecure", "aes");
            if (specificSchool)
                queryPost.Add("schoolId", SchoolId);
            string sign = GetSign(queryPost);
            queryPost.Add("sign", sign);
            // TODO: Add encryption method when acceptSecure it true
            return queryPost.ToString(Formatting.None);
        }

        /// <summary>
        /// 转换 <see cref="JObject"/> 对象为 GET 请求参数字符串。不进行 URL 编码。
        /// </summary>
        /// <param name="param">要转换的 <see cref="JObject"/> 对象</param>
        /// <returns>GET 请求参数字符串。</returns>
        public string JsonToQuery(JObject param)
        {
            return string.Join("&",
                param.Children().Cast<JProperty>()
                    .Select(jp => jp.Name + "=" + jp.Value.ToString()));
        }
    }
}
