using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XduCAS;

namespace XduCASDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginIds_Click(object sender, RoutedEventArgs e)
        {
            Ids ids = new Ids
            (
                Username.Text,
                Password.Password,
                "http%3A%2F%2Fehall.xidian.edu.cn%2Flogin%3Fservice%3Dhttp%3A%2F%2Fehall.xidian.edu.cn%2Fnew%2Findex.html"
            );
            HttpClient hc = ids.Login(out Image captchaImage);
            if (captchaImage == null)
            {
                hc.DefaultRequestHeaders.Referrer = new Uri("http://ehall.xidian.edu.cn/new/index.html");
                hc.DefaultRequestHeaders.Connection.Add("keep-alive");
                hc.DefaultRequestHeaders.ExpectContinue = false;
                hc.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                hc.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                string jsonReturn = hc.GetStringAsync("http://ehall.xidian.edu.cn/jsonp/userDesktopInfo.json?type=&_=" +
                                                      ids.GetTimestamp()).Result;
                JObject jsonLogin = (JObject) JsonConvert.DeserializeObject(jsonReturn);
                MessageBox.Show(
                    $"登录成功。\n\n姓名: {jsonLogin["userName"]}\n性别: {jsonLogin["userSex"]}\n学院: {jsonLogin["userDepartment"]}");
            }
        }

        private void LoginXduApp_Click(object sender, RoutedEventArgs e)
        {
            XduApp ixd = new XduApp
            (
                Username.Text,
                Password.Password
            );
            HttpClient hc = ixd.Login();
            JObject param = new JObject
            {
                {"userId", ixd.UserId}
            };
            string strParam = ixd.BuildQuery(param);
            hc.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            hc.DefaultRequestHeaders.Add("Accept-Language", "cn");
            if (!hc.DefaultRequestHeaders.Contains("Connection"))
                hc.DefaultRequestHeaders.Connection.Add("keep-alive");
            string jsonUserInfoReturn = hc.PostAsync("http://202.117.121.7:8080/baseCampus/user/getUserInfo.do", new StringContent(strParam, Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;
            JObject jsonUserInfo = (JObject) JsonConvert.DeserializeObject(jsonUserInfoReturn);
            MessageBox.Show($"登录成功。\n\n姓名: {jsonUserInfo["userBaseInfo"]["realName"]}\n\n学院: {jsonUserInfo["userBaseInfo"]["collegeName"]}");
        }
    }
}