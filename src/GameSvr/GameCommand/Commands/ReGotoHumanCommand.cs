﻿using GameSvr.Player;
using SystemModule.Data;

namespace GameSvr.GameCommand.Commands
{
    /// <summary>
    /// 飞到指定玩家身边
    /// </summary>
    [Command("ReGotoHuman", "飞到指定玩家身边", CommandHelp.GameCommandReGotoHelpMsg, 10)]
    public class ReGotoHumanCommand : Command
    {
        [ExecuteCommand]
        public void ReGotoHuman(string[] @Params, PlayObject PlayObject)
        {
            if (@Params == null)
            {
                return;
            }
            var sHumanName = @Params.Length > 0 ? @Params[0] : "";
            if (string.IsNullOrEmpty(sHumanName) || !string.IsNullOrEmpty(sHumanName) && sHumanName[1] == '?')
            {
                PlayObject.SysMsg(GameCommand.ShowHelp, MsgColor.Red, MsgType.Hint);
                return;
            }
            var m_PlayObject = M2Share.WorldEngine.GetPlayObject(sHumanName);
            if (m_PlayObject == null)
            {
                PlayObject.SysMsg(string.Format(CommandHelp.NowNotOnLineOrOnOtherServer, sHumanName), MsgColor.Red, MsgType.Hint);
                return;
            }
            PlayObject.SpaceMove(m_PlayObject.Envir.MapName, m_PlayObject.CurrX, m_PlayObject.CurrY, 0);
        }
    }
}