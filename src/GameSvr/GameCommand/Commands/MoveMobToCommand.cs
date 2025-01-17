﻿using GameSvr.Actor;
using GameSvr.Player;
using SystemModule.Data;

namespace GameSvr.GameCommand.Commands
{
    /// <summary>
    /// 将指定坐标的怪物移动到新坐标，名称为ALL则移动该坐标所有怪物
    /// </summary>
    [Command("MoveMobTo", "将指定坐标的怪物移动到新坐标", "怪物名称 原地图 原X 原Y 新地图 新X 新Y", 10)]
    public class MoveMobToCommand : Command
    {
        [ExecuteCommand]
        public void MoveMobTo(string[] @Params, PlayObject PlayObject)
        {
            if (@Params == null)
            {
                return;
            }
            var sMonName = @Params[0];
            var OleMap = @Params[1];
            var NewMap = @Params[2];
            var nX = @Params[3] == null ? (short)0 : System.Convert.ToInt16(@Params[3]);
            var nY = @Params[4] == null ? (short)0 : System.Convert.ToInt16(@Params[4]);
            var OnX = @Params[5] == null ? (short)0 : System.Convert.ToInt16(@Params[5]);
            var OnY = @Params[6] == null ? (short)0 : System.Convert.ToInt16(@Params[6]);
            BaseObject MoveMon;
            if (sMonName == "" || OleMap == "" || NewMap == "" || sMonName != "" && sMonName[0] == '?')
            {
                PlayObject.SysMsg(GameCommand.ShowHelp, MsgColor.Red, MsgType.Hint);
                return;
            }
            var boMoveAll = false;
            if (sMonName == "ALL")
            {
                boMoveAll = true;
            }
            if (nX < 0)
            {
                nX = 0;
            }
            if (nY < 0)
            {
                nY = 0;
            }
            if (OnX < 0)
            {
                OnX = 0;
            }
            if (OnY < 0)
            {
                OnY = 0;
            }
            var SrcEnvir = M2Share.MapMgr.FindMap(OleMap);// 原地图
            var DenEnvir = M2Share.MapMgr.FindMap(NewMap);// 新地图
            if (SrcEnvir == null || DenEnvir == null)
            {
                return;
            }
            IList<BaseObject> MonList = new List<BaseObject>();
            if (!boMoveAll)// 指定名称的怪移动
            {
                M2Share.WorldEngine.GetMapRangeMonster(SrcEnvir, OnX, OnY, 10, MonList);// 查指定XY范围内的怪
                if (MonList.Count > 0)
                {
                    for (var i = 0; i < MonList.Count; i++)
                    {
                        MoveMon = MonList[i];
                        if (MoveMon != PlayObject)
                        {
                            if (string.Compare(MoveMon.ChrName, sMonName, StringComparison.OrdinalIgnoreCase) == 0) // 是否是指定名称的怪
                            {
                                MoveMon.SpaceMove(NewMap, nX, nY, 0);
                            }
                        }
                    }
                }
            }
            else
            {
                // 所有怪移动
                M2Share.WorldEngine.GetMapRangeMonster(SrcEnvir, OnX, OnY, 1000, MonList);// 查指定XY范围内的怪
                for (var i = 0; i < MonList.Count; i++)
                {
                    MoveMon = MonList[i];
                    if (MoveMon != PlayObject)
                    {
                        MoveMon.SpaceMove(NewMap, nX, nY, 0);
                    }
                }
            }
        }
    }
}