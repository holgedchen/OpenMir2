﻿using GameSvr.Player;
using GameSvr.Script;
using SystemModule;
using SystemModule.Data;
using SystemModule.Packets.ClientPackets;

namespace GameSvr.Npc
{
    /// <summary>
    /// 行会NPC类
    /// 行会管理NPC 如：比奇国王
    /// </summary>
    public class GuildOfficial : NormNpc
    {
        public GuildOfficial() : base()
        {
            RaceImg = Grobal2.RCC_MERCHANT;
            Appr = 8;
        }

        public override void Click(PlayObject PlayObject)
        {
            base.Click(PlayObject);
        }

        protected override void GetVariableText(PlayObject PlayObject, ref string sMsg, string sVariable)
        {
            base.GetVariableText(PlayObject, ref sMsg, sVariable);
            if (sVariable == "$REQUESTCASTLELIST")
            {
                var sText = "";
                IList<string> List = new List<string>();
                M2Share.CastleMgr.GetCastleNameList(List);
                for (var i = 0; i < List.Count; i++)
                {
                    sText = sText + Format("<{0}/@requestcastlewarnow{1}> {2}", List[i], i, sText);
                }
                sText = sText + "\\ \\";
                sMsg = ReplaceVariableText(sMsg, "<$REQUESTCASTLELIST>", sText);
            }
        }

        public override void Run()
        {
            if (M2Share.RandomNumber.Random(40) == 0)
            {
                TurnTo(M2Share.RandomNumber.RandomByte(8));
            }
            else
            {
                if (M2Share.RandomNumber.Random(30) == 0)
                {
                    SendRefMsg(Grobal2.RM_HIT, Direction, CurrX, CurrY, 0, "");
                }
            }
            base.Run();
        }

