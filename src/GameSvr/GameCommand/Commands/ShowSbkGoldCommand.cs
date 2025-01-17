﻿using GameSvr.Player;
using SystemModule;
using SystemModule.Data;

namespace GameSvr.GameCommand.Commands
{
    /// <summary>
    /// 显示沙巴克收入金币
    /// </summary>
    [Command("ShowSbkGold", "显示沙巴克收入金币", 10)]
    public class ShowSbkGoldCommand : Command
    {
        [ExecuteCommand]
        public void ShowSbkGold(string[] @Params, PlayObject PlayObject)
        {
            if (@Params == null)
            {
                return;
            }
            var sCastleName = @Params.Length > 0 ? @Params[0] : "";
            var sCtr = @Params.Length > 1 ? @Params[1] : "";
            var sGold = @Params.Length > 2 ? @Params[2] : "";
            if (sCastleName != "" && sCastleName[0] == '?')
            {
                PlayObject.SysMsg(string.Format(CommandHelp.GameCommandParamUnKnow, this.GameCommand.Name, ""), MsgColor.Red, MsgType.Hint);
                return;
            }
            if (sCastleName == "")
            {
                IList<string> List = new List<string>();
                M2Share.CastleMgr.GetCastleGoldInfo(List);
                for (var i = 0; i < List.Count; i++)
                {
                    PlayObject.SysMsg(List[i], MsgColor.Green, MsgType.Hint);
                }

                return;
            }
            var Castle = M2Share.CastleMgr.Find(sCastleName);
            if (Castle == null)
            {
                PlayObject.SysMsg(string.Format(CommandHelp.GameCommandSbkGoldCastleNotFoundMsg, sCastleName), MsgColor.Red, MsgType.Hint);
                return;
            }
            var Ctr = sCtr[1];
            var nGold = HUtil32.StrToInt(sGold, -1);
            if (!new List<char>(new char[] { '=', '-', '+' }).Contains(Ctr) || nGold < 0 || nGold > 100000000)
            {
                PlayObject.SysMsg(string.Format(CommandHelp.GameCommandParamUnKnow, this.GameCommand.Name, CommandHelp.GameCommandSbkGoldHelpMsg), MsgColor.Red, MsgType.Hint);
                return;
            }
            switch (Ctr)
            {
                case '=':
                    Castle.TotalGold = nGold;
                    break;
                case '-':
                    Castle.TotalGold -= 1;
                    break;
                case '+':
                    Castle.TotalGold += nGold;
                    break;
            }
            if (Castle.TotalGold < 0)
            {
                Castle.TotalGold = 0;
            }
        }
    }
}