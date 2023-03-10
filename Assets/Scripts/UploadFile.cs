using System;
using System.IO;
using System.Net;
using System.Text;

public class UploadFile
{
    public static string HttpUploadFile(string url, string path)
    {
        //设置参数
        HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
        CookieContainer cookiect = new CookieContainer();
        request.CookieContainer = cookiect;
        request.Method = "POST";
        request.AllowAutoRedirect = true;
        string boundary = DateTime.Now.Ticks.ToString("X");   //随机分隔线
        request.ContentType = "multipart/form-data;charset=utf-8;boundary=" + boundary;
        byte[] itemBoundaryBtyes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "\r\n");
        byte[] endBoundaryBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

        int pos = path.LastIndexOf("\\");
        string fileName = path.Substring(pos + 1);

        //请求头部信息
        StringBuilder sbheader = new StringBuilder(string.Format("Content-Disposition:form-data;name=\"file\";filename=\"{0}\"\r\nContent-Type;application/octet-stream\r\n\r\n", fileName));
        byte[] postHeaderBytes = Encoding.UTF8.GetBytes(sbheader.ToString());

        FileStream fs = new FileStream(path, FileMode.Open,FileAccess.Read);
        byte[] bArr = new byte[fs.Length];
        fs.Read(bArr,0 ,bArr.Length);
        fs.Close();

        Stream postStream = request.GetRequestStream();
        postStream.Write(itemBoundaryBtyes,0,itemBoundaryBtyes.Length);
        postStream.Write(postHeaderBytes, 0, postHeaderBytes.Length);
        postStream.Write(bArr,0,bArr.Length);
        postStream.Write(endBoundaryBytes,0,endBoundaryBytes.Length);
        postStream.Close();

        //发送请求并获取相应回应数据
        HttpWebResponse response = request.GetResponse() as HttpWebResponse;
        //直到request.GetResponse()程序才开始向目标网页发送Post请求
        Stream instream = response.GetResponseStream();
        StreamReader sr = new StreamReader(instream, Encoding.UTF8);
        //返回结果网页(html)代码
        string content = sr.ReadToEnd();
        return content;
    }
}
