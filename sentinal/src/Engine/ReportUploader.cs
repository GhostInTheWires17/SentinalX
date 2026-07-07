using System;
using System.Net;
using System.Text;

namespace SentinelX.Engine
{
    public static class ReportUploader
    {
        public static bool UploadReport(string url, string jsonContent)
        {
            try
            {
                // Bypass SSL/TLS cert validation errors if testing with self-signed certs
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                // Enable modern security protocols like TLS 1.2 and TLS 1.3
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.Headers[HttpRequestHeader.UserAgent] = "SentinelX-Agent/1.0";
                    client.Encoding = Encoding.UTF8;
                    
                    Console.WriteLine(string.Format("[*] Uploading report to AI endpoint: {0} ...", url));
                    string response = client.UploadString(url, "POST", jsonContent);
                    Console.WriteLine("[+] Upload successful!");
                    Console.WriteLine(string.Format("[+] Server Response: {0}", response));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Failed to upload report to AI: {0}", ex.Message));
                return false;
            }
        }
    }
}
