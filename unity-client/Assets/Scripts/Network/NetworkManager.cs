using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [SerializeField] private string baseUrl = "https://47.251.170.164:8443/api";
    [SerializeField] private string wsUrl = "wss://47.251.170.164:8443/ws";
    [SerializeField] private float requestTimeout = 10f;

    private string _authToken = "";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IEnumerator Get(string path, Action<bool, string> callback)
    {
        string url = baseUrl + path;
        using UnityWebRequest req = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(_authToken))
            req.SetRequestHeader("Authorization", "Bearer " + _authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        bool ok = req.result == UnityWebRequest.Result.Success;
        callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
    }

    public IEnumerator Post(string path, string jsonBody, Action<bool, string> callback)
    {
        string url = baseUrl + path;
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(_authToken))
            req.SetRequestHeader("Authorization", "Bearer " + _authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        bool ok = req.result == UnityWebRequest.Result.Success;
        callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
    }

    public void SetAuthToken(string token) { _authToken = token; }
}
