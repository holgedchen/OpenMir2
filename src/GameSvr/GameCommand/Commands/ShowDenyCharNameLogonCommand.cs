﻿using GameSvr.Player;
using SystemModule.Data;

namespace GameSvr.GameCommand.Commands
{
    [Command("ShowDenyChrNameLogon", "", 10)]
    public class ShowDenyChrNameLogonCommand : Command
    {
        [ExecuteCommand]
        public void ShowDenyChrNameLogon(PlayObject PlayObject)
        {
            try
            {
                if (M2Share.g_DenyChrNameList.Count <= 0)
                {
                    PlayObject.SysMsg("禁止登录角色列表为空。", MsgColor.Green, MsgType.Hint);
                    return;
                }
                for (var i = 0; i < M2Share.g_DenyChrNameList.Count; i++)
                {
                    //PlayObject.SysMsg(M2Share.g_DenyChrNameList[i], TMsgColor.c_Green, TMsgType.t_Hint);
                }
            }
            finally
            {
            }
        }
    }
}