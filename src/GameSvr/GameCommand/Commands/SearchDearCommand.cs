﻿using GameSvr.Player;
using SystemModule.Data;

namespace GameSvr.GameCommand.Commands
{
    /// <summary>
    /// 此命令用于查询配偶当前所在位置
    /// </summary>
    [Command("SearchDear", "此命令用于查询配偶当前所在位置", 0)]
    public class SearchDearCommand : Command
    {
        [ExecuteCommand]
        public void SearchDear(PlayObject PlayObject)
        {
            if (PlayObject.m_sDearName == "")
            {
                // '你都没结婚查什么？'
                PlayObject.SysMsg(M2Share.g_sYouAreNotMarryedMsg, MsgColor.Red, MsgType.Hint);
                return;
            }

            if (PlayObject.m_DearHuman == null)
            {
                if (PlayObject.Gender == 0)
                {
                    // '你的老婆还没有上线!!!'
                    PlayObject.SysMsg(M2Share.g_sYourWifeNotOnlineMsg, MsgColor.Red, MsgType.Hint);
                }
                else
                {
                    // '你的老公还没有上线!!!'
                    PlayObject.SysMsg(M2Share.g_sYourHusbandNotOnlineMsg, MsgColor.Red, MsgType.Hint);
                }

                return;
            }

            if (PlayObject.Gender == 0)
            {
                // '你的老婆现在位于:'
                PlayObject.SysMsg(M2Share.g_sYourWifeNowLocateMsg, MsgColor.Green, MsgType.Hint);
                PlayObject.SysMsg(PlayObject.m_DearHuman.ChrName + ' ' + PlayObject.m_DearHuman.Envir.MapDesc +
                                  '(' + PlayObject.m_DearHuman.CurrX + ':'
                                  + PlayObject.m_DearHuman.CurrY + ')', MsgColor.Green, MsgType.Hint);

                // '你的老公正在找你，他现在位于:'
                PlayObject.m_DearHuman.SysMsg(M2Share.g_sYourHusbandSearchLocateMsg, MsgColor.Green, MsgType.Hint);
                PlayObject.m_DearHuman.SysMsg(PlayObject.ChrName + ' ' + PlayObject.Envir.MapDesc + '(' +
                                              PlayObject.CurrX + ':'
                                              + PlayObject.CurrY + ')', MsgColor.Green, MsgType.Hint);
            }
            else
            {
                // '你的老公现在位于:'
                PlayObject.SysMsg(M2Share.g_sYourHusbandNowLocateMsg, MsgColor.Red, MsgType.Hint);
                PlayObject.SysMsg(PlayObject.m_DearHuman.ChrName + ' ' + PlayObject.m_DearHuman.Envir.MapDesc +
                                  '(' + PlayObject.m_DearHuman.CurrX + ':'
                                  + PlayObject.m_DearHuman.CurrY + ')', MsgColor.Green, MsgType.Hint);

                // '你的老婆正在找你，她现在位于:'
                PlayObject.m_DearHuman.SysMsg(M2Share.g_sYourWifeSearchLocateMsg, MsgColor.Green, MsgType.Hint);
                PlayObject.m_DearHuman.SysMsg(PlayObject.ChrName + ' ' + PlayObject.Envir.MapDesc + '(' +
                                              PlayObject.CurrX + ':'
                                              + PlayObject.CurrY + ')', MsgColor.Green, MsgType.Hint);
            }
        }
    }
}