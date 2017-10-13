using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace SimpleHttp
{
    public enum ReferenceHold : byte
    {
        WithScene = 0,
        WaitForComplete = 1
    }

    public enum HttpMethod : byte
    {
        GET = 0,
        POST = 1,
        DOWNLOAD = 2,
        UPLOAD = 3
    }

    public enum HttpFileExistOption
    {
        Replace = 0,
        Cancel = 2
    }

    public delegate void ProgressCallback(ulong current, ulong total, float progress);
    public delegate void FinishedCallback(ulong downloaded);

    public class HttpDownloadHandler : DownloadHandlerScript
    {

        private FileStream mFileStream;
        private ProgressCallback mDownloadCallback;
        private FinishedCallback mFinishedCallback;

        public HttpDownloadHandler(FileStream fileStream, ProgressCallback downloadCallback, FinishedCallback finishedCallback)
        {
            mFileStream = fileStream;
            mDownloadCallback = downloadCallback;
            mFinishedCallback = finishedCallback;
        }

        private ulong totalLength = 0;
        private ulong downloadedLength = 0;
        protected override void CompleteContent()
        {
            if (null != mFinishedCallback)
            {
                mFinishedCallback(totalLength);
            }
        }

        protected override float GetProgress()
        {
            return (float)downloadedLength / (float)totalLength;
        }

        protected override void ReceiveContentLength(int contentLength)
        {
            totalLength = (ulong)contentLength;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            Debug.Log("ReceiveData: " + dataLength);
            downloadedLength += (ulong)dataLength;
            mFileStream.Write(data, 0, dataLength);
            if (null != mDownloadCallback)
            {
                mDownloadCallback(downloadedLength, totalLength, GetProgress());
            }
            return true;
        }
    }

    /// <summary>
    /// HttpSender is a dummy class added to run coroutines on temporary GameObjects
    /// </summary>
    public class HttpSender : MonoBehaviour
    {
        public string GUID = "";
        public HttpMethod Method = HttpMethod.GET;
        public string Url = "";
        public string Data = "";
        public int Timeout = 10;
        public System.Action<string, string, HttpSender> Completion;
        public ReferenceHold RefHold = ReferenceHold.WaitForComplete;
        public ProgressCallback mProgressCallback;
        public FinishedCallback mFinishedCallback;
        public string FilePath = "";
        public HttpFileExistOption ExistOption = HttpFileExistOption.Replace;

        public void Send()
        {
            switch (RefHold)
            {
                case ReferenceHold.WaitForComplete:
                    DontDestroyOnLoad(this);
                    break;
                default:
                    break;
            }

            switch (Method)
            {
                case HttpMethod.GET:
                    StartCoroutine(Get());
                    break;
                case HttpMethod.POST:
                    StartCoroutine(Post());
                    break;
                case HttpMethod.DOWNLOAD:
                    StartCoroutine(Download());
                    break;
            }
        }

        IEnumerator Get()
        {
            var url_ = Url;
            url_ = url_.TrimEnd('&');
            url_ = url_.TrimEnd('?');

            if (Data != null && Data != "")
            {
                if (url_.IndexOf('?') == -1)
                {
                    url_ += "?";
                    url_ += Data;
                }
                else
                {
                    url_ = url_.TrimEnd('&');
                    url_ += "&" + Data.Trim('&');
                }
            }

            UnityWebRequest www = UnityWebRequest.Get(url_);
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            www.timeout = Timeout;
            yield return www.Send();

            if (www.isNetworkError || www.isHttpError)
            {
                if (Completion != null)
                {
                    Completion(null, www.error, this);
                }
            }
            else
            {
                if (Completion != null)
                {
                    Completion(www.downloadHandler.text, null, this);
                }
            }

            Destroy(this.gameObject);
        }

        IEnumerator Post()
        {
            UnityWebRequest www = UnityWebRequest.Post(Url, Data);
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            www.timeout = Timeout;
            yield return www.Send();

            if (www.isNetworkError || www.isHttpError)
            {
                if (Completion != null)
                {
                    Completion(null, www.error, this);
                }
            }
            else
            {
                if (Completion != null)
                {
                    Completion(www.downloadHandler.text, null, this);
                }
            }

            Destroy(this.gameObject);
        }

        IEnumerator Download()
        {
            var placePath = Path.Combine(Application.persistentDataPath, FilePath);

            if (File.Exists(placePath))
            {
                switch (ExistOption)
                {
                    case HttpFileExistOption.Cancel:
                        yield break;
                    default:
                        File.Delete(placePath);
                        break;
                }
            }
            else
            {
                string placeFolder = Path.GetDirectoryName(placePath);

                if (!Directory.Exists(placeFolder))
                {
                    Directory.CreateDirectory(placeFolder);
                }
            }

            string tempFilePath = Path.Combine(Application.temporaryCachePath, FilePath + ".tmp");

            string tmpFolder = Path.GetDirectoryName(tempFilePath);

            if (!Directory.Exists(tmpFolder))
            {
                Directory.CreateDirectory(tmpFolder);
            }

            FileInfo tempFileInfo = new FileInfo(tempFilePath);

            FileStream fileStream = File.Open(tempFilePath, tempFileInfo.Exists ? FileMode.Append : FileMode.CreateNew);
            UnityWebRequest request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbGET, new HttpDownloadHandler(fileStream, mProgressCallback, null), null);

            if (tempFileInfo.Exists)
            {
                request.SetRequestHeader("RANGE", string.Format("bytes={0}-", tempFileInfo.Length));
            }

            yield return request.Send();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
            }
            else if (request.isDone)
            {
                fileStream.Close();
                File.Move(tempFilePath, placePath);
                File.Delete(tempFilePath);
                if (mFinishedCallback != null)
                {
                    mFinishedCallback(request.downloadedBytes);
                }
            }

            Destroy(this.gameObject);
        }
    }

    public class Http
    {
        const int defaultTimeout = 10;
        const ReferenceHold defaultRefHold = ReferenceHold.WaitForComplete;
        static Http _instance = null;
        public static Http Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Http();
                }
                return _instance;
            }
        }

        Dictionary<string, HttpSender> _senders;

        Http()
        {
            _senders = new Dictionary<string, HttpSender>();
        }

        string attachSender(
            string Url_,
            string Data_,
            HttpMethod Method_,
            int Timeout_,
            ReferenceHold RefHold_,
            System.Action<string, string> Completion_)
        {
            var guid = System.Guid.NewGuid().ToString();

            GameObject go = new GameObject("HttpSender(" + Method_.ToString() + ")" + guid);
            var sender = go.AddComponent<HttpSender>();
            sender.GUID = guid;
            sender.Url = Url_;
            sender.Data = Data_;
            sender.Method = Method_;
            sender.Timeout = Timeout_;
            sender.RefHold = RefHold_;
            sender.Completion = (string result_, string errMsg_, HttpSender sender_) =>
            {
                _senders.Remove(sender_.GUID);
                if (Completion_ != null)
                {
                    Completion_(result_, errMsg_);
                }
            };
            if (!_senders.ContainsKey(guid))
            {
                _senders.Add(guid, sender);
            }
            sender.Send();

            return guid;
        }

        public static string Send(
            string Url_,
            string Data_,
            HttpMethod Method_,
            int Timeout_,
            ReferenceHold RefHold_,
            System.Action<string, string> Completion_)
        {
            return Instance.attachSender(Url_, Data_, Method_, Timeout_, RefHold_, Completion_);
        }

        public static string Get(string URL_, string Data_, System.Action<string, string> CallBack_ = null)
        {
            return Instance.attachSender(URL_, Data_, HttpMethod.GET, defaultTimeout, defaultRefHold, CallBack_);
        }

        public static string Post(string URL_, string Data_, System.Action<string, string> CallBack_ = null)
        {
            return Instance.attachSender(URL_, Data_, HttpMethod.POST, defaultTimeout, defaultRefHold, CallBack_);
        }

        public static string Download(string URL_, string ToPath_, HttpFileExistOption existOpt, ProgressCallback progressCallback, FinishedCallback finishedCallback)
        {
            var guid = System.Guid.NewGuid().ToString();

            GameObject go = new GameObject("HttpSender(" + HttpMethod.DOWNLOAD.ToString() + ")" + guid);
            var sender = go.AddComponent<HttpSender>();
            sender.GUID = guid;
            sender.Url = URL_;
            sender.Data = null;
            sender.Method = HttpMethod.DOWNLOAD;
            sender.Timeout = 60;
            sender.RefHold = ReferenceHold.WaitForComplete;
            sender.FilePath = ToPath_;
            sender.mProgressCallback = progressCallback;
            sender.mFinishedCallback = finishedCallback;
            sender.ExistOption = existOpt;
            sender.Completion = null;
            if (!Instance._senders.ContainsKey(guid))
            {
                Instance._senders.Add(guid, sender);
            }
            sender.Send();

            return guid;
        }

        public static void Cancel(string guid)
        {
            if (Instance._senders.ContainsKey(guid))
            {
                var sender = Instance._senders[guid];
                Object.Destroy(sender.gameObject);
                Instance._senders.Remove(guid);
            }
        }

        public static void CancelAll()
        {
            var keys = Instance._senders.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                var guid = keys[i];
                var sender = Instance._senders[guid];
                Object.Destroy(sender.gameObject);
                Instance._senders.Remove(guid);
            }
        }
    }
}
