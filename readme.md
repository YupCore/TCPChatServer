C# message exchange server, with handshakes for authentication and a room system. Usage(unity):

```cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.IO.Compression;
using UnityEngine.SceneManagement;

public struct HandshakeStruct
{
    public long clientUID; // no use at time
    public float gameVersion; // used for my game version, you can delete it here and on server
    public string passwordHash; // hash from password, not very secure lol)
    public string roomName; //name says it
    public HandshakeStruct(long uid,float ver,string hash, string roomName)
    {
        this.clientUID = uid;
        this.gameVersion = ver;
        this.passwordHash = hash;  
        this.roomName = roomName;
    }
};

public class ChatController : MonoBehaviour
{
    public TMPro.TMP_InputField chatInput;
    public TMPro.TMP_InputField chat;
    public static ChatController instance;
    public GameObject chatCanvas;
    public bool inRoom = false;

    TcpClient theClient;
    bool hndsSuccesful = false;

    const float maxTimer = 4f;
    float timer = maxTimer;

    private string localChat = "";

    private void Awake() // Some singleton stuff, don't look much at it
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static string GetMD5FromString(string toGet) // Used for auth
    {
        using (var md5 = MD5.Create())
        {
            var hashB = md5.ComputeHash(Encoding.UTF8.GetBytes(toGet));
            return BitConverter.ToString(hashB);
        }
    }

    public void Connect(HandshakeStruct structH) // write your struct
    {
        if(theClient == null)
        {
            theClient = new TcpClient("yourdomain or ip", 1420);
            string conStr = "HANDSHAKE>"; //Separator that indicates a handshake

            HandshakeStruct hs = structH;
            conStr += JsonConvert.SerializeObject(hs); // Serializing a handshake using Newtonsoft.Json
            byte[] hndsB = Encoding.UTF8.GetBytes(conStr); // converting all to bytes
            List<byte> vs = new List<byte>(0);
            for (int i = 0; i < 1024; i++) // for fixed size of 1KB
            {
                vs.Add(255);
            }

            for (int i = 0; i < hndsB.Length; i++)
            {
                vs[i] = hndsB[i];
            }

            theClient.GetStream().Write(vs.ToArray(), 0, vs.Count);
            inRoom = true;
            chatInput.gameObject.SetActive(true);
            Debug.Log("Connected");
        }
    }

    // Update is called once per frame
    async void Update()
    {
        if (theClient != null)
        {
            try
            {
                byte[] buf = new byte[1024];
                await theClient.GetStream().ReadAsync(buf, 0, buf.Length); // Fetching new messages from other clients

                if (buf.Length > 0)
                {
                    List<byte> respBytes = new List<byte>();
                    foreach (var b in buf)
                    {
                        if (b != 255)
                        {
                            respBytes.Add(b);
                        }
                    }

                    string respString = Encoding.UTF8.GetString(respBytes.ToArray());
                    if (respString.Contains("HANDSHAKE SUCCESSFUL") && !hndsSuccesful)
                    {
                        hndsSuccesful = true;
                    }
                    if (string.IsNullOrEmpty(respString) || string.IsNullOrWhiteSpace(respString))
                    {
                        return;
                    }
                    localChat += respString + "\r\n";
                    timer = maxTimer;
                    chatCanvas.SetActive(true);
                    chat.text = localChat;
                }
            }
            catch
            {
                if(theClient != null)
                {
                    theClient.Close();
                    theClient = null;
                }
            }
        }
    }
    private void LateUpdate()
    {
        if(inRoom)
        {
            if (Input.GetKeyDown(KeyCode.T) && !chatInput.isFocused)
            {
                timer = maxTimer;
                if (SceneManager.GetActiveScene().name != "MainMeni")
                {
                    if (!chatInput.gameObject.activeInHierarchy)
                    {
                        chatCanvas.SetActive(!chatInput.gameObject.activeInHierarchy);
                        chatInput.gameObject.SetActive(!chatInput.gameObject.activeInHierarchy);
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        chatCanvas.SetActive(!chatInput.gameObject.activeInHierarchy);
                        chatInput.gameObject.SetActive(!chatInput.gameObject.activeInHierarchy);
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
                else
                {
                    chatCanvas.SetActive(!chatInput.gameObject.activeInHierarchy);
                    chatInput.gameObject.SetActive(!chatInput.gameObject.activeInHierarchy);
                }
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                timer = maxTimer;
                if (chatCanvas.activeInHierarchy)
                {
                    WriteMessage(chatInput);
                    chatInput.text = "";
                }
            }
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
            }
            else if (!chatInput.isFocused)
            {
                chatInput.gameObject.SetActive(false);
                chatCanvas.SetActive(false);
                timer = maxTimer;
            }
        }
        else
        {
            chatInput.gameObject.SetActive(false);
            chatCanvas.SetActive(false);
        }
    }

    public void WriteMessage(TMPro.TMP_InputField infield)
    {
        string message = "[" + Photon.Pun.PhotonNetwork.LocalPlayer.NickName + "]" + infield.text; //Formatting with nick ex. [Amogus]Sssss
        if(theClient != null)
        {
            if (string.IsNullOrEmpty(infield.text) || string.IsNullOrWhiteSpace(infield.text) || message.Contains("STPPOINT")) 
            {
                return;
            }
            byte[] msbody = Encoding.UTF8.GetBytes(message); //encode the message
            List<byte> vs = new List<byte>(1024);
            for (int i = 0; i < 1024; i++)
            {
                vs.Add(255); // used as null characters, theese guys(ÿ) will be igonred so dont write them! Idk why i choose them you can change it
            }

            for (int i = 0; i < msbody.Length; i++)
            {
                vs[i] = msbody[i];
            }

            theClient.GetStream().Write(vs.ToArray(), 0, vs.Count);

            byte[] msbody2 = Encoding.UTF8.GetBytes("STPPOINT"); // STP POINT is just like end character of message, it is written after every message
            List<byte> vs2 = new List<byte>(1024);
            for (int i = 0; i < 1024; i++)
            {
                vs2.Add(255);
            }

            for (int i = 0; i < msbody2.Length; i++)
            {
                vs2[i] = msbody2[i];
            }

            theClient.GetStream().Write(vs2.ToArray(), 0, vs2.Count);
        }
    }
    public async void Disconnect()
    {
        if (theClient != null)
        {
            chatCanvas.SetActive(false);
            localChat = "";
            inRoom = false;
            await Task.Delay(100);
            theClient.Close();
            theClient = null;
        }
    }
    private void OnApplicationQuit()
    {
        if(theClient != null)
        {
            theClient.Close();
            theClient = null;
        }
    }
}

```
