﻿using GameGate.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemModule;

namespace GameGate
{
    public class TimedService : BackgroundService
    {
        private readonly ILogger<TimedService> _logger;
        private static MirLog LogQueue => MirLog.Instance;
        private static ClientManager ClientManager => ClientManager.Instance;
        private static SessionManager SessionManager => SessionManager.Instance;
        private static ServerManager ServerManager => ServerManager.Instance;
        private int ProcessDelayTick { get; set; }
        private int ProcessDelayCloseTick { get; set; }
        private int ProcessClearSessionTick { get; set; }
        private int CheckServerConnectTick { get; set; }
        private int KepAliveTick { get; set; }

        private readonly PeriodicTimer _periodicTimer;

        public TimedService(ILogger<TimedService> logger)
        {
            _logger = logger;
            KepAliveTick = HUtil32.GetTickCount();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var startTick = HUtil32.GetTickCount();
            ProcessDelayTick = startTick;
            ProcessDelayCloseTick = startTick;
            ProcessClearSessionTick = startTick;
            KepAliveTick = startTick;
            CheckServerConnectTick = startTick;
            while (await _periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                var currentTick = HUtil32.GetTickCount();
                OutMianMessage();
                ProcessDelayMsg(currentTick);
                ClearIdleSession(currentTick);
                KeepAlive(currentTick);
                ProcessDelayClose(currentTick);
            }
        }

        private void OutMianMessage()
        {
            if (!GateShare.ShowLog)
                return;

            while (!LogQueue.MessageLogQueue.IsEmpty)
            {
                string message;
                if (!LogQueue.MessageLogQueue.TryDequeue(out message)) continue;
                _logger.LogInformation(message);
            }

            while (!LogQueue.DebugLogQueue.IsEmpty)
            {
                string message;
                if (!LogQueue.DebugLogQueue.TryDequeue(out message)) continue;
                _logger.LogDebug(message);
            }
        }

        /// <summary>
        /// GameGate->GameSvr 心跳
        /// </summary>
        private void KeepAlive(int currentTick)
        {
            if (currentTick - CheckServerConnectTick > 10000)
            {
                CheckServerConnectTick = HUtil32.GetTickCount();
                var clientList = ClientManager.GetClients();
                if (clientList.Count == 0)
                {
                    return;
                }
                var clients = clientList.ToArray();
                for (var i = 0; i < clients.Length; i++)
                {
                    if (clients[i] == null)
                    {
                        continue;
                    }
                    clients[i].CheckConnectedState();
                }
            }
        }

        /// <summary>
        /// 处理会话延时消息
        /// </summary>
        private void ProcessDelayMsg(int currentTick)
        {
            if (currentTick - ProcessDelayTick > 200)
            {
                ProcessDelayTick = currentTick;
                var sessionList = SessionManager.GetSessions();
                if (sessionList.Count == 0)
                {
                    return;
                }
                var sessions = sessionList.ToArray();
                for (var i = 0; i < sessions.Length; i++)
                {
                    var clientSession = sessions[i];
                    if (clientSession?.Session?.Socket == null || !clientSession.Session.Socket.Connected)
                    {
                        continue;
                    }
                    clientSession.ProcessDelayMessage();
                }
            }
        }

        private void ProcessDelayClose(int currentTick)
        {
            if (currentTick - ProcessDelayCloseTick > 4000)
            {
                ProcessDelayCloseTick = HUtil32.GetTickCount();
                var serverList = ServerManager.GetServerList();
                for (var i = 0; i < serverList.Length; i++)
                {
                    if (serverList[i] == null)
                    {
                        continue;
                    }
                    serverList[i].CloseWaitList();
                }
            }
        }

        /// <summary>
        /// 清理过期会话
        /// </summary>
        private void ClearIdleSession(int currentTick)
        {
            if (currentTick - ProcessClearSessionTick > 120000)
            {
                ProcessClearSessionTick = HUtil32.GetTickCount();
                var clientList = ClientManager.GetClients();
                if (clientList.Count == 0)
                {
                    return;
                }
                var clients = clientList.ToArray();
                for (var i = 0; i < clients.Length; i++)
                {
                    if (clients[i] == null)
                    {
                        continue;
                    }
                    clients[i].ProcessIdleSession();
                }
                LogQueue.DebugLog("清理超时无效会话...");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}