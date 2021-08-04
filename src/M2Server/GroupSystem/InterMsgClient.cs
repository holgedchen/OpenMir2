using System;
using System.Threading;
using SystemModule;
using SystemModule.Sockets.AsyncSocketClient;
using SystemModule.Sockets.Event;

namespace M2Server
{
    public class TFrmMsgClient
    {
        private int dw2D4Tick = 0;
        private string sRecvMsg = string.Empty;
        private readonly IClientScoket MsgClient;
        private Timer _heartTimer;

        public TFrmMsgClient()
        {
            MsgClient = new IClientScoket();
            MsgClient.OnConnected += MsgClientConnect;
            MsgClient.ReceivedDatagram += MsgClientRead;
            MsgClient.OnError += MsgClientError;
            MsgClient.OnDisconnected += MsgClientDisconnected;
        }

        public void ConnectMsgServer()
        {
            MsgClient.Address = M2Share.g_Config.sMsgSrvAddr;
            MsgClient.Port = M2Share.g_Config.nMsgSrvPort;
            dw2D4Tick = HUtil32.GetTickCount();
            MsgClient.Connect();
            _heartTimer = new Timer(Connected, null, 1000, 3000);
        }

        public void Run()
        {
            if (MsgClient.IsConnected)
            {
                DecodeSocStr();
            }
            else
            {
                if (HUtil32.GetTickCount() - dw2D4Tick > 20 * 1000)
                {
                    dw2D4Tick = HUtil32.GetTickCount();
                }
            }
        }

        private void Connected(object obj)
        {
            if (MsgClient.IsConnected)
            {
                return;
            }
            MsgClient.Connect();
        }

        private bool IsConnect()
        {
            return MsgClient.IsConnected;
        }

        private void DecodeSocStr()
        {
            string BufStr = string.Empty;
            string Str = string.Empty;
            string sNumStr = string.Empty;
            string Head = string.Empty;
            string Body = string.Empty;
            int Ident;
            int sNum;
            const string sExceptionMsg = "[Exception] TFrmSrvMsg::DecodeSocStr";
            if (sRecvMsg.IndexOf(')') <= 0)
            {
                return;
            }
            try
            {
                BufStr = sRecvMsg;
                sRecvMsg = string.Empty;
                while (BufStr.IndexOf(')') > 0)
                {
                    BufStr = HUtil32.ArrestStringEx(BufStr, "(", ")", ref Str);
                    if (!string.IsNullOrEmpty(Str))
                    {
                        Body = HUtil32.GetValidStr3(Str, ref Head, "/");
                        Body = HUtil32.GetValidStr3(Body, ref sNumStr, "/");
                        Ident = HUtil32.Str_ToInt(Head, 0);
                        sNum = HUtil32.Str_ToInt(sNumStr, -1);
                        M2Share.GroupServer.ProcessData(Ident, sNum, Body);
                    }
                    else
                    {
                        break;
                    }
                }
                sRecvMsg = BufStr + sRecvMsg;
            }
            catch (Exception ex)
            {
                M2Share.ErrorMessage(sExceptionMsg);
                M2Share.ErrorMessage(ex.StackTrace);
            }
        }

        public void SendSocket(string sMsg)
        {
            if (IsConnect())
            {
                var buff = System.Text.Encoding.GetEncoding("gb2312").GetBytes("(" + sMsg + ")");
                MsgClient.Send(buff);
            }
        }

        private void MsgClientConnect(object sender, DSCClientConnectedEventArgs e)
        {
            M2Share.MainOutMessage("连接主服务器(" + e.RemoteAddress + ':' + e.RemotePort + ")成功...");
        }

        private void MsgClientError(object sender, DSCClientErrorEventArgs e)
        {
            M2Share.MainOutMessage("无法连接主服务器(" + MsgClient.Address + ':' + MsgClient.Port + ")...");
        }

        private void MsgClientDisconnected(object sender, DSCClientConnectedEventArgs e)
        {
            M2Share.ErrorMessage("节点服务器(" + e.RemoteAddress + ':' + e.RemotePort + ")断开连接...");
        }

        private void MsgClientRead(object sender, DSCClientDataInEventArgs e)
        {
            sRecvMsg = sRecvMsg + e.Data;
        }
    }
}

namespace M2Server
{
    public class InterMsgClient
    {
        private static TFrmMsgClient instance = null;

        public static TFrmMsgClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new TFrmMsgClient();
                }
                return instance;
            }
        }
    }
}