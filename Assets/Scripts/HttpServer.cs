using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

public class HttpServer : MonoBehaviour
{
    public class DataMes
    {
        public string ip { get; set; }
        public bool isConnected { get; set; }
        public bool istakesimple { get; set; }
        public string filename { get; set; }
    }
    private Assembly editor;
    private System.Type type;
    private string current_Ip = string.Empty;
    public DataMes Message;
    public void Start()
    {
        IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in ipEntry.AddressList)
        {
            current_Ip = ip.ToString();
        }
        Message = new DataMes();
        editor = Assembly.Load("Assembly-CSharp-Editor");
        type = editor.GetType("ExtractMemoryInfo");
        //连接游戏
        type.GetMethod("ConnectGamesAndOpenProfiler").Invoke(null, new object[] { Message.ip });
        //执行TakeSample
        type.GetMethod("TakeSimple").Invoke(null, new object[] { });
        //执行写内存数据文件
        type.GetMethod("ExtractMemoryDetailed2").Invoke(null, new object[] { Message.filename });
    }
    public void Update()
    {
    }
}