        public override void UserSelect(PlayObject PlayObject, string sData)
        {
            var sLabel = string.Empty;
            const string sExceptionMsg = "[Exception] TGuildOfficial::UserSelect... ";
            base.UserSelect(PlayObject, sData);
            try
            {
                if (!string.IsNullOrEmpty(sData) && sData.StartsWith("@"))
                {
                    string sMsg = HUtil32.GetValidStr3(sData, ref sLabel, "\r");
                    var boCanJmp = PlayObject.LableIsCanJmp(sLabel);
                    GotoLable(PlayObject, sLabel, !boCanJmp);
                    if (!boCanJmp)
                    {
                        return;
                    }
                    if (string.Compare(sLabel, ScriptConst.sBUILDGUILDNOW, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        ReQuestBuildGuild(PlayObject, sMsg);
                    }
                    else if (string.Compare(sLabel, ScriptConst.sSCL_GUILDWAR, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        ReQuestGuildWar(PlayObject, sMsg);
                    }
                    else if (string.Compare(sLabel, ScriptConst.sDONATE, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        DoNate(PlayObject);
                    }
                    else if (HUtil32.CompareLStr(sLabel, ScriptConst.sREQUESTCASTLEWAR))
                    {
                        ReQuestCastleWar(PlayObject, sLabel.Substring(ScriptConst.sREQUESTCASTLEWAR.Length, sLabel.Length - ScriptConst.sREQUESTCASTLEWAR.Length));
                    }
                    else if (string.Compare(sLabel, ScriptConst.sEXIT, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        PlayObject.SendMsg(this, Grobal2.RM_MERCHANTDLGCLOSE, 0, ActorId, 0, 0, "");
                    }
                    else if (string.Compare(sLabel, ScriptConst.sBACK, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (PlayObject.m_sScriptGoBackLable == "")
                        {
                            PlayObject.m_sScriptGoBackLable = ScriptConst.sMAIN;
                        }
                        GotoLable(PlayObject, PlayObject.m_sScriptGoBackLable, false);
                    }
                }
            }
            catch
            {
                M2Share.Log.LogError(sExceptionMsg);
            }
        }

        /// <summary>
        /// 请求建立行会
        /// </summary>
        /// <param name="PlayObject"></param>
        /// <param name="sGuildName"></param>
        /// <returns></returns>
        private int ReQuestBuildGuild(PlayObject PlayObject, string sGuildName)
        {
            var result = 0;
            sGuildName = sGuildName.Trim();
            UserItem UserItem = null;
            if (sGuildName == "")
            {
                result = -4;
            }
            if (PlayObject.MyGuild == null)
            {
                if (PlayObject.Gold >= M2Share.Config.BuildGuildPrice)
                {
                    UserItem = PlayObject.CheckItems(M2Share.Config.WomaHorn);
                    if (UserItem == null)
                    {
                        result = -3;// '你没有准备好需要的全部物品。'
                    }
                }
                else
                {
                    result = -2;// '缺少创建费用。'
                }
            }
            else
            {
                result = -1;// '您已经加入其它行会。'
            }
            if (result == 0)
            {
                if (M2Share.GuildMgr.AddGuild(sGuildName, PlayObject.ChrName))
                {
                    M2Share.WorldEngine.SendServerGroupMsg(Grobal2.SS_205, M2Share.ServerIndex, sGuildName + '/' + PlayObject.ChrName);
                    PlayObject.SendDelItems(UserItem);
                    PlayObject.DelBagItem(UserItem.MakeIndex, M2Share.Config.WomaHorn);
                    PlayObject.DecGold(M2Share.Config.BuildGuildPrice);
                    PlayObject.GoldChanged();
                    PlayObject.MyGuild = M2Share.GuildMgr.MemberOfGuild(PlayObject.ChrName);
                    if (PlayObject.MyGuild != null)
                    {
                        PlayObject.GuildRankName = PlayObject.MyGuild.GetRankName(PlayObject, ref PlayObject.GuildRankNo);
                        RefShowName();
                    }
                }
                else
                {
                    result = -4;
                }
            }
            if (result >= 0)
            {
                PlayObject.SendMsg(this, Grobal2.RM_BUILDGUILD_OK, 0, 0, 0, 0, "");
            }
            else
            {
                PlayObject.SendMsg(this, Grobal2.RM_BUILDGUILD_FAIL, 0, result, 0, 0, "");
            }
            return result;
        }

        /// <summary>
        /// 请求行会战争
        /// </summary>
        /// <param name="PlayObject"></param>
        /// <param name="sGuildName"></param>
        /// <returns></returns>
        private void ReQuestGuildWar(PlayObject PlayObject, string sGuildName)
        {
            if (M2Share.GuildMgr.FindGuild(sGuildName) != null)
            {
                if (PlayObject.Gold >= M2Share.Config.GuildWarPrice)
                {
                    PlayObject.DecGold(M2Share.Config.GuildWarPrice);
                    PlayObject.GoldChanged();
                    PlayObject.ReQuestGuildWar(sGuildName);
                }
                else
                {
                    PlayObject.SysMsg("你没有足够的金币!!!", MsgColor.Red, MsgType.Hint);
                }
            }
            else
            {
                PlayObject.SysMsg("行会 " + sGuildName + " 不存在!!!", MsgColor.Red, MsgType.Hint);
            }
        }

        private void DoNate(PlayObject PlayObject)
        {
            PlayObject.SendMsg(this, Grobal2.RM_DONATE_OK, 0, 0, 0, 0, "");
        }

        private void ReQuestCastleWar(PlayObject PlayObject, string sIndex)
        {
            var nIndex = HUtil32.StrToInt(sIndex, -1);
            if (nIndex < 0)
            {
                nIndex = 0;
            }
            var Castle = M2Share.CastleMgr.GetCastle(nIndex);
            if (PlayObject.IsGuildMaster() && !Castle.IsMember(PlayObject))
            {
                var UserItem = PlayObject.CheckItems(M2Share.Config.ZumaPiece);
                if (UserItem != null)
                {
                    if (Castle.AddAttackerInfo(PlayObject.MyGuild))
                    {
                        PlayObject.SendDelItems(UserItem);
                        PlayObject.DelBagItem(UserItem.MakeIndex, M2Share.Config.ZumaPiece);
                        GotoLable(PlayObject, "~@request_ok", false);
                    }
                    else
                    {
                        PlayObject.SysMsg("你现在无法请求攻城!!!", MsgColor.Red, MsgType.Hint);
                    }
                }
                else
                {
                    PlayObject.SysMsg("你没有" + M2Share.Config.ZumaPiece + "!!!", MsgColor.Red, MsgType.Hint);
                }
            }
            else
            {
                PlayObject.SysMsg("你的请求被取消!!!", MsgColor.Red, MsgType.Hint);
            }
        }

        protected override void SendCustemMsg(PlayObject PlayObject, string sMsg)
        {
            base.SendCustemMsg(PlayObject, sMsg);
        }
    }
}

