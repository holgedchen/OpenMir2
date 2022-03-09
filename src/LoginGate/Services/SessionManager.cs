using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using LoginGate.Package;

namespace LoginGate.Services
{
    public class SessionManager
    {
        /// <summary>
        /// 发送封包（网关-》客户端）
        /// </summary>
        private readonly Channel<TMessageData> _sendQueue = null;
        private readonly ConcurrentDictionary<int, ClientSession> _connectionSessions;

        public SessionManager()
        {
            _connectionSessions = new ConcurrentDictionary<int, ClientSession>();
            _sendQueue = Channel.CreateUnbounded<TMessageData>();
        }

        public ChannelWriter<TMessageData> SendQueue => _sendQueue.Writer;

        /// <summary>
        /// 处理DBSvr发送过来的消息
        /// </summary>
        public async Task ProcessSendMessage()
        {
            while (await _sendQueue.Reader.WaitToReadAsync())
            {
                if (_sendQueue.Reader.TryRead(out var message))
                {
                    var userSession = GetSession(message.SessionId);
                    if (userSession == null)
                    {
                        return;
                    }
                    userSession.ProcessSvrData(message);
                }
            }
        }

        public void AddSession(int sessionId, ClientSession clientSession)
        {
            _connectionSessions.TryAdd(sessionId, clientSession);
        }

        public void CloseSession(int sessionId)
        {
            if (_connectionSessions.ContainsKey(sessionId))
            {
                _connectionSessions[sessionId].Session.Socket.Close();
            }
            Remove(sessionId);
        }

        public ClientSession GetSession(int sessionId)
        {
            if (_connectionSessions.ContainsKey(sessionId))
            {
                return _connectionSessions[sessionId];
            }
            return null;
        }
        
        public void Remove(int sessionId)
        {
            if (!_connectionSessions.TryRemove(sessionId, out var clientSession))
            {
               Console.WriteLine($"删除用户会话失败:[{sessionId}]");
            }
        }

        public bool CheckSession(int sessionId)
        {
            if (_connectionSessions.ContainsKey(sessionId))
            {
                return true;
            }
            return false;
        }

        public IList<ClientSession> GetAllSession()
        {
            return _connectionSessions.Values.ToList();
        }
    }
}