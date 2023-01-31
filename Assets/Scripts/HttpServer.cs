using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
/// <summary>
/// 内部状态码
/// </summary>
enum ETM_Runstate
{
    Check = 0,
    ConnetGame = 1,
    TakeSample = 2,
    WriteFile = 3,
    WaitForConncect = 4,
    WaitForTakingSample = 5,
    WaitForWriting = 6,
    DisConnectGame = 7,
}
public class HttpServer : MonoBehaviour
{
    public class DataMes
    {
        public string ip { get; set; }
        public bool isConnected { get; set; }
        public bool istakesimple { get; set; }
        public string filename { get; set; }
        public bool canWriteFile { get; set; }
    }
    private Assembly editor;
    private System.Type type;
    private string current_Ip = string.Empty;
    private DataMes Message;
    private static Thread subthread;    //HTTP监听线程
    private static Thread uploadthread;  //上传文件线程
    private float duration = 0;
    private bool isUpload = false;
    private string uploadurl = "http://memorycomparer.console.testplus.cn/v1/api/report/uploadFile";
    private ETM_Runstate m_CurrentState = ETM_Runstate.Check;
    public void Start()
    {
        IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in ipEntry.AddressList)
        {
            current_Ip = ip.ToString();
        }
        //打开Profiler窗口
        EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
        Message = new DataMes();
        Message.ip = current_Ip;
        editor = Assembly.Load("Assembly-CSharp-Editor");
        type = editor.GetType("ExtractMemoryInfo");
        subthread = new Thread(HttpListenerInit);  //启用一个线程进行监听操作
        uploadthread = new Thread(CheckFile);//启用一个线程进行上传文件操作
        uploadthread.Start();
        subthread.Start();
    }
    public void Update()
    {
        if (m_CurrentState == ETM_Runstate.Check)
        {
            //Check EveryState
            Manager();
        }
        else if (m_CurrentState == ETM_Runstate.WaitForConncect)
        {
            CheckConnect();
        }
        else if (m_CurrentState == ETM_Runstate.WaitForTakingSample)
        {
            CheckTakeSample();
        }
        else if (m_CurrentState == ETM_Runstate.WaitForWriting)
        {
            CheckWriting();
        }
        else if (m_CurrentState == ETM_Runstate.ConnetGame)
        {
            ConnectGame();
            m_CurrentState = ETM_Runstate.WaitForConncect;
        }
        else if (m_CurrentState == ETM_Runstate.TakeSample)
        {
            type.GetMethod("TakeSimple").Invoke(null, null);
            m_CurrentState = ETM_Runstate.WaitForTakingSample;
        }
        else if (m_CurrentState == ETM_Runstate.WriteFile)
        {
            m_CurrentState = ETM_Runstate.WaitForWriting;
        }
        else if(m_CurrentState == ETM_Runstate.DisConnectGame)
        {
            DisConnect();
        }
    }

    /// <summary>
    /// 检查各个状态进行转换
    /// </summary>
    private void Manager()
    {
        if (Message.isConnected && Message.istakesimple)
        {
            m_CurrentState = ETM_Runstate.TakeSample;
        }
        else if (ProfilerDriver.connectedProfiler == -1 && Message.isConnected)
        {
            //由于某种原因退出了连接，再次重新连接
            ConnectGame();
        }
    }

    private void DisConnect()
    {
        ProfilerDriver.DirectIPConnect("");
        ProfilerDriver.ClearAllFrames();
        Message.isConnected = false;
        m_CurrentState = ETM_Runstate.Check;
        UnityEngine.Debug.Log("断开游戏连接----");
    }

    private void ConnectGame()
    {
        ProfilerDriver.DirectIPConnect(Message.ip);
    }

    /// <summary>
    /// 等待写入文件是否成功
    /// </summary>
    private void CheckWriting()
    {
        //等待
        duration += 0.02f;
        if (duration > 20)
        {
            //由于时序问题，在此等待一段时间再进行写入文件
            type.GetMethod("ExtractMemoryDetailedByFileName").Invoke(null, new object[] { Message.filename });
            Message.canWriteFile = false;
            m_CurrentState = ETM_Runstate.Check;
            isUpload = true;   //开启文件上传
            duration = 0;
        }
        else
        {
            //wait
        }
    }

    /// <summary>
    /// 检查是否成功获取到内存
    /// </summary>
    private void CheckTakeSample()
    {
        //等待
        duration += 0.02f;
        if (duration > 50)
        {
            UnityEngine.Debug.Log("Take Sample成功----");
            //等待5秒钟成功刷新完内存后,代表可以进行写文件的操作
            Message.istakesimple = false;
            Message.canWriteFile = true;
            m_CurrentState = ETM_Runstate.WriteFile;
            duration = 0;
        }
        else
        {
            //wait
        }
    }

    /// <summary>
    /// 检查连接游戏是否成功
    /// </summary>
    private void CheckConnect()
    {
        if (ProfilerDriver.connectedProfiler != -1)
        {
            //成功连接完毕,代表可以进行takesameple操作了
            Message.isConnected = true;
            m_CurrentState = ETM_Runstate.Check;
            UnityEngine.Debug.Log("连接游戏成功----");
        }
        else
        {
            //waiting connect
        }
    }

    /// <summary>
    /// 启动Http监听
    /// </summary>
    private void HttpListenerInit()
    {
        using (HttpListener listerner = new HttpListener())
        {
            listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listerner.Prefixes.Add(string.Format("http://{0}:9044/getmemory/", current_Ip));
            listerner.Start();
            AddFireWallPort(9044);
            UnityEngine.Debug.Log("Server Start Successed.......");
            while (true)
            {
                //程序到此会阻断状态，等待Client连接
                HttpListenerContext context = listerner.GetContext();

                HttpListenerRequest request = context.Request;
                context.Response.StatusCode = 200;
                string filename = context.Request.QueryString["filename"];
                string connect = context.Request.QueryString["connectgame"];
                string postData = new StreamReader(request.InputStream).ReadToEnd();
                string result = string.Empty;
                result = "Faile";
                if (!string.IsNullOrEmpty(filename))
                {
                    Message.filename = filename;
                    Message.istakesimple = true;
                    m_CurrentState = ETM_Runstate.TakeSample;
                    result = "Success";
                }
                else if (!string.IsNullOrEmpty(connect))
                {
                    if(connect == "1")
                    {
                        Message.isConnected = false;
                        Message.istakesimple = false;   //先不进行TakeSample，等待下一个消息进来
                        m_CurrentState = ETM_Runstate.ConnetGame;
                        result = "Success";
                    }
                    else if(connect == "0")
                    {
                        m_CurrentState = ETM_Runstate.DisConnectGame;
                        result = "Success";
                    }
                }
                //foreach (var item in request.QueryString)
                //{
                //    UnityEngine.Debug.Log("Query: {0}"+ item);
                //}

                UnityEngine.Debug.Log("收到http请求：" + request.RawUrl);

                //UnityEngine.Debug.Log("URL: {0}"+ request.Url.OriginalString);
                //UnityEngine.Debug.Log("Raw URL: {0}"+ request.RawUrl);
                //UnityEngine.Debug.Log("Referred by: {0}"+ request.UrlReferrer);
                //UnityEngine.Debug.Log("HTTP Method: {0}"+ request.HttpMethod);
                //UnityEngine.Debug.Log("Host name: {0}"+ request.UserHostName);
                //UnityEngine.Debug.Log("Host address: {0}"+ request.UserHostAddress);
                //UnityEngine.Debug.Log("User agent: {0}"+ request.UserAgent);
                //使用Writer输出http响应代码
                using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                {
                    var reply = new { code = 200, result = result };
                    writer.WriteLine(reply);

                    writer.Close();
                    context.Response.Close();
                }
            }
        }
    }
    /// <summary>
    /// 设置防火墙
    /// </summary>
    /// <param name="port"></param>
    private static void AddFireWallPort(int port)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo("netsh", $"firewall set portopening TCP {port.ToString()} ENABLE");
        processStartInfo.Verb = "runas";
        processStartInfo.CreateNoWindow = true;
        processStartInfo.UseShellExecute = false;
        processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(processStartInfo).WaitForExit();
    }

    /// <summary>
    /// 确认是否可以进行上传文件
    /// </summary>
    private void CheckFile()
    {
        while (true)
        {
            if (isUpload)
            {
                string file = Message.filename + ".txt";
                string filePath = string.Format("{0}\\{1}\\{2}", System.Environment.CurrentDirectory, "MemoryDetailed", file);
                UploadFile.HttpUploadFile(uploadurl, filePath);
                UnityEngine.Debug.Log(Message.filename + "文件上传完成----");
                isUpload = false;   //关闭文件上传
            }
            else
            {
                //wait
            }
        }
    }
}
