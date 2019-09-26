using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;

namespace XduCAS
{
    /// <summary>
    /// 提供登录统一身份认证系统通用方法的类。
    /// </summary>
    public class Ids
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
        /// <para>获取或设置回调地址。接受原始 URI 或编码后的 URI。</para>
        /// <para>如果提供编码后的 URI，仅接受经过一次编码的 URI。</para>
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// 获取或设置登录使用的 <see cref="T:System.Net.Http.HttpClient"/> 类的 Referer 标头。
        /// </summary>
        public string Referrer { get; set; }

        /// <summary>
        /// 统一身份认证系统基础 URI。
        /// </summary>
        private readonly Uri _baseUri = new Uri("http://ids.xidian.edu.cn/authserver/login");

        /// <summary>
        /// 使用指定的登录信息初始化 <see cref="Ids"/> 类。
        /// </summary>
        /// <param name="id">用于登录的学号。</param>
        /// <param name="password">用于登录的密码。</param>
        /// <param name="redirectUri">回调地址。接受原始 URI 或编码后的 URI。</param>
        /// <param name="referrer">
        /// <para>登录使用的 <see cref="T:System.Net.Http.HttpClient"/> 类的 Referer 标头。</para>
        /// <para>如果不指定，则为统一身份认证系统默认标头。</para>
        /// </param>
        public Ids(string id, string password, string redirectUri, string referrer = "http://ids.xidian.edu.cn")
        {
            Id = id;
            Password = password;
            RedirectUri = redirectUri;
            Referrer = referrer;
        }

        /// <summary>
        /// <para>使用设置的学号和密码进行统一身份认证系统登录。如果提供验证码，则一并使用。</para>
        /// <para>返回包含 Cookies 等信息的 <see cref="T:System.Net.Http.HttpClient"/> 类。</para>
        /// </summary>
        /// <param name="captchaImage">
        /// 当此方法返回时，如果登录成功，则为 <see langword="null" />；
        /// 如果登录需要验证码，则为表示验证码图像的 <see cref="T:System.Drawing.Image" /> 类。
        /// </param>
        /// <param name="captcha">登录时使用的验证码。如果参数为空，则登录时不使用验证码。</param>
        /// <param name="proxyAddress">登录时使用的代理地址。如果参数为空，则登录时不使用代理。</param>
        /// <exception cref="FormatException">登录信息格式不正确。</exception>
        /// <exception cref="ArgumentException">密码不能为空。</exception>
        /// <exception cref="Exception">登录失败。</exception>
        /// <returns>包含 Cookies 等信息的 <see cref="T:System.Net.Http.HttpClient"/> 类。</returns>
        public HttpClient Login(out Image captchaImage, string captcha = "", string proxyAddress = "")
        {
            // Check format of id, password and redirect uri
            if (!long.TryParse(Id, out _) || Id.Length != 11)
                throw new FormatException("学号格式不正确。");
            if (Password == "")
                throw new ArgumentException("密码不能为空。");
            if (!CheckUri(HttpUtility.UrlDecode(RedirectUri)))
                throw new FormatException("回调地址格式不正确。");
            if (!CheckUri(Referrer))
                throw new FormatException("Referer 标头格式不正确。");
            HttpClient hc;
            if (proxyAddress != "")
            {
                var proxy = new WebProxy
                {
                    Address = new Uri(proxyAddress)
                };

                // Now create a client handler which uses that proxy

                var httpClientHandler = new HttpClientHandler
                {
                    Proxy = proxy
                };
                hc = new HttpClient(httpClientHandler, true);
            }
            else
                hc = new HttpClient();

            // Add HttpClient headers
            hc.DefaultRequestHeaders.Add("User-Agent", "okhttp/9.9.9");
            hc.DefaultRequestHeaders.ExpectContinue = false;
            hc.DefaultRequestHeaders.Connection.Add("keep-alive");
            // Build get params
            UriBuilder builder = new UriBuilder(_baseUri);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["service"] = HttpUtility.UrlDecode(RedirectUri);
            builder.Query = query.ToString();
            string loginUri = builder.ToString();
            // Load login page
            string html = hc.GetStringAsync(builder.ToString()).Result;
            // Get extra params
            string lt = Regex.Match(html, @"<input type=""hidden"" name=""lt"" value=""(.*?)""/>")
                .Groups[1].Value;
            string execution = Regex
                .Match(html, @"<input type=""hidden"" name=""execution"" value=""(.*?)""/>").Groups[1].Value;
            string eventId = Regex
                .Match(html, @"<input type=""hidden"" name=""_eventId"" value=""(.*?)""/>").Groups[1].Value;
            string rmShown = Regex.Match(html, @"<input type=""hidden"" name=""rmShown"" value=""(.*?)"">")
                .Groups[1].Value;
            // Set referer header
            hc.DefaultRequestHeaders.Referrer = new Uri(Referrer);
            // Build login params
            List<KeyValuePair<string, string>> @params = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", Id),
                new KeyValuePair<string, string>("password", Password),
                new KeyValuePair<string, string>("submit", ""),
                new KeyValuePair<string, string>("lt", lt),
                new KeyValuePair<string, string>("execution", execution),
                new KeyValuePair<string, string>("_eventId", eventId),
                new KeyValuePair<string, string>("rmShown", rmShown)
            };
            // Add verification code if it is provided
            if (captcha != "")
                @params.Add(new KeyValuePair<string, string>("captchaResponse", captcha));

            string loginReturn = hc.PostAsync(loginUri, new FormUrlEncodedContent(@params)).Result.Content
                .ReadAsStringAsync().Result;

            try
            {
                // Id or password is invalid
                if (loginReturn.Contains("有误"))
                    throw new Exception("学号或密码不正确。");

                // Login successfully
                captchaImage = null;
                // Verification required
                if (hc.GetStringAsync("http://ids.xidian.edu.cn/authserver/needCaptcha.html?username=" + Id + @"&_=" +
                                      GetTimestamp()).Result == "true" || loginReturn.Contains("请输入验证码"))
                {
                    captchaImage =
                        Image.FromStream(hc.GetStreamAsync("http://ids.xidian.edu.cn/authserver/captcha.html").Result);
                    return hc;
                }

                return hc;
            }
            catch (Exception e)
            {
                // Other exception
                throw new Exception($"登录失败。{e.Message}");
            }
        }

        /// <summary>
        /// 检查指定的 URI 是否合法。
        /// </summary>
        /// <param name="uri">要检查的 URI。</param>
        /// <returns>指示 URI 是否合法。</returns>
        private static bool CheckUri(string uri)
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// 获取表示当前时间的 Unix 时间戳。
        /// </summary>
        /// <returns>表示 Unix 时间戳的长整型。</returns>
        public long GetTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
}
