﻿using SystemModule;
using GameSvr.CommandSystem;

namespace GameSvr
{
    /// <summary>
    /// 调整指定玩家转生等级
    /// </summary>
    [GameCommand("ReNewLevel", "调整指定玩家转生等级", 10)]
    public class ReNewLevelCommand : BaseCommond
    {
        [DefaultCommand]
        public void ReNewLevel(string[] @Params, TPlayObject PlayObject)
        {
            var sHumanName = @Params.Length > 0 ? @Params[0] : "";
            var sLevel = @Params.Length > 1 ? @Params[1] : "";
            if (PlayObject.m_btPermission < 6)
            {
                return;
            }
            if (sHumanName == "" || sHumanName != "" && sHumanName[0] == '?')
            {
                PlayObject.SysMsg("命令格式: @" + this.Attributes.Name + " 人物名称 点数(为空则查看)", TMsgColor.c_Red, TMsgType.t_Hint);
                return;
            }
            var nLevel = HUtil32.Str_ToInt(sLevel, -1);
            var m_PlayObject = M2Share.UserEngine.GetPlayObject(sHumanName);
            if (m_PlayObject != null)
            {
                if (nLevel >= 0 && nLevel <= 255)
                {
                    m_PlayObject.m_btReLevel = (byte)nLevel;
                    m_PlayObject.RefShowName();
                }
                PlayObject.SysMsg(sHumanName + " 的转生等级为 " + PlayObject.m_btReLevel, TMsgColor.c_Green, TMsgType.t_Hint);
            }
            else
            {
                PlayObject.SysMsg(sHumanName + " 没在线上！！！", TMsgColor.c_Red, TMsgType.t_Hint);
            }
        }
    }
}