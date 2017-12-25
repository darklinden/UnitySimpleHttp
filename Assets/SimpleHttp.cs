using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using vag.frame;

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

	public delegate void ProgressCallback (ulong current, ulong total, float progress);
	public delegate void FinishedCallback (string errMsg);

	public class HttpDownloadHandler : DownloadHandlerScript
	{

		private FileStream mFileStream;
		private ProgressCallback mDownloadCallback;
		private FinishedCallback mFinishedCallback;

		public HttpDownloadHandler (FileStream fileStream, ProgressCallback downloadCallback, FinishedCallback finishedCallback)
		{
			mFileStream = fileStream;
			mDownloadCallback = downloadCallback;
			mFinishedCallback = finishedCallback;
		}

		private ulong totalLength = 0;
		private ulong downloadedLength = 0;

		protected override void CompleteContent ()
		{
			if (null != mFinishedCallback) {
				mFinishedCallback (null);
			}
		}

		protected override float GetProgress ()
		{
			return (float)downloadedLength / (float)totalLength;
		}

		protected override void ReceiveContentLength (int contentLength)
		{
			totalLength = (ulong)contentLength;
		}

		protected override bool ReceiveData (byte[] data, int dataLength)
		{
			// Debug.Log("ReceiveData: " + dataLength);
			downloadedLength += (ulong)dataLength;
			mFileStream.Write (data, 0, dataLength);
			if (null != mDownloadCallback) {
				mDownloadCallback (downloadedLength, totalLength, GetProgress ());
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

		public static Int64 timestamp ()
		{
			var timestamp_ = Math.Floor ((DateTime.UtcNow.Subtract (new DateTime (1970, 1, 1))).TotalMilliseconds);
			return (Int64)timestamp_;
		}

		public void Send ()
		{
			switch (RefHold) {
			case ReferenceHold.WaitForComplete:
				DontDestroyOnLoad (this);
				break;
			default:
				break;
			}

			switch (Method) {
			case HttpMethod.GET:
				StartCoroutine (Get ());
				break;
			case HttpMethod.POST:
				StartCoroutine (Post ());
				break;
			case HttpMethod.DOWNLOAD:
				StartCoroutine (Download ());
				break;
			}
		}

		IEnumerator Get ()
		{
			var url_ = Url;
			url_ = url_.TrimEnd ('&');
			url_ = url_.TrimEnd ('?');

			if (Data != null && Data != "") {
				if (url_.IndexOf ('?') == -1) {
					url_ += "?";
					url_ += Data;
				} else {
					url_ = url_.TrimEnd ('&');
					url_ += "&" + Data.Trim ('&');
				}
			}

			if (url_.IndexOf ('?') == -1) {
				url_ += "?";
				url_ += "t" + timestamp () + "=0";
			} else {
				url_ = url_.TrimEnd ('&');
				url_ += "&";
				url_ += "t" + timestamp () + "=0";
			}

			Dictionary<string, string> header = new Dictionary<string, string> ();
			header ["Content-Type"] = "application/x-www-form-urlencoded";
			WWW www = new WWW (url_, null, header);
			float timer = 0;
			bool failed = false;
			while (!www.isDone) {
				if (timer > Timeout) {
					failed = true;
					break; 
				}
				timer += Time.deltaTime;
				yield return null;
			}

			if (failed) {
				Debug.Log ("SimpleHttp Get " + url_ + " timeout");
				if (Completion != null) {
					Completion (null, www.error, this);
				}
				www.Dispose ();
				Destroy (this.gameObject);
			} else if (!string.IsNullOrEmpty (www.error)) {
				Debug.Log ("SimpleHttp Get " + url_ + " failed error: " + www.error);
				if (Completion != null) {
					Completion (null, www.error, this);
				}
				www.Dispose ();
				Destroy (this.gameObject);
			} else if (www.isDone) {
				Debug.Log ("SimpleHttp Get " + url_ + " success: " + www.text);
				if (Completion != null) {
					Completion (www.text, null, this);
				}
				www.Dispose ();
				Destroy (this.gameObject);
			} else {
				yield return null;
			}
		}

		IEnumerator Post ()
		{
			var url_ = Url;
			if (url_.IndexOf ('?') == -1) {
				url_ += "?";
				url_ += "t" + timestamp () + "=0";
			} else {
				url_ = url_.TrimEnd ('&');
				url_ += "&";
				url_ += "t" + timestamp () + "=0";
			}

			UnityWebRequest www = new UnityWebRequest (url_, UnityWebRequest.kHttpVerbPOST, new DownloadHandlerBuffer (), new UploadHandlerRaw (util.str2utf8 (Data)));
			www.SetRequestHeader ("Content-Type", "application/x-www-form-urlencoded");

			www.timeout = Timeout;
			yield return www.Send ();
			if (www.isError) {
				Debug.Log ("SimpleHttp Post " + Url + " data: " + Data + " error: " + www.error);
				if (Completion != null) {
					Completion (null, www.error, this);
				}
				Destroy (this.gameObject);
			} else if (www.isDone) {
				Debug.Log ("SimpleHttp Post " + Url + " data: " + Data + " success: " + www.downloadHandler.text);
				if (Completion != null) {
					Completion (www.downloadHandler.text, null, this);
				}
				Destroy (this.gameObject);
			} else {
				yield return null;
			}
		}

		IEnumerator Download ()
		{
			var placePath = Path.Combine (Application.persistentDataPath, FilePath);

			if (File.Exists (placePath)) {
				switch (ExistOption) {
				case HttpFileExistOption.Cancel:
					yield break;
				default:
					File.Delete (placePath);
					break;
				}
			} else {
				string placeFolder = Path.GetDirectoryName (placePath);

				if (!Directory.Exists (placeFolder)) {
					Directory.CreateDirectory (placeFolder);
				}
			}

			var guid = System.Guid.NewGuid ().ToString ().Replace ("-", "");
			string tempFilePath = Path.Combine (Application.temporaryCachePath, FilePath + ".tmp" + "-" + guid);

			string tmpFolder = Path.GetDirectoryName (tempFilePath);

			if (!Directory.Exists (tmpFolder)) {
				Directory.CreateDirectory (tmpFolder);
			}

			FileInfo tempFileInfo = new FileInfo (tempFilePath);

			FileStream fileStream = File.Open (tempFilePath, tempFileInfo.Exists ? FileMode.Append : FileMode.CreateNew);

			var url_ = Url;
			if (url_.IndexOf ('?') == -1) {
				url_ += "?";
				url_ += "t" + timestamp () + "=0";
			} else {
				url_ = url_.TrimEnd ('&');
				url_ += "&";
				url_ += "t" + timestamp () + "=0";
			}
			UnityWebRequest request = new UnityWebRequest (url_, UnityWebRequest.kHttpVerbGET, new HttpDownloadHandler (fileStream, mProgressCallback, null), null);

			if (tempFileInfo.Exists) {
				request.SetRequestHeader ("RANGE", string.Format ("bytes={0}-", tempFileInfo.Length));
			}

			yield return request.Send ();

			if (request.isError) {
				Debug.Log ("SimpleHttp Download " + Url + " error: " + request.error);
				mFinishedCallback (request.error);
				Destroy (this.gameObject);
			} else if (request.isDone) {
				Debug.Log ("SimpleHttp Download " + Url + " success");
				fileStream.Close ();
				try {
					File.Move (tempFilePath, placePath);
					File.Delete (tempFilePath);
					if (mFinishedCallback != null) {
						mFinishedCallback (null);
					}
				} catch (System.Exception e) {
					if (mFinishedCallback != null) {
						mFinishedCallback (e.ToString ());
					}	
				}
				Destroy (this.gameObject);
			} else {
				yield return null;
			}
		}
	}

	public class Http
	{
		const int defaultTimeout = 10;
		const ReferenceHold defaultRefHold = ReferenceHold.WaitForComplete;
		static Http _instance = null;

		public static Http Instance {
			get {
				if (_instance == null) {
					_instance = new Http ();
				}
				return _instance;
			}
		}

		Dictionary<string, HttpSender> _senders;

		Http ()
		{
			_senders = new Dictionary<string, HttpSender> ();
		}

		string attachSender (
			string Url_,
			string Data_,
			HttpMethod Method_,
			int Timeout_,
			ReferenceHold RefHold_,
			System.Action<string, string> Completion_)
		{
			var guid = System.Guid.NewGuid ().ToString ();

			GameObject go = new GameObject ("HttpSender(" + Method_.ToString () + ")" + guid);
			var sender = go.AddComponent<HttpSender> ();
			sender.GUID = guid;
			sender.Url = Url_;
			sender.Data = Data_;
			sender.Method = Method_;
			sender.Timeout = Timeout_;
			sender.RefHold = RefHold_;
			sender.Completion = (string result_, string errMsg_, HttpSender sender_) => {
				_senders.Remove (sender_.GUID);
				if (Completion_ != null) {
					Completion_ (result_, errMsg_);
				}
			};
			if (!_senders.ContainsKey (guid)) {
				_senders.Add (guid, sender);
			}
			sender.Send ();

			return guid;
		}

		public static string Send (
			string Url_,
			string Data_,
			HttpMethod Method_,
			int Timeout_,
			ReferenceHold RefHold_,
			System.Action<string, string> Completion_)
		{
			return Instance.attachSender (Url_, Data_, Method_, Timeout_, RefHold_, Completion_);
		}

		public static string Get (string URL_, string Data_, System.Action<string, string> CallBack_ = null)
		{
			return Instance.attachSender (URL_, Data_, HttpMethod.GET, defaultTimeout, defaultRefHold, CallBack_);
		}

		public static string Post (string URL_, string Data_, System.Action<string, string> CallBack_ = null)
		{
			return Instance.attachSender (URL_, Data_, HttpMethod.POST, defaultTimeout, defaultRefHold, CallBack_);
		}

		public static string Download (string URL_, string ToPath_, HttpFileExistOption existOpt, ProgressCallback progressCallback, FinishedCallback finishedCallback)
		{
			var guid = System.Guid.NewGuid ().ToString ();

			GameObject go = new GameObject ("HttpSender(" + HttpMethod.DOWNLOAD.ToString () + ")" + guid);
			var sender = go.AddComponent<HttpSender> ();
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
			if (!Instance._senders.ContainsKey (guid)) {
				Instance._senders.Add (guid, sender);
			}
			sender.Send ();

			return guid;
		}

		public static void Cancel (string guid)
		{
			if (Instance._senders.ContainsKey (guid)) {
				var sender = Instance._senders [guid];
				UnityEngine.Object.Destroy (sender.gameObject);
				Instance._senders.Remove (guid);
			}
		}

		public static void CancelAll ()
		{
			var keys = Instance._senders.Keys.ToArray ();
			for (int i = 0; i < keys.Length; i++) {
				var guid = keys [i];
				var sender = Instance._senders [guid];
				UnityEngine.Object.Destroy (sender.gameObject);
				Instance._senders.Remove (guid);
			}
		}
	}
}